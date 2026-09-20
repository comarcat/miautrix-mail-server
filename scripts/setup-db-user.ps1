<#
.SYNOPSIS
    Configures PostgreSQL user, databases, and grants schema public ownership.
.DESCRIPTION
    Fixes PostgreSQL 15+ "ERROR 42501: permission denied for schema public" by granting
    ownership of the database and schema public to 'mmdb-user'.
    Can run via SSH to the PostgreSQL host (10.11.1.52) or directly via local psql.
.PARAMETER DbHost
    PostgreSQL server host IP. Default: 10.11.1.52
.PARAMETER SshUser
    SSH user on the PostgreSQL server. Default: root
.PARAMETER Method
    Execution method: 'SSH' (default) or 'Psql'.
#>
param (
    [string]$DbHost = "10.11.1.52",
    [string]$SshUser = "root",
    [ValidateSet("SSH", "Psql")]
    [string]$Method = "SSH"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Miautrix Mail Server - Database User & Permissions Setup" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$ScriptDir = $PSScriptRoot
$SqlFile = Join-Path $ScriptDir "init-db-permissions.sql"

if (-not (Test-Path $SqlFile)) {
    Write-Error "SQL script not found at: $SqlFile"
    exit 1
}

if ($Method -eq "SSH") {
    Write-Host "[1/2] Copying init-db-permissions.sql to $($SshUser)@$($DbHost)..." -ForegroundColor Yellow
    scp "$SqlFile" "$($SshUser)@$($DbHost):/tmp/init-db-permissions.sql"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to copy SQL file to $($DbHost)"
        exit $LASTEXITCODE
    }

    Write-Host "[2/2] Executing SQL script as postgres superuser on $($DbHost)..." -ForegroundColor Yellow
    ssh "$($SshUser)@$($DbHost)" "sudo -u postgres psql -f /tmp/init-db-permissions.sql && rm -f /tmp/init-db-permissions.sql"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to execute PostgreSQL permissions setup"
        exit $LASTEXITCODE
    }
} else {
    Write-Host "Executing SQL script via local psql against $($DbHost)..." -ForegroundColor Yellow
    psql -h $DbHost -U postgres -f "$SqlFile"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to execute PostgreSQL permissions setup via psql"
        exit $LASTEXITCODE
    }
}

Write-Host ""
Write-Host "PostgreSQL user 'mmdb-user' and schema permissions configured successfully!" -ForegroundColor Green
Write-Host "You can now run ./scripts/init-database-scratch.ps1 or ./scripts/update-database.ps1" -ForegroundColor Cyan
