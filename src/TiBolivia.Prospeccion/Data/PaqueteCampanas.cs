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

        await CampanasSantaCruzAsync(db);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Campañas basadas en la estrategia comercial de TIBolivia (documento "Base de prospectos y estrategia comercial"):
    /// WhatsApp personalizado (tarea manual) → correo con propuesta → seguimiento → llamada → cierre.
    /// </summary>
    private static async Task CampanasSantaCruzAsync(AppDbContext db)
    {
        var apps = await ServicioAsync(db, "Apps móviles y CRM",
            "Apps de pedidos, reservas o citas y CRM para ordenar clientes y ventas que hoy llegan por WhatsApp o Excel.");
        var correo = await ServicioAsync(db, "Correo corporativo con dominio propio",
            "Correo profesional con el dominio de su empresa (nombre@suempresa.com), configurado y administrado por TIBolivia.");
        await ServicioAsync(db, "Outsourcing de sistemas (área de TI)",
            "TIBolivia como el área de sistemas de su empresa por una cuota mensual fija: soporte, correo, web y desarrollo.");
        var logistica = await ServicioAsync(db, "Sistemas de logística",
            "Control de flota, rastreo de envíos con portal para clientes, almacenes e inventario, rutas y despacho.");

        const string firma = "<p>Saludos cordiales,<br>{{Comercial}} · TIBolivia<br>{{EmailComercial}} · {{TelefonoComercial}}</p>";
        var waPresentacion = await TareaAsync(db, "Santa Cruz 1 · WhatsApp personalizado (tarea)",
            "Enviar WhatsApp de presentación a {{Empresa}}",
            "Buenos días, le saluda {{Comercial}} de TIBolivia, empresa cruceña de desarrollo de software. Ayudamos a empresas como {{Empresa}} con {{Necesidad}}. ¿Le puedo enviar una propuesta corta o agendar 30 minutos de diagnóstico sin costo?");
        var waSeguimiento = await TareaAsync(db, "Santa Cruz 3 · WhatsApp de seguimiento (tarea)",
            "WhatsApp de seguimiento a {{Empresa}} con un dato útil",
            "Hola, le escribo nuevamente de TIBolivia. En empresas de su rubro, gran parte de los pedidos y consultas llegan por WhatsApp y se pierden en el chat; con {{Necesidad}} quedan ordenados y medibles. ¿Le muestro en 30 minutos cómo funcionaría en {{Empresa}}?");
        var llamada = await TareaAsync(db, "Santa Cruz 4 · Llamada a gerencia (tarea)",
            "Llamar a {{Empresa}} y pedir hablar con gerencia o administración",
            "Guion: \"Buenos días, soy {{Comercial}} de TIBolivia. Les enviamos una propuesta sobre {{Necesidad}}. ¿Con quién puedo coordinar un diagnóstico gratuito de 30 minutos?\" Registre el resultado y agende el siguiente paso.");

        await CampanaAsync(db, "Santa Cruz: diagnóstico gratuito (estrategia comercial)",
            "Cadencia de 3 semanas del plan comercial: WhatsApp personalizado, correo con propuesta, seguimiento, llamada y cierre. Para la base de Santa Cruz.",
            apps,
            [
                new(0, waPresentacion),
                new(1, await CorreoAsync(db, "Santa Cruz 2 · Correo con propuesta",
                    "{{Empresa}}: menos tiempo en WhatsApp y Excel, más ventas",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Soy {{Comercial}}, de <b>TIBolivia</b> (Santa Cruz). Desarrollamos aplicaciones móviles, sistemas a medida y CRM,
                    administramos correo corporativo con dominio propio y podemos ser el área de sistemas de su empresa por una cuota mensual fija.</p>
                    <p>Para {{Empresa}} vemos una oportunidad concreta: <b>{{Necesidad}}</b>.</p>
                    <p>Les ofrezco un <b>diagnóstico gratuito de 30 minutos</b>, presencial o por videollamada, para identificar qué procesos conviene automatizar primero.
                    Puede responder este correo con el día y la hora que le acomoden.</p>
                    """ + firma)),
                new(6, waSeguimiento),
                new(7, llamada),
                new(7, await CorreoAsync(db, "Santa Cruz 5 · Cierre del ciclo",
                    "¿Lo retomamos más adelante?",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Este es mi último mensaje por ahora para no llenar su bandeja. Si más adelante quieren revisar {{Necesidad}},
                    respondan este correo y coordinamos el diagnóstico gratuito.</p>
                    """ + firma)),
            ]);

        await CampanaAsync(db, "Logística: control de flota, envíos y almacenes",
            "Para transporte, courier, agencias aduaneras, almacenes y distribuidoras. WhatsApp personalizado, 2 correos, llamada y cierre.",
            logistica,
            [
                new(0, waPresentacion),
                new(1, await CorreoAsync(db, "Logística 2 · Presentación",
                    "{{Empresa}}: sus clientes preguntan \"¿dónde está mi envío?\"",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Soy {{Comercial}}, de <b>TIBolivia</b>. Desarrollamos <b>sistemas de logística a medida</b> para empresas de Santa Cruz:</p>
                    <ul>
                      <li><b>Rastreo de envíos</b> con portal o app para que el cliente consulte el estado sin llamar.</li>
                      <li><b>Control de flota</b>: viajes, choferes, mantenimiento y combustible.</li>
                      <li><b>Almacenes e inventario</b>: entradas, salidas y ubicación de la mercadería.</li>
                      <li><b>Despacho y rutas</b> con guías y comprobantes digitales.</li>
                    </ul>
                    <p>Para {{Empresa}} pensamos en: <b>{{Necesidad}}</b>. ¿Coordinamos un diagnóstico gratuito de 30 minutos?</p>
                    """ + firma)),
                new(5, await CorreoAsync(db, "Logística 3 · Cómo trabajamos",
                    "Un sistema de logística por etapas, sin parar su operación",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Sabemos que en logística no se puede detener la operación para cambiar de sistema. Por eso trabajamos <b>por etapas</b>:
                    primero lo que más tiempo les quita (por ejemplo, el rastreo para clientes), con capacitación al personal y soporte local en Santa Cruz.</p>
                    <p>¿Les parece si conversamos 30 minutos esta semana?</p>
                    """ + firma)),
                new(4, llamada),
                new(7, await CorreoAsync(db, "Logística 4 · Cierre del ciclo",
                    "Quedamos atentos, {{Empresa}}",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Cierro aquí mis mensajes para no molestarles. Cuando quieran ordenar envíos, flota o almacenes, respondan este correo y les atendemos.</p>
                    """ + firma)),
            ]);

        await CampanaAsync(db, "Correo corporativo (empresas que usan Gmail o Hotmail)",
            "Venta rápida de ticket bajo para empresas que atienden con correo gratuito. Puerta de entrada a soporte, web y CRM.",
            correo,
            [
                new(0, await CorreoAsync(db, "Correo corporativo 1 · Presentación",
                    "{{Empresa}}: su correo con el dominio de su empresa",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Vimos que atienden a sus clientes con un correo gratuito (Gmail o Hotmail). Un correo con su propio dominio
                    (por ejemplo, <b>ventas@suempresa.com</b>) transmite confianza, evita que sus mensajes lleguen a spam y deja las cuentas a nombre de la empresa,
                    no de una persona.</p>
                    <p>En <b>TIBolivia</b> lo configuramos y administramos por ustedes, incluida la migración de los correos actuales.
                    ¿Les envío una cotización?</p>
                    """ + firma)),
                new(2, waPresentacion),
                new(4, await CorreoAsync(db, "Correo corporativo 2 · Seguimiento",
                    "¿Le preparamos la cotización del correo corporativo?",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Solo para saber si les interesa la cotización del correo con su dominio. Si ya tienen dominio web, el cambio se hace en pocos días
                    y sin perder sus correos actuales.</p>
                    """ + firma)),
                new(5, llamada),
            ]);
    }

    private static async Task<Plantilla> TareaAsync(AppDbContext db, string nombre, string titulo, string texto) =>
        await db.Plantillas.FirstOrDefaultAsync(x => x.Nombre == nombre)
        ?? db.Plantillas.Local.FirstOrDefault(x => x.Nombre == nombre)
        ?? db.Plantillas.Add(new Plantilla { Nombre = nombre, Canal = Canal.Tarea, Asunto = titulo, Cuerpo = texto }).Entity;

    private static async Task<Servicio> ServicioAsync(AppDbContext db, string nombre, string descripcion)
    {
        var s = await db.Servicios.FirstOrDefaultAsync(x => x.Nombre == nombre) ?? db.Servicios.Local.FirstOrDefault(x => x.Nombre == nombre);
        if (s is not null) return s;
        s = new Servicio { Nombre = nombre, Descripcion = descripcion };
        db.Servicios.Add(s);
        return s;
    }

    private static async Task<Plantilla> CorreoAsync(AppDbContext db, string nombre, string asunto, string cuerpo) =>
        await db.Plantillas.FirstOrDefaultAsync(x => x.Nombre == nombre)
        ?? db.Plantillas.Local.FirstOrDefault(x => x.Nombre == nombre)
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
        if (await db.Campanas.AnyAsync(c => c.Nombre == nombre) || db.Campanas.Local.Any(c => c.Nombre == nombre)) return;
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
