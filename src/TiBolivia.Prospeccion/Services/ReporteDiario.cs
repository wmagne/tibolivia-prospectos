using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Pages;

namespace TiBolivia.Prospeccion.Services;

/// <summary>Arma y envía por correo el resumen de actividades del día (envíos, respuestas, rebotes, tareas y campañas).</summary>
public class ReporteDiario(AppDbContext db, IEnviadorEmail enviador, IOptions<ReportesOptions> opciones,
    IOptions<SmtpOptions> smtp, IOptions<CampanasOptions> campanas)
{
    private static readonly string[] Dias = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    public async Task<ResultadoEnvio> EnviarAsync(DateTime dia, CancellationToken ct)
    {
        var o = opciones.Value;
        if (string.IsNullOrWhiteSpace(o.Para)) return ResultadoEnvio.Falla("Falta el destinatario del reporte (Reportes:Para).");
        var html = await ArmarAsync(dia, ct);
        return await enviador.EnviarAsync(new CorreoSaliente(o.Para, null, $"Reporte diario de prospección · {dia:dd/MM/yyyy}", html,
            campanas.Value.UrlPublica, null, Copia: o.Copia, Interno: true), ct);
    }

    public async Task<string> ArmarAsync(DateTime dia, CancellationToken ct)
    {
        var desde = Fechas.InicioDelDiaUtc(dia).AddHours(-8); // medianoche en Bolivia
        var hasta = desde.AddDays(1);
        var finManana = hasta.AddDays(1);

        var mensajes = await db.Mensajes.Where(m => m.CreadoEn >= desde && m.CreadoEn < hasta)
            .Select(m => new { m.Canal, m.Direccion, m.Estado, m.Asunto, m.ProveedorId, Empresa = m.Prospecto.Empresa }).ToListAsync(ct);
        int Contar(Canal c, DireccionMensaje d, EstadoMensaje e) => mensajes.Count(m => m.Canal == c && m.Direccion == d && m.Estado == e);
        var respuestas = mensajes.Where(m => m.Direccion == DireccionMensaje.Entrante && m.Estado == EstadoMensaje.Recibido).ToList();
        var whatsAppManual = mensajes.Count(m => m.Canal == Canal.WhatsApp && m.Direccion == DireccionMensaje.Saliente && m.ProveedorId != null && m.ProveedorId.StartsWith("manual-"));
        var whatsAppAuto = Contar(Canal.WhatsApp, DireccionMensaje.Saliente, EstadoMensaje.Enviado) - whatsAppManual;

        var llamadas = await db.Interacciones.CountAsync(i => i.Fecha >= desde && i.Fecha < hasta && i.Tipo == TipoInteraccion.Llamada, ct);
        var nuevos = await db.Prospectos.Where(p => p.CreadoEn >= desde && p.CreadoEn < hasta)
            .GroupBy(p => p.Origen ?? "-").Select(g => new { Origen = g.Key, Total = g.Count(), ConCorreo = g.Count(p => p.Email != null && p.Email != "") })
            .ToListAsync(ct);
        var inscritosHoy = await db.Inscripciones.Where(i => i.CreadoEn >= desde && i.CreadoEn < hasta)
            .GroupBy(i => i.Campana.Nombre).Select(g => new { Campana = g.Key, Total = g.Count() }).ToListAsync(ct);

        var tareasVencidas = await db.Prospectos.CountAsync(p => p.ProximaAccion != null && p.FechaProximaAccion < hasta, ct);
        var tareasManana = await db.Prospectos.CountAsync(p => p.ProximaAccion != null && p.FechaProximaAccion >= hasta && p.FechaProximaAccion < finManana, ct);
        var enviosManana = await db.Inscripciones.CountAsync(i => i.Estado == EstadoInscripcion.Activa && i.Campana.Activa && i.ProximoEnvio != null && i.ProximoEnvio < finManana, ct);

        var campanasActivas = await db.Campanas.Where(c => c.Activa).OrderBy(c => c.Nombre).Select(c => new
        {
            c.Nombre,
            EnCurso = c.Inscripciones.Count(i => i.Estado == EstadoInscripcion.Activa),
            Terminadas = c.Inscripciones.Count(i => i.Estado == EstadoInscripcion.Completada),
            Detenidas = c.Inscripciones.Count(i => i.Estado == EstadoInscripcion.Detenida),
        }).Where(c => c.EnCurso + c.Terminadas + c.Detenidas > 0).ToListAsync(ct);

        var totalProspectos = await db.Prospectos.CountAsync(ct);
        var porEstado = await db.Prospectos.GroupBy(p => p.Estado).Select(g => new { Estado = g.Key, Total = g.Count() }).ToListAsync(ct);

        var sb = new StringBuilder();
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        void Fila(string etiqueta, object valor) =>
            sb.Append($"<tr><td style=\"padding:6px 12px;border-bottom:1px solid #e3e8f0;\">{E(etiqueta)}</td><td style=\"padding:6px 12px;border-bottom:1px solid #e3e8f0;text-align:right;font-weight:bold;\">{valor}</td></tr>");
        void Titulo(string t) => sb.Append($"<h3 style=\"color:#0d47a1;margin:24px 0 8px;font-size:17px;\">{E(t)}</h3>");
        const string tabla = "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;border-collapse:collapse;font-size:14px;\">";

        sb.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:680px;margin:0 auto;color:#1f2937;\">");
        sb.Append("<div style=\"background:#0d47a1;color:#fff;padding:18px 24px;border-radius:10px 10px 0 0;\">");
        sb.Append($"<div style=\"font-size:20px;font-weight:bold;\">Reporte diario de prospección</div><div style=\"opacity:.85;\">TIBolivia · {Dias[(int)dia.DayOfWeek]} {dia:dd/MM/yyyy}</div></div>");
        sb.Append("<div style=\"background:#fff;border:1px solid #e3e8f0;border-top:0;padding:8px 24px 24px;border-radius:0 0 10px 10px;\">");

        Titulo("Actividad del día");
        sb.Append(tabla);
        Fila("Correos enviados", Contar(Canal.Email, DireccionMensaje.Saliente, EstadoMensaje.Enviado));
        Fila("Correos con error de envío", Contar(Canal.Email, DireccionMensaje.Saliente, EstadoMensaje.Fallido));
        Fila("Rebotes (direcciones que no existen)", Contar(Canal.Email, DireccionMensaje.Entrante, EstadoMensaje.Fallido));
        Fila("Respuestas de empresas", respuestas.Count);
        Fila("WhatsApp enviados desde el celular (Tareas)", whatsAppManual);
        if (whatsAppAuto > 0) Fila("WhatsApp automáticos", whatsAppAuto);
        Fila("Llamadas registradas", llamadas);
        sb.Append("</table>");

        if (respuestas.Count > 0)
        {
            Titulo("Respuestas recibidas: contáctelas hoy");
            sb.Append("<ul style=\"font-size:14px;padding-left:18px;\">");
            foreach (var r in respuestas) sb.Append($"<li><b>{E(r.Empresa)}</b> ({r.Canal}): {E(r.Asunto)}</li>");
            sb.Append("</ul>");
        }

        Titulo("Empresas nuevas cargadas");
        if (nuevos.Count == 0) sb.Append("<p style=\"font-size:14px;color:#6b7280;\">Hoy no se cargaron empresas nuevas.</p>");
        else
        {
            sb.Append(tabla);
            foreach (var n in nuevos) Fila($"{n.Origen.Replace("Lista: ", "")} ({n.ConCorreo} con correo)", n.Total);
            sb.Append("</table>");
        }
        if (inscritosHoy.Count > 0)
        {
            Titulo("Inscritas hoy en campañas");
            sb.Append(tabla);
            foreach (var i in inscritosHoy) Fila(i.Campana, i.Total);
            sb.Append("</table>");
        }

        Titulo("Para mañana");
        sb.Append(tabla);
        Fila("Envíos programados de campañas", enviosManana);
        Fila("Tareas (WhatsApp y llamadas) para mañana", tareasManana);
        Fila("Tareas pendientes o vencidas", tareasVencidas);
        sb.Append("</table>");

        Titulo("Campañas activas");
        sb.Append(tabla);
        sb.Append("<tr style=\"color:#6b7280;font-size:12px;\"><td style=\"padding:4px 12px;\">Campaña</td><td style=\"padding:4px 12px;text-align:right;\">En curso · terminadas · detenidas</td></tr>");
        foreach (var c in campanasActivas) Fila(c.Nombre, $"{c.EnCurso} · {c.Terminadas} · {c.Detenidas}");
        sb.Append("</table>");

        Titulo($"Base de prospectos: {totalProspectos} empresas");
        sb.Append("<p style=\"font-size:14px;\">");
        sb.Append(string.Join(" · ", porEstado.OrderByDescending(x => x.Total).Select(x => $"{E(Etiquetas.Texto(x.Estado))}: <b>{x.Total}</b>")));
        sb.Append("</p>");

        var url = campanas.Value.UrlPublica.TrimEnd('/');
        sb.Append($"<p style=\"margin-top:24px;\"><a href=\"{url}/Tareas\" style=\"background:#1565c0;color:#fff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:bold;\">Ver tareas de hoy</a> ");
        sb.Append($"<a href=\"{url}/Mensajes\" style=\"color:#1565c0;margin-left:12px;\">Ver mensajes</a></p>");
        sb.Append($"<p style=\"font-size:12px;color:#9ca3af;margin-top:24px;\">Enviado automáticamente por el Sistema de Prospección ({E(smtp.Value.RemitenteEmail)}).</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }
}

/// <summary>Envía el reporte una vez por día, después de la hora configurada (hora de Bolivia).</summary>
public class WorkerReporteDiario(IServiceScopeFactory scopes, IOptions<ReportesOptions> opciones, AjustesGuardados ajustes,
    ILogger<WorkerReporteDiario> log) : BackgroundService
{
    private const string ClaveUltimo = "Reportes:UltimoEnvio";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                var o = opciones.Value;
                var ahora = Fechas.ABolivia(DateTime.UtcNow);
                var hoy = ahora.Date.ToString("yyyy-MM-dd");
                if (!o.Habilitado || ahora.TimeOfDay < new TimeSpan(o.HoraEnvio, o.MinutoEnvio, 0) || ajustes.Obtener(ClaveUltimo) == hoy) continue;

                using var scope = scopes.CreateScope();
                var r = await scope.ServiceProvider.GetRequiredService<ReporteDiario>().EnviarAsync(ahora.Date, ct);
                if (r.Exito)
                {
                    await ajustes.GuardarAsync(new Dictionary<string, string?> { [ClaveUltimo] = hoy }, new HashSet<string>());
                    log.LogInformation("Reporte diario enviado a {Para} (copia {Copia})", o.Para, o.Copia);
                }
                else log.LogWarning("No se pudo enviar el reporte diario: {Error}", r.Error);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogError(ex, "Error en el reporte diario");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
