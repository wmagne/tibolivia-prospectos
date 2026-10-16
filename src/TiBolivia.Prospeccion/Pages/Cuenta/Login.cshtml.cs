using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages.Cuenta;

public class LoginModel(SignInManager<Usuario> signIn) : PageModel
{
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [BindProperty] public bool Recordarme { get; set; }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (!ModelState.IsValid) return Page();
        var usuario = await signIn.UserManager.FindByEmailAsync(Email);
        if (usuario is { Activo: true })
        {
            var r = await signIn.PasswordSignInAsync(usuario, Password, Recordarme, lockoutOnFailure: true);
            if (r.Succeeded) return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
            if (r.IsLockedOut)
            {
                ModelState.AddModelError("", "Cuenta bloqueada temporalmente por intentos fallidos.");
                return Page();
            }
        }
        ModelState.AddModelError("", "Correo o contraseña incorrectos.");
        return Page();
    }
}
