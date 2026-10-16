using System.Globalization;
using System.Security.Claims;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Prospectos;

public class ImportarModel(AppDbContext db) : PageModel
{
    public const string Encabezados = "Empresa,Contacto,Cargo,Email,Telefono,Rubro,Ciudad,SitioWeb,ConsentimientoWhatsApp";

    [BindProperty] public IFormFile? Archivo { get; set; }
    [BindProperty] public string? Origen { get; set; }
    [BindProperty] public bool AsignarmeAMi { get; set; } = true;
    public List<string> Errores { get; private set; } = [];

    public IActionResult OnGetPlantilla() =>
        File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
            Encabezados + "\nEmpresa Ejemplo SRL,Juan Pérez,Gerente General,juan@ejemplo.com,71234567,Comercio,Santa Cruz,https://ejemplo.com,no\n")).ToArray(),
            "text/csv", "plantilla_prospectos.csv");

    public async Task<IActionResult> OnPostAsync()
    {
        if (Archivo is null || Archivo.Length == 0)
        {
            Errores.Add("Seleccione un archivo CSV.");
            return Page();
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,
            PrepareHeaderForMatch = a => a.Header.Trim().ToLowerInvariant(),
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
        };
        using var lector = new StreamReader(Archivo.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(lector, config);

        var emails = (await db.Prospectos.Where(p => p.Email != null).Select(p => p.Email!).ToListAsync()).ToHashSet();
        var telefonos = (await db.Prospectos.Where(p => p.Telefono != null).Select(p => p.Telefono!).ToListAsync()).ToHashSet();
        var usuarioId = AsignarmeAMi ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        int nuevos = 0, duplicados = 0, fila = 1;

        await csv.ReadAsync();
        csv.ReadHeader();
        while (await csv.ReadAsync())
        {
            fila++;
            var empresa = Campo(csv, "empresa");
            if (empresa is null) { Errores.Add($"Fila {fila}: falta la empresa."); continue; }
            var email = Campo(csv, "email")?.ToLowerInvariant();
            if (email is not null && !email.Contains('@')) { Errores.Add($"Fila {fila}: correo inválido ({email})."); email = null; }
            var telefono = Telefonos.Normalizar(Campo(csv, "telefono"));
            if (email is null && telefono is null) { Errores.Add($"Fila {fila}: sin correo ni teléfono válido."); continue; }
            if ((email is not null && emails.Contains(email)) || (telefono is not null && telefonos.Contains(telefono))) { duplicados++; continue; }

            db.Prospectos.Add(new Prospecto
            {
                Empresa = empresa,
                NombreContacto = Campo(csv, "contacto"),
                Cargo = Campo(csv, "cargo"),
                Email = email,
                Telefono = telefono,
                Rubro = Campo(csv, "rubro"),
                Ciudad = Campo(csv, "ciudad"),
                SitioWeb = Campo(csv, "sitioweb"),
                ConsentimientoWhatsApp = Campo(csv, "consentimientowhatsapp")?.ToLowerInvariant() is "si" or "sí" or "true" or "1" or "x",
                Origen = string.IsNullOrWhiteSpace(Origen) ? "Importación CSV" : Origen.Trim(),
                AsignadoAId = usuarioId,
            });
            if (email is not null) emails.Add(email);
            if (telefono is not null) telefonos.Add(telefono);
            nuevos++;
        }
        await db.SaveChangesAsync();

        TempData["Ok"] = $"Importación terminada: {nuevos} nuevos, {duplicados} duplicados omitidos, {Errores.Count} filas con error.";
        if (Errores.Count == 0) return RedirectToPage("Index");
        return Page();
    }

    private static string? Campo(CsvReader csv, string nombre) =>
        csv.TryGetField<string>(nombre, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
}
