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
    public DbSet<AjusteGuardado> Ajustes => Set<AjusteGuardado>();

    public DbSet<SicoesProceso> SicoesProcesos => Set<SicoesProceso>();
    public DbSet<SicoesPerfil> SicoesPerfiles => Set<SicoesPerfil>();
    public DbSet<SicoesOportunidad> SicoesOportunidades => Set<SicoesOportunidad>();
    public DbSet<SicoesLectura> SicoesLecturas => Set<SicoesLectura>();
    public DbSet<SicoesAviso> SicoesAvisos => Set<SicoesAviso>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        ConfigurarSicoes(b);

        b.Entity<AjusteGuardado>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(100);
        });

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
            e.HasIndex(x => new { x.AsignadoAId, x.FechaProximaAccion });
            e.Property(x => x.Prioridad).HasMaxLength(1);
            e.Property(x => x.Direccion).HasMaxLength(300);
            e.Property(x => x.Necesidad).HasMaxLength(500);
            e.Property(x => x.Fuente).HasMaxLength(300);
            e.Property(x => x.ProximaAccion).HasMaxLength(300);
            e.Property(x => x.ValorEstimado).HasPrecision(14, 2);
            e.HasOne(x => x.AsignadoA).WithMany().HasForeignKey(x => x.AsignadoAId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Plantilla>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Canal).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Asunto).HasMaxLength(300);
            e.Property(x => x.WhatsAppPlantillaMeta).HasMaxLength(200);
            e.Property(x => x.WhatsAppIdioma).HasMaxLength(10);
            e.Property(x => x.AdjuntoNombre).HasMaxLength(200);
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

    /// <summary>Tablas del módulo SICOES: sin claves hacia prospectos ni campañas, para poder separarlo sin migrar datos ajenos.</summary>
    private static void ConfigurarSicoes(ModelBuilder b)
    {
        b.Entity<SicoesProceso>(e =>
        {
            e.ToTable("SicoesProcesos");
            e.Property(x => x.Cuce).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.Cuce).IsUnique();
            e.Property(x => x.Entidad).HasMaxLength(300).IsRequired();
            e.Property(x => x.Objeto).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Modalidad).HasMaxLength(120);
            e.Property(x => x.Monto).HasPrecision(16, 2);
            e.Property(x => x.Departamento).HasMaxLength(60);
            e.Property(x => x.Enlace).HasMaxLength(500);
            e.Property(x => x.Estado).HasMaxLength(80);
            e.Property(x => x.Origen).HasMaxLength(60);
            e.HasIndex(x => x.FechaLimite);
            e.Ignore(x => x.EsConsultaDePrecios);
        });

        b.Entity<SicoesPerfil>(e =>
        {
            e.ToTable("SicoesPerfiles");
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.PalabrasClave).HasMaxLength(2000);
            e.Property(x => x.PalabrasExcluidas).HasMaxLength(2000);
            e.Property(x => x.Departamentos).HasMaxLength(300);
            e.Property(x => x.MontoMinimo).HasPrecision(16, 2);
            e.Property(x => x.TarifaMensualBs).HasPrecision(10, 2);
            e.Property(x => x.Plan).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.CorreoAvisos).HasMaxLength(300);
            e.Property(x => x.WhatsAppAvisos).HasMaxLength(100);
        });

        b.Entity<SicoesOportunidad>(e =>
        {
            e.ToTable("SicoesOportunidades");
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Motivo).HasMaxLength(500);
            e.Property(x => x.Notas).HasMaxLength(2000);
            e.HasIndex(x => new { x.ProcesoId, x.PerfilId }).IsUnique();
            e.HasIndex(x => x.Estado);
            e.HasOne(x => x.Proceso).WithMany().HasForeignKey(x => x.ProcesoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Perfil).WithMany().HasForeignKey(x => x.PerfilId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SicoesLectura>(e =>
        {
            e.ToTable("SicoesLecturas");
            e.Property(x => x.Fuente).HasMaxLength(100);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasIndex(x => x.Inicio);
        });

        b.Entity<SicoesAviso>(e =>
        {
            e.ToTable("SicoesAvisos");
            e.Property(x => x.Tipo).HasMaxLength(30);
            e.Property(x => x.Canal).HasMaxLength(20);
            e.Property(x => x.Destino).HasMaxLength(300);
            e.Property(x => x.Clave).HasMaxLength(30);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasIndex(x => new { x.OportunidadId, x.Tipo, x.Canal });
            e.HasOne(x => x.Oportunidad).WithMany().HasForeignKey(x => x.OportunidadId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
