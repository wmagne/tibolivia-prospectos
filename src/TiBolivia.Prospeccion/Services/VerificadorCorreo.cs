using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TiBolivia.Prospeccion.Services;

public enum EstadoVerificacion { Valido, Invalido, Desconocido }

public record ResultadoVerificacion(EstadoVerificacion Estado, string Motivo)
{
    public static ResultadoVerificacion Valido(string motivo = "ok") => new(EstadoVerificacion.Valido, motivo);
    public static ResultadoVerificacion Invalido(string motivo) => new(EstadoVerificacion.Invalido, motivo);
    public static ResultadoVerificacion Desconocido(string motivo) => new(EstadoVerificacion.Desconocido, motivo);
}

public class VerificacionCorreoOptions
{
    public bool Habilitada { get; set; } = true;
    /// <summary>Además del dominio, pregunta al servidor de destino si la casilla existe (sin enviar ningún mensaje).</summary>
    public bool ProbarBuzon { get; set; } = true;
    /// <summary>Nombre con el que el sistema se presenta al servidor de destino (EHLO).</summary>
    public string NombreHelo { get; set; } = "prospectos.tibolivia.com";
    public int TimeoutSegundos { get; set; } = 10;
    /// <summary>Cuánto se recuerda que una dirección está bien, para no volver a preguntar en cada paso de la campaña.</summary>
    public int DiasCacheValido { get; set; } = 14;
}

public interface IVerificadorCorreo
{
    Task<ResultadoVerificacion> VerificarAsync(string email, CancellationToken ct);
}

/// <summary>Consulta DNS (separada para poder probar el verificador sin red).</summary>
public interface IConsultaDns
{
    /// <summary>Servidores de correo del dominio por prioridad; vacío si no tiene MX; null si el DNS no respondió.</summary>
    Task<List<string>?> ServidoresCorreoAsync(string dominio, CancellationToken ct);
    /// <summary>true si el dominio tiene dirección (A/AAAA); null si el DNS no respondió.</summary>
    Task<bool?> TieneDireccionAsync(string dominio, CancellationToken ct);
}

/// <summary>Conversación SMTP mínima hasta RCPT TO. Devuelve el código de respuesta al RCPT, o null si no se pudo hablar con el servidor.</summary>
public interface ISondaSmtp
{
    Task<(int Codigo, string Texto)?> ProbarAsync(string servidor, string email, CancellationToken ct);
}

