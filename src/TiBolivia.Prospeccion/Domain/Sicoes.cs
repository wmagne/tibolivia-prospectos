namespace TiBolivia.Prospeccion.Domain;

// Módulo "Alertas SICOES". Vive en tablas propias (prefijo Sicoes) y no toca prospectos ni campañas:
// si el negocio crece hasta 10 clientes pagando, el plan prevé separarlo como aplicación aparte y así se lleva entero.

public enum EstadoOportunidadSicoes { Nueva, EnAnalisis, PropuestaEnviada, Ganada, Perdida, Descartada }

/// <summary>Planes que se le pueden ofrecer a un cliente. Por ahora solo se anotan en su perfil: no se cobra nada.</summary>
public enum PlanSicoes { Interno, Piloto, AlertaWhatsApp, PlanGestor }

/// <summary>Una convocatoria publicada en el SICOES. El CUCE es el código único del proceso y evita duplicados.</summary>
public class SicoesProceso
{
    public int Id { get; set; }
    public string Cuce { get; set; } = "";
    public string Entidad { get; set; } = "";
    public string Objeto { get; set; } = "";
    public string? Modalidad { get; set; }
    public decimal? Monto { get; set; }
    public string? Departamento { get; set; }
    public DateTime? FechaPublicacion { get; set; }
    /// <summary>Fecha y hora límite de presentación, en UTC.</summary>
    public DateTime? FechaLimite { get; set; }
    public string? Enlace { get; set; }
    public string? Estado { get; set; }
    /// <summary>De dónde vino el dato: "archivo", "url" o el nombre de la fuente.</summary>
    public string Origen { get; set; } = "";
    public DateTime LeidoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public bool EsConsultaDePrecios =>
        Modalidad?.Contains("consulta de precio", StringComparison.OrdinalIgnoreCase) == true
        || Modalidad?.Trim().Equals("CP", StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>Lo que le interesa a un cliente del servicio de alertas (el primero es TIBolivia).</summary>
public class SicoesPerfil
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    /// <summary>Palabras clave separadas por coma. Una frase ("soporte técnico") cuenta como una sola clave.</summary>
    public string PalabrasClave { get; set; } = "";
    public string PalabrasExcluidas { get; set; } = "";
    /// <summary>Departamentos separados por coma. Vacío = todo el país.</summary>
    public string Departamentos { get; set; } = "";
    /// <summary>Si es true, también sirven los procesos sin departamento o de alcance nacional.</summary>
    public bool IncluyeNacional { get; set; } = true;
    public decimal MontoMinimo { get; set; }
    public PlanSicoes Plan { get; set; } = PlanSicoes.Interno;
    /// <summary>Tarifa mensual acordada en bolivianos (solo informativa: todavía no se cobra).</summary>
    public decimal TarifaMensualBs { get; set; }
    public DateTime? PilotoHasta { get; set; }
    /// <summary>Destinos de los avisos de este perfil. En pruebas, solo los del equipo de TIBolivia.</summary>
    public string? CorreoAvisos { get; set; }
    public string? WhatsAppAvisos { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Proceso que encaja con un perfil (puntaje 60 o más): se sigue como una venta posible.</summary>
public class SicoesOportunidad
{
    public int Id { get; set; }
    public int ProcesoId { get; set; }
    public SicoesProceso Proceso { get; set; } = null!;
    public int PerfilId { get; set; }
    public SicoesPerfil Perfil { get; set; } = null!;
    public int Puntaje { get; set; }
    /// <summary>Por qué tuvo ese puntaje, para poder ajustar las palabras clave.</summary>
    public string Motivo { get; set; } = "";
    public EstadoOportunidadSicoes Estado { get; set; } = EstadoOportunidadSicoes.Nueva;
    public string? Notas { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Registro de cada lectura, para el diagnóstico: si el lector deja de traer datos hay que saberlo el mismo día.</summary>
public class SicoesLectura
{
    public int Id { get; set; }
    public DateTime Inicio { get; set; } = DateTime.UtcNow;
    public DateTime? Fin { get; set; }
    public string Fuente { get; set; } = "";
    public int Leidos { get; set; }
    public int Nuevos { get; set; }
    public int Relevantes { get; set; }
    public bool Exito { get; set; }
    public string? Error { get; set; }
}

/// <summary>Avisos ya enviados: evita mandar dos veces el mismo aviso de la misma oportunidad.</summary>
public class SicoesAviso
{
    public int Id { get; set; }
    public int? OportunidadId { get; set; }
    public SicoesOportunidad? Oportunidad { get; set; }
    /// <summary>"resumen", "urgente" o "recordatorio48h".</summary>
    public string Tipo { get; set; } = "";
    public string Canal { get; set; } = "";
    public string Destino { get; set; } = "";
    /// <summary>Para el resumen diario: la fecha de Bolivia (yyyy-MM-dd).</summary>
    public string? Clave { get; set; }
    public bool Exito { get; set; }
    public string? Error { get; set; }
    public DateTime EnviadoEn { get; set; } = DateTime.UtcNow;
}
