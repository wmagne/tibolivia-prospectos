using Microsoft.Extensions.Options;

namespace TiBolivia.Prospeccion.Services;

/// <summary>Asegura que los envíos salgan en horario laboral de Bolivia.</summary>
public class HorarioLaboral(IOptions<CampanasOptions> opciones)
{
    private readonly CampanasOptions _o = opciones.Value;
    private readonly TimeZoneInfo _zona = TimeZoneInfo.FindSystemTimeZoneById(opciones.Value.ZonaHoraria);

    public bool EsHorarioLaboral(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, _zona);
        return DiaHabil(local) && local.Hour >= _o.HoraInicio && local.Hour < _o.HoraFin;
    }

    /// <summary>Devuelve la misma fecha si cae en horario laboral; si no, el siguiente inicio de jornada.</summary>
    public DateTime Ajustar(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, _zona);
        for (var i = 0; i < 14; i++)
        {
            if (!DiaHabil(local) || local.Hour >= _o.HoraFin)
                local = local.Date.AddDays(1).AddHours(_o.HoraInicio);
            else if (local.Hour < _o.HoraInicio)
                local = local.Date.AddHours(_o.HoraInicio);
            else
                break;
        }
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _zona);
    }

    public DateTime ALocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zona);

    private bool DiaHabil(DateTime local) =>
        _o.EnviarFinDeSemana || (local.DayOfWeek != DayOfWeek.Saturday && local.DayOfWeek != DayOfWeek.Sunday);
}
