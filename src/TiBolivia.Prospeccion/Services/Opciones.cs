namespace TiBolivia.Prospeccion.Services;

public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UsarStartTls { get; set; } = true;
    public string? Usuario { get; set; }
    public string? Password { get; set; }
    public string RemitenteNombre { get; set; } = "TI Bolivia";
    public string RemitenteEmail { get; set; } = "comercial@tibolivia.com";
    /// <summary>Si no hay Host configurado los correos solo se registran (modo simulación).</summary>
    public bool Simulacion => string.IsNullOrWhiteSpace(Host);
}

public class WhatsAppOptions
{
    public string ApiVersion { get; set; } = "v23.0";
    public string? PhoneNumberId { get; set; }
    public string? AccessToken { get; set; }
    /// <summary>Token que se configura en Meta para verificar el webhook.</summary>
    public string? VerifyToken { get; set; }
    /// <summary>App Secret de Meta: valida la firma X-Hub-Signature-256 de los webhooks.</summary>
    public string? AppSecret { get; set; }
    /// <summary>Política de Meta: solo escribir a quien dio su consentimiento (opt-in).</summary>
    public bool RequerirConsentimiento { get; set; } = true;
    public bool Simulacion => string.IsNullOrWhiteSpace(AccessToken) || string.IsNullOrWhiteSpace(PhoneNumberId);
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
}

public record ResultadoEnvio(bool Exito, string? ProveedorId, string? Error)
{
    public static ResultadoEnvio Ok(string? id) => new(true, id, null);
    public static ResultadoEnvio Falla(string error) => new(false, null, error);
}
