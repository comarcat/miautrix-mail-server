<#!
.SYNOPSIS
Runs read-only LAN checks against a Miautrix Mail Server by IP.

.DESCRIPTION
The server IP is the only network destination. MailHost is used as TLS SNI and
the HTTPS Host header. Domain selects the Host header for discovery endpoints.

.PARAMETER ServerIp
IPv4 or IPv6 address of the server on the local network.

.PARAMETER MailHost
Public mail hostname used for TLS name validation and HTTPS Host.

.PARAMETER Domain
Optional mail domain for Thunderbird autoconfig and MTA-STS.

.PARAMETER Credential
Optional mailbox credentials for API, IMAP and SMTP authenticated checks.

.PARAMETER TotpCode
Optional TOTP code when the account requires MFA.

.PARAMETER SendTestTo
Optional recipient address. Sends exactly one plain text test message.

.EXAMPLE
./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname>

.EXAMPLE
./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname> -Domain <mail-domain> -Credential (Get-Credential) -Json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ServerIp,
    [Parameter(Mandatory)][string]$MailHost,
    [string]$Domain,
    [ValidateRange(1,65535)][int]$HttpsPort = 443,
    [ValidateRange(1,65535)][int]$ImapsPort = 993,
    [int[]]$SmtpPorts = @(25,465,587),
    [ValidateRange(1,120)][int]$TimeoutSeconds = 10,
    [PSCredential]$Credential,
    [string]$TotpCode,
    [switch]$SkipCertificateCheck,
    [switch]$Json,
    [string]$SendTestTo,
    [Parameter(DontShow)][string]$TestTrustedCertificateThumbprint,
    [Parameter(DontShow)][ValidateRange(1,65535)][int]$TestSmtp25Port = 25,
    [Parameter(DontShow)][ValidateRange(1,65535)][int]$TestSmtp465Port = 465,
    [Parameter(DontShow)][ValidateRange(1,65535)][int]$TestSmtp587Port = 587
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    [Console]::Error.WriteLine('PowerShell 7 or later is required.')
    exit 2
}

$parsedIp = $null
if (-not [Net.IPAddress]::TryParse($ServerIp, [ref]$parsedIp)) {
    [Console]::Error.WriteLine('-ServerIp must be an IPv4 or IPv6 address.')
    exit 2
}
if ($MailHost -notmatch '^(?=.{1,253}$)(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)*[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$') {
    [Console]::Error.WriteLine('-MailHost must be a DNS hostname.')
    exit 2
}
if ($Domain -and $Domain -notmatch '^(?=.{1,253}$)(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$') {
    [Console]::Error.WriteLine('-Domain must be a valid DNS domain.')
    exit 2
}
if ($SmtpPorts.Count -eq 0 -or @($SmtpPorts | Where-Object { $_ -lt 1 -or $_ -gt 65535 }).Count -gt 0) {
    [Console]::Error.WriteLine('-SmtpPorts must contain valid ports.')
    exit 2
}
if ($SendTestTo -and -not $Credential) {
    [Console]::Error.WriteLine('-SendTestTo requires -Credential.')
    exit 2
}
if ($TestTrustedCertificateThumbprint -and $parsedIp -ne [Net.IPAddress]::Loopback) {
    [Console]::Error.WriteLine('-TestTrustedCertificateThumbprint is only allowed with -ServerIp 127.0.0.1.')
    exit 2
}
if (($TestSmtp25Port -ne 25 -or $TestSmtp465Port -ne 465 -or $TestSmtp587Port -ne 587) -and $parsedIp -ne [Net.IPAddress]::Loopback) {
    [Console]::Error.WriteLine('Test SMTP port overrides are only allowed with -ServerIp 127.0.0.1.')
    exit 2
}

