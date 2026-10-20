using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Prospectos;

public class DetalleModel(AppDbContext db, ServicioInscripciones inscripciones, IEnviadorWhatsApp whatsApp) : PageModel
{
    public Prospecto Prospecto { get; private set; } = null!;
    public List<(DateTime Fecha, string Clase, string Titulo, string? Detalle, string? Badge, string? BadgeColor, string? Error)> Historial { get; private set; } = [];
    public SelectList Campanas { get; private set; } = null!;
    public bool VentanaWhatsAppAbierta => !whatsApp.RequierePlantillasAprobadas || Prospecto.UltimaRespuestaEn > DateTime.UtcNow.AddHours(-24);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await db.Prospectos
            .Include(x => x.AsignadoA)
            .Include(x => x.Inscripciones).ThenInclude(i => i.Campana).ThenInclude(c => c.Pasos)
            .Include(x => x.Mensajes)
            .Include(x => x.Interacciones).ThenInclude(i => i.Usuario)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        Prospecto = p;

        Historial =
        [
            .. p.Mensajes.Select(m => (m.CreadoEn,
                m.Direccion == DireccionMensaje.Entrante ? "entrante" : "saliente",
                $"{Etiquetas.Texto(m.Canal)} {(m.Direccion == DireccionMensaje.Entrante ? "recibido" : "enviado")}{(m.Asunto is null ? "" : ": " + m.Asunto)}",
                (string?)m.Cuerpo, (string?)m.Estado.ToString(), (string?)Etiquetas.Color(m.Estado), m.Error)),
            .. p.Interacciones.Select(i => (i.Fecha, "interaccion",
                $"{i.Tipo}{(i.Usuario is null ? "" : " · " + i.Usuario.NombreCompleto)}",
                (string?)i.Detalle, (string?)null, (string?)null, (string?)null)),
        ];
        Historial = Historial.OrderByDescending(h => h.Fecha).ToList();

        var inscritas = p.Inscripciones.Select(i => i.CampanaId).ToList();
        Campanas = new SelectList(await db.Campanas.Where(c => !inscritas.Contains(c.Id)).OrderBy(c => c.Nombre).ToListAsync(), "Id", "Nombre");
        return Page();
    }

    public async Task<IActionResult> OnPostInteraccionAsync(int id, TipoInteraccion tipo, string detalle, EstadoProspecto? nuevoEstado)
    {
        var p = await db.Prospectos.FindAsync(id);
        if (p is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(detalle))
            db.Interacciones.Add(new Interaccion { ProspectoId = id, Tipo = tipo, Detalle = detalle.Trim(), UsuarioId = UsuarioId });
        if (nuevoEstado is not null && nuevoEstado != p.Estado)
        {
            db.Interacciones.Add(new Interaccion { ProspectoId = id, Tipo = TipoInteraccion.Sistema, UsuarioId = UsuarioId,
                Detalle = $"Estado: {Etiquetas.Texto(p.Estado)} → {Etiquetas.Texto(nuevoEstado.Value)}" });
            p.Estado = nuevoEstado.Value;
        }
        p.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostInscribirAsync(int id, int campanaId)
    {
        try
        {
            var n = await inscripciones.InscribirAsync(campanaId, [id], UsuarioId);
            TempData[n > 0 ? "Ok" : "Error"] = n > 0 ? "Inscrito en la campaña." : "No se pudo inscribir (ya inscrito o \"No contactar\").";
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEstadoInscripcionAsync(int id, int inscripcionId, EstadoInscripcion estado)
    {
        await inscripciones.CambiarEstadoAsync(inscripcionId, estado);
        return RedirectToPage(new { id });
    }

    /// <summary>Respuesta manual de un comercial por WhatsApp (solo dentro de la ventana de 24 h).</summary>
    public async Task<IActionResult> OnPostWhatsAppAsync(int id, string texto)
    {
        var p = await db.Prospectos.FindAsync(id);
        if (p?.Telefono is null || string.IsNullOrWhiteSpace(texto)) return RedirectToPage(new { id });
        if (whatsApp.RequierePlantillasAprobadas && !(p.UltimaRespuestaEn > DateTime.UtcNow.AddHours(-24)))
        {
            TempData["Error"] = "WhatsApp solo permite texto libre dentro de las 24 h desde el último mensaje del cliente. Use una campaña con plantilla aprobada.";
            return RedirectToPage(new { id });
        }
        var r = await whatsApp.EnviarTextoAsync(p.Telefono, texto.Trim(), HttpContext.RequestAborted);
        db.Mensajes.Add(new Mensaje
        {
            ProspectoId = id, Canal = Canal.WhatsApp, Direccion = DireccionMensaje.Saliente, Cuerpo = texto.Trim(),
            Estado = r.Exito ? EstadoMensaje.Enviado : EstadoMensaje.Fallido, ProveedorId = r.ProveedorId, Error = r.Error,
        });
        await db.SaveChangesAsync();
        TempData[r.Exito ? "Ok" : "Error"] = r.Exito ? "Mensaje enviado." : "Error al enviar: " + r.Error;
        return RedirectToPage(new { id });
    }

    private string? UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
