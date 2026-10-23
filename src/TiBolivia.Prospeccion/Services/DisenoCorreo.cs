namespace TiBolivia.Prospeccion.Services;

/// <summary>Diseño de TIBolivia para todos los correos (tablas y estilos en línea, compatibles con Gmail y Outlook).</summary>
public static class DisenoCorreo
{
    public static string Envolver(string contenido, string linkBaja) => $"""
        <!DOCTYPE html>
        <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#eef2f8;">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#eef2f8;padding:24px 12px;">
          <tr><td align="center">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:620px;background:#ffffff;border-radius:12px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
              <tr><td style="background:#0b3d91;background:linear-gradient(90deg,#0b3d91,#1565c0);padding:20px 28px;">
                <span style="display:inline-block;background:#ffffff;color:#0b3d91;font-weight:bold;border-radius:6px;padding:2px 7px;font-size:16px;">TI</span>
                <span style="color:#ffffff;font-size:20px;font-weight:bold;vertical-align:middle;">&nbsp;Bolivia</span>
                <div style="color:#cfe0ff;font-size:12px;margin-top:4px;">Desarrollo de software · Santa Cruz de la Sierra</div>
              </td></tr>
              <tr><td style="padding:28px;font-size:15px;line-height:1.55;">
        {contenido}
              </td></tr>
              <tr><td style="background:#f6f8fb;padding:16px 28px;font-size:11px;color:#6b7280;line-height:1.5;">
                TIBolivia · Apps móviles, sistemas a medida, CRM, correo corporativo y outsourcing de sistemas.<br>
                Recibe este correo porque su empresa podría beneficiarse de nuestros servicios.
                Si no desea recibir más mensajes, <a href="{linkBaja}" style="color:#6b7280;">haga clic aquí para darse de baja</a>.
              </td></tr>
            </table>
          </td></tr>
        </table>
        </body></html>
        """;
}
