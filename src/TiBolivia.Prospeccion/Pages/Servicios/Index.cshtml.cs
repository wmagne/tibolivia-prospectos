using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Servicios;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Servicio> Servicios { get; private set; } = [];
    [BindProperty] public Servicio Datos { get; set; } = new();

    public async Task OnGetAsync(int? editar)
    {
        Servicios = await db.Servicios.OrderBy(s => s.Nombre).ToListAsync();
        if (editar is not null) Datos = Servicios.FirstOrDefault(s => s.Id == editar) ?? new();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Datos.Nombre))
        {
            TempData["Error"] = "El nombre es obligatorio.";
            return RedirectToPage();
        }
        if (Datos.Id == 0) db.Servicios.Add(Datos);
        else
        {
            var s = await db.Servicios.FindAsync(Datos.Id);
            if (s is null) return NotFound();
            s.Nombre = Datos.Nombre; s.Descripcion = Datos.Descripcion ?? ""; s.UrlInformacion = Datos.UrlInformacion; s.Activo = Datos.Activo;
        }
        await db.SaveChangesAsync();
        TempData["Ok"] = "Servicio guardado.";
        return RedirectToPage();
    }
}
