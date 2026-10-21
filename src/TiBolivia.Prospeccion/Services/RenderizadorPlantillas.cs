using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

/// <summary>Reemplaza variables {{Nombre}}, {{Empresa}}, etc. en asuntos y cuerpos.</summary>
public partial class RenderizadorPlantillas(IOptions<CampanasOptions> opciones)
{
    public static readonly string[] Variables =
        ["Nombre", "Contacto", "Empresa", "Cargo", "Ciudad", "Rubro", "Necesidad", "Servicio", "DescripcionServicio",
         "UrlServicio", "Comercial", "EmailComercial", "TelefonoComercial", "LinkBaja"];

    public string Renderizar(string? texto, Prospecto p, Servicio? servicio, bool html)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var valores = Valores(p, servicio);
        return VariableRegex().Replace(texto, m =>
        {
            var clave = m.Groups[1].Value;
            if (!valores.TryGetValue(clave, out var valor)) return m.Value;
            return html && clave != "LinkBaja" ? WebUtility.HtmlEncode(valor) : valor;
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
    };

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex VariableRegex();
}
