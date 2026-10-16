using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

public class ServicioInscripciones(AppDbContext db, HorarioLaboral horario, TimeProvider tiempo)
{
    /// <summary>Inscribe prospectos en una campaña. Devuelve cuántos se inscribieron (omite duplicados y "no contactar").</summary>
    public async Task<int> InscribirAsync(int campanaId, IEnumerable<int> prospectoIds, string? usuarioId, CancellationToken ct = default)
    {
        var campana = await db.Campanas.Include(c => c.Pasos).FirstOrDefaultAsync(c => c.Id == campanaId, ct)
            ?? throw new InvalidOperationException("La campaña no existe.");
        var primerPaso = campana.Pasos.OrderBy(p => p.Orden).FirstOrDefault()
            ?? throw new InvalidOperationException("La campaña no tiene pasos.");

        var ids = prospectoIds.Distinct().ToList();
        var yaInscritos = await db.Inscripciones
            .Where(i => i.CampanaId == campanaId && ids.Contains(i.ProspectoId))
            .Select(i => i.ProspectoId).ToListAsync(ct);
        var validos = await db.Prospectos
            .Where(p => ids.Contains(p.Id) && !yaInscritos.Contains(p.Id) && p.Estado != EstadoProspecto.NoContactar)
            .Select(p => p.Id).ToListAsync(ct);

        var inicio = horario.Ajustar(tiempo.GetUtcNow().UtcDateTime.AddDays(primerPaso.DiasEspera));
        foreach (var id in validos)
        {
            db.Inscripciones.Add(new InscripcionCampana
            {
                CampanaId = campanaId,
                ProspectoId = id,
                ProximoEnvio = inicio,
                InscritoPorId = usuarioId,
            });
        }
        await db.SaveChangesAsync(ct);
        return validos.Count;
    }

    public async Task CambiarEstadoAsync(int inscripcionId, EstadoInscripcion estado, CancellationToken ct = default)
    {
        var i = await db.Inscripciones.Include(x => x.Campana).ThenInclude(c => c.Pasos)
            .FirstOrDefaultAsync(x => x.Id == inscripcionId, ct);
        if (i is null) return;
        i.Estado = estado;
        if (estado == EstadoInscripcion.Activa)
        {
            var pasos = i.Campana.Pasos.OrderBy(p => p.Orden).ToList();
            i.ProximoEnvio = i.PasoActual < pasos.Count ? horario.Ajustar(tiempo.GetUtcNow().UtcDateTime) : null;
        }
        else
        {
            i.ProximoEnvio = null;
        }
        await db.SaveChangesAsync(ct);
    }
}
