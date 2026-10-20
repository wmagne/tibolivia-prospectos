using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Configuracion;

public class AsistenteMetaModel(
    IOptions<WhatsAppOptions> opciones,
    IOptions<SmtpOptions> smtp,
    IOptions<CampanasOptions> campanas,
    ConfiguradorMeta configurador,
    DiagnosticoWhatsApp diagnostico,
    AppDbContext db) : PageModel
{
    public WhatsAppOptions O => opciones.Value;
    public bool DatosBasicos => !string.IsNullOrWhiteSpace(O.AccessToken) && !string.IsNullOrWhiteSpace(O.PhoneNumberId);
    public bool DatosPlantillas => DatosBasicos && !string.IsNullOrWhiteSpace(O.BusinessAccountId);
    public bool DatosWebhook => DatosPlantillas && !string.IsNullOrWhiteSpace(O.AppId) && !string.IsNullOrWhiteSpace(O.AppSecret) && !string.IsNullOrWhiteSpace(O.VerifyToken);

    public List<(string Texto, bool Ok)> Resultados { get; } = [];
    public List<(Plantilla Plantilla, string Estado)> Plantillas { get; private set; } = [];

    [BindProperty] public string? Pin { get; set; }
    [BindProperty] public PerfilEmpresa Perfil { get; set; } = new(null, null, null, null, null);
    [BindProperty] public string? UrlPublica { get; set; }

    public async Task OnGetAsync()
    {
        Perfil = new PerfilEmpresa(
            "Software para empresas en Bolivia: farmacias, facturación SIAT y sistemas a medida.",
            "TI Bolivia desarrolla sistemas para farmacias, facturación electrónica SIAT y software a medida para empresas bolivianas.",
            smtp.Value.RemitenteEmail, "https://tibolivia.com", null);
        UrlPublica = campanas.Value.UrlPublica;
        await CargarPlantillasAsync();
    }

    public async Task<IActionResult> OnPostRegistrarAsync()
    {
        if (Pin is null || Pin.Length != 6 || !Pin.All(char.IsDigit))
            Resultados.Add(("El PIN debe tener exactamente 6 dígitos.", false));
        else
            await EjecutarAsync("Número registrado en la API de WhatsApp. Guarde el PIN: es la verificación en dos pasos del número.",
                ct => configurador.RegistrarNumeroAsync(Pin, ct));
        await CargarPlantillasAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPerfilAsync()
    {
        await EjecutarAsync("Perfil de empresa actualizado. Se verá en WhatsApp al abrir el contacto de TI Bolivia.",
            ct => configurador.ActualizarPerfilAsync(Perfil, ct));
        await CargarPlantillasAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPlantillasAsync()
    {
        await CargarPlantillasAsync();
        var faltantes = Plantillas.Where(p => p.Estado is "NO EXISTE").Select(p => p.Plantilla).ToList();
        if (faltantes.Count == 0) Resultados.Add(("No hay plantillas por crear: todas existen en Meta.", true));
        foreach (var p in faltantes)
            await EjecutarAsync($"Plantilla {p.WhatsAppPlantillaMeta} enviada a revisión de Meta.",
                ct => configurador.CrearPlantillaAsync(p, ct), $"{p.WhatsAppPlantillaMeta}: ");
        await CargarPlantillasAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostWebhookAsync()
    {
        var url = $"{(UrlPublica ?? "").TrimEnd('/')}/webhooks/whatsapp";
        if (!url.StartsWith("https://") || url.Contains("localhost"))
            Resultados.Add(("La URL pública debe empezar con https:// y ser accesible desde internet (no localhost).", false));
        else if (await EjecutarAsync("App suscrita a su cuenta de WhatsApp.", configurador.SuscribirAppAsync))
            await EjecutarAsync($"Webhook conectado: Meta enviará las respuestas a {url}", ct => configurador.ConfigurarWebhookAsync(url, ct));
        await CargarPlantillasAsync();
        return Page();
    }

    private async Task<bool> EjecutarAsync(string mensajeOk, Func<CancellationToken, Task> accion, string prefijoError = "")
    {
        try
        {
            await accion(HttpContext.RequestAborted);
            Resultados.Add((mensajeOk, true));
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            Resultados.Add((prefijoError + ex.Message, false));
            return false;
        }
    }

    private async Task CargarPlantillasAsync()
    {
        var sistema = await db.Plantillas.Where(p => p.Canal == Canal.WhatsApp && p.WhatsAppPlantillaMeta != null)
            .OrderBy(p => p.Nombre).ToListAsync();
        List<PlantillaMeta>? meta = null;
        if (DatosPlantillas)
        {
            try { meta = await diagnostico.ListarPlantillasAsync(HttpContext.RequestAborted); }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            { Resultados.Add(("No se pudieron leer las plantillas de Meta: " + ex.Message, false)); }
        }
        Plantillas = sistema.Select(p => (p,
            meta is null ? "" :
            meta.FirstOrDefault(m => m.Nombre == p.WhatsAppPlantillaMeta && m.Idioma == p.WhatsAppIdioma)?.Estado ?? "NO EXISTE")).ToList();
    }
}
