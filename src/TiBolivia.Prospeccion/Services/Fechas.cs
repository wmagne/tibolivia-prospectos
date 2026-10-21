namespace TiBolivia.Prospeccion.Services;

/// <summary>Conversión de fechas de la interfaz (hora de Bolivia) a UTC para la base de datos.</summary>
public static class Fechas
{
    private static readonly TimeZoneInfo Bolivia = TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz");

    public static DateTime HoyBolivia => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Bolivia).Date;

    /// <summary>08:00 del día indicado en Bolivia, expresado en UTC (las tareas "vencen" al empezar la jornada).</summary>
    public static DateTime InicioDelDiaUtc(DateTime dia) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dia.Date.AddHours(8), DateTimeKind.Unspecified), Bolivia);

    public static DateTime FinDelDiaUtc(DateTime dia) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dia.Date.AddDays(1), DateTimeKind.Unspecified), Bolivia);

    public static DateTime ABolivia(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Bolivia);
}
