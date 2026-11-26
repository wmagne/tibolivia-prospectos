using TiBolivia.Prospeccion.Domain;
using TiBolivia.Prospeccion.Services.Sicoes;

namespace TiBolivia.Prospeccion.Tests;

public class FiltroSicoesTests
{
    private static SicoesPerfil PerfilTiBolivia() => new()
    {
        Nombre = PerfilesSicoes.NombreTiBolivia,
        PalabrasClave = PerfilesSicoes.ClavesTiBolivia,
        PalabrasExcluidas = PerfilesSicoes.ExcluidasTiBolivia,
        Departamentos = "Santa Cruz",
        IncluyeNacional = true,
    };

    private static SicoesProceso Proceso(string objeto, string? departamento = "SANTA CRUZ", string? modalidad = "ANPE", decimal? monto = 150000) => new()
    {
        Cuce = "26-0513-00-1234567-1-1", Entidad = "GOBIERNO AUTONOMO MUNICIPAL DE SANTA CRUZ", Objeto = objeto,
        Departamento = departamento, Modalidad = modalidad, Monto = monto,
    };

    [Theory]
    [InlineData("ADQUISICIÓN DE COMPUTADORAS DE ESCRITORIO PARA LA UNIDAD DE SISTEMAS")]
    [InlineData("Adquisicion de computadora portatil")]
    [InlineData("SERVICIO DE SOPORTE TÉCNICO Y MANTENIMIENTO INFORMÁTICO")]
    [InlineData("Instalación de cámaras de seguridad en mercados municipales")]
    [InlineData("ADQUISICION DE LICENCIAS DE SOFTWARE ANTIVIRUS")]
    [InlineData("Cableado estructurado y equipos de red de datos")]
    public void Compras_de_TI_en_Santa_Cruz_son_oportunidad(string objeto)
    {
        var ev = FiltroSicoes.Evaluar(Proceso(objeto), PerfilTiBolivia());
        Assert.True(ev.EsRelevante, $"{objeto} → {ev.Puntaje} ({ev.Motivo})");
    }

    [Theory]
    [InlineData("ADQUISICIÓN DE CEMENTO PORTLAND PARA OBRAS")]
    [InlineData("Servicio de limpieza de oficinas")]
    [InlineData("Compra de material de escritorio")]
    public void Compras_de_otro_rubro_no_llegan_al_umbral(string objeto)
    {
        var ev = FiltroSicoes.Evaluar(Proceso(objeto), PerfilTiBolivia());
        Assert.False(ev.EsRelevante, $"{objeto} → {ev.Puntaje} ({ev.Motivo})");
        Assert.InRange(ev.Puntaje, 0, FiltroSicoes.UmbralOportunidad - 1);
    }

    [Theory]
    [InlineData("Mantenimiento de cámara frigorífica del matadero")]
    [InlineData("Ampliación de red de agua potable barrio Los Lotes")]
    [InlineData("Provisión de cámaras de llanta para volquetas")]
    public void Palabras_excluidas_dan_cero_aunque_haya_palabra_clave(string objeto)
    {
        var ev = FiltroSicoes.Evaluar(Proceso(objeto), PerfilTiBolivia());
        Assert.Equal(0, ev.Puntaje);
        Assert.StartsWith("Excluido", ev.Motivo);
    }

    [Fact]
    public void Otro_departamento_queda_fuera()
    {
        var ev = FiltroSicoes.Evaluar(Proceso("Adquisición de computadoras", departamento: "LA PAZ"), PerfilTiBolivia());
        Assert.Equal(0, ev.Puntaje);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NACIONAL")]
    public void Alcance_nacional_sirve_si_el_perfil_lo_incluye(string? departamento)
    {
        var perfil = PerfilTiBolivia();
        Assert.True(FiltroSicoes.Evaluar(Proceso("Adquisición de servidores", departamento), perfil).EsRelevante);
        perfil.IncluyeNacional = false;
        Assert.False(FiltroSicoes.Evaluar(Proceso("Adquisición de servidores", departamento), perfil).EsRelevante);
    }

    [Fact]
    public void Monto_menor_al_minimo_da_cero_y_sin_monto_no_se_descarta()
    {
        var perfil = PerfilTiBolivia();
        perfil.MontoMinimo = 50000;
        Assert.Equal(0, FiltroSicoes.Evaluar(Proceso("Adquisición de computadoras", monto: 20000), perfil).Puntaje);
        Assert.True(FiltroSicoes.Evaluar(Proceso("Adquisición de computadoras", monto: null), perfil).EsRelevante);
    }

    [Fact]
    public void Consulta_de_precios_suma_puntaje()
    {
        var normal = FiltroSicoes.Evaluar(Proceso("Adquisición de computadoras"), PerfilTiBolivia());
        var cp = FiltroSicoes.Evaluar(Proceso("Adquisición de computadoras", modalidad: "Consulta de Precios"), PerfilTiBolivia());
        Assert.True(cp.Puntaje > normal.Puntaje);
        Assert.Contains("Consulta de Precios", cp.Motivo);
    }

    [Fact]
    public void Mas_palabras_clave_dan_mas_puntaje_sin_pasar_de_100()
    {
        var una = FiltroSicoes.Evaluar(Proceso("Compra de laptop"), PerfilTiBolivia());
        var varias = FiltroSicoes.Evaluar(Proceso("Compra de laptop, servidores, licencias y software con soporte técnico",
            modalidad: "Consulta de Precios"), PerfilTiBolivia());
        Assert.True(varias.Puntaje > una.Puntaje);
        Assert.InRange(varias.Puntaje, 0, 100);
    }

    [Fact]
    public void Singular_y_plural_de_la_misma_clave_cuentan_una_vez()
    {
        // El perfil tiene "computadora" y "computadoras": el proceso no debe valer más que con una sola palabra.
        var ev = FiltroSicoes.Evaluar(Proceso("Adquisición de computadoras"), PerfilTiBolivia());
        Assert.Equal(55 + 15, ev.Puntaje);
    }

    [Fact]
    public void La_palabra_debe_ser_entera_no_un_pedazo()
    {
        // "redes" no debe encontrarse dentro de "paredes", ni "red" dentro de "reductor".
        var ev = FiltroSicoes.Evaluar(Proceso("Pintado de paredes y reductor de velocidad"), PerfilTiBolivia());
        Assert.False(ev.EsRelevante, ev.Motivo);
    }
}

public class PlantillaAvisoSicoesTests
{
    [Fact]
    public void La_plantilla_de_Meta_tiene_las_4_variables_que_manda_el_aviso()
    {
        // AvisosSicoes envía entidad, objeto, monto y modalidad, y cierre: si la plantilla aprobada cambia, Meta rechaza el envío.
        var variables = System.Text.RegularExpressions.Regex.Matches(TiBolivia.Prospeccion.Services.ConfiguradorMeta.TextoAvisoSicoes, @"\{\{(\d)\}\}")
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["1", "2", "3", "4"], variables);
        Assert.Equal(4, TiBolivia.Prospeccion.Services.ConfiguradorMeta.EjemplosAvisoSicoes.Length);
    }
}

public class PlantillaAvisoSicoesFormaTests
{
    [Fact]
    public void Meta_no_acepta_variables_al_principio_ni_al_final()
    {
        var t = TiBolivia.Prospeccion.Services.ConfiguradorMeta.TextoAvisoSicoes.Trim().TrimEnd('.', ' ');
        Assert.False(t.StartsWith("{{"), "empieza con variable");
        Assert.False(t.EndsWith("}}"), "termina con variable");
    }
}
