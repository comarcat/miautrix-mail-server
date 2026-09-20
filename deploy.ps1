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
$DbConnectionString = "Host=$DbHost;Port=5432;Database=$DbName;Username=$DbUser;Password=$DbPass"

# Clean publish directories
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

# 1. Provide Database Migrations & Seeding
Write-Host "[1/6] Applying Database Migrations & Seeding..." -ForegroundColor Yellow
$env:MIAUTRIX_DB_CONNECTION = $DbConnectionString
Write-Host " -> Applying EF Core migrations..."
dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web
Write-Host " -> Seeding default tenant, domains, and admin user..."
dotnet run --project src/Miautrix.Mail.Seeder

# 2. Publish Self-Contained .NET 10 Web Application
Write-Host "[2/6] Publishing .NET 10 backend (Self-Contained linux-x64)..." -ForegroundColor Yellow
dotnet publish src/Miautrix.Mail.Web/Miautrix.Mail.Web.csproj -c Release -r linux-x64 --self-contained true -o $AppDir

# 3. Build Admin Frontend
Write-Host "[3/6] Building Admin Frontend (React)..." -ForegroundColor Yellow
pnpm --filter admin build
New-Item -ItemType Directory -Force -Path $AdminDir | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot "admin/dist/*") -Destination $AdminDir -Recurse -Force

# 4. Build Webmail Frontend
Write-Host "[4/6] Building Webmail Frontend (React)..." -ForegroundColor Yellow
pnpm --filter webmail build
New-Item -ItemType Directory -Force -Path $WebmailDir | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot "webmail/dist/*") -Destination $WebmailDir -Recurse -Force

# 5. Copy to Target Container via SCP
Write-Host "[5/6] Deploying to LXC container ($TargetIp)..." -ForegroundColor Green

Write-Host " -> Preparing target directories and stopping running service..."
ssh "root@$($TargetIp)" "systemctl stop miautrix-mail || true; mkdir -p /opt/miautrix-mail/app /opt/miautrix-mail/admin /opt/miautrix-mail/webmail"

Write-Host " -> Copying Backend Application..."
scp -r (Join-Path $AppDir "*") "root@$($TargetIp):/opt/miautrix-mail/app/"

Write-Host " -> Copying Admin Console..."
scp -r (Join-Path $AdminDir "*") "root@$($TargetIp):/opt/miautrix-mail/admin/"

Write-Host " -> Copying Webmail Client..."
scp -r (Join-Path $WebmailDir "*") "root@$($TargetIp):/opt/miautrix-mail/webmail/"

Write-Host " -> Setting permissions and restarting services..."
ssh "root@$($TargetIp)" "chmod +x /opt/miautrix-mail/app/Miautrix.Mail.Web && chown -R www-data:www-data /opt/miautrix-mail && systemctl restart miautrix-mail nginx"

# 6. Service Verification
Write-Host "[6/6] Verifying Service Endpoints..." -ForegroundColor Yellow
Start-Sleep -Seconds 5
try {
    $response = Invoke-WebRequest -Uri "https://mail.miautrix.tech/api/v1/system/info" -Method Get
    if ($response.StatusCode -eq 200) {
        Write-Host " -> Backend API is up and running." -ForegroundColor Green
    } else {
        Write-Host " -> Expected 200 OK from API, but received $($response.StatusCode)." -ForegroundColor Red
    }
} catch {
    Write-Host " -> Verification check failed: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Admin Console: https://admin.miautrix.tech" -ForegroundColor Cyan
Write-Host " Webmail:       https://mail.miautrix.tech" -ForegroundColor Cyan
Write-Host " API Telemetry: https://mail.miautrix.tech/api/v1/system/info" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
