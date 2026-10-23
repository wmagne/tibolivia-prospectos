#!/usr/bin/env bash
# Cambia el correo (y usuario de ingreso) de una cuenta del Sistema de Prospección, sin tocar nada más.
# Uso (root):  bash cambiar-correo-admin.sh correo-actual@tibolivia.com correo-nuevo@tibolivia.com
set -euo pipefail
ACTUAL="${1:?Indique el correo actual}"; NUEVO="${2:?Indique el correo nuevo}"
[[ "$NUEVO" =~ ^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$ ]] || { echo "Correo nuevo inválido: $NUEVO"; exit 1; }
DB=tibolivia-prospectos-db
sqlv() { docker exec -i "$DB" psql -v ON_ERROR_STOP=1 -U tibolivia -d tibolivia_prospeccion -tA "$@"; }
EXISTE=$(sqlv -v c="$ACTUAL" <<<'SELECT count(*) FROM "AspNetUsers" WHERE "NormalizedEmail" = upper(:'"'"'c'"'"');')
[[ "$EXISTE" == 1 ]] || { echo "No existe un usuario con el correo $ACTUAL."; exit 1; }
OCUPADO=$(sqlv -v n="$NUEVO" <<<'SELECT count(*) FROM "AspNetUsers" WHERE "NormalizedEmail" = upper(:'"'"'n'"'"');')
[[ "$OCUPADO" == 0 ]] || { echo "Ya existe un usuario con el correo $NUEVO."; exit 1; }
docker exec "$DB" pg_dump -U tibolivia -t '"AspNetUsers"' tibolivia_prospeccion | gzip > "/var/backups/tibolivia-prospectos/usuarios_antes_$(date +%F_%H%M).sql.gz"
sqlv -v c="$ACTUAL" -v n="$NUEVO" <<'SQL'
UPDATE "AspNetUsers"
   SET "Email" = :'n', "NormalizedEmail" = upper(:'n'),
       "UserName" = :'n', "NormalizedUserName" = upper(:'n'),
       "SecurityStamp" = md5(random()::text), "ConcurrencyStamp" = md5(random()::text)
 WHERE "NormalizedEmail" = upper(:'c');
SQL
echo "Correo cambiado: $ACTUAL -> $NUEVO (la contraseña no cambia; vuelva a ingresar con el correo nuevo)."
sqlv <<<'SELECT "Email", "NombreCompleto" FROM "AspNetUsers" ORDER BY "Email";'
