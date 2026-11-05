using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

/// <summary>
/// Inscribe de a poco, en horario laboral, prospectos en campañas según reglas (con correo, con celular o con teléfono fijo).
/// El cupo diario se reparte por hora de la jornada para no enviar todo de golpe (cuida la reputación del servidor de correo).
/// Se detiene sola si la campaña se desactiva.
/// </summary>
public class InscripcionAutomatica(AppDbContext db, ServicioInscripciones inscripciones, HorarioLaboral horario,
    IOptions<CampanasOptions> opciones, TimeProvider tiempo, ILogger<InscripcionAutomatica> log)
{
    public async Task<int> EjecutarAsync(CancellationToken ct)
    {
        var total = 0;
        // Primero las reglas específicas (por rubro u origen), para que sus prospectos no caigan en las generales.
        var reglas = opciones.Value.OtrasAutoInscripciones.Prepend(opciones.Value.AutoInscripcion)
            .OrderBy(r => r.EsEspecifica ? 0 : 1).ToList();
        foreach (var regla in reglas)
            total += await EjecutarReglaAsync(regla, reglas.Where(r => r.Habilitada && r.EsEspecifica && r != regla).ToList(), ct);
        return total;
    }

    private async Task<int> EjecutarReglaAsync(AutoInscripcionOptions o, List<AutoInscripcionOptions> especificas, CancellationToken ct)
    {
        var ahora = tiempo.GetUtcNow().UtcDateTime;
        if (!o.Habilitada || o.MaximoPorDia <= 0 || !horario.EsHorarioLaboral(ahora)) return 0;

        var campana = await db.Campanas.FirstOrDefaultAsync(c => c.Nombre == o.Campana && c.Activa, ct);
        if (campana is null) return 0;

        // Cupo por hora (p. ej. 200 por día en 10 horas = 20 por hora), sin pasar el máximo diario.
        var local = horario.ALocal(ahora);
        var horas = Math.Max(1, opciones.Value.HoraFin - opciones.Value.HoraInicio);
        var porHora = (int)Math.Ceiling(o.MaximoPorDia / (double)horas);
        var inicioHora = ahora.AddMinutes(-local.Minute).AddSeconds(-local.Second);
        var desdeDia = Fechas.InicioDelDiaUtc(local.Date);
        var estaHora = await db.Inscripciones.CountAsync(i => i.CampanaId == campana.Id && i.CreadoEn >= inicioHora, ct);
        var hoy = await db.Inscripciones.CountAsync(i => i.CampanaId == campana.Id && i.CreadoEn >= desdeDia, ct);
        var cupo = Math.Min(porHora - estaHora, o.MaximoPorDia - hoy);
        if (cupo <= 0) return 0;

        var q = Candidatos(o, campana.Id);
        if (o.ConSitioWeb) q = q.Where(p => p.SitioWeb != null && p.SitioWeb != "");
        if (!string.IsNullOrWhiteSpace(o.Rubro)) q = q.Where(p => p.Rubro != null && p.Rubro.StartsWith(o.Rubro));
        if (!string.IsNullOrWhiteSpace(o.Origen)) q = q.Where(p => p.Origen != null && p.Origen.StartsWith(o.Origen));
        if (!string.IsNullOrWhiteSpace(o.Contiene)) q = q.Where(ContieneAlguna(o.Contiene));
        if (!o.EsEspecifica)
            foreach (var e in especificas)
            {
                // Lo que reclama una campaña específica no se manda a las generales.
                if (!string.IsNullOrWhiteSpace(e.Rubro)) { var r = e.Rubro; q = q.Where(p => p.Rubro == null || !p.Rubro.StartsWith(r)); }
                if (!string.IsNullOrWhiteSpace(e.Origen)) { var or = e.Origen; q = q.Where(p => p.Origen == null || !p.Origen.StartsWith(or)); }
                if (!string.IsNullOrWhiteSpace(e.Contiene)) q = q.Where(Negar(ContieneAlguna(e.Contiene)));
            }
        var ids = await q
            .OrderBy(p => p.Prioridad ?? "Z").ThenBy(p => p.DatosPorVerificar).ThenBy(p => p.Id)
            .Select(p => p.Id).Take(cupo).ToListAsync(ct);
        if (ids.Count == 0) return 0;

        var n = await inscripciones.InscribirAsync(campana.Id, ids, null, ct);
        log.LogInformation("Inscripción automática: {Cantidad} prospectos inscritos en \"{Campana}\"", n, campana.Nombre);
        return n;
    }

    /// <summary>Prospectos que todavía no se trabajaron por este canal y no están en otra campaña en curso.</summary>
    private IQueryable<Prospecto> Candidatos(AutoInscripcionOptions o, int campanaId)
    {
        var destino = o.Destino;
        if (destino.Equals("Recontactar", StringComparison.OrdinalIgnoreCase))
        {
            // Ya pasaron por una campaña completa y no contestaron: se les ofrece lo nuevo una sola vez.
            var limite = tiempo.GetUtcNow().UtcDateTime.AddDays(-Math.Max(7, o.DiasDesdeUltimaCampana));
            return db.Prospectos.Where(p => p.Email != null && p.Email != "" && !p.BajaEmail
                && p.Estado == EstadoProspecto.Contactado && p.UltimaRespuestaEn == null
                && p.Inscripciones.Any() && p.Inscripciones.All(i => i.Estado != EstadoInscripcion.Activa && i.CreadoEn < limite)
                && !p.Inscripciones.Any(i => i.CampanaId == campanaId));
        }
        if (destino.Equals("WhatsAppConPermiso", StringComparison.OrdinalIgnoreCase))
        {
            // Solo quienes dieron su permiso (escribieron al número o lo aceptaron): regla de Meta para mensajes iniciados por la empresa.
            var hace3Dias = tiempo.GetUtcNow().UtcDateTime.AddDays(-3);
            return db.Prospectos.Where(p => p.ConsentimientoWhatsApp && !p.BajaWhatsApp && p.Telefono != null && p.Empresa != "(sin empresa)"
                && (p.Estado == EstadoProspecto.Nuevo || p.Estado == EstadoProspecto.Contactado || p.Estado == EstadoProspecto.Interesado)
                && (p.UltimaRespuestaEn == null || p.UltimaRespuestaEn < hace3Dias)
                && !p.Inscripciones.Any(i => i.Estado == EstadoInscripcion.Activa || i.CampanaId == campanaId));
        }
        if (destino.Equals("Cualquiera", StringComparison.OrdinalIgnoreCase))
            return db.Prospectos.Where(p => ((p.Email != null && p.Email != "" && !p.BajaEmail) || p.Telefono != null)
                && p.Estado == EstadoProspecto.Nuevo
                && !p.Inscripciones.Any(i => i.Estado == EstadoInscripcion.Activa || i.CampanaId == campanaId));
        if (destino.Equals("Correo", StringComparison.OrdinalIgnoreCase))
            return db.Prospectos.Where(p => p.Email != null && p.Email != "" && !p.BajaEmail && p.Estado == EstadoProspecto.Nuevo && !p.Inscripciones.Any());

        // Sin correo (o con correo que rebotó): se trabajan por teléfono.
        var q = db.Prospectos.Where(p => (p.Email == null || p.Email == "" || p.BajaEmail) && p.Telefono != null
            && (p.Estado == EstadoProspecto.Nuevo || p.Estado == EstadoProspecto.Contactado)
            && !p.Inscripciones.Any(i => i.Estado == EstadoInscripcion.Activa || i.CampanaId == campanaId));
        Expression<Func<Prospecto, bool>> celular = p => p.Telefono!.Length == 11 && (p.Telefono.StartsWith("5916") || p.Telefono.StartsWith("5917"));
        return destino.Equals("Celular", StringComparison.OrdinalIgnoreCase)
            ? q.Where(celular).Where(p => !p.BajaWhatsApp)
            : q.Where(Negar(celular));
    }

    /// <summary>Nombre, necesidad o notas del prospecto contienen alguna de las palabras (separadas por "|"), sin distinguir mayúsculas.</summary>
    private static Expression<Func<Prospecto, bool>> ContieneAlguna(string palabras)
    {
        Expression<Func<Prospecto, string>> texto = p => (p.Empresa + " " + (p.Necesidad ?? "") + " " + (p.Notas ?? "")).ToLower();
        Expression? cuerpo = null;
        foreach (var palabra in palabras.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var contiene = Expression.Call(texto.Body, nameof(string.Contains), null, Expression.Constant(palabra.ToLowerInvariant()));
            cuerpo = cuerpo is null ? contiene : Expression.OrElse(cuerpo, contiene);
        }
        return Expression.Lambda<Func<Prospecto, bool>>(cuerpo ?? Expression.Constant(false), texto.Parameters);
    }

    private static Expression<Func<T, bool>> Negar<T>(Expression<Func<T, bool>> e) =>
        Expression.Lambda<Func<T, bool>>(Expression.Not(e.Body), e.Parameters);
}
