using Microsoft.Extensions.Options;

namespace TiBolivia.Prospeccion.Services.Sicoes;

/// <summary>
/// Lee la fuente del SICOES a las horas configuradas (7:00 y 14:00 de Bolivia), manda el resumen diario después de
/// la primera lectura y revisa avisos urgentes y recordatorios entre las 7:00 y las 21:00 (nadie quiere un WhatsApp de madrugada).
/// </summary>
public class WorkerSicoes(IServiceScopeFactory scopes, IOptions<SicoesOptions> opciones, AjustesGuardados ajustes, ILogger<WorkerSicoes> log) : BackgroundService
{
    private const string ClaveLectura = "Sicoes:UltimaLectura";
    private const string ClaveResumen = "Sicoes:UltimoResumen";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                var o = opciones.Value;
                if (!o.Habilitado) continue;
                var ahora = Fechas.ABolivia(DateTime.UtcNow);
                using var scope = scopes.CreateScope();
                var sp = scope.ServiceProvider;

                var turno = o.HorasLectura.Where(h => ahora.Hour >= h).DefaultIfEmpty(-1).Max();
                var claveTurno = $"{ahora:yyyy-MM-dd}-{turno:00}";
                if (turno >= 0 && ajustes.Obtener(ClaveLectura) != claveTurno)
                {
                    // Se guarda antes de leer: si la fuente falla no se reintenta en bucle cada 5 minutos, se espera al próximo turno.
                    await ajustes.GuardarAsync(new Dictionary<string, string?> { [ClaveLectura] = claveTurno }, new HashSet<string>());
                    var fuente = sp.GetRequiredService<FuenteSicoesUrl>();
                    var (procesos, error) = await fuente.LeerAsync(ct);
                    await sp.GetRequiredService<ServicioSicoes>().ProcesarAsync(procesos, fuente.Configurada ? "url" : "sin fuente", error, ct);
                }

                if (ahora.Hour < 7 || ahora.Hour >= 21) continue;
                var avisos = sp.GetRequiredService<AvisosSicoes>();
                var hoy = ahora.ToString("yyyy-MM-dd");
                if (turno >= 0 && ajustes.Obtener(ClaveResumen) != hoy)
                {
                    await ajustes.GuardarAsync(new Dictionary<string, string?> { [ClaveResumen] = hoy }, new HashSet<string>());
                    await avisos.EnviarResumenDiarioAsync(ahora.Date, ct);
                }
                await avisos.EnviarUrgentesAsync(ct);
                await avisos.EnviarRecordatoriosAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogError(ex, "Error en el módulo SICOES");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}

/// <summary>Servicios y tarifas que se pueden anotar en la ficha de un cliente. Todavía no se cobra nada desde el sistema.</summary>
public static class TarifasSicoes
{
    public record Tarifa(string Servicio, decimal PrecioBs, string Unidad, string Detalle);

    public static readonly IReadOnlyList<Tarifa> Lista =
    [
        new("Alerta WhatsApp", 100, "al mes", "Perfil propio, avisos diarios y recordatorio de cierre."),
        new("Plan Gestor", 400, "al mes", "Alertas, carpeta de documentos y revisión de 2 procesos al mes."),
        new("Propuesta lista", 800, "por propuesta", "Propuesta armada para presentar en un proceso."),
        new("Curso «Venderle al Estado con el SICOES»", 350, "por persona", "8 horas."),
        new("Piloto", 0, "30 días", "30 días gratis de Alerta WhatsApp para los 5 pilotos."),
    ];

    public static decimal TarifaDelPlan(Domain.PlanSicoes plan) => plan switch
    {
        Domain.PlanSicoes.AlertaWhatsApp => 100,
        Domain.PlanSicoes.PlanGestor => 400,
        _ => 0,
    };
}
