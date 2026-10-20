using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TiBolivia.Prospeccion.Services;

/// <summary>
/// Envío por TextMeBot (https://textmebot.com): usa un WhatsApp normal vinculado por QR.
/// No requiere plantillas aprobadas, pero NO es oficial: los envíos masivos a desconocidos
/// pueden hacer que WhatsApp bloquee el número. Úselo con pocos mensajes y contactos que lo esperan.
/// </summary>
public class EnviadorWhatsAppTextMeBot(HttpClient http, IOptions<WhatsAppOptions> opciones, ILogger<EnviadorWhatsAppTextMeBot> log) : IEnviadorWhatsApp
{
    // Una sola cola para todo el proceso: respeta la pausa entre mensajes aunque envíen el motor y un comercial a la vez.
    private static readonly SemaphoreSlim Cola = new(1, 1);
    private static DateTime _ultimoEnvio = DateTime.MinValue;

    public bool RequierePlantillasAprobadas => false;

    /// <summary>TextMeBot no maneja plantillas: se envía el texto con los parámetros separados por línea.</summary>
    public Task<ResultadoEnvio> EnviarPlantillaAsync(string telefono, string plantilla, string idioma, IReadOnlyList<string> parametros, CancellationToken ct) =>
        EnviarTextoAsync(telefono, parametros.Count == 0 ? $"Hola, le saludamos de TI Bolivia ({plantilla})." : string.Join("\n", parametros), ct);

    public async Task<ResultadoEnvio> EnviarTextoAsync(string telefono, string texto, CancellationToken ct)
    {
        var o = opciones.Value.TextMeBot;
        if (string.IsNullOrWhiteSpace(o.ApiKey))
        {
            log.LogInformation("[SIMULACIÓN] TextMeBot a {Telefono}", telefono);
            return ResultadoEnvio.Ok($"sim-tmb-{Guid.NewGuid():N}");
        }

        await Cola.WaitAsync(ct);
        try
        {
            var espera = _ultimoEnvio.AddSeconds(o.SegundosEntreMensajes) - DateTime.UtcNow;
            if (espera > TimeSpan.Zero) await Task.Delay(espera, ct);

            var url = $"{o.Url}?recipient={Uri.EscapeDataString("+" + telefono)}&apikey={Uri.EscapeDataString(o.ApiKey)}" +
                      $"&text={Uri.EscapeDataString(texto)}&json=yes";
            using var resp = await http.GetAsync(url, ct);
            _ultimoEnvio = DateTime.UtcNow;
            var cuerpo = await resp.Content.ReadAsStringAsync(ct);
            return Interpretar(resp.IsSuccessStatusCode, cuerpo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Error enviando por TextMeBot a {Telefono}", telefono);
            return ResultadoEnvio.Falla(ex.Message);
        }
        finally
        {
            Cola.Release();
        }
    }

    /// <summary>La respuesta con json=yes trae "status" y "comment" (ej. clave incorrecta, plan vencido).</summary>
    internal static ResultadoEnvio Interpretar(bool httpOk, string cuerpo)
    {
        string? estado = null, comentario = null;
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            if (doc.RootElement.TryGetProperty("status", out var s)) estado = s.ToString();
            if (doc.RootElement.TryGetProperty("comment", out var c)) comentario = c.ToString();
        }
        catch (JsonException) { comentario = cuerpo.Length > 300 ? cuerpo[..300] : cuerpo; }

        var fallo = !httpOk
            || (estado is not null && (estado.Contains("error", StringComparison.OrdinalIgnoreCase) || estado.Contains("fail", StringComparison.OrdinalIgnoreCase)))
            || (estado is null && comentario is not null && comentario.Contains("error", StringComparison.OrdinalIgnoreCase));
        return fallo
            ? ResultadoEnvio.Falla($"TextMeBot: {comentario ?? estado ?? "respuesta inesperada"}")
            : ResultadoEnvio.Ok($"tmb-{Guid.NewGuid():N}");
    }
}
