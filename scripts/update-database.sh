#!/usr/bin/env bash
# ==============================================================================
# Miautrix Mail Server - Database Update & Migration Script (Bash)
# ==============================================================================
set -euo pipefail

DB_HOST="${1:-10.11.1.52}"
DB_PORT="${2:-5432}"
DB_NAME="${3:-miautrix-mail-pro}"
DB_USER="${4:-mmdb-user}"
DB_PASS="${5:-Mi@usito#2026!}"

echo "=========================================================="
echo "  Miautrix Mail Server - Database Update & Migration"
echo "=========================================================="

CONNECTION_STRING="Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}"
export MIAUTRIX_DB_CONNECTION="${CONNECTION_STRING}"

echo "[1/2] Applying EF Core schema migrations to ${DB_HOST}/${DB_NAME}..."
dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --connection "${CONNECTION_STRING}"

echo "[2/2] Running database seeder..."
dotnet run --project src/Miautrix.Mail.Seeder

echo ""
echo "Database migration and seeding completed successfully!"
