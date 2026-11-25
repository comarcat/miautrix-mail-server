BeforeAll {
    $script:Probe = Join-Path (Split-Path $PSScriptRoot -Parent) 'smoke-test.ps1'
}

Describe 'smoke-test parameter and safety behavior' {
    It 'rejects an invalid IP with exit code 2' {
        $output = & pwsh -NoProfile -File $script:Probe -ServerIp 'not-an-ip' -MailHost 'mail.example.test' 2>&1
        $LASTEXITCODE | Should -Be 2
        ($output -join ' ') | Should -Match 'IPv4 or IPv6'
    }

    It 'rejects a malformed mail hostname with exit code 2' {
        $output = & pwsh -NoProfile -File $script:Probe -ServerIp '127.0.0.1' -MailHost 'bad host' 2>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'does not accept arbitrary ports' {
        $output = & pwsh -NoProfile -File $script:Probe -ServerIp '127.0.0.1' -MailHost 'mail.example.test' -SmtpPorts 0 2>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'contains no configured real server address or hostname' {
        $source = Get-Content -Raw $script:Probe
        $source | Should -Not -Match '10\.11\.1\.(51|52)|miautrix\.tech'
    }
}
