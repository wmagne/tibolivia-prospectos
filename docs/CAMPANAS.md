# Campañas comerciales de TI Bolivia

El sistema trae **3 campañas listas** (menú **Campañas**). Se crean **inactivas** para que revise los textos antes de activarlas.

| Campaña | Servicio | Público | Secuencia |
|---|---|---|---|
| **Farmacias: sistema de gestión y facturación** | Sistema para farmacias | Dueños y administradores de farmacias y cadenas | 5 contactos en 12 días |
| **Comercios: facturación electrónica SIAT** | Facturación electrónica SIAT | Comercios, distribuidoras, ferreterías, servicios | 4 contactos en 10 días |
| **Empresas: software a medida** | Desarrollo de software a medida | Medianas empresas con procesos en Excel o papel | 4 contactos en 12 días |

## 1. Farmacias (campaña principal)

Es la oferta más fuerte porque TI Bolivia **ya tiene el producto funcionando**: ventas con factura SIAT, alertas de vencimiento y stock mínimo, varias sucursales con traspasos y kardex, compras a proveedores, caja con cobro QR, reportes de ganancias e inventario, y recordatorios de cumpleaños.

| Día | Canal | Mensaje | Objetivo |
|---|---|---|---|
| 0 | Correo | **Presentación**: vencimientos, stock descuadrado, facturación lenta | Que reconozca su problema |
| 2 | WhatsApp | Seguimiento corto e invitación a una demostración de 20 min | Respuesta rápida |
| 4 | Correo | **Funciones y beneficios** en una tabla | Mostrar que el sistema está completo |
| 7 | Correo | **Invitación a demo con sus propios productos**, migración y capacitación | Quitar el miedo al cambio |
| 12 | Correo | **Último contacto**, puerta abierta | Cerrar sin presionar |

**Dónde conseguir prospectos:** directorio de farmacias del Colegio de Bioquímica y Farmacia y de las asociaciones de propietarios de farmacias, Google Maps ("farmacia" + ciudad), ferias de salud, y referidos de laboratorios y distribuidoras.

## 2. Comercios – facturación electrónica SIAT

Para empresas obligadas a facturar en línea. El argumento es el **cumplimiento sin complicaciones**, más el orden en ventas, caja e inventario.

**Dónde conseguir prospectos:** cámaras de comercio e industria, asociaciones de distribuidores, contadores que atienden a varias empresas (son un excelente canal de referidos).

## 3. Software a medida

Para empresas medianas. El argumento es: "lo que hoy hace en Excel, automatizado y por etapas, con precio cerrado por etapa".

**Dónde conseguir prospectos:** LinkedIn (gerentes generales y de operaciones), cámaras empresariales, clientes actuales de TI Bolivia que tengan otras necesidades.

## Plantillas de WhatsApp para enviar a Meta

Créelas en **WhatsApp Manager → Plantillas de mensajes → Crear**, categoría **Marketing**, idioma **Español (es)**. El **nombre** debe ser exactamente el indicado, porque el sistema lo busca así.

**`farmacia_seguimiento`** (1 variable)
```
Hola {{1}}, le escribimos de TI Bolivia. Le enviamos un correo sobre nuestro sistema para farmacias: alertas de vencimiento, stock por sucursal y facturación SIAT. ¿Le gustaría ver una demostración de 20 minutos? Responda BAJA si no desea recibir más mensajes.
```
Ejemplo: {{1}} = Carlos

**`siat_seguimiento`** (2 variables)
```
Hola {{1}}, le escribimos de TI Bolivia sobre la facturación electrónica para {{2}}. ¿Tiene 15 minutos esta semana para resolver sus dudas sin compromiso? Responda BAJA si no desea recibir más mensajes.
```
Ejemplo: {{1}} = Ana, {{2}} = Distribuidora Sur

**`software_seguimiento`** (2 variables)
```
Hola {{1}}, de TI Bolivia. ¿Hay algún proceso en {{2}} que hoy se haga en Excel o papel y le gustaría automatizar? Podemos conversarlo sin compromiso. Responda BAJA si no desea recibir más mensajes.
```
Ejemplo: {{1}} = Jorge, {{2}} = Importadora Andina

Sugerencia: agregue dos **botones de respuesta rápida**: "Sí, me interesa" y "Ahora no". Así responder le cuesta un toque al prospecto, y el sistema lo marca como *Interesado*.

## Cómo lanzar una campaña (paso a paso)

1. **Servicios:** revise que la descripción de cada servicio sea la correcta.
2. **Plantillas:** abra cada plantilla, ajuste lo que quiera y use **Vista previa**.
3. **Prospectos → Importar CSV:** suba la lista del segmento y complete la columna **Rubro** (ej. "Farmacia") para poder filtrar.
4. **Asigne** los prospectos a cada comercial: su nombre y correo aparecen como firma.
5. **Campañas:** active la campaña.
6. **Prospectos:** filtre por rubro o ciudad, seleccione y pulse **Inscribir en campaña**.
7. Revise el **Panel** cada mañana: quienes respondieron aparecen en "Respondieron y esperan atención". **Llámelos el mismo día.**

## Metas sugeridas (por cada 100 prospectos)

| Indicador | Meta |
|---|---|
| Correos entregados | más de 95 |
| Respuestas (correo o WhatsApp) | 8 a 15 |
| Demostraciones agendadas | 3 a 6 |
| Propuestas enviadas | 2 a 4 |
| Ventas | 1 a 2 |

Si las respuestas quedan por debajo de 5 de cada 100, cambie el **asunto** del primer correo antes de cambiar el resto. Es lo que más influye.

## Buenas prácticas

- Empiece con **30 a 50 prospectos por día** para cuidar la reputación del correo.
- Escriba solo a empresas donde el servicio **tenga sentido** (farmacias para la campaña de farmacias, etc.).
- Por WhatsApp, solo a quien dio su **consentimiento** (opt-in).
- Respete siempre a quien pida la **baja**: el sistema lo hace solo.
- **Antes de activar**, confirme con su equipo lo que se promete en los textos: la demostración sin costo, la migración de datos, la capacitación y la propuesta con precio cerrado por etapa.
