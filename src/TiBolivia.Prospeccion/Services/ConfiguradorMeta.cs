using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

public record PerfilEmpresa(string? Acerca, string? Descripcion, string? Email, string? Web, string? Direccion);

/// <summary>
/// Configura en Meta lo que se puede hacer por API: registrar el número, el perfil de empresa,
/// crear las plantillas de las campañas, suscribir la app y conectar el webhook.
/// </summary>
public partial class ConfiguradorMeta(HttpClient http, IOptionsSnapshot<WhatsAppOptions> opciones)
{
    private WhatsAppOptions O => opciones.Value;

    /// <summary>Registra el número en la Cloud API. El PIN de 6 dígitos queda como verificación en dos pasos: anótelo.</summary>
    /// <summary>Ajuste (cifrado) donde se guarda el PIN de verificación en dos pasos del número.</summary>
    public const string ClavePin = "WhatsApp:PinRegistro";

    public Task RegistrarNumeroAsync(string pin, CancellationToken ct, string? phoneNumberId = null) =>
        PostAsync(O.Url($"{phoneNumberId ?? O.PhoneNumberId}/register"), new { messaging_product = "whatsapp", pin }, ct);

    /// <summary>Agrega un número a la cuenta de WhatsApp (queda pendiente de verificar con código). Devuelve su Phone Number ID.</summary>
    public async Task<string> AgregarNumeroAsync(string codigoPais, string numero, string nombreVisible, CancellationToken ct)
    {
        var cuerpo = await PostAsync(O.Url($"{O.BusinessAccountId}/phone_numbers"),
            new { cc = codigoPais, phone_number = numero, verified_name = nombreVisible }, ct);
        using var doc = JsonDocument.Parse(cuerpo);
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Meta no devolvió el identificador del número.");
    }

    /// <summary>Pide a Meta que envíe el código de verificación al número (SMS o VOICE).</summary>
    public Task SolicitarCodigoAsync(string phoneNumberId, string metodo, CancellationToken ct) =>
        PostAsync(O.Url($"{phoneNumberId}/request_code"), new { code_method = metodo, language = "es" }, ct);

    public Task VerificarCodigoAsync(string phoneNumberId, string codigo, CancellationToken ct) =>
        PostAsync(O.Url($"{phoneNumberId}/verify_code"), new { code = codigo }, ct);

    public Task ActualizarPerfilAsync(PerfilEmpresa p, CancellationToken ct)
    {
        var datos = new Dictionary<string, object> { ["messaging_product"] = "whatsapp", ["vertical"] = "PROF_SERVICES" };
        if (!string.IsNullOrWhiteSpace(p.Acerca)) datos["about"] = Recortar(p.Acerca, 139);
        if (!string.IsNullOrWhiteSpace(p.Descripcion)) datos["description"] = Recortar(p.Descripcion, 512);
        if (!string.IsNullOrWhiteSpace(p.Email)) datos["email"] = p.Email.Trim();
        if (!string.IsNullOrWhiteSpace(p.Web)) datos["websites"] = new[] { p.Web.Trim() };
        if (!string.IsNullOrWhiteSpace(p.Direccion)) datos["address"] = Recortar(p.Direccion, 256);
        return PostAsync(O.Url($"{O.PhoneNumberId}/whatsapp_business_profile"), datos, ct);
    }

    /// <summary>Crea en Meta la plantilla de marketing a partir de una plantilla de WhatsApp del sistema.</summary>
    public Task CrearPlantillaAsync(Plantilla plantilla, CancellationToken ct)
    {
        var (texto, ejemplos) = ConvertirAPlantillaMeta(plantilla);
        var componentes = new List<object>
        {
            ejemplos.Count == 0
                ? new { type = "BODY", text = texto }
                : new { type = "BODY", text = texto, example = new { body_text = new[] { ejemplos.ToArray() } } },
            new
            {
                type = "BUTTONS",
                buttons = new[] { new { type = "QUICK_REPLY", text = "Sí, me interesa" }, new { type = "QUICK_REPLY", text = "Ahora no" } },
            },
        };
        return PostAsync(O.Url($"{O.BusinessAccountId}/message_templates"), new
        {
            name = plantilla.WhatsAppPlantillaMeta,
            language = plantilla.WhatsAppIdioma,
            category = "MARKETING",
            components = componentes,
        }, ct);
    }

