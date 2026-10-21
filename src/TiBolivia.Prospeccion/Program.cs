using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
// Proveedor de WhatsApp parametrizable: "WhatsApp:Proveedor" = "Meta" (por defecto) o "TextMeBot".
if (string.Equals(builder.Configuration["WhatsApp:Proveedor"], ProveedoresWhatsApp.TextMeBot, StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<IEnviadorWhatsApp, EnviadorWhatsAppTextMeBot>();
else
    builder.Services.AddHttpClient<IEnviadorWhatsApp, EnviadorWhatsAppCloud>();
builder.Services.AddHttpClient<IClienteChatbot, ClienteChatbotHttp>();
builder.Services.AddHttpClient<DiagnosticoWhatsApp>();
builder.Services.AddHttpClient<ConfiguradorMeta>();
builder.Services.AddScoped<MotorCampanas>();
builder.Services.AddScoped<ServicioInscripciones>();
builder.Services.AddScoped<LectorRespuestasEmail>();
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
    await DatosIniciales.InicializarAsync(scope.ServiceProvider, app.Configuration);

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
app.MapWhatsAppWebhook();
app.MapTextMeBotWebhook();
app.MapRazorPages().WithStaticAssets();

app.Run();
