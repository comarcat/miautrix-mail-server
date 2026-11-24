#!/usr/bin/env bash
# Remove test-fixture tenants that automated tests left in a database.
#
# Usage (on the app server, as root):
#   bash cleanup-test-tenants.sh --list                      # review + rehearsal, always ROLLBACK
#   bash cleanup-test-tenants.sh --apply --expect <count>    # pg_dump first, then delete and COMMIT
#
# A tenant is a candidate only when its slug matches a test-fixture pattern
# (prefix followed by a GUID, or the fixed fixture slugs 'raw-test' / 'acme')
# and it is not listed in --keep (the production tenant is always kept).
# Deletion is generic: every table with a tenant_id column, plus child tables
# without tenant_id that reference those rows through a single-column FK, are
# cleared in FK-safe order inside ONE transaction. Any failure rolls back all of it.
set -Eeuo pipefail

ROOT=${MIAUTRIX_ROOT:-/opt/miautrix-mail}
BACKUPS="$ROOT/backups"
KEEP='c1b615a2-b437-b25d-8aed-61bb68749c8b'
MODE=list
EXPECT=

die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
usage() { sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }

while (($#)); do
  case "$1" in
    --list) MODE=list; shift;;
    --apply) MODE=apply; shift;;
    --expect) [[ -n ${2:-} ]] || die '--expect needs a number'; EXPECT=$2; shift 2;;
    --keep) [[ -n ${2:-} ]] || die '--keep needs a tenant id'; KEEP="$KEEP,$2"; shift 2;;
    -h|--help) usage;;
    *) die "unknown option $1";;
  esac
done
[[ "$MODE" == list || "$EXPECT" =~ ^[0-9]+$ ]] || die '--apply requires --expect <count> (the candidate count shown by --list)'
for t in psql pg_dump pg_restore; do command -v "$t" >/dev/null || die "$t is required (apt install postgresql-client-18)"; done
[[ $EUID -eq 0 ]] || die 'run as root'

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
export PGPASSFILE=$passfile PGHOST=$DB_HOST PGPORT=$DB_PORT PGDATABASE=$DB_NAME PGUSER=$DB_USER

if [[ "$MODE" == apply ]]; then
  mkdir -p "$BACKUPS"; chmod 700 "$BACKUPS"
  dump="$BACKUPS/$(date -u +%Y%m%dT%H%M%SZ)_before-tenant-cleanup.dump"
  printf '== taking database dump %s ==\n' "$dump"
  pg_dump -Fc --no-owner --file="$dump"
  pg_restore --list "$dump" >/dev/null
  chmod 600 "$dump"
  printf '== dump verified (%s bytes) ==\n' "$(stat -c %s "$dump")"
fi

