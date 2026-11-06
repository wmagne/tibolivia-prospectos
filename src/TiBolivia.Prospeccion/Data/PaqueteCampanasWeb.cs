using TiBolivia.Prospeccion.Domain;

namespace TiBolivia.Prospeccion.Data;

/// <summary>
/// Campañas de los productos del sitio web nuevo (tibolivia.com, noviembre de 2026): facturación SIAT desde Bs 79,
/// cobros con QR confirmados por el banco, apps de pedidos y delivery, asistente con IA para la web, y un correo
/// de "novedades" para empresas que ya recibieron una campaña y no respondieron.
/// Cada correo lleva a la página del producto, donde el asistente con IA del sitio atiende las 24 horas y avisa a
/// gerencia de cada interesado: así el primer contacto lo cierra el agente del sitio y el comercial recibe al cliente listo.
/// Las reglas de inscripción están en appsettings.json (Campanas:OtrasAutoInscripciones).
/// </summary>
public static partial class PaqueteCampanas
{
    private const string Sitio = "https://tibolivia.com";

    private static string BotonSitio(string ruta, string texto) =>
        $"<p style=\"margin:18px 0;\"><a href=\"{Sitio}{ruta}\" style=\"display:inline-block;background:#00a8a8;color:#ffffff;text-decoration:none;" +
        $"font-weight:bold;padding:12px 20px;border-radius:8px;\">{texto}</a></p>";

