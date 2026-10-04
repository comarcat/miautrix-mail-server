#!/usr/bin/env bash
set -euo pipefail

DB_HOST="${1:-10.11.1.52}"
DB_PORT="${2:-5432}"
DB_NAME="${3:-miautrix-mail-pro}"
DB_USER="${4:-mmdb-user}"
DB_PASS="${5:-Mi@usito#2026!}"

echo "=========================================================="
echo "  Miautrix Mail Server - TEST Database Update & Migration"
echo "=========================================================="

CONNECTION_STRING="Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}"
export MIAUTRIX_DB_CONNECTION="${CONNECTION_STRING}"

echo "[1/2] Applying EF Core schema migrations to ${DB_HOST}/${DB_NAME}..."
dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --connection "${CONNECTION_STRING}"

echo "[2/2] Running database seeder..."
dotnet run --project src/Miautrix.Mail.Seeder

if command -v psql >/dev/null 2>&1; then
  echo "Verifying domains mfa/session/lockout columns exist (best-effort) ..."
  export PGPASSWORD="${DB_PASS}"
  for col in mfa_enforced session_lifetime_minutes lockout_max_failed_attempts lockout_duration_minutes; do
    got=$(psql "host=${DB_HOST} port=${DB_PORT} dbname=${DB_NAME} user=${DB_USER}" -tAc "select column_name from information_schema.columns where table_name='domains' and column_name='${col}';" | head -n1 | tr -d '[:space:]')
    if [[ "${got}" != "${col}" ]]; then
      echo "Migration verification failed: domains.${col} was not found" >&2
      exit 1
    fi
  done
  unset PGPASSWORD
else
  echo "psql not found; skipping direct column verification."
fi

echo ""
echo "Test DB migration + seeding completed successfully!"
