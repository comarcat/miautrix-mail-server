<#
.SYNOPSIS
    Builds and uploads the Webmail and Admin web applications to the Miautrix Mail Server.
.DESCRIPTION
    Compiles Admin (React/Vite) and Webmail (React/Vite), transfers the static assets
    via SCP to /opt/miautrix-mail/admin/ and /opt/miautrix-mail/webmail/, sets ownership,
    and reloads nginx on the target server.
.PARAMETER TargetIp
    Target LXC/VM IP address. Default: 10.11.1.51
.PARAMETER TargetUser
    Target SSH username. Default: root
.PARAMETER SkipBuild
    Skip running `pnpm build` if dist artifacts are already fresh.
#>
param (
    [string]$TargetIp = "10.11.1.51",
    [string]$TargetUser = "root",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Miautrix Mail Server - Web Applications Deploy Script" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$PublishDir = Join-Path $RepoRoot "publish"
$AdminDir = Join-Path $PublishDir "admin"
$WebmailDir = Join-Path $PublishDir "webmail"

# 1. Build Frontends
if (-not $SkipBuild) {
    Write-Host "[1/3] Building Admin Console (React)..." -ForegroundColor Yellow
    Push-Location (Join-Path $RepoRoot "admin")
    pnpm build
    Pop-Location

    Write-Host "[2/3] Building Webmail Client (React)..." -ForegroundColor Yellow
    Push-Location (Join-Path $RepoRoot "webmail")
    pnpm build
    Pop-Location
} else {
    Write-Host "Skipping build steps (-SkipBuild specified)..." -ForegroundColor Yellow
}

# Stage build outputs
New-Item -ItemType Directory -Force -Path $AdminDir | Out-Null
New-Item -ItemType Directory -Force -Path $WebmailDir | Out-Null

Copy-Item -Path (Join-Path $RepoRoot "admin/dist/*") -Destination $AdminDir -Recurse -Force
Copy-Item -Path (Join-Path $RepoRoot "webmail/dist/*") -Destination $WebmailDir -Recurse -Force

# 2. Upload via SCP
Write-Host "[3/3] Uploading websites to $TargetUser@${TargetIp}..." -ForegroundColor Green

Write-Host (" -> Syncing Admin Console to /opt/miautrix-mail/admin/...")
scp -r (Join-Path $AdminDir "*") "$($TargetUser)@$($TargetIp):/opt/miautrix-mail/admin/"

Write-Host (" -> Syncing Webmail Client to /opt/miautrix-mail/webmail/...")
scp -r (Join-Path $WebmailDir "*") "$($TargetUser)@$($TargetIp):/opt/miautrix-mail/webmail/"

Write-Host (" -> Setting ownership and reloading Nginx...")
ssh "$($TargetUser)@$($TargetIp)" "chown -R www-data:www-data /opt/miautrix-mail/admin /opt/miautrix-mail/webmail && systemctl reload nginx"

Write-Host ""
Write-Host "Website deployment completed successfully!" -ForegroundColor Green
Write-Host "  Admin Console: https://admin.miautrix.tech (or https://${TargetIp}/admin)" -ForegroundColor Cyan
Write-Host "  Webmail:       https://mail.miautrix.tech  (or https://${TargetIp})" -ForegroundColor Cyan
