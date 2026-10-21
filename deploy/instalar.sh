#!/usr/bin/env bash
# Instalador del Sistema de Prospección TI Bolivia en un servidor Ubuntu 22.04/24.04.
# Uso (en el servidor, dentro de la carpeta del proyecto):  sudo bash deploy/instalar.sh
set -euo pipefail
cd "$(dirname "$0")"

if [[ $EUID -ne 0 ]]; then echo "Ejecute con sudo: sudo bash deploy/instalar.sh"; exit 1; fi

echo "=== 1/5 Instalando Docker ==="
if ! command -v docker >/dev/null || ! docker compose version >/dev/null 2>&1; then
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq docker.io docker-compose-v2 curl dnsutils
  systemctl enable --now docker
fi
docker compose version

echo "=== 2/5 Datos de configuración ==="
aleatorio() { tr -dc 'A-Za-z0-9' </dev/urandom | head -c "$1"; }
# Los valores se guardan entre comillas simples para admitir $, # o espacios; solo se rechaza la comilla simple.
sin_comilla() { while [[ "$1" == *"'"* ]]; do read -r -s -p "No use comillas simples ('). Escriba de nuevo: " v; echo >&2; set -- "$v"; done; echo "$1"; }
preguntar() { local v; read -r -p "$1: " v; sin_comilla "$v"; }
secreto() { local v; read -r -s -p "$1: " v; echo >&2; sin_comilla "$v"; }

if [[ -f .env ]]; then
  echo "Ya existe deploy/.env: se mantiene su configuración (edítelo con: nano deploy/.env)."
else
  DOMINIO=$(preguntar "Dominio del sistema (ej. prospectos.tibolivia.com)")
  ADMIN_PASSWORD=$(secreto "Contraseña para admin@tibolivia.com (mín. 8 caracteres)")
  SMTP_PASSWORD=$(secreto "Contraseña del correo comercial@tibolivia.com")
  echo "Proveedor de WhatsApp: 1 = Meta (oficial)  2 = TextMeBot"
  OPCION=$(preguntar "Elija 1 o 2 (Enter = 1)")
  {
    echo "DOMINIO='$DOMINIO'"
    echo "POSTGRES_PASSWORD='$(aleatorio 32)'"
    echo "ADMIN_EMAIL='admin@tibolivia.com'"
    echo "ADMIN_PASSWORD='$ADMIN_PASSWORD'"
    echo "SMTP_PASSWORD='$SMTP_PASSWORD'"
    if [[ "$OPCION" == "2" ]]; then
      echo "WHATSAPP_PROVEEDOR='TextMeBot'"
      echo "TEXTMEBOT_API_KEY='$(secreto "API Key de TextMeBot")'"
      echo "TEXTMEBOT_TOKEN_WEBHOOK='$(aleatorio 20)'"
    else
      echo "WHATSAPP_PROVEEDOR='Meta'"
      echo "WHATSAPP_PHONE_NUMBER_ID='$(preguntar "Phone Number ID (Enter para cargarlo después)")'"
      echo "WHATSAPP_BUSINESS_ACCOUNT_ID='$(preguntar "WhatsApp Business Account ID")'"
      echo "WHATSAPP_ACCESS_TOKEN='$(secreto "Access Token permanente")'"
      echo "WHATSAPP_APP_ID='$(preguntar "App ID")'"
      echo "WHATSAPP_APP_SECRET='$(secreto "App Secret")'"
      echo "WHATSAPP_VERIFY_TOKEN='$(aleatorio 20)'"
    fi
    echo "CHATBOT_URL='$(preguntar "URL del chatbot (opcional)")'"
  } > .env
  chmod 600 .env
fi
set -a; source .env; set +a

echo "=== 3/5 Verificando el dominio ==="
IP_SERVIDOR=$(curl -s4 https://api.ipify.org || true)
IP_DOMINIO=$(dig +short A "$DOMINIO" | tail -1 || true)
if [[ -n "$IP_SERVIDOR" && "$IP_SERVIDOR" != "$IP_DOMINIO" ]]; then
  echo "AVISO: $DOMINIO apunta a '${IP_DOMINIO:-nada}' y este servidor es $IP_SERVIDOR."
  echo "Cree en su DNS un registro A: $DOMINIO -> $IP_SERVIDOR. Sin eso no habrá HTTPS (se reintenta solo cuando el DNS esté listo)."
fi
if command -v ufw >/dev/null && ufw status | grep -q "Status: active"; then ufw allow 80/tcp; ufw allow 443/tcp; fi

echo "=== 4/5 Construyendo e iniciando (puede tardar unos minutos) ==="
docker compose --env-file .env up -d --build

echo "=== 5/5 Respaldo diario de la base de datos (3:00 a.m.) ==="
chmod +x respaldar.sh actualizar.sh
echo "0 3 * * * root $(pwd)/respaldar.sh >> /var/log/tibolivia-respaldo.log 2>&1" > /etc/cron.d/tibolivia-respaldo

echo ""
echo "======================================================================"
echo " Sistema:            https://$DOMINIO"
echo " Usuario:            ${ADMIN_EMAIL}  (la contraseña que eligió)"
echo " Privacidad (Meta):  https://$DOMINIO/privacidad"
if [[ "${WHATSAPP_PROVEEDOR}" == "TextMeBot" ]]; then
  echo " Webhook TextMeBot:  https://$DOMINIO/webhooks/textmebot?token=${TEXTMEBOT_TOKEN_WEBHOOK}"
else
  echo " Webhook Meta:       https://$DOMINIO/webhooks/whatsapp"
  echo " Verify Token:       ${WHATSAPP_VERIFY_TOKEN:-}"
fi
echo " Ver registros:      cd $(pwd) && docker compose logs -f app"
echo "======================================================================"
