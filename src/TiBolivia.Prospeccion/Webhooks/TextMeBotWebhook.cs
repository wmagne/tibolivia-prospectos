using System.Text.Json;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Webhooks;

/// <summary>
/// Mensajes entrantes de TextMeBot. Configure en su panel de TextMeBot la URL:
/// https://SU-DOMINIO/webhooks/textmebot?token=SU_TOKEN
/// Acepta JSON o formulario y busca el teléfono y el texto en los nombres de campo más comunes.
/// </summary>
public static class TextMeBotWebhook
{
    private static readonly string[] CamposTelefono = ["from", "phone", "sender", "number", "wa_id", "remoteJid"];
    private static readonly string[] CamposTexto = ["text", "message", "body", "msg", "content"];
    private static readonly string[] CamposId = ["id", "message_id", "msgid", "key"];

    public static void MapTextMeBotWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/textmebot", async (HttpRequest req, IServiceProvider sp, IOptionsSnapshot<WhatsAppOptions> o, ILoggerFactory lf, CancellationToken ct) =>
        {
            var log = lf.CreateLogger("TextMeBotWebhook");
            var token = o.Value.TextMeBot.TokenWebhook;
            if (string.IsNullOrEmpty(token) || req.Query["token"] != token) return Results.Unauthorized();

            var campos = await LeerCamposAsync(req, ct);
            log.LogInformation("TextMeBot entrante: {Campos}", string.Join(", ", campos.Keys));
            var telefono = Telefonos.Normalizar(Primero(campos, CamposTelefono)?.Split('@')[0]);
            var texto = Primero(campos, CamposTexto);
            if (telefono is null || string.IsNullOrWhiteSpace(texto))
            {
                log.LogWarning("TextMeBot: no se reconoció el teléfono o el texto. Campos recibidos: {Campos}", string.Join(", ", campos.Keys));
                return Results.Ok();
            }

            try
            {
                var id = Primero(campos, CamposId);
                await WhatsAppWebhook.ProcesarEntranteAsync(sp.GetRequiredService<AppDbContext>(), sp, telefono,
                    id is null ? null : "tmb-in-" + id, texto, log, ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error procesando mensaje de TextMeBot");
            }
            return Results.Ok();
        }).AllowAnonymous().DisableAntiforgery();
    }

    private static async Task<Dictionary<string, string>> LeerCamposAsync(HttpRequest req, CancellationToken ct)
    {
        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (req.HasFormContentType)
        {
            foreach (var (k, v) in await req.ReadFormAsync(ct)) campos[k] = v.ToString();
        }
        else
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(req.Body, cancellationToken: ct);
                Aplanar(doc.RootElement, campos);
            }
            catch (JsonException) { }
        }
        foreach (var (k, v) in req.Query) campos.TryAdd(k, v.ToString());
        campos.Remove("token");
        return campos;
    }

    private static void Aplanar(JsonElement e, Dictionary<string, string> campos)
    {
        if (e.ValueKind != JsonValueKind.Object) return;
        foreach (var p in e.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Object) Aplanar(p.Value, campos);
            else campos.TryAdd(p.Name, p.Value.ToString());
        }
    }

    private static string? Primero(Dictionary<string, string> campos, string[] nombres) =>
        nombres.Select(n => campos.GetValueOrDefault(n)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
