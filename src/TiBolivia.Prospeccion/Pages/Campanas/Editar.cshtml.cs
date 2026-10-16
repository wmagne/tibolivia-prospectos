using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Campanas;

public class EditarModel(AppDbContext db) : PageModel
{
    [BindProperty] public int Id { get; set; }
    [BindProperty, Required(ErrorMessage = "El nombre es obligatorio")] public string Nombre { get; set; } = "";
    [BindProperty] public string? Descripcion { get; set; }
    [BindProperty] public int? ServicioId { get; set; }
    [BindProperty] public bool PausarAlResponder { get; set; } = true;

    public List<PasoCampana> Pasos { get; private set; } = [];
    public List<Plantilla> Plantillas { get; private set; } = [];
    public SelectList Servicios { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is not null)
        {
            var c = await db.Campanas.FindAsync(id);
            if (c is null) return NotFound();
            (Id, Nombre, Descripcion, ServicioId, PausarAlResponder) = (c.Id, c.Nombre, c.Descripcion, c.ServicioId, c.PausarAlResponder);
        }
        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await CargarAsync(); return Page(); }
        var c = Id == 0 ? new Campana { Activa = false } : await db.Campanas.FindAsync(Id);
        if (c is null) return NotFound();
        if (Id == 0) db.Campanas.Add(c);
        c.Nombre = Nombre.Trim();
        c.Descripcion = Descripcion;
        c.ServicioId = ServicioId;
        c.PausarAlResponder = PausarAlResponder;
        await db.SaveChangesAsync();
        TempData["Ok"] = Id == 0 ? "Campaña creada. Agregue los pasos y luego actívela." : "Campaña guardada.";
        return RedirectToPage(new { id = c.Id });
    }

    public async Task<IActionResult> OnPostAgregarPasoAsync(int id, int diasEspera, int plantillaId)
    {
        var plantilla = await db.Plantillas.FindAsync(plantillaId);
        if (plantilla is null) return RedirectToPage(new { id });
        var orden = (await db.PasosCampana.Where(p => p.CampanaId == id).MaxAsync(p => (int?)p.Orden) ?? 0) + 1;
        db.PasosCampana.Add(new PasoCampana { CampanaId = id, Orden = orden, DiasEspera = Math.Max(0, diasEspera), Canal = plantilla.Canal, PlantillaId = plantillaId });
        await db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEliminarPasoAsync(int id, int pasoId)
    {
        // Las inscripciones guardan el índice del paso: eliminar pasos de una campaña en curso los desplaza.
        if (await db.Inscripciones.AnyAsync(i => i.CampanaId == id && i.Estado == EstadoInscripcion.Activa))
        {
            TempData["Error"] = "No se pueden eliminar pasos mientras haya prospectos en curso. Desactive la campaña o cree una nueva versión.";
            return RedirectToPage(new { id });
        }
        await db.PasosCampana.Where(p => p.Id == pasoId && p.CampanaId == id).ExecuteDeleteAsync();
        return RedirectToPage(new { id });
    }

    private async Task CargarAsync()
    {
        Servicios = new SelectList(await db.Servicios.Where(s => s.Activo).OrderBy(s => s.Nombre).ToListAsync(), "Id", "Nombre");
        Plantillas = await db.Plantillas.OrderBy(p => p.Canal).ThenBy(p => p.Nombre).ToListAsync();
        if (Id != 0)
            Pasos = await db.PasosCampana.Include(p => p.Plantilla).Where(p => p.CampanaId == Id).OrderBy(p => p.Orden).ToListAsync();
    }
}
