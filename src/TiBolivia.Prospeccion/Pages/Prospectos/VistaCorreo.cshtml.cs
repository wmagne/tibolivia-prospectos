using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Prospectos;

/// <summary>Muestra el correo tal como lo recibirá la empresa (con diseño, variables y rubro aplicados).</summary>
public class VistaCorreoModel(AppDbContext db, RenderizadorPlantillas renderizador) : PageModel
{
    public const string PlantillaPorDefecto = "Publicidad 1 · Presentación por rubro";

    public async Task<IActionResult> OnGetAsync(int id, int? plantillaId)
    {
        var p = await db.Prospectos.Include(x => x.AsignadoA).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        var plantilla = plantillaId is not null
            ? await db.Plantillas.FirstOrDefaultAsync(x => x.Id == plantillaId && x.Canal == Canal.Email)
            : await db.Plantillas.FirstOrDefaultAsync(x => x.Nombre == PlantillaPorDefecto);
        if (plantilla is null) return NotFound();

        var servicio = await db.PasosCampana.Where(x => x.PlantillaId == plantilla.Id).Select(x => x.Campana.Servicio).FirstOrDefaultAsync();
        var asunto = renderizador.Renderizar(plantilla.Asunto, p, servicio, html: false);
        var cuerpo = DisenoCorreo.Envolver(renderizador.Renderizar(plantilla.Cuerpo, p, servicio, html: true), renderizador.LinkBaja(p));
        var cabecera =
            "<div style=\"font-family:Arial,sans-serif;background:#fff8e1;border-bottom:1px solid #e0c46c;padding:10px 16px;font-size:14px;\">" +
            $"<b>Vista previa</b> · Para: {WebUtility.HtmlEncode(p.Email ?? "(sin correo)")} · Asunto: <b>{WebUtility.HtmlEncode(asunto)}</b></div>";
        return Content(cabecera + cuerpo, "text/html; charset=utf-8");
    }
}
