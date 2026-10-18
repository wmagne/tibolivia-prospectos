using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Data;

/// <summary>Crea roles, el usuario administrador y datos de ejemplo la primera vez.</summary>
public static class DatosIniciales
{
    public static async Task InicializarAsync(IServiceProvider sp, IConfiguration config)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in new[] { Roles.Admin, Roles.Comercial })
            if (!await roles.RoleExistsAsync(rol))
                await roles.CreateAsync(new IdentityRole(rol));

        var usuarios = sp.GetRequiredService<UserManager<Usuario>>();
        var emailAdmin = config["Admin:Email"] ?? "admin@tibolivia.com";
        if (await usuarios.FindByEmailAsync(emailAdmin) is null)
        {
            var admin = new Usuario { UserName = emailAdmin, Email = emailAdmin, NombreCompleto = "Administrador", EmailConfirmed = true };
            var r = await usuarios.CreateAsync(admin, config["Admin:Password"] ?? throw new InvalidOperationException("Configure Admin:Password"));
            if (!r.Succeeded) throw new InvalidOperationException(string.Join("; ", r.Errors.Select(e => e.Description)));
            await usuarios.AddToRoleAsync(admin, Roles.Admin);
        }

        if (!await db.Servicios.AnyAsync()) await CrearEjemplosAsync(db);
        await PaqueteCampanas.AplicarAsync(db);
    }

    private static async Task CrearEjemplosAsync(AppDbContext db)
    {

        // Datos de ejemplo: edítelos desde la interfaz con los servicios y textos reales de TI Bolivia.
        var desarrollo = new Servicio { Nombre = "Desarrollo de software a medida", Descripcion = "Sistemas web y móviles diseñados para los procesos de su empresa." };
        db.Servicios.AddRange(
            desarrollo,
            new Servicio { Nombre = "Facturación electrónica SIAT", Descripcion = "Integración con Impuestos Nacionales para emitir facturas en línea." },
            new Servicio { Nombre = "Chatbots y automatización por WhatsApp", Descripcion = "Atención 24/7 a sus clientes en su web y WhatsApp." },
            new Servicio { Nombre = "Soporte técnico e infraestructura", Descripcion = "Redes, servidores, respaldos y mesa de ayuda." });

        var correo1 = new Plantilla
        {
            Nombre = "Presentación inicial", Canal = Canal.Email,
            Asunto = "{{Empresa}}: una propuesta de TI Bolivia",
            Cuerpo = """
                <p>Hola {{Nombre}},</p>
                <p>Soy {{Comercial}} de <b>TI Bolivia</b>. Ayudamos a empresas de {{Ciudad}} a crecer con tecnología.</p>
                <p>Creemos que <b>{{Servicio}}</b> puede aportar mucho a {{Empresa}}: {{DescripcionServicio}}</p>
                <p>¿Le parece si agendamos una reunión de 20 minutos esta semana?</p>
                <p>Saludos,<br>{{Comercial}}<br>TI Bolivia</p>
                """,
        };
        var whatsapp1 = new Plantilla
        {
            Nombre = "Seguimiento por WhatsApp", Canal = Canal.WhatsApp,
            Cuerpo = "Hola {{Nombre}}, le escribimos de TI Bolivia. Le enviamos un correo sobre {{Servicio}} para {{Empresa}}. ¿Tiene alguna consulta?",
            WhatsAppPlantillaMeta = "seguimiento_propuesta", WhatsAppIdioma = "es",
            WhatsAppParametros = "{{Nombre}},{{Servicio}},{{Empresa}}",
        };
        var correo2 = new Plantilla
        {
            Nombre = "Caso de éxito", Canal = Canal.Email,
            Asunto = "Cómo otras empresas de {{Rubro}} usan {{Servicio}}",
            Cuerpo = """
                <p>Hola {{Nombre}},</p>
                <p>Le comparto cómo una empresa similar a {{Empresa}} redujo tiempos y costos con nuestra solución de {{Servicio}}.</p>
                <p>Si le interesa, puedo prepararle una demostración sin costo.</p>
                <p>Saludos,<br>{{Comercial}}</p>
                """,
        };
        var correo3 = new Plantilla
        {
            Nombre = "Último contacto", Canal = Canal.Email,
            Asunto = "¿Cerramos el tema, {{Nombre}}?",
            Cuerpo = """
                <p>Hola {{Nombre}},</p>
                <p>No quiero llenar su bandeja. Si ahora no es el momento, no hay problema: responda este correo cuando lo necesite.</p>
                <p>Saludos,<br>{{Comercial}} — TI Bolivia</p>
                """,
        };
        db.Plantillas.AddRange(correo1, whatsapp1, correo2, correo3);

        db.Campanas.Add(new Campana
        {
            Nombre = "Presentación de servicios TI Bolivia",
            Descripcion = "Secuencia de 4 contactos en 10 días (correo + WhatsApp).",
            Servicio = desarrollo,
            Activa = false,
            Pasos =
            [
                new PasoCampana { Orden = 1, DiasEspera = 0, Canal = Canal.Email, Plantilla = correo1 },
                new PasoCampana { Orden = 2, DiasEspera = 2, Canal = Canal.WhatsApp, Plantilla = whatsapp1 },
                new PasoCampana { Orden = 3, DiasEspera = 3, Canal = Canal.Email, Plantilla = correo2 },
                new PasoCampana { Orden = 4, DiasEspera = 5, Canal = Canal.Email, Plantilla = correo3 },
            ],
        });
        await db.SaveChangesAsync();
    }
}
