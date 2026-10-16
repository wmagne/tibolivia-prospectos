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

### 2.2 Correo
Use un proveedor de envío profesional, **no** una cuenta de Gmail personal (se bloquea con envíos masivos):
- Amazon SES, Brevo, SendGrid, Mailgun o Microsoft 365. Todos ofrecen SMTP; solo se configura la sección `Smtp`.
- En el DNS de `tibolivia.com` configure **SPF, DKIM y DMARC**. Sin esto los correos llegan a spam.
- Recomendado: usar un subdominio para prospección (ej. `info.tibolivia.com`) y no arriesgar la reputación del dominio principal.
- Haga un "calentamiento": empiece con 30–50 correos diarios y aumente gradualmente.

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
git clone <este repositorio>
cd SistemaProspectos
dotnet tool restore

# Configurar secretos (no los guarde en appsettings.json)
cd src/TiBolivia.Prospeccion
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=tibolivia_prospeccion;Username=postgres;Password=****"
dotnet user-secrets set "Admin:Password" "UnaClaveSegura123"

dotnet run
```

Al iniciar, el sistema **crea la base de datos y sus tablas** (migraciones), el usuario administrador
(`Admin:Email` / `Admin:Password`) y datos de ejemplo: 4 servicios, 4 plantillas y una campaña de 4 pasos (inactiva).

Mientras `Smtp:Host` o `WhatsApp:AccessToken` estén vacíos, el sistema funciona en **modo simulación**: registra
los mensajes pero no los envía. Así puede probar las campañas sin riesgo.

En producción configure las variables de entorno (en IIS: `web.config` o el panel del App Pool), por ejemplo:
`ConnectionStrings__Default`, `Smtp__Host`, `Smtp__Usuario`, `Smtp__Password`, `WhatsApp__PhoneNumberId`,
`WhatsApp__AccessToken`, `WhatsApp__VerifyToken`, `WhatsApp__AppSecret`, `Chatbot__Url`, `Chatbot__ApiKey`,
`Campanas__UrlPublica`.

Publicar: `dotnet publish -c Release -o publish`.

### Copiar a `D:\Waldo\TIBolivia\SistemaProspectos` en su PC

```powershell
git clone -b claude/dazzling-meitner-6s8v1b https://github.com/wmagne/farmacia.git $env:TEMP\farmacia
New-Item -ItemType Directory -Force D:\Waldo\TIBolivia | Out-Null
Copy-Item -Recurse $env:TEMP\farmacia\SistemaProspectos D:\Waldo\TIBolivia\SistemaProspectos
cd D:\Waldo\TIBolivia\SistemaProspectos
git init   # recomendado: que sea su propio repositorio (ej. wmagne/tibolivia-prospectos)
```

## 4. Uso diario (equipo comercial)
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
  Services/WhatsAppSender.cs   WhatsApp Cloud API
  Services/ChatbotClient.cs    Integración con el chatbot de la web
  Webhooks/WhatsAppWebhook.cs  Mensajes entrantes y estados de entrega
  Pages/                       Interfaz del área comercial
```

## 6. Sugerencias para mejorar el sistema

**Resultados comerciales**
- **Pruebas A/B**: dos asuntos o textos por paso y quedarse con el que tenga más respuestas.
- **Seguimiento de aperturas y clics**: píxel de apertura y enlaces con redirección para medir interés y priorizar a quién llamar (lead scoring).
- **Detectar respuestas por correo**: leer el buzón (IMAP o webhook del proveedor) para pausar la secuencia cuando el prospecto responde un correo. Hoy eso ocurre automáticamente solo con WhatsApp; con correo lo hace el comercial desde la ficha.
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
