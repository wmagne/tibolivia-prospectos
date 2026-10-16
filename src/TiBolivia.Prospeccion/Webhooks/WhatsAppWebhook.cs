using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Webhooks;

/// <summary>
/// Webhook de WhatsApp Cloud API: recibe estados de entrega y mensajes entrantes.
/// Los mensajes entrantes se pasan al chatbot de TI Bolivia y su respuesta se devuelve por WhatsApp.
/// </summary>
public static class WhatsAppWebhook
{
    private static readonly string[] PalabrasBaja = ["baja", "stop", "no molestar", "no me escriban", "cancelar"];

    public static void MapWhatsAppWebhook(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/webhooks/whatsapp").AllowAnonymous().DisableAntiforgery();

        // Verificación inicial que hace Meta al registrar el webhook.
        grupo.MapGet("", (HttpRequest req, IOptions<WhatsAppOptions> o) =>
        {
            var q = req.Query;
            return q["hub.mode"] == "subscribe" && !string.IsNullOrEmpty(o.Value.VerifyToken) && q["hub.verify_token"] == o.Value.VerifyToken
                ? Results.Text(q["hub.challenge"].ToString())
                : Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        grupo.MapPost("", async (HttpRequest req, IServiceProvider sp, IOptions<WhatsAppOptions> o, ILoggerFactory lf, CancellationToken ct) =>
        {
            var log = lf.CreateLogger("WhatsAppWebhook");
            using var lector = new StreamReader(req.Body, Encoding.UTF8);
            var cuerpo = await lector.ReadToEndAsync(ct);

            if (!FirmaValida(cuerpo, req.Headers["X-Hub-Signature-256"], o.Value.AppSecret))
            {
                log.LogWarning("Webhook de WhatsApp con firma inválida");
                return Results.Unauthorized();
            }

            try
            {
                await ProcesarAsync(cuerpo, sp, log, ct);
            }
            catch (Exception ex)
            {
                // Siempre responder 200 para que Meta no reintente indefinidamente; el error queda en el log.
                log.LogError(ex, "Error procesando webhook de WhatsApp");
            }
            return Results.Ok();
        });
    }

    internal static bool FirmaValida(string cuerpo, string? firma, string? appSecret)
    {
        if (string.IsNullOrEmpty(appSecret)) return true; // desarrollo: sin App Secret no se valida
        if (string.IsNullOrEmpty(firma) || !firma.StartsWith("sha256=")) return false;
        var esperado = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes(cuerpo));
        byte[] recibido;
        try { recibido = Convert.FromHexString(firma["sha256=".Length..]); }
        catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(esperado, recibido);
    }

    private static async Task ProcesarAsync(string cuerpo, IServiceProvider sp, ILogger log, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(cuerpo);
        if (!doc.RootElement.TryGetProperty("entry", out var entries)) return;

        var db = sp.GetRequiredService<AppDbContext>();
        foreach (var entry in entries.EnumerateArray())
        foreach (var change in entry.GetProperty("changes").EnumerateArray())
        {
            if (!change.TryGetProperty("value", out var value)) continue;

            if (value.TryGetProperty("statuses", out var estados))
                foreach (var s in estados.EnumerateArray())
                    await ActualizarEstadoAsync(db, s, ct);

            if (value.TryGetProperty("messages", out var mensajes))
                foreach (var m in mensajes.EnumerateArray())
                    await RecibirMensajeAsync(db, sp, m, log, ct);
        }
    }

    private static async Task ActualizarEstadoAsync(AppDbContext db, JsonElement s, CancellationToken ct)
    {
        var id = s.GetProperty("id").GetString();
        var mensaje = await db.Mensajes.FirstOrDefaultAsync(m => m.ProveedorId == id, ct);
        if (mensaje is null) return;
        mensaje.Estado = s.GetProperty("status").GetString() switch
        {
            "delivered" => EstadoMensaje.Entregado,
            "read" => EstadoMensaje.Leido,
            "failed" => EstadoMensaje.Fallido,
            _ => mensaje.Estado,
        };
        if (mensaje.Estado == EstadoMensaje.Fallido && s.TryGetProperty("errors", out var errores))
            mensaje.Error = errores.ToString();
        await db.SaveChangesAsync(ct);
    }

    private static async Task RecibirMensajeAsync(AppDbContext db, IServiceProvider sp, JsonElement m, ILogger log, CancellationToken ct)
    {
        var telefono = Telefonos.Normalizar(m.GetProperty("from").GetString())!;
        var wamid = m.GetProperty("id").GetString();
        if (await db.Mensajes.AnyAsync(x => x.ProveedorId == wamid, ct)) return; // Meta puede reenviar el mismo evento

        var texto = ExtraerTexto(m);
        var prospecto = await db.Prospectos
            .Include(p => p.AsignadoA)
            .Include(p => p.Inscripciones).ThenInclude(i => i.Campana).ThenInclude(c => c.Servicio)
            .FirstOrDefaultAsync(p => p.Telefono == telefono, ct);

        if (prospecto is null)
        {
            // Alguien nuevo escribió por WhatsApp: se registra como prospecto (ya dio su consentimiento al escribir).
            prospecto = new Prospecto { Empresa = "(sin empresa)", Telefono = telefono, Origen = "WhatsApp entrante", ConsentimientoWhatsApp = true };
            db.Prospectos.Add(prospecto);
        }

        var ahora = DateTime.UtcNow;
        prospecto.UltimaRespuestaEn = ahora;
        prospecto.ActualizadoEn = ahora;
        prospecto.ConsentimientoWhatsApp = true;
        prospecto.Mensajes.Add(new Mensaje
        {
            Canal = Canal.WhatsApp, Direccion = DireccionMensaje.Entrante, Cuerpo = texto,
            Estado = EstadoMensaje.Recibido, ProveedorId = wamid,
        });

        var whatsApp = sp.GetRequiredService<IEnviadorWhatsApp>();

        if (PalabrasBaja.Any(b => texto.Trim().Equals(b, StringComparison.OrdinalIgnoreCase)))
        {
            prospecto.BajaWhatsApp = true;
            foreach (var i in prospecto.Inscripciones.Where(i => i.Estado == EstadoInscripcion.Activa))
            { i.Estado = EstadoInscripcion.Detenida; i.ProximoEnvio = null; }
            prospecto.Interacciones.Add(new Interaccion { Tipo = TipoInteraccion.Sistema, Detalle = "Solicitó no recibir más mensajes por WhatsApp." });
            await db.SaveChangesAsync(ct);
            await ResponderAsync(db, whatsApp, prospecto, "Entendido, no le enviaremos más mensajes. ¡Gracias!", ct);
            return;
        }

        // El prospecto respondió: se pausa la secuencia automática para que lo atienda una persona / el chatbot.
        Respuestas.Registrar(prospecto, ahora);
        await db.SaveChangesAsync(ct);

        var campana = prospecto.Inscripciones.OrderByDescending(i => i.CreadoEn).Select(i => i.Campana).FirstOrDefault();
        var chatbot = sp.GetRequiredService<IClienteChatbot>();
        var respuesta = await chatbot.ConsultarAsync(new SolicitudChatbot("whatsapp", telefono, texto,
            new ContextoChatbot(prospecto.Id, prospecto.Empresa, prospecto.NombreContacto, campana?.Nombre,
                campana?.Servicio?.Nombre, prospecto.AsignadoA?.NombreCompleto)), ct);

        if (respuesta is null) return;
        if (respuesta.DerivarAHumano)
        {
            prospecto.Interacciones.Add(new Interaccion
            {
                Tipo = TipoInteraccion.Sistema,
                Detalle = "El chatbot derivó la conversación a un comercial. Último mensaje: " + texto,
            });
            await db.SaveChangesAsync(ct);
        }
        if (!string.IsNullOrWhiteSpace(respuesta.Respuesta))
            await ResponderAsync(db, whatsApp, prospecto, respuesta.Respuesta, ct);
        log.LogInformation("Mensaje de {Telefono} atendido por el chatbot", telefono);
    }

    private static async Task ResponderAsync(AppDbContext db, IEnviadorWhatsApp whatsApp, Prospecto p, string texto, CancellationToken ct)
    {
        var r = await whatsApp.EnviarTextoAsync(p.Telefono!, texto, ct);
        db.Mensajes.Add(new Mensaje
        {
            ProspectoId = p.Id, Canal = Canal.WhatsApp, Direccion = DireccionMensaje.Saliente, Cuerpo = texto,
            Estado = r.Exito ? EstadoMensaje.Enviado : EstadoMensaje.Fallido, ProveedorId = r.ProveedorId, Error = r.Error,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string ExtraerTexto(JsonElement m) => m.GetProperty("type").GetString() switch
    {
        "text" => m.GetProperty("text").GetProperty("body").GetString() ?? "",
        "button" => m.GetProperty("button").GetProperty("text").GetString() ?? "",
        "interactive" when m.GetProperty("interactive").TryGetProperty("button_reply", out var br) => br.GetProperty("title").GetString() ?? "",
        "interactive" when m.GetProperty("interactive").TryGetProperty("list_reply", out var lr) => lr.GetProperty("title").GetString() ?? "",
        var tipo => $"[{tipo}]",
    };
}
