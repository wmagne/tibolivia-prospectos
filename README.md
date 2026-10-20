# Sistema de Prospección – TI Bolivia

Sistema para que el área comercial de **TI Bolivia** envíe propuestas de servicios a prospectos de clientes,
por **correo** y **WhatsApp**, en **secuencias automáticas a lo largo del tiempo** (campañas), con respuestas
de WhatsApp atendidas por el **chatbot de la página de TI Bolivia**.

Tecnología: **ASP.NET Core (.NET 10 LTS) + PostgreSQL**, Razor Pages + Bootstrap, EF Core, MailKit, WhatsApp Cloud API (Meta).

---

## 1. Qué hace

| Módulo | Descripción |
|---|---|
| **Prospectos** | Lista con filtros (estado, ciudad, rubro, comercial), alta/edición, importación CSV con eliminación de duplicados, asignación a comerciales, inscripción masiva en campañas. |
| **Ficha del prospecto** | Historial completo (correos, WhatsApp enviados/recibidos, llamadas, reuniones, notas), cambio de estado del embudo, pausar/reanudar secuencias, responder por WhatsApp. |
| **Campañas** | Secuencias de pasos: *día 0 correo de presentación → día 2 WhatsApp → día 5 caso de éxito → día 10 último contacto*. Cada paso usa una plantilla y espera N días. |
| **Plantillas** | Correos HTML y mensajes de WhatsApp con variables: `{{Nombre}}`, `{{Empresa}}`, `{{Servicio}}`, `{{Comercial}}`, `{{LinkBaja}}`… con vista previa. |
| **Servicios** | Catálogo de servicios de TI Bolivia que promociona cada campaña. |
| **Motor de envíos** | Proceso en segundo plano que envía cada paso cuando corresponde, **solo en horario laboral de Bolivia** (lun–vie 8:00–18:00), con reintentos. |
| **Webhook de WhatsApp** | Recibe respuestas y estados (entregado/leído). Cuando un prospecto responde: se **pausa su secuencia**, pasa a *Interesado*, el mensaje se envía al **chatbot** y su respuesta vuelve por WhatsApp. Si escribe "BAJA" deja de recibir mensajes. |
| **Panel** | KPIs (enviados, respuestas, fallidos), embudo por estado, prospectos que respondieron y esperan atención. |
| **Usuarios y roles** | **Admin** (configura campañas, plantillas, servicios, usuarios) y **Comercial** (gestiona prospectos). |
| **Baja (opt-out)** | Enlace de baja en cada correo + cabecera `List-Unsubscribe` de un clic (exigida por Gmail/Outlook para envíos masivos). |

## 2. Qué necesita para ponerlo en producción

### 2.1 Infraestructura
- Servidor Windows (IIS) o Linux con el **runtime de ASP.NET Core 10**.
- **PostgreSQL 14+**.
- Un **dominio con HTTPS** (ej. `prospectos.tibolivia.com`). Meta exige HTTPS para el webhook de WhatsApp.

### 2.2 Correo (servidor propio `mail.tibolivia.com`)
Ya viene configurado en `appsettings.json` para la casilla **comercial@tibolivia.com**:

| | Servidor | Puerto | Seguridad |
|---|---|---|---|
| Envío (SMTP) | mail.tibolivia.com | 465 | SSL/TLS |
| Lectura de respuestas (IMAP) | mail.tibolivia.com | 993 | SSL/TLS |

Solo falta la **contraseña**, que nunca se guarda en el código:
```powershell
dotnet user-secrets set "Smtp:Password" "CLAVE_DE_COMERCIAL"
```
IMAP usa el mismo usuario y contraseña. Si la red bloquea el puerto 465, cambie en `appsettings.json` `"Port": 587` y `"UsarStartTls": true`.

**Respuestas por correo:** cada 2 minutos el sistema revisa la bandeja de entrada de comercial@ en modo de **solo lectura**: no marca los correos como leídos ni los mueve. Si un prospecto respondió, pausa su secuencia, lo marca como *Interesado* y lo muestra en el Panel. Las respuestas automáticas ("fuera de la oficina") se registran pero no pausan la secuencia.

En el menú **Correo** (solo administradores) puede enviar un correo de prueba y probar la conexión IMAP.

