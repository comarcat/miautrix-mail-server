#!/usr/bin/env bash
# ==============================================================================
# Miautrix Mail Server - Greenfield Database Deployment Script (Bash)
# ==============================================================================
set -euo pipefail

DB_HOST="${1:-127.0.0.1}"
DB_PORT="${2:-5432}"
DB_NAME="${3:-miautrix-mail-scratch}"
DB_USER="${4:-miautrix_mail_scratch}"
ADMIN_EMAIL="${5:-admin@miautrix.org}"
DB_PASS="${MIAUTRIX_DB_PASSWORD:-}"
if [[ -z "$DB_PASS" ]]; then echo "Set MIAUTRIX_DB_PASSWORD in the environment." >&2; exit 1; fi
if [[ "$DB_HOST" =~ (10\.11\.1\.52|miautrix-mail-pro) || "$DB_NAME" =~ (10\.11\.1\.52|miautrix-mail-pro) ]]; then echo "Scratch initialization refuses the production host/database." >&2; exit 1; fi
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
    dotnet ef database drop --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --force || true
else
    echo "[1/4] Skipping drop step (using existing or clean database)..."
fi

echo "[2/4] Applying Entity Framework Core migrations from scratch..."
dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web

echo "[3/4] Running Miautrix Seeder for bootstrap entities..."
dotnet run --project src/Miautrix.Mail.Seeder

echo "Create the initial admin credential; the password is read through a secure prompt."
DOTNET_ENVIRONMENT=Development dotnet run --project src/Miautrix.Mail.Cli -- users bootstrap-admin --email "$ADMIN_EMAIL"

echo ""
echo "[4/4] Greenfield Database Deployment Completed!"
echo "=========================================================="
echo "  Database:             ${DB_NAME} (${DB_HOST}:${DB_PORT})"
echo "  Default Tenant:       default"
echo "  Default Domain:       miautrix.org (Verified, Primary)"
echo "  Initial Admin User:   ${ADMIN_EMAIL}"
echo "  Initial Password:     entered securely at the CLI prompt; no default password is configured"
echo "  Standard Mailbox:     ${ADMIN_EMAIL}"
echo "=========================================================="
