BeforeAll {
    $script:ProbePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'smoke-test.ps1'
    Import-Module (Join-Path $PSScriptRoot 'SmokeTestFakes.psm1') -Force
    $script:Certs = @{}
    $script:Certs.Valid = New-TestCertificate -DnsName 'mail.example.test' -AdditionalDnsName @('autoconfig.example.test', 'mta-sts.example.test') -NotAfter ([datetime]::UtcNow.AddDays(60))
    $script:Certs.NearExpiry = New-TestCertificate -DnsName 'mail.example.test' -AdditionalDnsName @('autoconfig.example.test', 'mta-sts.example.test') -NotAfter ([datetime]::UtcNow.AddDays(10))
    $script:Certs.Expired = New-TestCertificate -DnsName 'mail.example.test' -AdditionalDnsName @('autoconfig.example.test', 'mta-sts.example.test') -NotAfter ([datetime]::UtcNow.AddDays(-1))
    $script:Certs.WrongName = New-TestCertificate -DnsName 'wrong.example.test' -NotAfter ([datetime]::UtcNow.AddDays(60))

    function Start-FixtureSet {
        param(
            [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate = $script:Certs.Valid,
            [int]$AnonymousStatus = 401,
            [bool]$AdvertiseAuthBeforeTls = $false,
            [string[]]$ImapCapabilities = @('IMAP4rev1', 'IMAP4rev2'),
            [switch]$MtaStsMissing,
            [switch]$MfaRequired
        )
        $https = Start-FakeHttps -Certificate $Certificate -AuthMeAnonymousStatus $AnonymousStatus -MtaStsMissing:$MtaStsMissing -MfaRequired:$MfaRequired
        $imap = Start-FakeImap -Certificate $Certificate -Capabilities $ImapCapabilities
        $smtp25 = Start-FakeSmtp -Mode StartTls -Certificate $Certificate
        $smtp465 = Start-FakeSmtp -Mode Implicit -Certificate $Certificate
        $smtp587 = Start-FakeSmtp -Mode StartTls -AdvertiseAuthBeforeTls:$AdvertiseAuthBeforeTls -Certificate $Certificate
        return [pscustomobject]@{
            Https = $https
            Imap = $imap
            Smtp25 = $smtp25
            Smtp465 = $smtp465
            Smtp587 = $smtp587
            Certificate = $Certificate
            All = @($https, $imap, $smtp25, $smtp465, $smtp587)
        }
    }

    function Stop-FixtureSet {
        param($Fixtures)
        foreach ($server in $Fixtures.All) {
            Stop-FakeServer $server
        }
    }

    function Invoke-SmokeProbe {
        param(
            [Parameter(Mandatory)]$Fixtures,
            [PSCredential]$Credential,
            [string]$SendTestTo,
            [string]$Domain,
            [switch]$SkipCertificateCheck,
            [switch]$Json
        )
        $env:MIAUTRIX_SMOKE_FAKE_PASSWORD = 'S3cr3t-Smoke-Pw!'
        $environmentArg = if ($Credential) {
            '$secure = ConvertTo-SecureString $env:MIAUTRIX_SMOKE_FAKE_PASSWORD -AsPlainText -Force; $cred = [PSCredential]::new(''smoke@example.test'', $secure); '
        }
        else {
            ''
        }
        $switchArgs = @()
        if ($SkipCertificateCheck) { $switchArgs += '-SkipCertificateCheck' }
        if ($Json) { $switchArgs += '-Json' }
        $domainArg = if ($Domain) { "-Domain '$Domain'" } else { '' }
        $sendArg = if ($SendTestTo) { "-SendTestTo '$SendTestTo'" } else { '' }
        $credentialArg = if ($Credential) { '-Credential $cred' } else { '' }
        $command = @"
$environmentArg
& '$($script:ProbePath.Replace("'", "''"))' -ServerIp 127.0.0.1 -MailHost mail.example.test -HttpsPort $($Fixtures.Https.Port) -ImapsPort $($Fixtures.Imap.Port) -SmtpPorts $($Fixtures.Smtp25.Port),$($Fixtures.Smtp465.Port),$($Fixtures.Smtp587.Port) -TestSmtp25Port $($Fixtures.Smtp25.Port) -TestSmtp465Port $($Fixtures.Smtp465.Port) -TestSmtp587Port $($Fixtures.Smtp587.Port) -TimeoutSeconds 3 -TestTrustedCertificateThumbprint '$($Fixtures.Certificate.Thumbprint)' $domainArg $credentialArg $sendArg $($switchArgs -join ' ')
"@
        try {
            $output = & pwsh -NoProfile -Command $command *>&1
            $exitCode = $LASTEXITCODE
            return [pscustomobject]@{
                ExitCode = $exitCode
                Output = @($output | ForEach-Object { $_.ToString() })
                Text = ($output | ForEach-Object { $_.ToString() }) -join "`n"
                Json = if ($Json) { (($output | ForEach-Object { $_.ToString() }) -join "`n" | ConvertFrom-Json) } else { $null }
            }
        }
        finally {
            Remove-Item Env:MIAUTRIX_SMOKE_FAKE_PASSWORD -ErrorAction SilentlyContinue
        }
    }

    function New-FakeCredential {
        $secure = ConvertTo-SecureString 'S3cr3t-Smoke-Pw!' -AsPlainText -Force
        return [PSCredential]::new('smoke@example.test', $secure)
    }

    function Get-CheckStatus {
        param($Run, [string]$Check)
        return ($Run.Json | Where-Object Check -Like $Check | Select-Object -First 1).Status
    }
}

AfterAll {
    foreach ($certificate in $script:Certs.Values) {
        $certificate.Dispose()
    }
    Remove-Module SmokeTestFakes -ErrorAction SilentlyContinue
}

Describe 'smoke-test parameter validation' {
    It 'returns exit code 2 for an invalid IP' {
        $output = & pwsh -NoProfile -File $script:ProbePath -ServerIp invalid -MailHost mail.example.test 2>&1
        $LASTEXITCODE | Should -Be 2
        ($output -join ' ') | Should -Match 'IPv4 or IPv6'
    }

    It 'returns exit code 2 for a malformed mail hostname' {
        $null = & pwsh -NoProfile -File $script:ProbePath -ServerIp 127.0.0.1 -MailHost 'bad host' 2>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'returns exit code 2 when send mode has no credential' {
        $null = & pwsh -NoProfile -File $script:ProbePath -ServerIp 127.0.0.1 -MailHost mail.example.test -SendTestTo receiver@example.test 2>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'rejects test-only certificate and SMTP port hooks for non-loopback targets' {
        $null = & pwsh -NoProfile -File $script:ProbePath -ServerIp 192.0.2.1 -MailHost mail.example.test -TestTrustedCertificateThumbprint AABB -TestSmtp465Port 5465 2>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'contains no real production address or configured hostname' {
        (Get-Content -Raw $script:ProbePath) | Should -Not -Match '10\.11\.1\.(51|52)|miautrix\.tech'
    }
}

Describe 'full fake server probe' {
    It 'passes every health family on local servers and returns exit code 0' {
        $fixtures = Start-FixtureSet
        try {
            $run = Invoke-SmokeProbe $fixtures -Json
            if ($run.ExitCode -ne 0) { Write-Host $run.Text }
            $run.ExitCode | Should -Be 0
            ($run.Json | Where-Object Status -EQ 'FAIL').Count | Should -Be 0
            Get-CheckStatus $run 'HTTPS API auth' | Should -Be 'PASS'
            Get-CheckStatus $run "SMTP $($fixtures.Smtp587.Port) greeting/EHLO/STARTTLS" | Should -Be 'PASS'
            Get-CheckStatus $run "IMAP $($fixtures.Imap.Port) greeting/CAPABILITY" | Should -Be 'PASS'
            $run.Json[0].PSObject.Properties.Name | Should -Contain 'Check'
            $run.Json[0].PSObject.Properties.Name | Should -Contain 'Status'
            $run.Json[0].PSObject.Properties.Name | Should -Contain 'DurationMs'
            $run.Json[0].PSObject.Properties.Name | Should -Contain 'Detail'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'fails if anonymous auth/me returns 200' {
        $fixtures = Start-FixtureSet -AnonymousStatus 200
        try {
            $run = Invoke-SmokeProbe $fixtures -Json
            $run.ExitCode | Should -Be 1
            Get-CheckStatus $run 'HTTPS API auth' | Should -Be 'FAIL'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'fails when SMTP 587 advertises AUTH before STARTTLS' {
        $fixtures = Start-FixtureSet -AdvertiseAuthBeforeTls $true
        try {
            $run = Invoke-SmokeProbe $fixtures -Json
            $run.ExitCode | Should -Be 1
            Get-CheckStatus $run "SMTP $($fixtures.Smtp587.Port) greeting/EHLO/STARTTLS" | Should -Be 'FAIL'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'fails for an expired certificate' {
        $fixtures = Start-FixtureSet -Certificate $script:Certs.Expired
        try {
            $run = Invoke-SmokeProbe $fixtures -Json
            $run.ExitCode | Should -Be 1
            Get-CheckStatus $run 'TLS SMTP implicit TLS*' | Should -Be 'FAIL'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'warns for an expired certificate when certificate checks are skipped' {
        $fixtures = Start-FixtureSet -Certificate $script:Certs.Expired
        try {
            $run = Invoke-SmokeProbe $fixtures -SkipCertificateCheck -Json
            $run.ExitCode | Should -Be 0
            Get-CheckStatus $run 'TLS SMTP implicit TLS*' | Should -Be 'WARN'
            ($run.Json | Where-Object Check -Like 'TLS SMTP implicit TLS*').Detail | Should -Match 'EXPIRED [0-9]+ days ago'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'fails for a wrong-name certificate' {
        $fixtures = Start-FixtureSet -Certificate $script:Certs.WrongName
        try {
            $run = Invoke-SmokeProbe $fixtures -Json
            $run.ExitCode | Should -Be 1
            Get-CheckStatus $run 'TLS IMAPS*' | Should -Be 'FAIL'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'warns for a wrong-name certificate when certificate checks are skipped' {
        $fixtures = Start-FixtureSet -Certificate $script:Certs.WrongName
        try {
            $run = Invoke-SmokeProbe $fixtures -SkipCertificateCheck -Json
            $run.ExitCode | Should -Be 0
            Get-CheckStatus $run 'TLS IMAPS*' | Should -Be 'WARN'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'warns when the certificate expires within 21 days' {
        $fixtures = Start-FixtureSet -Certificate $script:Certs.NearExpiry
        try {
            $run = Invoke-SmokeProbe $fixtures -Json
            $run.ExitCode | Should -Be 0
            Get-CheckStatus $run 'TLS IMAPS*' | Should -Be 'WARN'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'reports IMAP4rev2 as true and false without failing' {
        $rev2Fixtures = Start-FixtureSet -ImapCapabilities @('IMAP4rev1', 'IMAP4rev2')
        try {
            $run = Invoke-SmokeProbe $rev2Fixtures -Json
            $run.ExitCode | Should -Be 0
            ($run.Json | Where-Object Check -Like 'IMAP *').Detail | Should -Match 'IMAP4rev2 advertised: True'
        }
        finally { Stop-FixtureSet $rev2Fixtures }

        $rev1Fixtures = Start-FixtureSet -ImapCapabilities @('IMAP4rev1')
        try {
            $run = Invoke-SmokeProbe $rev1Fixtures -Json
            $run.ExitCode | Should -Be 0
            ($run.Json | Where-Object Check -Like 'IMAP *').Detail | Should -Match 'IMAP4rev2 advertised: False'
        }
        finally { Stop-FixtureSet $rev1Fixtures }
    }

    It 'passes API, IMAP and SMTP credential checks' {
        $fixtures = Start-FixtureSet
        try {
            $credential = New-FakeCredential
            $run = Invoke-SmokeProbe $fixtures -Credential $credential -Json
            $run.ExitCode | Should -Be 0
            Get-CheckStatus $run 'Authenticated API login/logout' | Should -Be 'PASS'
            Get-CheckStatus $run 'Authenticated IMAP EXAMINE' | Should -Be 'PASS'
            Get-CheckStatus $run 'Authenticated SMTP AUTH' | Should -Be 'PASS'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'sends one test message and reports the queue response' {
        $fixtures = Start-FixtureSet
        try {
            $credential = New-FakeCredential
            $run = Invoke-SmokeProbe $fixtures -Credential $credential -SendTestTo receiver@example.test -Json
            $run.ExitCode | Should -Be 0
            Get-CheckStatus $run 'SMTP test message' | Should -Be 'PASS'
            ($run.Json | Where-Object Check -EQ 'SMTP test message').Detail | Should -Match 'TEST123'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'skips an MFA-required API login with the requested guidance' {
        $fixtures = Start-FixtureSet -MfaRequired
        try {
            $credential = New-FakeCredential
            $run = Invoke-SmokeProbe $fixtures -Credential $credential -Json
            $run.ExitCode | Should -Be 0
            Get-CheckStatus $run 'Authenticated API login/logout' | Should -Be 'SKIP'
            ($run.Json | Where-Object Check -EQ 'Authenticated API login/logout').Detail | Should -Match 'MFA enabled; add -TotpCode'
        }
        finally { Stop-FixtureSet $fixtures }
    }

    It 'passes autoconfig and MTA-STS, and reports a missing MTA-STS policy as INFO' {
        $fixtures = Start-FixtureSet
        try {
            $run = Invoke-SmokeProbe $fixtures -Domain example.test -Json
            if ($run.ExitCode -ne 0) { Write-Host $run.Text }
            $run.ExitCode | Should -Be 0
            Get-CheckStatus $run 'Thunderbird autoconfig' | Should -Be 'PASS'
            Get-CheckStatus $run 'MTA-STS policy' | Should -Be 'PASS'
        }
        finally { Stop-FixtureSet $fixtures }

        $missingPolicy = Start-FixtureSet -MtaStsMissing
        try {
            $run = Invoke-SmokeProbe $missingPolicy -Domain example.test -Json
            if ($run.ExitCode -ne 0) { Write-Host $run.Text }
            $run.ExitCode | Should -Be 0
            Get-CheckStatus $run 'MTA-STS policy' | Should -Be 'INFO'
        }
        finally { Stop-FixtureSet $missingPolicy }
    }

    It 'masks the fake password, AUTH PLAIN payload and token from all captured output' {
        $fixtures = Start-FixtureSet
        try {
            $credential = New-FakeCredential
            $run = Invoke-SmokeProbe $fixtures -Credential $credential -Json
            $run.ExitCode | Should -Be 0
            $expectedPlain = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("`0smoke@example.test`0S3cr3t-Smoke-Pw!"))
            $run.Text | Should -Not -Match 'S3cr3t-Smoke-Pw!'
            $run.Text | Should -Not -Match ([regex]::Escape($expectedPlain))
            $run.Text | Should -Not -Match 'FAKE_ACCESS_TOKEN_01|FAKE_REFRESH_TOKEN_01'
            $run.Output | Should -Not -Contain 'S3cr3t-Smoke-Pw!'
            $run.Output | Should -Not -Contain $expectedPlain
            ($fixtures.Smtp587.LastAuthPlainBase64) | Should -Be $expectedPlain
        }
        finally { Stop-FixtureSet $fixtures }
    }
}
