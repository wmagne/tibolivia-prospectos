using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

/// <summary>
/// Avisa por correo al dueño y a gerencia apenas una empresa responde de verdad (no respuestas automáticas).
/// En ventas, contestar en la primera hora multiplica las chances de cerrar: el reporte de la tarde llega tarde.
/// </summary>
public class AvisoInteresado(AppDbContext db, IEnviadorEmail enviador, IOptions<ReportesOptions> reportes,
    IOptions<CampanasOptions> campanas, ILogger<AvisoInteresado> log)
{
    public async Task EnviarAsync(Prospecto p, string canal, string? asunto, string? texto, CancellationToken ct)
    {
        var o = reportes.Value;
        if (!o.Habilitado || string.IsNullOrWhiteSpace(o.Para)) return;
        try
        {
            var servicio = await db.Inscripciones.Where(i => i.ProspectoId == p.Id)
                .OrderByDescending(i => i.CreadoEn).Select(i => i.Campana.Nombre).FirstOrDefaultAsync(ct);
            var html = new StringBuilder();
            html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:620px;color:#1f2937;\">");
            html.Append($"<p style=\"font-size:15px;\"><b>{WebUtility.HtmlEncode(p.Empresa)}</b> respondió por {WebUtility.HtmlEncode(canal)}. Contáctela hoy, mejor en la próxima hora.</p>");
            html.Append(FichaInteresado.Html(p, servicio, asunto, texto, DateTime.UtcNow, campanas.Value.UrlPublica));
            html.Append("</div>");
            var r = await enviador.EnviarAsync(new CorreoSaliente(o.Para, null, $"Nuevo interesado: {p.Empresa}", html.ToString(),
                campanas.Value.UrlPublica, p.Email, Copia: o.Copia, Interno: true), ct);
            if (!r.Exito) log.LogWarning("No se pudo enviar el aviso de interesado de {Empresa}: {Error}", p.Empresa, r.Error);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Un aviso que falla no debe frenar el registro de la respuesta.
            log.LogWarning(ex, "Error al avisar del interesado {Empresa}", p.Empresa);
        }
    }
}

/// <summary>Ficha de contacto lista para llamar, escribir por WhatsApp o responder el correo con un toque.</summary>
public static partial class FichaInteresado
{
    public static string Html(Prospecto p, string? campana, string? asunto, string? texto, DateTime? respondio, string urlPublica)
    {
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var url = urlPublica.TrimEnd('/');
        var sb = new StringBuilder();
        sb.Append("<div style=\"border:1px solid #e3e8f0;border-left:4px solid #00a8a8;border-radius:8px;padding:12px 14px;margin:10px 0;font-size:14px;\">");
        sb.Append($"<div style=\"font-size:16px;font-weight:bold;color:#0d47a1;\">{E(p.Empresa)}</div>");
        var persona = string.Join(" · ", new[] { p.NombreContacto, p.Cargo, p.Ciudad, p.Rubro }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (persona.Length > 0) sb.Append($"<div style=\"color:#6b7280;\">{E(persona)}</div>");
        if (!string.IsNullOrWhiteSpace(campana)) sb.Append($"<div style=\"color:#6b7280;\">Campaña: {E(campana)}</div>");
        if (respondio is { } r) sb.Append($"<div style=\"color:#6b7280;\">Respondió: {Fechas.ABolivia(r):dd/MM HH:mm}</div>");

        sb.Append("<div style=\"margin:8px 0;\">");
        var tel = Telefonos.Normalizar(p.Telefono);
        if (tel is not null)
        {
            sb.Append($"📞 <a href=\"tel:+{tel}\" style=\"color:#0d47a1;font-weight:bold;\">+{tel}</a> ");
            sb.Append($"· <a href=\"https://wa.me/{tel}\" style=\"color:#00a884;font-weight:bold;\">WhatsApp</a><br>");
        }
        if (!string.IsNullOrWhiteSpace(p.Email) && !p.BajaEmail)
        {
            var re = Uri.EscapeDataString(asunto is { Length: > 0 } a ? (a.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? a : "Re: " + a) : $"TIBolivia - {p.Empresa}");
            sb.Append($"✉ <a href=\"mailto:{E(p.Email)}?subject={re}\" style=\"color:#0d47a1;font-weight:bold;\">{E(p.Email)}</a><br>");
        }
        if (!string.IsNullOrWhiteSpace(p.SitioWeb)) sb.Append($"🌐 <a href=\"{E(p.SitioWeb)}\" style=\"color:#0d47a1;\">{E(p.SitioWeb)}</a>");
        sb.Append("</div>");

        var extracto = Extracto(texto);
        if (extracto.Length > 0)
            sb.Append($"<div style=\"background:#f3f6fa;border-radius:6px;padding:8px 10px;color:#374151;\"><i>«{E(extracto)}»</i></div>");
        sb.Append($"<div style=\"margin-top:8px;\"><a href=\"{url}/Prospectos/Detalle/{p.Id}\" style=\"color:#1565c0;\">Abrir ficha en el sistema</a></div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>Lo que escribió la persona, sin el correo nuestro citado debajo.</summary>
    public static string Extracto(string? texto, int max = 280)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var t = texto.Replace("\r", "");
        var corte = CitaRegex().Match(t);
        if (corte.Success && corte.Index > 0) t = t[..corte.Index];
        t = Regex.Replace(Regex.Replace(t, "<[^>]+>", " "), @"\s+", " ").Trim();
        return t.Length > max ? t[..max] + "…" : t;
    }

    [GeneratedRegex(@"(\n\s*>|\nEl .{5,80} escribió:|\nOn .{5,80} wrote:|\n-{2,} ?Mensaje original|\nDe: |\nFrom: )", RegexOptions.IgnoreCase)]
    private static partial Regex CitaRegex();
}