$script:Results = [Collections.Generic.List[object]]::new()
$script:Secrets = [Collections.Generic.List[string]]::new()
$script:IpAddress = $parsedIp
$script:HostName = $MailHost
$script:Timeout = $TimeoutSeconds
$script:IgnoreCertificateErrors = [bool]$SkipCertificateCheck
$script:TestTrustedCertificateThumbprint = $TestTrustedCertificateThumbprint
$script:SmtpPort25 = $TestSmtp25Port
$script:SmtpPort465 = $TestSmtp465Port
$script:SmtpPort587 = $TestSmtp587Port
$script:CredentialUser = $null
$script:CredentialPassword = $null
$script:ApiToken = $null
$script:RefreshToken = $null
$script:PlainAuthBase64 = $null

Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public static class SmokeHttpTransport
{
    private static bool SkipCertificateCheck;
    private static string TrustedThumbprint;

    public static void Configure(bool skipCertificateCheck, string trustedThumbprint)
    {
        SkipCertificateCheck = skipCertificateCheck;
        TrustedThumbprint = trustedThumbprint;
    }

    public static bool ValidateCertificate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain, SslPolicyErrors errors)
    {
        if (SkipCertificateCheck || errors == SslPolicyErrors.None) return true;
        return !String.IsNullOrEmpty(TrustedThumbprint) &&
            certificate.GetCertHashString().Equals(TrustedThumbprint, StringComparison.OrdinalIgnoreCase) &&
            (errors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0;
    }

    public static SocketsHttpHandler Create(string address, bool skipCertificateCheck, string trustedThumbprint)
    {
        Configure(skipCertificateCheck, trustedThumbprint);
        var ip = IPAddress.Parse(address);
        var handler = new SocketsHttpHandler();
        handler.AllowAutoRedirect = false;
        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            var tcp = new TcpClient(ip.AddressFamily);
            await tcp.ConnectAsync(ip, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            return tcp.GetStream();
        };
        handler.SslOptions.RemoteCertificateValidationCallback = ValidateCertificate;
        return handler;
    }
}
'@

function Protect-Text {
    param([AllowNull()][string]$Text)
    if ($null -eq $Text) { return '' }
    $safe = $Text
    foreach ($secret in $script:Secrets) {
        if (-not [string]::IsNullOrEmpty($secret)) {
            $safe = $safe.Replace($secret, '[REDACTED]')
        }
    }
    $safe = $safe -replace '(?i)(password|token|cookie|authorization)\s*[:=]?\s*\S+', '$1=[REDACTED]'
    return $safe
}

function Add-Result {
    param([string]$Name, [string]$Status, [string]$Detail, [long]$DurationMs)
    $script:Results.Add([pscustomobject]@{
        Check = $Name
        Status = $Status
        DurationMs = $DurationMs
        Detail = Protect-Text $Detail
    })
}

function Invoke-Check {
    param([string]$Name, [scriptblock]$Action)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        $result = & $Action
        Add-Result $Name $result.Status $result.Detail $timer.ElapsedMilliseconds
    }
    catch {
        Add-Result $Name 'FAIL' $_.Exception.Message $timer.ElapsedMilliseconds
    }
}

function Open-ServerTcp {
    param([int]$Port)
    $client = [Net.Sockets.TcpClient]::new($script:IpAddress.AddressFamily)
    $async = $client.BeginConnect($script:IpAddress, $Port, $null, $null)
    if (-not $async.AsyncWaitHandle.WaitOne([timespan]::FromSeconds($script:Timeout))) {
        $client.Dispose()
        throw 'Connection timed out.'
    }
    $client.EndConnect($async)
    return $client
}

function New-ProtocolIo {
    param([IO.Stream]$Stream)
    $reader = [IO.StreamReader]::new($Stream, [Text.Encoding]::ASCII, $false, 1024, $true)
    $writer = [IO.StreamWriter]::new($Stream, [Text.Encoding]::ASCII, 1024, $true)
    $writer.NewLine = "`r`n"
    $writer.AutoFlush = $true
    return [pscustomobject]@{
        Reader = $reader
        Writer = $writer
    }
}

function Read-ProtocolLine {
    param([IO.StreamReader]$Reader)
    $line = $Reader.ReadLine()
    if ($null -eq $line) { throw 'Remote server closed the connection.' }
    return $line
}

function Read-SmtpReply {
    param([IO.StreamReader]$Reader)
    $lines = [Collections.Generic.List[string]]::new()
    do {
        $line = Read-ProtocolLine $Reader
        $lines.Add($line)
    } while ($line -match '^\d{3}-')
    return ,$lines.ToArray()
}

function Start-ServerTls {
    param([Net.Sockets.TcpClient]$Client, [string]$TargetHost)
    [SmokeHttpTransport]::Configure($script:IgnoreCertificateErrors, $script:TestTrustedCertificateThumbprint)
    $callback = [Net.Security.RemoteCertificateValidationCallback][SmokeHttpTransport]::ValidateCertificate
    $ssl = [Net.Security.SslStream]::new($Client.GetStream(), $false, $callback)
    $ssl.AuthenticateAsClient($TargetHost, $null, [Security.Authentication.SslProtocols]::Tls12 -bor [Security.Authentication.SslProtocols]::Tls13, $false)
    return $ssl
}

