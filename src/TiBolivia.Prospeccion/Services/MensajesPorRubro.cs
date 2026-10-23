namespace TiBolivia.Prospeccion.Services;

/// <summary>Argumento de venta de TIBolivia para cada rubro: titular, párrafo y tres beneficios.</summary>
public record MensajeRubro(string Titular, string Parrafo, string[] Beneficios);

public static class MensajesPorRubro
{
    private static readonly MensajeRubro General = new(
        "Menos tiempo en WhatsApp y Excel, más ventas",
        "Ayudamos a empresas de Santa Cruz a ordenar sus ventas, clientes y operaciones con sistemas hechos a su medida.",
        ["Sistemas a medida y CRM para seguir a cada cliente", "Apps móviles de pedidos, reservas o citas", "Correo corporativo con su propio dominio"]);

    // Clave: inicio del rubro del prospecto (en minúsculas, sin tildes).
    private static readonly (string Clave, MensajeRubro Mensaje)[] Mensajes =
    [
        ("salud", new("Más pacientes atendidos, menos tiempo en WhatsApp y papel",
            "Laboratorios, clínicas y consultorios de Santa Cruz están dejando de coordinar citas y entregar resultados por WhatsApp y papel.",
            ["Agenda de citas en línea con recordatorios automáticos", "Entrega de resultados por app o portal seguro", "Historial de pacientes y CRM para fidelizar"])),
        ("educacion", new("Admisiones y comunicación con las familias, en un solo lugar",
            "Colegios e institutos que ordenan sus admisiones y su comunicación con las familias ganan inscripciones y tiempo.",
            ["CRM de admisiones: seguimiento de cada familia interesada", "App o portal para padres: avisos, notas y pagos", "Correo institucional con su dominio .edu.bo"])),
        ("inmobiliaria", new("Cada interesado registrado, cada venta con seguimiento",
            "En inmobiliarias y constructoras, cada consulta que no se registra es una venta que se pierde.",
            ["CRM para agentes y vendedores, con seguimiento de cada interesado", "Portal o app de propiedades con su marca", "Control de cobranza de cuotas y avance de obras"])),
        ("distribucion", new("Pedidos de sus clientes y vendedores, en tiempo real",
            "Distribuidoras e importadoras que llevan sus pedidos en papel o WhatsApp pierden tiempo y ventas cada día.",
            ["App de preventa para sus vendedores en calle", "Portal de pedidos para clientes y distribuidores", "Inventario y facturación SIAT conectados"])),
        ("logistica", new("Sus clientes preguntan: ¿dónde está mi envío?",
            "En transporte, courier y comercio exterior, la información en línea es hoy parte del servicio que su cliente espera.",
            ["Rastreo de envíos con portal o app para sus clientes", "Control de flota, choferes, mantenimiento y combustible", "Almacenes y despacho con guías y comprobantes digitales"])),
        ("gastronomia", new("Pedidos y reservas propios, sin depender de terceros",
            "Restaurantes, hoteles y servicios que reciben pedidos y reservas por sus propios canales ganan clientes frecuentes.",
            ["App o web de pedidos y reservas con su marca", "Fidelización: clientes frecuentes, promociones y cumpleaños", "Agenda y órdenes de trabajo para su equipo"])),
        ("farmacias", new("Menos vencimientos y facturación SIAT sin complicaciones",
            "Tenemos un sistema para farmacias funcionando, pensado para el día a día de mostrador y sucursales.",
            ["Alertas de productos por vencer y de stock mínimo", "Ventas con facturación electrónica SIAT", "Inventario por sucursal, compras y kardex"])),
        ("comercio", new("Venda más con su inventario y facturación al día",
            "Los comercios que tienen su inventario y sus ventas en un solo sistema venden más y pierden menos.",
            ["Punto de venta con facturación electrónica SIAT", "Inventario y compras en un solo sistema", "Catálogo en línea y pedidos por WhatsApp ordenados"])),
        ("servicios empresariales", new("Su equipo organizado y sus clientes bien atendidos",
            "Las empresas de servicios que ordenan contratos, personal y clientes en un sistema crecen sin perder el control.",
            ["CRM de clientes y contratos", "Control de personal, turnos y asistencia", "Correo corporativo con su dominio"])),
        ("automotriz", new("Repuestos, taller y clientes bajo control",
            "En repuestos y talleres, encontrar la pieza correcta y avisar al cliente a tiempo hace la diferencia.",
            ["Inventario de repuestos con códigos y compatibilidades", "Órdenes de trabajo e historial de cada vehículo", "Citas y recordatorios para sus clientes"])),
        ("industria", new("Producción, inventario y ventas conectados",
            "Las industrias que conectan producción, inventario y ventas en un sistema reducen pérdidas y cumplen mejor sus entregas.",
            ["Control de producción y órdenes de fabricación", "Inventario de insumos y producto terminado", "Cotizaciones y ventas con facturación SIAT"])),
    ];

    public static MensajeRubro Para(string? rubro)
    {
        var r = Normalizar(rubro);
        return Mensajes.FirstOrDefault(m => r.StartsWith(m.Clave)).Mensaje ?? General;
    }

    private static string Normalizar(string? s) =>
        new string((s ?? "").ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
}
