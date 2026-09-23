#!/usr/bin/env bash
# ==============================================================================
# Miautrix Mail Server - Production Database Migration Script
# ==============================================================================
# Uses an explicit production connection string so dotnet-ef cannot silently use
# the local/dev database from appsettings or shell defaults.
#
# Usage options:
#   MIAUTRIX_PROD_DB_CONNECTION='Host=10.11.1.52;Port=5432;Database=miautrix-mail-pro;Username=mmdb-user;Password=...' \
#     ./scripts/update-prod-database.sh
#
#   MIAUTRIX_PROD_DB_PASSWORD='...' ./scripts/update-prod-database.sh
#
#   ./scripts/update-prod-database.sh --connection 'Host=...;Port=5432;Database=miautrix-mail-pro;Username=...;Password=...'
#
# Add --seed to run the seeder after migrations.
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"

DB_HOST="${MIAUTRIX_PROD_DB_HOST:-10.11.1.52}"
DB_PORT="${MIAUTRIX_PROD_DB_PORT:-5432}"
DB_NAME="${MIAUTRIX_PROD_DB_NAME:-miautrix-mail-pro}"
DB_USER="${MIAUTRIX_PROD_DB_USER:-mmdb-user}"
CONNECTION_STRING="${MIAUTRIX_PROD_DB_CONNECTION:-}"
RUN_SEED=false
ASSUME_YES=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --connection)
      CONNECTION_STRING="${2:?--connection requires a value}"
      shift 2
      ;;
    --host)
      DB_HOST="${2:?--host requires a value}"
      shift 2
      ;;
    --port)
      DB_PORT="${2:?--port requires a value}"
      shift 2
      ;;
    --database)
      DB_NAME="${2:?--database requires a value}"
      shift 2
      ;;
    --user)
      DB_USER="${2:?--user requires a value}"
      shift 2
      ;;
    --seed)
      RUN_SEED=true
      shift
      ;;
    -y|--yes)
      ASSUME_YES=true
      shift
      ;;
    -h|--help)
      sed -n '1,28p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if [[ -z "${CONNECTION_STRING}" ]]; then
  if [[ -z "${MIAUTRIX_PROD_DB_PASSWORD:-}" ]]; then
    echo "Missing production DB password." >&2
    echo "Set MIAUTRIX_PROD_DB_CONNECTION or MIAUTRIX_PROD_DB_PASSWORD." >&2
    exit 2
  fi

  CONNECTION_STRING="Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${MIAUTRIX_PROD_DB_PASSWORD}"
fi

if [[ "${CONNECTION_STRING}" =~ Database=([^;]+) ]]; then
  TARGET_DB="${BASH_REMATCH[1]}"
else
  echo "Connection string must include Database=..." >&2
  exit 2
fi

if [[ "${CONNECTION_STRING}" =~ Host=([^;]+) ]]; then
  TARGET_HOST="${BASH_REMATCH[1]}"
else
  echo "Connection string must include Host=..." >&2
  exit 2
fi

if [[ "${TARGET_DB}" == *dev* || "${TARGET_DB}" == *test* || "${TARGET_HOST}" == "localhost" || "${TARGET_HOST}" == "127.0.0.1" ]]; then
  echo "Refusing to run production migration against suspicious target: ${TARGET_HOST}/${TARGET_DB}" >&2
  exit 3
fi

echo "=========================================================="
echo "  Miautrix Mail Server - PRODUCTION Database Update"
echo "=========================================================="
echo "Target: ${TARGET_HOST}/${TARGET_DB}"
echo "Seeder: ${RUN_SEED}"
echo ""

if [[ "${ASSUME_YES}" != "true" ]]; then
  read -r -p "Type the database name '${TARGET_DB}' to apply migrations: " CONFIRM_DB
  if [[ "${CONFIRM_DB}" != "${TARGET_DB}" ]]; then
    echo "Confirmation did not match. Aborting." >&2
    exit 4
  fi
fi

cd "${REPO_ROOT}"
export ASPNETCORE_ENVIRONMENT=Production
export MIAUTRIX_DB_CONNECTION="${CONNECTION_STRING}"

echo "[1/2] Applying EF Core migrations with explicit --connection..."
dotnet ef database update \
  --project src/Miautrix.Mail.Persistence \
  --startup-project src/Miautrix.Mail.Web \
  --connection "${CONNECTION_STRING}"

if [[ "${RUN_SEED}" == "true" ]]; then
  echo "[2/2] Running seeder against production connection..."
  dotnet run --project src/Miautrix.Mail.Seeder
else
  echo "[2/2] Seeder skipped. Use --seed if required."
fi

if command -v psql >/dev/null 2>&1; then
  DB_PORT_VALUE="$(sed -n 's/.*Port=\([^;]*\).*/\1/p' <<<"${CONNECTION_STRING}")"
  DB_USER_VALUE="$(sed -n 's/.*Username=\([^;]*\).*/\1/p' <<<"${CONNECTION_STRING}")"
  DB_PASSWORD_VALUE="$(sed -n 's/.*Password=\([^;]*\).*/\1/p' <<<"${CONNECTION_STRING}")"

  echo "Verifying tenants.mfa_enforced exists..."
  PGPASSWORD="${DB_PASSWORD_VALUE}" \
    psql "host=${TARGET_HOST} port=${DB_PORT_VALUE} dbname=${TARGET_DB} user=${DB_USER_VALUE}" \
    -tAc "select column_name from information_schema.columns where table_name='tenants' and column_name='mfa_enforced';" | grep -qx "mfa_enforced"
else
  echo "psql not found; skipping direct column verification."
fi

echo "Production database update completed successfully."