psql -X -q -v ON_ERROR_STOP=1 -v keep="$KEEP" -v expect="${EXPECT:-0}" -v apply="$([[ $MODE == apply ]] && echo true || echo false)" <<'SQL'
\pset footer off
BEGIN;
CREATE TEMP TABLE doomed (id uuid PRIMARY KEY) ON COMMIT DROP;
INSERT INTO doomed
SELECT t.id FROM tenants t
WHERE t.id <> ALL (string_to_array(:'keep', ',')::uuid[])
  AND (t.slug ~ '-[0-9a-f]{32}$'
       OR t.slug ~ '-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR t.slug IN ('raw-test', 'acme'));

\echo
\echo '== tenants KEPT (not touched) =='
SELECT t.id, t.slug, t.created_at::date AS created,
       (SELECT count(*) FROM mailboxes x WHERE x.tenant_id = t.id) AS mailboxes,
       (SELECT count(*) FROM messages x WHERE x.tenant_id = t.id) AS messages,
       (SELECT string_agg(d.name, ', ') FROM domains d WHERE d.tenant_id = t.id) AS domains
FROM tenants t WHERE t.id NOT IN (SELECT id FROM doomed) ORDER BY t.created_at;

\echo '== candidate test tenants by slug prefix =='
SELECT regexp_replace(t.slug, '-[0-9a-f-]{32,36}$', '-<guid>') AS slug_pattern, count(*) AS tenants,
       min(t.created_at)::date AS first, max(t.created_at)::date AS last,
       sum((SELECT count(*) FROM messages x WHERE x.tenant_id = t.id)) AS messages,
       sum((SELECT count(*) FROM mailboxes x WHERE x.tenant_id = t.id)) AS mailboxes
FROM tenants t JOIN doomed USING (id) GROUP BY 1 ORDER BY 2 DESC;

SELECT count(*) AS candidate_count FROM doomed \gset
\echo '== candidate tenants:' :candidate_count '=='

DO $$
DECLARE r record; pass int; n bigint; blocked int; progress boolean; total bigint := 0;
BEGIN
  FOR pass IN 1..25 LOOP
    progress := false; blocked := 0;
    -- Child tables without tenant_id that reference tenant-scoped rows (single-column FKs).
    FOR r IN
      SELECT c.conrelid::regclass AS child, a.attname AS col, c.confrelid::regclass AS parent, pa.attname AS pcol
      FROM pg_constraint c
      JOIN pg_attribute a  ON a.attrelid = c.conrelid  AND a.attnum = c.conkey[1]
      JOIN pg_attribute pa ON pa.attrelid = c.confrelid AND pa.attnum = c.confkey[1]
      WHERE c.contype = 'f' AND array_length(c.conkey, 1) = 1
        AND c.connamespace = 'public'::regnamespace
        AND NOT EXISTS (SELECT 1 FROM pg_attribute x WHERE x.attrelid = c.conrelid AND x.attname = 'tenant_id' AND NOT x.attisdropped)
        AND (c.confrelid = 'tenants'::regclass
             OR EXISTS (SELECT 1 FROM pg_attribute x WHERE x.attrelid = c.confrelid AND x.attname = 'tenant_id' AND NOT x.attisdropped))
    LOOP
      BEGIN
        IF r.parent = 'tenants'::regclass THEN
          EXECUTE format('DELETE FROM %s WHERE %I IN (SELECT id FROM doomed)', r.child, r.col);
        ELSE
          EXECUTE format('DELETE FROM %s WHERE %I IN (SELECT %I FROM %s WHERE tenant_id IN (SELECT id FROM doomed))', r.child, r.col, r.pcol, r.parent);
        END IF;
        GET DIAGNOSTICS n = ROW_COUNT;
        IF n > 0 THEN progress := true; total := total + n; RAISE NOTICE '%: % rows', r.child, n; END IF;
      EXCEPTION WHEN foreign_key_violation THEN blocked := blocked + 1;
      END;
    END LOOP;
    -- Tenant-scoped tables.
    FOR r IN
      SELECT c.oid::regclass AS tbl FROM pg_class c
      JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
      WHERE c.relkind IN ('r', 'p') AND c.relnamespace = 'public'::regnamespace
    LOOP
      BEGIN
        EXECUTE format('DELETE FROM %s WHERE tenant_id IN (SELECT id FROM doomed)', r.tbl);
        GET DIAGNOSTICS n = ROW_COUNT;
        IF n > 0 THEN progress := true; total := total + n; RAISE NOTICE '%: % rows', r.tbl, n; END IF;
      EXCEPTION WHEN foreign_key_violation THEN blocked := blocked + 1;
      END;
    END LOOP;
    EXIT WHEN blocked = 0;
    IF NOT progress THEN RAISE EXCEPTION 'foreign keys still block % table(s); nothing was deleted', blocked; END IF;
  END LOOP;
  DELETE FROM tenants WHERE id IN (SELECT id FROM doomed);
  GET DIAGNOSTICS n = ROW_COUNT;
  RAISE NOTICE 'tenants: % rows (plus % dependent rows)', n, total;
END $$;

\if :apply
  SELECT (:candidate_count = :expect) AS count_ok \gset
  \if :count_ok
    COMMIT;
    \echo '== COMMITTED: test tenants deleted =='
  \else
    ROLLBACK;
    \echo '== ROLLED BACK: candidate count does not match --expect; nothing deleted =='
    DO $$ BEGIN RAISE EXCEPTION 'candidate count mismatch'; END $$;
  \endif
\else
  ROLLBACK;
  \echo '== REHEARSAL ONLY: rolled back, nothing deleted. Apply with: --apply --expect' :candidate_count '=='
\endif
SQL
