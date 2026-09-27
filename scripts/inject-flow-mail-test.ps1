# Inject a test email with a rich HTML body and (up to) two file attachments.
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
#   $env:TEST_SUBJECT="[miautrix] Rich HTML body + 2 attachments" # optional
#   .\scripts\inject-flow-mail-test.ps1 -AttachmentPath .\empty.txt -AttachmentPath2 .\report.csv
#
# Optional:
#   $env:SMTP_SKIP_TLS_VERIFY="true"  # only for self-signed test certs
#
# Notes:
#   - When -AttachmentPath2 is omitted a small generated CSV is attached, so the
#     two-attachment layout is always exercised.
#   - The message body is multipart/alternative: a plain-text fallback plus an
#     HTML part with colours, headings and a table, to exercise the reading pane
#     and the HTML sanitiser.

[CmdletBinding()]
param(
    [string]$ToAddress = 'owner@miautrix.tech',
    [string]$Subject = $env:TEST_SUBJECT,
    [string]$AttachmentPath = $env:TEST_ATTACHMENT_PATH,
    [string]$AttachmentPath2 = $env:TEST_ATTACHMENT_PATH_2
)

$ErrorActionPreference = 'Stop'

function Get-EnvRequired([string]$Name) {
    $v = (Get-Item -Path "Env:$Name" -ErrorAction SilentlyContinue).Value
    if ([string]::IsNullOrWhiteSpace($v)) { throw "$Name is required." }
    return $v
}

function Get-ContentTypeForPath([string]$Path) {
    switch ([System.IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        '.txt'  { 'text/plain' }
        '.csv'  { 'text/csv' }
        '.json' { 'application/json' }
        '.xml'  { 'application/xml' }
        '.html' { 'text/html' }
        '.md'   { 'text/markdown' }
        '.pdf'  { 'application/pdf' }
        '.zip'  { 'application/zip' }
        '.png'  { 'image/png' }
        '.jpg'  { 'image/jpeg' }
        '.jpeg' { 'image/jpeg' }
        '.gif'  { 'image/gif' }
        default { 'application/octet-stream' }
    }
}

function Get-Base64Lines([byte[]]$Bytes) {
    if ($Bytes.Length -eq 0) { return @('') }
    $b64 = [Convert]::ToBase64String($Bytes)
    $out = @()
    for ($i = 0; $i -lt $b64.Length; $i += 76) {
        $out += $b64.Substring($i, [Math]::Min(76, $b64.Length - $i))
    }
    return $out
}

$hostName = if ([string]::IsNullOrWhiteSpace($env:SMTP_HOST)) { 'mail.miautrix.tech' } else { $env:SMTP_HOST }
$port = if ([string]::IsNullOrWhiteSpace($env:SMTP_PORT)) { 587 } else { [int]$env:SMTP_PORT }
$username = Get-EnvRequired 'SMTP_USERNAME'
$password = Get-EnvRequired 'SMTP_PASSWORD'
$fromAddress = if ([string]::IsNullOrWhiteSpace($env:SMTP_FROM)) { $username } else { $env:SMTP_FROM }

if ([string]::IsNullOrWhiteSpace($Subject)) { $Subject = '[miautrix] Rich HTML body + 2 attachments' }

# --- Attachments -------------------------------------------------------------

if ([string]::IsNullOrWhiteSpace($AttachmentPath)) {
    $AttachmentPath = Join-Path (Get-Item $PSScriptRoot).Parent.FullName 'empty.txt'
}

$generatedSecondAttachment = $false
if ([string]::IsNullOrWhiteSpace($AttachmentPath2)) {
    $AttachmentPath2 = Join-Path ([System.IO.Path]::GetTempPath()) 'miautrix-flow-test-second.csv'
    @(
        'region,delivered,deferred,bounced',
        'eu-west,1841,12,3',
        'us-east,2210,7,1',
        'ap-south,573,4,0'
    ) | Set-Content -Path $AttachmentPath2 -Encoding UTF8
    $generatedSecondAttachment = $true
}

$attachmentSpecs = @()
foreach ($path in @($AttachmentPath, $AttachmentPath2)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Attachment not found: $path"
    }
    $item = Get-Item -LiteralPath $path
    $attachmentSpecs += [pscustomobject]@{
        FileName    = $item.Name
        ContentType = Get-ContentTypeForPath $item.FullName
        Base64Lines = Get-Base64Lines ([System.IO.File]::ReadAllBytes($item.FullName))
    }
}

$skipTlsVerify = $false
if (-not [string]::IsNullOrWhiteSpace($env:SMTP_SKIP_TLS_VERIFY)) {
    $skipTlsVerify = $env:SMTP_SKIP_TLS_VERIFY.ToLowerInvariant() -eq 'true'
}

$crlf = "`r`n"
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
$outerBoundary = '----=_miautrix_flow_test_mixed_' + $stamp
$altBoundary = '----=_miautrix_flow_test_alt_' + $stamp
$msgGuid = [Guid]::NewGuid().ToString()
$messageId = "<$msgGuid@$hostName>"
$rfc2822Date = (Get-Date).ToUniversalTime().ToString('ddd, dd MMM yyyy HH:mm:ss') + ' +0000'

# --- Bodies ------------------------------------------------------------------

$attachmentNames = ($attachmentSpecs | ForEach-Object { $_.FileName }) -join ', '

$text = @"
Hello,

This is a rich-format test message used to exercise the webmail reading pane.
It carries a multipart/alternative body (plain text + HTML with colours and a
table) and two attachments: $attachmentNames.

Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor
incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis
nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.

