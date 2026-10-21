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
            AdjuntoActual = p.AdjuntoNombre;
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

    private const long MaxAdjunto = 5 * 1024 * 1024;

    /// <summary>Archivo para adjuntar a los correos de esta plantilla (ej. propuesta comercial en PDF).</summary>
    [BindProperty] public IFormFile? Adjunto { get; set; }
    [BindProperty] public bool QuitarAdjunto { get; set; }
    public string? AdjuntoActual { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Adjunto is { Length: > MaxAdjunto }) ModelState.AddModelError("Adjunto", "El adjunto no puede superar 5 MB.");
        if (string.IsNullOrWhiteSpace(Datos.Nombre)) ModelState.AddModelError("Datos.Nombre", "El nombre es obligatorio");
        if (string.IsNullOrWhiteSpace(Datos.Cuerpo)) ModelState.AddModelError("Datos.Cuerpo", "El cuerpo es obligatorio");
        if (Datos.Canal == Canal.Email && string.IsNullOrWhiteSpace(Datos.Asunto)) ModelState.AddModelError("Datos.Asunto", "El asunto es obligatorio");
        if (!ModelState.IsValid) return Page();

        Plantilla p;
        if (Datos.Id == 0)
        {
            p = new Plantilla();
            db.Plantillas.Add(p);
        }
        else
        {
            p = await db.Plantillas.FindAsync(Datos.Id) ?? throw new InvalidOperationException("Plantilla no encontrada");
        }
        p.Nombre = Datos.Nombre; p.Canal = Datos.Canal; p.Asunto = Datos.Asunto; p.Cuerpo = Datos.Cuerpo;
        p.WhatsAppPlantillaMeta = Datos.WhatsAppPlantillaMeta; p.WhatsAppIdioma = Datos.WhatsAppIdioma; p.WhatsAppParametros = Datos.WhatsAppParametros;
        if (QuitarAdjunto || p.Canal != Canal.Email) { p.AdjuntoNombre = null; p.AdjuntoContenido = null; }
        if (Adjunto is { Length: > 0 } && p.Canal == Canal.Email)
        {
            using var ms = new MemoryStream();
            await Adjunto.CopyToAsync(ms);
            p.AdjuntoNombre = Path.GetFileName(Adjunto.FileName);
            p.AdjuntoContenido = ms.ToArray();
        }
        await db.SaveChangesAsync();
        TempData["Ok"] = "Plantilla guardada.";
        return RedirectToPage("Index");
    }
}
