#!/usr/bin/env bash
# =============================================================================
#  Sistema de Prospección TIBolivia: instalación/actualización en el servidor Docker
#  compartido (mismo esquema que crm.tibolivia.com: Caddy compartido da el HTTPS).
#  Uso (root, desde el código copiado al servidor):
#     bash deploy/servidor/desplegar.sh prospectos.tibolivia.com
#  Variables opcionales: ADMIN_CLAVE, SMTP_CLAVE, WHATSAPP_PROVEEDOR, WHATSAPP_PHONE_NUMBER_ID,
#  WHATSAPP_BUSINESS_ACCOUNT_ID, WHATSAPP_ACCESS_TOKEN, WHATSAPP_APP_ID, WHATSAPP_APP_SECRET,
#  TEXTMEBOT_API_KEY, CHATBOT_URL. Solo se escriben si vienen con valor (no borran lo ya configurado).
#  No modifica los demás contenedores ni sitios.
# =============================================================================
set -euo pipefail

DOMINIO="${1:-prospectos.tibolivia.com}"
CODIGO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DESTINO="/opt/tibolivia-prospectos"
RED_CADDY="${RED_CADDY:-estacionacai_default}"
CADDY_CONT="${CADDY_CONT:-heladeria-caddy}"
CADDYFILE="${CADDYFILE:-/opt/estacionacai/Caddyfile}"
APP="tibolivia-prospectos-app"
DB="tibolivia-prospectos-db"
RESPALDOS="/var/backups/tibolivia-prospectos"

paso() { echo; echo "==> $*"; }
aleatorio() { local s; s="$(LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom 2>/dev/null | head -c "${1:-32}")" || true; printf '%s' "$s"; }
# Escribe o reemplaza CLAVE='valor' en app.env solo si el valor no está vacío.
# Entre comillas simples para que Docker no interprete $ ni # (no se admite la comilla simple en el valor).
fijar() { local k="$1" v="${2:-}"; [[ -n "$v" ]] || return 0
  if [[ "$v" == *"'"* ]]; then echo "AVISO: $k contiene una comilla simple ('); no se aplicó."; return 0; fi
  grep -v "^${k}=" "$DESTINO/app.env" > "$DESTINO/app.env.tmp" || true
  printf "%s='%s'\n" "$k" "$v" >> "$DESTINO/app.env.tmp"; mv "$DESTINO/app.env.tmp" "$DESTINO/app.env"; chmod 600 "$DESTINO/app.env"; }

[[ $EUID -eq 0 ]] || { echo "Ejecute como root."; exit 1; }
command -v docker >/dev/null || { echo "Docker no está instalado."; exit 1; }
docker network inspect "$RED_CADDY" >/dev/null || { echo "No existe la red Docker $RED_CADDY."; exit 1; }
docker inspect "$CADDY_CONT" >/dev/null || { echo "No existe el contenedor $CADDY_CONT."; exit 1; }
[[ -f "$CADDYFILE" ]] || { echo "No encuentro $CADDYFILE."; exit 1; }

paso "1/6 Configuración en $DESTINO"
mkdir -p "$DESTINO" "$RESPALDOS"
install -m 644 "$CODIGO_DIR/deploy/servidor/docker-compose.yml" "$DESTINO/docker-compose.yml"
if [[ ! -f "$DESTINO/.env" ]]; then
  PG_CLAVE="$(aleatorio 28)"
  umask 077
  printf 'PG_CLAVE=%s\nCODIGO_DIR=%s\nRED_CADDY=%s\n' "$PG_CLAVE" "$CODIGO_DIR" "$RED_CADDY" > "$DESTINO/.env"
  ADMIN="${ADMIN_CLAVE:-$(aleatorio 16)}"
  [[ "$ADMIN" != *"'"* ]] || { echo "La clave del administrador no puede tener comillas simples."; exit 1; }
  cat > "$DESTINO/app.env" <<CONF
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Host=${DB};Port=5432;Database=tibolivia_prospeccion;Username=tibolivia;Password=${PG_CLAVE}
Admin__Email=admin@tibolivia.com
Admin__Password='${ADMIN}'
Campanas__UrlPublica=https://${DOMINIO}
WhatsApp__VerifyToken=$(aleatorio 24)
WhatsApp__TextMeBot__TokenWebhook=$(aleatorio 24)
CONF
  umask 022
  printf 'Sistema de Prospección TIBolivia\nURL: https://%s\nUsuario: admin@tibolivia.com\nContraseña inicial: %s\n' "$DOMINIO" "$ADMIN" > /root/tibolivia-prospectos-credenciales.txt
  chmod 600 /root/tibolivia-prospectos-credenciales.txt
  echo "Configuración creada (credenciales iniciales en /root/tibolivia-prospectos-credenciales.txt)."
else
  sed -i "s#^CODIGO_DIR=.*#CODIGO_DIR=${CODIGO_DIR}#" "$DESTINO/.env"
  fijar Campanas__UrlPublica "https://${DOMINIO}"
  echo "Se conserva la configuración existente."
