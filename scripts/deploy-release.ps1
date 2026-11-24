[CmdletBinding()]
param(
    [string]$TargetIp = '10.11.1.51',
    [string]$TargetUser = 'root',
    [string]$SshKey,
    [switch]$SkipTests,
    [switch]$RunMigrations,
    [switch]$DryRun,
    [switch]$Rollback,
    [switch]$RestoreDatabase,
    [switch]$Status,
    [switch]$SkipCleanup,
    [switch]$AllowDirty,
    [switch]$LocalTest
)
$ErrorActionPreference = 'Stop'
$Repo = Split-Path -Parent $PSScriptRoot
function Invoke-Checked([string]$Label, [scriptblock]$Action) {
    Write-Host "== $Label =="
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Label failed with exit code $LASTEXITCODE" }
}
function Get-ReleaseId {
    $version = Select-String -Path (Join-Path $Repo 'CHANGELOG.md') -Pattern '^## \[([^\]]+)\]' | Select-Object -First 1
    if (-not $version) { throw 'Could not read top CHANGELOG version.' }
    $sha = (& git -C $Repo rev-parse --short HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $sha) { throw 'HEAD is unavailable.' }
    return "v$($version.Matches[0].Groups[1].Value)-$sha-$([DateTime]::UtcNow.ToString('yyyyMMddHHmm'))"
}
function Test-PublishLayout([string]$Path) {
    foreach ($file in @('app/Miautrix.Mail.Web','worker/Miautrix.Mail.Worker','cli/Miautrix.Mail.Cli','admin/index.html','webmail/index.html')) {
        if (-not (Test-Path (Join-Path $Path $file))) { throw "Publish layout missing $file" }
    }
    foreach ($component in @('app','worker','cli')) {
        $runtime = Get-ChildItem (Join-Path $Path $component) -Filter '*.runtimeconfig.json' | Select-Object -First 1
        if (-not $runtime -or (Get-Content -Raw $runtime.FullName) -notmatch '"tfm"\s*:\s*"net10\.0"') { throw "$component runtimeconfig must target net10.0" }
    }
}
if ($LocalTest) {
    $id = Get-ReleaseId
    if ($id -notmatch '^v2\.0\.0-[0-9a-f]+-\d{12}$') { throw "Release id format unexpected: $id" }
    $temp = Join-Path ([IO.Path]::GetTempPath()) "miautrix-release-layout-$([guid]::NewGuid().ToString('N'))"
    try {
        foreach ($d in @('app','worker','cli','admin','webmail')) { New-Item -ItemType Directory -Path (Join-Path $temp $d) -Force | Out-Null }
        foreach ($f in @('app/Miautrix.Mail.Web','worker/Miautrix.Mail.Worker','cli/Miautrix.Mail.Cli')) { New-Item -ItemType File -Path (Join-Path $temp $f) -Force | Out-Null }
        foreach ($d in @('app','worker','cli')) { Set-Content (Join-Path $temp "$d/test.runtimeconfig.json") '{"runtimeOptions":{"tfm":"net10.0"}}' }
        Set-Content (Join-Path $temp 'admin/index.html') 'admin'; Set-Content (Join-Path $temp 'webmail/index.html') 'webmail'
        Test-PublishLayout $temp
        Write-Host "Local release plan test passed: $id; publish layout is complete. No network was used."
    } finally { if (Test-Path $temp) { Remove-Item -LiteralPath $temp -Recurse -Force } }
    exit 0
}
$ReleaseId = Get-ReleaseId
if ($ReleaseId -notmatch '^[A-Za-z0-9._-]+$') { throw 'Computed release ID contains unsupported characters.' }
if ($Rollback -or $Status) {
    if ($RestoreDatabase -and -not $Rollback) { throw '-RestoreDatabase requires -Rollback.' }
    $Subcommand = if ($Rollback) { 'rollback' } else { 'status' }
    if ($RestoreDatabase) { $Subcommand += ' --restore-db' }
    $script = Join-Path $PSScriptRoot 'lxc-release.sh'
    $opts = @('-o','StrictHostKeyChecking=accept-new')
    if ($SshKey) { $opts += @('-o','BatchMode=yes','-i',$SshKey) }
    $remoteScript = '/tmp/miautrix-lxc-release-admin.sh'
    & scp @($opts + @($script, "$TargetUser@$TargetIp`:$remoteScript"))
    if ($LASTEXITCODE -ne 0) { throw 'Could not upload release manager.' }
    & ssh @($opts + @("$TargetUser@$TargetIp", "sudo bash '$remoteScript' $Subcommand"))
    $code = $LASTEXITCODE
    & ssh @($opts + @("$TargetUser@$TargetIp", "rm -f '$remoteScript'")) 2>$null
    if ($code -ne 0) { throw "Remote $Subcommand failed." }
    exit 0
}
if (-not $AllowDirty) {
    $dirty = (& git -C $Repo status --porcelain -- . ':(exclude)graphify-out')
    if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
    if ($dirty) { throw 'Working tree must be clean; commit or pass -AllowDirty for a reviewed local snapshot.' }
}
$head = (& git -C $Repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $head) { throw 'HEAD is unknown.' }
Invoke-Checked 'Build' { dotnet build (Join-Path $Repo 'Miautrix.Mail.sln') -warnaserror }
if (-not $SkipTests) {
    Invoke-Checked 'Unit tests' { dotnet test (Join-Path $Repo 'tests/Miautrix.Mail.UnitTests') }
    Invoke-Checked 'Security tests' { dotnet test (Join-Path $Repo 'tests/Miautrix.Mail.SecurityTests') }
    $db = $env:MIAUTRIX_DB_CONNECTION
    if ($db) {
        if ($db -match '(?i)(10\.11\.1\.52|miautrix-mail-pro)') { throw 'Refusing DB-backed tests because MIAUTRIX_DB_CONNECTION targets production.' }
        Invoke-Checked 'Integration tests' { dotnet test (Join-Path $Repo 'tests/Miautrix.Mail.IntegrationTests') }
        Invoke-Checked 'Protocol tests' { dotnet test (Join-Path $Repo 'tests/Miautrix.Mail.ProtocolTests') }
    }
    Invoke-Checked 'Webmail tests' { pnpm --filter webmail test }
    Invoke-Checked 'Admin tests' { pnpm --filter admin test -- --run }
}
$PublishRoot = Join-Path $Repo "publish/$ReleaseId"
if (Test-Path $PublishRoot) { throw "Publish output already exists: $PublishRoot" }
New-Item -ItemType Directory -Path $PublishRoot -Force | Out-Null
try {
    foreach ($pair in @(@('Web','src/Miautrix.Mail.Web/Miautrix.Mail.Web.csproj','app'),@('Worker','src/Miautrix.Mail.Worker/Miautrix.Mail.Worker.csproj','worker'),@('CLI','src/Miautrix.Mail.Cli/Miautrix.Mail.Cli.csproj','cli'))) {
        $dest = Join-Path $PublishRoot $pair[2]
        Invoke-Checked "Publish $($pair[0])" { dotnet publish (Join-Path $Repo $pair[1]) -c Release -r linux-x64 --self-contained true -o $dest }
    }
    Invoke-Checked 'Build admin' { pnpm --filter admin build }
    Invoke-Checked 'Build webmail' { pnpm --filter webmail build }
    Copy-Item (Join-Path $Repo 'admin/dist') (Join-Path $PublishRoot 'admin') -Recurse
    Copy-Item (Join-Path $Repo 'webmail/dist') (Join-Path $PublishRoot 'webmail') -Recurse
    Test-PublishLayout $PublishRoot
    $metadata = @{ id=$ReleaseId; gitSha=$head; builtAtUtc=[DateTime]::UtcNow.ToString('o'); tfm='net10.0' } | ConvertTo-Json
    Set-Content -Path (Join-Path $PublishRoot 'RELEASE.json') -Value $metadata -Encoding utf8
    $archive = Join-Path $Repo "publish/$ReleaseId.tar.gz"
    tar -czf $archive -C (Split-Path $PublishRoot) $ReleaseId
    if ($LASTEXITCODE -ne 0) { throw 'Artifact tar failed.' }
    $hash = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
    $remoteTmp = "/tmp/miautrix-release-$ReleaseId"
    $scpOpts = @('-o','StrictHostKeyChecking=accept-new'); if ($SshKey) { $scpOpts += @('-o','BatchMode=yes','-i',$SshKey) }
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "mkdir -p '$remoteTmp'"))
    if ($LASTEXITCODE -ne 0) { throw 'Remote temporary directory creation failed.' }
    & scp @($scpOpts + @((Join-Path $PSScriptRoot 'lxc-release.sh'), "$TargetUser@$TargetIp`:$remoteTmp/lxc-release.sh"))
    if ($LASTEXITCODE -ne 0) { throw 'Upload of release manager failed.' }
    & scp @($scpOpts + @($archive, "$TargetUser@$TargetIp`:$remoteTmp/$ReleaseId.tar.gz"))
    if ($LASTEXITCODE -ne 0) { throw 'Artifact upload failed.' }
    $remoteBase = "set -e; cd '$remoteTmp'; printf '%s  %s.tar.gz\n' '$hash' '$ReleaseId' | sha256sum -c -; mkdir -p upload; tar -xzf '$ReleaseId.tar.gz' -C upload"
    $uploadPath = "$remoteTmp/upload/$ReleaseId"
    $remote = "$remoteBase; bash lxc-release.sh preflight --release-id '$ReleaseId' --upload '$uploadPath'"
    if ($DryRun) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "$remote; bash lxc-release.sh status"))
        if ($LASTEXITCODE -ne 0) { throw 'Dry-run preflight/status failed.' }
        Write-Host "Dry run complete for $ReleaseId. No backup, switch, or migration was run."
        exit 0
    }
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "$remote; bash lxc-release.sh backup --release-id '$ReleaseId'"))
    if ($LASTEXITCODE -ne 0) { throw 'Remote preflight or backup failed.' }
    if ($RunMigrations) {
        # The design-time factory only needs a non-empty value; no connection is opened while bundling.
        $previousDbConnection = $env:MIAUTRIX_DB_CONNECTION
        $env:MIAUTRIX_DB_CONNECTION = 'Host=bundle-build.invalid;Database=unused;Username=unused'
        try {
            Invoke-Checked 'Build migration bundle' { dotnet ef migrations bundle --project (Join-Path $Repo 'src/Miautrix.Mail.Persistence') --startup-project (Join-Path $Repo 'src/Miautrix.Mail.Web') -r linux-x64 --self-contained --output (Join-Path $PublishRoot 'migration-bundle') }
        } finally {
            $env:MIAUTRIX_DB_CONNECTION = $previousDbConnection
        }
        & scp @($scpOpts + @((Join-Path $PublishRoot 'migration-bundle'), "$TargetUser@$TargetIp`:$remoteTmp/migration-bundle"))
        if ($LASTEXITCODE -ne 0) { throw 'Migration bundle upload failed.' }
        $migration = "cd '$remoteTmp'; bash lxc-release.sh migrate --bundle '$remoteTmp/migration-bundle'"
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", $migration))
        if ($LASTEXITCODE -ne 0) { throw 'Remote migration failed.' }
    }
    $stage = "cd '$remoteTmp'; bash lxc-release.sh stage --release-id '$ReleaseId' --upload '$uploadPath'"
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", $stage))
    if ($LASTEXITCODE -ne 0) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "rm -rf '/opt/miautrix-mail/releases/$ReleaseId'")) 2>$null
        throw "Remote stage failed; partial release removed if present, no rollback was attempted. Database dump remains in /opt/miautrix-mail/backups."
    }
    $switchAttempted = $true
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "cd '$remoteTmp'; bash lxc-release.sh switch --release-id '$ReleaseId'"))
    if ($LASTEXITCODE -ne 0) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh rollback"))
        throw "Remote switch failed; rollback attempted. Database dump remains in /opt/miautrix-mail/backups; inspect server status."
    }
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh verify"))
    if ($LASTEXITCODE -ne 0) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh rollback"))
        throw "Remote verify failed; rollback attempted. Database dump remains in /opt/miautrix-mail/backups; inspect server status."
    }
    if (-not $SkipCleanup) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh cleanup --keep 2"))
        if ($LASTEXITCODE -ne 0) { throw 'Deploy verified but cleanup failed; release retained.' }
    }
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh status"))
    if ($LASTEXITCODE -ne 0) { throw 'Final remote status failed.' }
} finally {
    if ($remoteTmp) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "rm -rf '$remoteTmp'")) 2>$null
    }
}
