BeforeAll {
    $script:Probe = Join-Path (Split-Path $PSScriptRoot -Parent) 'smoke-test.ps1'
    Add-Type -TypeDefinition @'
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
public static class LocalProbeServer
{
    public static TcpListener Listener;
    public static void Start(TcpListener listener, int accepts)
    {
        Listener = listener;
        var thread = new Thread(() => {
            for (var i = 0; i < accepts; i++) {
                try { using (var client = Listener.AcceptTcpClient()) { } }
                catch { break; }
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }
}
'@
}

Describe 'smoke-test parameter validation' {
    It 'returns exit code 2 for an invalid IP' {
        $output = & pwsh -NoProfile -File $script:Probe -ServerIp invalid -MailHost mail.example.test 2>&1
        $LASTEXITCODE | Should -Be 2
        ($output -join ' ') | Should -Match 'IPv4 or IPv6'
    }

    It 'returns exit code 2 for a malformed mail hostname' {
        $output = & pwsh -NoProfile -File $script:Probe -ServerIp 127.0.0.1 -MailHost 'bad host' 2>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'returns exit code 2 when send mode has no credential' {
        $output = & pwsh -NoProfile -File $script:Probe -ServerIp 127.0.0.1 -MailHost mail.example.test -SendTestTo receiver@example.test 2>&1
        $LASTEXITCODE | Should -Be 2
    }

    It 'contains no real production address or configured hostname' {
        $source = Get-Content -Raw $script:Probe
        $source | Should -Not -Match '10\.11\.1\.(51|52)|miautrix\.tech'
    }
}

Describe 'loopback probe and JSON output' {
    It 'reports local TCP reachability and local protocol failures as JSON' {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
        [LocalProbeServer]::Start($listener, 10)
        try {
            $output = & pwsh -NoProfile -File $script:Probe -ServerIp 127.0.0.1 -MailHost mail.example.test -HttpsPort $port -ImapsPort $port -SmtpPorts $port -TimeoutSeconds 1 -Json 2>&1
            $exit = $LASTEXITCODE
            $joined = $output -join "`n"
            $joined | Should -Match '"Check"'
            $joined | Should -Match '"Status"'
            $joined | Should -Match '"DurationMs"'
            $joined | Should -Match '"Detail"'
            $exit | Should -Be 1
            $joined | Should -Match '"Status":\s*"PASS"'
            $joined | Should -Match '"Status":\s*"FAIL"'
        }
        finally {
            $listener.Stop()
        }
    }
}
