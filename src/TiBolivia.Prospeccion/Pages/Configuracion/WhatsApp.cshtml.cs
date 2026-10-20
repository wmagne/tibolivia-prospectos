using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Configuracion;

public class WhatsAppModel(
    IOptions<WhatsAppOptions> opciones,
    IOptions<ChatbotOptions> chatbot,
    DiagnosticoWhatsApp diagnostico,
    IEnviadorWhatsApp enviador,
    RenderizadorPlantillas renderizador,
    AppDbContext db) : PageModel
{
    public WhatsAppOptions O => opciones.Value;
    public bool ChatbotConfigurado => !string.IsNullOrWhiteSpace(chatbot.Value.Url);
    public string UrlWebhook => $"{Request.Scheme}://{Request.Host}/webhooks/whatsapp";
    public bool EsLocal => Request.Host.Host is "localhost" or "127.0.0.1";

    public List<Plantilla> PlantillasSistema { get; private set; } = [];
    public InfoNumero? Numero { get; private set; }
    public List<PlantillaMeta>? PlantillasMeta { get; private set; }
    public string? ErrorVerificacion { get; private set; }

    [BindProperty] public string? Telefono { get; set; }
    [BindProperty] public int PlantillaId { get; set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostVerificarAsync()
    {
        await CargarAsync();
        if (O.Simulacion)
        {
            ErrorVerificacion = "Falta configurar el Access Token y el Phone Number ID.";
            return Page();
        }
        try
        {
            Numero = await diagnostico.ObtenerNumeroAsync(HttpContext.RequestAborted);
            if (!string.IsNullOrWhiteSpace(O.BusinessAccountId))
                PlantillasMeta = await diagnostico.ListarPlantillasAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            ErrorVerificacion = ex.Message;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostPruebaAsync()
    {
        var telefono = Telefonos.Normalizar(Telefono);
        if (telefono is null)
        {
            TempData["Error"] = "Escriba un número válido (ej. 71234567 o +591 71234567).";
            return RedirectToPage();
        }

        ResultadoEnvio r;
        if (PlantillaId == 0)
        {
            // Plantilla que Meta crea en todas las cuentas: sirve para probar sin esperar aprobaciones.
            r = await enviador.EnviarPlantillaAsync(telefono, "hello_world", "en_US", [], HttpContext.RequestAborted);
        }
        else
        {
            var p = await db.Plantillas.FindAsync(PlantillaId);
            if (p?.WhatsAppPlantillaMeta is null) return RedirectToPage();
            var ejemplo = new Prospecto
            {
                Empresa = "Empresa de Prueba", NombreContacto = "Cliente Prueba", Telefono = telefono,
                AsignadoA = new Usuario { NombreCompleto = User.Identity?.Name ?? "Comercial" },
            };
            var servicio = new Servicio { Nombre = "Sistema para farmacias" };
            var parametros = (p.WhatsAppParametros ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => renderizador.Renderizar(v, ejemplo, servicio, html: false)).ToList();
            r = await enviador.EnviarPlantillaAsync(telefono, p.WhatsAppPlantillaMeta, p.WhatsAppIdioma, parametros, HttpContext.RequestAborted);
        }

        TempData[r.Exito ? "Ok" : "Error"] = r.Exito
            ? (O.Simulacion ? "Modo simulación: el mensaje NO se envió. Configure los datos de Meta." : $"Mensaje enviado a +{telefono}. Revise el WhatsApp de ese número.")
            : "No se pudo enviar: " + r.Error;
        return RedirectToPage();
    }

    /// <summary>Estado de una plantilla del sistema según lo que devolvió Meta.</summary>
    public string EstadoEnMeta(Plantilla p) =>
        PlantillasMeta is null ? "" :
        PlantillasMeta.FirstOrDefault(m => m.Nombre == p.WhatsAppPlantillaMeta && m.Idioma == p.WhatsAppIdioma)?.Estado
        ?? (PlantillasMeta.Any(m => m.Nombre == p.WhatsAppPlantillaMeta) ? "IDIOMA DISTINTO" : "NO EXISTE");

    private async Task CargarAsync() =>
        PlantillasSistema = await db.Plantillas
            .Where(p => p.Canal == Canal.WhatsApp && p.WhatsAppPlantillaMeta != null)
            .OrderBy(p => p.Nombre).ToListAsync();
}
