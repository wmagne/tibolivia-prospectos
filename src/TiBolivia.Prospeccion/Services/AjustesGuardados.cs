using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

/// <summary>
/// Configuración editable desde la interfaz (WhatsApp y chatbot). Tiene prioridad sobre appsettings y variables de entorno,
/// se guarda en la base (los secretos cifrados con Data Protection) y se aplica sin reiniciar.
/// </summary>
public class AjustesGuardados(IServiceScopeFactory scopes, IDataProtectionProvider proteccion)
{
    private readonly IDataProtector _cifrador = proteccion.CreateProtector("TiBolivia.Prospeccion.Ajustes");
    private volatile Dictionary<string, string> _valores = new();

    public string? Obtener(string clave) => _valores.TryGetValue(clave, out var v) ? v : null;
    public bool Tiene(string clave) => _valores.ContainsKey(clave);

    public async Task RecargarAsync()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var nuevos = new Dictionary<string, string>();
        foreach (var a in await db.Ajustes.AsNoTracking().ToListAsync())
        {
            try { nuevos[a.Clave] = a.Cifrado ? _cifrador.Unprotect(a.Valor) : a.Valor; }
            catch (System.Security.Cryptography.CryptographicException) { /* clave de cifrado cambiada: se ignora el valor */ }
        }
        _valores = nuevos;
    }

    /// <summary>Guarda los valores indicados. null = no cambiar; "" = borrar (vuelve al valor de appsettings).</summary>
    public async Task GuardarAsync(IReadOnlyDictionary<string, string?> cambios, ISet<string> secretos)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var (clave, valor) in cambios)
        {
            if (valor is null) continue;
            var existente = await db.Ajustes.FindAsync(clave);
            if (valor.Length == 0)
            {
                if (existente is not null) db.Ajustes.Remove(existente);
                continue;
            }
            existente ??= db.Ajustes.Add(new AjusteGuardado { Clave = clave }).Entity;
            existente.Cifrado = secretos.Contains(clave);
            existente.Valor = existente.Cifrado ? _cifrador.Protect(valor) : valor;
            existente.ActualizadoEn = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        await RecargarAsync();
    }
}

/// <summary>Aplica los ajustes guardados sobre las opciones de WhatsApp y del chatbot.</summary>
public class AplicarAjustesGuardados(AjustesGuardados ajustes) : IPostConfigureOptions<WhatsAppOptions>, IPostConfigureOptions<ChatbotOptions>
{
    public void PostConfigure(string? name, WhatsAppOptions o)
    {
        o.Proveedor = ajustes.Obtener("WhatsApp:Proveedor") ?? o.Proveedor;
        o.PhoneNumberId = ajustes.Obtener("WhatsApp:PhoneNumberId") ?? o.PhoneNumberId;
        o.BusinessAccountId = ajustes.Obtener("WhatsApp:BusinessAccountId") ?? o.BusinessAccountId;
        o.AccessToken = ajustes.Obtener("WhatsApp:AccessToken") ?? o.AccessToken;
        o.AppId = ajustes.Obtener("WhatsApp:AppId") ?? o.AppId;
        o.AppSecret = ajustes.Obtener("WhatsApp:AppSecret") ?? o.AppSecret;
        o.VerifyToken = ajustes.Obtener("WhatsApp:VerifyToken") ?? o.VerifyToken;
        if (bool.TryParse(ajustes.Obtener("WhatsApp:RequerirConsentimiento"), out var c)) o.RequerirConsentimiento = c;
        o.TextMeBot.ApiKey = ajustes.Obtener("WhatsApp:TextMeBot:ApiKey") ?? o.TextMeBot.ApiKey;
        o.TextMeBot.TokenWebhook = ajustes.Obtener("WhatsApp:TextMeBot:TokenWebhook") ?? o.TextMeBot.TokenWebhook;
        if (int.TryParse(ajustes.Obtener("WhatsApp:TextMeBot:SegundosEntreMensajes"), out var s)) o.TextMeBot.SegundosEntreMensajes = s;
    }

    public void PostConfigure(string? name, ChatbotOptions o)
    {
        o.Url = ajustes.Obtener("Chatbot:Url") ?? o.Url;
        o.ApiKey = ajustes.Obtener("Chatbot:ApiKey") ?? o.ApiKey;
    }
}

/// <summary>Elige el proveedor de WhatsApp vigente (Meta o TextMeBot) en cada uso, según la configuración actual.</summary>
public class EnviadorWhatsAppSegunProveedor(IOptionsSnapshot<WhatsAppOptions> opciones, IServiceProvider sp) : IEnviadorWhatsApp
{
    private IEnviadorWhatsApp Actual => opciones.Value.EsMeta
        ? sp.GetRequiredService<EnviadorWhatsAppCloud>()
        : sp.GetRequiredService<EnviadorWhatsAppTextMeBot>();

    public bool RequierePlantillasAprobadas => Actual.RequierePlantillasAprobadas;
    public Task<ResultadoEnvio> EnviarPlantillaAsync(string telefono, string plantilla, string idioma, IReadOnlyList<string> parametros, CancellationToken ct) =>
        Actual.EnviarPlantillaAsync(telefono, plantilla, idioma, parametros, ct);
    public Task<ResultadoEnvio> EnviarTextoAsync(string telefono, string texto, CancellationToken ct) => Actual.EnviarTextoAsync(telefono, texto, ct);
}
