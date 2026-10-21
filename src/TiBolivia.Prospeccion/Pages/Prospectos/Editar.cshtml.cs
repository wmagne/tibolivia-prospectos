using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Pages.Prospectos;

public class EditarModel(AppDbContext db) : PageModel
{
    [BindProperty] public Formulario Datos { get; set; } = new();
    public SelectList Usuarios { get; private set; } = null!;

    public class Formulario
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "La empresa es obligatoria"), StringLength(200)] public string Empresa { get; set; } = "";
        [Display(Name = "Contacto")] public string? NombreContacto { get; set; }
        public string? Cargo { get; set; }
        [EmailAddress(ErrorMessage = "Correo inválido")] public string? Email { get; set; }
        [Display(Name = "Teléfono / WhatsApp")] public string? Telefono { get; set; }
        public string? Rubro { get; set; }
        public string? Ciudad { get; set; }
        [Display(Name = "Sitio web")] public string? SitioWeb { get; set; }
        public string? Origen { get; set; }
        public string? Notas { get; set; }
        public EstadoProspecto Estado { get; set; }
        [Display(Name = "Comercial asignado")] public string? AsignadoAId { get; set; }
        [Display(Name = "Aceptó recibir WhatsApp (opt-in)")] public bool ConsentimientoWhatsApp { get; set; }
        [Display(Name = "Baja de correos")] public bool BajaEmail { get; set; }
        [Display(Name = "Baja de WhatsApp")] public bool BajaWhatsApp { get; set; }
        [RegularExpression("^[ABC]?$", ErrorMessage = "Use A, B o C")] public string? Prioridad { get; set; }
        [Display(Name = "Dirección")] public string? Direccion { get; set; }
        [Display(Name = "Qué ofrecerle")] public string? Necesidad { get; set; }
        [Display(Name = "Fuente de los datos")] public string? Fuente { get; set; }
        [Display(Name = "Datos por verificar")] public bool DatosPorVerificar { get; set; }
        [Display(Name = "Valor estimado (Bs)"), Range(0, 100_000_000)] public decimal? ValorEstimado { get; set; }
        [Display(Name = "Próxima acción")] public string? ProximaAccion { get; set; }
        [Display(Name = "Fecha"), DataType(DataType.Date)] public DateTime? FechaProximaAccion { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is not null)
        {
            var p = await db.Prospectos.FindAsync(id);
            if (p is null) return NotFound();
            Datos = new Formulario
            {
                Id = p.Id, Empresa = p.Empresa, NombreContacto = p.NombreContacto, Cargo = p.Cargo, Email = p.Email,
                Telefono = p.Telefono, Rubro = p.Rubro, Ciudad = p.Ciudad, SitioWeb = p.SitioWeb, Origen = p.Origen,
                Notas = p.Notas, Estado = p.Estado, AsignadoAId = p.AsignadoAId, ConsentimientoWhatsApp = p.ConsentimientoWhatsApp,
                BajaEmail = p.BajaEmail, BajaWhatsApp = p.BajaWhatsApp,
                Prioridad = p.Prioridad, Direccion = p.Direccion, Necesidad = p.Necesidad, Fuente = p.Fuente,
                DatosPorVerificar = p.DatosPorVerificar, ValorEstimado = p.ValorEstimado, ProximaAccion = p.ProximaAccion,
                FechaProximaAccion = p.FechaProximaAccion is { } f ? Fechas.ABolivia(f).Date : null,
            };
        }
        else
        {
            Datos.AsignadoAId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var telefono = Telefonos.Normalizar(Datos.Telefono);
        if (!string.IsNullOrWhiteSpace(Datos.Telefono) && telefono is null)
            ModelState.AddModelError("Datos.Telefono", "Teléfono inválido");
        var email = string.IsNullOrWhiteSpace(Datos.Email) ? null : Datos.Email.Trim().ToLowerInvariant();
        if (email is not null && await db.Prospectos.AnyAsync(p => p.Email == email && p.Id != Datos.Id))
            ModelState.AddModelError("Datos.Email", "Ya existe un prospecto con este correo");
        if (telefono is not null && await db.Prospectos.AnyAsync(p => p.Telefono == telefono && p.Id != Datos.Id))
            ModelState.AddModelError("Datos.Telefono", "Ya existe un prospecto con este teléfono");
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            return Page();
        }

        var p = Datos.Id == 0 ? new Prospecto() : await db.Prospectos.FindAsync(Datos.Id);
        if (p is null) return NotFound();
        if (Datos.Id == 0) db.Prospectos.Add(p);

        p.Empresa = Datos.Empresa.Trim();
        p.NombreContacto = Datos.NombreContacto?.Trim();
        p.Cargo = Datos.Cargo?.Trim();
        p.Email = email;
        p.Telefono = telefono;
        p.Rubro = Datos.Rubro?.Trim();
        p.Ciudad = Datos.Ciudad?.Trim();
        p.SitioWeb = Datos.SitioWeb?.Trim();
        p.Origen = Datos.Origen?.Trim();
        p.Notas = Datos.Notas;
        p.Estado = Datos.Estado;
        p.AsignadoAId = string.IsNullOrEmpty(Datos.AsignadoAId) ? null : Datos.AsignadoAId;
        p.ConsentimientoWhatsApp = Datos.ConsentimientoWhatsApp;
        p.BajaEmail = Datos.BajaEmail;
        p.BajaWhatsApp = Datos.BajaWhatsApp;
        p.Prioridad = string.IsNullOrWhiteSpace(Datos.Prioridad) ? null : Datos.Prioridad;
        p.Direccion = Datos.Direccion?.Trim();
        p.Necesidad = Datos.Necesidad?.Trim();
        p.Fuente = Datos.Fuente?.Trim();
        p.DatosPorVerificar = Datos.DatosPorVerificar;
        p.ValorEstimado = Datos.ValorEstimado;
        p.ProximaAccion = string.IsNullOrWhiteSpace(Datos.ProximaAccion) ? null : Datos.ProximaAccion.Trim();
        p.FechaProximaAccion = p.ProximaAccion is null ? null : Fechas.InicioDelDiaUtc(Datos.FechaProximaAccion ?? DateTime.Today);
        p.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();

        TempData["Ok"] = "Prospecto guardado.";
        return RedirectToPage("Detalle", new { id = p.Id });
    }

    private async Task CargarAsync() =>
        Usuarios = new SelectList(await db.Users.Where(u => u.Activo).OrderBy(u => u.NombreCompleto).ToListAsync(), "Id", "NombreCompleto");
}
