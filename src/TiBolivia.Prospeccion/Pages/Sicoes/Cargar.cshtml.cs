using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services.Sicoes;

namespace TiBolivia.Prospeccion.Pages.Sicoes;

/// <summary>
/// Carga a mano de una lista de convocatorias (archivo CSV o tabla copiada de una planilla). Es la fuente permitida
/// mientras no haya un acceso automático autorizado: el buscador del portal pide captcha y no se lo salta.
/// </summary>
public class CargarModel(ServicioSicoes servicio) : PageModel
{
    [BindProperty] public string? Texto { get; set; }
    [BindProperty] public IFormFile? Archivo { get; set; }
    public SicoesLectura? Resultado { get; private set; }
    public List<string> Avisos { get; private set; } = [];

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var texto = Texto;
        if (Archivo is { Length: > 0 })
        {
            if (Archivo.Length > 5_000_000) { Avisos.Add("El archivo pesa más de 5 MB."); return Page(); }
            using var lector = new StreamReader(Archivo.OpenReadStream());
            texto = await lector.ReadToEndAsync(ct);
        }
        if (string.IsNullOrWhiteSpace(texto)) { Avisos.Add("Pegue la lista o elija un archivo."); return Page(); }

        var r = LectorSicoes.LeerTexto(texto, "archivo");
        Avisos.AddRange(r.Errores.Take(20));
        if (r.Procesos.Count == 0) return Page();
        Resultado = await servicio.ProcesarAsync(r.Procesos, "archivo", null, ct);
        return Page();
    }
}