/// <summary>
/// Comprueba un correo antes de enviarle una campaña. Un rebote le cuesta reputación a todo el dominio tibolivia.com:
/// con la mitad de los envíos rebotando, Gmail y Outlook empiezan a mandar a spam también los correos buenos.
/// Solo descarta lo seguro (dirección mal escrita, dominio que no recibe correo, casilla que el servidor dice que no existe);
/// ante cualquier duda (DNS o servidor que no responde, servidor que acepta todo) deja enviar.
/// </summary>
public class VerificadorCorreo(IConsultaDns dns, ISondaSmtp sonda, IOptions<VerificacionCorreoOptions> opciones, TimeProvider tiempo,
    ILogger<VerificadorCorreo> log) : IVerificadorCorreo
{
    // Se registra como singleton: la caché vive mientras corre el sistema.
    private readonly ConcurrentDictionary<string, (ResultadoVerificacion Resultado, DateTime Hasta)> Cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ResultadoVerificacion> VerificarAsync(string email, CancellationToken ct)
    {
        var o = opciones.Value;
        if (!o.Habilitada) return ResultadoVerificacion.Desconocido("Comprobación desactivada");
        email = (email ?? "").Trim();
        var ahora = tiempo.GetUtcNow().UtcDateTime;
        if (Cache.TryGetValue(email, out var c) && c.Hasta > ahora) return c.Resultado;

        var r = await VerificarSinCacheAsync(email, o, ct);
        // Solo se recuerda lo que está bien: lo inválido queda marcado en el prospecto y lo desconocido se vuelve a intentar.
        if (r.Estado == EstadoVerificacion.Valido) Cache[email] = (r, ahora.AddDays(o.DiasCacheValido));
        if (r.Estado != EstadoVerificacion.Valido) log.LogInformation("Correo {Email}: {Estado} ({Motivo})", email, r.Estado, r.Motivo);
        return r;
    }

    private async Task<ResultadoVerificacion> VerificarSinCacheAsync(string email, VerificacionCorreoOptions o, CancellationToken ct)
    {
        if (!FormatoValido(email)) return ResultadoVerificacion.Invalido("La dirección está mal escrita");
        var dominio = email[(email.LastIndexOf('@') + 1)..].ToLowerInvariant();

        var servidores = await dns.ServidoresCorreoAsync(dominio, ct);
        if (servidores is null) return ResultadoVerificacion.Desconocido("El DNS no respondió");
        if (servidores.Count == 0)
        {
            // Sin MX, la norma permite entregar al propio dominio si tiene dirección.
            var tiene = await dns.TieneDireccionAsync(dominio, ct);
            if (tiene is null) return ResultadoVerificacion.Desconocido("El DNS no respondió");
            if (tiene == false) return ResultadoVerificacion.Invalido($"El dominio {dominio} no existe o no recibe correo");
            servidores = [dominio];
        }
        if (!o.ProbarBuzon) return ResultadoVerificacion.Valido("El dominio recibe correo");

        foreach (var servidor in servidores.Take(2))
        {
            var respuesta = await sonda.ProbarAsync(servidor, email, ct);
            if (respuesta is null) continue; // no respondió: se prueba el siguiente
            var (codigo, texto) = respuesta.Value;
            if (codigo is >= 200 and < 300) return ResultadoVerificacion.Valido();
            // 550/551/553 (o 5.1.x) = la casilla no existe. Otros 5xx (política, lista negra) no prueban que no exista.
            if (codigo is 550 or 551 or 553 && CasillaInexistente(texto))
                return ResultadoVerificacion.Invalido($"El servidor de {dominio} dice que la casilla no existe");
            return ResultadoVerificacion.Desconocido($"El servidor respondió {codigo}");
        }
        return ResultadoVerificacion.Desconocido("El servidor de correo no respondió");
    }

    public static bool FormatoValido(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || email.Count(ch => ch == '@') != 1 || email.Any(char.IsWhiteSpace)) return false;
        if (!MailboxAddress.TryParse(email, out var m) || !string.Equals(m.Address, email, StringComparison.OrdinalIgnoreCase)) return false;
        var dominio = email[(email.IndexOf('@') + 1)..];
        return dominio.Contains('.') && !dominio.StartsWith('.') && !dominio.EndsWith('.') && !dominio.Contains("..");
    }

    /// <summary>
    /// Un 550 también se usa para "lo bloqueamos" o "está en lista negra": eso no prueba que la casilla no exista.
    /// Se descarta solo si el texto o el código ampliado hablan de usuario o casilla.
    /// </summary>
    public static bool CasillaInexistente(string texto)
    {
        var t = texto.ToLowerInvariant();
        if (t.Contains("5.7.") || t.Contains("blocked") || t.Contains("blacklist") || t.Contains("spamhaus") || t.Contains("policy")) return false;
        return t.Contains("5.1.1") || t.Contains("5.1.0") || t.Contains("5.1.10") || t.Contains("user unknown") || t.Contains("unknown user")
            || t.Contains("no such user") || t.Contains("does not exist") || t.Contains("doesn't exist") || t.Contains("mailbox unavailable")
            || t.Contains("mailbox not found") || t.Contains("recipient not found") || t.Contains("invalid recipient") || t.Contains("unknown recipient")
            || t.Contains("address rejected") || t.Contains("no mailbox") || t.Contains("not exist") || t.Contains("inexistente") || t.Contains("no existe");
    }
}

