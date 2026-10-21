using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Configuracion;

public class WhatsAppModel(
    IOptionsSnapshot<WhatsAppOptions> opciones,
    IOptionsSnapshot<ChatbotOptions> chatbot,
    DiagnosticoWhatsApp diagnostico,
    IEnviadorWhatsApp enviador,
    RenderizadorPlantillas renderizador,
    AjustesGuardados ajustes,
    AppDbContext db) : PageModel
{
    private static readonly HashSet<string> Secretos =
        ["WhatsApp:AccessToken", "WhatsApp:AppSecret", "WhatsApp:TextMeBot:ApiKey", "Chatbot:ApiKey"];

    /// <summary>Datos de conexión editables desde la pantalla. Los secretos vacíos no cambian el valor guardado.</summary>
    public class FormConexion
    {
        public string Proveedor { get; set; } = ProveedoresWhatsApp.Meta;
        public string? PhoneNumberId { get; set; }
        public string? BusinessAccountId { get; set; }
        public string? AccessToken { get; set; }
        public string? AppId { get; set; }
        public string? AppSecret { get; set; }
        public string? VerifyToken { get; set; }
        public bool RequerirConsentimiento { get; set; } = true;
        public string? TextMeBotApiKey { get; set; }
        public string? TextMeBotTokenWebhook { get; set; }
        public int SegundosEntreMensajes { get; set; } = 8;
        public string? ChatbotUrl { get; set; }
        public string? ChatbotApiKey { get; set; }
    }

    [BindProperty] public FormConexion Conexion { get; set; } = new();
    public bool TieneToken => !string.IsNullOrEmpty(O.AccessToken);
    public bool TieneAppSecret => !string.IsNullOrEmpty(O.AppSecret);
    public bool TieneTextMeBot => !string.IsNullOrEmpty(O.TextMeBot.ApiKey);
    public bool TieneChatbotKey => !string.IsNullOrEmpty(chatbot.Value.ApiKey);

    private void LlenarConexion() => Conexion = new FormConexion
    {
        Proveedor = O.EsMeta ? ProveedoresWhatsApp.Meta : ProveedoresWhatsApp.TextMeBot,
        PhoneNumberId = O.PhoneNumberId, BusinessAccountId = O.BusinessAccountId, AppId = O.AppId,
        VerifyToken = O.VerifyToken, RequerirConsentimiento = O.RequerirConsentimiento,
        TextMeBotTokenWebhook = O.TextMeBot.TokenWebhook, SegundosEntreMensajes = O.TextMeBot.SegundosEntreMensajes,
        ChatbotUrl = chatbot.Value.Url,
    };

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        var c = Conexion;
        string? Texto(string? v) => v?.Trim() ?? "";          // "" = volver al valor por defecto
        string? Secreto(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim(); // vacío = no cambiar
        if (c.ChatbotUrl is { Length: > 0 } u && !Uri.TryCreate(u.Trim(), UriKind.Absolute, out _))
        {
            TempData["Error"] = "La URL del chatbot no es válida (debe empezar con https://).";
            return RedirectToPage();
        }
        await ajustes.GuardarAsync(new Dictionary<string, string?>
        {
            ["WhatsApp:Proveedor"] = c.Proveedor == ProveedoresWhatsApp.TextMeBot ? ProveedoresWhatsApp.TextMeBot : ProveedoresWhatsApp.Meta,
            ["WhatsApp:PhoneNumberId"] = Texto(c.PhoneNumberId),
            ["WhatsApp:BusinessAccountId"] = Texto(c.BusinessAccountId),
            ["WhatsApp:AppId"] = Texto(c.AppId),
            ["WhatsApp:VerifyToken"] = Texto(c.VerifyToken),
            ["WhatsApp:RequerirConsentimiento"] = c.RequerirConsentimiento.ToString(),
            ["WhatsApp:TextMeBot:TokenWebhook"] = Texto(c.TextMeBotTokenWebhook),
            ["WhatsApp:TextMeBot:SegundosEntreMensajes"] = Math.Clamp(c.SegundosEntreMensajes, 3, 120).ToString(),
            ["Chatbot:Url"] = Texto(c.ChatbotUrl),
            ["WhatsApp:AccessToken"] = Secreto(c.AccessToken),
            ["WhatsApp:AppSecret"] = Secreto(c.AppSecret),
            ["WhatsApp:TextMeBot:ApiKey"] = Secreto(c.TextMeBotApiKey),
            ["Chatbot:ApiKey"] = Secreto(c.ChatbotApiKey),
        }, Secretos);
        TempData["Ok"] = "Configuración de WhatsApp guardada y aplicada. Use \"Verificar ahora\" para probar la conexión.";
        return RedirectToPage();
    }

    public WhatsAppOptions O => opciones.Value;
    public bool ChatbotConfigurado => !string.IsNullOrWhiteSpace(chatbot.Value.Url);
    public string UrlWebhook => O.EsMeta
        ? $"{Request.Scheme}://{Request.Host}/webhooks/whatsapp"
        : $"{Request.Scheme}://{Request.Host}/webhooks/textmebot?token={O.TextMeBot.TokenWebhook ?? "(configure TokenWebhook)"}";
    public bool EsLocal => Request.Host.Host is "localhost" or "127.0.0.1";

    public List<Plantilla> PlantillasSistema { get; private set; } = [];
    public InfoNumero? Numero { get; private set; }
    public List<PlantillaMeta>? PlantillasMeta { get; private set; }
    public string? ErrorVerificacion { get; private set; }

    [BindProperty] public string? Telefono { get; set; }
    [BindProperty] public int PlantillaId { get; set; }

    public async Task OnGetAsync()
    {
        LlenarConexion();
        await CargarAsync();
    }

    public async Task<IActionResult> OnPostVerificarAsync()
    {
        LlenarConexion();
        await CargarAsync();
        if (!O.EsMeta)
        {
            ErrorVerificacion = "La verificación con Meta no aplica a TextMeBot: use \"Enviar mensaje de prueba\".";
            return Page();
        }
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
        if (!enviador.RequierePlantillasAprobadas)
        {
            // TextMeBot: se envía el texto de la plantilla del sistema tal como lo recibiría un prospecto.
            var p = PlantillaId == 0 ? null : await db.Plantillas.FindAsync(PlantillaId);
            var texto = p is null
                ? "Mensaje de prueba del Sistema de Prospección TI Bolivia. Si lo recibe, el envío por WhatsApp funciona."
                : renderizador.Renderizar(p.Cuerpo, Ejemplo(telefono), new Servicio { Nombre = "Sistema para farmacias" }, html: false);
            r = await enviador.EnviarTextoAsync(telefono, texto, HttpContext.RequestAborted);
        }
        else if (PlantillaId == 0)
        {
            // Plantilla que Meta crea en todas las cuentas: sirve para probar sin esperar aprobaciones.
            r = await enviador.EnviarPlantillaAsync(telefono, "hello_world", "en_US", [], HttpContext.RequestAborted);
        }
        else
        {
            var p = await db.Plantillas.FindAsync(PlantillaId);
            if (p?.WhatsAppPlantillaMeta is null) return RedirectToPage();
            var ejemplo = Ejemplo(telefono);
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

    private Prospecto Ejemplo(string telefono) => new()
    {
        Empresa = "Empresa de Prueba", NombreContacto = "Cliente Prueba", Telefono = telefono,
        AsignadoA = new Usuario { NombreCompleto = User.Identity?.Name ?? "Comercial" },
    };

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
