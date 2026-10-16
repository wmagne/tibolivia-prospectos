using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Mensajes;

public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public Canal? Canal { get; set; }
    [BindProperty(SupportsGet = true)] public DireccionMensaje? Direccion { get; set; }
    [BindProperty(SupportsGet = true)] public EstadoMensaje? Estado { get; set; }
    public List<Mensaje> Mensajes { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var q = db.Mensajes.Include(m => m.Prospecto).AsQueryable();
        if (Canal is not null) q = q.Where(m => m.Canal == Canal);
        if (Direccion is not null) q = q.Where(m => m.Direccion == Direccion);
        if (Estado is not null) q = q.Where(m => m.Estado == Estado);
        Mensajes = await q.OrderByDescending(m => m.CreadoEn).Take(200).ToListAsync();
    }
}
