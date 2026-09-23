# Inject a test email to flows@miautrix.tech with a configurable file attachment.
#
# This version does NOT require swaks. It uses a minimal SMTP client implemented
# with .NET SslStream (STARTTLS + AUTH LOGIN).
#
# Usage (recommended):
#   $env:SMTP_HOST="mail.miautrix.tech"
#   $env:SMTP_PORT="587"
#   $env:SMTP_USERNAME="user@miautrix.tech"
#   $env:SMTP_PASSWORD="..."   # NOT echoed
#   $env:SMTP_FROM="user@miautrix.tech"  # optional
#   $env:TEST_SUBJECT="[miautrix] ZIP attachment file test" # optional
#   .\scripts\inject-flow-mail-test.ps1 -AttachmentPath .\empty.txt
#
# Optional:
#   $env:SMTP_SKIP_TLS_VERIFY="true"  # only for self-signed test certs

[CmdletBinding()]
param(
    [string]$ToAddress = 'owner@miautrix.tech',
    [string]$Subject = $env:TEST_SUBJECT,
    [string]$AttachmentPath = $env:TEST_ATTACHMENT_PATH
)

$ErrorActionPreference = 'Stop'

function Get-EnvRequired([string]$Name) {
    $v = (Get-Item -Path "Env:$Name" -ErrorAction SilentlyContinue).Value
    if ([string]::IsNullOrWhiteSpace($v)) { throw "$Name is required." }
    return $v
}

$hostName = if ([string]::IsNullOrWhiteSpace($env:SMTP_HOST)) { 'mail.miautrix.tech' } else { $env:SMTP_HOST }
$port = if ([string]::IsNullOrWhiteSpace($env:SMTP_PORT)) { 587 } else { [int]$env:SMTP_PORT }
$username = Get-EnvRequired 'SMTP_USERNAME'
$password = Get-EnvRequired 'SMTP_PASSWORD'
$fromAddress = if ([string]::IsNullOrWhiteSpace($env:SMTP_FROM)) { $username } else { $env:SMTP_FROM }

if ([string]::IsNullOrWhiteSpace($Subject)) { $Subject = '[miautrix] ZIP attachment file test' }

if ([string]::IsNullOrWhiteSpace($AttachmentPath)) {
    $AttachmentPath = Join-Path (Get-Item $PSScriptRoot).Parent.FullName 'empty.txt'
}
if (-not (Test-Path -LiteralPath $AttachmentPath -PathType Leaf)) {
    throw "AttachmentPath not found: $AttachmentPath"
}
$attachmentItem = Get-Item -LiteralPath $AttachmentPath
$attachmentFileName = $attachmentItem.Name
$attachmentBytes = [System.IO.File]::ReadAllBytes($attachmentItem.FullName)
$attachmentBase64 = [Convert]::ToBase64String($attachmentBytes)
$attachmentBase64Lines = if ($attachmentBase64.Length -eq 0) {
    @('')
} else {
    for ($i = 0; $i -lt $attachmentBase64.Length; $i += 76) {
        $attachmentBase64.Substring($i, [Math]::Min(76, $attachmentBase64.Length - $i))
    }
}
$attachmentContentType = 'application/octet-stream'

$skipTlsVerify = $false
if (-not [string]::IsNullOrWhiteSpace($env:SMTP_SKIP_TLS_VERIFY)) {
    $skipTlsVerify = $env:SMTP_SKIP_TLS_VERIFY.ToLowerInvariant() -eq 'true'
}

