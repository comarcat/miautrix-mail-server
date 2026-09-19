# Miautrix Mail Server - Automated Windows Deploy Script
# Usage: .\deploy.ps1 [-TargetIp 10.11.1.51] [-DbHost 10.11.1.52] [-DbName miautrix-mail-pro]

param (
    [string]$TargetIp = "10.11.1.51",
    [string]$DbHost = "10.11.1.52",
    [string]$DbName = "miautrix-mail-pro",
    [string]$DbUser = "mmdb-user",
    [string]$DbPass = "Mi@usito#2026!"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Miautrix Mail Server - Build & Deploy Pipeline" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$PublishDir = Join-Path $PSScriptRoot "publish"
$AppDir = Join-Path $PublishDir "app"
$AdminDir = Join-Path $PublishDir "admin"
$WebmailDir = Join-Path $PublishDir "webmail"

# Clean publish directories
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

# 1. Publish Self-Contained .NET 10 Web Application
Write-Host "[1/4] Publishing .NET 10 backend (Self-Contained linux-x64)..." -ForegroundColor Yellow
dotnet publish src/Miautrix.Mail.Web/Miautrix.Mail.Web.csproj -c Release -r linux-x64 --self-contained true -o $AppDir

# 2. Build Admin Frontend
Write-Host "[2/4] Building Admin Frontend (React)..." -ForegroundColor Yellow
pnpm --filter admin build
New-Item -ItemType Directory -Force -Path $AdminDir | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot "admin/dist/*") -Destination $AdminDir -Recurse -Force

# 3. Build Webmail Frontend
Write-Host "[3/4] Building Webmail Frontend (React)..." -ForegroundColor Yellow
pnpm --filter webmail build
New-Item -ItemType Directory -Force -Path $WebmailDir | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot "webmail/dist/*") -Destination $WebmailDir -Recurse -Force

# 4. Copy to Target Container via SCP
Write-Host "[4/4] Deploying to LXC container ($TargetIp)..." -ForegroundColor Green
Write-Host "Please enter the root password for $TargetIp when prompted:" -ForegroundColor Cyan

Write-Host " -> Copying Backend Application..."
scp -r (Join-Path $AppDir "*") "root@${TargetIp}:/opt/miautrix-mail/app/"

Write-Host " -> Copying Admin Console..."
scp -r (Join-Path $AdminDir "*") "root@${TargetIp}:/opt/miautrix-mail/admin/"

Write-Host " -> Copying Webmail Client..."
scp -r (Join-Path $WebmailDir "*") "root@${TargetIp}:/opt/miautrix-mail/webmail/"

Write-Host " -> Setting permissions and restarting services..."
ssh "root@${TargetIp}" "chmod +x /opt/miautrix-mail/app/Miautrix.Mail.Web && chown -R www-data:www-data /opt/miautrix-mail && systemctl restart miautrix-mail nginx"

Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host "Verify at: https://mail.miautrix.tech" -ForegroundColor Cyan
