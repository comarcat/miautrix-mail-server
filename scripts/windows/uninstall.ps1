[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
param(
    [string]$InstallDir = 'C:\Program Files\Miautrix Mail',
    [string]$DataDir = 'C:\ProgramData\Miautrix\mail',
    [switch]$RemoveData
)
$ErrorActionPreference = 'Stop'
$serviceNames = @('MiautrixMail','MiautrixMailWorker')
foreach ($entry in @(@('InstallDir',$InstallDir),@('DataDir',$DataDir))) {
    if (-not [IO.Path]::IsPathRooted($entry[1])) { throw "$($entry[0]) must be an absolute path." }
    $fullPath = [IO.Path]::GetFullPath($entry[1])
    $full = $fullPath.TrimEnd([char[]](92,47))
    $root = [IO.Path]::GetPathRoot($fullPath).TrimEnd([char[]](92,47))
    if ($full.Equals($root,[StringComparison]::OrdinalIgnoreCase)) { throw "$($entry[0]) cannot be a filesystem root." }
    if ($entry[0] -eq 'InstallDir') { $InstallDir = $full }
    else { $DataDir = $full }
}
$separator = [IO.Path]::DirectorySeparatorChar
if ($DataDir.StartsWith("$InstallDir$separator",[StringComparison]::OrdinalIgnoreCase) -or $InstallDir.StartsWith("$DataDir$separator",[StringComparison]::OrdinalIgnoreCase) -or $DataDir.Equals($InstallDir,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'InstallDir and DataDir must be separate, non-nested directories.'
}
if (-not $WhatIfPreference) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run uninstall.ps1 from an elevated PowerShell 7 session.'
    }
    if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 or newer is required.' }
}
if ($WhatIfPreference) {
    Write-Host "WHATIF: stop and remove services $($serviceNames -join ', '), remove Miautrix firewall rules, and remove binaries at $InstallDir."
    if ($RemoveData) { Write-Host "WHATIF: data removal requested for $DataDir; a typed confirmation is required." }
    Write-Host 'WHATIF: no services, firewall rules, registry keys, files, or database objects were changed.'
    return
}
if ($RemoveData) {
    $expected = "REMOVE DATA $DataDir"
    $answer = Read-Host "This permanently removes mail storage and backups at $DataDir. Type exactly '$expected' to continue"
    if ($answer -cne $expected) { throw 'Data removal confirmation did not match; no uninstall actions were taken.' }
}

foreach ($name in $serviceNames) {
    Stop-Service -Name $name -Force -ErrorAction SilentlyContinue
    & sc.exe delete $name | Out-Null
    if ($LASTEXITCODE -ne 0 -and (Get-Service -Name $name -ErrorAction SilentlyContinue)) { throw "Could not remove service $name" }
}
Get-NetFirewallRule -DisplayName 'Miautrix Mail TCP *' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
if (Test-Path -LiteralPath $InstallDir) { Remove-Item -LiteralPath $InstallDir -Recurse -Force }
if (Test-Path -LiteralPath "$InstallDir.previous") { Remove-Item -LiteralPath "$InstallDir.previous" -Recurse -Force }

if ($RemoveData) {
    if (Test-Path -LiteralPath $DataDir) { Remove-Item -LiteralPath $DataDir -Recurse -Force }
}
Write-Host 'Services, firewall rules, and installed binaries were removed; the database was not changed.'
