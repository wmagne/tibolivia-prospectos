namespace TiBolivia.Prospeccion.Services;

public static class Telefonos
{
    /// <summary>Normaliza a solo dígitos con código de país. Números bolivianos de 8 dígitos reciben el prefijo 591.</summary>
    public static string? Normalizar(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return null;
        var digitos = new string(telefono.Where(char.IsDigit).ToArray());
        if (digitos.StartsWith("00")) digitos = digitos[2..];
        if (digitos.Length == 8 && (digitos[0] == '6' || digitos[0] == '7')) digitos = "591" + digitos;
        return digitos.Length >= 8 ? digitos : null;
    }
}
