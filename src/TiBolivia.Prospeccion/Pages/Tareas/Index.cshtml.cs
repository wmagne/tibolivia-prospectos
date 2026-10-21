using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Tareas;

/// <summary>Agenda del comercial: próximas acciones vencidas, de hoy y de los próximos 7 días.</summary>
public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Comercial { get; set; }
    public List<Prospecto> Vencidas { get; private set; } = [];
    public List<Prospecto> Hoy { get; private set; } = [];
    public List<Prospecto> Proximas { get; private set; } = [];
    public SelectList Usuarios { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Comercial ??= User.FindFirstValue(ClaimTypes.NameIdentifier);
        var hoy = Fechas.HoyBolivia;
        var inicioHoy = Fechas.InicioDelDiaUtc(hoy).AddHours(-8); // medianoche de hoy en Bolivia
        var finHoy = Fechas.FinDelDiaUtc(hoy);
        var fin7 = Fechas.FinDelDiaUtc(hoy.AddDays(7));

        var q = db.Prospectos.Include(p => p.AsignadoA).Where(p => p.ProximaAccion != null && p.FechaProximaAccion != null
            && p.Estado != EstadoProspecto.NoContactar && p.Estado != EstadoProspecto.Ganado && p.Estado != EstadoProspecto.Perdido);
        if (Comercial != "*") q = q.Where(p => p.AsignadoAId == Comercial);

        var lista = await q.Where(p => p.FechaProximaAccion < fin7)
            .OrderBy(p => p.FechaProximaAccion).ThenBy(p => p.Prioridad ?? "Z").ToListAsync();
        Vencidas = lista.Where(p => p.FechaProximaAccion < inicioHoy).ToList();
        Hoy = lista.Where(p => p.FechaProximaAccion >= inicioHoy && p.FechaProximaAccion < finHoy).ToList();
        Proximas = lista.Where(p => p.FechaProximaAccion >= finHoy).ToList();

        Usuarios = new SelectList(await db.Users.Where(u => u.Activo).OrderBy(u => u.NombreCompleto).ToListAsync(), "Id", "NombreCompleto", Comercial);
    }

    /// <summary>Posterga la tarea un día (cuando no se pudo hacer hoy).</summary>
    public async Task<IActionResult> OnPostPostergarAsync(int id, int dias = 1)
    {
        var p = await db.Prospectos.FindAsync(id);
        if (p?.FechaProximaAccion is not null)
        {
            p.FechaProximaAccion = Fechas.InicioDelDiaUtc(Fechas.HoyBolivia.AddDays(dias));
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Comercial });
    }
}
