using Microsoft.Extensions.Options;

namespace TiBolivia.Prospeccion.Services;

/// <summary>Proceso en segundo plano que ejecuta el motor de campañas periódicamente.</summary>
public class WorkerCampanas(IServiceScopeFactory scopes, IOptions<CampanasOptions> opciones, ILogger<WorkerCampanas> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(10, opciones.Value.IntervaloSegundos)));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var motor = scope.ServiceProvider.GetRequiredService<MotorCampanas>();
                var procesados = await motor.ProcesarPendientesAsync(ct);
                if (procesados > 0) log.LogInformation("Motor de campañas: {Cantidad} envíos procesados", procesados);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogError(ex, "Error en el motor de campañas");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
