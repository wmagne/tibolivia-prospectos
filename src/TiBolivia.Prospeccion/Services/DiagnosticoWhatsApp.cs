using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TiBolivia.Prospeccion.Services;

public record InfoNumero(string? Numero, string? NombreVerificado, string? Calidad, string? EstadoNombre, string? LimiteMensajes);
public record NumeroMeta(string Id, string? Numero, string? Nombre, string? Verificacion, string? Estado, string? EstadoNombre);
public record PlantillaMeta(string Nombre, string Idioma, string Estado, string Categoria, string? Motivo = null);

/// <summary>Consultas de solo lectura a la API de Meta para verificar la configuración de WhatsApp.</summary>
public class DiagnosticoWhatsApp(HttpClient http, IOptionsSnapshot<WhatsAppOptions> opciones)
{
    private WhatsAppOptions O => opciones.Value;

    /// <summary>Datos del número: valida a la vez el token y el Phone Number ID.</summary>
    public async Task<InfoNumero> ObtenerNumeroAsync(CancellationToken ct)
    {
        using var doc = await GetAsync(O.Url($"{O.PhoneNumberId}?fields=display_phone_number,verified_name,quality_rating,name_status,messaging_limit_tier"), ct);
        var r = doc.RootElement;
        return new InfoNumero(Texto(r, "display_phone_number"), Texto(r, "verified_name"), Texto(r, "quality_rating"),
            Texto(r, "name_status"), Texto(r, "messaging_limit_tier"));
    }

    /// <summary>Números de la cuenta de WhatsApp (incluye los pendientes de verificar).</summary>
    public async Task<List<NumeroMeta>> ListarNumerosAsync(CancellationToken ct)
    {
        using var doc = await GetAsync(O.Url($"{O.BusinessAccountId}/phone_numbers?fields=id,display_phone_number,verified_name,code_verification_status,status,name_status"), ct);
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(n => new NumeroMeta(Texto(n, "id") ?? "", Texto(n, "display_phone_number"), Texto(n, "verified_name"),
                Texto(n, "code_verification_status"), Texto(n, "status"), Texto(n, "name_status"))).ToList();
    }

    /// <summary>Todas las cuentas de WhatsApp del portafolio dueño de la cuenta configurada, con sus números (texto para diagnóstico).</summary>
    public async Task<List<string>> InventarioPortafolioAsync(CancellationToken ct)
    {
        var lineas = new List<string>();
        using var waba = await GetAsync(O.Url($"{O.BusinessAccountId}?fields=name,owner_business_info"), ct);
        var negocio = waba.RootElement.TryGetProperty("owner_business_info", out var ob) ? ob : default;
        var negocioId = negocio.ValueKind == JsonValueKind.Object ? Texto(negocio, "id") : null;
        lineas.Add($"Cuenta configurada: {Texto(waba.RootElement, "name")} ({O.BusinessAccountId}) · Portafolio: {(negocio.ValueKind == JsonValueKind.Object ? Texto(negocio, "name") : "?")} ({negocioId})");
        if (negocioId is null) return lineas;
        foreach (var tipo in new[] { "owned_whatsapp_business_accounts", "client_whatsapp_business_accounts" })
        {
            try
            {
                using var cuentas = await GetAsync(O.Url($"{negocioId}/{tipo}?fields=id,name&limit=50"), ct);
                foreach (var c in cuentas.RootElement.GetProperty("data").EnumerateArray())
                {
                    lineas.Add($"[{tipo.Split('_')[0]}] Cuenta {Texto(c, "name")} ({Texto(c, "id")})");
                    using var nums = await GetAsync(O.Url($"{Texto(c, "id")}/phone_numbers?fields=id,display_phone_number,verified_name,code_verification_status,status,platform_type"), ct);
                    foreach (var n in nums.RootElement.GetProperty("data").EnumerateArray())
                        lineas.Add($"    {Texto(n, "display_phone_number")} · {Texto(n, "verified_name")} · id {Texto(n, "id")} · {Texto(n, "code_verification_status")} · {Texto(n, "status")} · {Texto(n, "platform_type")}");
                }
            }
            catch (InvalidOperationException ex) { lineas.Add($"No se pudo leer {tipo}: {ex.Message}"); }
        }
        return lineas;
    }