Recomendaciones:
- En el DNS de `tibolivia.com` verifique **SPF, DKIM y DMARC** (pruebe en https://www.mail-tester.com, la meta es 9/10 o más). Sin esto los correos llegan a spam.
- Pregunte el **límite de envíos por hora** de su servidor y empiece con 30–50 correos diarios.

### 2.3 WhatsApp (Meta WhatsApp Business Cloud API)
1. Cuenta de **Meta Business** verificada (Business Manager) de TI Bolivia.
2. Crear una app en developers.facebook.com → agregar el producto **WhatsApp**.
3. Registrar un **número de teléfono** dedicado. No puede estar en uso en la app de WhatsApp normal; si es el mismo del chatbot, se comparte este mismo webhook.
4. Obtener **Phone Number ID** y un **token permanente** (Usuario del sistema en Business Manager).
5. Configurar el webhook: URL `https://SU-DOMINIO/webhooks/whatsapp`, el **Verify Token** que usted elija, y suscribirse a `messages`.
6. Crear y hacer aprobar **plantillas de mensaje** (categoría *Marketing*) en WhatsApp Manager, por ejemplo `seguimiento_propuesta`:
   > Hola {{1}}, le escribimos de TI Bolivia. Le enviamos información sobre {{2}} para {{3}}. ¿Le gustaría agendar una llamada?
7. **Reglas importantes de WhatsApp:**
   - El primer mensaje a un prospecto **solo** puede ser una plantilla aprobada.
   - Solo se puede escribir a personas que dieron su **consentimiento (opt-in)**. El sistema no envía WhatsApp a prospectos sin la casilla "Aceptó recibir WhatsApp" (configurable en `WhatsApp:RequerirConsentimiento`). Enviar sin consentimiento provoca reportes y bloqueo del número.
   - El texto libre solo se permite durante **24 h** después del último mensaje del cliente.
   - Meta cobra por conversación iniciada por la empresa (marketing). Revise la tarifa para Bolivia.

### 2.3.0 Elegir el proveedor de WhatsApp

El proveedor se elige con el ajuste `WhatsApp:Proveedor`:

| Valor | Servicio | Cuándo usarlo |
|---|---|---|
| `Meta` (por defecto) | API oficial de WhatsApp Business | Prospección a empresas: plantillas aprobadas, sin riesgo de bloqueo si respeta el opt-in |
| `TextMeBot` | Servicio no oficial (WhatsApp normal vinculado por QR) | Pocos mensajes a contactos que esperan su mensaje. **Riesgo de bloqueo del número** con envíos masivos |

Con TextMeBot:
- Se envía el **texto completo** de la plantilla del sistema, sin aprobación de Meta y sin la ventana de 24 h.
- Los mensajes salen con una **pausa** (`WhatsApp:TextMeBot:SegundosEntreMensajes`, 8 s por defecto).
- Las respuestas llegan a `https://SU-DOMINIO/webhooks/textmebot?token=SU_TOKEN`, que se configura en el panel de TextMeBot.

El asistente `scripts\configurar-whatsapp.ps1` pregunta el proveedor y pide solo los datos que ese proveedor necesita.

### 2.3.1 Cargar los datos en el sistema

Con el sistema detenido, ejecute el asistente en PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File D:\Waldo\TIBolivia\SistemaProspectos\scripts\configurar-whatsapp.ps1
```

Le pedirá **Phone Number ID**, **WhatsApp Business Account ID**, **Access Token**, **App Secret**, **Verify Token** y la URL del chatbot.
Los datos se guardan con `user-secrets`: quedan fuera del proyecto y no se suben a GitHub. Luego ejecute `dotnet run`.

En el menú **WhatsApp** (solo administradores) puede:
1. Ver qué datos faltan y la **URL del webhook** que debe poner en Meta.
2. **Verificar la conexión**: número, nombre verificado, calidad y límite diario.
3. **Enviar un mensaje de prueba** con `hello_world` o con las plantillas de las campañas.
4. Ver si cada plantilla de las campañas está **APPROVED** en Meta.

### 2.4 Integración con el chatbot de la página de TI Bolivia
Cuando un prospecto responde por WhatsApp, este sistema hace un `POST` a `Chatbot:Url` (con `Authorization: Bearer {Chatbot:ApiKey}` si se configura):

```json
{
  "canal": "whatsapp",
  "telefono": "59171234567",
  "mensaje": "Hola, me interesa",
  "contexto": {
    "prospectoId": 1,
    "empresa": "Ferretería Andina",
    "nombre": "Carlos Mamani",
    "campana": "Presentación de servicios TI Bolivia",
    "servicio": "Desarrollo de software a medida",
    "comercial": "María López"
  }
}
```

El chatbot debe responder:

```json
{ "respuesta": "Hola Carlos, con gusto le cuento más...", "derivarAHumano": false }
```

- `respuesta`: texto que se envía al prospecto por WhatsApp (vacío o `null` = no responder).
- `derivarAHumano: true`: registra en la ficha que el chatbot pidió atención de un comercial.

Del lado del chatbot solo hay que agregar este endpoint, que reutiliza el mismo motor y conocimiento que ya usa en la web.
Use `telefono` como identificador de la conversación para mantener el contexto entre mensajes.

## 3. Instalación

```bash
# Requisitos: .NET SDK 10 y PostgreSQL
git clone https://github.com/wmagne/tibolivia-prospectos.git SistemaProspectos
cd SistemaProspectos
dotnet tool restore

# Configurar secretos (no los guarde en appsettings.json)
cd src/TiBolivia.Prospeccion
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=tibolivia_prospeccion;Username=postgres;Password=****"
dotnet user-secrets set "Admin:Password" "UnaClaveSegura123"
dotnet user-secrets set "Smtp:Password" "CLAVE_DE_COMERCIAL"

dotnet run
```

Al iniciar, el sistema **crea la base de datos y sus tablas** (migraciones), el usuario administrador
(`Admin:Email` / `Admin:Password`) y datos de ejemplo: 4 servicios, 4 plantillas y una campaña de 4 pasos (inactiva).

Mientras falte la contraseña SMTP o `WhatsApp:AccessToken` esté vacío, el sistema funciona en **modo simulación**: registra
los mensajes pero no los envía. Así puede probar las campañas sin riesgo.

En producción configure las variables de entorno (en IIS: `web.config` o el panel del App Pool), por ejemplo:
`ConnectionStrings__Default`, `Smtp__Password`, `WhatsApp__PhoneNumberId`,
`WhatsApp__AccessToken`, `WhatsApp__VerifyToken`, `WhatsApp__AppSecret`, `Chatbot__Url`, `Chatbot__ApiKey`,
`Campanas__UrlPublica`.

Publicar: `dotnet publish -c Release -o publish`.

### Repositorio y actualizaciones

El proyecto vive en https://github.com/wmagne/tibolivia-prospectos (privado). Para traer cambios a la PC:

```powershell
cd D:\Waldo\TIBolivia\SistemaProspectos
git pull
```

Para subir cambios hechos en la PC:

```powershell
git add .
git commit -m "Descripción del cambio"
git push
```

## 4. Uso diario (equipo comercial)

Vea **[docs/CAMPANAS.md](docs/CAMPANAS.md)**: las 3 campañas comerciales incluidas, los textos de WhatsApp para Meta y cómo lanzarlas.

1. **Importar** la lista de prospectos (Prospectos → Importar CSV; ver `docs/prospectos_ejemplo.csv`).
2. El admin revisa/edita las **plantillas** y **activa** la campaña.
3. El comercial filtra prospectos, los **selecciona** y pulsa **Inscribir en campaña**.
4. El sistema envía cada paso automáticamente. Cuando alguien responde, aparece en el **Panel → Respondieron y esperan atención**.
5. El comercial registra llamadas/reuniones y mueve el prospecto en el embudo: *Nuevo → Contactado → Interesado → Reunión agendada → Propuesta enviada → Ganado/Perdido*.

## 5. Estructura

```
src/TiBolivia.Prospeccion/
  Domain/Entidades.cs          Prospecto, Campaña, Paso, Plantilla, Inscripción, Mensaje, Interacción
  Data/AppDbContext.cs         EF Core + Identity (PostgreSQL)
  Data/DatosIniciales.cs       Roles, admin y datos de ejemplo
  Services/MotorCampanas.cs    Lógica de envío de cada paso
  Services/WorkerCampanas.cs   Servicio en segundo plano (cada 60 s)
  Services/EmailSender.cs      SMTP (MailKit)
  Services/LectorRespuestasEmail.cs  Lee respuestas por IMAP
  Services/WhatsAppSender.cs   WhatsApp Cloud API
  Services/ChatbotClient.cs    Integración con el chatbot de la web
  Webhooks/WhatsAppWebhook.cs  Mensajes entrantes y estados de entrega
  Pages/                       Interfaz del área comercial
```

## 6. Sugerencias para mejorar el sistema

**Resultados comerciales**
- **Pruebas A/B**: dos asuntos o textos por paso y quedarse con el que tenga más respuestas.
- **Seguimiento de aperturas y clics**: píxel de apertura y enlaces con redirección para medir interés y priorizar a quién llamar (lead scoring).
- **Agenda de reuniones**: enlace a Calendly, Google Calendar o Microsoft Bookings en las plantillas. Al agendar, el prospecto pasa a *Reunión agendada*.
- **Propuesta en PDF** generada desde el sistema con los servicios elegidos y su seguimiento (enviada, vista, aceptada).
- **Segmentación por rubro**: campañas distintas para salud, comercio, educación o gobierno, cada una con su caso de éxito.
- **Generación de textos con IA**: redactar o personalizar correos según el rubro y el sitio web del prospecto, siempre con revisión del comercial antes de enviar.

**Operación y seguridad**
- Respaldo diario de PostgreSQL y monitoreo (logs con Serilog/Seq, alertas si sube la tasa de fallidos o rebotes).
- Procesar los rebotes (bounces) del proveedor de correo para marcar correos inválidos automáticamente.
- Límite diario de envíos por canal para cuidar la reputación del dominio y del número.
- Si en el futuro corre más de una instancia del servidor, use una cola (Hangfire o Quartz) o un bloqueo en la base de datos para que el motor no envíe duplicados.
- Autenticación en dos pasos para los usuarios administradores.
- Registro de auditoría de cambios de estado y de exportaciones de datos.

**Legal**
- Guarde **cuándo y cómo** dio su consentimiento cada prospecto (formulario web, feria, llamada) y respete siempre las bajas.
- Use listas de contactos obtenidas legítimamente (ferias, referidos, formularios de la web, directorios públicos empresariales). No use listas compradas.
