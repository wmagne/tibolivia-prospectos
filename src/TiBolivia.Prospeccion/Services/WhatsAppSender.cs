using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TiBolivia.Prospeccion.Services;

public interface IEnviadorWhatsApp
{
    /// <summary>
    /// true (Meta): el primer mensaje debe ser una plantilla aprobada y el texto libre solo vale 24 h después de que el cliente escribe.
    /// false (TextMeBot): siempre se envía el texto de la plantilla del sistema, sin aprobación previa.
    /// </summary>
    bool RequierePlantillasAprobadas { get; }

    /// <summary>Plantilla aprobada por Meta: única forma de iniciar una conversación (fuera de la ventana de 24 h).</summary>
    Task<ResultadoEnvio> EnviarPlantillaAsync(string telefono, string plantilla, string idioma, IReadOnlyList<string> parametros, CancellationToken ct);
    /// <summary>Texto libre: solo permitido dentro de las 24 h posteriores al último mensaje del cliente.</summary>
    Task<ResultadoEnvio> EnviarTextoAsync(string telefono, string texto, CancellationToken ct);
}

/// <summary>Cliente de WhatsApp Business Cloud API (Meta).</summary>
public class EnviadorWhatsAppCloud(HttpClient http, IOptionsSnapshot<WhatsAppOptions> opciones, ILogger<EnviadorWhatsAppCloud> log) : IEnviadorWhatsApp
{
    public bool RequierePlantillasAprobadas => true;

    public Task<ResultadoEnvio> EnviarPlantillaAsync(string telefono, string plantilla, string idioma, IReadOnlyList<string> parametros, CancellationToken ct)
    {
        object template = parametros.Count == 0
            ? new { name = plantilla, language = new { code = idioma } }
            : new
            {
                name = plantilla,
                language = new { code = idioma },
                components = new[]
                {
                    new { type = "body", parameters = parametros.Select(p => new { type = "text", text = p }).ToArray() }
                }
            };
        return EnviarAsync(new { messaging_product = "whatsapp", to = telefono, type = "template", template }, telefono, $"plantilla {plantilla}", ct);
    }

    public Task<ResultadoEnvio> EnviarTextoAsync(string telefono, string texto, CancellationToken ct) =>
        EnviarAsync(new { messaging_product = "whatsapp", to = telefono, type = "text", text = new { body = texto } }, telefono, "texto", ct);

    private async Task<ResultadoEnvio> EnviarAsync(object payload, string telefono, string descripcion, CancellationToken ct)
    {
        var o = opciones.Value;
        if (o.Simulacion)
        {
            log.LogInformation("[SIMULACIÓN] WhatsApp a {Telefono}: {Descripcion}", telefono, descripcion);
            return ResultadoEnvio.Ok($"sim-wamid-{Guid.NewGuid():N}");
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, o.Url($"{o.PhoneNumberId}/messages"))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.AccessToken);
            using var resp = await http.SendAsync(req, ct);
            var cuerpo = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("WhatsApp respondió {Status}: {Cuerpo}", (int)resp.StatusCode, cuerpo);
                return ResultadoEnvio.Falla(DiagnosticoWhatsApp.ErrorLegible(cuerpo, (int)resp.StatusCode));
            }
            using var doc = JsonDocument.Parse(cuerpo);
            var id = doc.RootElement.GetProperty("messages")[0].GetProperty("id").GetString();
            return ResultadoEnvio.Ok(id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Error enviando WhatsApp a {Telefono}", telefono);
            return ResultadoEnvio.Falla(ex.Message);
        }
    }
}
