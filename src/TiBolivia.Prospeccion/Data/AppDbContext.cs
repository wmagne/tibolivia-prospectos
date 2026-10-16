using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<Usuario>(options)
{
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Prospecto> Prospectos => Set<Prospecto>();
    public DbSet<Plantilla> Plantillas => Set<Plantilla>();
    public DbSet<Campana> Campanas => Set<Campana>();
    public DbSet<PasoCampana> PasosCampana => Set<PasoCampana>();
    public DbSet<InscripcionCampana> Inscripciones => Set<InscripcionCampana>();
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();
    public DbSet<Interaccion> Interacciones => Set<Interaccion>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Servicio>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(2000);
        });

        b.Entity<Prospecto>(e =>
        {
            e.Property(x => x.Empresa).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Telefono).HasMaxLength(20);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Telefono);
            e.HasIndex(x => x.Estado);
            e.HasIndex(x => x.TokenBaja).IsUnique();
            e.HasOne(x => x.AsignadoA).WithMany().HasForeignKey(x => x.AsignadoAId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Plantilla>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Canal).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Asunto).HasMaxLength(300);
            e.Property(x => x.WhatsAppPlantillaMeta).HasMaxLength(200);
            e.Property(x => x.WhatsAppIdioma).HasMaxLength(10);
        });

        b.Entity<Campana>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.HasOne(x => x.Servicio).WithMany().HasForeignKey(x => x.ServicioId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PasoCampana>(e =>
        {
            e.Property(x => x.Canal).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Campana).WithMany(c => c.Pasos).HasForeignKey(x => x.CampanaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Plantilla).WithMany().HasForeignKey(x => x.PlantillaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<InscripcionCampana>(e =>
        {
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.CampanaId, x.ProspectoId }).IsUnique();
            e.HasIndex(x => new { x.Estado, x.ProximoEnvio });
            e.HasOne(x => x.Campana).WithMany(c => c.Inscripciones).HasForeignKey(x => x.CampanaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Prospecto).WithMany(p => p.Inscripciones).HasForeignKey(x => x.ProspectoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Mensaje>(e =>
        {
            e.Property(x => x.Canal).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Direccion).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ProveedorId).HasMaxLength(200);
            e.HasIndex(x => x.ProveedorId);
            e.HasIndex(x => x.CreadoEn);
            e.HasOne(x => x.Prospecto).WithMany(p => p.Mensajes).HasForeignKey(x => x.ProspectoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Inscripcion).WithMany().HasForeignKey(x => x.InscripcionId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Interaccion>(e =>
        {
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Prospecto).WithMany(p => p.Interacciones).HasForeignKey(x => x.ProspectoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
