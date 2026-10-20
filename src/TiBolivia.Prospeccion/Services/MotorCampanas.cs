using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

/// <summary>Envía el siguiente paso de cada inscripción cuya fecha ya llegó.</summary>
public class MotorCampanas(
    AppDbContext db,
    IEnviadorEmail email,
    IEnviadorWhatsApp whatsApp,
    RenderizadorPlantillas renderizador,
    HorarioLaboral horario,
    IOptions<CampanasOptions> campanasOpciones,
    IOptions<WhatsAppOptions> whatsAppOpciones,
    IOptions<SmtpOptions> smtpOpciones,
    TimeProvider tiempo,
    ILogger<MotorCampanas> log)
{
    private readonly CampanasOptions _o = campanasOpciones.Value;

    public async Task<int> ProcesarPendientesAsync(CancellationToken ct)
    {
        var ahora = tiempo.GetUtcNow().UtcDateTime;
        if (!horario.EsHorarioLaboral(ahora)) return 0;

        var pendientes = await db.Inscripciones
            .Include(i => i.Prospecto).ThenInclude(p => p.AsignadoA)
            .Include(i => i.Campana).ThenInclude(c => c.Servicio)
            .Include(i => i.Campana).ThenInclude(c => c.Pasos).ThenInclude(p => p.Plantilla)
            .Where(i => i.Estado == EstadoInscripcion.Activa && i.Campana.Activa && i.ProximoEnvio != null && i.ProximoEnvio <= ahora)
            .OrderBy(i => i.ProximoEnvio)
            .Take(_o.LoteMaximo)
            .AsSplitQuery()
            .ToListAsync(ct);

        foreach (var inscripcion in pendientes)
        {
            await ProcesarAsync(inscripcion, ahora, ct);
            // Se guarda uno por uno para no reenviar si el proceso se cae a mitad del lote.
            await db.SaveChangesAsync(ct);
        }
        return pendientes.Count;
    }

    private async Task ProcesarAsync(InscripcionCampana i, DateTime ahora, CancellationToken ct)
    {
        var p = i.Prospecto;
        if (p.Estado is EstadoProspecto.NoContactar or EstadoProspecto.Ganado or EstadoProspecto.Perdido)
        {
            Finalizar(i, EstadoInscripcion.Detenida);
            return;
        }

        var pasos = i.Campana.Pasos.OrderBy(x => x.Orden).ToList();
        if (i.PasoActual >= pasos.Count)
        {
            Finalizar(i, EstadoInscripcion.Completada);
            return;
        }

        var paso = pasos[i.PasoActual];
        var mensaje = paso.Canal == Canal.Email
            ? await EnviarEmailAsync(i, paso.Plantilla!, ct)
            : await EnviarWhatsAppAsync(i, paso.Plantilla!, ahora, ct);
        db.Mensajes.Add(mensaje);

        if (mensaje.Estado == EstadoMensaje.Fallido && ++i.Intentos < _o.MaxIntentos)
        {
            i.ProximoEnvio = horario.Ajustar(ahora.AddMinutes(30 * i.Intentos));
            return;
        }

        if (mensaje.Estado == EstadoMensaje.Enviado && p.Estado == EstadoProspecto.Nuevo)
            p.Estado = EstadoProspecto.Contactado;

        i.Intentos = 0;
        i.PasoActual++;
        if (i.PasoActual >= pasos.Count)
            Finalizar(i, EstadoInscripcion.Completada);
        else
            i.ProximoEnvio = horario.Ajustar(ahora.AddDays(pasos[i.PasoActual].DiasEspera));
    }

    private static void Finalizar(InscripcionCampana i, EstadoInscripcion estado)
    {
        i.Estado = estado;
        i.ProximoEnvio = null;
    }

    private async Task<Mensaje> EnviarEmailAsync(InscripcionCampana i, Plantilla plantilla, CancellationToken ct)
    {
        var p = i.Prospecto;
        var servicio = i.Campana.Servicio;
        var asunto = renderizador.Renderizar(plantilla.Asunto, p, servicio, html: false);
        var cuerpo = renderizador.Renderizar(plantilla.Cuerpo, p, servicio, html: true);
        var mensaje = NuevoMensaje(i, Canal.Email, asunto, cuerpo);

        if (!p.PuedeRecibirEmail)
            return Omitir(mensaje, p.BajaEmail ? "El prospecto se dio de baja de correos" : "Sin correo electrónico");

        var r = await email.EnviarAsync(
            new CorreoSaliente(p.Email!, p.NombreContacto, asunto, cuerpo, renderizador.LinkBaja(p),
                smtpOpciones.Value.ResponderAlComercial ? p.AsignadoA?.Email : null), ct);
        return Aplicar(mensaje, r);
    }

    private async Task<Mensaje> EnviarWhatsAppAsync(InscripcionCampana i, Plantilla plantilla, DateTime ahora, CancellationToken ct)
    {
        var p = i.Prospecto;
        var servicio = i.Campana.Servicio;
        var texto = renderizador.Renderizar(plantilla.Cuerpo, p, servicio, html: false);
        var mensaje = NuevoMensaje(i, Canal.WhatsApp, null, texto);

        if (!p.PuedeRecibirWhatsApp)
            return Omitir(mensaje, p.BajaWhatsApp ? "El prospecto se dio de baja de WhatsApp" : "Sin número de WhatsApp");
        if (whatsAppOpciones.Value.RequerirConsentimiento && !p.ConsentimientoWhatsApp)
            return Omitir(mensaje, "Sin consentimiento (opt-in) para WhatsApp");

        ResultadoEnvio r;
        if (!whatsApp.RequierePlantillasAprobadas)
        {
            r = await whatsApp.EnviarTextoAsync(p.Telefono!, texto, ct);
        }
        else if (!string.IsNullOrWhiteSpace(plantilla.WhatsAppPlantillaMeta))
        {
            var parametros = (plantilla.WhatsAppParametros ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => renderizador.Renderizar(v, p, servicio, html: false))
                .ToList();
            r = await whatsApp.EnviarPlantillaAsync(p.Telefono!, plantilla.WhatsAppPlantillaMeta, plantilla.WhatsAppIdioma, parametros, ct);
        }
        else if (p.UltimaRespuestaEn > ahora.AddHours(-24))
        {
            r = await whatsApp.EnviarTextoAsync(p.Telefono!, texto, ct);
        }
        else
        {
            return Omitir(mensaje, "Fuera de la ventana de 24 h: el paso necesita una plantilla aprobada por Meta");
        }
        return Aplicar(mensaje, r);
    }

    private static Mensaje NuevoMensaje(InscripcionCampana i, Canal canal, string? asunto, string cuerpo) => new()
    {
        ProspectoId = i.ProspectoId,
        InscripcionId = i.Id,
        Canal = canal,
        Direccion = DireccionMensaje.Saliente,
        Asunto = asunto,
        Cuerpo = cuerpo,
        Estado = EstadoMensaje.Pendiente,
    };

    private Mensaje Omitir(Mensaje m, string motivo)
    {
        log.LogInformation("Paso omitido para prospecto {Id}: {Motivo}", m.ProspectoId, motivo);
        m.Estado = EstadoMensaje.Omitido;
        m.Error = motivo;
        return m;
    }

    private static Mensaje Aplicar(Mensaje m, ResultadoEnvio r)
    {
        m.Estado = r.Exito ? EstadoMensaje.Enviado : EstadoMensaje.Fallido;
        m.ProveedorId = r.ProveedorId;
        m.Error = r.Error;
        return m;
    }
}
