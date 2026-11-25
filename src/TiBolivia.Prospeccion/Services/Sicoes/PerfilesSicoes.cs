using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Data;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Services.Sicoes;

public static class PerfilesSicoes
{
    public const string NombreTiBolivia = "TIBolivia";

    /// <summary>Lo que TIBolivia le puede vender al Estado. Se ajusta desde SICOES → Perfiles según lo que llegue cada día.</summary>
    public const string ClavesTiBolivia =
        "computadora, computadoras, equipos de computacion, equipo de computacion, laptop, portatil, software, sistema informatico, " +
        "sistemas informaticos, licencia de software, licencias, redes, red de datos, cableado estructurado, servidor, servidores, " +
        "soporte tecnico, mantenimiento informatico, mantenimiento de equipos informaticos, mantenimiento de equipos de computacion, " +
        "tecnologias de la informacion, informatica, camaras de seguridad, camaras de vigilancia, videovigilancia, camaras";

    /// <summary>Usos de las mismas palabras que no son compras de tecnología.</summary>
    public const string ExcluidasTiBolivia = "camara frigorifica, camaras frigorificas, camara de llanta, camaras de llanta, red de agua, red de alcantarillado, redes de agua, red de gas, redes de gas, red electrica";

    public static async Task CrearPerfilTiBoliviaAsync(AppDbContext db)
    {
        if (await db.SicoesPerfiles.AnyAsync()) return;
        db.SicoesPerfiles.Add(new SicoesPerfil
        {
            Nombre = NombreTiBolivia,
            PalabrasClave = ClavesTiBolivia,
            PalabrasExcluidas = ExcluidasTiBolivia,
            Departamentos = "Santa Cruz",
            IncluyeNacional = true,
            MontoMinimo = 0,
            Plan = PlanSicoes.Interno,
            CorreoAvisos = "gerencia@tibolivia.com",
            WhatsAppAvisos = "59170935349",
        });
        await db.SaveChangesAsync();
    }
}
