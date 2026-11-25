using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;
using TiBolivia.Prospeccion.Services.Sicoes;

namespace TiBolivia.Prospeccion.Tests;

public class ServicioSicoesTests
{
    private static AppDbContext NuevaBase()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        PerfilesSicoes.CrearPerfilTiBoliviaAsync(db).GetAwaiter().GetResult();
        return db;
    }

    private static List<SicoesProceso> Lista(string? estado = "Vigente", DateTime? limite = null) =>
    [
        new() { Cuce = "26-0513-00-1000001-1-1", Entidad = "GAM Santa Cruz", Objeto = "Adquisición de computadoras", Departamento = "Santa Cruz", Estado = estado, FechaLimite = limite, Origen = "prueba" },
        new() { Cuce = "26-0513-00-1000002-1-1", Entidad = "GAM Santa Cruz", Objeto = "Compra de cemento", Departamento = "Santa Cruz", Origen = "prueba" },
    ];

    [Fact]
    public async Task Guarda_procesos_y_crea_oportunidad_solo_para_los_relevantes()
    {
        await using var db = NuevaBase();
        var l = await new ServicioSicoes(db, NullLogger<ServicioSicoes>.Instance).ProcesarAsync(Lista(), "prueba", null, default);
        Assert.True(l.Exito);
        Assert.Equal((2, 2, 1), (l.Leidos, l.Nuevos, l.Relevantes));
        Assert.Equal(2, await db.SicoesProcesos.CountAsync());
        var o = await db.SicoesOportunidades.Include(x => x.Proceso).SingleAsync();
        Assert.Equal("Adquisición de computadoras", o.Proceso.Objeto);
        Assert.Equal(EstadoOportunidadSicoes.Nueva, o.Estado);
    }

    [Fact]
    public async Task Leer_dos_veces_lo_mismo_no_duplica_y_actualiza_estado_y_fecha()
    {
        await using var db = NuevaBase();
        var servicio = new ServicioSicoes(db, NullLogger<ServicioSicoes>.Instance);
        await servicio.ProcesarAsync(Lista(), "prueba", null, default);
        var nuevaFecha = new DateTime(2026, 12, 10, 14, 0, 0, DateTimeKind.Utc);
        var l = await servicio.ProcesarAsync(Lista("Ampliado", nuevaFecha), "prueba", null, default);

        Assert.Equal(0, l.Nuevos);
        Assert.Equal(0, l.Relevantes);
        Assert.Equal(2, await db.SicoesProcesos.CountAsync());
        Assert.Equal(1, await db.SicoesOportunidades.CountAsync());
        var p = await db.SicoesProcesos.SingleAsync(x => x.Cuce == "26-0513-00-1000001-1-1");
        Assert.Equal("Ampliado", p.Estado);
        Assert.Equal(nuevaFecha, p.FechaLimite);
    }

    [Fact]
    public async Task Lectura_fallida_queda_registrada_para_el_diagnostico()
    {
        await using var db = NuevaBase();
        await new ServicioSicoes(db, NullLogger<ServicioSicoes>.Instance).ProcesarAsync([], "url", "La fuente respondió 403", default);
        var l = await db.SicoesLecturas.SingleAsync();
        Assert.False(l.Exito);
        Assert.Equal("La fuente respondió 403", l.Error);
        Assert.NotNull(l.Fin);
    }

    // --- Avisos: en pruebas nada sale a terceros ---

    private sealed class CorreoFalso : IEnviadorEmail
    {
        public List<CorreoSaliente> Enviados { get; } = [];
        public Task<ResultadoEnvio> EnviarAsync(CorreoSaliente correo, CancellationToken ct) { Enviados.Add(correo); return Task.FromResult(ResultadoEnvio.Ok("x")); }
    }

    private sealed class WhatsAppFalso : IEnviadorWhatsApp
    {
        public List<string> Enviados { get; } = [];
        public bool RequierePlantillasAprobadas => true;
        public Task<ResultadoEnvio> EnviarPlantillaAsync(string telefono, string plantilla, string idioma, IReadOnlyList<string> parametros, CancellationToken ct)
        { Enviados.Add(telefono); return Task.FromResult(ResultadoEnvio.Ok("x")); }
        public Task<ResultadoEnvio> EnviarTextoAsync(string telefono, string texto, CancellationToken ct)
        { Enviados.Add(telefono); return Task.FromResult(ResultadoEnvio.Ok("x")); }
    }

    private static (AvisosSicoes Avisos, CorreoFalso Correo, WhatsAppFalso WhatsApp) Avisos(AppDbContext db)
    {
        var correo = new CorreoFalso();
        var wa = new WhatsAppFalso();
        var opciones = Options.Create(new SicoesOptions
        {
            ModoPruebas = true, CorreosPermitidos = ["gerencia@tibolivia.com"], WhatsAppPermitidos = ["70935349"],
        });
        return (new AvisosSicoes(db, correo, wa, opciones, Options.Create(new CampanasOptions()), NullLogger<AvisosSicoes>.Instance), correo, wa);
    }

    [Fact]
    public async Task Urgentes_y_recordatorio_van_al_celular_permitido_una_sola_vez()
    {
        await using var db = NuevaBase();
        var cierre = DateTime.UtcNow.AddHours(30);
        await new ServicioSicoes(db, NullLogger<ServicioSicoes>.Instance).ProcesarAsync(Lista(limite: cierre), "prueba", null, default);
        var (avisos, correo, wa) = Avisos(db);

        Assert.Equal(1, await avisos.EnviarUrgentesAsync(default));
        Assert.Equal(1, await avisos.EnviarRecordatoriosAsync(default));
        // Segunda pasada del proceso (cada 5 minutos): no repite nada.
        Assert.Equal(0, await avisos.EnviarUrgentesAsync(default));
        Assert.Equal(0, await avisos.EnviarRecordatoriosAsync(default));

        Assert.All(wa.Enviados, t => Assert.Equal("59170935349", t));
        Assert.Equal(2, wa.Enviados.Count);
        Assert.Single(correo.Enviados);
        Assert.Equal("gerencia@tibolivia.com", correo.Enviados[0].Para);
    }

    [Fact]
    public async Task En_pruebas_un_destino_no_permitido_no_recibe_nada()
    {
        await using var db = NuevaBase();
        var perfil = await db.SicoesPerfiles.SingleAsync();
        perfil.CorreoAvisos = "cliente@empresa.com";
        perfil.WhatsAppAvisos = "77712345";
        await db.SaveChangesAsync();
        await new ServicioSicoes(db, NullLogger<ServicioSicoes>.Instance).ProcesarAsync(Lista(limite: DateTime.UtcNow.AddHours(30)), "prueba", null, default);
        var (avisos, correo, wa) = Avisos(db);

        await avisos.EnviarResumenDiarioAsync(new DateTime(2026, 11, 26), default);
        await avisos.EnviarUrgentesAsync(default);
        await avisos.EnviarRecordatoriosAsync(default);

        Assert.Empty(correo.Enviados);
        Assert.Empty(wa.Enviados);
        Assert.All(await db.SicoesAvisos.ToListAsync(), a => Assert.Equal("Destino no permitido en modo pruebas", a.Error));
    }

    [Fact]
    public async Task Resumen_diario_sale_una_vez_por_dia()
    {
        await using var db = NuevaBase();
        await new ServicioSicoes(db, NullLogger<ServicioSicoes>.Instance).ProcesarAsync(Lista(), "prueba", null, default);
        var (avisos, correo, _) = Avisos(db);
        var hoy = new DateTime(2026, 11, 26);
        Assert.Equal(1, await avisos.EnviarResumenDiarioAsync(hoy, default));
        Assert.Equal(0, await avisos.EnviarResumenDiarioAsync(hoy, default));
        Assert.Contains("1 oportunidades nuevas", correo.Enviados.Single().Asunto);
    }

    [Fact]
    public async Task Oportunidad_descartada_no_genera_avisos()
    {
        await using var db = NuevaBase();
        await new ServicioSicoes(db, NullLogger<ServicioSicoes>.Instance).ProcesarAsync(Lista(limite: DateTime.UtcNow.AddHours(30)), "prueba", null, default);
        (await db.SicoesOportunidades.SingleAsync()).Estado = EstadoOportunidadSicoes.Descartada;
        await db.SaveChangesAsync();
        var (avisos, _, wa) = Avisos(db);
        Assert.Equal(0, await avisos.EnviarUrgentesAsync(default));
        Assert.Equal(0, await avisos.EnviarRecordatoriosAsync(default));
        Assert.Empty(wa.Enviados);
    }
}
