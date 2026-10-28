using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;
using TiBolivia.Prospeccion.Webhooks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<Usuario, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Cuenta/Login";
    o.AccessDeniedPath = "/Cuenta/AccesoDenegado";
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToPage("/Cuenta/Login");
    o.Conventions.AllowAnonymousToPage("/Baja");
    o.Conventions.AllowAnonymousToPage("/Privacidad");
    o.Conventions.AllowAnonymousToPage("/Error");
    // Configuración de campañas, plantillas, servicios y usuarios: solo administradores.
    foreach (var carpeta in new[] { "/Campanas", "/Plantillas", "/Servicios", "/Usuarios", "/Configuracion" })
        o.Conventions.AuthorizeFolder(carpeta, "Admin");
});
builder.Services.AddAuthorizationBuilder().AddPolicy("Admin", p => p.RequireRole(Roles.Admin));

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<ImapOptions>(builder.Configuration.GetSection("Imap"));
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.Configure<ChatbotOptions>(builder.Configuration.GetSection("Chatbot"));
builder.Services.Configure<CampanasOptions>(builder.Configuration.GetSection("Campanas"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<HorarioLaboral>();
builder.Services.AddSingleton<RenderizadorPlantillas>();
builder.Services.AddSingleton<IEnviadorEmail, EnviadorEmailSmtp>();
// Proveedor de WhatsApp parametrizable ("Meta" o "TextMeBot"), editable desde la pantalla WhatsApp sin reiniciar.
builder.Services.AddSingleton<AjustesGuardados>();
builder.Services.AddSingleton<IPostConfigureOptions<WhatsAppOptions>, AplicarAjustesGuardados>();
builder.Services.AddSingleton<IPostConfigureOptions<ChatbotOptions>, AplicarAjustesGuardados>();
builder.Services.AddHttpClient<EnviadorWhatsAppCloud>();
builder.Services.AddHttpClient<EnviadorWhatsAppTextMeBot>();
builder.Services.AddScoped<IEnviadorWhatsApp, EnviadorWhatsAppSegunProveedor>();
builder.Services.AddHttpClient<IClienteChatbot, ClienteChatbotHttp>();
builder.Services.AddHttpClient<DiagnosticoWhatsApp>();
builder.Services.AddHttpClient<ConfiguradorMeta>();
builder.Services.AddScoped<MotorCampanas>();
builder.Services.AddScoped<ServicioInscripciones>();
builder.Services.AddScoped<InscripcionAutomatica>();
builder.Services.AddScoped<LectorRespuestasEmail>();
builder.Services.AddScoped<ImportadorProspectos>();
if (builder.Configuration.GetValue("Campanas:WorkerHabilitado", true))
{
    builder.Services.AddHostedService<WorkerCampanas>();
    builder.Services.AddHostedService<WorkerRespuestasEmail>();
}

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// En el servidor (Docker) las claves de sesión se guardan en un volumen: así los usuarios no pierden la sesión al reiniciar.
if (builder.Configuration["DataProtection:Ruta"] is { Length: > 0 } rutaClaves)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(rutaClaves)).SetApplicationName("TiBolivia.Prospeccion");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DatosIniciales.InicializarAsync(scope.ServiceProvider, app.Configuration);
    await app.Services.GetRequiredService<AjustesGuardados>().RecargarAsync();
}

// Consulta de solo lectura para el diagnóstico del servidor: estado del número y de las plantillas en Meta.
if (args.Contains("--plantillas-meta"))
{
    using var scope = app.Services.CreateScope();
    var diag = scope.ServiceProvider.GetRequiredService<DiagnosticoWhatsApp>();
    try
    {
        var n = await diag.ObtenerNumeroAsync(CancellationToken.None);
        Console.WriteLine($"Número: {n.Numero} · Nombre: {n.NombreVerificado} ({n.EstadoNombre}) · Calidad: {n.Calidad} · Límite: {n.LimiteMensajes}");
        var meta = await diag.ListarPlantillasAsync(CancellationToken.None);
        Console.WriteLine($"Plantillas en Meta: {meta.Count}");
        foreach (var t in meta)
            Console.WriteLine($"  {t.Nombre} [{t.Idioma}] {t.Categoria}: {t.Estado}{(string.IsNullOrEmpty(t.Motivo) || t.Motivo == "NONE" ? "" : " · motivo: " + t.Motivo)}");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var usadas = await db.Plantillas.Where(p => p.Canal == Canal.WhatsApp && p.WhatsAppPlantillaMeta != null).ToListAsync();
        Console.WriteLine("Plantillas del sistema que usan una plantilla de Meta:");
        foreach (var p in usadas)
        {
            var m = meta.FirstOrDefault(x => x.Nombre == p.WhatsAppPlantillaMeta && x.Idioma == p.WhatsAppIdioma);
            Console.WriteLine($"  {p.Nombre} -> {p.WhatsAppPlantillaMeta} [{p.WhatsAppIdioma}]: {m?.Estado ?? "NO EXISTE EN META"}");
        }
    }
    catch (Exception ex) { Console.WriteLine("Error consultando Meta: " + ex.Message); }
    return;
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapGet("/health", () => Results.Text("ok")).AllowAnonymous();
app.MapWhatsAppWebhook();
app.MapTextMeBotWebhook();
app.MapRazorPages().WithStaticAssets();

app.Run();
