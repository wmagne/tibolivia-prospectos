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
  echo "== Correos registrados (dirección estado: cantidad)"
  docker exec -i tibolivia-prospectos-db psql -U tibolivia -d tibolivia_prospeccion -tA <<'SQL' 2>/dev/null
SELECT "Direccion" || ' ' || "Estado" || ': ' || count(*) FROM "Mensajes" WHERE "Canal" = 'Email' GROUP BY "Direccion", "Estado";
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
