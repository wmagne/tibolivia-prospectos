using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages;

/// <summary>Página pública de baja. El POST también atiende el "baja en un clic" (List-Unsubscribe-Post) de Gmail/Outlook.</summary>
[IgnoreAntiforgeryToken]
public class BajaModel(AppDbContext db) : PageModel
{
    public Prospecto? Prospecto { get; private set; }
    public bool Confirmado { get; private set; }

    public async Task OnGetAsync(Guid token)
    {
        Prospecto = await db.Prospectos.FirstOrDefaultAsync(p => p.TokenBaja == token);
        Confirmado = Prospecto?.BajaEmail == true;
    }

    public async Task OnPostAsync(Guid token)
    {
        Prospecto = await db.Prospectos.FirstOrDefaultAsync(p => p.TokenBaja == token);
        if (Prospecto is null) return;
        if (!Prospecto.BajaEmail)
        {
            Prospecto.BajaEmail = true;
            Prospecto.ActualizadoEn = DateTime.UtcNow;
            db.Interacciones.Add(new Interaccion { ProspectoId = Prospecto.Id, Tipo = TipoInteraccion.Sistema, Detalle = "Se dio de baja de los correos." });
            await db.SaveChangesAsync();
        }
        Confirmado = true;
    }
}
