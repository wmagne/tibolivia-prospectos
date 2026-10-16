using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TiBolivia.Prospeccion.Services;

public record CorreoSaliente(string Para, string? NombrePara, string Asunto, string Html, string LinkBaja, string? ResponderA);

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
        if (!string.IsNullOrWhiteSpace(c.ResponderA)) msg.ReplyTo.Add(MailboxAddress.Parse(c.ResponderA));
        msg.Subject = c.Asunto;
        // Baja en un clic (RFC 8058): Gmail y Outlook la exigen para envíos masivos.
        msg.Headers.Add("List-Unsubscribe", $"<{c.LinkBaja}>");
        msg.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");

        var html = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#222;max-width:640px">
            {c.Html}
            <hr style="border:none;border-top:1px solid #ddd;margin-top:32px">
            <p style="font-size:11px;color:#888">Recibe este correo porque su empresa podría beneficiarse de los servicios de TI Bolivia.
            Si no desea recibir más mensajes, <a href="{c.LinkBaja}">haga clic aquí para darse de baja</a>.</p>
            </div>
            """;
        msg.Body = new BodyBuilder { HtmlBody = html, TextBody = ATexto(c.Html) + $"\n\nDarse de baja: {c.LinkBaja}" }.ToMessageBody();

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
