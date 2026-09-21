<#
.SYNOPSIS
    Applies EF Core migrations to the production Miautrix PostgreSQL database.
.DESCRIPTION
    Uses an explicit production connection string and passes it to dotnet-ef with
    --connection so the command cannot silently use appsettings/dev defaults.
.EXAMPLE
    $env:MIAUTRIX_PROD_DB_PASSWORD = "..."
    ./scripts/update-prod-database.ps1
.EXAMPLE
    ./scripts/update-prod-database.ps1 -ConnectionString "Host=10.11.1.52;Port=5432;Database=miautrix-mail-pro;Username=mmdb-user;Password=..."
#>
param (
    [string]$ConnectionString = $env:MIAUTRIX_PROD_DB_CONNECTION,
    [string]$DbHost = $(if ($env:MIAUTRIX_PROD_DB_HOST) { $env:MIAUTRIX_PROD_DB_HOST } else { "10.11.1.52" }),
    [int]$DbPort = $(if ($env:MIAUTRIX_PROD_DB_PORT) { [int]$env:MIAUTRIX_PROD_DB_PORT } else { 5432 }),
    [string]$DbName = $(if ($env:MIAUTRIX_PROD_DB_NAME) { $env:MIAUTRIX_PROD_DB_NAME } else { "miautrix-mail-pro" }),
    [string]$DbUser = $(if ($env:MIAUTRIX_PROD_DB_USER) { $env:MIAUTRIX_PROD_DB_USER } else { "mmdb-user" }),
    [switch]$Seed,
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    if ([string]::IsNullOrWhiteSpace($env:MIAUTRIX_PROD_DB_PASSWORD)) {
        throw "Set MIAUTRIX_PROD_DB_CONNECTION or MIAUTRIX_PROD_DB_PASSWORD before running production migrations."
    }

    $ConnectionString = "Host=$DbHost;Port=$DbPort;Database=$DbName;Username=$DbUser;Password=$($env:MIAUTRIX_PROD_DB_PASSWORD)"
}

$targetDb = [regex]::Match($ConnectionString, "Database=([^;]+)").Groups[1].Value
$targetHost = [regex]::Match($ConnectionString, "Host=([^;]+)").Groups[1].Value

if ([string]::IsNullOrWhiteSpace($targetDb) -or [string]::IsNullOrWhiteSpace($targetHost)) {
    throw "Connection string must include Host=... and Database=..."
}

if ($targetDb -match "dev|test" -or $targetHost -in @("localhost", "127.0.0.1")) {
    throw "Refusing to run production migration against suspicious target: $targetHost/$targetDb"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Miautrix Mail Server - PRODUCTION Database Update" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target: $targetHost/$targetDb" -ForegroundColor Yellow
Write-Host "Seeder: $Seed" -ForegroundColor Yellow
Write-Host ""

if (-not $Yes) {
    $confirmDb = Read-Host "Type the database name '$targetDb' to apply migrations"
    if ($confirmDb -ne $targetDb) {
        throw "Confirmation did not match. Aborting."
    }
}

Set-Location $RepoRoot
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:MIAUTRIX_DB_CONNECTION = $ConnectionString

Write-Host "[1/2] Applying EF Core migrations with explicit --connection..." -ForegroundColor Yellow
dotnet ef database update `
    --project src/Miautrix.Mail.Persistence `
    --startup-project src/Miautrix.Mail.Web `
    --connection "$ConnectionString"

if ($LASTEXITCODE -ne 0) {
    throw "EF Core migration failed with exit code $LASTEXITCODE"
}

if ($Seed) {
    Write-Host "[2/2] Running seeder against production connection..." -ForegroundColor Yellow
    dotnet run --project src/Miautrix.Mail.Seeder
    if ($LASTEXITCODE -ne 0) {
        throw "Seeder failed with exit code $LASTEXITCODE"
    }
} else {
    Write-Host "[2/2] Seeder skipped. Use -Seed if required." -ForegroundColor Yellow
}

Write-Host "Production database update completed successfully." -ForegroundColor Green
