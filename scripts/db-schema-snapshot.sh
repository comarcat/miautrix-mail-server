#!/usr/bin/env bash
# Read-only schema fingerprint of the database in /opt/miautrix-mail/.env (or --env-file <file>).
# Prints one sorted line per migration/column/constraint/index; no row data, no credentials.
# Usage (app server, root): bash db-schema-snapshot.sh [--env-file <path>] > /tmp/schema-<name>.txt
# Needs db-schema-snapshot.sql next to it. Compare two outputs with: diff -u a.txt b.txt
set -Eeuo pipefail
ENV_FILE=${MIAUTRIX_ROOT:-/opt/miautrix-mail}/.env
[[ ${1:-} == --env-file ]] && ENV_FILE=${2:?--env-file needs a path}
SQL="$(dirname "$0")/db-schema-snapshot.sql"
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
command -v psql >/dev/null || die 'psql is required (apt install postgresql-client-18)'
[[ -f "$SQL" ]] || die "missing $SQL"
[[ -f "$ENV_FILE" ]] || die "missing $ENV_FILE"
line=$(sed -nE 's/^MIAUTRIX_DB_CONNECTION=(.*)$/\1/p' "$ENV_FILE" | tail -n1 | tr -d '\r')
line=${line#\"}; line=${line%\"}; line=${line#\'}; line=${line%\'}
[[ -n "$line" ]] || die 'MIAUTRIX_DB_CONNECTION is missing'
val() { printf '%s' "$line" | tr ';' '\n' | sed -nE "s/^[[:space:]]*$1[[:space:]]*=(.*)$/\1/Ip" | head -n1; }
passfile=$(mktemp); trap 'rm -f "$passfile"' EXIT; chmod 600 "$passfile"
esc() { printf '%s' "$1" | sed -e 's/\/\\/g' -e 's/:/\:/g'; }
host=$(val host); port=$(val port); port=${port:-5432}; db=$(val database); user=$(val 'user(name)?')
printf '%s:%s:%s:%s:%s\n' "$(esc "$host")" "$port" "$(esc "$db")" "$(esc "$user")" "$(esc "$(val password)")" > "$passfile"
unset line
PGPASSFILE=$passfile PGOPTIONS='-c default_transaction_read_only=on' \
  psql -X -At -v ON_ERROR_STOP=1 -h "$host" -p "$port" -d "$db" -U "$user" -f "$SQL"
