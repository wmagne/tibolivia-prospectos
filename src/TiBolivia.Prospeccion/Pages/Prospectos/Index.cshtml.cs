using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Prospectos;

public class IndexModel(AppDbContext db, ServicioInscripciones inscripciones) : PageModel
{
    private const int TamanoPagina = 50;

    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public EstadoProspecto? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public string? Ciudad { get; set; }
    [BindProperty(SupportsGet = true)] public string? Rubro { get; set; }
    [BindProperty(SupportsGet = true)] public string? AsignadoA { get; set; }
    [BindProperty(SupportsGet = true)] public bool Mios { get; set; }
    [BindProperty(SupportsGet = true)] public string? Prioridad { get; set; }
    [BindProperty(SupportsGet = true)] public bool PorVerificar { get; set; }
    public List<string> Rubros { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public int Pagina { get; set; } = 1;

    public List<Prospecto> Prospectos { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)TamanoPagina);
    public SelectList Campanas { get; private set; } = null!;
    public SelectList Usuarios { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        var q = db.Prospectos.Include(p => p.AsignadoA).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var t = $"%{Buscar.Trim()}%";
            q = q.Where(p => EF.Functions.ILike(p.Empresa, t) || EF.Functions.ILike(p.NombreContacto ?? "", t)
                || EF.Functions.ILike(p.Email ?? "", t) || EF.Functions.ILike(p.Telefono ?? "", t)
                || EF.Functions.ILike(p.Necesidad ?? "", t) || EF.Functions.ILike(p.Notas ?? "", t));
        }
        if (Estado is not null) q = q.Where(p => p.Estado == Estado);
        if (!string.IsNullOrWhiteSpace(Ciudad)) q = q.Where(p => p.Ciudad == Ciudad);
        if (!string.IsNullOrWhiteSpace(Rubro)) q = q.Where(p => p.Rubro == Rubro);
        if (!string.IsNullOrWhiteSpace(Prioridad)) q = q.Where(p => p.Prioridad == Prioridad);
        if (PorVerificar) q = q.Where(p => p.DatosPorVerificar);
        if (Mios) AsignadoA = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (AsignadoA == "-") q = q.Where(p => p.AsignadoAId == null);
        else if (!string.IsNullOrWhiteSpace(AsignadoA)) q = q.Where(p => p.AsignadoAId == AsignadoA);

        Total = await q.CountAsync();
        Pagina = Math.Max(1, Pagina);
        Prospectos = await q.OrderBy(p => p.Prioridad ?? "Z").ThenByDescending(p => p.ActualizadoEn).Skip((Pagina - 1) * TamanoPagina).Take(TamanoPagina).ToListAsync();
        await CargarListasAsync();
    }

    public async Task<IActionResult> OnPostInscribirAsync(int campanaId, int[] seleccion)
    {
        if (seleccion.Length == 0)
        {
            TempData["Error"] = "Seleccione al menos un prospecto.";
            return RedirectToPage(RutaActual());
        }
        try
        {
            var n = await inscripciones.InscribirAsync(campanaId, seleccion, User.FindFirstValue(ClaimTypes.NameIdentifier));
            TempData["Ok"] = $"{n} prospecto(s) inscritos en la campaña. Los ya inscritos o marcados \"No contactar\" se omitieron.";
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToPage(RutaActual());
    }

    public async Task<IActionResult> OnPostAsignarAsync(string? usuarioId, int[] seleccion)
    {
        await db.Prospectos.Where(p => seleccion.Contains(p.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.AsignadoAId, string.IsNullOrEmpty(usuarioId) ? null : usuarioId)
                                      .SetProperty(p => p.ActualizadoEn, DateTime.UtcNow));
        TempData["Ok"] = $"{seleccion.Length} prospecto(s) reasignados.";
        return RedirectToPage(RutaActual());
    }

    private object RutaActual() => new { Buscar, Estado, Ciudad, Rubro, AsignadoA, Mios, Prioridad, PorVerificar, Pagina };

    private async Task CargarListasAsync()
    {
        Rubros = await db.Prospectos.Where(p => p.Rubro != null).Select(p => p.Rubro!).Distinct().OrderBy(r => r).ToListAsync();
        Campanas = new SelectList(await db.Campanas.OrderBy(c => c.Nombre).ToListAsync(), "Id", "Nombre");
        Usuarios = new SelectList(await db.Users.Where(u => u.Activo).OrderBy(u => u.NombreCompleto).ToListAsync(), "Id", "NombreCompleto");
    }
}
