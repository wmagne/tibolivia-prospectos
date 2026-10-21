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
