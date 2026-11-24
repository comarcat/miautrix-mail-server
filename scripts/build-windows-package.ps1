[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 or newer is required.' }
$repo = Split-Path -Parent $PSScriptRoot
function Invoke-Checked([string]$Label, [scriptblock]$Action) {
    Write-Host "== $Label =="
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Label failed with exit code $LASTEXITCODE" }
}

$dirty = (& git -C $repo status --porcelain -- . ':(exclude)graphify-out')
if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
if ($dirty) { throw 'Working tree must be clean; commit or stash changes before building a Windows package.' }

$versionMatch = Select-String -Path (Join-Path $repo 'CHANGELOG.md') -Pattern '^## \[([^\]]+)\]' | Select-Object -First 1
if (-not $versionMatch) { throw 'Could not read top CHANGELOG version.' }
$version = $versionMatch.Matches[0].Groups[1].Value
$gitSha = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $gitSha) { throw 'HEAD is unavailable.' }
$sha = (& git -C $repo rev-parse --short HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $sha) { throw 'HEAD short SHA is unavailable.' }
$packageName = "miautrix-mail-win-x64-$version-$sha"
$publishRoot = Join-Path $repo "publish/$packageName"
$zipPath = "$publishRoot.zip"
$checksumPath = "$zipPath.sha256"
if (Test-Path $publishRoot) { throw "Package staging already exists: $publishRoot" }
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
try {
    Invoke-Checked 'Build solution' { dotnet build (Join-Path $repo 'Miautrix.Mail.sln') -c Release -warnaserror }
    Invoke-Checked 'Unit tests' { dotnet test (Join-Path $repo 'tests/Miautrix.Mail.UnitTests') -c Release --no-build }
    foreach ($entry in @(
        @{ Name='web'; Project='src/Miautrix.Mail.Web/Miautrix.Mail.Web.csproj' },
        @{ Name='worker'; Project='src/Miautrix.Mail.Worker/Miautrix.Mail.Worker.csproj' },
        @{ Name='cli'; Project='src/Miautrix.Mail.Cli/Miautrix.Mail.Cli.csproj' },
        @{ Name='seeder'; Project='src/Miautrix.Mail.Seeder/Miautrix.Mail.Seeder.csproj' }
    )) {
        Invoke-Checked "Publish $($entry.Name)" {
            dotnet publish (Join-Path $repo $entry.Project) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o (Join-Path $publishRoot $entry.Name)
        }
    }
    Invoke-Checked 'Build admin frontend' { pnpm --filter admin build }
    Invoke-Checked 'Build webmail frontend' { pnpm --filter webmail build }
    Copy-Item (Join-Path $repo 'admin/dist') (Join-Path $publishRoot 'admin') -Recurse
    Copy-Item (Join-Path $repo 'webmail/dist') (Join-Path $publishRoot 'webmail') -Recurse
    Invoke-Checked 'Build self-contained EF migrations bundle' {
        dotnet ef migrations bundle --project (Join-Path $repo 'src/Miautrix.Mail.Persistence') --startup-project (Join-Path $repo 'src/Miautrix.Mail.Web') --self-contained -r win-x64 --output (Join-Path $publishRoot 'efbundle.exe')
    }
    Copy-Item (Join-Path $PSScriptRoot 'windows/install.ps1') $publishRoot
    Copy-Item (Join-Path $PSScriptRoot 'windows/uninstall.ps1') $publishRoot
    Copy-Item (Join-Path $PSScriptRoot 'windows/README-WINDOWS.md') $publishRoot
    $release = [ordered]@{ id=$packageName; gitSha=$gitSha; builtAtUtc=[DateTime]::UtcNow.ToString('o'); tfm='net10.0'; rid='win-x64' }
    $release | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publishRoot 'RELEASE.json') -Encoding utf8
    if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $checksumPath -Value "$hash  $(Split-Path -Leaf $zipPath)" -Encoding ascii
    Write-Host "Package: $zipPath"
    Write-Host "Size: $((Get-Item $zipPath).Length) bytes"
    Write-Host "SHA256: $hash"
    Write-Host 'Top-level files:'
    Get-ChildItem -LiteralPath $publishRoot | Select-Object -ExpandProperty Name
} catch {
    if (Test-Path $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
    throw
}
