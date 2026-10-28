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
    /// <summary>Texto ya personalizado de la última tarea de campaña de cada prospecto (mensaje de WhatsApp o guion de llamada).</summary>
    public Dictionary<int, string> TextoTarea { get; private set; } = [];

    public static bool EsWhatsApp(Prospecto p) => p.ProximaAccion?.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) == true;
    public static bool EsCelular(Prospecto p) => p.Telefono is { Length: 11 } t && (t.StartsWith("5916") || t.StartsWith("5917"));
    public string EnlaceWhatsApp(Prospecto p) =>
        $"https://wa.me/{p.Telefono}?text={Uri.EscapeDataString(TextoTarea.GetValueOrDefault(p.Id, ""))}";

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

        var ids = lista.Select(p => p.Id).ToList();
        var tareas = await db.Interacciones
            .Where(i => ids.Contains(i.ProspectoId) && i.Detalle.StartsWith("Tarea de la campaña"))
            .Select(i => new { i.ProspectoId, i.Fecha, i.Detalle }).ToListAsync();
        TextoTarea = tareas.GroupBy(t => t.ProspectoId)
            .ToDictionary(g => g.Key, g => g.MaxBy(t => t.Fecha)!.Detalle.Split('\n', 2) is [_, var texto] ? texto.Trim() : "");

        Usuarios = new SelectList(await db.Users.Where(u => u.Activo).OrderBy(u => u.NombreCompleto).ToListAsync(), "Id", "NombreCompleto", Comercial);
    }

    /// <summary>Marca la tarea como hecha: registra el contacto y la quita de la agenda (la campaña agenda el siguiente paso).</summary>
    public async Task<IActionResult> OnPostHechoAsync(int id)
    {
        var p = await db.Prospectos.FindAsync(id);
        if (p?.ProximaAccion is null) return RedirectToPage(new { Comercial });
        var usuario = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var whatsApp = EsWhatsApp(p);
        if (whatsApp)
            db.Mensajes.Add(new Mensaje { ProspectoId = id, Canal = Canal.WhatsApp, Direccion = DireccionMensaje.Saliente, Cuerpo = p.ProximaAccion,
                Estado = EstadoMensaje.Enviado, ProveedorId = "manual-" + Guid.NewGuid().ToString("N") });
        db.Interacciones.Add(new Interaccion { ProspectoId = id, UsuarioId = usuario,
            Tipo = whatsApp ? TipoInteraccion.WhatsApp : TipoInteraccion.Llamada, Detalle = "Hecho: " + p.ProximaAccion });
        if (p.Estado == EstadoProspecto.Nuevo) p.Estado = EstadoProspecto.Contactado;
        p.ProximaAccion = null;
        p.FechaProximaAccion = null;
        p.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage(new { Comercial });
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
