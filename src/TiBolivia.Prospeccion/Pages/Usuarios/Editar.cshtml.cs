using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Usuarios;

/// <summary>Edición de un usuario por el administrador (incluido el usuario principal).</summary>
public class EditarModel(UserManager<Usuario> usuarios, SignInManager<Usuario> sesion) : PageModel
{
    [BindProperty, Required(ErrorMessage = "El nombre es obligatorio")] public string NombreCompleto { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "El correo es obligatorio"), EmailAddress(ErrorMessage = "Correo inválido")] public string Email { get; set; } = "";
    [BindProperty] public string? Telefono { get; set; }
    [BindProperty] public bool EsAdmin { get; set; }
    [BindProperty, MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")] public string? NuevaPassword { get; set; }
    public bool EsUnoMismo { get; private set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var u = await usuarios.FindByIdAsync(id);
        if (u is null) return NotFound();
        (NombreCompleto, Email, Telefono) = (u.NombreCompleto, u.Email ?? "", u.PhoneNumber);
        EsAdmin = await usuarios.IsInRoleAsync(u, Roles.Admin);
        EsUnoMismo = u.Id == User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var u = await usuarios.FindByIdAsync(id);
        if (u is null) return NotFound();
        EsUnoMismo = u.Id == User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eraAdmin = await usuarios.IsInRoleAsync(u, Roles.Admin);
        if (eraAdmin && !EsAdmin && (EsUnoMismo || (await usuarios.GetUsersInRoleAsync(Roles.Admin)).Count <= 1))
            ModelState.AddModelError(nameof(EsAdmin), "Debe quedar al menos un administrador y no puede quitarse el rol a sí mismo.");
        var otro = await usuarios.FindByEmailAsync(Email.Trim());
        if (otro is not null && otro.Id != u.Id) ModelState.AddModelError(nameof(Email), "Ya existe un usuario con ese correo.");
        if (!ModelState.IsValid) return Page();

        u.NombreCompleto = NombreCompleto.Trim();
        u.PhoneNumber = string.IsNullOrWhiteSpace(Telefono) ? null : Telefono.Trim();
        if (!string.Equals(u.Email, Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            u.Email = Email.Trim();
            u.UserName = Email.Trim();
        }
        var r = await usuarios.UpdateAsync(u);
        if (r.Succeeded && !string.IsNullOrWhiteSpace(NuevaPassword))
        {
            var token = await usuarios.GeneratePasswordResetTokenAsync(u);
            r = await usuarios.ResetPasswordAsync(u, token, NuevaPassword);
        }
        if (r.Succeeded && EsAdmin != eraAdmin)
        {
            r = EsAdmin ? await usuarios.AddToRoleAsync(u, Roles.Admin) : await usuarios.RemoveFromRoleAsync(u, Roles.Admin);
            if (r.Succeeded && !EsAdmin && !await usuarios.IsInRoleAsync(u, Roles.Comercial)) await usuarios.AddToRoleAsync(u, Roles.Comercial);
        }
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            return Page();
        }
        if (EsUnoMismo) await sesion.RefreshSignInAsync(u); // mantiene la sesión si cambió su correo o contraseña
        TempData["Ok"] = $"Usuario {u.Email} actualizado.";
        return RedirectToPage("Index");
    }
}
