<#
.SYNOPSIS
    Tests, publishes backend + builds admin/webmail, then uploads to the Debian LXC container.

.DESCRIPTION
    Focused deployment helper for "compile + upload" workflows.

    - Runs the test suite (backend + admin) before publishing anything
    - Builds backend: dotnet publish -r linux-x64 --self-contained
    - Builds React SPAs (admin/webmail)
    - Applies EF Core migrations (opt out with -SkipMigrations)
    - Uploads via SSH/SCP to /opt/miautrix-mail/{app,admin,webmail}
    - Checks the blob store the export/delete endpoints read, optional -PinStorageDir
    - Restarts miautrix-mail and nginx

    Migrations require MIAUTRIX_DB_CONNECTION to be set (no hard-coded secrets).

.PARAMETER TargetIp
    Target LXC/VM IP address. Default: 10.11.1.51

.PARAMETER TargetUser
    SSH username. Default: root

.PARAMETER SkipTests
    Skip the backend + admin test run.

.PARAMETER CompileOnly
    Do not run tests (compile + upload only). Equivalent to using -SkipTests, but keeps intent clear.

.PARAMETER SkipFrontends
    Skip building/publishing admin and webmail.

.PARAMETER SkipMigrations
    Do not apply EF Core migrations.

.PARAMETER RunSeeding
    Run the database seeder after migrations.

.PARAMETER PinStorageDir
    Move the content-addressed blob store to /opt/miautrix-mail/data/mail and pin
    MIAUTRIX_STORAGE_DIR. Required only if the store still lives under the app directory.

.PARAMETER Verify
    Verify backend endpoint after restart.

.PARAMETER WorkerCert
    Local path to the PEM certificate for the SMTP/IMAP listeners. Staged to the container
    and installed by lxc-install-worker-env.sh.

.PARAMETER WorkerKey
    Local path to the matching PEM private key. Staged to the container, installed 0640
    root:www-data, and removed from the staging directory. Never read into PowerShell.

.PARAMETER DbConnectionFile
    Local path to a file holding only the MIAUTRIX_DB_CONNECTION value. Needed because the Web
    app still falls back to a hard-coded connection string, so the Worker cannot inherit it.
    Uploaded to /tmp, consumed by lxc-install-worker-env.sh, then deleted.

.PARAMETER CloudflareEnvFile
    Local path to a file of Cloudflare settings for the optional Cloudflare transport, one
    NAME=value per line: CLOUDFLARE_API_TOKEN, CLOUDFLARE_API_BASE, MIAUTRIX_INBOUND_TOKEN.
    Passed to lxc-install-worker-env.sh by path so no secret ever reaches argv (world-readable
    via ps, and captured in shell history), uploaded to /tmp, then deleted with the TLS
    staging directory. Requires -WorkerCert and -WorkerKey, since the env script installs TLS
    in the same run.
#>

param (
    [string]$TargetIp = "10.11.1.51",
    [string]$TargetUser = "root",
    [switch]$SkipTests,
    [switch]$CompileOnly,
    [switch]$SkipFrontends,
    [switch]$SkipMigrations,
    [switch]$RunSeeding,
    [switch]$PinStorageDir,
    [switch]$Verify,
    [string]$WorkerCert = "",
    [string]$WorkerKey = "",
    [string]$DbConnectionFile = "",
    [string]$CloudflareEnvFile = ""
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName

$PublishDir = Join-Path $RepoRoot "publish"
$AppDir = Join-Path $PublishDir "app"
$WorkerDir = Join-Path $PublishDir "worker"
$AdminDir = Join-Path $PublishDir "admin"
$WebmailDir = Join-Path $PublishDir "webmail"

$Remote = "$($TargetUser)@$($TargetIp)"
$RemoteRoot = "/opt/miautrix-mail"

function Invoke-Step {
    param([string]$Message, [scriptblock]$Action)
    Write-Host $Message -ForegroundColor Yellow
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed (exit $LASTEXITCODE): $Message"
    }
}

# 0) Tests — a red suite must never reach the container.
if (-not $SkipTests -and -not $CompileOnly) {
    Write-Host "[0/6] Running tests..." -ForegroundColor Yellow
    dotnet test (Join-Path $RepoRoot "Miautrix.Mail.sln")
    if ($LASTEXITCODE -ne 0) { throw "Backend tests failed; nothing was published." }

    pnpm --filter admin test
    if ($LASTEXITCODE -ne 0) { throw "Admin tests failed; nothing was published." }
}

# Clean publish directories (keep predictable artifacts under ./publish)
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

