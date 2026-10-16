using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Plantillas;

public class EditarModel(AppDbContext db, RenderizadorPlantillas renderizador) : PageModel
{
    [BindProperty] public Plantilla Datos { get; set; } = new() { Canal = Canal.Email };
    public string? VistaPreviaAsunto { get; private set; }
    public string? VistaPrevia { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is not null)
        {
            var p = await db.Plantillas.FindAsync(id);
            if (p is null) return NotFound();
            Datos = p;
        }
        return Page();
    }

    public IActionResult OnPostVistaPrevia()
    {
        var ejemplo = new Prospecto
        {
            Empresa = "Empresa Ejemplo SRL", NombreContacto = "María Quispe", Cargo = "Gerente", Ciudad = "La Paz", Rubro = "Comercio",
            AsignadoA = new Usuario { NombreCompleto = User.Identity?.Name ?? "Comercial", Email = User.Identity?.Name },
        };
        var servicio = new Servicio { Nombre = "Desarrollo de software a medida", Descripcion = "Sistemas diseñados para sus procesos." };
        VistaPreviaAsunto = renderizador.Renderizar(Datos.Asunto, ejemplo, servicio, html: false);
        VistaPrevia = renderizador.Renderizar(Datos.Cuerpo, ejemplo, servicio, html: Datos.Canal == Canal.Email);
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Datos.Nombre)) ModelState.AddModelError("Datos.Nombre", "El nombre es obligatorio");
        if (string.IsNullOrWhiteSpace(Datos.Cuerpo)) ModelState.AddModelError("Datos.Cuerpo", "El cuerpo es obligatorio");
        if (Datos.Canal == Canal.Email && string.IsNullOrWhiteSpace(Datos.Asunto)) ModelState.AddModelError("Datos.Asunto", "El asunto es obligatorio");
        if (!ModelState.IsValid) return Page();

        if (Datos.Id == 0) db.Plantillas.Add(Datos);
        else
        {
            var p = await db.Plantillas.FindAsync(Datos.Id);
            if (p is null) return NotFound();
            p.Nombre = Datos.Nombre; p.Canal = Datos.Canal; p.Asunto = Datos.Asunto; p.Cuerpo = Datos.Cuerpo;
            p.WhatsAppPlantillaMeta = Datos.WhatsAppPlantillaMeta; p.WhatsAppIdioma = Datos.WhatsAppIdioma; p.WhatsAppParametros = Datos.WhatsAppParametros;
        }
        await db.SaveChangesAsync();
        TempData["Ok"] = "Plantilla guardada.";
        return RedirectToPage("Index");
    }
}
