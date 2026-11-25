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
    [switch]$SkipCertificateCheck,
    [switch]$Json,
    [string]$SendTestTo
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { [Console]::Error.WriteLine('PowerShell 7 or later is required.'); exit 2 }
$ip = $null
if (-not [Net.IPAddress]::TryParse($ServerIp, [ref]$ip)) { [Console]::Error.WriteLine('-ServerIp must be an IPv4 or IPv6 address.'); exit 2 }
if ($MailHost -notmatch '^(?=.{1,253}$)(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)*[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$') { [Console]::Error.WriteLine('-MailHost must be a DNS hostname.'); exit 2 }
if ($Domain -and $Domain -notmatch '^(?=.{1,253}$)(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$') { [Console]::Error.WriteLine('-Domain must be a DNS domain.'); exit 2 }
if ($SmtpPorts.Count -eq 0 -or @($SmtpPorts | Where-Object { $_ -lt 1 -or $_ -gt 65535 } | Select-Object -First 1).Count) { [Console]::Error.WriteLine('-SmtpPorts must contain valid TCP ports.'); exit 2 }
$script:Results = [Collections.Generic.List[object]]::new()
function Add-Result([string]$Name,[string]$Status,[string]$Detail,[long]$Ms) {
    $script:Results.Add([pscustomobject]@{Check=$Name;Status=$Status;DurationMs=$Ms;Detail=($Detail -replace '(?i)(password|token|cookie|authorization)\s*[:=]?\s*\S+','$1=[REDACTED]')})
}
function Invoke-Check([string]$Name,[scriptblock]$Action) {
    $sw=[Diagnostics.Stopwatch]::StartNew(); try { $r=& $Action; Add-Result $Name $r.Status $r.Detail $sw.ElapsedMilliseconds } catch { Add-Result $Name 'FAIL' ($_.Exception.Message -replace '[\r\n]+',' ') $sw.ElapsedMilliseconds }
}
function Open-Tcp([int]$Port) {
    $client=[Net.Sockets.TcpClient]::new($ip.AddressFamily); $ar=$client.BeginConnect($ip,$Port,$null,$null)
    if(-not $ar.AsyncWaitHandle.WaitOne([timespan]::FromSeconds($TimeoutSeconds))){$client.Dispose();throw 'Connection timed out.'}; $client.EndConnect($ar); return $client
}
function Read-Line($Reader) { $x=$Reader.ReadLine(); if($null -eq $x){throw 'Remote endpoint closed the connection.'}; return $x }
function New-Reader($Stream) { $r=[IO.StreamReader]::new($Stream,[Text.Encoding]::ASCII,$false,1024,$true); $w=[IO.StreamWriter]::new($Stream,[Text.Encoding]::ASCII,1024,$true);$w.NewLine="`r`n";$w.AutoFlush=$true; return @($r,$w) }
function Read-SmtpReply($Reader) { $lines=@(); do { $line=Read-Line $Reader; $lines+=$line } while($line -match '^\d{3}-'); return $lines }
function Start-Tls($Client,[string]$Target) {
    $callback=[Net.Security.RemoteCertificateValidationCallback]{ param($sender,$cert,$chain,$errors) if($script:SkipCertificateCheck){return $true}; return $errors -eq [Net.Security.SslPolicyErrors]::None }
    $ssl=[Net.Security.SslStream]::new($Client.GetStream(),$false,$callback); $ssl.AuthenticateAsClient($Target); return $ssl
}
foreach($p in (@($HttpsPort,$ImapsPort)+$SmtpPorts | Select-Object -Unique)) { Invoke-Check "TCP $p" { $c=Open-Tcp $p;$c.Dispose();@{Status='PASS';Detail='Port accepts a TCP connection.'} } }
foreach($spec in @(@(465,'implicit SMTP TLS'),@($ImapsPort,'IMAPS'))) { $p=[int]$spec[0]; Invoke-Check "TLS $p" { $c=Open-Tcp $p;try{$s=Start-Tls $c $MailHost;try{$cert=[Security.Cryptography.X509Certificates.X509Certificate2]::new($s.RemoteCertificate);$days=[math]::Floor(($cert.NotAfter-[datetime]::Now).TotalDays);$status=if($days -lt 0){'FAIL'}elseif($days -lt 21){'WARN'}else{'PASS'};if($s.SslProtocol -notin @('Tls12','Tls13')){$status='FAIL'};@{Status=$status;Detail="TLS $($s.SslProtocol); certificate expires in $days days."}}finally{$s.Dispose()}}finally{$c.Dispose()} } }
foreach($p in @($SmtpPorts | Where-Object {$_ -in @(25,587)})) { Invoke-Check "SMTP $p greeting/EHLO/STARTTLS" { $c=Open-Tcp $p;try{$io=New-Reader $c.GetStream();$g=Read-SmtpReply $io[0];if($g[0] -notmatch '^220'){throw 'SMTP greeting was not 220.'};$io[1].WriteLine("EHLO $MailHost");$eh=Read-SmtpReply $io[0];$auth=($eh -join ' ') -match '(?i)AUTH';if($p -eq 587 -and $auth){throw 'AUTH was advertised before STARTTLS on 587.'};if($p -eq 25 -and $auth){throw 'AUTH was advertised without TLS on port 25.'};if(-not (($eh -join ' ') -match '(?i)STARTTLS')){throw 'STARTTLS was not advertised.'};$io[1].WriteLine('STARTTLS');if((Read-SmtpReply $io[0])[0] -notmatch '^220'){throw 'STARTTLS was rejected.'};$s=Start-Tls $c $MailHost;$tlsio=New-Reader $s;$tlsio[1].WriteLine("EHLO $MailHost");$eh2=Read-SmtpReply $tlsio[0];if($p -eq 587 -and (($eh2 -join ' ') -notmatch '(?i)AUTH')){throw 'AUTH was not advertised after STARTTLS.'};$tlsio[1].WriteLine('QUIT');@{Status='PASS';Detail='Greeting, EHLO and STARTTLS policy passed.'}}finally{$c.Dispose()} } }
Invoke-Check "IMAP $ImapsPort greeting/CAPABILITY" { $c=Open-Tcp $ImapsPort;try{$s=Start-Tls $c $MailHost;$io=New-Reader $s;$g=Read-Line $io[0];if($g -notmatch '^\* OK'){throw 'IMAP greeting was not OK.'};$io[1].WriteLine('a1 CAPABILITY');$lines=@();do{$l=Read-Line $io[0];$lines+=$l}while($l -notmatch '^a1 ');if(($lines -join ' ') -notmatch '(?i)IMAP4rev1|IMAP4rev2'){throw 'IMAP capability greeting missing IMAP version.'};@{Status='PASS';Detail="CAPABILITY received; IMAP4rev2 advertised: $([bool](($lines -join ' ') -match '(?i)IMAP4rev2'))."}}finally{$c.Dispose()} }
function Invoke-Http([string]$HostName,[string]$Path) { $handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false;$handler.ServerCertificateCustomValidationCallback={param($m,$c,$ch,$e) if($script:SkipCertificateCheck){$true}else{$e -eq [Net.Security.SslPolicyErrors]::None}};$handler.ConnectCallback={param($ctx,$ct)$tc=[Net.Sockets.TcpClient]::new($ip.AddressFamily);$tc.ConnectAsync($ip,$ctx.DnsEndPoint.Port,$ct).GetAwaiter().GetResult();$tc.GetStream()};$client=[Net.Http.HttpClient]::new($handler);try{$req=[Net.Http.HttpRequestMessage]::new('GET',[uri]::new("https://$HostName`:$HttpsPort$Path"));$req.Headers.Host=$HostName;$resp=$client.Send($req);return $resp}finally{$client.Dispose();$handler.Dispose()} }
foreach($route in @(@('/api/v1/auth/me','unauthenticated API auth'),@('/','webmail root'),@('/admin/','admin'))) { Invoke-Check "HTTPS $($route[1])" { $r=Invoke-Http $MailHost $route[0];$status=[int]$r.StatusCode;$hsts=$r.Headers.Contains('Strict-Transport-Security');if($route[0] -eq '/api/v1/auth/me'){if($status -eq 200){throw 'Unauthenticated /auth/me returned 200.'};if($status -ne 401){throw "Expected 401; received HTTP $status."}}elseif($status -lt 200 -or $status -ge 400){throw "HTTP $status."};if(-not $hsts){throw 'Strict-Transport-Security header is absent.'};@{Status='PASS';Detail="HTTP $status; HSTS present."} } }
if($Domain){Invoke-Check 'Autoconfig' { $r=Invoke-Http "autoconfig.$Domain" '/.well-known/autoconfig/mail/config-v1.1.xml';$body=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult();if([int]$r.StatusCode -ne 200 -or $body -notmatch '(?i)imap' -or $body -notmatch '(?i)smtp'){throw 'Expected XML naming IMAP and SMTP.'};@{Status='PASS';Detail='XML names IMAP and SMTP.'} };Invoke-Check 'MTA-STS' { $r=Invoke-Http "mta-sts.$Domain" '/.well-known/mta-sts.txt';$body=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult();if([int]$r.StatusCode -eq 404){@{Status='INFO';Detail='Policy is absent (mode none or not configured).'}}elseif([int]$r.StatusCode -eq 200 -and $body -match '(?im)^version:\s*STSv1\s*$' -and $body -match '(?im)^mode:\s*(enforce|testing|none)\s*$' -and $body -match '(?im)^max_age:\s*\d+\s*$'){@{Status='PASS';Detail='MTA-STS policy fields are valid.'}}else{throw 'MTA-STS response is not a valid policy.'} } }
if($Credential){ Add-Result 'Authenticated API login/logout' 'SKIP' 'Credential flow requires server-specific API login contract.' 0;Add-Result 'Authenticated IMAP EXAMINE' 'SKIP' 'Credential flow not configured.' 0;Add-Result 'Authenticated SMTP AUTH' 'SKIP' 'Credential flow not configured.' 0 }
if($SendTestTo){Add-Result 'Send test message' 'SKIP' 'SMTP message submission is not implemented in this read-only probe.' 0}
if($SkipCertificateCheck){Add-Result 'Certificate validation' 'WARN' 'Certificate validation was bypassed for this run.' 0}
if($Json){$Results|ConvertTo-Json -Depth 4}else{$Results|Format-Table -AutoSize | Out-String | Write-Host}
$fails=@($Results|Where-Object Status -eq 'FAIL').Count
if(-not $Json){Write-Host "Summary: $(@($Results|Where-Object Status -eq 'PASS').Count) PASS, $(@($Results|Where-Object Status -eq 'WARN').Count) WARN, $fails FAIL, $(@($Results|Where-Object Status -eq 'SKIP').Count) SKIP, $(@($Results|Where-Object Status -eq 'INFO').Count) INFO."}
if($Credential){$Credential.Password.Dispose()}
exit $(if($fails){1}else{0})