$crlf = "`r`n"
$boundary = '----=_miautrix_flow_test_' + (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
$msgGuid = [Guid]::NewGuid().ToString()
$messageId = "<$msgGuid@$hostName>"

# Build message.
$text = "This is a test message to verify ZIP export downloads with attachment '$attachmentFileName'."

$lines = @(
    "From: $fromAddress",
    "To: $ToAddress",
    "Subject: $Subject",
    "Message-ID: $messageId",
    'MIME-Version: 1.0',
    'Date: (UTC)',
    "Content-Type: multipart/mixed; boundary=`"$boundary`"",
    '',
    "--$boundary",
    'Content-Type: text/plain; charset=utf-8',
    'Content-Transfer-Encoding: 8bit',
    '',
    $text,
    '',
    "--$boundary",
    "Content-Type: $attachmentContentType; name=`"$attachmentFileName`"",
    "Content-Disposition: attachment; filename=`"$attachmentFileName`"",
    'Content-Transfer-Encoding: base64',
    ''
) + $attachmentBase64Lines + @(
    "--$boundary--",
    ''
)

$message = ($lines -join $crlf)

function Read-SmtpResponse {
    param(
        [Parameter(Mandatory=$true)]
        [System.IO.StreamReader]$Reader
    )

    $code = $null
    $sep = $null
    $lines = @()

    while ($true) {
        $line = $Reader.ReadLine()
        if ($null -eq $line) { throw 'SMTP connection closed by peer.' }

        $lines += $line

        if ($line -match '^(?<code>\d{3})(?<sep>[ -])(.*)$') {
            $code = [int]$Matches['code']
            $sep = $Matches['sep']
            if ($sep -eq ' ') {
                return [pscustomobject]@{ Code = $code; Lines = $lines }
            }
        }
        else {
            # If it's not parseable as a standard SMTP reply, keep reading.
            # (Some servers might include extra lines.)
        }
    }
}

function Send-SmtpLine {
    param(
        [Parameter(Mandatory=$true)]
        [System.IO.StreamWriter]$Writer,
        [Parameter(Mandatory=$true)]
        [string]$Line
    )
    $Writer.WriteLine($Line)
    $Writer.Flush()
}

$client = New-Object System.Net.Sockets.TcpClient
try {
    $client.Connect($hostName, $port)

    $stream = $client.GetStream()
    $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::ASCII)
    $writer = New-Object System.IO.StreamWriter($stream, [System.Text.Encoding]::ASCII)
    $writer.NewLine = $crlf
    $writer.AutoFlush = $true

    $greeting = Read-SmtpResponse -Reader $reader
    if ($greeting.Code -lt 200 -or $greeting.Code -ge 400) {
        throw "Unexpected SMTP greeting: $($greeting.Code) $($greeting.Lines -join ' | ')"
    }

    Send-SmtpLine -Writer $writer -Line "EHLO localhost"
    $ehlo = Read-SmtpResponse -Reader $reader
    if ($ehlo.Code -ne 250) {
        throw "EHLO failed: $($ehlo.Code) $($ehlo.Lines -join ' | ')"
    }

    Send-SmtpLine -Writer $writer -Line 'STARTTLS'
    $startTls = Read-SmtpResponse -Reader $reader
    if ($startTls.Code -ne 220) {
        throw "STARTTLS failed: $($startTls.Code) $($startTls.Lines -join ' | ')"
    }

    # Upgrade to TLS.
    $ssl = New-Object System.Net.Security.SslStream($stream, $false, { param($sender,$cert,$chain,$errors)
        if ($skipTlsVerify) { return $true }
        return $errors -eq [System.Net.Security.SslPolicyErrors]::None
    })

    $ssl.AuthenticateAsClient($hostName)

    $reader = New-Object System.IO.StreamReader($ssl, [System.Text.Encoding]::ASCII)
    $writer = New-Object System.IO.StreamWriter($ssl, [System.Text.Encoding]::ASCII)
    $writer.NewLine = $crlf
    $writer.AutoFlush = $true

    Send-SmtpLine -Writer $writer -Line "EHLO localhost"
    $ehlo2 = Read-SmtpResponse -Reader $reader
    if ($ehlo2.Code -ne 250) {
        throw "EHLO after STARTTLS failed: $($ehlo2.Code) $($ehlo2.Lines -join ' | ')"
    }

    # AUTH LOGIN
    Send-SmtpLine -Writer $writer -Line 'AUTH LOGIN'
    $auth1 = Read-SmtpResponse -Reader $reader
    if ($auth1.Code -ne 334) {
        throw "AUTH LOGIN rejected: $($auth1.Code) $($auth1.Lines -join ' | ')"
    }

    $userB64 = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($username))
    Send-SmtpLine -Writer $writer -Line $userB64
    $auth2 = Read-SmtpResponse -Reader $reader
    if ($auth2.Code -ne 334) {
        throw "AUTH LOGIN username rejected: $($auth2.Code) $($auth2.Lines -join ' | ')"
    }

    $passB64 = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($password))
    Send-SmtpLine -Writer $writer -Line $passB64
    $auth3 = Read-SmtpResponse -Reader $reader
    if ($auth3.Code -ne 235) {
        throw "AUTH LOGIN failed: $($auth3.Code) $($auth3.Lines -join ' | ')"
    }

    # Envelope.
    Send-SmtpLine -Writer $writer -Line "MAIL FROM:<$fromAddress>"
    $mailFrom = Read-SmtpResponse -Reader $reader
    if ($mailFrom.Code -ne 250) {
        throw "MAIL FROM failed: $($mailFrom.Code) $($mailFrom.Lines -join ' | ')"
    }

    Send-SmtpLine -Writer $writer -Line "RCPT TO:<$ToAddress>"
    $rcptTo = Read-SmtpResponse -Reader $reader
    if ($rcptTo.Code -ne 250 -and $rcptTo.Code -ne 251) {
        throw "RCPT TO failed: $($rcptTo.Code) $($rcptTo.Lines -join ' | ')"
    }

    Send-SmtpLine -Writer $writer -Line 'DATA'
    $dataResp = Read-SmtpResponse -Reader $reader
    if ($dataResp.Code -ne 354) {
        throw "DATA command rejected: $($dataResp.Code) $($dataResp.Lines -join ' | ')"
    }

    # Dot-stuff the body.
    $msgLines = $message -split "\r\n"
    $stuffedLines = foreach ($l in $msgLines) { if ($l.StartsWith('.')) { '.' + $l } else { $l } }
    $dataToSend = ($stuffedLines -join $crlf) + $crlf + '.' + $crlf

    $writer.Write($dataToSend)
    $writer.Flush()

    $afterData = Read-SmtpResponse -Reader $reader
    if ($afterData.Code -ne 250) {
        throw "Message send failed: $($afterData.Code) $($afterData.Lines -join ' | ')"
    }

    Send-SmtpLine -Writer $writer -Line 'QUIT'
    $quitResp = Read-SmtpResponse -Reader $reader

    Write-Host "OK: injected message to $ToAddress via ${hostName}:$port (attachment $attachmentFileName)." -ForegroundColor Green
}
finally {
    try {
        $client.Close()
    } catch {
        # ignore
    }
}