    private static async Task ServiciosDelSitioWebAsync(AppDbContext db)
    {
        const string firma = "<p style=\"margin-top:20px;\">Saludos cordiales,<br><b>{{Comercial}}</b> · TIBolivia<br>{{EmailComercial}} · {{TelefonoComercial}}</p>";
        const string titulo = "<h2 style=\"color:#0d47a1;font-size:22px;margin:0 0 16px;\">";
        const string destacado = "<p style=\"background:#e8f1fb;border-left:4px solid #1565c0;padding:12px 16px;border-radius:4px;\">";

        // ---------- Facturación en línea SIAT ----------
        var facturacion = await ServicioAsync(db, "Facturación en línea SIAT (planes desde Bs 79)",
            "Facturación computarizada o electrónica en línea: CUIS/CUFD y catálogos automáticos, contingencia si el SIAT se cae, " +
            "PDF carta o rollo, varias sucursales y API. Planes mensuales por NIT desde Bs 79 con alta incluida.");
        await CampanaAsync(db, "Facturación SIAT desde Bs 79 (sitio web)",
            "Para comercios, ferreterías, librerías, distribuidoras e importadoras. Correo con planes y enlace a la página de facturación, " +
            "WhatsApp (tarea), seguimiento a los 3 días y llamada a los 5.",
            facturacion,
            [
                new(0, await CorreoAsync(db, "Facturación web 1 · Planes desde Bs 79",
                    "{{Empresa}}: facture en línea desde Bs 79 al mes, con el alta incluida",
                    titulo + "Facture en segundos, aunque el SIAT se caiga</h2>" + """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Soy {{Comercial}}, de <b>TIBolivia</b>. Ofrecemos <b>facturación en línea del SIAT</b> (computarizada o electrónica) desde su computadora o celular:</p>
                    <ul>
                      <li>El sistema pide y renueva solo el <b>CUIS y el CUFD</b> y sincroniza los catálogos.</li>
                      <li><b>Contingencia automática</b>: si el SIAT o internet fallan, sigue facturando y envía todo al volver.</li>
                      <li>PDF en hoja carta o rollo, enviado al cliente por correo. Varias sucursales y API para su sistema actual.</li>
                    </ul>
                    """ + destacado + """
                    Planes por NIT: <b>Emprendedor Bs 79</b> (300 facturas) · <b>Negocio Bs 149</b> (1.500) · <b>Empresa Bs 299</b> (5.000, electrónica y API) · <b>Ilimitado Bs 499</b>.
                    El alta, las pruebas piloto y la capacitación están incluidas.</p>
                    """ + BotonSitio("/facturacion/", "Ver planes y calcular el mío") + """
                    <p style="font-size:14px;color:#6b7280;">En la página hay un asistente que responde sus dudas al momento, a cualquier hora.</p>
                    {{BotonesContacto}}
                    """ + firma)),
                new(0, await TareaAsync(db, "Facturación web 2 · WhatsApp (tarea)",
                    "Enviar WhatsApp a {{Empresa}} (facturación SIAT desde Bs 79)",
                    "Buenos días, le saluda {{Comercial}} de TIBolivia. Hacemos la facturación en línea del SIAT desde Bs 79 al mes, con el alta incluida, y si el SIAT se cae usted sigue facturando. Aquí puede ver los planes: https://tibolivia.com/facturacion/ ¿Cuántas facturas emiten al mes? Le digo qué plan le conviene.")),
                new(3, await CorreoAsync(db, "Facturación web 3 · Seguimiento",
                    "Re: {{Empresa}}: facture en línea desde Bs 79 al mes",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Retomo mi correo sobre la facturación en línea. Si me dicen cuántas facturas emiten al mes, les respondo con el plan que les conviene y el costo por factura.
                    Si ya trabajan con otro proveedor con rutas compatibles, normalmente basta con cambiar la dirección y la clave.</p>
                    """ + BotonSitio("/facturacion/", "Calcular mi plan") + """
                    <p>Si no es el momento, me lo indican y no les vuelvo a escribir.</p>
                    {{BotonesContacto}}
                    """ + firma)),
                new(2, await TareaAsync(db, "Facturación web 4 · Llamada (tarea)",
                    "Llamar a {{Empresa}} por facturación SIAT",
                    "Guion: \"Buenos días, soy {{Comercial}} de TIBolivia. Les escribimos sobre la facturación en línea desde Bs 79 al mes. ¿Con quién veo el tema de facturación? Pregunte cuántas facturas emiten al mes y qué sistema usan hoy, y ofrezca el diagnóstico sin costo.\"")),
            ], activa: true);

        // ---------- Cobros con QR ----------
        var qr = await ServicioAsync(db, "Cobros con QR confirmados por el banco",
            "Un QR de un solo uso por venta con el monto exacto; el sistema confirma el pago con el banco (no con capturas) y cierra la venta, " +
            "con la factura si tiene facturación. El dinero va directo a la cuenta del comercio.");
        await CampanaAsync(db, "Cobros con QR confirmados por el banco (sitio web)",
            "Para restaurantes, cafeterías, tiendas y ventas por redes. Correo con enlace a la página de cobros QR, WhatsApp (tarea), " +
            "seguimiento a los 3 días y llamada a los 5.",
            qr,
            [
                new(0, await CorreoAsync(db, "Cobros QR web 1 · Presentación",
                    "{{Empresa}}: ¿y si el pago QR de la captura es falso?",
                    titulo + "Cobre con QR y sepa al instante si el pago es real</h2>" + """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Soy {{Comercial}}, de <b>TIBolivia</b>. Una captura de pago se edita en segundos. Con nuestro sistema de <b>cobros con QR</b>:</p>
                    <ul>
                      <li>Cada venta tiene su <b>QR de un solo uso</b> con el monto exacto.</li>
                      <li>El sistema <b>confirma el pago con el banco</b> y cierra la venta solo, con la factura si la tiene.</li>
                      <li>El dinero llega <b>directo a su cuenta</b>: TIBolivia no toca su plata.</li>
                      <li>Sirve en mostrador, delivery y ventas por WhatsApp o redes.</li>
                    </ul>
                    """ + destacado + "Le mostramos el cobro funcionando con un caso como el suyo. <b>Demo y configuración sin costo.</b></p>"
                    + BotonSitio("/cobros-qr/", "Ver cómo funciona") + """
                    {{BotonesContacto}}
                    """ + firma)),
                new(0, await TareaAsync(db, "Cobros QR web 2 · WhatsApp (tarea)",
                    "Enviar WhatsApp a {{Empresa}} (cobros con QR)",
                    "Buenos días, le saluda {{Comercial}} de TIBolivia. ¿Le ha pasado que le muestran una captura de pago QR y no sabe si es real? Nuestro sistema genera un QR por venta y confirma el pago directamente con el banco antes de entregar. El dinero va directo a su cuenta. Mire cómo funciona: https://tibolivia.com/cobros-qr/ ¿Le muestro una demo sin costo?")),
                new(3, await CorreoAsync(db, "Cobros QR web 3 · Seguimiento",
                    "Re: {{Empresa}}: cobros con QR confirmados por el banco",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Retomo mi correo sobre los cobros con QR. Los pagos confirmados entran solos al cierre de caja, separados del efectivo y la tarjeta, y se acaban los pagos falsos.
                    Si les interesa, coordinamos una demo de 15 minutos; si no es el momento, me lo indican y no les vuelvo a escribir.</p>
                    {{BotonesContacto}}
                    """ + firma)),
                new(2, await TareaAsync(db, "Cobros QR web 4 · Llamada (tarea)",
                    "Llamar a {{Empresa}} por cobros con QR",
                    "Guion: \"Buenos días, soy {{Comercial}} de TIBolivia. Les escribimos sobre un sistema que confirma con el banco cada pago QR antes de entregar el pedido, para evitar capturas falsas. ¿Cobran hoy con QR? ¿Con qué banco?\" Ofrezca la demo sin costo.")),
            ], activa: true);

        // ---------- Apps de pedidos y delivery ----------
        var delivery = await ServicioAsync(db, "App propia de pedidos y delivery",
            "App de clientes (catálogo, carrito, pedido y seguimiento del repartidor en el mapa), app de repartidores y panel web para el mostrador. " +
            "TIBolivia la publica en Google Play. Proyecto con precio cerrado; se integra con cobros QR y facturación.");
        await CampanaAsync(db, "Apps de pedidos y delivery (sitio web)",
            "Para negocios con delivery propio: restaurantes, licorerías, farmacias, mensajería y courier. Correo, WhatsApp (tarea), " +
            "seguimiento a los 4 días y llamada a los 6.",
            delivery,
            [
                new(0, await CorreoAsync(db, "Delivery web 1 · Presentación",
                    "{{Empresa}}: su propia app de pedidos, sin comisiones por pedido",
                    titulo + "Sus pedidos ordenados y su cliente viendo al repartidor en el mapa</h2>" + """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Soy {{Comercial}}, de <b>TIBolivia</b>. Si hoy los pedidos llegan desordenados por WhatsApp, le hacemos <b>su propia app de pedidos</b>, con su marca:</p>
                    <ul>
                      <li><b>App de clientes</b>: catálogo, carrito y seguimiento del repartidor en vivo en el mapa.</li>
                      <li><b>App de repartidores</b>: toman el pedido, abren la ruta en Google Maps y cierran la entrega.</li>
                      <li><b>Panel web</b> para el mostrador: pedidos, stock y repartidores en un solo lugar.</li>
                      <li>Cobro con <b>QR confirmado por el banco</b> antes de despachar.</li>
                    </ul>
                    """ + destacado + "Ya tenemos una app de delivery <b>publicada en Google Play y en uso</b> en Santa Cruz. La publicamos por usted, con <b>precio cerrado</b> y sin comisión por pedido.</p>"
                    + BotonSitio("/#productos", "Ver nuestros productos") + """
                    {{BotonesContacto}}
                    """ + firma)),
                new(0, await TareaAsync(db, "Delivery web 2 · WhatsApp (tarea)",
                    "Enviar WhatsApp a {{Empresa}} (app propia de delivery)",
                    "Buenos días, le saluda {{Comercial}} de TIBolivia. Hacemos apps de pedidos con la marca de su negocio: su cliente pide desde el celular, paga con QR confirmado por el banco y sigue al repartidor en el mapa. Sin comisión por pedido y con precio cerrado; ya tenemos una funcionando en Google Play. ¿Le muestro la demo en su celular?")),
                new(4, await CorreoAsync(db, "Delivery web 3 · Seguimiento",
                    "Re: {{Empresa}}: su propia app de pedidos",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Retomo mi correo sobre la app propia de pedidos. Con una app de terceros cada pedido paga comisión; con la suya, el cliente y sus datos quedan en su negocio.
                    Le puedo mostrar la app en su celular en 15 minutos. Si no es el momento, me lo indica y no le vuelvo a escribir.</p>
                    {{BotonesContacto}}
                    """ + firma)),
                new(2, await TareaAsync(db, "Delivery web 4 · Llamada (tarea)",
                    "Llamar a {{Empresa}} por la app de delivery",
                    "Guion: \"Buenos días, soy {{Comercial}} de TIBolivia. Les escribimos sobre una app de pedidos con su marca, con seguimiento del repartidor y cobro QR. ¿Cuántos pedidos de delivery hacen por día y cómo los reciben hoy?\" Ofrezca la demo en el celular.")),
            ], activa: true);

        // ---------- Asistente con IA para el sitio web ----------
        var ia = await ServicioAsync(db, "Asistente con IA para su sitio web",
            "Un vendedor virtual que atiende el sitio web las 24 horas, responde con los productos y precios de la empresa y avisa por correo cada interesado. " +
            "Instalación y cuota mensual.");
        await CampanaAsync(db, "Asistente con IA para su web (sitio web)",
            "Para empresas con sitio web propio. Correo que invita a probar el asistente de tibolivia.com, seguimiento a los 4 días y llamada a los 6.",
            ia,
            [
                new(0, await CorreoAsync(db, "IA web 1 · Presentación",
                    "{{Empresa}}: un vendedor que atiende su web las 24 horas",
                    titulo + "Su sitio web, atendiendo clientes mientras usted duerme</h2>" + """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Soy {{Comercial}}, de <b>TIBolivia</b>. Vimos que {{Empresa}} tiene sitio web. Le instalamos un <b>asistente con inteligencia artificial</b> que:</p>
                    <ul>
                      <li>Atiende a quien entra a su web <b>las 24 horas</b>, en español y con naturalidad.</li>
                      <li>Responde con <b>sus productos, servicios y precios</b>: lo entrenamos con su información.</li>
                      <li>Le <b>avisa por correo cada interesado</b> con sus datos, para que su equipo cierre la venta.</li>
                    </ul>
                    """ + destacado + "<b>Pruébelo usted mismo:</b> el chat de nuestra página es uno de estos asistentes. Pregúntele lo que quiera.</p>"
                    + BotonSitio("/", "Probar el asistente en tibolivia.com") + """
                    {{BotonesContacto}}
                    """ + firma)),
                new(4, await CorreoAsync(db, "IA web 2 · Seguimiento",
                    "Re: {{Empresa}}: un vendedor que atiende su web las 24 horas",
                    """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Retomo mi correo sobre el asistente con IA. La mayoría de las visitas a un sitio web se van sin dejar sus datos; el asistente conversa con ellas y le pasa los interesados.
                    Si me envían el enlace de su web, les preparo una demo con su propia información, sin costo. Si no es el momento, me lo indican y no les vuelvo a escribir.</p>
                    {{BotonesContacto}}
                    """ + firma)),
                new(2, await TareaAsync(db, "IA web 3 · Llamada (tarea)",
                    "Llamar a {{Empresa}} por el asistente con IA para su web",
                    "Guion: \"Buenos días, soy {{Comercial}} de TIBolivia. Les escribimos sobre un asistente con inteligencia artificial que atiende su sitio web las 24 horas y les pasa los interesados. ¿Quién ve el sitio web y las consultas que llegan por ahí?\" Ofrezca una demo con su propia información.")),
            ], activa: true);

        await WhatsAppApiAsync(db, facturacion, qr, delivery);

        // ---------- Novedades para quienes ya recibieron una campaña y no respondieron ----------
        var novedades = await ServicioAsync(db, "Venda, cobre y facture desde un solo lugar",
            "Oferta conjunta del sitio web: facturación SIAT, cobros con QR, sistema de ventas e inventario, apps de delivery, asistente con IA y área de sistemas.");
        await CampanaAsync(db, "Novedades TIBolivia: venda, cobre y facture (recontacto)",
            "Un solo correo con los productos nuevos del sitio web, para empresas que recibieron una campaña hace más de 21 días y no respondieron. " +
            "Si responden, se trabajan como interesados.",
            novedades,
            [
                new(0, await CorreoAsync(db, "Novedades 1 · Productos nuevos",
                    "Novedades de TIBolivia para {{Empresa}}: facturación desde Bs 79 y cobros QR seguros",
                    titulo + "Venda, cobre y facture desde un solo lugar</h2>" + """
                    <p>Estimados de {{Empresa}}:</p>
                    <p>Hace unas semanas les escribimos desde <b>TIBolivia</b>. Desde entonces lanzamos productos que pueden empezar a usar sin un proyecto grande:</p>
                    <table role="presentation" cellpadding="0" cellspacing="0" style="width:100%;border-collapse:collapse;font-size:15px;margin:8px 0 16px;">
                      <tr><td style="padding:8px 0;border-bottom:1px solid #e3e8f0;"><b>🧾 Facturación en línea SIAT</b><br>Desde Bs 79 al mes, con el alta incluida y contingencia si el SIAT se cae.</td></tr>
                      <tr><td style="padding:8px 0;border-bottom:1px solid #e3e8f0;"><b>📱 Cobros con QR confirmados por el banco</b><br>Se acabaron las capturas falsas: la venta se cierra cuando el banco confirma.</td></tr>
                      <tr><td style="padding:8px 0;border-bottom:1px solid #e3e8f0;"><b>🛒 Sistema de ventas e inventario</b><br>Caja, kárdex, compras, crédito y reportes, conectado a factura y QR.</td></tr>
                      <tr><td style="padding:8px 0;border-bottom:1px solid #e3e8f0;"><b>🛵 App propia de pedidos y delivery</b><br>Con su marca y el repartidor en el mapa. Precio cerrado.</td></tr>
                      <tr><td style="padding:8px 0;"><b>🤖 Asistente con IA para su web</b><br>Atiende su sitio las 24 horas y le pasa los interesados.</td></tr>
                    </table>
                    """ + BotonSitio("/", "Ver todo en tibolivia.com") + """
                    <p>Respondan este correo con el producto que les interesa y les enviamos el detalle. Si no desean recibir más correos, usen el enlace de baja al pie.</p>
                    """ + firma)),
            ], activa: true);
    }

    /// <summary>
    /// Primer contacto por WhatsApp desde el número oficial (API de Meta), autorizado por el dueño el 06/11/2026.
    /// Un solo mensaje por empresa, con plantilla de marketing aprobada y botones "Sí, me interesa" / "Ahora no":
    /// quien toca "Ahora no" queda dado de baja, así el número no acumula bloqueos y conserva su calidad.
    /// Las campañas no envían nada hasta que Meta aprueba su plantilla (ver InscripcionAutomatica).
    /// </summary>
    private static async Task WhatsAppApiAsync(AppDbContext db, Servicio facturacion, Servicio qr, Servicio delivery)
    {
        var general = await ServicioAsync(db, "Venda, cobre y facture desde un solo lugar", "");
        var salud = await ServicioAsync(db, "Salud digital para consultorios",
            "Agenda de citas con recordatorios por WhatsApp, historia clínica digital y cobro con QR con factura, con 60 días de prueba.");

        (string Campana, string Descripcion, Servicio Servicio, string Plantilla, string Meta, string Texto)[] defs =
        [
            ("WhatsApp API: presentación general", "Empresas con celular sin otra campaña en curso.", general,
                "WhatsApp API · Presentación general", "tibolivia_presentacion",
                "Hola, le saluda TIBolivia, empresa de software de Santa Cruz. Ayudamos a negocios como {{Empresa}} a vender, cobrar y facturar desde un solo lugar: " +
                "facturación en línea del SIAT desde Bs 79 al mes, cobros con QR confirmados por el banco y sistema de ventas e inventario. " +
                "¿Le gustaría ver una demo gratuita de 15 minutos? Responda con uno de los botones."),
            ("WhatsApp API: cobros con QR", "Restaurantes, cafeterías y tiendas con celular.", qr,
                "WhatsApp API · Cobros con QR", "tibolivia_cobros_qr",
                "Hola, le escribe TIBolivia. ¿En {{Empresa}} alguna vez les mostraron una captura de pago QR que no era real? " +
                "Nuestro sistema genera un QR por venta y confirma el pago directamente con el banco antes de entregar el pedido. " +
                "El dinero llega a su cuenta y la venta se cierra sola. ¿Le mostramos cómo funciona en una demo gratuita?"),
            ("WhatsApp API: apps de delivery", "Negocios con delivery, licorerías, courier y mensajería.", delivery,
                "WhatsApp API · Apps de delivery", "tibolivia_delivery",
                "Hola, le escribe TIBolivia. Creamos apps de pedidos con la marca de su negocio: sus clientes piden desde el celular, pagan con QR " +
                "y siguen al repartidor en el mapa, sin comisión por pedido. Ya tenemos una funcionando en Google Play. " +
                "¿Le gustaría ver esta semana la demo pensada para {{Empresa}} y su equipo de reparto?"),
            ("WhatsApp API: facturación SIAT", "Comercios, ferreterías, librerías, distribuidoras e importadoras.", facturacion,
                "WhatsApp API · Facturación SIAT", "tibolivia_facturacion",
                "Hola, le escribe TIBolivia. Ofrecemos facturación en línea del SIAT para {{Empresa}} desde Bs 79 al mes, con el alta incluida " +
                "y sin cortes: si el SIAT se cae, usted sigue facturando. ¿Le enviamos el plan que le conviene según cuántas facturas emite al mes?"),
            ("WhatsApp API: salud digital", "Clínicas, consultorios, odontologías, laboratorios y centros de estética.", salud,
                "WhatsApp API · Salud digital", "tibolivia_salud",
                "Hola, le saluda TIBolivia. Para {{Empresa}} tenemos agenda de citas con recordatorios automáticos por WhatsApp, historia clínica digital " +
                "y cobro con QR con factura. Ofrecemos 60 días de prueba sin costo. ¿Le interesa ver una demo de 15 minutos?"),
        ];
        foreach (var d in defs)
            await CampanaAsync(db, d.Campana, d.Descripcion + " Un mensaje con plantilla aprobada desde el WhatsApp oficial; si responde, se avisa al comercial.",
                d.Servicio, [new(0, await WhatsAppAsync(db, d.Plantilla, d.Texto, d.Meta, "{{Empresa}}"))], activa: true);
    }
}
