using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Configuracion;

public class CorreoModel(
    IOptions<SmtpOptions> smtp,
    IOptions<ImapOptions> imap,
    IEnviadorEmail enviador,
    LectorRespuestasEmail lector) : PageModel
{
    public SmtpOptions Smtp => smtp.Value;
    public ImapOptions Imap => imap.Value;
    public bool ImapConfigurado => lector.Configurado;
    [BindProperty] public string? Destino { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostPruebaSmtpAsync()
    {
        if (string.IsNullOrWhiteSpace(Destino) || !Destino.Contains('@'))
        {
            TempData["Error"] = "Escriba un correo de destino válido.";
            return RedirectToPage();
        }
        var r = await enviador.EnviarAsync(new CorreoSaliente(Destino.Trim(), null,
            "Prueba del Sistema de Prospección TI Bolivia",
            "<p>Si está leyendo este mensaje, el envío de correos del sistema funciona correctamente.</p>",
            $"{Request.Scheme}://{Request.Host}/", null), HttpContext.RequestAborted);
        TempData[r.Exito ? "Ok" : "Error"] = r.Exito
            ? (Smtp.Simulacion ? "Modo simulación: el correo NO se envió. Configure la contraseña SMTP." : $"Correo de prueba enviado a {Destino}. Revise también la carpeta de spam.")
            : "No se pudo enviar: " + r.Error;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPruebaImapAsync()
    {
        try
        {
            var n = await lector.ProcesarAsync(HttpContext.RequestAborted);
            TempData[lector.Configurado ? "Ok" : "Error"] = lector.Configurado
                ? $"Conexión IMAP correcta. Respuestas nuevas de prospectos registradas: {n}."
                : "IMAP no configurado: falta servidor, usuario o contraseña.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error de conexión IMAP: " + ex.Message;
        }
        return RedirectToPage();
    }
}
