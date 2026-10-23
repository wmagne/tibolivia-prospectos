using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

/// <summary>
/// Inscribe de a poco, en horario laboral, a los prospectos con correo que todavía no están en ninguna campaña.
/// El cupo diario se reparte entre las horas de la jornada para no enviar todo de golpe (cuida la reputación del servidor de correo).
/// Se detiene sola si la campaña se desactiva.
/// </summary>
public class InscripcionAutomatica(AppDbContext db, ServicioInscripciones inscripciones, HorarioLaboral horario,
    IOptions<CampanasOptions> opciones, TimeProvider tiempo, ILogger<InscripcionAutomatica> log)
{
    public async Task<int> EjecutarAsync(CancellationToken ct)
    {
        var o = opciones.Value.AutoInscripcion;
        var ahora = tiempo.GetUtcNow().UtcDateTime;
        if (!o.Habilitada || o.MaximoPorDia <= 0 || !horario.EsHorarioLaboral(ahora)) return 0;

        var campana = await db.Campanas.FirstOrDefaultAsync(c => c.Nombre == o.Campana && c.Activa, ct);
        if (campana is null) return 0;

        // Cupo acumulado hasta la hora actual: p. ej. 40 por día en 10 horas = 4 por hora.
        var local = horario.ALocal(ahora);
        var horas = Math.Max(1, opciones.Value.HoraFin - opciones.Value.HoraInicio);
        var transcurridas = Math.Clamp(local.Hour - opciones.Value.HoraInicio + 1, 1, horas);
        var permitidas = (int)Math.Ceiling(o.MaximoPorDia * (double)transcurridas / horas);
        var desde = Fechas.InicioDelDiaUtc(local.Date);
        var hoy = await db.Inscripciones.CountAsync(i => i.CampanaId == campana.Id && i.CreadoEn >= desde, ct);
        var cupo = permitidas - hoy;
        if (cupo <= 0) return 0;

        var ids = await db.Prospectos
            .Where(p => p.Email != null && p.Email != "" && !p.BajaEmail && p.Estado == EstadoProspecto.Nuevo && !p.Inscripciones.Any())
            .OrderBy(p => p.Prioridad ?? "Z").ThenBy(p => p.DatosPorVerificar).ThenBy(p => p.Id)
            .Select(p => p.Id).Take(cupo).ToListAsync(ct);
        if (ids.Count == 0) return 0;

        var n = await inscripciones.InscribirAsync(campana.Id, ids, null, ct);
        log.LogInformation("Inscripción automática: {Cantidad} prospectos inscritos en \"{Campana}\"", n, campana.Nombre);
        return n;
    }
}
