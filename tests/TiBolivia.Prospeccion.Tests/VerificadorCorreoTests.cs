using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services;

namespace TiBolivia.Prospeccion.Tests;

public class VerificadorCorreoTests
{
    private sealed class DnsFalso : IConsultaDns
    {
        public Dictionary<string, List<string>?> Mx { get; } = new();
        public Dictionary<string, bool?> Direccion { get; } = new();
        public int Consultas;
        public Task<List<string>?> ServidoresCorreoAsync(string dominio, CancellationToken ct)
        { Consultas++; return Task.FromResult(Mx.TryGetValue(dominio, out var v) ? v : new List<string>()); }
        public Task<bool?> TieneDireccionAsync(string dominio, CancellationToken ct) =>
            Task.FromResult(Direccion.TryGetValue(dominio, out var v) ? v : false);
    }

    private sealed class SondaFalsa : ISondaSmtp
    {
        public Dictionary<string, (int, string)?> Respuestas { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Probadas { get; } = [];
        public Task<(int Codigo, string Texto)?> ProbarAsync(string servidor, string email, CancellationToken ct)
        {
            Probadas.Add(email);
            return Task.FromResult(Respuestas.TryGetValue(email, out var r) ? r : ((int, string)?)(250, "250 2.1.5 Ok"));
        }
    }

    private static (VerificadorCorreo V, DnsFalso Dns, SondaFalsa Sonda) Crear(bool probarBuzon = true)
    {
        var dns = new DnsFalso();
        dns.Mx["empresa.com.bo"] = ["mx.empresa.com.bo"];
        dns.Mx["gmail.com"] = ["gmail-smtp-in.l.google.com"];
        var sonda = new SondaFalsa();
        var v = new VerificadorCorreo(dns, sonda, Options.Create(new VerificacionCorreoOptions { ProbarBuzon = probarBuzon }),
            TimeProvider.System, NullLogger<VerificadorCorreo>.Instance);
        return (v, dns, sonda);
    }

    [Theory]
    [InlineData("ventas@empresa..com")]
    [InlineData("ventas@@empresa.com")]
    [InlineData("ventas empresa@empresa.com")]
    [InlineData("ventas@empresa")]
    [InlineData("sin-arroba.empresa.com")]
    public async Task Direccion_mal_escrita_es_invalida(string email)
    {
        var (v, _, sonda) = Crear();
        Assert.Equal(EstadoVerificacion.Invalido, (await v.VerificarAsync(email, default)).Estado);
        Assert.Empty(sonda.Probadas);
    }

    [Fact]
    public async Task Dominio_sin_correo_ni_direccion_es_invalido()
    {
        var (v, _, _) = Crear();
        var r = await v.VerificarAsync("info@dominio-que-no-existe.bo", default);
        Assert.Equal(EstadoVerificacion.Invalido, r.Estado);
        Assert.Contains("no existe o no recibe correo", r.Motivo);
    }

    [Fact]
    public async Task Dominio_sin_MX_pero_con_direccion_se_prueba_en_el_propio_dominio()
    {
        var (v, dns, sonda) = Crear();
        dns.Direccion["viejo.com.bo"] = true;
        Assert.Equal(EstadoVerificacion.Valido, (await v.VerificarAsync("info@viejo.com.bo", default)).Estado);
        Assert.Single(sonda.Probadas);
    }

    [Fact]
    public async Task Casilla_que_el_servidor_dice_que_no_existe_es_invalida()
    {
        var (v, _, sonda) = Crear();
        sonda.Respuestas["noexiste@empresa.com.bo"] = (550, "550 5.1.1 <noexiste@empresa.com.bo>: Recipient address rejected: User unknown");
        Assert.Equal(EstadoVerificacion.Invalido, (await v.VerificarAsync("noexiste@empresa.com.bo", default)).Estado);
    }

    [Theory]
    [InlineData(550, "550 5.7.1 Service unavailable; client host blocked using Spamhaus")]
    [InlineData(550, "550 Rejected by policy")]
    [InlineData(450, "450 4.2.0 Greylisted, try again later")]
    [InlineData(421, "421 Too many connections")]
    public async Task Bloqueos_listas_negras_y_errores_temporales_no_descartan_el_correo(int codigo, string texto)
    {
        var (v, _, sonda) = Crear();
        sonda.Respuestas["ventas@empresa.com.bo"] = (codigo, texto);
        Assert.Equal(EstadoVerificacion.Desconocido, (await v.VerificarAsync("ventas@empresa.com.bo", default)).Estado);
    }

    [Fact]
    public async Task Servidor_o_DNS_que_no_responde_deja_enviar()
    {
        var (v, dns, sonda) = Crear();
        sonda.Respuestas["ventas@empresa.com.bo"] = null;
        Assert.Equal(EstadoVerificacion.Desconocido, (await v.VerificarAsync("ventas@empresa.com.bo", default)).Estado);
        dns.Mx["caido.bo"] = null;
        Assert.Equal(EstadoVerificacion.Desconocido, (await v.VerificarAsync("info@caido.bo", default)).Estado);
    }