    /// <summary>Suscribe la app a la cuenta de WhatsApp: sin esto Meta no envía los mensajes entrantes al webhook.</summary>
    public Task SuscribirAppAsync(CancellationToken ct) => PostAsync(O.Url($"{O.BusinessAccountId}/subscribed_apps"), null, ct);

    /// <summary>
    /// Registra la URL del webhook en la app. Meta llama de inmediato a esa URL para verificarla,
    /// así que el sistema debe estar accesible desde internet (túnel o servidor).
    /// </summary>
    public Task ConfigurarWebhookAsync(string urlWebhook, CancellationToken ct)
    {
        var url = O.Url($"{O.AppId}/subscriptions") +
                  $"?object=whatsapp_business_account&fields=messages&include_values=true" +
                  $"&callback_url={Uri.EscapeDataString(urlWebhook)}&verify_token={Uri.EscapeDataString(O.VerifyToken ?? "")}" +
                  $"&access_token={Uri.EscapeDataString($"{O.AppId}|{O.AppSecret}")}";
        return PostAsync(url, null, ct, usarToken: false);
    }

    /// <summary>
    /// Convierte "Hola {{Nombre}} de {{Empresa}}" en "Hola {{1}} de {{2}}" según el orden de WhatsAppParametros,
    /// con valores de ejemplo que Meta exige para revisar la plantilla.
    /// </summary>
    public static (string Texto, List<string> Ejemplos) ConvertirAPlantillaMeta(Plantilla p)
    {
        var parametros = (p.WhatsAppParametros ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => VariableRegex().Match(v) is { Success: true } m ? m.Groups[1].Value : v.Trim('{', '}', ' '))
            .ToList();
        var texto = p.Cuerpo;
        var ejemplos = new List<string>();
        for (var i = 0; i < parametros.Count; i++)
        {
            texto = Regex.Replace(texto, @"\{\{\s*" + Regex.Escape(parametros[i]) + @"\s*\}\}", $"{{{{{i + 1}}}}}", RegexOptions.IgnoreCase);
            ejemplos.Add(Ejemplo(parametros[i]));
        }
        var sobrantes = VariableRegex().Matches(texto).Select(m => m.Groups[1].Value).Where(v => !int.TryParse(v, out _)).Distinct().ToList();
        if (sobrantes.Count > 0)
            throw new InvalidOperationException(
                $"La plantilla \"{p.Nombre}\" usa {string.Join(", ", sobrantes.Select(v => "{{" + v + "}}"))} pero no están en \"Parámetros\". Agréguelas allí o quítelas del texto.");
        return (texto, ejemplos);
    }

    private static string Ejemplo(string variable) => variable.ToLowerInvariant() switch
    {
        "nombre" => "Carlos",
        "contacto" => "Carlos Mamani",
        "empresa" => "Farmacia Central",
        "servicio" => "facturación electrónica",
        "ciudad" => "La Paz",
        "rubro" => "farmacias",
        "comercial" => "María López",
        _ => "ejemplo",
    };

    private async Task<string> PostAsync(string url, object? cuerpo, CancellationToken ct, bool usarToken = true)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (cuerpo is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json");
        if (usarToken) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", O.AccessToken);
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(DiagnosticoWhatsApp.ErrorLegible(await resp.Content.ReadAsStringAsync(ct), (int)resp.StatusCode));
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static string Recortar(string s, int max) => s.Trim().Length <= max ? s.Trim() : s.Trim()[..max];

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex VariableRegex();
}
