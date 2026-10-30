using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Configuracion;

public class AsistenteMetaModel(
    IOptionsSnapshot<WhatsAppOptions> opciones,
    IOptions<SmtpOptions> smtp,
    IOptions<CampanasOptions> campanas,
    ConfiguradorMeta configurador,
    DiagnosticoWhatsApp diagnostico,
    AjustesGuardados ajustes,
    AppDbContext db) : PageModel
{
    public WhatsAppOptions O => opciones.Value;
    public string? PinGuardado => ajustes.Obtener(ConfiguradorMeta.ClavePin);
    public bool DatosBasicos => !string.IsNullOrWhiteSpace(O.AccessToken) && !string.IsNullOrWhiteSpace(O.PhoneNumberId);
    public bool DatosPlantillas => DatosBasicos && !string.IsNullOrWhiteSpace(O.BusinessAccountId);
    public bool DatosWebhook => DatosPlantillas && !string.IsNullOrWhiteSpace(O.AppId) && !string.IsNullOrWhiteSpace(O.AppSecret) && !string.IsNullOrWhiteSpace(O.VerifyToken);

    public List<(string Texto, bool Ok)> Resultados { get; } = [];
    public List<(Plantilla Plantilla, string Estado)> Plantillas { get; private set; } = [];

    [BindProperty] public string? Pin { get; set; }
    [BindProperty] public PerfilEmpresa Perfil { get; set; } = new(null, null, null, null, null);
    [BindProperty] public string? UrlPublica { get; set; }
    [BindProperty] public string CodigoPais { get; set; } = "591";
    [BindProperty] public string? NumeroNuevo { get; set; }
    [BindProperty] public string NombreVisible { get; set; } = "TIBolivia";
    [BindProperty] public string? NumeroId { get; set; }
    [BindProperty] public string? Codigo { get; set; }
    public List<NumeroMeta>? Numeros { get; private set; }

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
        {
            if (await EjecutarAsync("Número registrado en la API de WhatsApp. El PIN quedó guardado (cifrado) en el sistema.",
                    ct => configurador.RegistrarNumeroAsync(Pin, ct)))
                await ajustes.GuardarAsync(new Dictionary<string, string?> { [ConfiguradorMeta.ClavePin] = Pin }, new HashSet<string> { ConfiguradorMeta.ClavePin });
        }
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

    // ---------- Número real (alta, código de verificación y registro) ----------

    public async Task<IActionResult> OnPostAgregarNumeroAsync()
    {
        var numero = new string((NumeroNuevo ?? "").Where(char.IsDigit).ToArray());
        if (numero.Length < 7 || string.IsNullOrWhiteSpace(NombreVisible))
            Resultados.Add(("Escriba el número (sin código de país) y el nombre visible.", false));
        else
            await EjecutarAsync($"Número +{CodigoPais} {numero} agregado a la cuenta de WhatsApp. Ahora pida el código de verificación.",
                async ct => NumeroId = await configurador.AgregarNumeroAsync(CodigoPais, numero, NombreVisible.Trim(), ct));
        await CargarPlantillasAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCodigoAsync(string metodo)
    {
        if (!string.IsNullOrEmpty(NumeroId))
            await EjecutarAsync(metodo == "VOICE" ? "Meta está llamando al número para dictarle el código." : "Meta envió el código por SMS al número.",
                ct => configurador.SolicitarCodigoAsync(NumeroId, metodo == "VOICE" ? "VOICE" : "SMS", ct));
        await CargarPlantillasAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostVerificarAsync()
    {
        var codigo = new string((Codigo ?? "").Where(char.IsDigit).ToArray());
        if (!string.IsNullOrEmpty(NumeroId) && codigo.Length > 0)
            await EjecutarAsync("Código correcto: el número quedó verificado. Último paso: registrarlo con un PIN.",
                ct => configurador.VerificarCodigoAsync(NumeroId, codigo, ct));
        await CargarPlantillasAsync();
        return Page();
    }

    /// <summary>Registra el número en la API y lo deja como número de envío del sistema.</summary>
    public async Task<IActionResult> OnPostUsarNumeroAsync()
    {
        if (string.IsNullOrEmpty(NumeroId) || Pin is null || Pin.Length != 6 || !Pin.All(char.IsDigit))
            Resultados.Add(("El PIN debe tener exactamente 6 dígitos.", false));
        else if (await EjecutarAsync("Número registrado en la API de WhatsApp. Guarde el PIN: es la verificación en dos pasos del número.",
                     ct => configurador.RegistrarNumeroAsync(Pin, ct, NumeroId)))
        {
            await ajustes.GuardarAsync(new Dictionary<string, string?> { [ConfiguradorMeta.ClavePin] = Pin }, new HashSet<string> { ConfiguradorMeta.ClavePin });
            await ajustes.GuardarAsync(new Dictionary<string, string?> { ["WhatsApp:PhoneNumberId"] = NumeroId }, new HashSet<string>());
            Resultados.Add(("El sistema ahora envía desde este número.", true));
            await EjecutarAsync("App suscrita a la cuenta de WhatsApp (para recibir respuestas).", configurador.SuscribirAppAsync);
        }
        await CargarPlantillasAsync();
        return Page();
    }

    private async Task CargarPlantillasAsync()
    {
        if (DatosPlantillas)
        {
            try { Numeros = await diagnostico.ListarNumerosAsync(HttpContext.RequestAborted); }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            { Resultados.Add(("No se pudieron leer los números de la cuenta: " + ex.Message, false)); }
        }

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
