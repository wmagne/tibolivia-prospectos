using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services.Sicoes;

public class SicoesOptions
{
    public bool Habilitado { get; set; }
    /// <summary>Horas de Bolivia en que se lee la fuente. El plan pide dos lecturas al día para no cargar el portal.</summary>
    public List<int> HorasLectura { get; set; } = [7, 14];
    /// <summary>
    /// Dirección de una lista de convocatorias en CSV de una fuente autorizada. Vacía = solo se cargan listas a mano
    /// (menú SICOES → Cargar lista). No se debe apuntar al buscador del portal: está protegido con captcha.
    /// </summary>
    public string? UrlFuente { get; set; }
    /// <summary>Modo pruebas: los avisos solo pueden ir a estos destinos, aunque un perfil diga otra cosa.</summary>
    public bool ModoPruebas { get; set; } = true;
    public List<string> CorreosPermitidos { get; set; } = [];
    public List<string> WhatsAppPermitidos { get; set; } = [];
    /// <summary>Plantilla aprobada en Meta para iniciar el aviso por WhatsApp. Sin plantilla, se intenta texto libre (Meta solo lo entrega dentro de las 24 h de un mensaje del destinatario).</summary>
    public string? PlantillaWhatsApp { get; set; }
    public string IdiomaPlantilla { get; set; } = "es";
    /// <summary>Días antes del cierre en que un proceso se avisa por WhatsApp al momento.</summary>
    public int DiasCierreUrgente { get; set; } = 3;
    public int HorasRecordatorio { get; set; } = 48;
}

/// <summary>Guarda los procesos leídos (sin duplicar el CUCE), los compara con los perfiles y crea las oportunidades.</summary>
public class ServicioSicoes(AppDbContext db, ILogger<ServicioSicoes> log)
{
    public async Task<SicoesLectura> ProcesarAsync(IReadOnlyList<SicoesProceso> leidos, string fuente, string? error, CancellationToken ct)
    {
        var lectura = new SicoesLectura { Fuente = fuente, Leidos = leidos.Count, Error = error, Exito = error is null };
        db.SicoesLecturas.Add(lectura);
        try
        {
            var cuces = leidos.Select(p => p.Cuce).ToList();
            var existentes = await db.SicoesProcesos.Where(p => cuces.Contains(p.Cuce)).ToDictionaryAsync(p => p.Cuce, StringComparer.OrdinalIgnoreCase, ct);
            var tocados = new List<SicoesProceso>();
            foreach (var p in leidos)
            {
                if (existentes.TryGetValue(p.Cuce, out var e))
                {
                    // Un proceso ya guardado puede cambiar de estado o de fecha límite (ampliaciones): se actualiza, no se duplica.
                    e.Estado = p.Estado ?? e.Estado;
                    e.FechaLimite = p.FechaLimite ?? e.FechaLimite;
                    e.Monto = p.Monto ?? e.Monto;
                    e.Enlace = p.Enlace ?? e.Enlace;
                    e.Modalidad = p.Modalidad ?? e.Modalidad;
                    e.Departamento = p.Departamento ?? e.Departamento;
                    e.ActualizadoEn = DateTime.UtcNow;
                    tocados.Add(e);
                }
                else
                {
                    db.SicoesProcesos.Add(p);
                    existentes[p.Cuce] = p;
                    tocados.Add(p);
                    lectura.Nuevos++;
                }
            }
            await db.SaveChangesAsync(ct);

            var perfiles = await db.SicoesPerfiles.Where(p => p.Activo).ToListAsync(ct);
            var ids = tocados.Select(p => p.Id).ToList();
            var ya = (await db.SicoesOportunidades.Where(o => ids.Contains(o.ProcesoId)).Select(o => new { o.ProcesoId, o.PerfilId }).ToListAsync(ct))
                .Select(o => (o.ProcesoId, o.PerfilId)).ToHashSet();
            foreach (var proceso in tocados)
            foreach (var perfil in perfiles)
            {
                var ev = FiltroSicoes.Evaluar(proceso, perfil);
                if (!ev.EsRelevante || ya.Contains((proceso.Id, perfil.Id))) continue;
                db.SicoesOportunidades.Add(new SicoesOportunidad { ProcesoId = proceso.Id, PerfilId = perfil.Id, Puntaje = ev.Puntaje, Motivo = ev.Motivo });
                lectura.Relevantes++;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogError(ex, "Error guardando la lectura del SICOES");
            db.ChangeTracker.Clear();
            db.SicoesLecturas.Add(lectura);
            lectura.Exito = false;
            lectura.Error = Recortar(ex.Message);
        }
        lectura.Fin = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        log.LogInformation("SICOES ({Fuente}): {Leidos} leídos, {Nuevos} nuevos, {Relevantes} oportunidades nuevas", fuente, lectura.Leidos, lectura.Nuevos, lectura.Relevantes);
        return lectura;
    }

    private static string Recortar(string s) => s.Length <= 1000 ? s : s[..1000];
}

/// <summary>Lee la lista de una dirección autorizada (CSV). Sin dirección configurada no hace ninguna consulta.</summary>
public class FuenteSicoesUrl(HttpClient http, IOptions<SicoesOptions> opciones)
{
    public bool Configurada => !string.IsNullOrWhiteSpace(opciones.Value.UrlFuente);

    public async Task<(List<SicoesProceso> Procesos, string? Error)> LeerAsync(CancellationToken ct)
    {
        if (!Configurada) return ([], "Sin fuente automática configurada (Sicoes:UrlFuente). Cargue la lista desde SICOES → Cargar lista.");
        try
        {
            http.Timeout = TimeSpan.FromSeconds(60);
            using var req = new HttpRequestMessage(HttpMethod.Get, opciones.Value.UrlFuente);
            req.Headers.UserAgent.ParseAdd("TIBolivia-Prospeccion/1.0 (lectura dos veces al dia)");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return ([], $"La fuente respondió {(int)resp.StatusCode}");
            var texto = await resp.Content.ReadAsStringAsync(ct);
            var r = LectorSicoes.LeerTexto(texto, "url");
            return (r.Procesos, r.Procesos.Count == 0 ? "La fuente no trajo procesos. " + string.Join(" ", r.Errores.Take(2)) : null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return ([], ex.Message);
        }
    }
}
