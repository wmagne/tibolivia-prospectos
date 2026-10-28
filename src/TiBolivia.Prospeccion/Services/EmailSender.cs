using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TiBolivia.Prospeccion.Services;

public record CorreoSaliente(string Para, string? NombrePara, string Asunto, string Html, string LinkBaja, string? ResponderA,
    string? AdjuntoNombre = null, byte[]? AdjuntoContenido = null, string? Copia = null, bool Interno = false);

public interface IEnviadorEmail
{
    Task<ResultadoEnvio> EnviarAsync(CorreoSaliente correo, CancellationToken ct);
}

public partial class EnviadorEmailSmtp(IOptions<SmtpOptions> opciones, ILogger<EnviadorEmailSmtp> log) : IEnviadorEmail
{
    public async Task<ResultadoEnvio> EnviarAsync(CorreoSaliente c, CancellationToken ct)
    {
        var o = opciones.Value;
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(o.RemitenteNombre, o.RemitenteEmail));
        msg.To.Add(new MailboxAddress(c.NombrePara ?? "", c.Para));
        foreach (var cc in (c.Copia ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            msg.Cc.Add(MailboxAddress.Parse(cc));
        if (!string.IsNullOrWhiteSpace(c.ResponderA)) msg.ReplyTo.Add(MailboxAddress.Parse(c.ResponderA));
        msg.Subject = c.Asunto;
        if (!c.Interno)
        {
            // Baja en un clic (RFC 8058): Gmail y Outlook la exigen para envíos masivos.
            msg.Headers.Add("List-Unsubscribe", $"<{c.LinkBaja}>");
            msg.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        // Los correos internos (reportes) llevan su propio HTML, sin pie de baja.
        var html = c.Interno ? c.Html : DisenoCorreo.Envolver(c.Html, c.LinkBaja);
        var cuerpo = new BodyBuilder { HtmlBody = html, TextBody = ATexto(c.Html) + (c.Interno ? "" : $"\n\nDarse de baja: {c.LinkBaja}") };
        if (c.AdjuntoContenido is { Length: > 0 } && !string.IsNullOrWhiteSpace(c.AdjuntoNombre))
            cuerpo.Attachments.Add(c.AdjuntoNombre, c.AdjuntoContenido);
        msg.Body = cuerpo.ToMessageBody();

        if (o.Simulacion)
        {
            log.LogInformation("[SIMULACIÓN] Correo a {Para}: {Asunto}", c.Para, c.Asunto);
            return ResultadoEnvio.Ok($"sim-{msg.MessageId}");
        }

        try
        {
            using var smtp = new SmtpClient();
            var seguridad = o.UsarStartTls ? SecureSocketOptions.StartTls
                : o.Port == 465 ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.Auto;
            await smtp.ConnectAsync(o.Host!, o.Port, seguridad, ct);
            if (!string.IsNullOrEmpty(o.Usuario)) await smtp.AuthenticateAsync(o.Usuario, o.Password ?? "", ct);
            await smtp.SendAsync(msg, ct);
            await smtp.DisconnectAsync(true, ct);
            return ResultadoEnvio.Ok(msg.MessageId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Error enviando correo a {Para}", c.Para);
            return ResultadoEnvio.Falla(ex.Message);
        }
    }

    private static string ATexto(string html) =>
        System.Net.WebUtility.HtmlDecode(TagRegex().Replace(BrRegex().Replace(html, "\n"), "")).Trim();

    [GeneratedRegex(@"<(br|/p|/div|/li)\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrRegex();
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}
