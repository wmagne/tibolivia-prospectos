using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services.Sicoes;

public record ResultadoLectura(List<SicoesProceso> Procesos, List<string> Errores);

/// <summary>
/// Convierte una lista de convocatorias (CSV o tabla copiada de una planilla) en procesos.
/// El buscador del portal SICOES pide un código de seguridad (captcha) y cambia el orden de sus columnas en cada consulta:
/// es una protección contra la lectura automática y no se la salta. Por eso el lector recibe los datos de una fuente
/// permitida (un archivo exportado, una lista pegada o una dirección de datos autorizada) y no de las páginas del buscador.
/// </summary>
public static partial class LectorSicoes
{
    // Nombres de columna que se aceptan, sin tildes ni mayúsculas. El SICOES y las planillas usan variantes.
    private static readonly Dictionary<string, string[]> Columnas = new()
    {
        ["cuce"] = ["cuce", "codigo cuce", "cuce del proceso"],
        ["entidad"] = ["entidad", "nombre entidad", "entidad contratante"],
        ["objeto"] = ["objeto", "objeto de contratacion", "objeto de la contratacion", "descripcion", "objeto del proceso"],
        ["modalidad"] = ["modalidad", "modalidad de contratacion", "tipo de proceso"],
        ["monto"] = ["monto", "precio referencial", "monto referencial", "presupuesto", "precio referencial bs", "monto bs"],
        ["departamento"] = ["departamento", "depto", "ubicacion"],
        ["publicacion"] = ["fecha publicacion", "fecha de publicacion", "publicacion", "publicado"],
        ["limite"] = ["fecha limite", "fecha de presentacion", "fecha presentacion", "presentacion de propuestas", "cierre", "fecha cierre", "fecha limite de presentacion"],
        ["enlace"] = ["enlace", "link", "url"],
        ["estado"] = ["estado", "estado del proceso"],
    };

    public static ResultadoLectura LeerTexto(string texto, string origen)
    {
        var procesos = new List<SicoesProceso>();
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(texto)) return new(procesos, ["La lista está vacía."]);

        var primera = texto.Split('\n', 2)[0];
        var separador = new[] { "\t", ";", "," }.MaxBy(s => primera.Split(s).Length)!;
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = separador,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            DetectColumnCountChanges = false,
        };
        using var csv = new CsvReader(new StringReader(texto), cfg);
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null) return new(procesos, ["No se encontró la fila de títulos."]);

        var indice = new Dictionary<string, int>();
        for (var i = 0; i < csv.HeaderRecord.Length; i++)
        {
            var h = FiltroSicoes.Normalizar(csv.HeaderRecord[i]).Trim();
            h = EspaciosRegex().Replace(h, " ");
            foreach (var (clave, nombres) in Columnas)
                if (!indice.ContainsKey(clave) && nombres.Contains(h)) indice[clave] = i;
        }
        foreach (var obligatoria in new[] { "cuce", "entidad", "objeto" })
            if (!indice.ContainsKey(obligatoria))
                errores.Add($"Falta la columna «{obligatoria}». Títulos encontrados: {string.Join(", ", csv.HeaderRecord)}");
        if (errores.Count > 0) return new(procesos, errores);

        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fila = 1;
        while (csv.Read())
        {
            fila++;
            string? Campo(string clave) => indice.TryGetValue(clave, out var i) && csv.TryGetField<string>(i, out var v)
                ? (string.IsNullOrWhiteSpace(v) ? null : v.Trim()) : null;

            var cuce = Campo("cuce");
            if (cuce is null) continue;
            if (!CuceRegex().IsMatch(cuce)) { errores.Add($"Fila {fila}: CUCE con formato raro «{cuce}», se omite."); continue; }
            if (!vistos.Add(cuce)) continue;

            var limite = Fecha(Campo("limite"));
            procesos.Add(new SicoesProceso
            {
                Cuce = cuce,
                Entidad = Recortar(Campo("entidad") ?? "(sin entidad)", 300)!,
                Objeto = Recortar(Campo("objeto") ?? "(sin objeto)", 2000)!,
                Modalidad = Recortar(Campo("modalidad"), 120),
                Monto = Monto(Campo("monto")),
                Departamento = Recortar(Campo("departamento"), 60),
                FechaPublicacion = Fecha(Campo("publicacion")) is { } fp ? AUtc(fp) : null,
                FechaLimite = limite is { } fl ? AUtc(fl) : null,
                Enlace = Recortar(Campo("enlace"), 500),
                Estado = Recortar(Campo("estado"), 80),
                Origen = origen,
            });
        }
        return new(procesos, errores);
    }

    /// <summary>Montos en formato boliviano (1.234.567,89) o con punto decimal (1,234,567.89 o 1234567.89).</summary>
    public static decimal? Monto(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = MontoLimpioRegex().Replace(s, "");
        if (t.Length == 0) return null;
        var coma = t.LastIndexOf(',');
        var punto = t.LastIndexOf('.');
        if (coma > punto) t = t.Replace(".", "").Replace(',', '.');
        else if (punto > coma && coma >= 0) t = t.Replace(",", "");
        else if (coma < 0 && t.Count(c => c == '.') > 1) t = t.Replace(".", "");
        else if (coma < 0 && punto >= 0 && t.Length - punto - 1 == 3) t = t.Replace(".", ""); // 25.000 = veinticinco mil
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>Fecha de Bolivia (dd/MM/yyyy con hora opcional, o ISO).</summary>
    public static DateTime? Fecha(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string[] formatos = ["dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm", "dd/MM/yyyy HH:mm:ss", "d/M/yyyy HH:mm", "dd/MM/yyyy", "d/M/yyyy",
            "dd-MM-yyyy HH:mm", "dd-MM-yyyy", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd"];
        return DateTime.TryParseExact(s.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    private static DateTime AUtc(DateTime bolivia) =>
        DateTime.SpecifyKind(bolivia.AddHours(4), DateTimeKind.Utc); // Bolivia es UTC-4 todo el año, sin horario de verano.

    private static string? Recortar(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"^\d{2}-\d{4,6}-\d{2}-\d{4,8}-\d-\d$|^[0-9A-Za-z][0-9A-Za-z\-/]{6,59}$")]
    private static partial Regex CuceRegex();
    [GeneratedRegex(@"[^\d,.\-]")]
    private static partial Regex MontoLimpioRegex();
    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosRegex();
}
