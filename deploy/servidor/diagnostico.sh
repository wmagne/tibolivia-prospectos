#!/usr/bin/env bash
# Inventario de solo lectura del servidor para el Sistema de Prospección (no modifica nada).
DOMINIO="${1:-prospectos.tibolivia.com}"
echo "== Sistema"; . /etc/os-release; echo "$PRETTY_NAME"; echo "CPUs: $(nproc)"; free -h | head -2; df -h / | tail -1
echo "== IP pública: $(curl -fs https://api.ipify.org || echo '?')"
echo "== DNS: $DOMINIO -> $(getent ahostsv4 "$DOMINIO" | awk '{print $1; exit}')"
echo "== DNS: mail.tibolivia.com -> $(getent ahostsv4 mail.tibolivia.com | awk '{print $1; exit}')"
echo "== Docker"; docker ps --format '{{.Names}}: {{.Status}}'
echo "== Red estacionacai_default: $(docker network inspect estacionacai_default >/dev/null 2>&1 && echo existe || echo NO existe)"
echo "== Caddyfile (sitios)"; grep -oE '^[a-z0-9.-]+\.[a-z]+[[:space:]]*\{' /opt/estacionacai/Caddyfile 2>/dev/null | tr -d '{ '
grep -q '^(comun)' /opt/estacionacai/Caddyfile 2>/dev/null && echo "(snippet 'comun' disponible)"
echo "== Prospección instalada"; docker ps --filter name=tibolivia-prospectos --format '{{.Names}}: {{.Status}}'; ls /opt/tibolivia-prospectos 2>/dev/null
echo "== https://$DOMINIO/health -> $(curl -s -m 15 "https://$DOMINIO/health")"
if docker ps --format '{{.Names}}' | grep -qx tibolivia-prospectos-db; then
  echo "== Usuarios del sistema (correo | nombre | teléfono | rol | activo)"
  docker exec tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA -F ' | ' -c \
    'SELECT u."Email", u."NombreCompleto", coalesce(u."PhoneNumber", $$-$$), coalesce(string_agg(r."Name", $$,$$), $$-$$), u."Activo"
       FROM "AspNetUsers" u LEFT JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id" LEFT JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
      GROUP BY u."Id" ORDER BY u."Email";'
fi
if docker ps --format '{{.Names}}' | grep -qx tibolivia-prospectos-db; then
  echo "== Configuración de WhatsApp guardada desde la web (las claves cifradas no se muestran)"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA -F ' | ' <<'SQL' 2>/dev/null
SELECT "Clave",
       CASE WHEN "Cifrado" THEN '(guardado cifrado)'
            WHEN "Clave" IN ('WhatsApp:VerifyToken', 'WhatsApp:TextMeBot:TokenWebhook') THEN '(definido)'
            ELSE "Valor" END,
       to_char("ActualizadoEn" - interval '4 hours', 'DD/MM HH24:MI')
  FROM "Ajustes" ORDER BY 1;
SQL
  echo "== Mensajes de WhatsApp registrados"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA <<'SQL' 2>/dev/null
SELECT "Direccion" || ' ' || "Estado" || ': ' || count(*) FROM "Mensajes" WHERE "Canal" = 'WhatsApp' GROUP BY "Direccion", "Estado";
SQL
fi
if docker ps --format '{{.Names}}' | grep -qx tibolivia-prospectos-db; then
  echo "== Campañas (activa | inscritos activos | finalizados/otros | próximo envío)"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA -F ' | ' <<'SQL' 2>/dev/null
SELECT c."Nombre", CASE WHEN c."Activa" THEN 'ACTIVA' ELSE 'inactiva' END,
       count(i."Id") FILTER (WHERE i."Estado" = 'Activa'), count(i."Id") FILTER (WHERE i."Estado" <> 'Activa'),
       coalesce(to_char(min(i."ProximoEnvio") FILTER (WHERE i."Estado" = 'Activa') - interval '4 hours', 'DD/MM HH24:MI'), '-')
  FROM "Campanas" c LEFT JOIN "Inscripciones" i ON i."CampanaId" = c."Id"
 GROUP BY c."Id" ORDER BY c."Activa" DESC, c."Nombre";
SQL
  echo "== Prospectos por origen (total | con correo)"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA -F ' | ' <<'SQL' 2>/dev/null
SELECT coalesce("Origen", '-'), count(*), count(*) FILTER (WHERE coalesce("Email", '') <> '') FROM "Prospectos" GROUP BY 1 ORDER BY 1;
SQL
  echo "== Correos registrados (dirección estado: cantidad)"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA <<'SQL' 2>/dev/null