function Test-TcpPorts {
    $ports = @($HttpsPort, $ImapsPort) + $SmtpPorts | Select-Object -Unique
    foreach ($port in $ports) {
        Invoke-Check "TCP $port" {
            $client = Open-ServerTcp $port
            $client.Dispose()
            return @{ Status = 'PASS'; Detail = 'TCP connection succeeded.' }
        }
    }
}

function Test-TlsEndpoint {
    param([int]$Port, [string]$Label)
    Invoke-Check "TLS $Label ($Port)" {
        $client = Open-ServerTcp $Port
        try {
            $ssl = Start-ServerTls $client $script:HostName
            try {
                $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($ssl.RemoteCertificate)
                $daysLeft = [math]::Floor(($certificate.NotAfter - [datetime]::Now).TotalDays)
                if ($daysLeft -lt 0 -and -not $script:IgnoreCertificateErrors) {
                    throw "Certificate EXPIRED $([math]::Abs($daysLeft)) days ago."
                }
                if ($ssl.SslProtocol -notin @('Tls12', 'Tls13')) { throw "Unsupported TLS protocol $($ssl.SslProtocol)." }
                $status = 'PASS'
                if ($daysLeft -lt 21 -or $script:IgnoreCertificateErrors) { $status = 'WARN' }
                $expiryDetail = if ($daysLeft -lt 0) { "certificate EXPIRED $([math]::Abs($daysLeft)) days ago" } else { "certificate expires in $daysLeft days" }
                return @{ Status = $status; Detail = "TLS $($ssl.SslProtocol); $expiryDetail." }
            }
            finally { $ssl.Dispose() }
        }
        finally { $client.Dispose() }
    }
}

function Test-SmtpStartTls {
    param([int]$Port)
    Invoke-Check "SMTP $Port greeting/EHLO/STARTTLS" {
        $client = Open-ServerTcp $Port
        try {
            $io = New-ProtocolIo $client.GetStream()
            $greeting = Read-SmtpReply $io.Reader
            if ($greeting[0] -notmatch '^220') { throw 'SMTP greeting was not 220.' }
            $io.Writer.WriteLine("EHLO $script:HostName")
            $beforeTls = Read-SmtpReply $io.Reader
            $capabilities = $beforeTls -join ' '
            if ($Port -eq $script:SmtpPort587 -and $capabilities -match '(?i)AUTH') { throw 'AUTH was advertised before TLS.' }
            if ($Port -eq $script:SmtpPort25 -and $capabilities -match '(?i)AUTH') { throw 'AUTH was advertised without TLS.' }
            if ($capabilities -notmatch '(?i)STARTTLS') { throw 'STARTTLS was not advertised.' }
            $io.Writer.WriteLine('STARTTLS')
            if ((Read-SmtpReply $io.Reader)[0] -notmatch '^220') { throw 'STARTTLS was rejected.' }
            $ssl = Start-ServerTls $client $script:HostName
            try {
                $tlsIo = New-ProtocolIo $ssl
                $tlsIo.Writer.WriteLine("EHLO $script:HostName")
                $afterTls = Read-SmtpReply $tlsIo.Reader
                if ($Port -eq $script:SmtpPort587 -and (($afterTls -join ' ') -notmatch '(?i)AUTH')) { throw 'AUTH was not advertised after STARTTLS.' }
                if ($Credential -and $Port -eq $script:SmtpPort587) { Invoke-SmtpAuthentication $tlsIo $false }
                if ($Credential -and $Port -eq $script:SmtpPort587) { Add-Result 'Authenticated SMTP AUTH' 'PASS' 'AUTH succeeded over STARTTLS; session closed with QUIT.' 0 }
                if ($SendTestTo -and $Port -eq $script:SmtpPort587) { Send-TestMessage $tlsIo }
                $tlsIo.Writer.WriteLine('QUIT')
                return @{ Status = 'PASS'; Detail = 'Greeting, EHLO and STARTTLS policy passed.' }
            }
            finally { $ssl.Dispose() }
        }
        finally { $client.Dispose() }
    }
}

