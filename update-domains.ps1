#requires -Version 5.1
<#
.SYNOPSIS
    Builds the Domains feature changes (backend + admin frontend) and reports status.

.DESCRIPTION
    The domain edit/update, real DNS verification, and per-domain SPF/DKIM/DMARC
    generation changes are already applied to the source tree. This script does NOT
    patch source files and does NOT run database migrations or seeders. It only:
      1. Builds the .NET solution with warnings treated as errors.
      2. Builds the admin frontend (TypeScript strict + Vite), unless -SkipFrontend.

.PARAMETER SkipFrontend
    Skip the admin frontend build; build only the .NET solution.

.EXAMPLE
    .\update-domains.ps1
    .\update-domains.ps1 -SkipFrontend
#>
[CmdletBinding()]
param(
    [switch]$SkipFrontend
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $RepoRoot

Write-Host '=== Miautrix Domains: build & verify ===' -ForegroundColor Cyan
Write-Host "Repo root: $RepoRoot"

# 1) Backend build (warnings are errors) --------------------------------------
Write-Host ''
Write-Host '[1/2] Building .NET solution (dotnet build -warnaserror)...' -ForegroundColor Cyan
dotnet build (Join-Path $RepoRoot 'Miautrix.Mail.sln') -warnaserror
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Backend build FAILED.' -ForegroundColor Red
    exit 1
}
Write-Host 'Backend build succeeded.' -ForegroundColor Green

# 2) Frontend build (TS strict + Vite) ----------------------------------------
if ($SkipFrontend) {
    Write-Host ''
    Write-Host '[2/2] Skipping admin frontend build (-SkipFrontend).' -ForegroundColor Yellow
} else {
    Write-Host ''
    Write-Host '[2/2] Building admin frontend (pnpm --filter admin build)...' -ForegroundColor Cyan
    pnpm --filter admin build
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Admin frontend build FAILED.' -ForegroundColor Red
        exit 1
    }
    Write-Host 'Admin frontend build succeeded.' -ForegroundColor Green
}

Write-Host ''
Write-Host '=== Done. Domain changes built successfully. ===' -ForegroundColor Green
Write-Host 'Note: no database migrations or seeders were run.' -ForegroundColor DarkGray
