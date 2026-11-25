using System.Globalization;
using System.Text;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services.Sicoes;

public record EvaluacionSicoes(int Puntaje, string Motivo)
{
    public bool EsRelevante => Puntaje >= FiltroSicoes.UmbralOportunidad;
}

/// <summary>
/// Puntaje de 0 a 100 de cuánto encaja un proceso con un perfil. Lo que pesa es el objeto de la compra:
/// sin ninguna palabra clave no llega al umbral aunque el departamento y el monto encajen, porque una alerta
/// que no le sirve al cliente es la forma más rápida de que deje de leerlas.
/// </summary>
public static class FiltroSicoes
{
    public const int UmbralOportunidad = 60;

    public static EvaluacionSicoes Evaluar(SicoesProceso p, SicoesPerfil perfil)
    {
        var texto = Tokens(p.Objeto + " " + p.Modalidad);

        var excluida = Lista(perfil.PalabrasExcluidas).FirstOrDefault(x => Contiene(texto, Tokens(x)));
        if (excluida is not null) return new(0, $"Excluido por «{excluida}»");

        if (p.Monto is { } monto && perfil.MontoMinimo > 0 && monto < perfil.MontoMinimo)
            return new(0, $"Monto Bs {monto:N0} menor al mínimo de Bs {perfil.MontoMinimo:N0}");

        var departamentos = Lista(perfil.Departamentos);
        var esNacional = string.IsNullOrWhiteSpace(p.Departamento) || Normalizar(p.Departamento).Contains("nacional");
        var enDepartamento = departamentos.Count == 0
            || departamentos.Any(d => !string.IsNullOrWhiteSpace(p.Departamento) && Normalizar(p.Departamento!).Contains(Normalizar(d)));
        if (!enDepartamento && !(esNacional && perfil.IncluyeNacional))
            return new(0, $"Fuera de los departamentos del perfil ({p.Departamento})");

        // "computadora" y "computadoras" son la misma palabra clave: contarlas dos veces inflaría el puntaje.
        var coincidencias = Lista(perfil.PalabrasClave).Where(k => Contiene(texto, Tokens(k)))
            .DistinctBy(k => string.Join(' ', Tokens(k).Select(Raiz))).ToList();
        var motivos = new List<string>();
        var puntaje = 0;
        if (coincidencias.Count > 0)
        {
            // La primera palabra clave ya deja al proceso cerca del umbral; las demás confirman que es del rubro.
            puntaje += 55 + Math.Min(20, (coincidencias.Count - 1) * 10);
            motivos.Add("Palabras: " + string.Join(", ", coincidencias.Take(5)));
        }
        else motivos.Add("Sin palabras clave");

        if (departamentos.Count > 0 && enDepartamento) { puntaje += 15; motivos.Add(p.Departamento!); }
        else if (esNacional) { puntaje += 10; motivos.Add("Alcance nacional"); }
        else { puntaje += 10; motivos.Add(p.Departamento ?? "Departamento sin dato"); }

        // La Consulta de Precios da solo unos días para mejorar la oferta: vale más enterarse primero.
        if (p.EsConsultaDePrecios) { puntaje += 10; motivos.Add("Consulta de Precios"); }
        if (p.Monto is not null && perfil.MontoMinimo > 0) { puntaje += 5; }

        return new(Math.Clamp(puntaje, 0, 100), string.Join(" · ", motivos));
    }

    public static List<string> Lista(string? valores) =>
        (valores ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Busca la frase como palabras seguidas, aceptando singular y plural ("computadora" encuentra "computadoras").</summary>
    private static bool Contiene(string[] texto, string[] frase)
    {
        if (frase.Length == 0 || frase.Length > texto.Length) return false;
        for (var i = 0; i <= texto.Length - frase.Length; i++)
        {
            var ok = true;
            for (var j = 0; j < frase.Length && ok; j++) ok = MismaPalabra(texto[i + j], frase[j]);
            if (ok) return true;
        }
        return false;
    }

    private static bool MismaPalabra(string a, string b) =>
        a == b || a == b + "s" || a == b + "es" || b == a + "s" || b == a + "es";

    private static string Raiz(string palabra) =>
        palabra.EndsWith("es") && palabra.Length > 4 ? palabra[..^2] : palabra.EndsWith('s') && palabra.Length > 3 ? palabra[..^1] : palabra;

    private static string[] Tokens(string? s) =>
        Normalizar(s).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Minúsculas, sin tildes y sin signos: en el SICOES el mismo objeto aparece en mayúsculas, con y sin tilde.</summary>
    public static string Normalizar(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return sb.ToString();
    }
}
