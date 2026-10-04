#!/usr/bin/env bash
set -euo pipefail

if [ -z "${MIAUTRIX_DB_CONNECTION:-}" ]; then
  echo "FATAL: MIAUTRIX_DB_CONNECTION environment variable is required." >&2
  exit 1
fi

dotnet ef migrations add InitialCreate --project src/Miautrix.Mail.Persistence --output-dir Migrations
