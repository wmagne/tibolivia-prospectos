# Alertas SICOES (módulo en pruebas)

Lee convocatorias del Estado, las compara con el perfil de cada cliente y deja como **oportunidad** las que tienen
puntaje 60 o más. Plan de negocio: https://claude.ai/artifact/44Sx65se5HGd9cwTYzKvqz

## Cómo entran los datos

El buscador del portal SICOES pide un código de seguridad (captcha) y cambia el orden de sus columnas en cada consulta:
es una protección contra la lectura automática y **el sistema no se la salta**. Por eso hay dos entradas permitidas:

1. **SICOES → Cargar lista**: se pega la tabla de convocatorias (o se sube un CSV). Hacen falta las columnas CUCE,
   Entidad y Objeto; también se leen Modalidad, Monto, Departamento, fechas, Enlace y Estado.
2. **`Sicoes:UrlFuente`**: una dirección que entregue la lista en CSV, solo si es una fuente autorizada
   (por ejemplo un acceso que habilite el Ministerio de Economía). Se consulta a las 7:00 y a las 14:00.

El CUCE evita duplicados: si un proceso ya estaba, se actualizan su estado y su fecha límite.

## Puntaje (0 a 100)

- Palabra excluida o monto menor al mínimo: 0. Otro departamento (y no es nacional): 0.
- Primera palabra clave: 55; cada palabra distinta más: +10 (hasta +20). Singular y plural cuentan una vez.
- Departamento del perfil: +15; alcance nacional: +10. Consulta de Precios: +10.
- Sin ninguna palabra clave no llega a 60.

## Avisos (con `Sicoes:Habilitado = true`)

- Resumen diario por correo después de la lectura de las 7:00.
- WhatsApp al momento para cada Consulta de Precios o cierre en menos de 3 días.
- Recordatorio 48 horas antes del cierre (correo y WhatsApp).
- Entre las 7:00 y las 21:00. Cada aviso se registra en `SicoesAvisos` y no se repite.
- **Modo pruebas** (`Sicoes:ModoPruebas`): solo salen a `CorreosPermitidos` y `WhatsAppPermitidos`, aunque un perfil diga otra cosa.

Para que el WhatsApp llegue sin que el destinatario haya escrito antes, Meta exige una plantilla aprobada
(`Sicoes:PlantillaWhatsApp` = `tibolivia_aviso_sicoes`, de utilidad, 4 variables: entidad, objeto, monto y modalidad, fecha de cierre). La primera versión (`tibolivia_alerta_sicoes`) Meta la pasó a marketing; esta se envió el 26/11/2026 con la acción `whatsapp-plantilla-sicoes` de `desplegar.yml`.

## Tablas propias

`SicoesProcesos`, `SicoesPerfiles`, `SicoesOportunidades`, `SicoesLecturas`, `SicoesAvisos`. No tienen claves hacia
prospectos ni campañas: si el módulo se separa como aplicación aparte, se lleva entero.

## Diagnóstico

`desplegar.yml` con `accion=diagnostico` muestra la sección «SICOES»: últimas lecturas, procesos leídos, oportunidades
abiertas, avisos y errores.
