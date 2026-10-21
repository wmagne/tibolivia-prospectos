using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

public record ResultadoImportacion(int Nuevos, int Duplicados, List<string> Errores);

/// <summary>Importa prospectos desde CSV (pantalla "Importar" y carga automática de listas).</summary>
public class ImportadorProspectos(AppDbContext db)
{
    public const string Encabezados =
        "Empresa,Contacto,Cargo,Email,Telefono,Rubro,Ciudad,SitioWeb,ConsentimientoWhatsApp,Prioridad,Direccion,Necesidad,Fuente,Notas,Verificar";

    public async Task<ResultadoImportacion> ImportarAsync(Stream contenido, string origen, string? asignarA, CancellationToken ct = default)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,
            PrepareHeaderForMatch = a => a.Header.Trim().ToLowerInvariant(),
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
        };
        using var lector = new StreamReader(contenido, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(lector, config);

        var emails = (await db.Prospectos.Where(p => p.Email != null).Select(p => p.Email!).ToListAsync(ct)).ToHashSet();
        var telefonos = (await db.Prospectos.Where(p => p.Telefono != null).Select(p => p.Telefono!).ToListAsync(ct)).ToHashSet();
        var empresas = (await db.Prospectos.Select(p => p.Empresa.ToLower()).ToListAsync(ct)).ToHashSet();
        var errores = new List<string>();
        int nuevos = 0, duplicados = 0, fila = 1;

        await csv.ReadAsync();
        csv.ReadHeader();
        while (await csv.ReadAsync())
        {
            fila++;
            var empresa = Campo(csv, "empresa");
            if (empresa is null) { errores.Add($"Fila {fila}: falta la empresa."); continue; }
            var email = Campo(csv, "email")?.ToLowerInvariant();
            if (email is not null && !email.Contains('@')) { errores.Add($"Fila {fila}: correo inválido ({email})."); email = null; }
            var telefono = Telefonos.Normalizar(Campo(csv, "telefono"));
            if (email is null && telefono is null) { errores.Add($"Fila {fila} ({empresa}): sin correo ni teléfono válido."); continue; }
            if ((email is not null && emails.Contains(email)) || (telefono is not null && telefonos.Contains(telefono))
                || (email is null && telefono is null && empresas.Contains(empresa.ToLowerInvariant())))
            {
                duplicados++;
                continue;
            }

            var prioridad = Campo(csv, "prioridad")?.ToUpperInvariant();
            db.Prospectos.Add(new Prospecto
            {
                Empresa = Recortar(empresa, 200)!,
                NombreContacto = Campo(csv, "contacto"),
                Cargo = Campo(csv, "cargo"),
                Email = email,
                Telefono = telefono,
                Rubro = Campo(csv, "rubro"),
                Ciudad = Campo(csv, "ciudad"),
                SitioWeb = Campo(csv, "sitioweb"),
                ConsentimientoWhatsApp = EsSi(Campo(csv, "consentimientowhatsapp")),
                Prioridad = prioridad is "A" or "B" or "C" ? prioridad : null,
                Direccion = Recortar(Campo(csv, "direccion"), 300),
                Necesidad = Recortar(Campo(csv, "necesidad"), 500),
                Fuente = Recortar(Campo(csv, "fuente"), 300),
                Notas = Campo(csv, "notas"),
                DatosPorVerificar = EsSi(Campo(csv, "verificar")),
                Origen = origen,
                AsignadoAId = asignarA,
            });
            if (email is not null) emails.Add(email);
            if (telefono is not null) telefonos.Add(telefono);
            empresas.Add(empresa.ToLowerInvariant());
            nuevos++;
        }
        await db.SaveChangesAsync(ct);
        return new ResultadoImportacion(nuevos, duplicados, errores);
    }

    private static bool EsSi(string? v) => v?.ToLowerInvariant() is "si" or "sí" or "true" or "1" or "x";
    private static string? Recortar(string? v, int max) => v is null || v.Length <= max ? v : v[..max];
    private static string? Campo(CsvReader csv, string nombre) =>
        csv.TryGetField<string>(nombre, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
}
