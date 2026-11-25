using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Sicoes;

/// <summary>Oportunidades del SICOES: procesos con puntaje 60 o más, para seguirlos como cualquier venta.</summary>
public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Ver { get; set; }
    public List<SicoesOportunidad> Oportunidades { get; private set; } = [];
    public SicoesLectura? UltimaLectura { get; private set; }
    public int ProcesosGuardados { get; private set; }

    public static string Nombre(EstadoOportunidadSicoes e) => e switch
    {
        EstadoOportunidadSicoes.Nueva => "Nueva",
        EstadoOportunidadSicoes.EnAnalisis => "En análisis",
        EstadoOportunidadSicoes.PropuestaEnviada => "Propuesta enviada",
        EstadoOportunidadSicoes.Ganada => "Ganada",
        EstadoOportunidadSicoes.Perdida => "Perdida",
        _ => "Descartada",
    };

    public static string Color(EstadoOportunidadSicoes e) => e switch
    {
        EstadoOportunidadSicoes.Nueva => "primary",
        EstadoOportunidadSicoes.EnAnalisis => "info",
        EstadoOportunidadSicoes.PropuestaEnviada => "warning",
        EstadoOportunidadSicoes.Ganada => "success",
        EstadoOportunidadSicoes.Perdida => "danger",
        _ => "secondary",
    };

    public async Task OnGetAsync()
    {
        var q = db.SicoesOportunidades.Include(o => o.Proceso).Include(o => o.Perfil).AsQueryable();
        q = Ver switch
        {
            "cerradas" => q.Where(o => o.Estado == EstadoOportunidadSicoes.Ganada || o.Estado == EstadoOportunidadSicoes.Perdida || o.Estado == EstadoOportunidadSicoes.Descartada),
            "todas" => q,
            _ => q.Where(o => o.Estado != EstadoOportunidadSicoes.Ganada && o.Estado != EstadoOportunidadSicoes.Perdida && o.Estado != EstadoOportunidadSicoes.Descartada),
        };
        // Lo que cierra antes, primero; lo que no tiene fecha, al final.
        Oportunidades = await q.OrderBy(o => o.Proceso.FechaLimite == null).ThenBy(o => o.Proceso.FechaLimite).ThenByDescending(o => o.Puntaje)
            .Take(300).ToListAsync();
        UltimaLectura = await db.SicoesLecturas.OrderByDescending(l => l.Inicio).FirstOrDefaultAsync();
        ProcesosGuardados = await db.SicoesProcesos.CountAsync();
    }

    public async Task<IActionResult> OnPostEstadoAsync(int id, EstadoOportunidadSicoes estado, string? notas)
    {
        var o = await db.SicoesOportunidades.FindAsync(id);
        if (o is null) return NotFound();
        o.Estado = estado;
        if (notas is not null) o.Notas = notas.Length > 2000 ? notas[..2000] : notas;
        o.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = $"Oportunidad marcada como «{Nombre(estado)}».";
        return RedirectToPage(new { Ver });
    }
}
