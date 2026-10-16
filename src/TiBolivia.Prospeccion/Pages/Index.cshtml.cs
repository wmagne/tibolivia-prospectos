using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public Dictionary<EstadoProspecto, int> PorEstado { get; private set; } = [];
    public int TotalProspectos { get; private set; }
    public int EnviadosHoy { get; private set; }
    public int Enviados7Dias { get; private set; }
    public int Respuestas7Dias { get; private set; }
    public int Fallidos7Dias { get; private set; }
    public int InscripcionesActivas { get; private set; }
    public List<Prospecto> PorAtender { get; private set; } = [];
    public List<(string Campana, int Activas, int Completadas, int Pausadas)> Campanas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var hoy = DateTime.UtcNow.Date;
        var hace7 = DateTime.UtcNow.AddDays(-7);
        var usuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        PorEstado = await db.Prospectos.GroupBy(p => p.Estado).ToDictionaryAsync(g => g.Key, g => g.Count());
        TotalProspectos = PorEstado.Values.Sum();

        var salientes = db.Mensajes.Where(m => m.Direccion == DireccionMensaje.Saliente);
        EnviadosHoy = await salientes.CountAsync(m => m.CreadoEn >= hoy && m.Estado != EstadoMensaje.Fallido && m.Estado != EstadoMensaje.Omitido);
        Enviados7Dias = await salientes.CountAsync(m => m.CreadoEn >= hace7 && m.Estado != EstadoMensaje.Fallido && m.Estado != EstadoMensaje.Omitido);
        Fallidos7Dias = await salientes.CountAsync(m => m.CreadoEn >= hace7 && m.Estado == EstadoMensaje.Fallido);
        Respuestas7Dias = await db.Mensajes.CountAsync(m => m.Direccion == DireccionMensaje.Entrante && m.CreadoEn >= hace7);
        InscripcionesActivas = await db.Inscripciones.CountAsync(i => i.Estado == EstadoInscripcion.Activa);

        // Prospectos que respondieron y esperan atención de un comercial (primero los asignados al usuario actual).
        PorAtender = await db.Prospectos.Include(p => p.AsignadoA)
            .Where(p => p.Estado == EstadoProspecto.Interesado && p.UltimaRespuestaEn != null)
            .OrderByDescending(p => p.AsignadoAId == usuarioId).ThenByDescending(p => p.UltimaRespuestaEn)
            .Take(15).ToListAsync();

        Campanas = (await db.Campanas
            .Select(c => new
            {
                c.Nombre,
                Activas = c.Inscripciones.Count(i => i.Estado == EstadoInscripcion.Activa),
                Completadas = c.Inscripciones.Count(i => i.Estado == EstadoInscripcion.Completada),
                Pausadas = c.Inscripciones.Count(i => i.Estado == EstadoInscripcion.Pausada),
            }).ToListAsync())
            .Select(c => (c.Nombre, c.Activas, c.Completadas, c.Pausadas)).ToList();
    }
}
