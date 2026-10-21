# Puesta en marcha de la campaña comercial

## 1. Qué trae el sistema

### Prospectos (se cargan solos al iniciar)

| Lista | Empresas | Detalle |
|---|---|---|
| `01-documento-santa-cruz` | 61 | Del documento "Base de prospectos y estrategia comercial": 20 de prioridad A, 49 con correo. 8 tienen datos marcados para verificar |
| `02-web-logistica` | 22 | Transporte, courier, agencias aduaneras, almacenes, mudanzas, delivery |
| `03-web-empresas-sistemas` | 28 | Farmacias (7), supermercados, distribuidoras, seguridad, limpieza, veterinarias, ópticas, librerías, repuestos, electrodomésticos, industria |

- Todas quedan asignadas al administrador. Las listas 2 y 3 salen de búsquedas web: **todas están marcadas "por verificar"**.
  Confirme los datos (filtro **"Solo datos por verificar"**) antes de escribir.
- Ninguna tiene consentimiento de WhatsApp: el primer contacto por WhatsApp es **manual**, desde el celular del comercial
  (botón **"Abrir en WhatsApp"**). Cuando el prospecto responde, ya puede recibir mensajes del sistema.
- Las listas están en `src/TiBolivia.Prospeccion/Data/Listas/`. Para agregar una nueva lista, deje otro `.csv` en esa carpeta (se carga al reiniciar)
  o use **Prospectos → Importar CSV**.

### Campañas

| Campaña | Para quién | Secuencia |
|---|---|---|
| **Santa Cruz: diagnóstico gratuito** (principal) | Toda la base | Día 0 WhatsApp personalizado (tarea) · Día 1 correo con propuesta PDF · Día 7 WhatsApp de seguimiento (tarea) · Día 14 llamada a gerencia (tarea) · Día 21 correo de cierre |
| **Logística: control de flota, envíos y almacenes** | Rubro "Logística y transporte" | WhatsApp (tarea) · 2 correos · llamada · cierre |
| **Correo corporativo** | Empresas con Gmail/Hotmail | Correo · WhatsApp (tarea) · seguimiento · llamada |
| Farmacias / SIAT / Software a medida | Por rubro | Secuencias anteriores |

- Cada mensaje usa **{{Necesidad}}** ("qué ofrecerle" de cada empresa), así que no hay dos correos iguales.
- Si la empresa no responde al terminar la campaña, se agenda **"Retomar contacto" a los 3 meses**.
- Si responde (correo o WhatsApp), la secuencia se pausa y aparece en el **Panel → Respondieron y esperan atención**.

### Pantallas nuevas

| Pantalla | Para qué |
|---|---|
| **Mis tareas** | Agenda del día: tareas vencidas, de hoy y de los próximos 7 días (llamadas, WhatsApp, seguimientos) |
| **Embudo** | Empresas por etapa (Nuevo → Contactado → Interesado → Diagnóstico → Propuesta → Ganado) con el valor estimado en Bs |
| **Ficha del prospecto** | Prioridad, qué ofrecerle, próxima acción, **"Abrir en WhatsApp"** con el mensaje ya redactado, historial completo |

## 2. Antes de lanzar (una sola vez)

1. **Actualizar:** en el servidor, `sudo bash /opt/tibolivia/deploy/actualizar.sh`; en la PC, `git pull` y `dotnet run`.
2. **Propuesta en PDF:** en **Plantillas → "Santa Cruz 2 · Correo con propuesta"**, suba su propuesta comercial en PDF como adjunto.
3. **Su teléfono en la firma:** en **Usuarios**, cree a cada comercial con su teléfono (aparece en {{TelefonoComercial}}).
   Para el administrador, edítelo en la base o cree su propio usuario comercial.
4. **Revise los textos** de las plantillas "Santa Cruz", "Logística" y "Correo corporativo" (botón **Vista previa**).
5. **Correo:** menú **Correo → Enviar correo de prueba**.
6. **Reparta la base:** en **Prospectos**, filtre por rubro, seleccione y use **"Asignar a"** para cada comercial.

## 3. Lanzar (semana 1)

1. **Verifique los datos A primero:** Prospectos → Prioridad **A** → revise los ⚠ (datos por verificar) y corrija en **Editar**.
2. **Campañas:** active **"Santa Cruz: diagnóstico gratuito"** (y **"Logística"** para ese rubro).
3. **Inscriba 20 empresas por día**, empezando por la prioridad A:
   - Rubro "Logística y transporte" → campaña **Logística**.
   - Empresas con Gmail/Hotmail en las notas → campaña **Correo corporativo**.
   - El resto → campaña **Santa Cruz**.
4. Cada mañana, cada comercial abre **Mis tareas**:
   - **WhatsApp personalizado:** abra la empresa → **Abrir en WhatsApp** → revise y envíe → **Registrar que lo envié** →
     **Hecho y agendar la siguiente**.
   - **Llamada:** use el guion de la tarea, registre el resultado (llamada, reunión) y cambie el estado.
5. Cuando alguien acepta el diagnóstico, cámbielo a **"Diagnóstico agendado"**, ponga el **valor estimado** y agende la reunión como próxima acción.

## 4. Seguimiento semanal

| Meta semanal (del plan comercial) | Dónde se ve |
|---|---|
| 20 contactos nuevos (prioridad A primero) | Panel → enviados; Embudo → Contactado |
| 5 diagnósticos agendados | Embudo → Diagnóstico agendado |
| 2 propuestas enviadas | Embudo → Propuesta enviada (con valor en Bs) |
| 1 correo corporativo cerrado por semana, 1 app/CRM al mes | Embudo → Ganado |

Si una campaña tiene menos de 5 respuestas cada 100 empresas, cambie primero el **asunto** del primer correo.

## 5. Reglas para no quemar la base

- WhatsApp: **mensajes individuales y personalizados**, nunca masivos a quien no escribió primero.
- Empiece con **20–30 correos por día** y suba de a poco.
- Respete a quien pida no ser contactado (el sistema lo hace solo con "BAJA" o el enlace de baja).
- Las inconsistencias detectadas en las webs de los prospectos (correos mal configurados, WhatsApp que llevan a otro número)
  son **argumento de venta**: muestran lo que TIBolivia puede ordenar.
