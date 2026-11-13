# Servicios del sitio web y sus campañas

La búsqueda diaria revisa el sitio (repositorio `wmagne/TIBoliviaWeb`, carpeta `sitio/`, y el guion del asistente
`contacto/ficha_ventas.md`). Si aparece un producto o servicio que no está en esta tabla, se le arma su campaña
en `src/TiBolivia.Prospeccion/Data/PaqueteCampanasWeb.cs` (correo con enlace a su página, WhatsApp como tarea,
seguimiento y llamada) y su regla en `appsettings.json` (`Campanas:OtrasAutoInscripciones`, antes de las reglas
generales), se agrega la fila aquí y se avisa en el informe del día.

Revisado por última vez: 2026-11-13 (commit f047ffb del sitio, sin cambios).

| Producto en el sitio | Página | Campaña en el sistema | A quién se inscribe (regla) |
| --- | --- | --- | --- |
| Facturación en línea SIAT (desde Bs 79) | /facturacion/ | Facturación SIAT desde Bs 79 (sitio web) | Comercios, ferreterías, librerías, distribuidoras e importadoras (80/día) |
| Cobros con QR confirmados por el banco | /cobros-qr/ | Cobros con QR confirmados por el banco (sitio web) | Gastronomía (100/día) y comercio minorista (40/día) |
| Sistema de ventas e inventario | / (#productos) | Publicidad por rubro (correo) y la de Novedades | Resto de empresas con correo |
| Apps de pedidos y delivery | / (#productos) | Apps de pedidos y delivery (sitio web) | Negocios con delivery, licorerías, courier y mensajería (60/día) |
| Asistente con IA para su web (nuevo) | / (el chat del sitio) | Asistente con IA para su web (sitio web) | Empresas con correo y sitio web propio (40/día) |
| Área de sistemas y correo | / (#productos) | Correo corporativo (empresas que usan Gmail o Hotmail) | Manual |
| Todo junto («Venda, cobre y facture») | / | Novedades TIBolivia: venda, cobre y facture (recontacto) | Contactados hace más de 21 días que no respondieron (150/día) |
