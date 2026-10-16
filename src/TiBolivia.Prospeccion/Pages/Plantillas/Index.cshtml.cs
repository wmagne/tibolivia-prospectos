using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Plantillas;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Plantilla> Plantillas { get; private set; } = [];
    public async Task OnGetAsync() => Plantillas = await db.Plantillas.OrderBy(p => p.Canal).ThenBy(p => p.Nombre).ToListAsync();
}
