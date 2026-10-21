using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Prospectos;

public class ImportarModel(ImportadorProspectos importador) : PageModel
{
    public static string Encabezados => ImportadorProspectos.Encabezados;

    [BindProperty] public IFormFile? Archivo { get; set; }
    [BindProperty] public string? Origen { get; set; }
    [BindProperty] public bool AsignarmeAMi { get; set; } = true;
    public List<string> Errores { get; private set; } = [];

    public IActionResult OnGetPlantilla() =>
        File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
            Encabezados + "\nEmpresa Ejemplo SRL,Juan Pérez,Gerente General,juan@ejemplo.com,71234567,Comercio,Santa Cruz,https://ejemplo.com,no,A,Av. Ejemplo 123,\"Correo corporativo, CRM\",ejemplo.com,,no\n")).ToArray(),
            "text/csv", "plantilla_prospectos.csv");

    public async Task<IActionResult> OnPostAsync()
    {
        if (Archivo is null || Archivo.Length == 0)
        {
            Errores.Add("Seleccione un archivo CSV.");
            return Page();
        }
        await using var flujo = Archivo.OpenReadStream();
        var r = await importador.ImportarAsync(flujo,
            string.IsNullOrWhiteSpace(Origen) ? "Importación CSV" : Origen.Trim(),
            AsignarmeAMi ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null, HttpContext.RequestAborted);
        Errores = r.Errores;
        TempData["Ok"] = $"Importación terminada: {r.Nuevos} nuevos, {r.Duplicados} duplicados omitidos, {r.Errores.Count} filas con error.";
        return Errores.Count == 0 ? RedirectToPage("Index") : Page();
    }
}
