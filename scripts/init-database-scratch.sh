#!/usr/bin/env bash
# ==============================================================================
# Miautrix Mail Server - Greenfield Database Deployment Script (Bash)
# ==============================================================================
set -euo pipefail

DB_HOST="${1:-10.11.1.52}"
DB_PORT="${2:-5432}"
DB_NAME="${3:-miautrix-mail-pro}"
DB_USER="${4:-mmdb-user}"
DB_PASS="${5:-Mi@usito#2026!}"
DROP_EXISTING="${6:-false}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

echo "=========================================================="
echo "  Miautrix Mail Server - Greenfield Database Deployment"
echo "=========================================================="
echo "Target Database Host: ${DB_HOST}:${DB_PORT}"
echo "Target Database Name: ${DB_NAME}"
echo "Target User:          ${DB_USER}"

CONNECTION_STRING="Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}"
export MIAUTRIX_DB_CONNECTION="${CONNECTION_STRING}"

cd "${REPO_ROOT}"

if [ "${DROP_EXISTING}" = "true" ] || [ "${DROP_EXISTING}" = "--drop" ]; then
    echo "[1/4] Dropping existing database '${DB_NAME}'..."
    dotnet ef database drop --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --connection "${CONNECTION_STRING}" --force || true
else
    echo "[1/4] Skipping drop step (using existing or clean database)..."
fi

echo "[2/4] Applying Entity Framework Core migrations from scratch..."
dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --connection "${CONNECTION_STRING}"

echo "[3/4] Running Miautrix Seeder for bootstrap entities..."
dotnet run --project src/Miautrix.Mail.Seeder

echo ""
echo "[4/4] Greenfield Database Deployment Completed!"
echo "=========================================================="
echo "  Database:             ${DB_NAME} (${DB_HOST}:${DB_PORT})"
echo "  Default Tenant:       default"
echo "  Default Domain:       miautrix.org (Verified, Primary)"
echo "  Initial Admin User:   admin@miautrix.org"
echo "  Initial Password:     CH@nGEm3! (Must change on first login)"
echo "  Standard Mailbox:     admin@miautrix.org"
echo "=========================================================="
