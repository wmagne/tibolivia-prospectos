using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Cuenta;

/// <summary>Cada usuario edita su nombre, teléfono (firma de los mensajes) y contraseña.</summary>
public class PerfilModel(UserManager<Usuario> usuarios, SignInManager<Usuario> sesion) : PageModel
{
    [BindProperty, Required(ErrorMessage = "El nombre es obligatorio")] public string NombreCompleto { get; set; } = "";
    [BindProperty] public string? Telefono { get; set; }
    [BindProperty, DataType(DataType.Password)] public string? PasswordActual { get; set; }
    [BindProperty, DataType(DataType.Password), MinLength(8, ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres")] public string? NuevaPassword { get; set; }
    public string Email { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var u = await usuarios.GetUserAsync(User);
        if (u is null) return Challenge();
        (NombreCompleto, Telefono, Email) = (u.NombreCompleto, u.PhoneNumber, u.Email ?? "");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var u = await usuarios.GetUserAsync(User);
        if (u is null) return Challenge();
        Email = u.Email ?? "";
        if (!string.IsNullOrWhiteSpace(NuevaPassword) && string.IsNullOrWhiteSpace(PasswordActual))
            ModelState.AddModelError(nameof(PasswordActual), "Escriba su contraseña actual para cambiarla.");
        if (!ModelState.IsValid) return Page();

        u.NombreCompleto = NombreCompleto.Trim();
        u.PhoneNumber = string.IsNullOrWhiteSpace(Telefono) ? null : Telefono.Trim();
        var r = await usuarios.UpdateAsync(u);
        if (r.Succeeded && !string.IsNullOrWhiteSpace(NuevaPassword))
            r = await usuarios.ChangePasswordAsync(u, PasswordActual!, NuevaPassword);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Code == "PasswordMismatch" ? "La contraseña actual no es correcta." : e.Description);
            return Page();
        }
        await sesion.RefreshSignInAsync(u);
        TempData["Ok"] = "Perfil actualizado.";
        return RedirectToPage();
    }
}