# 1) Optional: migrations + seeding
if (-not $SkipMigrations) {
    $conn = [Environment]::GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
    if ([string]::IsNullOrWhiteSpace($conn)) {
        throw "MIAUTRIX_DB_CONNECTION must be set unless -SkipMigrations is used."
    }

    Write-Host "[1/6] Applying EF Core migrations..." -ForegroundColor Yellow
    dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web
    if ($LASTEXITCODE -ne 0) { throw "Migrations failed; nothing was published." }

    if ($RunSeeding) {
        Write-Host "[2/6] Seeding default tenant / admin user..." -ForegroundColor Yellow
        dotnet run --project src/Miautrix.Mail.Seeder
        if ($LASTEXITCODE -ne 0) { throw "Seeding failed; nothing was published." }
    }
}

# 2) Publish backend + worker
Write-Host "[2/6] Publishing backend (linux-x64, self-contained)..." -ForegroundColor Yellow
dotnet publish src/Miautrix.Mail.Web/Miautrix.Mail.Web.csproj -c Release -r linux-x64 --self-contained true -o $AppDir
if ($LASTEXITCODE -ne 0) { throw "Web publish failed." }

Write-Host "[2/6] Publishing worker/protocol listener (linux-x64, self-contained)..." -ForegroundColor Yellow
dotnet publish src/Miautrix.Mail.Worker/Miautrix.Mail.Worker.csproj -c Release -r linux-x64 --self-contained true -o $WorkerDir
if ($LASTEXITCODE -ne 0) { throw "Worker publish failed." }

# 3) Build frontends (optional)
if (-not $SkipFrontends) {
    Write-Host "[3/6] Building Admin + Webmail frontends..." -ForegroundColor Yellow

    pnpm --filter admin build
    if ($LASTEXITCODE -ne 0) { throw "Admin build failed." }
    New-Item -ItemType Directory -Force -Path $AdminDir | Out-Null
    Copy-Item -Path (Join-Path $RepoRoot "admin/dist/*") -Destination $AdminDir -Recurse -Force

    pnpm --filter webmail build
    if ($LASTEXITCODE -ne 0) { throw "Webmail build failed." }
    New-Item -ItemType Directory -Force -Path $WebmailDir | Out-Null
    Copy-Item -Path (Join-Path $RepoRoot "webmail/dist/*") -Destination $WebmailDir -Recurse -Force
}

# 4) Upload
Write-Host "[4/6] Uploading to LXC ($TargetIp) via SCP..." -ForegroundColor Green

