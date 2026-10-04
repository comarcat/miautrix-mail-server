<#
.SYNOPSIS
    Provisions, migrates, and seeds the Miautrix Mail Server PostgreSQL database from scratch.
.DESCRIPTION
    This script performs a complete greenfield initialization of the Miautrix database:
    1. Validates connection parameters.
    2. (Optional) Drops and recreates the target database if -DropExisting is specified.
    3. Applies all Entity Framework Core schema migrations from scratch.
    4. Executes the Seeder to provision default tenant, domain (miautrix.org), system roles,
       granular permissions, and the initial Administrator account (admin@miautrix.org).
.PARAMETER DbHost
    PostgreSQL server hostname or IP address. Default: 10.11.1.52
.PARAMETER DbPort
    PostgreSQL server port. Default: 5432
.PARAMETER DbName
    Database name to create/initialize. Default: miautrix-mail-pro
.PARAMETER DbUser
    PostgreSQL database user. Default: mmdb-user
.PARAMETER DbPass
    PostgreSQL database password. Default: Mi@usito#2026!
.PARAMETER DropExisting
    If specified, drops the database if it already exists before running migrations.
.PARAMETER Force
    Skips interactive confirmation when -DropExisting is specified.
#>
param (
    [string]$DbHost = "10.11.1.52",
    [int]$DbPort = 5432,
    [string]$DbName = "miautrix-mail-pro",
    [string]$DbUser = "mmdb-user",
    [string]$DbPass = "Mi@usito#2026!",
    [switch]$DropExisting,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Miautrix Mail Server - Greenfield Database Deployment" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$ConnectionString = "Host=$($DbHost);Port=$($DbPort);Database=$($DbName);Username=$($DbUser);Password=$($DbPass)"
$env:MIAUTRIX_DB_CONNECTION = $ConnectionString

Write-Host "Target Database Host: $($DbHost):$($DbPort)" -ForegroundColor DarkCyan
Write-Host "Target Database Name: $($DbName)" -ForegroundColor DarkCyan
Write-Host "Target User:          $($DbUser)" -ForegroundColor DarkCyan

if ($DropExisting) {
    if (-not $Force) {
        $confirmation = Read-Host "WARNING: -DropExisting specified. This will permanently ERASE all data in '$($DbName)' on '$($DbHost)'. Are you sure? (y/N)"
        if ($confirmation -notmatch "^[Yy]$") {
            Write-Host "Database initialization aborted by user." -ForegroundColor Yellow
            exit 0
        }
    }

    Write-Host "[1/4] Dropping existing database '$($DbName)'..." -ForegroundColor Red
    Push-Location $RepoRoot
    dotnet ef database drop --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --connection "$ConnectionString" --force
    Pop-Location
} else {
    Write-Host "[1/4] Skipping drop step (using existing or clean database)..." -ForegroundColor Gray
}

# 2. Apply EF Core Schema Migrations
Write-Host "[2/4] Applying Entity Framework Core migrations from scratch..." -ForegroundColor Yellow
Push-Location $RepoRoot
dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --connection "$ConnectionString"

if ($LASTEXITCODE -ne 0) {
    Pop-Location
    Write-Error "EF Core migration failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}
Pop-Location

# 3. Execute Seeder
Write-Host "[3/4] Running Miautrix Seeder for bootstrap entities..." -ForegroundColor Yellow
Push-Location $RepoRoot
dotnet run --project src/Miautrix.Mail.Seeder

if ($LASTEXITCODE -ne 0) {
    Pop-Location
    Write-Error "Seeder execution failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}
Pop-Location

# 4. Summary and Credentials Notice
Write-Host "[4/4] Greenfield Database Deployment Completed!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Database:             $($DbName) ($($DbHost):$($DbPort))" -ForegroundColor Cyan
Write-Host "  Default Tenant:       default" -ForegroundColor Cyan
Write-Host "  Default Domain:       miautrix.org (Verified, Primary)" -ForegroundColor Cyan
Write-Host "  Initial Admin User:   admin@miautrix.org" -ForegroundColor Cyan
Write-Host "  Initial Password:     CH@nGEm3! (Must change on first login)" -ForegroundColor Cyan
Write-Host "  Standard Mailbox:     admin@miautrix.org (INBOX, Sent, Drafts, Trash, Junk, Archive)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
