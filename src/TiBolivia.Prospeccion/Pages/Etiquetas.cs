using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Pages;

/// <summary>Textos y colores para mostrar estados en la interfaz.</summary>
public static class Etiquetas
{
    public static string Texto(EstadoProspecto e) => e switch
    {
        EstadoProspecto.ReunionAgendada => "Diagnóstico agendado",
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

    public static string Texto(Canal c) => c switch { Canal.Email => "Correo", Canal.WhatsApp => "WhatsApp", _ => "Tarea manual" };

    public static string ColorPrioridad(string? p) => p switch { "A" => "danger", "B" => "warning text-dark", "C" => "secondary", _ => "light text-dark" };

    /// <summary>Lista de estados con su texto, para los select (los enum muestran el nombre interno).</summary>
    public static IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Estados(EstadoProspecto? seleccionado = null) =>
        Enum.GetValues<EstadoProspecto>().Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(Texto(e), ((int)e).ToString(), e == seleccionado));
}