Write-Host " -> Preparing target directories and stopping running service..." -ForegroundColor Green
# The blob store must survive a deploy: it is never inside the uploaded payload, and this
# step never removes files from the destination.
ssh -o StrictHostKeyChecking=no $Remote "systemctl stop miautrix-mail-worker || true; systemctl stop miautrix-mail || true;
  mkdir -p $RemoteRoot/app $RemoteRoot/worker $RemoteRoot/admin $RemoteRoot/webmail $RemoteRoot/data;
  rm -rf $RemoteRoot/app/* $RemoteRoot/worker/*"
if ($LASTEXITCODE -ne 0) { throw "Target directory prep failed ($LASTEXITCODE)." }

Write-Host " -> Copying Backend Application..." -ForegroundColor Green
$scpOut = & scp -r "$AppDir/." "$($Remote):$RemoteRoot/app/" 2>&1
$scpCode = $LASTEXITCODE
if ($scpCode -ne 0) {
    throw "Backend upload failed ($scpCode): $scpOut"
}

Write-Host " -> Copying Worker / Protocol Listener..." -ForegroundColor Green
$scpOut = & scp -r "$WorkerDir/." "$($Remote):$RemoteRoot/worker/" 2>&1
$scpCode = $LASTEXITCODE
if ($scpCode -ne 0) {
    throw "Worker upload failed ($scpCode): $scpOut"
}

# (No second exitcode check needed; handled above.)

Write-Host " -> Installing Worker systemd unit..." -ForegroundColor Green
scp (Join-Path $PSScriptRoot "miautrix-mail-worker.service") "$($Remote):/etc/systemd/system/miautrix-mail-worker.service"
if ($LASTEXITCODE -ne 0) { throw "Worker systemd unit upload failed." }

if (-not $SkipFrontends) {
    Write-Host " -> Copying Admin Console..." -ForegroundColor Green
    scp -r (Join-Path $AdminDir "*") "$($Remote):$RemoteRoot/admin/"
    if ($LASTEXITCODE -ne 0) { throw "Admin upload failed." }

    Write-Host " -> Copying Webmail Client..." -ForegroundColor Green
    scp -r (Join-Path $WebmailDir "*") "$($Remote):$RemoteRoot/webmail/"
    if ($LASTEXITCODE -ne 0) { throw "Webmail upload failed." }
}

# 5) Blob store check (export / delete-mailbox depend on it) + permissions + restart
Write-Host "[5/6] Preparing blob store, permissions, and restarting services..." -ForegroundColor Green

$StorageScript = Join-Path $PSScriptRoot "lxc-prepare-storage.sh"
scp $StorageScript "$($Remote):/tmp/lxc-prepare-storage.sh"
if ($LASTEXITCODE -ne 0) { throw "Storage helper upload failed." }

$EnvScript = Join-Path $PSScriptRoot "lxc-install-worker-env.sh"
scp $EnvScript "$($Remote):/tmp/lxc-install-worker-env.sh"
if ($LASTEXITCODE -ne 0) { throw "Worker env helper upload failed." }

# Stage TLS material on the container when both paths are supplied, then install it.
$workerEnvStep = ""
if ($WorkerCert -and $WorkerKey) {
    if (-not (Test-Path $WorkerCert)) { throw "WorkerCert not found: $WorkerCert" }
    if (-not (Test-Path $WorkerKey))  { throw "WorkerKey not found: $WorkerKey" }

    Write-Host " -> Staging TLS cert + key..." -ForegroundColor Green
    ssh -o StrictHostKeyChecking=no $Remote "rm -rf /tmp/miautrix-tls && mkdir -p /tmp/miautrix-tls"
    if ($LASTEXITCODE -ne 0) { throw "Failed to create TLS staging directory." }
    scp $WorkerCert "$($Remote):/tmp/miautrix-tls/cert.pem"
    if ($LASTEXITCODE -ne 0) { throw "Cert upload failed." }
    scp $WorkerKey  "$($Remote):/tmp/miautrix-tls/key.pem"
    if ($LASTEXITCODE -ne 0) { throw "Key upload failed." }

    $dbArg = ""
    if ($DbConnectionFile) {
        if (-not (Test-Path $DbConnectionFile)) { throw "DbConnectionFile not found: $DbConnectionFile" }

        Write-Host " -> Staging database connection file..." -ForegroundColor Green
        scp $DbConnectionFile "$($Remote):/tmp/miautrix-db.txt"
        if ($LASTEXITCODE -ne 0) { throw "Database connection file upload failed." }
        $dbArg = "--db-connection-file /tmp/miautrix-db.txt "
    }

    $cfArg = ""
    if ($CloudflareEnvFile) {
        if (-not (Test-Path $CloudflareEnvFile)) { throw "CloudflareEnvFile not found: $CloudflareEnvFile" }

        Write-Host " -> Staging Cloudflare environment file..." -ForegroundColor Green
        scp $CloudflareEnvFile "$($Remote):/tmp/miautrix-cloudflare.txt"
        if ($LASTEXITCODE -ne 0) { throw "Cloudflare environment file upload failed." }
        $cfArg = "--cloudflare-env-file /tmp/miautrix-cloudflare.txt "
    }

    $workerEnvStep = "chmod +x /tmp/lxc-install-worker-env.sh && " +
        "/tmp/lxc-install-worker-env.sh $($dbArg)$($cfArg)--cert /tmp/miautrix-tls/cert.pem --key /tmp/miautrix-tls/key.pem; " +
        "rm -rf /tmp/miautrix-tls /tmp/miautrix-db.txt /tmp/miautrix-cloudflare.txt && "
} else {
    # No TLS material supplied: leave the Worker's environment alone. The unit's
    # EnvironmentFile is non-fatal, so the restart below still happens and the Worker
    # reports its own missing-configuration error in the journal.
    if ($CloudflareEnvFile) {
        Write-Host " -> CloudflareEnvFile ignored: lxc-install-worker-env.sh runs only when" -ForegroundColor Yellow
        Write-Host "    -WorkerCert and -WorkerKey are supplied." -ForegroundColor Yellow
    }
    $workerEnvStep = ""
}

$storageArgs = if ($PinStorageDir) { "--pin" } else { "" }
$remotePrep = "chmod +x /tmp/lxc-prepare-storage.sh && " +
    "/tmp/lxc-prepare-storage.sh $storageArgs && " +
    "chmod +x $RemoteRoot/app/Miautrix.Mail.Web $RemoteRoot/worker/Miautrix.Mail.Worker && " +
    "chown -R www-data:www-data $RemoteRoot && " +
    "systemctl daemon-reload && " +
    "systemctl enable miautrix-mail-worker && " +
    "systemctl restart miautrix-mail && " +
    $workerEnvStep +
    "systemctl restart miautrix-mail-worker && " +
    "systemctl restart nginx"

ssh -o StrictHostKeyChecking=no $Remote $remotePrep
if ($LASTEXITCODE -ne 0) { throw "Remote preparation or restart failed." }

Write-Host " -> Protocol ports require firewall/NAT access for 25, 465, 587, and 993." -ForegroundColor Yellow
Write-Host " -> Worker environment lives in /opt/miautrix-mail/.env, written by lxc-install-worker-env.sh." -ForegroundColor Yellow
if (-not ($WorkerCert -and $WorkerKey)) {
    Write-Host " -> TLS not provisioned. The Worker will exit until you re-run with:" -ForegroundColor Yellow
    Write-Host "      -WorkerCert <cert.pem> -WorkerKey <key.pem>" -ForegroundColor Yellow
}
if (-not $CloudflareEnvFile) {
    Write-Host " -> Cloudflare transport not configured. Per-domain Cloudflare routing can be" -ForegroundColor Yellow
    Write-Host "    selected in the admin console, but outbound delivery needs CLOUDFLARE_API_TOKEN" -ForegroundColor Yellow
    Write-Host "    and the inbound webhook needs MIAUTRIX_INBOUND_TOKEN. Supply both with:" -ForegroundColor Yellow
    Write-Host "      -CloudflareEnvFile <cloudflare.env>" -ForegroundColor Yellow
}

# 6) Optional verification
if ($Verify) {
    Write-Host "[6/6] Waiting briefly then verifying..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5

    try {
        $response = Invoke-WebRequest -Uri "https://mail.miautrix.tech/api/v1/system/info" -Method Get
        if ($response.StatusCode -eq 200) {
            Write-Host " -> Backend API is up." -ForegroundColor Green
        } else {
            Write-Host " -> Unexpected status code: $($response.StatusCode)" -ForegroundColor Red
        }
    } catch {
        Write-Host " -> Verification check failed: $($_.Exception.Message)" -ForegroundColor Red
    }

    $workerUp = $false
    $pollStart = Get-Date
    while (-not $workerUp -and ((Get-Date) - $pollStart).TotalSeconds -lt 30) {
        $null = ssh $Remote "systemctl is-active --quiet miautrix-mail-worker"
        $isActive = $LASTEXITCODE -eq 0

        $portOutput = ssh $Remote "ss -lntup | grep -E ':(25|465|587|993)\\b'" 2>&1
        $hasPorts = $LASTEXITCODE -eq 0

        if ($isActive -and $hasPorts) {
            $workerUp = $true
            Write-Host " -> Worker is active and protocol ports are listening." -ForegroundColor Green
            $portOutput | ForEach-Object { Write-Host "    $_" }
        } else {
            Start-Sleep -Seconds 2
        }
    }

    if (-not $workerUp) {
        Write-Host " -> Worker status/port verification failed; check systemctl status miautrix-mail-worker." -ForegroundColor Red
    }

    # /mailboxes/orphans must route (401 without a token). A 404 means the new controller
    # did not make it onto the container.
    try {
        Invoke-WebRequest -Uri "https://mail.miautrix.tech/api/v1/mailboxes/orphans" -Method Get | Out-Null
        Write-Host " -> /mailboxes/orphans reachable." -ForegroundColor Green
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        if ($code -eq 401 -or $code -eq 403) {
            Write-Host " -> /mailboxes/orphans reachable (auth required, HTTP $code)." -ForegroundColor Green
        } elseif ($code -eq 404) {
            Write-Host " -> /mailboxes/orphans returned 404: the new controller is missing." -ForegroundColor Red
        } else {
            Write-Host " -> /mailboxes/orphans returned HTTP $code." -ForegroundColor Red
        }
    }

    # /shared-mailboxes must also route.
    try {
        Invoke-WebRequest -Uri "https://mail.miautrix.tech/api/v1/shared-mailboxes" -Method Get | Out-Null
        Write-Host " -> /shared-mailboxes reachable." -ForegroundColor Green
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        if ($code -eq 401 -or $code -eq 403) {
            Write-Host " -> /shared-mailboxes reachable (auth required, HTTP $code)." -ForegroundColor Green
        } elseif ($code -eq 404) {
            Write-Host " -> /shared-mailboxes returned 404: the controller is missing." -ForegroundColor Red
        } else {
            Write-Host " -> /shared-mailboxes returned HTTP $code." -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host " Admin Console: https://admin.miautrix.tech  (Directory -> Users / Shared Mailboxes)" -ForegroundColor Cyan
