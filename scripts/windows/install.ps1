[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
param(
    [string]$InstallDir = 'C:\Program Files\Miautrix Mail',
    [string]$DataDir = 'C:\ProgramData\Miautrix\mail',
    [Parameter(Mandatory=$true)][string]$DbConnection,
    [Parameter(Mandatory=$true)][string]$TlsCertPath,
    [Parameter(Mandatory=$true)][string]$TlsKeyPath,
    [Parameter(Mandatory=$true)][string]$AdminEmail,
    [string]$MailHostname,
    [ValidateRange(1,65535)][int]$HttpPort = 5000,
    [ValidateRange(1,65535)][int]$HttpsPort = 443
)
$ErrorActionPreference = 'Stop'
$packageRoot = $PSScriptRoot
$serviceNames = @('MiautrixMail','MiautrixMailWorker')

function Assert-Package {
    foreach ($file in @('web/Miautrix.Mail.Web.exe','worker/Miautrix.Mail.Worker.exe','cli/Miautrix.Mail.Cli.exe','seeder/Miautrix.Mail.Seeder.exe','efbundle.exe','admin/index.html','webmail/index.html')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $file) -PathType Leaf)) { throw "Package is missing $file" }
    }
    foreach ($path in @($TlsCertPath,$TlsKeyPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "TLS PEM file not found: $path" }
    }
    if ($DbConnection -match '(?i)(^|;)\s*(Password|Pwd)\s*=') { throw 'DbConnection must omit Password/Pwd; the installer prompts for it securely.' }
}

