#!/usr/bin/env bash
# Respaldo comprimido de la base de datos; conserva los últimos 14 días.
# Restaurar: gunzip -c respaldos/ARCHIVO.sql.gz | docker compose exec -T db psql -U tibolivia -d tibolivia_prospeccion
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p ../respaldos
archivo="../respaldos/tibolivia_$(date +%Y%m%d_%H%M).sql.gz"
docker compose --env-file .env exec -T db pg_dump -U tibolivia -d tibolivia_prospeccion | gzip > "$archivo"
find ../respaldos -name 'tibolivia_*.sql.gz' -mtime +14 -delete
echo "Respaldo creado: $archivo"