public class ConsultaDns(ILogger<ConsultaDns> log) : IConsultaDns
{
    private static readonly LookupClient Cliente = new(new LookupClientOptions { Timeout = TimeSpan.FromSeconds(5), Retries = 1, UseCache = true });

    public async Task<List<string>?> ServidoresCorreoAsync(string dominio, CancellationToken ct)
    {
        try
        {
            var r = await Cliente.QueryAsync(dominio, QueryType.MX, cancellationToken: ct);
            if (r.HasError && r.Header.ResponseCode != DnsHeaderResponseCode.NotExistentDomain) return null;
            return r.Answers.MxRecords().OrderBy(m => m.Preference).Select(m => m.Exchange.Value.TrimEnd('.'))
                .Where(h => h.Length > 0).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogDebug(ex, "DNS MX de {Dominio}", dominio);
            return null;
        }
    }

    public async Task<bool?> TieneDireccionAsync(string dominio, CancellationToken ct)
    {
        try
        {
            var r = await Cliente.QueryAsync(dominio, QueryType.A, cancellationToken: ct);
            if (r.HasError && r.Header.ResponseCode != DnsHeaderResponseCode.NotExistentDomain) return null;
            if (r.Answers.ARecords().Any()) return true;
            var r6 = await Cliente.QueryAsync(dominio, QueryType.AAAA, cancellationToken: ct);
            return r6.Answers.AaaaRecords().Any();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogDebug(ex, "DNS A de {Dominio}", dominio);
            return null;
        }
    }
}

/// <summary>Habla con el servidor de destino solo hasta RCPT TO y se despide: nunca envía DATA, así que no llega ningún mensaje.</summary>
public class SondaSmtp(IOptions<VerificacionCorreoOptions> opciones, IOptions<SmtpOptions> smtp, ILogger<SondaSmtp> log) : ISondaSmtp
{
    public async Task<(int Codigo, string Texto)?> ProbarAsync(string servidor, string email, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, opciones.Value.TimeoutSegundos)));
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(servidor, 25, limite.Token);
            await using var flujo = tcp.GetStream();
            using var lector = new StreamReader(flujo, Encoding.ASCII);
            await using var escritor = new StreamWriter(flujo, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            if ((await LeerAsync(lector, limite.Token)).Codigo != 220) return null;
            await escritor.WriteLineAsync($"EHLO {opciones.Value.NombreHelo}".AsMemory(), limite.Token);
            if ((await LeerAsync(lector, limite.Token)).Codigo != 250) return null;
            await escritor.WriteLineAsync($"MAIL FROM:<{smtp.Value.RemitenteEmail}>".AsMemory(), limite.Token);
            if ((await LeerAsync(lector, limite.Token)).Codigo != 250) return null;
            await escritor.WriteLineAsync($"RCPT TO:<{email}>".AsMemory(), limite.Token);
            var rcpt = await LeerAsync(lector, limite.Token);
            try { await escritor.WriteLineAsync("QUIT".AsMemory(), limite.Token); } catch { /* el resultado ya está */ }
            return rcpt.Codigo > 0 ? rcpt : null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogDebug(ex, "Sonda SMTP a {Servidor}", servidor);
            return null;
        }
    }

    /// <summary>Lee una respuesta SMTP, de una o varias líneas ("250-..." sigue, "250 ..." termina).</summary>
    private static async Task<(int Codigo, string Texto)> LeerAsync(StreamReader lector, CancellationToken ct)
    {
        var texto = new StringBuilder();
        for (var i = 0; i < 50; i++)
        {
            var linea = await lector.ReadLineAsync(ct);
            if (linea is null) break;
            texto.AppendLine(linea);
            if (linea.Length < 4 || linea[3] != '-')
                return (int.TryParse(linea.AsSpan(0, Math.Min(3, linea.Length)), out var codigo) ? codigo : 0, texto.ToString());
        }
        return (0, texto.ToString());
    }
}