function Test-SmtpImplicitTls {
    Invoke-Check "SMTP $script:SmtpPort465 greeting/EHLO" {
        $client = Open-ServerTcp $script:SmtpPort465
        try {
            $ssl = Start-ServerTls $client $script:HostName
            try {
                $io = New-ProtocolIo $ssl
                $greeting = Read-SmtpReply $io.Reader
                if ($greeting[0] -notmatch '^220') { throw 'SMTP greeting was not 220.' }
                $io.Writer.WriteLine("EHLO $script:HostName")
                $ehlo = Read-SmtpReply $io.Reader
                if (-not $ehlo) { throw 'SMTP EHLO returned no response.' }
                $io.Writer.WriteLine('QUIT')
                return @{ Status = 'PASS'; Detail = 'Implicit TLS greeting and EHLO passed.' }
            }
            finally {
                $ssl.Dispose()
            }
        }
        finally {
            $client.Dispose()
        }
    }
}

function New-PlainAuth {
    $bytes = [Text.Encoding]::UTF8.GetBytes("`0$script:CredentialUser`0$script:CredentialPassword")
    $script:PlainAuthBase64 = [Convert]::ToBase64String($bytes)
    $script:Secrets.Add($script:PlainAuthBase64)
    return $script:PlainAuthBase64
}

function Invoke-SmtpAuthentication {
    param($Io, [bool]$QuitOnFailure)
    $plain = New-PlainAuth
    $Io.Writer.WriteLine("AUTH PLAIN $plain")
    $reply = Read-SmtpReply $Io.Reader
    if ($reply[0] -notmatch '^235') { throw "SMTP AUTH failed: $($reply -join ' ')" }
}

function Send-TestMessage {
    param($Io)
    $Io.Writer.WriteLine("MAIL FROM:<$script:CredentialUser>")
    if ((Read-SmtpReply $Io.Reader)[0] -notmatch '^250') { throw 'MAIL FROM was rejected.' }
    $Io.Writer.WriteLine("RCPT TO:<$SendTestTo>")
    if ((Read-SmtpReply $Io.Reader)[0] -notmatch '^250|^251') { throw 'RCPT TO was rejected.' }
    $Io.Writer.WriteLine('DATA')
    if ((Read-SmtpReply $Io.Reader)[0] -notmatch '^354') { throw 'DATA was rejected.' }
    $subject = 'Miautrix smoke test ' + [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $Io.Writer.WriteLine("From: <$script:CredentialUser>")
    $Io.Writer.WriteLine("To: <$SendTestTo>")
    $Io.Writer.WriteLine("Subject: $subject")
    $Io.Writer.WriteLine('Content-Type: text/plain; charset=utf-8')
    $Io.Writer.WriteLine()
    $Io.Writer.WriteLine('This is a Miautrix server smoke test.')
    $Io.Writer.WriteLine('.')
    $reply = Read-SmtpReply $Io.Reader
    if ($reply[0] -notmatch '^250') { throw "Message submission failed: $($reply -join ' ')" }
    Add-Result 'SMTP test message' 'PASS' "Message accepted: $($reply -join ' ')" 0
}

function Test-Imap {
    Invoke-Check "IMAP $ImapsPort greeting/CAPABILITY" {
        $client = Open-ServerTcp $ImapsPort
        try {
            $ssl = Start-ServerTls $client $script:HostName
            try {
                $io = New-ProtocolIo $ssl
                $greeting = Read-ProtocolLine $io.Reader
                if ($greeting -notmatch '^\* OK') { throw 'IMAP greeting was not OK.' }
                $io.Writer.WriteLine('a1 CAPABILITY')
                $capabilities = [Collections.Generic.List[string]]::new()
                do { $line = Read-ProtocolLine $io.Reader; $capabilities.Add($line) } while ($line -notmatch '^a1 ')
                if (($capabilities -join ' ') -notmatch '(?i)IMAP4rev1|IMAP4rev2') { throw 'IMAP version capability was missing.' }
                $rev2 = [bool](($capabilities -join ' ') -match '(?i)IMAP4rev2')
                if ($Credential) { Invoke-ImapAuthentication $io; Test-ImapExamine $io }
                return @{ Status = 'PASS'; Detail = "CAPABILITY received; IMAP4rev2 advertised: $rev2." }
            }
            finally { $ssl.Dispose() }
        }
        finally { $client.Dispose() }
    }
}

function Invoke-ImapAuthentication {
    param($Io)
    $plain = New-PlainAuth
    $Io.Writer.WriteLine("a2 AUTHENTICATE PLAIN $plain")
    $reply = Read-ProtocolLine $Io.Reader
    if ($reply -match '^\+') { $Io.Writer.WriteLine($plain); $reply = Read-ProtocolLine $Io.Reader }
    if ($reply -notmatch '^a2 OK') {
        $Io.Writer.WriteLine("a2 LOGIN `"$script:CredentialUser`" `"$script:CredentialPassword`"")
        $reply = Read-ProtocolLine $Io.Reader
    }
    if ($reply -notmatch '^a2 OK|^a3 OK') { throw 'IMAP authentication failed.' }
}

function Test-ImapExamine {
    param($Io)
    $Io.Writer.WriteLine('a3 EXAMINE INBOX')
    $line = Read-ProtocolLine $Io.Reader
    while ($line -notmatch '^a3 OK') { if ($line -match '^a3 (NO|BAD)') { throw 'EXAMINE INBOX failed.' }; $line = Read-ProtocolLine $Io.Reader }
    $Io.Writer.WriteLine('a4 LOGOUT')
    [void](Read-ProtocolLine $Io.Reader)
    Add-Result 'Authenticated IMAP EXAMINE' 'PASS' 'AUTH succeeded; EXAMINE INBOX was read-only.' 0
}

function New-HttpClient {
    $handler = [SmokeHttpTransport]::Create($script:IpAddress.ToString(), $script:IgnoreCertificateErrors, $script:TestTrustedCertificateThumbprint)
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [timespan]::FromSeconds($script:Timeout)
    return $client
}

function Invoke-HttpRequest {
    param([string]$HostHeader, [string]$Path, [string]$Method = 'GET', [object]$Body = $null, [string]$Bearer = $null)
    $client = New-HttpClient
    try {
        $request = [Net.Http.HttpRequestMessage]::new($Method, [uri]::new("https://$HostHeader`:$HttpsPort$Path"))
        $request.Headers.Host = $HostHeader
        $request.Headers.Add('X-Smoke-Probe', '1')
        if ($Bearer) { $request.Headers.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Bearer) }
        if ($Method -eq 'POST') { $request.Headers.Add('Idempotency-Key', [guid]::NewGuid().ToString()) }
        if ($null -ne $Body) { $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Compress), [Text.Encoding]::UTF8, 'application/json') }
        return $client.Send($request)
    }
    finally { $client.Dispose() }
}

