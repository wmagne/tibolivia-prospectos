using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TiBolivia.Prospeccion.Services;

public record SolicitudChatbot(string Canal, string Telefono, string Mensaje, ContextoChatbot Contexto);
public record ContextoChatbot(int ProspectoId, string Empresa, string? Nombre, string? Campana, string? Servicio, string? Comercial);
public record RespuestaChatbot(string? Respuesta, bool DerivarAHumano);

public interface IClienteChatbot
{
    Task<RespuestaChatbot?> ConsultarAsync(SolicitudChatbot solicitud, CancellationToken ct);
}

/// <summary>
/// Reenvía los mensajes entrantes de WhatsApp al chatbot de la web de TI Bolivia
/// para que responda con el mismo conocimiento que usa en la página.
/// </summary>
public class ClienteChatbotHttp(HttpClient http, IOptionsSnapshot<ChatbotOptions> opciones, ILogger<ClienteChatbotHttp> log) : IClienteChatbot
{
    public async Task<RespuestaChatbot?> ConsultarAsync(SolicitudChatbot solicitud, CancellationToken ct)
    {
        var o = opciones.Value;
        if (string.IsNullOrWhiteSpace(o.Url)) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, o.Url) {
                // Contenido con Content-Length (no "chunked"): algunos backends PHP/Node no leen cuerpos chunked.
                Content = new StringContent(JsonSerializer.Serialize(solicitud, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrWhiteSpace(o.ApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSegundos));
            using var resp = await http.SendAsync(req, cts.Token);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<RespuestaChatbot>(cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "No se pudo consultar el chatbot");
            return null;
        }
    }
}
