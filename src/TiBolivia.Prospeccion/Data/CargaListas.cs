using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Data;

/// <summary>
/// Carga automáticamente las listas de prospectos de la carpeta Data/Listas (archivos .csv) la primera vez.
/// Cada archivo se identifica por su "Origen" ("Lista: nombre-del-archivo"); si ya hay prospectos con ese
/// origen no se vuelve a cargar, así que los cambios hechos desde la interfaz no se pisan.
/// </summary>
public static class CargaListas
{
    public static async Task AplicarAsync(IServiceProvider sp, string? asignarA, ILogger log)
    {
        var carpeta = Path.Combine(AppContext.BaseDirectory, "Data", "Listas");
        if (!Directory.Exists(carpeta)) return;

        var db = sp.GetRequiredService<AppDbContext>();
        var importador = sp.GetRequiredService<ImportadorProspectos>();
        foreach (var archivo in Directory.GetFiles(carpeta, "*.csv").Order())
        {
            var origen = "Lista: " + Path.GetFileNameWithoutExtension(archivo);
            if (await db.Prospectos.AnyAsync(p => p.Origen == origen)) continue;
            await using var flujo = File.OpenRead(archivo);
            var r = await importador.ImportarAsync(flujo, origen, asignarA);
            log.LogInformation("Lista {Origen}: {Nuevos} prospectos nuevos, {Duplicados} duplicados, {Errores} con error",
                origen, r.Nuevos, r.Duplicados, r.Errores.Count);
        }
    }
}