function Test-HttpsRoutes {
    foreach ($route in @(@('/api/v1/auth/me', 'API auth'), @('/', 'Webmail root'), @('/admin/', 'Admin'))) {
        Invoke-Check "HTTPS $($route[1])" {
            $response = Invoke-HttpRequest $script:HostName $route[0]
            $code = [int]$response.StatusCode
            $hsts = $response.Headers.Contains('Strict-Transport-Security')
            if ($route[0] -eq '/api/v1/auth/me' -and $code -ne 401) {
                if ($code -eq 200) { throw 'Unauthenticated /auth/me returned 200.' }
                throw "Expected 401; received HTTP $code."
            }
            if ($route[0] -ne '/api/v1/auth/me' -and ($code -lt 200 -or $code -ge 400)) { throw "HTTP $code." }
            if (-not $hsts) { throw 'Strict-Transport-Security header is absent.' }
            $status = if ($script:IgnoreCertificateErrors) { 'WARN' } else { 'PASS' }
            return @{ Status = $status; Detail = "HTTP $code; HSTS present." }
        }
    }
}

function Test-Discovery {
    if (-not $Domain) { return }
    Invoke-Check 'Thunderbird autoconfig' {
        $response = Invoke-HttpRequest "autoconfig.$Domain" '/.well-known/autoconfig/mail/config-v1.1.xml'
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if ([int]$response.StatusCode -ne 200 -or $body -notmatch '(?i)imap' -or $body -notmatch '(?i)smtp') { throw 'Expected XML naming IMAP and SMTP.' }
        return @{ Status = 'PASS'; Detail = 'XML names IMAP and SMTP.' }
    }
    Invoke-Check 'MTA-STS policy' {
        $response = Invoke-HttpRequest "mta-sts.$Domain" '/.well-known/mta-sts.txt'
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if ([int]$response.StatusCode -eq 404) { return @{ Status = 'INFO'; Detail = 'Policy not configured (mode none or absent).' } }
        if ([int]$response.StatusCode -ne 200 -or $body -notmatch '(?im)^version:\s*STSv1\s*$' -or $body -notmatch '(?im)^mode:\s*(enforce|testing|none)\s*$' -or $body -notmatch '(?im)^max_age:\s*\d+\s*$') { throw 'MTA-STS policy is invalid.' }
        return @{ Status = 'PASS'; Detail = 'Policy syntax is valid.' }
    }
}

