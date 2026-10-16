using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages;

/// <summary>Textos y colores para mostrar estados en la interfaz.</summary>
public static class Etiquetas
{
    public static string Texto(EstadoProspecto e) => e switch
    {
        EstadoProspecto.ReunionAgendada => "Reunión agendada",
        EstadoProspecto.PropuestaEnviada => "Propuesta enviada",
        EstadoProspecto.NoContactar => "No contactar",
        _ => e.ToString(),
    };

    public static string Color(EstadoProspecto e) => e switch
    {
        EstadoProspecto.Nuevo => "secondary",
        EstadoProspecto.Contactado => "info",
        EstadoProspecto.Interesado => "primary",
        EstadoProspecto.ReunionAgendada or EstadoProspecto.PropuestaEnviada => "warning",
        EstadoProspecto.Ganado => "success",
        _ => "dark",
    };

    public static string Color(EstadoMensaje e) => e switch
    {
        EstadoMensaje.Enviado or EstadoMensaje.Entregado => "info",
        EstadoMensaje.Leido or EstadoMensaje.Recibido => "success",
        EstadoMensaje.Fallido => "danger",
        EstadoMensaje.Omitido => "secondary",
        _ => "light",
    };

    public static string Color(EstadoInscripcion e) => e switch
    {
        EstadoInscripcion.Activa => "success",
        EstadoInscripcion.Pausada => "warning",
        EstadoInscripcion.Completada => "info",
        _ => "secondary",
    };

    public static string Texto(Canal c) => c == Canal.Email ? "Correo" : "WhatsApp";
}
