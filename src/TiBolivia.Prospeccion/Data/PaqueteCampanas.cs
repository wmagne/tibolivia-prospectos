using Microsoft.EntityFrameworkCore;
using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Data;

/// <summary>
/// Campañas comerciales de TI Bolivia listas para usar. Se agregan al iniciar el sistema solo si no
/// existen (se buscan por nombre), así que no pisan cambios hechos desde la interfaz.
/// Las campañas se crean INACTIVAS: revise los textos y actívelas desde "Campañas".
/// </summary>
public static class PaqueteCampanas
{
    private record PasoDef(int DiasEspera, Plantilla Plantilla);

    public static async Task AplicarAsync(AppDbContext db)
    {
        // ---------- Servicios ----------
        var farmacias = await ServicioAsync(db, "Sistema para farmacias",
            "Sistema web para farmacias y cadenas: ventas con facturación electrónica SIAT, control de stock por sucursal, " +
            "alertas de productos por vencer y stock mínimo, compras a proveedores, caja, cobro con QR y reportes de ganancias.");
        var siat = await ServicioAsync(db, "Facturación electrónica SIAT",
            "Emisión de facturas electrónicas en línea integrada con Impuestos Nacionales, con inventario, caja y reportes.");
        var software = await ServicioAsync(db, "Desarrollo de software a medida",
            "Sistemas web y móviles diseñados para los procesos de su empresa: ventas, inventarios, cobranzas, RR.HH. y más.");

        // ---------- Campaña 1: Sistema para farmacias ----------
        await CampanaAsync(db, "Farmacias: sistema de gestión y facturación",
            "5 contactos en 12 días para farmacias y cadenas farmacéuticas. Público: propietarios y administradores.",
            farmacias,
            [
                new(0, await CorreoAsync(db, "Farmacias 1 · Presentación",
                    "{{Empresa}}: menos vencimientos y facturación SIAT sin complicaciones",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>Le escribo de <b>TI Bolivia</b>. Trabajamos con farmacias que querían dejar atrás tres problemas muy comunes:</p>
                    <ul>
                      <li>Medicamentos que <b>se vencen en el estante</b> sin que nadie lo note a tiempo.</li>
                      <li><b>Stock descuadrado</b> entre sucursales, kardex y lo que realmente hay.</li>
                      <li>La <b>facturación electrónica SIAT</b> que demora la atención en mostrador.</li>
                    </ul>
                    <p>Nuestro <b>sistema para farmacias</b> resuelve los tres en una sola plataforma web, desde cualquier computadora o sucursal.</p>
                    <p>¿Le parece si le muestro cómo funcionaría en {{Empresa}}? Son 20 minutos, por videollamada o en su farmacia.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia<br>{{EmailComercial}}</p>
                    """)),
                new(2, await WhatsAppAsync(db, "Farmacias 2 · WhatsApp seguimiento",
                    "Hola {{Nombre}}, le escribimos de TI Bolivia. Le enviamos un correo sobre nuestro sistema para farmacias: alertas de vencimiento, stock por sucursal y facturación SIAT. ¿Le gustaría ver una demostración de 20 minutos? Responda BAJA si no desea recibir más mensajes.",
                    "farmacia_seguimiento", "{{Nombre}}")),
                new(2, await CorreoAsync(db, "Farmacias 3 · Funciones y beneficios",
                    "Lo que su farmacia controla con nuestro sistema",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>Le comparto en concreto lo que incluye el sistema para farmacias de TI Bolivia:</p>
                    <table cellpadding="6" style="border-collapse:collapse">
                      <tr><td>💊</td><td><b>Ventas y facturación electrónica SIAT</b> en línea, con proformas, descuentos y devoluciones.</td></tr>
                      <tr><td>⏰</td><td><b>Alertas de productos por vencer</b> y de stock mínimo o agotado, directo en el panel.</td></tr>
                      <tr><td>🏬</td><td><b>Varias sucursales</b>: stock por sucursal, traspasos entre almacenes y kardex por producto.</td></tr>
                      <tr><td>🧾</td><td><b>Compras a proveedores</b>: órdenes de compra, recepción y pagos pendientes.</td></tr>
                      <tr><td>💵</td><td><b>Caja y cobros</b>: apertura y cierre de caja por usuario, cobro con QR y transferencias.</td></tr>
                      <tr><td>📊</td><td><b>Reportes</b> de ventas, ganancias, inversión en inventario y clientes, en PDF y Excel.</td></tr>
                      <tr><td>🎂</td><td><b>Fidelización</b>: clientes, médicos y recordatorios de cumpleaños para promociones.</td></tr>
                    </table>
                    <p>Cada usuario tiene permisos según su rol (cajero, farmacéutico, administrador) y todo queda registrado en una bitácora.</p>
                    <p>¿Agendamos la demostración? Puede responder este correo con el día y la hora que le acomode.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia</p>
                    """)),
                new(3, await CorreoAsync(db, "Farmacias 4 · Invitación a demostración",
                    "{{Nombre}}, ¿vemos su inventario funcionando en el sistema?",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>Una propuesta concreta: en la demostración cargamos algunos productos reales de {{Empresa}} para que vea
                    cómo quedarían sus alertas de vencimiento, su stock y una venta con factura electrónica.</p>
                    <p>Además le explicamos cómo es la <b>migración de sus datos actuales</b> (Excel u otro sistema) y la
                    <b>capacitación</b> de su personal, para que el cambio no afecte la atención.</p>
                    <p>Solo responda este correo o escríbame por WhatsApp y coordinamos.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia</p>
                    """)),
                new(5, await CorreoAsync(db, "Farmacias 5 · Último contacto",
                    "¿Lo dejamos para más adelante, {{Nombre}}?",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>No quiero llenar su bandeja, así que este es mi último correo por ahora.</p>
                    <p>Si en algún momento quiere ordenar el inventario o la facturación de {{Empresa}}, responda este mensaje
                    y le preparo la demostración. Con gusto le atendemos.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia</p>
                    """)),
            ]);

        // ---------- Campaña 2: Facturación electrónica SIAT ----------
        await CampanaAsync(db, "Comercios: facturación electrónica SIAT",
            "4 contactos en 10 días para comercios, distribuidoras y empresas de servicios que deben facturar en línea.",
            siat,
            [
                new(0, await CorreoAsync(db, "SIAT 1 · Presentación",
                    "{{Empresa}}: facturación electrónica en línea, sin dolores de cabeza",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>Soy {{Comercial}} de <b>TI Bolivia</b>. Ayudamos a empresas a cumplir con la <b>facturación electrónica en línea del SIAT</b>
                    con un sistema que además ordena sus ventas, inventario y caja.</p>
                    <ul>
                      <li>Facturas emitidas en segundos, desde la computadora o el celular.</li>
                      <li>Anulaciones, notas y reportes de facturación listos para su contador.</li>
                      <li>Control de stock y caja en el mismo sistema.</li>
                    </ul>
                    <p>¿Cómo factura hoy {{Empresa}}? Si me cuenta, le digo en 15 minutos qué le conviene.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia<br>{{EmailComercial}}</p>
                    """)),
                new(2, await WhatsAppAsync(db, "SIAT 2 · WhatsApp seguimiento",
                    "Hola {{Nombre}}, le escribimos de TI Bolivia sobre la facturación electrónica para {{Empresa}}. ¿Tiene 15 minutos esta semana para resolver sus dudas sin compromiso? Responda BAJA si no desea recibir más mensajes.",
                    "siat_seguimiento", "{{Nombre}},{{Empresa}}")),
                new(3, await CorreoAsync(db, "SIAT 3 · Errores comunes",
                    "3 errores frecuentes con la factura electrónica (y cómo evitarlos)",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>Estos son los problemas que más vemos en empresas que recién empiezan a facturar en línea:</p>
                    <ol>
                      <li><b>Caídas o demoras</b> al emitir que hacen esperar al cliente.</li>
                      <li><b>Datos de productos y clientes desordenados</b>, que generan facturas con errores y anulaciones.</li>
                      <li><b>Reportes manuales</b> para el contador, armados a fin de mes en Excel.</li>
                    </ol>
                    <p>Nuestro sistema está pensado para evitar los tres. Si quiere, se lo muestro con un ejemplo de su rubro, {{Rubro}}.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia</p>
                    """)),
                new(5, await CorreoAsync(db, "SIAT 4 · Último contacto",
                    "¿Seguimos en contacto, {{Nombre}}?",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>Este es mi último correo por ahora. Cuando {{Empresa}} necesite apoyo con la facturación electrónica,
                    responda este mensaje y le atendemos de inmediato.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia</p>
                    """)),
            ]);

        // ---------- Campaña 3: Software a medida ----------
        await CampanaAsync(db, "Empresas: software a medida",
            "4 contactos en 12 días para medianas empresas con procesos en Excel o papel.",
            software,
            [
                new(0, await CorreoAsync(db, "Software 1 · Presentación",
                    "¿Qué proceso de {{Empresa}} sigue en Excel?",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>Soy {{Comercial}} de <b>TI Bolivia</b>. Desarrollamos <b>sistemas a medida</b> para empresas que crecieron
                    y hoy dependen de planillas Excel, cuadernos o grupos de WhatsApp para coordinar su trabajo.</p>
                    <p>Algunos ejemplos de lo que automatizamos: pedidos y ventas, inventarios, cobranzas, rutas de distribución,
                    control de personal y reportes para gerencia.</p>
                    <p>¿Hay algún proceso en {{Empresa}} que le quite tiempo cada semana? Cuénteme y le propongo una solución.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia<br>{{EmailComercial}}</p>
                    """)),
                new(3, await WhatsAppAsync(db, "Software 2 · WhatsApp seguimiento",
                    "Hola {{Nombre}}, de TI Bolivia. ¿Hay algún proceso en {{Empresa}} que hoy se haga en Excel o papel y le gustaría automatizar? Podemos conversarlo sin compromiso. Responda BAJA si no desea recibir más mensajes.",
                    "software_seguimiento", "{{Nombre}},{{Empresa}}")),
                new(4, await CorreoAsync(db, "Software 3 · Cómo trabajamos",
                    "Así trabajamos un proyecto de software (sin sorpresas)",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>Muchas empresas temen que un sistema a medida sea caro, lento o que nunca se termine. Por eso trabajamos así:</p>
                    <ol>
                      <li><b>Reunión de diagnóstico</b> sin costo para entender su proceso.</li>
                      <li><b>Propuesta por etapas</b> con alcance, tiempos y precio cerrado por etapa.</li>
                      <li><b>Entregas parciales</b> que su equipo usa desde las primeras semanas.</li>
                      <li><b>Capacitación y soporte</b> después de la puesta en marcha.</li>
                    </ol>
                    <p>¿Agendamos el diagnóstico? Responda este correo con su disponibilidad.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia</p>
                    """)),
                new(5, await CorreoAsync(db, "Software 4 · Último contacto",
                    "Quedo atento, {{Nombre}}",
                    """
                    <p>Hola {{Nombre}},</p>
                    <p>Cierro aquí mis mensajes para no molestarle. Si más adelante quiere automatizar algún proceso de {{Empresa}},
                    responda este correo y conversamos.</p>
                    <p>Saludos,<br>{{Comercial}}<br>TI Bolivia</p>
                    """)),
            ]);

        await db.SaveChangesAsync();
    }

    private static async Task<Servicio> ServicioAsync(AppDbContext db, string nombre, string descripcion)
    {
        var s = await db.Servicios.FirstOrDefaultAsync(x => x.Nombre == nombre);
        if (s is not null) return s;
        s = new Servicio { Nombre = nombre, Descripcion = descripcion };
        db.Servicios.Add(s);
        return s;
    }

    private static async Task<Plantilla> CorreoAsync(AppDbContext db, string nombre, string asunto, string cuerpo) =>
        await db.Plantillas.FirstOrDefaultAsync(x => x.Nombre == nombre)
        ?? db.Plantillas.Add(new Plantilla { Nombre = nombre, Canal = Canal.Email, Asunto = asunto, Cuerpo = cuerpo }).Entity;

    private static async Task<Plantilla> WhatsAppAsync(AppDbContext db, string nombre, string texto, string plantillaMeta, string parametros) =>
        await db.Plantillas.FirstOrDefaultAsync(x => x.Nombre == nombre)
        ?? db.Plantillas.Add(new Plantilla
        {
            Nombre = nombre, Canal = Canal.WhatsApp, Cuerpo = texto,
            WhatsAppPlantillaMeta = plantillaMeta, WhatsAppIdioma = "es", WhatsAppParametros = parametros,
        }).Entity;

    private static async Task CampanaAsync(AppDbContext db, string nombre, string descripcion, Servicio servicio, PasoDef[] pasos)
    {
        if (await db.Campanas.AnyAsync(c => c.Nombre == nombre)) return;
        db.Campanas.Add(new Campana
        {
            Nombre = nombre,
            Descripcion = descripcion,
            Servicio = servicio,
            Activa = false,
            Pasos = pasos.Select((p, i) => new PasoCampana
            {
                Orden = i + 1, DiasEspera = p.DiasEspera, Canal = p.Plantilla.Canal, Plantilla = p.Plantilla,
            }).ToList(),
        });
    }
}
