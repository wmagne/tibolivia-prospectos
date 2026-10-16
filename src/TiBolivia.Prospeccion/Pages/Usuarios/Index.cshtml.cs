using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Usuarios;

public class IndexModel(UserManager<Usuario> usuarios) : PageModel
{
    public List<(Usuario Usuario, bool EsAdmin)> Lista { get; private set; } = [];

    [BindProperty, Required] public string NombreCompleto { get; set; } = "";
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty] public string? Telefono { get; set; }
    [BindProperty, Required, MinLength(8)] public string Password { get; set; } = "";
    [BindProperty] public bool EsAdmin { get; set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await CargarAsync(); return Page(); }
        var u = new Usuario { UserName = Email, Email = Email, NombreCompleto = NombreCompleto, PhoneNumber = Telefono, EmailConfirmed = true };
        var r = await usuarios.CreateAsync(u, Password);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            await CargarAsync();
            return Page();
        }
        await usuarios.AddToRoleAsync(u, EsAdmin ? Roles.Admin : Roles.Comercial);
        TempData["Ok"] = $"Usuario {Email} creado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivoAsync(string id)
    {
        var u = await usuarios.FindByIdAsync(id);
        if (u is null) return NotFound();
        if (u.UserName == User.Identity?.Name) { TempData["Error"] = "No puede desactivarse a sí mismo."; return RedirectToPage(); }
        u.Activo = !u.Activo;
        await usuarios.UpdateAsync(u);
        await usuarios.UpdateSecurityStampAsync(u); // cierra sus sesiones abiertas
        return RedirectToPage();
    }

    private async Task CargarAsync()
    {
        var admins = (await usuarios.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();
        Lista = (await usuarios.Users.OrderBy(u => u.NombreCompleto).ToListAsync()).Select(u => (u, admins.Contains(u.Id))).ToList();
    }
}
