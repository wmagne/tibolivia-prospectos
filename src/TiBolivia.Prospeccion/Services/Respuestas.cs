using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services;

public static class Respuestas
{
    /// <summary>
    /// El prospecto respondió (correo o WhatsApp): se pausan sus secuencias para que lo atienda una persona
    /// y pasa a "Interesado". Requiere Inscripciones y su Campana cargadas.
    /// </summary>
    public static void Registrar(Prospecto p, DateTime cuando)
    {
        p.UltimaRespuestaEn = cuando;
        p.ActualizadoEn = DateTime.UtcNow;
        foreach (var i in p.Inscripciones.Where(i => i.Estado == EstadoInscripcion.Activa && i.Campana.PausarAlResponder))
        {
            i.Estado = EstadoInscripcion.Pausada;
            i.ProximoEnvio = null;
        }
        if (p.Estado is EstadoProspecto.Nuevo or EstadoProspecto.Contactado)
            p.Estado = EstadoProspecto.Interesado;
    }
}
