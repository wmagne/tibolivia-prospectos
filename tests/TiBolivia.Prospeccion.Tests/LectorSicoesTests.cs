using TiBolivia.Prospeccion.Services.Sicoes;

namespace TiBolivia.Prospeccion.Tests;

public class LectorSicoesTests
{
    [Fact]
    public void Lee_tabla_pegada_con_punto_y_coma_titulos_con_tildes_y_formato_boliviano()
    {
        const string texto = """
            CUCE;Entidad;Objeto de Contratación;Modalidad;Precio Referencial;Departamento;Fecha de Publicación;Fecha Límite;Estado
            26-0513-00-1234567-1-1;GAM SANTA CRUZ;ADQUISICIÓN DE COMPUTADORAS;ANPE;1.234.567,50;SANTA CRUZ;20/11/2026;28/11/2026 10:30;Vigente
            26-0101-00-7654321-1-1;MINISTERIO DE SALUD;SERVIDORES;Consulta de Precios;85000;LA PAZ;21/11/2026;;Vigente
            """;
        var r = LectorSicoes.LeerTexto(texto, "archivo");
        Assert.Empty(r.Errores);
        Assert.Equal(2, r.Procesos.Count);
        var p = r.Procesos[0];
        Assert.Equal("26-0513-00-1234567-1-1", p.Cuce);
        Assert.Equal("ADQUISICIÓN DE COMPUTADORAS", p.Objeto);
        Assert.Equal(1234567.50m, p.Monto);
        // 10:30 de Bolivia = 14:30 UTC.
        Assert.Equal(new DateTime(2026, 11, 28, 14, 30, 0, DateTimeKind.Utc), p.FechaLimite);
        Assert.Equal(DateTimeKind.Utc, p.FechaLimite!.Value.Kind);
        Assert.Null(r.Procesos[1].FechaLimite);
        Assert.True(r.Procesos[1].EsConsultaDePrecios);
    }

    [Fact]
    public void Lee_tabla_copiada_de_planilla_separada_por_tabulador()
    {
        var texto = "cuce\tentidad\tobjeto\tmonto\n26-0513-00-1111111-1-1\tYPFB\tLicencias de software\t25.000\n";
        var r = LectorSicoes.LeerTexto(texto, "archivo");
        Assert.Single(r.Procesos);
        Assert.Equal(25000m, r.Procesos[0].Monto);
    }

    [Fact]
    public void Lee_csv_con_comas_y_campos_entre_comillas()
    {
        var texto = "CUCE,Entidad,Objeto,Monto\n\"26-0513-00-2222222-1-1\",\"GAM, Warnes\",\"Cámaras, cableado y redes\",\"1,500.75\"\n";
        var r = LectorSicoes.LeerTexto(texto, "archivo");
        Assert.Single(r.Procesos);
        Assert.Equal("GAM, Warnes", r.Procesos[0].Entidad);
        Assert.Equal(1500.75m, r.Procesos[0].Monto);
    }

    [Fact]
    public void El_mismo_CUCE_repetido_en_la_lista_entra_una_sola_vez()
    {
        var texto = "CUCE;Entidad;Objeto\n26-0513-00-1234567-1-1;A;Computadoras\n26-0513-00-1234567-1-1;A;Computadoras\n";
        Assert.Single(LectorSicoes.LeerTexto(texto, "archivo").Procesos);
    }

    [Fact]
    public void Sin_columnas_obligatorias_avisa_y_no_lee_nada()
    {
        var r = LectorSicoes.LeerTexto("Nombre;Telefono\nA;123\n", "archivo");
        Assert.Empty(r.Procesos);
        Assert.Contains(r.Errores, e => e.Contains("cuce"));
    }

    [Fact]
    public void Filas_sin_CUCE_o_con_CUCE_raro_se_omiten()
    {
        var texto = "CUCE;Entidad;Objeto\n;A;Sin cuce\nxx;B;Cuce raro\n26-0513-00-1234567-1-1;C;Bueno\n";
        var r = LectorSicoes.LeerTexto(texto, "archivo");
        Assert.Single(r.Procesos);
        Assert.Single(r.Errores);
    }

    [Theory]
    [InlineData("Bs 1.500.000,00", 1500000d)]
    [InlineData("1,500,000.00", 1500000d)]
    [InlineData("85000", 85000d)]
    [InlineData("85000.5", 85000.5)]
    [InlineData("12.000", 12000d)]
    [InlineData("", null)]
    [InlineData("sin dato", null)]
    public void Montos(string texto, double? esperado)
    {
        Assert.Equal(esperado is null ? null : (decimal)esperado, LectorSicoes.Monto(texto));
    }
}