function Test-ApiCredentials {
    if (-not $Credential) { return }
    Invoke-Check 'Authenticated API login/logout' {
        $body = @{ emailOrUsername = $script:CredentialUser; password = $script:CredentialPassword; totpCode = $TotpCode }
        $login = Invoke-HttpRequest $script:HostName '/api/v1/auth/login' 'POST' $body
        $json = $login.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        if ([int]$login.StatusCode -ne 200) {
            $loginError = ''
            if ($json.PSObject.Properties['message']) { $loginError = [string]$json.message }
            if ($json.PSObject.Properties['error'] -and $json.error.PSObject.Properties['message']) { $loginError += ' ' + [string]$json.error.message }
            if (($loginError -match '(?i)mfa|two.factor|totp') -and -not $TotpCode) { return @{ Status = 'SKIP'; Detail = 'MFA enabled; add -TotpCode.' } }
            throw 'API login failed.'
        }
        if (-not $json.PSObject.Properties['token'] -or -not $json.PSObject.Properties['refreshToken']) { throw 'API login response omitted tokens.' }
        $script:ApiToken = [string]$json.token
        $script:RefreshToken = [string]$json.refreshToken
        $script:Secrets.Add($script:ApiToken)
        $script:Secrets.Add($script:RefreshToken)
        if (-not $script:ApiToken -or -not $script:RefreshToken) { throw 'API login response omitted tokens.' }
        $me = Invoke-HttpRequest $script:HostName '/api/v1/auth/me' 'GET' $null $script:ApiToken
        if ([int]$me.StatusCode -ne 200) { throw 'Authenticated /auth/me did not return 200.' }
        $logout = Invoke-HttpRequest $script:HostName '/api/v1/auth/logout' 'POST' @{} $script:ApiToken
        if ([int]$logout.StatusCode -ne 204) { throw 'API logout did not return 204.' }
        $after = Invoke-HttpRequest $script:HostName '/api/v1/auth/me' 'GET' $null $script:ApiToken
        if ([int]$after.StatusCode -ne 401) { throw 'Logged-out token was not rejected.' }
        return @{ Status = 'PASS'; Detail = 'Login, authenticated /me, logout and token revocation passed.' }
    }
}

function Clear-Secrets {
    $script:CredentialPassword = $null
    $script:ApiToken = $null
    $script:RefreshToken = $null
    $script:PlainAuthBase64 = $null
    if ($Credential) { $Credential.Password.Dispose() }
}

try {
    if ($Credential) {
        $script:CredentialUser = $Credential.UserName
        $script:CredentialPassword = [Net.NetworkCredential]::new('', $Credential.Password).Password
        $script:Secrets.Add($script:CredentialPassword)
    }
    if ($TotpCode) { $script:Secrets.Add($TotpCode) }

    Test-TcpPorts
    Test-TlsEndpoint $script:SmtpPort465 'SMTP implicit TLS'
    Test-TlsEndpoint $ImapsPort 'IMAPS'
    Test-SmtpImplicitTls
    foreach ($port in $SmtpPorts | Where-Object { $_ -in @($script:SmtpPort25, $script:SmtpPort587) }) { Test-SmtpStartTls $port }
    Test-Imap
    Test-HttpsRoutes
    Test-Discovery
    Test-ApiCredentials

}
finally {
    Clear-Secrets
}

$failureCount = @($script:Results | Where-Object Status -eq 'FAIL').Count
if ($Json) {
    $jsonOutput = $script:Results | ConvertTo-Json -Depth 4
    [Console]::Out.WriteLine((Protect-Text $jsonOutput))
}
else {
    $table = $script:Results | Select-Object Check, Status, DurationMs, Detail | Format-Table -AutoSize | Out-String
    [Console]::Out.WriteLine((Protect-Text $table))
    [Console]::Out.WriteLine("Summary: $(@($script:Results | Where-Object Status -eq 'PASS').Count) PASS, $(@($script:Results | Where-Object Status -eq 'WARN').Count) WARN, $failureCount FAIL, $(@($script:Results | Where-Object Status -eq 'SKIP').Count) SKIP, $(@($script:Results | Where-Object Status -eq 'INFO').Count) INFO.")
}
$script:Secrets.Clear()
if ($failureCount -gt 0) { exit 1 }
exit 0
