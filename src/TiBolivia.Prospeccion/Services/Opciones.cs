namespace TiBolivia.Prospeccion.Services;

public class SmtpOptions
{
    public string? Host { get; set; }
    /// <summary>465 = SSL/TLS directo (recomendado). 587 = STARTTLS (solo si la red bloquea el 465).</summary>
    public int Port { get; set; } = 465;
    /// <summary>true solo con el puerto 587.</summary>
    public bool UsarStartTls { get; set; }
    public string? Usuario { get; set; }
    public string? Password { get; set; }
    public string RemitenteNombre { get; set; } = "TI Bolivia";
    public string RemitenteEmail { get; set; } = "comercial@tibolivia.com";
    /// <summary>
    /// false (recomendado): las respuestas llegan a la casilla del remitente, que el sistema revisa por IMAP.
    /// true: las respuestas van directo al correo del comercial asignado (el sistema no las detecta).
    /// </summary>
    public bool ResponderAlComercial { get; set; }
    /// <summary>Sin servidor o sin contraseña los correos solo se registran (modo simulación).</summary>
    public bool Simulacion => string.IsNullOrWhiteSpace(Host) || (!string.IsNullOrWhiteSpace(Usuario) && string.IsNullOrWhiteSpace(Password));
}

/// <summary>Lectura de la casilla del remitente para detectar respuestas de los prospectos.</summary>
public class ImapOptions
{
    public bool Habilitado { get; set; } = true;
    public string? Host { get; set; }
    public int Port { get; set; } = 993;
    /// <summary>Si están vacíos se usan el usuario y la contraseña de Smtp.</summary>
    public string? Usuario { get; set; }
    public string? Password { get; set; }
    public string Carpeta { get; set; } = "INBOX";
    public int IntervaloSegundos { get; set; } = 120;
    /// <summary>Cuántos días hacia atrás revisar en cada lectura (los ya registrados se omiten).</summary>
    public int DiasARevisar { get; set; } = 3;
}

public static class ProveedoresWhatsApp
{
    /// <summary>API oficial de Meta (WhatsApp Business Cloud API). Recomendado para prospectos.</summary>
    public const string Meta = "Meta";
    /// <summary>TextMeBot: servicio no oficial que usa un WhatsApp normal vinculado por QR.</summary>
    public const string TextMeBot = "TextMeBot";
}

public class TextMeBotOptions
{
    public string? ApiKey { get; set; }
    public string Url { get; set; } = "https://api.textmebot.com/send.php";
    /// <summary>Pausa entre mensajes para no ser bloqueado por WhatsApp (TextMeBot recomienda varios segundos).</summary>
    public int SegundosEntreMensajes { get; set; } = 8;
    /// <summary>Clave que debe ir en la URL del webhook (?token=...) para aceptar mensajes entrantes.</summary>
    public string? TokenWebhook { get; set; }
}

public class WhatsAppOptions
{
    /// <summary>"Meta" (por defecto) o "TextMeBot".</summary>
    public string Proveedor { get; set; } = ProveedoresWhatsApp.Meta;
    public TextMeBotOptions TextMeBot { get; set; } = new();
    public bool EsMeta => !string.Equals(Proveedor, ProveedoresWhatsApp.TextMeBot, StringComparison.OrdinalIgnoreCase);

    public string ApiVersion { get; set; } = "v23.0";
    /// <summary>Dirección de la API de Meta (solo se cambia para pruebas).</summary>
    public string GraphUrl { get; set; } = "https://graph.facebook.com";
    /// <summary>Identificador del número en Meta (no es el número de teléfono).</summary>
    public string? PhoneNumberId { get; set; }
    /// <summary>Identificador de la cuenta de WhatsApp Business (WABA); permite consultar las plantillas.</summary>
    public string? BusinessAccountId { get; set; }
    /// <summary>Identificador de la app en developers.facebook.com (solo para conectar el webhook automáticamente).</summary>
    public string? AppId { get; set; }
    public string? AccessToken { get; set; }
    /// <summary>Token que se configura en Meta para verificar el webhook.</summary>
    public string? VerifyToken { get; set; }
    /// <summary>App Secret de Meta: valida la firma X-Hub-Signature-256 de los webhooks.</summary>
    public string? AppSecret { get; set; }
    /// <summary>Política de Meta: solo escribir a quien dio su consentimiento (opt-in).</summary>
    public bool RequerirConsentimiento { get; set; } = true;
    public bool Simulacion => EsMeta
        ? string.IsNullOrWhiteSpace(AccessToken) || string.IsNullOrWhiteSpace(PhoneNumberId)
        : string.IsNullOrWhiteSpace(TextMeBot.ApiKey);
    public string Url(string ruta) => $"{GraphUrl.TrimEnd('/')}/{ApiVersion}/{ruta}";
}

public class ChatbotOptions
{
    /// <summary>URL del chatbot de la página de TI Bolivia que recibe los mensajes entrantes de WhatsApp.</summary>
    public string? Url { get; set; }
    public string? ApiKey { get; set; }
    public int TimeoutSegundos { get; set; } = 20;
}

public class CampanasOptions
{
    public int IntervaloSegundos { get; set; } = 60;
    public int LoteMaximo { get; set; } = 50;
    public string ZonaHoraria { get; set; } = "America/La_Paz";
    public int HoraInicio { get; set; } = 8;
    public int HoraFin { get; set; } = 18;
    public bool EnviarFinDeSemana { get; set; }
    public int MaxIntentos { get; set; } = 3;
    /// <summary>URL pública del sistema (para enlaces de baja en los correos).</summary>
    public string UrlPublica { get; set; } = "https://localhost:7058";
    public AutoInscripcionOptions AutoInscripcion { get; set; } = new();
}

/// <summary>Inscripción gradual de los prospectos nuevos con correo en una campaña (ver InscripcionAutomatica).</summary>
public class AutoInscripcionOptions
{
    public bool Habilitada { get; set; }
    public string Campana { get; set; } = "Publicidad por rubro (correo)";
    public int MaximoPorDia { get; set; } = 40;
}

public record ResultadoEnvio(bool Exito, string? ProveedorId, string? Error)
{
    public static ResultadoEnvio Ok(string? id) => new(true, id, null);
    public static ResultadoEnvio Falla(string error) => new(false, null, error);
}
