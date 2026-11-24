#!/usr/bin/env bash
# Read-only schema/migration inspection of the database configured in /opt/miautrix-mail/.env.
# The session is forced read-only (default_transaction_read_only=on); nothing can be changed.
# Usage (on the app server, as root): bash db-inspect.sh
set -Eeuo pipefail
ROOT=${MIAUTRIX_ROOT:-/opt/miautrix-mail}
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
command -v psql >/dev/null || die 'psql is required (apt install postgresql-client-18)'
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
[[ -f "$ROOT/.env" ]] || die "missing $ROOT/.env"
line=$(sed -nE 's/^MIAUTRIX_DB_CONNECTION=(.*)$/\1/p' "$ROOT/.env" | tail -n1)
line=${line#\"}; line=${line%\"}; line=${line#\'}; line=${line%\'}
[[ -n "$line" ]] || die 'MIAUTRIX_DB_CONNECTION is missing from .env'
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

psql -X -q -v ON_ERROR_STOP=1 <<'SQL'
\pset footer off
\echo '== server / database =='
SELECT current_database() AS database, split_part(version(), ' ', 2) AS server_version, current_setting('default_transaction_read_only') AS read_only;
\echo '== last 8 applied EF migrations =='
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 8;
\echo '== smtp_queue.direction column (AddQueueDirection) =='
SELECT column_name, data_type, is_nullable, character_maximum_length FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'smtp_queue' AND column_name = 'direction';
\echo '== ck_smtp_queue_direction constraint =='
SELECT conname, pg_get_constraintdef(oid) AS definition FROM pg_constraint WHERE conname = 'ck_smtp_queue_direction';
\echo '== smtp_queue.direction values =='
SELECT coalesce(direction, '<NULL>') AS direction, count(*) FROM smtp_queue GROUP BY 1 ORDER BY 1;
\echo '== IMAP-006 objects (expected absent before the UID migration) =='
SELECT conname FROM pg_constraint WHERE conname = 'ck_messages_uid_positive'
UNION ALL SELECT indexname FROM pg_indexes WHERE indexname = 'uq_messages_folder_id_uid';
\echo '== message UID summary =='
SELECT count(*) AS messages, count(*) FILTER (WHERE uid = 0) AS uid_zero, count(DISTINCT folder_id) AS folders FROM messages;
SQL
