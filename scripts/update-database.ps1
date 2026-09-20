<#
.SYNOPSIS
    Applies EF Core migrations and seeds the Miautrix Mail Server PostgreSQL database.
.DESCRIPTION
    Runs database schema migrations using dotnet-ef and invokes Miautrix.Mail.Seeder
    to ensure default tenant, domains, permissions, and administrative credentials exist.
.PARAMETER DbHost
    PostgreSQL server hostname or IP address. Default: 10.11.1.52
.PARAMETER DbPort
    PostgreSQL server port. Default: 5432
.PARAMETER DbName
    Database name. Default: miautrix-mail-pro
.PARAMETER DbUser
    Database username. Default: mmdb-user
.PARAMETER DbPass
    Database user password. Default: Mi@usito#2026!
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
Write-Host "  Miautrix Mail Server - Database Update & Migration" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$ConnectionString = "Host=$($DbHost);Port=$($DbPort);Database=$($DbName);Username=$($DbUser);Password=$($DbPass)"
$env:MIAUTRIX_DB_CONNECTION = $ConnectionString

Write-Host "[1/2] Applying EF Core schema migrations to $($DbHost)/$($DbName)..." -ForegroundColor Yellow
dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --connection "$ConnectionString"

if ($LASTEXITCODE -ne 0) {
    Write-Error "EF Core migration failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "[2/2] Running database seeder..." -ForegroundColor Yellow
dotnet run --project src/Miautrix.Mail.Seeder

if ($LASTEXITCODE -ne 0) {
    Write-Error "Database seeding failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Database migration and seeding completed successfully!" -ForegroundColor Green
