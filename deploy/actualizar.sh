#!/usr/bin/env bash
# Trae la última versión desde GitHub y reinicia el sistema sin perder datos.
# Uso: sudo bash deploy/actualizar.sh
set -euo pipefail
cd "$(dirname "$0")"
./respaldar.sh
git -C .. pull --ff-only
docker compose --env-file .env up -d --build
docker image prune -f >/dev/null
echo "Sistema actualizado."
