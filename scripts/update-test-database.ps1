<#
.SYNOPSIS
    Updates the Miautrix Mail Server PostgreSQL TEST database (migrations + seeder).
.DESCRIPTION
    Safe refresh for the test site/dev DB: runs EF Core migrations and then Miautrix.Mail.Seeder.

    Default targets the test DB name used by integration tests: miautrix-mail-dev.
#>

param (
    [string]$DbHost = "10.11.1.52",
    [int]$DbPort = 5432,
    [string]$DbName = "miautrix-mail-pro",
    [string]$DbUser = "mmdb-user",
    [string]$DbPass = "Mi@usito#2026!"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Miautrix Mail Server - TEST Database Update & Migration" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$ConnectionString = "Host=$($DbHost);Port=$($DbPort);Database=$($DbName);Username=$($DbUser);Password=$($DbPass)"
$env:MIAUTRIX_DB_CONNECTION = $ConnectionString

Write-Host "[1/2] Applying EF Core schema migrations to $($DbHost)/$($DbName)..." -ForegroundColor Yellow
& dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --connection "$ConnectionString"
if ($LASTEXITCODE -ne 0) {
    throw "EF Core migration failed with exit code $LASTEXITCODE"
}

Write-Host "[2/2] Running database seeder..." -ForegroundColor Yellow
& dotnet run --project src/Miautrix.Mail.Seeder
if ($LASTEXITCODE -ne 0) {
    throw "Database seeding failed with exit code $LASTEXITCODE"
}

Write-Host "Verifying domains mfa/session/lockout columns exist (best-effort) ..." -ForegroundColor Yellow
$psql = Get-Command psql -ErrorAction SilentlyContinue
if ($psql) {
    $env:PGPASSWORD = $DbPass

    $needed = @("mfa_enforced", "session_lifetime_minutes", "lockout_max_failed_attempts", "lockout_duration_minutes")
    foreach ($col in $needed) {
        $column = psql "host=$DbHost port=$DbPort dbname=$DbName user=$DbUser" -tAc "select column_name from information_schema.columns where table_name='domains' and column_name='$col';"
        $column = ($column | Select-Object -First 1).Trim()
        if ($column -ne $col) {
            throw "Migration verification failed: domains.$col was not found."
        }
    }

    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
} else {
    Write-Host "psql not found; skipping direct column verification." -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "Test DB migration + seeding completed successfully!" -ForegroundColor Green