function Normalize-SafeTarget([string]$PathValue,[string]$Label) {
    if (-not [IO.Path]::IsPathRooted($PathValue)) { throw "$Label must be an absolute path." }
    $full = [IO.Path]::GetFullPath($PathValue)
    $trimmed = $full.TrimEnd([char[]](92,47))
    $root = [IO.Path]::GetPathRoot($full).TrimEnd([char[]](92,47))
    if ($trimmed.Equals($root,[StringComparison]::OrdinalIgnoreCase)) { throw "$Label cannot be a filesystem root." }
    return $trimmed
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Protect-ServiceEnvironment([string]$ServiceName, [string[]]$Values) {
    $keyPath = "SYSTEM\CurrentControlSet\Services\$ServiceName"
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($keyPath, $true)
    if (-not $key) { throw "Service registry key is missing for $ServiceName" }
    try { $key.SetValue('Environment', $Values, [Microsoft.Win32.RegistryValueKind]::MultiString) } finally { $key.Dispose() }
    $acl = Get-Acl "Registry::HKEY_LOCAL_MACHINE\$keyPath"
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($rule in @($acl.Access)) { [void]$acl.RemoveAccessRuleAll($rule) }
    foreach ($sid in @('S-1-5-18','S-1-5-32-544')) {
        $account = [Security.Principal.SecurityIdentifier]::new($sid)
        $rule = [System.Security.AccessControl.RegistryAccessRule]::new(
            $account,
            [System.Security.AccessControl.RegistryRights]::FullControl,
            [System.Security.AccessControl.InheritanceFlags]::ContainerInherit,
            [System.Security.AccessControl.PropagationFlags]::None,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    Set-Acl "Registry::HKEY_LOCAL_MACHINE\$keyPath" $acl
}

function Install-ServiceBinary([string]$Name,[string]$Binary,[string[]]$Environment) {
    $binPath = 'binPath= "' + $Binary + '"'
    $existing = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $existing) {
        & sc.exe create $Name $binPath 'start= delayed-auto' 'obj= LocalSystem' 'DisplayName= Miautrix Mail'
        if ($LASTEXITCODE -ne 0) { throw "Could not create service $Name" }
    } else {
        & sc.exe config $Name $binPath 'start= delayed-auto' 'obj= LocalSystem'
        if ($LASTEXITCODE -ne 0) { throw "Could not configure service $Name" }
    }
    & sc.exe failure $Name 'reset= 86400' 'actions= restart/60000/restart/60000/restart/60000' | Out-Null
    Protect-ServiceEnvironment $Name $Environment
}

function Wait-Port([int]$Port,[int]$Seconds=30) {
    $end = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $end) {
        if (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Port $Port did not start listening."
}

function Verify-Install {
    foreach ($name in $serviceNames) {
        $svc = Get-Service -Name $name
        if ($svc.Status -ne 'Running') { throw "Service $name is not running." }
    }
    foreach ($port in @(25,465,587,993,$HttpsPort,$HttpPort)) { Wait-Port $port }
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:$HttpPort/" -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 15
    if ([int]$response.StatusCode -ge 500) { throw "HTTP health returned $($response.StatusCode)." }
    $httpsResponse = Invoke-WebRequest -Uri "https://127.0.0.1:$HttpsPort/" -UseBasicParsing -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 15
    if ([int]$httpsResponse.StatusCode -ge 500) { throw "HTTPS health returned $($httpsResponse.StatusCode)." }
    $tcp = [Net.Sockets.TcpClient]::new('127.0.0.1',993)
    try {
        $ssl = [Net.Security.SslStream]::new($tcp.GetStream(),$false, { $true })
        try {
            $ssl.AuthenticateAsClient('localhost')
            $reader = [IO.StreamReader]::new($ssl,[Text.Encoding]::ASCII,$false,1024,$true)
            $greeting = $reader.ReadLine()
            if (-not $greeting -or $greeting -notmatch '^\* OK') { throw 'IMAPS greeting was not received.' }
        } finally { $ssl.Dispose() }
    } finally { $tcp.Dispose() }
}

$InstallDir = Normalize-SafeTarget $InstallDir 'InstallDir'
$DataDir = Normalize-SafeTarget $DataDir 'DataDir'
$separator = [IO.Path]::DirectorySeparatorChar
if ($DataDir.StartsWith("$InstallDir$separator",[StringComparison]::OrdinalIgnoreCase) -or $InstallDir.StartsWith("$DataDir$separator",[StringComparison]::OrdinalIgnoreCase) -or $DataDir.Equals($InstallDir,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'InstallDir and DataDir must be separate, non-nested directories.'
}
if ($HttpPort -eq $HttpsPort) { throw 'HttpPort and HttpsPort must be different ports.' }
Assert-Package
if ([string]::IsNullOrWhiteSpace($MailHostname)) { $MailHostname = "mail.$($AdminEmail.Split('@')[-1])" }
$hadOldInstall = Test-Path -LiteralPath (Join-Path $InstallDir 'web/Miautrix.Mail.Web.exe') -PathType Leaf
if ($WhatIfPreference) {
    $adminAction = if ($hadOldInstall) { 'keep the existing admin credential' } else { "securely bootstrap $AdminEmail" }
    Write-Host "WHATIF: install/upgrade binaries at $InstallDir; preserve data at $DataDir; migrate DB; seed roles; $adminAction; configure two Windows services, public firewall ports 25/465/587/993/$HttpsPort, loopback-only HTTP port $HttpPort; start and verify."
    Write-Host 'WHATIF: no password prompts, database connection, registry, service, firewall, or filesystem changes were made.'
    return
}
if (-not (Test-Administrator)) { throw 'Run install.ps1 from an elevated PowerShell 7 session.' }
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 or newer is required.' }
if (-not (Test-NetConnection -ComputerName '127.0.0.1' -Port 3310 -InformationLevel Quiet)) { throw 'ClamAV must be listening on the local TCP endpoint 127.0.0.1:3310.' }

$oldInstall = "$InstallDir.previous"
$stageInstall = "$InstallDir.next"
$oldRegistry = @{}
$existedServices = @{}
$firewallRulesCreated = [Collections.Generic.List[string]]::new()
$swappedInstall = $false
$mutationsStarted = $false
$currentStep = 'secure configuration prompts'
$migrationStarted = $false
try {
$dbSecure = Read-Host 'PostgreSQL password' -AsSecureString
$dbPassword = [Net.NetworkCredential]::new('', $dbSecure).Password
$builder = [System.Data.Common.DbConnectionStringBuilder]::new()
$builder.ConnectionString = $DbConnection
$builder['Password'] = $dbPassword
$connection = $builder.ConnectionString
$env:MIAUTRIX_DB_CONNECTION = $connection
$env:MIAUTRIX_STORAGE_DIR = Join-Path $DataDir 'storage'
$serviceEnvironment = @(
    "DOTNET_ENVIRONMENT=Production",
    "MIAUTRIX_DB_CONNECTION=$connection",
    "MIAUTRIX_STORAGE_DIR=$(Join-Path $DataDir 'storage')",
    "MIAUTRIX_BACKUP_DIR=$(Join-Path $DataDir 'backups')",
    "MIAUTRIX_ADMIN_DIR=$(Join-Path $InstallDir 'admin')",
    "MIAUTRIX_WEBMAIL_DIR=$(Join-Path $InstallDir 'webmail')",
    "MIAUTRIX_TLS_CERT_PATH=$TlsCertPath",
    "MIAUTRIX_TLS_KEY_PATH=$TlsKeyPath",
    "MIAUTRIX_HOSTNAME=$MailHostname",
    'MIAUTRIX_CLAMAV_SOCKET=tcp://127.0.0.1:3310'
)
$webServiceEnvironment = @($serviceEnvironment) + @(
    "Kestrel__Endpoints__Https__Url=https://0.0.0.0:$HttpsPort",
    "Kestrel__Endpoints__Https__Certificate__Path=$TlsCertPath",
    "Kestrel__Endpoints__Https__Certificate__KeyPath=$TlsKeyPath",
    "Kestrel__Endpoints__Http__Url=http://127.0.0.1:$HttpPort"
)
    $mutationsStarted = $true
    $currentStep = 'stopping existing services'
    foreach ($service in $serviceNames) {
        $reg = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Services\$service")
        $existedServices[$service] = $null -ne $reg
        if ($reg) { try { $oldRegistry[$service] = $reg.GetValue('Environment',$null) } finally { $reg.Dispose() } }
        Stop-Service -Name $service -Force -ErrorAction SilentlyContinue
    }
    $currentStep = 'staging new binaries'
    if (Test-Path -LiteralPath $stageInstall) { Remove-Item -LiteralPath $stageInstall -Recurse -Force }
    New-Item -ItemType Directory -Path $stageInstall -Force | Out-Null
    Get-ChildItem -LiteralPath $packageRoot -Force | Where-Object Name -notin @('install.ps1','uninstall.ps1','README-WINDOWS.md') | Copy-Item -Destination $stageInstall -Recurse -Force
    if (Test-Path -LiteralPath $oldInstall) { Move-Item -LiteralPath $oldInstall -Destination "$oldInstall.$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))" }
    if ($hadOldInstall) { Move-Item -LiteralPath $InstallDir -Destination $oldInstall }
    Move-Item -LiteralPath $stageInstall -Destination $InstallDir
    $swappedInstall = $true
    New-Item -ItemType Directory -Path $DataDir,(Join-Path $DataDir 'storage'),(Join-Path $DataDir 'backups') -Force | Out-Null

    $currentStep = 'configuring services'
    Install-ServiceBinary 'MiautrixMail' (Join-Path $InstallDir 'web/Miautrix.Mail.Web.exe') $webServiceEnvironment
    Install-ServiceBinary 'MiautrixMailWorker' (Join-Path $InstallDir 'worker/Miautrix.Mail.Worker.exe') $serviceEnvironment
    $currentStep = 'database migrations'
    $migrationStarted = $true
    & (Join-Path $InstallDir 'efbundle.exe')
    if ($LASTEXITCODE -ne 0) { throw 'EF migrations bundle failed.' }
    $currentStep = 'bootstrap data seeding'
    & (Join-Path $InstallDir 'seeder/Miautrix.Mail.Seeder.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Bootstrap entity seeding failed.' }
    if (-not $hadOldInstall) {
        $currentStep = 'initial admin bootstrap'
        & (Join-Path $InstallDir 'cli/Miautrix.Mail.Cli.exe') users bootstrap-admin --email $AdminEmail --reset
        if ($LASTEXITCODE -ne 0) { throw 'Secure admin bootstrap failed.' }
    }
    $currentStep = 'firewall configuration'
    foreach ($port in @(25,465,587,993,$HttpsPort)) {
        $ruleName = "Miautrix Mail TCP $port"
        if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
            $null = New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $port
            $firewallRulesCreated.Add($ruleName)
        }
    }
    $currentStep = 'starting services'
    foreach ($service in $serviceNames) { Start-Service -Name $service }
    Start-Sleep -Seconds 3
    $currentStep = 'local HTTPS, HTTP and IMAPS verification'
    Verify-Install
    Write-Host 'Installation or upgrade completed and local checks passed.'
} catch {
    $failureMessage = $_.Exception.Message
    $failedStep = $currentStep
    $rollbackSummary = if ($swappedInstall -and (Test-Path -LiteralPath $oldInstall)) { 'previous binaries were restored' } elseif ($swappedInstall) { 'new binaries were removed; no previous installation existed' } else { 'existing binaries remained in place' }
    if ($mutationsStarted) {
        foreach ($service in $serviceNames) { Stop-Service -Name $service -Force -ErrorAction SilentlyContinue }
        if ($swappedInstall) {
            if (Test-Path -LiteralPath $InstallDir) { Remove-Item -LiteralPath $InstallDir -Recurse -Force }
            if (Test-Path -LiteralPath $oldInstall) { Move-Item -LiteralPath $oldInstall -Destination $InstallDir }
        }
        foreach ($service in $serviceNames) {
            if ($oldRegistry.ContainsKey($service) -and $null -ne $oldRegistry[$service]) { Protect-ServiceEnvironment $service $oldRegistry[$service] }
            if (-not $existedServices[$service]) { & sc.exe delete $service | Out-Null }
        }
        foreach ($ruleName in $firewallRulesCreated) {
            Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
        }
        foreach ($service in $serviceNames) { Start-Service -Name $service -ErrorAction SilentlyContinue }
    }
    $databaseNote = if ($migrationStarted) { 'The database may have advanced and remains forward-only if efbundle ran.' } else { 'The migration bundle did not run.' }
    throw "Install failed at '$failedStep'; $rollbackSummary. $databaseNote Original error: $failureMessage"
} finally {
    Remove-Item Env:\MIAUTRIX_DB_CONNECTION -ErrorAction SilentlyContinue
    Remove-Item Env:\MIAUTRIX_STORAGE_DIR -ErrorAction SilentlyContinue
    $dbPassword = $null; $connection = $null; $builder = $null
}
