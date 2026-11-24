#!/usr/bin/env bash
# Read-only schema fingerprint of the database in /opt/miautrix-mail/.env (or --env-file <file>).
# Prints one sorted line per migration/column/constraint/index; no row data, no credentials.
# Never prompts for a password (psql -w): a parsing problem fails instead of echoing input.
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
connection_value() {
  local input=$1 wanted=$2 key value rest quote ch
  while [[ -n "$input" ]]; do
    key=${input%%=*}; [[ "$input" == *=* ]] || break; rest=${input#*=}; value=; quote=
    while [[ -n "$rest" ]]; do
      ch=${rest:0:1}; rest=${rest:1}
      if [[ -n "$quote" ]]; then if [[ "$ch" == "$quote" ]]; then quote=; else value+=$ch; fi
      elif [[ "$ch" == "'" || "$ch" == '"' ]]; then quote=$ch
      elif [[ "$ch" == ';' ]]; then break
      else value+=$ch; fi
    done
    key=$(printf '%s' "$key" | tr '[:upper:]' '[:lower:]' | tr -d ' ')
    case "$key" in
      host|server) [[ "$wanted" == host ]] && { printf '%s' "$value"; return 0; };;
      port) [[ "$wanted" == port ]] && { printf '%s' "$value"; return 0; };;
      database) [[ "$wanted" == database ]] && { printf '%s' "$value"; return 0; };;
      username|userid|user_id) [[ "$wanted" == username ]] && { printf '%s' "$value"; return 0; };;
      password) [[ "$wanted" == password ]] && { printf '%s' "$value"; return 0; };;
    esac
    input=$rest
  done
  return 1
}
pgpass_escape() { local v=$1; v=${v//\\/\\\\}; v=${v//:/\\:}; printf '%s' "$v"; }
line=$(sed -nE 's/^MIAUTRIX_DB_CONNECTION=(.*)$/\1/p' "$ENV_FILE" | tail -n1 | tr -d '\r')
line=${line#\"}; line=${line%\"}; line=${line#\'}; line=${line%\'}
[[ -n "$line" ]] || die 'MIAUTRIX_DB_CONNECTION is missing'
DB_HOST=$(connection_value "$line" host) || die 'Host missing'
DB_PORT=$(connection_value "$line" port || printf '5432')
DB_NAME=$(connection_value "$line" database) || die 'Database missing'
DB_USER=$(connection_value "$line" username) || die 'Username missing'
DB_PASSWORD=$(connection_value "$line" password) || die 'Password missing'
unset line
passfile=$(mktemp); trap 'rm -f "$passfile"' EXIT; chmod 600 "$passfile"
printf '%s:%s:%s:%s:%s\n' "$(pgpass_escape "$DB_HOST")" "$DB_PORT" "$(pgpass_escape "$DB_NAME")" "$(pgpass_escape "$DB_USER")" "$(pgpass_escape "$DB_PASSWORD")" > "$passfile"
unset DB_PASSWORD
export PGPASSFILE=$passfile PGHOST=$DB_HOST PGPORT=$DB_PORT PGDATABASE=$DB_NAME PGUSER=$DB_USER PGOPTIONS='-c default_transaction_read_only=on'
psql -X -w -At -v ON_ERROR_STOP=1 -f "$SQL" </dev/null
