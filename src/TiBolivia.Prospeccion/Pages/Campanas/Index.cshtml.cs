using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Campanas;

public class IndexModel(AppDbContext db) : PageModel
{
    public record Fila(Campana Campana, int Pasos, int Activas, int Total, int Enviados, int Respuestas);
    public List<Fila> Campanas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Campanas = await db.Campanas.Include(c => c.Servicio).OrderBy(c => c.Nombre)
            .Select(c => new Fila(c, c.Pasos.Count,
                c.Inscripciones.Count(i => i.Estado == EstadoInscripcion.Activa),
                c.Inscripciones.Count,
                db.Mensajes.Count(m => m.Inscripcion!.CampanaId == c.Id && (m.Estado == EstadoMensaje.Enviado || m.Estado == EstadoMensaje.Entregado || m.Estado == EstadoMensaje.Leido)),
                c.Inscripciones.Count(i => i.Prospecto.UltimaRespuestaEn > i.CreadoEn)))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostActivarAsync(int id)
    {
        var c = await db.Campanas.Include(x => x.Pasos).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (!c.Activa && c.Pasos.Count == 0) { TempData["Error"] = "Agregue al menos un paso antes de activar."; return RedirectToPage(); }
        c.Activa = !c.Activa;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
