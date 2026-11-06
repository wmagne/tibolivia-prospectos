using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

/// <summary>Reemplaza variables {{Nombre}}, {{Empresa}}, etc. en asuntos y cuerpos.</summary>
public partial class RenderizadorPlantillas(IOptions<CampanasOptions> opciones, IOptions<SmtpOptions> smtp, IOptionsMonitor<WhatsAppOptions> whatsApp)
{
    public static readonly string[] Variables =
        ["Nombre", "Contacto", "Empresa", "Cargo", "Ciudad", "Rubro", "Necesidad", "Servicio", "DescripcionServicio",
         "UrlServicio", "Comercial", "EmailComercial", "TelefonoComercial", "LinkBaja",
         "TitularRubro", "PropuestaRubro", "BeneficiosRubro", "BotonesContacto"];

    /// <summary>Variables que ya son HTML armado por el sistema (no se codifican).</summary>
    private static readonly HashSet<string> VariablesHtml = new(StringComparer.OrdinalIgnoreCase) { "LinkBaja", "BeneficiosRubro", "BotonesContacto" };

    public string Renderizar(string? texto, Prospecto p, Servicio? servicio, bool html)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var valores = Valores(p, servicio);
        return VariableRegex().Replace(texto, m =>
        {
            var clave = m.Groups[1].Value;
            if (!valores.TryGetValue(clave, out var valor)) return m.Value;
            if (!html && VariablesHtml.Contains(clave) && clave != "LinkBaja") return valor.Contains('<') ? "" : valor;
            return html && !VariablesHtml.Contains(clave) ? WebUtility.HtmlEncode(valor) : valor;
        });
    }

    public string LinkBaja(Prospecto p) => $"{opciones.Value.UrlPublica.TrimEnd('/')}/baja/{p.TokenBaja}";

    private Dictionary<string, string> Valores(Prospecto p, Servicio? s) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Nombre"] = string.IsNullOrEmpty(p.NombreSaludo) ? "estimado/a" : p.NombreSaludo,
        ["Contacto"] = p.NombreContacto ?? "",
        ["Empresa"] = p.Empresa,
        ["Cargo"] = p.Cargo ?? "",
        ["Ciudad"] = p.Ciudad ?? "",
        ["Rubro"] = p.Rubro ?? "",
        // Qué ofrecerle a esta empresa (del análisis del prospecto); si falta, la oferta general de TIBolivia.
        ["Necesidad"] = string.IsNullOrWhiteSpace(p.Necesidad)
            ? "apps móviles, sistemas CRM y correo corporativo con dominio propio"
            : char.ToLowerInvariant(p.Necesidad.Trim()[0]) + p.Necesidad.Trim()[1..].TrimEnd('.'),
        ["Servicio"] = s?.Nombre ?? "",
        ["DescripcionServicio"] = s?.Descripcion ?? "",
        ["UrlServicio"] = s?.UrlInformacion ?? "",
        ["Comercial"] = p.AsignadoA?.NombreCompleto ?? "Equipo comercial TI Bolivia",
        ["EmailComercial"] = p.AsignadoA?.Email ?? "",
        ["TelefonoComercial"] = p.AsignadoA?.PhoneNumber ?? "",
        ["LinkBaja"] = LinkBaja(p),
        ["TitularRubro"] = MensajesPorRubro.Para(p.Rubro).Titular,
        ["PropuestaRubro"] = MensajesPorRubro.Para(p.Rubro).Parrafo,
        ["BeneficiosRubro"] = Beneficios(p),
        ["BotonesContacto"] = Botones(p),
    };

    private static string Beneficios(Prospecto p) =>
        "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:12px 0 16px;\">" +
        string.Concat(MensajesPorRubro.Para(p.Rubro).Beneficios.Select(b =>
            $"<tr><td style=\"color:#00a8a8;font-size:18px;padding:4px 10px 4px 0;vertical-align:top;\">&#10004;</td>" +
            $"<td style=\"padding:4px 0;font-size:15px;\">{WebUtility.HtmlEncode(b)}</td></tr>")) +
        "</table>";

    /// <summary>Botones "Responder por correo" y "Escribir por WhatsApp" (al celular del comercial asignado).</summary>
    /// <summary>59170935349 → 709 35349 (como lo escriben los bolivianos).</summary>
    private static string Visible(string numero)
    {
        var local = numero.StartsWith("591") ? numero[3..] : numero;
        return local.Length == 8 ? $"{local[..3]} {local[3..]}" : local;
    }

    private string Botones(Prospecto p)
    {
        var correo = p.AsignadoA?.Email ?? smtp.Value.RemitenteEmail;
        var asunto = Uri.EscapeDataString($"Diagnóstico gratuito - {p.Empresa}");
        var b = $"<a href=\"mailto:{correo}?subject={asunto}\" style=\"display:inline-block;background:#1565c0;color:#ffffff;text-decoration:none;" +
                "font-weight:bold;padding:12px 20px;border-radius:8px;margin:4px 8px 4px 0;\">Quiero mi diagnóstico gratuito</a>";
        // Dos números: el oficial (conectado al sistema, la conversación queda registrada) y el del comercial (celular).
        var texto = Uri.EscapeDataString($"Hola, soy de {p.Empresa}. Me interesa el diagnóstico gratuito de TIBolivia.");
        var oficial = Telefonos.Normalizar(whatsApp.CurrentValue.NumeroPublico);
        var comercial = Telefonos.Normalizar(whatsApp.CurrentValue.NumeroComercial) ?? Telefonos.Normalizar(p.AsignadoA?.PhoneNumber);
        if (oficial is not null)
            b += $"<a href=\"https://wa.me/{oficial}?text={texto}\" style=\"display:inline-block;background:#25d366;color:#ffffff;text-decoration:none;" +
                 $"font-weight:bold;padding:12px 20px;border-radius:8px;margin:4px 8px 4px 0;\">WhatsApp {Visible(oficial)}</a>";
        if (comercial is not null && comercial != oficial)
            b += $"<a href=\"https://wa.me/{comercial}?text={texto}\" style=\"display:inline-block;background:#128c7e;color:#ffffff;text-decoration:none;" +
                 $"font-weight:bold;padding:12px 20px;border-radius:8px;margin:4px 0;\">WhatsApp {Visible(comercial)}</a>";
        return $"<div style=\"margin:20px 0 8px;\">{b}</div>";
    }

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex VariableRegex();
}
