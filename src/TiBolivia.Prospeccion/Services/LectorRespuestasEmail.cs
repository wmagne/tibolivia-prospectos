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
public partial class LectorRespuestasEmail(
    AppDbContext db,
    AvisoInteresado aviso,
    IOptions<ImapOptions> imapOpciones,
    IOptions<SmtpOptions> smtpOpciones,
    TimeProvider tiempo,
    ILogger<LectorRespuestasEmail> log)
{
    private static readonly string[] AsuntosAutomaticos =
        ["respuesta automática", "respuesta automatica", "fuera de la oficina", "out of office", "automatic reply", "autoreply", "vacaciones"];

    private static readonly string[] AsuntosRebote =
        ["undelivered mail", "undeliverable", "delivery status notification (failure)", "mail delivery failed",
         "returned mail", "failure notice", "no se puede entregar", "no se pudo entregar", "entrega fallida"];

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

            if (EsRebote(remitente, r.Envelope.Subject))
            {
                if (await ProcesarReboteAsync(carpeta, r, messageId, ct)) nuevos++;
                continue;
            }

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

            var cambio = await CambiarCorreoSiLoPideAsync(prospecto, remitente, texto, ct);
            if (cambio is not null)
                db.Interacciones.Add(new Interaccion { ProspectoId = prospecto.Id, Tipo = TipoInteraccion.Sistema, Detalle = cambio });
            if (cambio is not null && EsSoloAvisoDeCambio(texto))
            {
                // "Este correo ya no está activo, escriba a…": no es interés, las campañas siguen al correo nuevo.
            }
            else if (EsAutomatico(mensaje) || EsTextoAutomatico(texto))
            {
                // Respuestas de "fuera de la oficina": se registran pero no pausan la secuencia.
                db.Interacciones.Add(new Interaccion { ProspectoId = prospecto.Id, Tipo = TipoInteraccion.Sistema, Detalle = "Respuesta automática de correo: " + r.Envelope.Subject });
            }
            else
            {
                Respuestas.Registrar(prospecto, fecha);
                db.Interacciones.Add(new Interaccion { ProspectoId = prospecto.Id, Tipo = TipoInteraccion.Sistema, Detalle = "Respondió por correo: " + r.Envelope.Subject });
                await db.SaveChangesAsync(ct);
                await aviso.EnviarAsync(prospecto, "correo", r.Envelope.Subject, texto, ct);
            }
            await db.SaveChangesAsync(ct);
            nuevos++;
        }

        await imap.DisconnectAsync(true, ct);
        nuevos += await RevisarCambiosDeCorreoGuardadosAsync(ct);
        if (nuevos > 0) log.LogInformation("IMAP: {Cantidad} respuestas de prospectos registradas", nuevos);
        return nuevos;
    }

    private static bool EsRebote(string remitente, string? asunto)
    {
        var local = remitente.Split('@')[0];
        if (local is "mailer-daemon" or "postmaster") return true;
        var a = asunto?.ToLowerInvariant() ?? "";
        return AsuntosRebote.Any(a.Contains);
    }

    /// <summary>
    /// Rebote definitivo (la dirección no existe o rechaza el correo): se marca el correo del prospecto como inválido
    /// para no volver a escribirle y se detienen sus campañas. Los avisos de "entrega demorada" se ignoran.
    /// </summary>
    private async Task<bool> ProcesarReboteAsync(IMailFolder carpeta, IMessageSummary r, string messageId, CancellationToken ct)
    {
        if (await db.Mensajes.AnyAsync(m => m.ProveedorId == messageId, ct)) return false;
        var mensaje = await carpeta.GetMessageAsync(r.UniqueId, ct);

        var fallidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reporte = mensaje.BodyParts.OfType<MessageDeliveryStatus>().FirstOrDefault();
        if (reporte is not null)
        {
            foreach (var grupo in reporte.StatusGroups.Skip(1))
            {
                var accion = grupo["Action"]?.Trim().ToLowerInvariant();
                if (accion is not null && accion != "failed") continue;
                var destino = grupo["Final-Recipient"] ?? grupo["Original-Recipient"];
                if (destino?.Split(';').LastOrDefault()?.Trim() is { Length: > 3 } d) fallidos.Add(d);
            }
        }
        else
        {
            var asunto = mensaje.Subject?.ToLowerInvariant() ?? "";
            if (asunto.Contains("delay") || asunto.Contains("demora")) return false;
            foreach (System.Text.RegularExpressions.Match m in CorreoEnTexto().Matches(mensaje.TextBody ?? ""))
                fallidos.Add(m.Groups[1].Value);
        }
        if (fallidos.Count == 0) return false;

        var correos = fallidos.Select(x => x.Trim('<', '>', ' ').ToLowerInvariant()).ToList();
        var prospectos = await db.Prospectos.Include(p => p.Inscripciones)
            .Where(p => p.Email != null && correos.Contains(p.Email.ToLower())).ToListAsync(ct);
        if (prospectos.Count == 0) return false;

        var detalle = (mensaje.TextBody ?? "").Trim();
        if (detalle.Length > 1500) detalle = detalle[..1500] + "…";
        foreach (var p in prospectos)
        {
            p.BajaEmail = true;
            p.DatosPorVerificar = true;
            p.ActualizadoEn = DateTime.UtcNow;
            foreach (var i in p.Inscripciones.Where(i => i.Estado == EstadoInscripcion.Activa))
            {
                i.Estado = EstadoInscripcion.Detenida;
                i.ProximoEnvio = null;
            }
            db.Mensajes.Add(new Mensaje
            {
                ProspectoId = p.Id, Canal = Canal.Email, Direccion = DireccionMensaje.Entrante, Asunto = "Rebote: " + mensaje.Subject,
                Cuerpo = detalle, Estado = EstadoMensaje.Fallido, ProveedorId = messageId, CreadoEn = (r.Envelope.Date ?? DateTimeOffset.UtcNow).UtcDateTime,
            });
            db.Interacciones.Add(new Interaccion { ProspectoId = p.Id, Tipo = TipoInteraccion.Sistema,
                Detalle = $"El correo {p.Email} rebotó (no existe o rechaza mensajes). Se dejó de enviarle correos: busque otro correo de contacto." });
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("IMAP: rebote registrado para {Correos}", string.Join(", ", correos));
        return true;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<([^<>@\s]+@[^<>@\s]+)>")]
    private static partial System.Text.RegularExpressions.Regex CorreoEnTexto();

    private static readonly string[] TextosAutomaticos =
        ["gracias por escribirnos", "gracias por contactarnos", "gracias por comunicarte", "gracias por comunicarse",
         "hemos recibido su", "hemos recibido tu", "recibimos su mensaje", "recibimos tu mensaje", "este es un mensaje automático",
         "este es un mensaje automatico", "mensaje generado automáticamente", "no responda a este correo", "no-reply", "noreply"];

    private static readonly string[] PideCambioDeCorreo =
        ["correo no activo", "correo ya no está activo", "correo ya no esta activo", "ya no se encuentra activ", "dejará de funcionar",
         "dejara de funcionar", "nuevo correo", "nuevos correos", "nueva dirección", "nueva direccion", "cambiamos de correo",
         "cambio de correo", "reenviar", "reenvíe", "reenvie", "favor escribir a", "favor enviar a", "dirigir su correo", "escríbanos a", "escribanos a"];

    /// <summary>Frases de un aviso de casilla dada de baja (respuesta automática), a diferencia de "envíe la propuesta a…", que sí es interés.</summary>
    private static readonly string[] AvisoCasillaInactiva =
        ["correo no activo", "correo ya no está activo", "correo ya no esta activo", "ya no se encuentra activ", "dejará de funcionar",
         "dejara de funcionar", "cambiamos de correo", "cambio de correo", "ya no está en uso", "ya no esta en uso"];

    private static bool EsSoloAvisoDeCambio(string texto) => AvisoCasillaInactiva.Any(texto.ToLowerInvariant().Contains);

    [System.Text.RegularExpressions.GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial System.Text.RegularExpressions.Regex CorreoSuelto();

    /// <summary>Solo mira el comienzo: el correo nuestro citado debajo no cuenta.</summary>
    private static bool EsTextoAutomatico(string texto)
    {
        var inicio = FichaInteresado.Extracto(texto, 400).ToLowerInvariant();
        return TextosAutomaticos.Any(inicio.Contains);
    }

    /// <summary>
    /// La empresa avisa que ese correo ya no se usa y da otro: se actualiza el prospecto y sus campañas siguen al correo nuevo.
    /// No cuenta como interés, así que si la respuesta había pausado sus campañas, se reanudan.
    /// </summary>
    private async Task<string?> CambiarCorreoSiLoPideAsync(Prospecto p, string remitente, string texto, CancellationToken ct)
    {
        var minus = texto.ToLowerInvariant();
        if (!PideCambioDeCorreo.Any(minus.Contains)) return null;
        var nuevo = CorreoSuelto().Matches(texto).Select(m => m.Value.Trim('.').ToLowerInvariant())
            .FirstOrDefault(c => c != remitente && !c.EndsWith("@tibolivia.com") && !c.Contains("noreply") && !c.Contains("no-reply"));
        if (nuevo is null) return null;
        if (await db.Prospectos.AnyAsync(x => x.Id != p.Id && x.Email == nuevo, ct)) return null;

        var anterior = p.Email;
        p.Email = nuevo;
        p.BajaEmail = false;
        p.Notas = ((p.Notas ?? "") + $" | Correo anterior {anterior}: la empresa pidió escribir a {nuevo} ({Fechas.ABolivia(DateTime.UtcNow):dd/MM/yyyy}).").Trim(' ', '|');
        p.ActualizadoEn = DateTime.UtcNow;
        if (EsSoloAvisoDeCambio(texto))
        {
            var ahora = tiempo.GetUtcNow().UtcDateTime;
            foreach (var i in p.Inscripciones.Where(i => i.Estado == EstadoInscripcion.Pausada))
            {
                i.Estado = EstadoInscripcion.Activa;
                i.ProximoEnvio = ahora;
            }
            if (p.Estado == EstadoProspecto.Interesado && p.Inscripciones.Any()) p.Estado = EstadoProspecto.Contactado;
        }
        log.LogInformation("Correo actualizado a pedido de la empresa {Empresa}: {Anterior} → {Nuevo}", p.Empresa, anterior, nuevo);
        return $"Cambio de correo a pedido de la empresa: {anterior} → {nuevo}";
    }

    /// <summary>Aplica los pedidos de cambio de correo que llegaron antes de que existiera esta función (una sola vez por mensaje).</summary>
    private async Task<int> RevisarCambiosDeCorreoGuardadosAsync(CancellationToken ct)
    {
        var desde = tiempo.GetUtcNow().UtcDateTime.AddDays(-30);
        var candidatos = await db.Mensajes
            .Where(m => m.Canal == Canal.Email && m.Direccion == DireccionMensaje.Entrante && m.Estado == EstadoMensaje.Recibido && m.CreadoEn >= desde
                && m.Cuerpo != null && !db.Interacciones.Any(i => i.ProspectoId == m.ProspectoId && i.Detalle.StartsWith("Cambio de correo")))
            .Select(m => new { m.ProspectoId, m.Cuerpo }).ToListAsync(ct);
        var n = 0;
        foreach (var c in candidatos.Where(c => PideCambioDeCorreo.Any(c.Cuerpo!.ToLowerInvariant().Contains)))
        {
            var p = await db.Prospectos.Include(x => x.Inscripciones).FirstAsync(x => x.Id == c.ProspectoId, ct);
            var cambio = await CambiarCorreoSiLoPideAsync(p, p.Email?.ToLowerInvariant() ?? "", c.Cuerpo!, ct);
            if (cambio is null) continue;
            db.Interacciones.Add(new Interaccion { ProspectoId = p.Id, Tipo = TipoInteraccion.Sistema, Detalle = cambio });
            await db.SaveChangesAsync(ct);
            n++;
        }
        return n;
    }

    private static bool EsAutomatico(MimeMessage m)
    {
        var precedencia = m.Headers["Precedence"]?.ToLowerInvariant() ?? "";
        if (precedencia is "auto_reply" or "bulk" or "junk") return true;
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
