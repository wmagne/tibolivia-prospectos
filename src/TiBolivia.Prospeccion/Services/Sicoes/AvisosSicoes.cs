using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services.Sicoes;

/// <summary>
/// Avisos del módulo SICOES: resumen diario por correo, WhatsApp al momento para Consultas de Precios y cierres
/// en menos de 3 días, y recordatorio 48 horas antes del cierre. Cada aviso se registra en SicoesAvisos para no repetirlo.
/// En modo pruebas solo salen a los destinos permitidos en la configuración: nada a terceros y nada masivo.
/// </summary>
public class AvisosSicoes(AppDbContext db, IEnviadorEmail correo, IEnviadorWhatsApp whatsapp, IOptions<SicoesOptions> opciones,
    IOptions<CampanasOptions> campanas, ILogger<AvisosSicoes> log)
{
    private static readonly EstadoOportunidadSicoes[] Cerradas =
        [EstadoOportunidadSicoes.Ganada, EstadoOportunidadSicoes.Perdida, EstadoOportunidadSicoes.Descartada];

    public async Task<int> EnviarResumenDiarioAsync(DateTime hoyBolivia, CancellationToken ct)
    {
        var clave = hoyBolivia.ToString("yyyy-MM-dd");
        var enviados = 0;
        var desde = DateTime.UtcNow.AddHours(-24);
        var ultima = await db.SicoesLecturas.OrderByDescending(l => l.Inicio).FirstOrDefaultAsync(ct);
        foreach (var perfil in await db.SicoesPerfiles.Where(p => p.Activo && p.CorreoAvisos != null).ToListAsync(ct))
        {
            if (await db.SicoesAvisos.AnyAsync(a => a.Tipo == "resumen" && a.Clave == clave && a.Destino == perfil.CorreoAvisos, ct)) continue;
            var nuevas = await Abiertas(perfil.Id).Where(o => o.CreadoEn >= desde).OrderByDescending(o => o.Puntaje).Take(30).ToListAsync(ct);
            var cierran = await Abiertas(perfil.Id)
                .Where(o => o.Proceso.FechaLimite > DateTime.UtcNow && o.Proceso.FechaLimite < DateTime.UtcNow.AddDays(opciones.Value.DiasCierreUrgente))
                .OrderBy(o => o.Proceso.FechaLimite).Take(20).ToListAsync(ct);
            var html = ArmarResumen(perfil, nuevas, cierran, ultima, hoyBolivia);
            var asunto = nuevas.Count > 0
                ? $"SICOES: {nuevas.Count} oportunidades nuevas para {perfil.Nombre} · {hoyBolivia:dd/MM}"
                : $"SICOES: sin oportunidades nuevas para {perfil.Nombre} · {hoyBolivia:dd/MM}";
            if (await EnviarCorreoAsync(perfil.CorreoAvisos!, asunto, html, "resumen", null, clave, ct)) enviados++;
        }
        return enviados;
    }

    public async Task<int> EnviarUrgentesAsync(CancellationToken ct)
    {
        var limite = DateTime.UtcNow.AddDays(opciones.Value.DiasCierreUrgente);
        var lista = await db.SicoesOportunidades.Include(o => o.Proceso).Include(o => o.Perfil)
            .Where(o => o.Perfil.Activo && o.Perfil.WhatsAppAvisos != null && !Cerradas.Contains(o.Estado)
                && (o.Proceso.FechaLimite == null || o.Proceso.FechaLimite > DateTime.UtcNow)
                && !db.SicoesAvisos.Any(a => a.OportunidadId == o.Id && a.Tipo == "urgente"))
            .ToListAsync(ct);
        var enviados = 0;
        foreach (var o in lista.Where(o => o.Proceso.EsConsultaDePrecios || (o.Proceso.FechaLimite is { } f && f < limite)).Take(20))
        {
            var motivo = o.Proceso.EsConsultaDePrecios ? "Consulta de Precios" : "Cierra pronto";
            if (await EnviarWhatsAppAsync(o, "urgente", $"SICOES · {motivo}", ct)) enviados++;
        }
        return enviados;
    }

    public async Task<int> EnviarRecordatoriosAsync(CancellationToken ct)
    {
        var limite = DateTime.UtcNow.AddHours(opciones.Value.HorasRecordatorio);
        var lista = await db.SicoesOportunidades.Include(o => o.Proceso).Include(o => o.Perfil)
            .Where(o => o.Perfil.Activo && !Cerradas.Contains(o.Estado)
                && o.Proceso.FechaLimite > DateTime.UtcNow && o.Proceso.FechaLimite <= limite
                && !db.SicoesAvisos.Any(a => a.OportunidadId == o.Id && a.Tipo == "recordatorio48h"))
            .Take(20).ToListAsync(ct);
        var enviados = 0;
        foreach (var o in lista)
        {
            var ok = false;
            if (o.Perfil.CorreoAvisos is { } para)
            {
                var html = Envolver($"<p style=\"font-size:15px;\">Faltan menos de {opciones.Value.HorasRecordatorio} horas para el cierre de este proceso.</p>"
                    + Ficha(o), "Recordatorio de cierre");
                ok |= await EnviarCorreoAsync(para, $"SICOES: cierra en menos de 48 h · {Corto(o.Proceso.Objeto, 60)}", html, "recordatorio48h", o.Id, null, ct);
            }
            if (o.Perfil.WhatsAppAvisos is not null) ok |= await EnviarWhatsAppAsync(o, "recordatorio48h", "SICOES · cierra en 48 h", ct);
            if (ok) enviados++;
        }
        return enviados;
    }

    private IQueryable<SicoesOportunidad> Abiertas(int perfilId) =>
        db.SicoesOportunidades.Include(o => o.Proceso).Where(o => o.PerfilId == perfilId && !Cerradas.Contains(o.Estado));

    /// <summary>En pruebas, un destino que no está en la lista permitida no recibe nada, aunque el perfil lo tenga cargado.</summary>
    public bool DestinoPermitido(string destino, bool esWhatsApp)
    {
        var o = opciones.Value;
        if (!o.ModoPruebas) return true;
        if (esWhatsApp)
        {
            var tel = Telefonos.Normalizar(destino);
            return tel is not null && o.WhatsAppPermitidos.Any(p => Telefonos.Normalizar(p) == tel);
        }
        return o.CorreosPermitidos.Any(p => p.Equals(destino.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> EnviarCorreoAsync(string para, string asunto, string html, string tipo, int? oportunidadId, string? clave, CancellationToken ct)
    {
        var aviso = new SicoesAviso { Tipo = tipo, Canal = "Email", Destino = para, OportunidadId = oportunidadId, Clave = clave };
        if (!DestinoPermitido(para, false))
        {
            aviso.Error = "Destino no permitido en modo pruebas";
            log.LogWarning("SICOES: no se envía {Tipo} a {Destino} (no permitido en pruebas)", tipo, para);
        }
        else
        {
            var url = campanas.Value.UrlPublica.TrimEnd('/');
            var r = await correo.EnviarAsync(new CorreoSaliente(para, null, asunto, html, url, null, Interno: true), ct);
            aviso.Exito = r.Exito;
            aviso.Error = r.Error is { Length: > 1000 } e ? e[..1000] : r.Error;
        }
        db.SicoesAvisos.Add(aviso);
        await db.SaveChangesAsync(ct);
        return aviso.Exito;
    }

    private async Task<bool> EnviarWhatsAppAsync(SicoesOportunidad o, string tipo, string titulo, CancellationToken ct)
    {
        var destino = o.Perfil.WhatsAppAvisos!;
        var tel = Telefonos.Normalizar(destino);
        var aviso = new SicoesAviso { Tipo = tipo, Canal = "WhatsApp", Destino = destino, OportunidadId = o.Id };
        if (tel is null || !DestinoPermitido(destino, true))
            aviso.Error = tel is null ? "Número inválido" : "Destino no permitido en modo pruebas";
        else
        {
            var p = o.Proceso;
            var monto = p.Monto is { } m ? $"Bs {m:N0}" : "monto sin dato";
            var cierre = p.FechaLimite is { } f ? Fechas.ABolivia(f).ToString("dd/MM HH:mm") : "sin fecha";
            var plantilla = opciones.Value.PlantillaWhatsApp;
            // Meta solo deja iniciar la conversación con una plantilla aprobada; sin ella, el texto libre solo llega
            // si el destinatario escribió al número de la empresa en las últimas 24 horas.
            var r = !string.IsNullOrWhiteSpace(plantilla) && whatsapp.RequierePlantillasAprobadas
                ? await whatsapp.EnviarPlantillaAsync(tel, plantilla, opciones.Value.IdiomaPlantilla,
                    [Corto(p.Entidad, 80), Corto(p.Objeto, 200), $"{monto} · {p.Modalidad ?? "-"}", cierre], ct)
                : await whatsapp.EnviarTextoAsync(tel, $"*{titulo}*\n{p.Entidad}\n{Corto(p.Objeto, 300)}\n{monto} · {p.Modalidad ?? "-"}\nCierre: {cierre}\nCUCE {p.Cuce}"
                    + (p.Enlace is null ? "" : $"\n{p.Enlace}"), ct);
            aviso.Exito = r.Exito;
            aviso.Error = r.Error is { Length: > 1000 } e ? e[..1000] : r.Error;
        }
        db.SicoesAvisos.Add(aviso);
        await db.SaveChangesAsync(ct);
        if (!aviso.Exito) log.LogWarning("SICOES: WhatsApp {Tipo} de la oportunidad {Id} no salió: {Error}", tipo, o.Id, aviso.Error);
        return aviso.Exito;
    }

    private string ArmarResumen(SicoesPerfil perfil, List<SicoesOportunidad> nuevas, List<SicoesOportunidad> cierran, SicoesLectura? ultima, DateTime hoy)
    {
        var sb = new StringBuilder();
        sb.Append($"<p style=\"font-size:15px;\">Resumen del {hoy:dd/MM/yyyy} para <b>{E(perfil.Nombre)}</b>.</p>");
        if (ultima is not null)
            sb.Append($"<p style=\"color:#6b7280;font-size:13px;\">Última lectura: {Fechas.ABolivia(ultima.Inicio):dd/MM HH:mm} · {ultima.Leidos} procesos leídos · "
                + (ultima.Exito ? "sin errores" : "con error: " + E(ultima.Error)) + "</p>");
        sb.Append($"<h3 style=\"color:#0d47a1;font-size:16px;\">Nuevas oportunidades ({nuevas.Count})</h3>");
        if (nuevas.Count == 0) sb.Append("<p style=\"color:#6b7280;\">Ninguna en las últimas 24 horas.</p>");
        foreach (var o in nuevas) sb.Append(Ficha(o));
        if (cierran.Count > 0)
        {
            sb.Append($"<h3 style=\"color:#b91c1c;font-size:16px;\">Cierran en menos de {opciones.Value.DiasCierreUrgente} días ({cierran.Count})</h3>");
            foreach (var o in cierran) sb.Append(Ficha(o));
        }
        sb.Append($"<p style=\"margin-top:18px;\"><a href=\"{campanas.Value.UrlPublica.TrimEnd('/')}/Sicoes\">Ver todas en el sistema</a></p>");
        return Envolver(sb.ToString(), "Alertas SICOES");
    }

    private string Ficha(SicoesOportunidad o)
    {
        var p = o.Proceso;
        var cierre = p.FechaLimite is { } f ? Fechas.ABolivia(f).ToString("dd/MM/yyyy HH:mm") : "sin fecha";
        var enlace = p.Enlace is null ? "" : $" · <a href=\"{E(p.Enlace)}\">ver en SICOES</a>";
        return $"<div style=\"border:1px solid #e3e8f0;border-left:4px solid {(p.EsConsultaDePrecios ? "#d97706" : "#00a8a8")};border-radius:8px;padding:10px 12px;margin:8px 0;font-size:14px;\">"
            + $"<div style=\"font-weight:bold;color:#0d47a1;\">{E(Corto(p.Objeto, 220))}</div>"
            + $"<div>{E(p.Entidad)} · {E(p.Departamento ?? "-")}</div>"
            + $"<div style=\"color:#374151;\">{(p.Monto is { } m ? $"Bs {m:N2}" : "Monto sin dato")} · {E(p.Modalidad ?? "-")} · Cierre: <b>{cierre}</b></div>"
            + $"<div style=\"color:#6b7280;font-size:12px;\">CUCE {E(p.Cuce)} · Puntaje {o.Puntaje} · {E(o.Motivo)}{enlace}</div></div>";
    }

    private static string Envolver(string cuerpo, string titulo) =>
        $"<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:680px;margin:0 auto;color:#1f2937;\">"
        + $"<div style=\"background:#0d47a1;color:#fff;padding:14px 20px;border-radius:10px 10px 0 0;font-size:18px;font-weight:bold;\">{E(titulo)}</div>"
        + $"<div style=\"background:#fff;border:1px solid #e3e8f0;border-top:0;padding:8px 20px 20px;border-radius:0 0 10px 10px;\">{cuerpo}"
        + "<p style=\"font-size:12px;color:#9ca3af;margin-top:20px;\">Aviso interno del Sistema de Prospección (módulo SICOES, en pruebas).</p></div></div>";

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    public static string Corto(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
