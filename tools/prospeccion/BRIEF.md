# Búsqueda de prospectos B2B en Bolivia (TI Bolivia)

TI Bolivia (Santa Cruz) vende sistemas a medida, apps móviles, CRM, facturación SIAT y correo corporativo.
Buscamos **empresas medianas** (aprox. 10–250 empleados, con operación real: sucursales, flota, planta, varias líneas de venta)
que se beneficiarían de un sistema a medida. No incluir: entidades del Estado, ONGs, multinacionales gigantes (Coca-Cola, Tigo, bancos grandes),
microemprendimientos de una persona.

## Qué datos recoger (solo datos PÚBLICOS de la empresa)
Usa WebSearch y WebFetch: sitios web de las empresas (páginas "Contacto"), directorios de cámaras empresariales
(CAINCO, Cámara Nacional de Comercio, CNI, CAMEX, Cámara de Industria de Cochabamba, CADEX, CAINCO socios, etc.),
páginas amarillas bolivianas, listados de ferias (Expocruz, Fexpocruz, Fexco), Google Maps/fichas públicas citadas en resultados.
- Solo correos y teléfonos **corporativos publicados por la propia empresa** (info@, ventas@, contacto@, comercial@, o el Gmail que la empresa publica como su correo de contacto).
- NO inventes correos (nada de "adivinar" info@dominio si no aparece publicado). Si no hay correo publicado, deja Email vacío.
- NO recojas datos personales privados (celulares personales de empleados, correos personales que no sean el contacto oficial de la empresa).
- Si el nombre de una persona de contacto (gerente) aparece públicamente en el sitio de la empresa, puedes ponerlo en Contacto/Cargo.

## Formato de salida: CSV UTF-8, separado por comas, con comillas donde haga falta
Encabezado EXACTO:
Empresa,Contacto,Cargo,Email,Telefono,Rubro,Ciudad,SitioWeb,ConsentimientoWhatsApp,Prioridad,Direccion,Necesidad,Fuente,Notas,Verificar

- Telefono: formato +591XXXXXXXX (sin espacios). Prefiere celular/WhatsApp corporativo publicado; si solo hay fijo, úsalo.
- Rubro: usar EXACTAMENTE uno de: "Salud", "Educación", "Inmobiliaria y construcción", "Distribución, logística y agro",
  "Logística y transporte", "Gastronomía, hotelería y servicios", "Farmacias", "Comercio minorista", "Servicios empresariales",
  "Automotriz", "Industria".
- Ciudad: Santa Cruz, La Paz, El Alto, Cochabamba, Tarija, Sucre, Oruro, Potosí, Trinidad, Cobija, Montero, etc.
- ConsentimientoWhatsApp: siempre "no".
- Prioridad: A (tiene correo y web, claramente mediana, necesidad evidente), B (datos parciales), C (dudosa).
- Necesidad: una frase concreta de qué sistema le serviría (ej. "App de preventa para vendedores y control de inventario por sucursal").
- Fuente: URL donde encontraste los datos.
- Notas: actividad de la empresa + "Encontrado en búsqueda web (oct. 2026): confirmar datos antes de contactar."
- Verificar: "si".

## Reglas
- Excluye las empresas listadas en el archivo existentes.txt indicado en tu tarea (ya están en el sistema).
- Sin duplicados dentro de tu archivo.
- Meta: entre 80 y 150 empresas reales de tu segmento. Calidad antes que cantidad: cada fila debe tener al menos teléfono o correo publicado.
- Escribe el CSV con Python (módulo csv) para que las comillas queden bien. Guarda avances parciales en el archivo a medida que avanzas.
- No modifiques ningún otro archivo. No envíes nada a ninguna empresa.
- Al terminar responde SOLO con: cantidad de filas, cuántas con email, y las fuentes principales usadas (máx. 5 líneas).

## Presupuesto de búsquedas (IMPORTANTE)
El límite de búsquedas web es compartido entre varios agentes: usa como máximo **22 búsquedas WebSearch**, en modo "standard".
**Desde el 17/11/2026 la red permite abrir los directorios** www.amarillas.bo, www.directorio.com.bo, www.boliviaentusmanos.com,
www.construex.com.bo, www.ibce.org.bo, www.fexpocruz.com.bo, www.asfi.gob.bo, www.boliviaemprende.com, bolivia.paginasamarillas.com
y algunos diarios (lostiempos.com, eju.tv, correodelsur.com). Úsalos: abre con WebFetch (o `curl -sL` por Bash) las páginas de
LISTADO por rubro y ciudad de esos directorios y saca de cada página varias empresas con su teléfono y correo publicados.
Eso rinde mucho más que los resúmenes del buscador. Usa WebSearch sobre todo para encontrar las URL de esos listados.
cybo.com y vymaps.com responden 403: no insistas con ellos.
Guarda el CSV antes de quedarte sin búsquedas.