SELECT "Direccion" || ' ' || "Estado" || ': ' || count(*) FROM "Mensajes" WHERE "Canal" = 'Email' GROUP BY "Direccion", "Estado";
SQL
  echo "== Respuestas de prospectos (hora Bolivia | empresa | asunto)"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA -F ' | ' <<'SQL' 2>/dev/null
SELECT to_char(m."CreadoEn" - interval '4 hours', 'DD/MM HH24:MI'), p."Empresa", coalesce(m."Asunto", '-')
  FROM "Mensajes" m JOIN "Prospectos" p ON p."Id" = m."ProspectoId"
 WHERE m."Direccion" = 'Entrante' AND m."Estado" = 'Recibido' ORDER BY m."CreadoEn" DESC LIMIT 20;
SQL
  echo "== Prospectos por estado"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA -F ': ' <<'SQL' 2>/dev/null
SELECT "Estado", count(*) FROM "Prospectos" GROUP BY 1 ORDER BY 2 DESC;
SQL
  echo "== Correos enviados por día (hora Bolivia)"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA -F ': ' <<'SQL' 2>/dev/null
SELECT to_char("CreadoEn" - interval '4 hours', 'DD/MM'), count(*) FROM "Mensajes" WHERE "Canal" = 'Email' AND "Direccion" = 'Saliente' AND "Estado" = 'Enviado' GROUP BY 1 ORDER BY 1;
SQL
  echo "== Últimos 15 correos (hora Bolivia | estado | empresa | asunto | error)"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA -F ' | ' <<'SQL' 2>/dev/null
SELECT to_char(m."CreadoEn" - interval '4 hours', 'DD/MM HH24:MI'), m."Estado", p."Empresa", coalesce(m."Asunto", '-'), coalesce(left(m."Error", 120), '')
  FROM "Mensajes" m JOIN "Prospectos" p ON p."Id" = m."ProspectoId" WHERE m."Canal" = 'Email' ORDER BY m."CreadoEn" DESC LIMIT 15;
SQL
fi
echo "== Últimos avisos del sistema sobre WhatsApp/Meta (24 h)"
docker logs --since 24h tibolivia-prospectos-app 2>&1 | grep -iE "whatsapp|meta respondi|graph" | tail -10 || true
echo "== Últimos avisos sobre correo y campañas (24 h)"
docker logs --since 24h tibolivia-prospectos-app 2>&1 | grep -iE "smtp|correo|campa|mail" | tail -15 || true
echo "== DNS de correo de tibolivia.com (SPF, DMARC, MX, DKIM)"
consultar() { if command -v dig >/dev/null; then dig +short "$2" "$1"; else docker run --rm alpine:3.20 sh -c "apk add -q bind-tools >/dev/null 2>&1 && dig +short $2 $1"; fi; }
echo "MX:    $(consultar tibolivia.com MX | tr '\n' ' ')"
echo "SPF:   $(consultar tibolivia.com TXT | grep -i spf)"
echo "DMARC: $(consultar _dmarc.tibolivia.com TXT)"
for s in mail default dkim s1 selector1 x; do r=$(consultar "$s._domainkey.tibolivia.com" TXT); [ -n "$r" ] && echo "DKIM ($s): $(echo "$r" | cut -c1-80)…"; done
echo "IP del servidor de correo: $(consultar mail.tibolivia.com A) · PTR: $(consultar "$(consultar mail.tibolivia.com A | head -1 | awk -F. '{print $4"."$3"."$2"."$1}').in-addr.arpa" PTR)"
echo "== Contenedores de correo"; docker ps --format '{{.Names}} {{.Image}}' | grep -iE "mail|postfix|dovecot|opendkim|rspamd" || echo "(no hay contenedores de correo; puede ser un servicio del sistema)"
systemctl is-active postfix opendkim 2>/dev/null | paste -sd' ' | sed 's/^/postfix\/opendkim: /'
echo "== WhatsApp en Meta: número y plantillas (solo lectura)"
docker exec -e Logging__LogLevel__Default=Warning -e Logging__LogLevel__Microsoft=Warning tibolivia-prospectos-app \
  dotnet TiBolivia.Prospeccion.dll --plantillas-meta 2>&1 | grep -vE "^(warn|info|fail):|^\s+(Microsoft|Storing|No XML|Failed to)" | tail -40 || true
