using Microsoft.AspNetCore.Identity;

namespace TiBolivia.Prospeccion.Domain;

public enum EstadoProspecto { Nuevo, Contactado, Interesado, ReunionAgendada, PropuestaEnviada, Ganado, Perdido, NoContactar }
/// <summary>Tarea = paso manual que se asigna al comercial (llamar, WhatsApp personalizado, visita).</summary>
public enum Canal { Email, WhatsApp, Tarea }
public enum EstadoInscripcion { Activa, Pausada, Completada, Detenida }
public enum DireccionMensaje { Saliente, Entrante }
public enum EstadoMensaje { Pendiente, Enviado, Entregado, Leido, Fallido, Recibido, Omitido }
public enum TipoInteraccion { Nota, Llamada, Reunion, Correo, WhatsApp, Sistema }

public static class Roles
{
    public const string Admin = "Admin";
    public const string Comercial = "Comercial";
}

public class Usuario : IdentityUser
{
    public string NombreCompleto { get; set; } = "";
    public bool Activo { get; set; } = true;
}

/// <summary>Servicio que ofrece TI Bolivia y que se promociona en las campañas.</summary>
public class Servicio
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string? UrlInformacion { get; set; }
    public bool Activo { get; set; } = true;
}

public class Prospecto
{
    public int Id { get; set; }
    public string Empresa { get; set; } = "";
    public string? NombreContacto { get; set; }
    public string? Cargo { get; set; }
    public string? Email { get; set; }
    /// <summary>Número en formato internacional solo dígitos (ej. 59171234567).</summary>
    public string? Telefono { get; set; }
    public string? Rubro { get; set; }
    public string? Ciudad { get; set; }
    public string? SitioWeb { get; set; }
    public string? Origen { get; set; }
    public string? Notas { get; set; }
    public EstadoProspecto Estado { get; set; } = EstadoProspecto.Nuevo;

    /// <summary>A = necesidad visible y decisión rápida; B = buen encaje; C = empresa grande, ciclo largo.</summary>
    public string? Prioridad { get; set; }
    public string? Direccion { get; set; }
    /// <summary>Qué ofrecerle: necesidad detectada / servicio de entrada.</summary>
    public string? Necesidad { get; set; }
    /// <summary>De dónde salieron los datos (URL o directorio).</summary>
    public string? Fuente { get; set; }
    public bool DatosPorVerificar { get; set; }
    /// <summary>Valor estimado del negocio en Bs (para el embudo).</summary>
    public decimal? ValorEstimado { get; set; }
    public string? ProximaAccion { get; set; }
    public DateTime? FechaProximaAccion { get; set; }

    public string? AsignadoAId { get; set; }
    public Usuario? AsignadoA { get; set; }

    public bool ConsentimientoWhatsApp { get; set; }
    public bool BajaEmail { get; set; }
    public bool BajaWhatsApp { get; set; }
    public Guid TokenBaja { get; set; } = Guid.NewGuid();

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? UltimaRespuestaEn { get; set; }

    public List<InscripcionCampana> Inscripciones { get; set; } = [];
    public List<Mensaje> Mensajes { get; set; } = [];
    public List<Interaccion> Interacciones { get; set; } = [];

    public string NombreSaludo =>
        string.IsNullOrWhiteSpace(NombreContacto) ? "" : NombreContacto.Trim().Split(' ')[0];

    public bool PuedeRecibirEmail => !string.IsNullOrWhiteSpace(Email) && !BajaEmail && Estado != EstadoProspecto.NoContactar;
    public bool PuedeRecibirWhatsApp => !string.IsNullOrWhiteSpace(Telefono) && !BajaWhatsApp && Estado != EstadoProspecto.NoContactar;
}

public class Plantilla
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public Canal Canal { get; set; }
    /// <summary>Asunto del correo (solo Email). Admite variables {{...}}.</summary>
    public string? Asunto { get; set; }
    /// <summary>Email: HTML. WhatsApp: texto de referencia / texto libre dentro de la ventana de 24 h.</summary>
    public string Cuerpo { get; set; } = "";
    /// <summary>Nombre de la plantilla aprobada en Meta (obligatoria para iniciar conversaciones).</summary>
    public string? WhatsAppPlantillaMeta { get; set; }
    public string WhatsAppIdioma { get; set; } = "es";
    /// <summary>Variables separadas por coma que llenan {{1}}, {{2}}... de la plantilla de Meta. Ej: {{Nombre}},{{Empresa}}</summary>
    public string? WhatsAppParametros { get; set; }
    /// <summary>Archivo adjunto de los correos (ej. propuesta comercial en PDF).</summary>
    public string? AdjuntoNombre { get; set; }
    public byte[]? AdjuntoContenido { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Secuencia de mensajes (drip) que se envía a lo largo del tiempo.</summary>
public class Campana
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public int? ServicioId { get; set; }
    public Servicio? Servicio { get; set; }
    public bool Activa { get; set; } = true;
    /// <summary>Si el prospecto responde (WhatsApp) se pausa la secuencia para que lo atienda un comercial.</summary>
    public bool PausarAlResponder { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public List<PasoCampana> Pasos { get; set; } = [];
    public List<InscripcionCampana> Inscripciones { get; set; } = [];
}

public class PasoCampana
{
    public int Id { get; set; }
    public int CampanaId { get; set; }
    public Campana? Campana { get; set; }
    public int Orden { get; set; }
    /// <summary>Días de espera desde el paso anterior (o desde la inscripción para el primer paso).</summary>
    public int DiasEspera { get; set; }
    public Canal Canal { get; set; }
    public int PlantillaId { get; set; }
    public Plantilla? Plantilla { get; set; }
}

public class InscripcionCampana
{
    public int Id { get; set; }
    public int CampanaId { get; set; }
    public Campana Campana { get; set; } = null!;
    public int ProspectoId { get; set; }
    public Prospecto Prospecto { get; set; } = null!;
    /// <summary>Índice (0..n) del siguiente paso a enviar, según el orden de los pasos.</summary>
    public int PasoActual { get; set; }
    public DateTime? ProximoEnvio { get; set; }
    public int Intentos { get; set; }
    public EstadoInscripcion Estado { get; set; } = EstadoInscripcion.Activa;
    public string? InscritoPorId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public class Mensaje
{
    public long Id { get; set; }
    public int ProspectoId { get; set; }
    public Prospecto? Prospecto { get; set; }
    public int? InscripcionId { get; set; }
    public InscripcionCampana? Inscripcion { get; set; }
    public Canal Canal { get; set; }
    public DireccionMensaje Direccion { get; set; }
    public string? Asunto { get; set; }
    public string Cuerpo { get; set; } = "";
    public EstadoMensaje Estado { get; set; }
    /// <summary>Id del proveedor (wamid de WhatsApp o Message-Id del correo).</summary>
    public string? ProveedorId { get; set; }
    public string? Error { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public class Interaccion
{
    public int Id { get; set; }
    public int ProspectoId { get; set; }
    public Prospecto? Prospecto { get; set; }
    public string? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public TipoInteraccion Tipo { get; set; }
    public string Detalle { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