    public async Task<List<PlantillaMeta>> ListarPlantillasAsync(CancellationToken ct)
    {
        using var doc = await GetAsync(O.Url($"{O.BusinessAccountId}/message_templates?fields=name,language,status,category,rejected_reason&limit=200"), ct);
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(t => new PlantillaMeta(Texto(t, "name") ?? "", Texto(t, "language") ?? "", Texto(t, "status") ?? "", Texto(t, "category") ?? "", Texto(t, "rejected_reason")))
            .OrderBy(t => t.Nombre).ToList();
    }

    private async Task<JsonDocument> GetAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", O.AccessToken);
        using var resp = await http.SendAsync(req, ct);
        var cuerpo = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(ErrorLegible(cuerpo, (int)resp.StatusCode));
        return JsonDocument.Parse(cuerpo);
    }

    /// <summary>Traduce los errores más comunes de Meta a una explicación en español.</summary>
    public static string ErrorLegible(string cuerpo, int status)
    {
        string? mensaje = null, mensajeUsuario = null, detalles = null; int codigo = 0, subcodigo = 0;
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            var e = doc.RootElement.GetProperty("error");
            mensaje = Texto(e, "message");
            mensajeUsuario = Texto(e, "error_user_msg");
            if (e.TryGetProperty("code", out var c) && c.TryGetInt32(out var n)) codigo = n;
            if (e.TryGetProperty("error_subcode", out var sc) && sc.TryGetInt32(out var sn)) subcodigo = sn;
            if (e.TryGetProperty("error_data", out var ed))
                detalles = ed.ValueKind == JsonValueKind.Object ? Texto(ed, "details") : ed.ToString();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }

        var texto = (mensaje + " " + mensajeUsuario).ToLowerInvariant();
        var explicacion =
            texto.Contains("callback verification") || texto.Contains("callback url")
                ? "Meta no pudo verificar la URL del webhook. Revise que el sistema esté accesible desde internet (túnel o servidor) y que el Verify Token sea el mismo."
            : texto.Contains("already exists") || subcodigo is 2388023 or 2388024
                ? "Ya existe una plantilla con ese nombre e idioma en Meta (puede estar en revisión)."
            : codigo switch
            {
                190 => "El token de acceso no es válido o venció. Genere un token permanente (usuario del sistema) y vuelva a configurarlo.",
                10 or 200 => "El token no tiene permisos. Al generarlo marque whatsapp_business_messaging y whatsapp_business_management, y asigne la app y la cuenta de WhatsApp al usuario del sistema.",
                100 => "Algún dato enviado no es válido (identificador, texto de la plantilla o campo del perfil).",
                131030 => "El número de destino no está en la lista de números permitidos del número de prueba. Agréguelo en Configuración de la API → Para.",
                132001 => "La plantilla no existe o no está aprobada en ese idioma. Revise el nombre exacto y el idioma (es).",
                132000 => "La cantidad de variables no coincide con la plantilla aprobada en Meta.",
                131047 => "Pasaron más de 24 h desde el último mensaje del cliente: solo se puede enviar una plantilla aprobada.",
                131026 => "El número de destino no tiene WhatsApp o no puede recibir el mensaje.",
                133010 => "El número de envío no está registrado en la API. Use \"Registrar número\" en el asistente de configuración.",
                133005 => "El PIN no coincide con la verificación en dos pasos ya configurada para este número.",
                _ => null,
            };
        var detalle = (mensajeUsuario ?? mensaje ?? cuerpo)
            + (string.IsNullOrWhiteSpace(detalles) ? "" : " · Detalle: " + detalles)
            + (codigo != 0 ? $" · código {codigo}{(subcodigo != 0 ? "/" + subcodigo : "")}" : "");
        return explicacion is null ? $"Meta respondió HTTP {status}: {detalle}" : $"{explicacion} (Meta: {detalle})";
    }

    private static string? Texto(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var v) ? v.ToString() : null;
}
