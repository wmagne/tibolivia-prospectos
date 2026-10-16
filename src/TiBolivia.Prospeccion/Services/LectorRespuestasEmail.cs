using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

/// <summary>
/// Revisa por IMAP la casilla del remitente (ej. comercial@tibolivia.com) y registra los correos
/// que envían los prospectos. Solo lee: no marca como leído ni mueve nada, así la casilla
/// se sigue usando normalmente desde Outlook o el celular.
/// </summary>
public class LectorRespuestasEmail(
    AppDbContext db,
    IOptions<ImapOptions> imapOpciones,
    IOptions<SmtpOptions> smtpOpciones,
    TimeProvider tiempo,
    ILogger<LectorRespuestasEmail> log)
{
    private static readonly string[] AsuntosAutomaticos =
        ["respuesta automática", "respuesta automatica", "fuera de la oficina", "out of office", "automatic reply", "autoreply", "vacaciones"];

    public bool Configurado
    {
        get
        {
            var o = imapOpciones.Value;
            return o.Habilitado && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Password);
        }
    }

    private string? Host => imapOpciones.Value.Host is { Length: > 0 } h ? h : smtpOpciones.Value.Host;
    private string? Usuario => imapOpciones.Value.Usuario is { Length: > 0 } u ? u : smtpOpciones.Value.Usuario;
    private string? Password => imapOpciones.Value.Password is { Length: > 0 } p ? p : smtpOpciones.Value.Password;

    public async Task<int> ProcesarAsync(CancellationToken ct)
    {
        if (!Configurado) return 0;
        var o = imapOpciones.Value;

        using var imap = new ImapClient();
        await imap.ConnectAsync(Host!, o.Port, o.Port == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
        await imap.AuthenticateAsync(Usuario!, Password!, ct);
        var carpeta = await imap.GetFolderAsync(o.Carpeta, ct);
        await carpeta.OpenAsync(FolderAccess.ReadOnly, ct);

        var desde = tiempo.GetUtcNow().UtcDateTime.Date.AddDays(-o.DiasARevisar);
        var uids = await carpeta.SearchAsync(SearchQuery.DeliveredAfter(desde), ct);
        if (uids.Count == 0) { await imap.DisconnectAsync(true, ct); return 0; }

        var resumenes = await carpeta.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId, ct);
        var nuevos = 0;
        foreach (var r in resumenes)
        {
            if (r.Envelope is null) continue;
            var remitente = r.Envelope.From.Mailboxes.FirstOrDefault()?.Address?.Trim().ToLowerInvariant();
            var messageId = r.Envelope.MessageId;
            if (remitente is null || string.IsNullOrEmpty(messageId)) continue;

            var prospecto = await db.Prospectos
                .Include(p => p.Inscripciones).ThenInclude(i => i.Campana)
                .FirstOrDefaultAsync(p => p.Email == remitente, ct);
            if (prospecto is null) continue; // no es un prospecto (correo interno, rebote, etc.)
            if (await db.Mensajes.AnyAsync(m => m.ProveedorId == messageId, ct)) continue;

            var mensaje = await carpeta.GetMessageAsync(r.UniqueId, ct);
            var texto = (mensaje.TextBody ?? mensaje.HtmlBody ?? "").Trim();
            if (texto.Length > 4000) texto = texto[..4000] + "…";
            var fecha = (r.Envelope.Date ?? DateTimeOffset.UtcNow).UtcDateTime;

            db.Mensajes.Add(new Mensaje
            {
                ProspectoId = prospecto.Id, Canal = Canal.Email, Direccion = DireccionMensaje.Entrante,
                Asunto = r.Envelope.Subject, Cuerpo = texto, Estado = EstadoMensaje.Recibido, ProveedorId = messageId, CreadoEn = fecha,
            });

            if (EsAutomatico(mensaje))
            {
                // Respuestas de "fuera de la oficina": se registran pero no pausan la secuencia.
                db.Interacciones.Add(new Interaccion { ProspectoId = prospecto.Id, Tipo = TipoInteraccion.Sistema, Detalle = "Respuesta automática de correo: " + r.Envelope.Subject });
            }
            else
            {
                Respuestas.Registrar(prospecto, fecha);
                db.Interacciones.Add(new Interaccion { ProspectoId = prospecto.Id, Tipo = TipoInteraccion.Sistema, Detalle = "Respondió por correo: " + r.Envelope.Subject });
            }
            await db.SaveChangesAsync(ct);
            nuevos++;
        }

        await imap.DisconnectAsync(true, ct);
        if (nuevos > 0) log.LogInformation("IMAP: {Cantidad} respuestas de prospectos registradas", nuevos);
        return nuevos;
    }

    private static bool EsAutomatico(MimeMessage m)
    {
        var autoSubmitted = m.Headers["Auto-Submitted"];
        if (!string.IsNullOrEmpty(autoSubmitted) && !autoSubmitted.Equals("no", StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrEmpty(m.Headers["X-Autoreply"]) || !string.IsNullOrEmpty(m.Headers["X-Autorespond"])) return true;
        var asunto = m.Subject?.ToLowerInvariant() ?? "";
        return AsuntosAutomaticos.Any(asunto.Contains);
    }
}

/// <summary>Ejecuta el lector de respuestas por correo periódicamente.</summary>
public class WorkerRespuestasEmail(IServiceScopeFactory scopes, IOptions<ImapOptions> opciones, ILogger<WorkerRespuestasEmail> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, opciones.Value.IntervaloSegundos)));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<LectorRespuestasEmail>().ProcesarAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "No se pudo revisar la casilla de correo por IMAP");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