-- Miautrix mail flow test
"@

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>Miautrix rich email test</title>
</head>
<body style="margin:0;padding:24px;background:#f5f6fa;font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#1f2430;">
  <div style="max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #dfe3ec;border-radius:10px;overflow:hidden;">

    <div style="background:linear-gradient(135deg,#4b3bd8 0%,#7a5af8 100%);padding:28px 32px;">
      <h1 style="margin:0;font-size:24px;line-height:1.3;color:#ffffff;">Miautrix rich email test</h1>
      <p style="margin:8px 0 0;font-size:14px;color:#e6e1ff;">
        HTML body &middot; colours &middot; table &middot; two attachments
      </p>
    </div>

    <div style="padding:28px 32px;">
      <p style="margin:0 0 16px;font-size:15px;line-height:1.7;">
        <strong style="color:#4b3bd8;">Lorem ipsum dolor sit amet</strong>, consectetur adipiscing elit,
        sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam,
        quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.
      </p>

      <p style="margin:0 0 16px;font-size:15px;line-height:1.7;color:#5a6273;">
        Duis aute irure <em style="color:#d64545;">dolor in reprehenderit</em> in voluptate velit esse
        cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in
        culpa qui officia deserunt mollit anim id est laborum.
      </p>

      <h2 style="margin:24px 0 12px;font-size:18px;color:#1f2430;">Delivery summary</h2>

      <table style="width:100%;border-collapse:collapse;font-size:14px;">
        <thead>
          <tr style="background:#eef0f9;">
            <th style="text-align:left;padding:10px 12px;border:1px solid #dfe3ec;color:#3a4257;">Region</th>
            <th style="text-align:right;padding:10px 12px;border:1px solid #dfe3ec;color:#3a4257;">Delivered</th>
            <th style="text-align:right;padding:10px 12px;border:1px solid #dfe3ec;color:#3a4257;">Deferred</th>
            <th style="text-align:right;padding:10px 12px;border:1px solid #dfe3ec;color:#3a4257;">Bounced</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;">eu-west</td>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;text-align:right;color:#1f8a4c;font-weight:600;">1,841</td>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;text-align:right;color:#b8860b;">12</td>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;text-align:right;color:#d64545;">3</td>
          </tr>
          <tr style="background:#fafbff;">
            <td style="padding:10px 12px;border:1px solid #dfe3ec;">us-east</td>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;text-align:right;color:#1f8a4c;font-weight:600;">2,210</td>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;text-align:right;color:#b8860b;">7</td>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;text-align:right;color:#d64545;">1</td>
          </tr>
          <tr>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;">ap-south</td>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;text-align:right;color:#1f8a4c;font-weight:600;">573</td>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;text-align:right;color:#b8860b;">4</td>
            <td style="padding:10px 12px;border:1px solid #dfe3ec;text-align:right;color:#d64545;">0</td>
          </tr>
        </tbody>
      </table>

      <ul style="margin:20px 0 0;padding-left:20px;font-size:15px;line-height:1.8;">
        <li>Coloured status text (green / amber / red)</li>
        <li>A table with borders and zebra striping</li>
        <li>Two attachments: <code style="background:#f0f1f7;padding:2px 6px;border-radius:4px;">$attachmentNames</code></li>
      </ul>

      <blockquote style="margin:24px 0 0;padding:12px 18px;border-left:4px solid #7a5af8;background:#f7f5ff;color:#4a4363;font-style:italic;">
        &ldquo;Neque porro quisquam est qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit.&rdquo;
      </blockquote>
    </div>

    <div style="padding:18px 32px;background:#f5f6fa;border-top:1px solid #dfe3ec;font-size:12px;color:#8b93a7;">
      Sent by the Miautrix mail flow test script.
    </div>
  </div>
</body>
</html>
"@

# --- Assemble the MIME message ----------------------------------------------

$lines = @(
    "From: $fromAddress",
    "To: $ToAddress",
    "Subject: $Subject",
    "Message-ID: $messageId",
    'MIME-Version: 1.0',
    "Date: $rfc2822Date",
    "Content-Type: multipart/mixed; boundary=`"$outerBoundary`"",
    '',
    "This is a multi-part message in MIME format.",
    '',
    "--$outerBoundary",
    "Content-Type: multipart/alternative; boundary=`"$altBoundary`"",
    '',
    "--$altBoundary",
    'Content-Type: text/plain; charset=utf-8',
    'Content-Transfer-Encoding: 8bit',
    '',
    $text,
    '',
    "--$altBoundary",
    'Content-Type: text/html; charset=utf-8',
    'Content-Transfer-Encoding: 8bit',
    '',
    $html,
    '',
    "--$altBoundary--",
    ''
)

foreach ($att in $attachmentSpecs) {
    $lines += @(
        "--$outerBoundary",
        "Content-Type: $($att.ContentType); name=`"$($att.FileName)`"",
        "Content-Disposition: attachment; filename=`"$($att.FileName)`"",
        'Content-Transfer-Encoding: base64',
        ''
    )
    $lines += $att.Base64Lines
    $lines += ''
}

$lines += @(
    "--$outerBoundary--",
    ''
)

$message = ($lines -join $crlf)

# --- Minimal SMTP client -----------------------------------------------------

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

    Write-Host "OK: injected message to $ToAddress via ${hostName}:$port" -ForegroundColor Green
    Write-Host ("  body:  multipart/alternative (text/plain + text/html with table and colours)") -ForegroundColor DarkGray
    foreach ($att in $attachmentSpecs) {
        Write-Host ("  attach: {0} ({1})" -f $att.FileName, $att.ContentType) -ForegroundColor DarkGray
    }
}
finally {
    try {
        $client.Close()
    } catch {
        # ignore
    }
    if ($generatedSecondAttachment) {
        Remove-Item -LiteralPath $AttachmentPath2 -ErrorAction SilentlyContinue
    }
}
