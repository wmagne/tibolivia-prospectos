using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services.Sicoes;

namespace TiBolivia.Prospeccion.Pages.Sicoes;

/// <summary>Perfiles de búsqueda (por ahora solo TIBolivia) y la tarifa anotada en la ficha de cada cliente.</summary>
public class PerfilesModel(AppDbContext db) : PageModel
{
    public List<SicoesPerfil> Perfiles { get; private set; } = [];
    [BindProperty] public SicoesPerfil Editado { get; set; } = new();

    public async Task OnGetAsync() => Perfiles = await db.SicoesPerfiles.OrderBy(p => p.Id).ToListAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.IsInRole(Roles.Admin)) return Forbid();
        var p = await db.SicoesPerfiles.FindAsync(Editado.Id);
        if (p is null) return NotFound();
        p.PalabrasClave = Editado.PalabrasClave?.Trim() ?? "";
        p.PalabrasExcluidas = Editado.PalabrasExcluidas?.Trim() ?? "";
        p.Departamentos = Editado.Departamentos?.Trim() ?? "";
        p.IncluyeNacional = Editado.IncluyeNacional;
        p.MontoMinimo = Math.Max(0, Editado.MontoMinimo);
        p.Plan = Editado.Plan;
        p.TarifaMensualBs = Editado.TarifaMensualBs > 0 ? Editado.TarifaMensualBs : TarifasSicoes.TarifaDelPlan(Editado.Plan);
        p.PilotoHasta = Editado.Plan == PlanSicoes.Piloto ? (Editado.PilotoHasta ?? DateTime.UtcNow.Date.AddDays(30)) : null;
        p.CorreoAvisos = string.IsNullOrWhiteSpace(Editado.CorreoAvisos) ? null : Editado.CorreoAvisos.Trim();
        p.WhatsAppAvisos = string.IsNullOrWhiteSpace(Editado.WhatsAppAvisos) ? null : Editado.WhatsAppAvisos.Trim();
        p.Activo = Editado.Activo;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = $"Perfil «{p.Nombre}» guardado. Los cambios se aplican desde la próxima lectura.";
        return RedirectToPage();
    }
}