    [Fact]
    public async Task Casilla_aceptada_es_valida_y_no_se_vuelve_a_preguntar()
    {
        var (v, dns, sonda) = Crear();
        Assert.Equal(EstadoVerificacion.Valido, (await v.VerificarAsync("ventas@empresa.com.bo", default)).Estado);
        Assert.Equal(EstadoVerificacion.Valido, (await v.VerificarAsync("VENTAS@empresa.com.bo", default)).Estado);
        Assert.Single(sonda.Probadas);
        Assert.Equal(1, dns.Consultas);
    }

    [Fact]
    public async Task Sin_probar_buzon_basta_con_que_el_dominio_reciba_correo()
    {
        var (v, _, sonda) = Crear(probarBuzon: false);
        Assert.Equal(EstadoVerificacion.Valido, (await v.VerificarAsync("ventas@empresa.com.bo", default)).Estado);
        Assert.Empty(sonda.Probadas);
    }

    // --- En el envío de campañas ---

    private sealed class TiempoFijo(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private sealed class OpcionesFijas<T>(T valor) : IOptionsSnapshot<T>, IOptionsMonitor<T> where T : class
    {
        public T Value => valor;
        public T CurrentValue => valor;
        public T Get(string? name) => valor;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class CorreoFalso : IEnviadorEmail
    {
        public List<string> Para { get; } = [];
        public Task<ResultadoEnvio> EnviarAsync(CorreoSaliente c, CancellationToken ct) { Para.Add(c.Para); return Task.FromResult(ResultadoEnvio.Ok("x")); }
    }

    private sealed class WhatsAppNulo : IEnviadorWhatsApp
    {
        public bool RequierePlantillasAprobadas => true;
        public Task<ResultadoEnvio> EnviarPlantillaAsync(string t, string p, string i, IReadOnlyList<string> pa, CancellationToken ct) => Task.FromResult(ResultadoEnvio.Ok("x"));
        public Task<ResultadoEnvio> EnviarTextoAsync(string t, string x, CancellationToken ct) => Task.FromResult(ResultadoEnvio.Ok("x"));
    }

    private static async Task<(AppDbContext Db, CorreoFalso Correo, MotorCampanas Motor)> MotorConProspectoAsync(string email, SondaFalsa sonda)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var plantilla = new Plantilla { Nombre = "Correo", Canal = Canal.Email, Asunto = "Hola {{Empresa}}", Cuerpo = "<p>Hola</p>" };
        var campana = new Campana { Nombre = "Prueba", Pasos = [new PasoCampana { Orden = 0, Canal = Canal.Email, Plantilla = plantilla }] };
        var prospecto = new Prospecto { Empresa = "Empresa SRL", Email = email, Telefono = "59133334444" };
        // Miércoles 15:00 UTC = 11:00 en Bolivia: horario laboral.
        var ahora = new DateTime(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc);
        db.Inscripciones.Add(new InscripcionCampana { Campana = campana, Prospecto = prospecto, ProximoEnvio = ahora.AddMinutes(-1) });
        await db.SaveChangesAsync();

        var dns = new DnsFalso();
        dns.Mx["empresa.com.bo"] = ["mx.empresa.com.bo"];
        var verificador = new VerificadorCorreo(dns, sonda, Options.Create(new VerificacionCorreoOptions()), TimeProvider.System, NullLogger<VerificadorCorreo>.Instance);
        var campanas = Options.Create(new CampanasOptions());
        var smtp = Options.Create(new SmtpOptions());
        var whatsapp = new OpcionesFijas<WhatsAppOptions>(new WhatsAppOptions());
        var correo = new CorreoFalso();
        var motor = new MotorCampanas(db, correo, new WhatsAppNulo(), new RenderizadorPlantillas(campanas, smtp, whatsapp), new HorarioLaboral(campanas),
            campanas, whatsapp, smtp, verificador, new TiempoFijo(ahora), NullLogger<MotorCampanas>.Instance);
        return (db, correo, motor);
    }

    [Fact]
    public async Task La_campana_no_envia_a_una_casilla_que_no_existe_y_la_marca_como_rebote()
    {
        var sonda = new SondaFalsa();
        sonda.Respuestas["noexiste@empresa.com.bo"] = (550, "550 5.1.1 User unknown");
        var (db, correo, motor) = await MotorConProspectoAsync("noexiste@empresa.com.bo", sonda);

        await motor.ProcesarPendientesAsync(default);

        Assert.Empty(correo.Para);
        var p = await db.Prospectos.SingleAsync();
        Assert.True(p.BajaEmail);
        var m = await db.Mensajes.SingleAsync();
        Assert.Equal(EstadoMensaje.Omitido, m.Estado);
        Assert.StartsWith("Correo no existe", m.Error);
        Assert.Contains(await db.Interacciones.ToListAsync(), i => i.Detalle.Contains("descartado antes de enviar"));
    }

    [Fact]
    public async Task La_campana_si_envia_cuando_la_casilla_existe_o_no_se_puede_saber()
    {
        var sonda = new SondaFalsa();
        sonda.Respuestas["dudoso@empresa.com.bo"] = null;
        foreach (var email in new[] { "ventas@empresa.com.bo", "dudoso@empresa.com.bo" })
        {
            var (db, correo, motor) = await MotorConProspectoAsync(email, sonda);
            await motor.ProcesarPendientesAsync(default);
            Assert.Equal([email], correo.Para);
            Assert.False((await db.Prospectos.SingleAsync()).BajaEmail);
        }
    }
}