fi
# Datos que llegan desde los secretos de GitHub (solo si tienen valor).
fijar Smtp__Password "${SMTP_CLAVE:-}"
fijar WhatsApp__Proveedor "${WHATSAPP_PROVEEDOR:-}"
fijar WhatsApp__PhoneNumberId "${WHATSAPP_PHONE_NUMBER_ID:-}"
fijar WhatsApp__BusinessAccountId "${WHATSAPP_BUSINESS_ACCOUNT_ID:-}"
fijar WhatsApp__AccessToken "${WHATSAPP_ACCESS_TOKEN:-}"
fijar WhatsApp__AppId "${WHATSAPP_APP_ID:-}"
fijar WhatsApp__AppSecret "${WHATSAPP_APP_SECRET:-}"
fijar WhatsApp__TextMeBot__ApiKey "${TEXTMEBOT_API_KEY:-}"
fijar Chatbot__Url "${CHATBOT_URL:-}"

paso "2/6 Respaldo previo de la base (si ya existe)"
if docker ps --format '{{.Names}}' | grep -qx "$DB"; then
  ARCH="$RESPALDOS/antes_$(date +%F_%H%M).sql.gz"
  docker exec "$DB" pg_dump -U tibolivia tibolivia_prospeccion | gzip -9 > "$ARCH" && chmod 600 "$ARCH" && echo "Respaldo: $ARCH"
else
  echo "Primera instalación: no hay base que respaldar."
fi

paso "3/6 Construir la imagen y levantar los contenedores"
cd "$DESTINO"
[[ "${OMITIR_BUILD:-0}" == 1 ]] || docker compose build --pull app
docker compose up -d --remove-orphans

paso "4/6 Esperar a que el sistema responda"
IP=""
for _ in $(seq 1 90); do
  IP="$(docker inspect -f "{{(index .NetworkSettings.Networks \"$RED_CADDY\").IPAddress}}" "$APP" 2>/dev/null || true)"
  if [[ -n "$IP" ]] && curl -fs --max-time 3 "http://$IP:8080/health" >/dev/null; then break; fi
  sleep 2
done
if ! curl -fs --max-time 3 "http://$IP:8080/health" >/dev/null; then
  echo "El sistema no respondió. Últimos registros:"; docker logs --tail 80 "$APP" || true
  exit 1
fi
echo "Sistema funcionando ($APP en $IP:8080)."
docker logs "$APP" 2>&1 | grep -E "Lista " | tail -5 || true

paso "5/6 Caddy: subdominio $DOMINIO"
if grep -qE "^${DOMINIO//./\\.}[[:space:]]*\{" "$CADDYFILE"; then
  echo "El Caddyfile ya tiene $DOMINIO; no se modifica."
else
  IMPORT=""; grep -qE '^\(comun\)' "$CADDYFILE" && IMPORT=$'\timport comun\n'
  RESPALDO="${CADDYFILE}.antes-prospectos-$(date +%F_%H%M%S)"
  cp -a "$CADDYFILE" "$RESPALDO"
  # Se agrega al final (>>) para conservar el mismo archivo montado en el contenedor de Caddy.
  printf '\n# Sistema de Prospección TIBolivia (campañas de correo y WhatsApp).\n%s {\n%s\treverse_proxy %s:8080\n}\n' "$DOMINIO" "$IMPORT" "$APP" >> "$CADDYFILE"
  if docker exec "$CADDY_CONT" caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile >/dev/null 2>&1; then
    docker exec "$CADDY_CONT" caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile
    echo "Caddy recargado (respaldo del Caddyfile: $RESPALDO)."
  else
    cat "$RESPALDO" > "$CADDYFILE"
    echo "ERROR: la configuración de Caddy no validó; se restauró el Caddyfile original."; exit 1
  fi
fi

paso "6/6 Respaldo diario (03:30, se guardan 14 días)"
cat > /usr/local/bin/tibolivia-prospectos-respaldo <<'SH'
#!/usr/bin/env bash
set -euo pipefail
D=/var/backups/tibolivia-prospectos; mkdir -p "$D"
A="$D/tibolivia_prospeccion_$(date +%F_%H%M).sql.gz"
docker exec tibolivia-prospectos-db pg_dump -U tibolivia tibolivia_prospeccion | gzip -9 > "$A"; chmod 600 "$A"
find "$D" -name 'tibolivia_prospeccion_*.sql.gz' -mtime +14 -delete
echo "Respaldo: $A"
SH
chmod 755 /usr/local/bin/tibolivia-prospectos-respaldo
echo "30 3 * * * root /usr/local/bin/tibolivia-prospectos-respaldo >> /var/log/tibolivia-prospectos-respaldo.log 2>&1" > /etc/cron.d/tibolivia-prospectos-respaldo

echo
echo "Listo: https://${DOMINIO}  (usuario admin@tibolivia.com; la contraseña inicial está en /root/tibolivia-prospectos-credenciales.txt)"
