using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Embudo;

/// <summary>Vista de ventas por etapa con el valor estimado de cada negocio.</summary>
public class IndexModel(AppDbContext db) : PageModel
{
    public static readonly EstadoProspecto[] Etapas =
        [EstadoProspecto.Nuevo, EstadoProspecto.Contactado, EstadoProspecto.Interesado, EstadoProspecto.ReunionAgendada, EstadoProspecto.PropuestaEnviada, EstadoProspecto.Ganado];

    [BindProperty(SupportsGet = true)] public string? Comercial { get; set; }
    [BindProperty(SupportsGet = true)] public string? Rubro { get; set; }
    [BindProperty(SupportsGet = true)] public string? Prioridad { get; set; }

    public Dictionary<EstadoProspecto, List<Prospecto>> Columnas { get; private set; } = [];
    public int Perdidos { get; private set; }
    public SelectList Usuarios { get; private set; } = null!;
    public List<string> Rubros { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var q = db.Prospectos.AsQueryable();
        if (!string.IsNullOrEmpty(Comercial)) q = q.Where(p => p.AsignadoAId == Comercial);
        if (!string.IsNullOrEmpty(Rubro)) q = q.Where(p => p.Rubro == Rubro);
        if (!string.IsNullOrEmpty(Prioridad)) q = q.Where(p => p.Prioridad == Prioridad);

        var todos = await q.Where(p => Etapas.Contains(p.Estado))
            .OrderBy(p => p.Prioridad ?? "Z").ThenByDescending(p => p.ValorEstimado).ThenByDescending(p => p.ActualizadoEn)
            .ToListAsync();
        Columnas = Etapas.ToDictionary(e => e, e => todos.Where(p => p.Estado == e).ToList());
        Perdidos = await q.CountAsync(p => p.Estado == EstadoProspecto.Perdido);
        Usuarios = new SelectList(await db.Users.Where(u => u.Activo).OrderBy(u => u.NombreCompleto).ToListAsync(), "Id", "NombreCompleto", Comercial);
        Rubros = await db.Prospectos.Where(p => p.Rubro != null).Select(p => p.Rubro!).Distinct().OrderBy(r => r).ToListAsync();
    }

    public async Task<IActionResult> OnPostMoverAsync(int id, EstadoProspecto estado)
    {
        var p = await db.Prospectos.FindAsync(id);
        if (p is not null && p.Estado != estado)
        {
            db.Interacciones.Add(new Interaccion
            {
                ProspectoId = id, Tipo = TipoInteraccion.Sistema, UsuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Detalle = $"Estado: {Etiquetas.Texto(p.Estado)} → {Etiquetas.Texto(estado)}",
            });
            p.Estado = estado;
            p.ActualizadoEn = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Comercial, Rubro, Prioridad });
    }
}
