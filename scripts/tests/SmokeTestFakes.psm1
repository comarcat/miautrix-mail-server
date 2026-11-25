if (-not ('MiautrixSmokeFakeHost' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

public sealed class MiautrixSmokeFakeHost : IDisposable
{
    private readonly TcpListener listener;
    private readonly X509Certificate2 certificate;
    private readonly string mode;
    private readonly bool advertiseAuthBeforeTls;
    private readonly bool advertiseRev2;
    private readonly int anonymousStatus;
    private readonly bool mtaStsMissing;
    private readonly bool mfaRequired;
    private readonly Thread thread;
    private volatile bool stopping;
    private volatile bool loggedOut;
    public int Port { get; private set; }
    public string LastAuthPlainBase64 { get; private set; }
    public string LastError { get; private set; }

    public MiautrixSmokeFakeHost(string mode, X509Certificate2 certificate, bool advertiseAuthBeforeTls, bool advertiseRev2, int anonymousStatus, bool mtaStsMissing, bool mfaRequired, int listenPort)
    {
        this.mode = mode;
        this.certificate = certificate;
        this.advertiseAuthBeforeTls = advertiseAuthBeforeTls;
        this.advertiseRev2 = advertiseRev2;
        this.anonymousStatus = anonymousStatus;
        this.mtaStsMissing = mtaStsMissing;
        this.mfaRequired = mfaRequired;
        listener = new TcpListener(IPAddress.Loopback, listenPort);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        thread = new Thread(AcceptLoop) { IsBackground = true, Name = "Miautrix smoke fake " + mode };
        thread.Start();
    }

    private void AcceptLoop()
    {
        while (!stopping)
        {
            try
            {
                var client = listener.AcceptTcpClient();
                var clientThread = new Thread(() => HandleClient(client)) { IsBackground = true };
                clientThread.Start();
            }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { if (stopping) return; }
            catch (Exception ex) { LastError = ex.GetType().Name + ": " + ex.Message; }
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            using (client)
                Handle(client);
        }
        catch (Exception ex)
        {
            if (!stopping) LastError = ex.GetType().Name + ": " + ex.Message;
        }
    }

    private static string ReadLine(Stream stream)
    {
        var bytes = new MemoryStream();
        while (true)
        {
            var value = stream.ReadByte();
            if (value < 0) return bytes.Length == 0 ? null : Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            if (value == 10) return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            bytes.WriteByte((byte)value);
            if (bytes.Length > 65536) throw new IOException("Line too long.");
        }
    }

    private static void SendLine(Stream stream, string line)
    {
        var bytes = Encoding.ASCII.GetBytes(line + "\r\n");
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private SslStream Upgrade(Stream stream)
    {
        var ssl = new SslStream(stream, true);
        ssl.AuthenticateAsServer(certificate, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);
        return ssl;
    }

    private void Handle(TcpClient client)
    {
        Stream stream = client.GetStream();
        if (mode == "SmtpImplicit" || mode == "Imap" || mode == "Https") stream = Upgrade(stream);
        if (mode.StartsWith("Smtp")) HandleSmtp(stream, mode == "SmtpStartTls");
        else if (mode == "Imap") HandleImap(stream);
        else if (mode == "Https") HandleHttp(stream);
    }

    private void HandleSmtp(Stream stream, bool startTls)
    {
        SendLine(stream, "220 fake.example.test ESMTP ready");
        var inData = false;
        var tlsActive = !startTls;
        while (true)
        {
            var line = ReadLine(stream);
            if (line == null) return;
            var upper = line.ToUpperInvariant();
            if (inData)
            {
                if (line == ".") { inData = false; SendLine(stream, "250 2.0.0 queued as TEST123"); }
                continue;
            }
            if (upper.StartsWith("EHLO ") || upper.StartsWith("HELO "))
            {
                SendLine(stream, "250-fake.example.test");
                if (startTls && !tlsActive)
                {
                    if (advertiseAuthBeforeTls) SendLine(stream, "250-AUTH PLAIN");
                    SendLine(stream, "250 STARTTLS");
                }
                else SendLine(stream, "250 AUTH PLAIN");
            }
            else if (upper == "STARTTLS")
            {
                SendLine(stream, "220 2.0.0 ready to start TLS");
                stream = Upgrade(stream);
                tlsActive = true;
            }
            else if (upper.StartsWith("AUTH PLAIN "))
            {
                LastAuthPlainBase64 = line.Substring("AUTH PLAIN ".Length).Trim();
                SendLine(stream, "235 2.7.0 authenticated");
            }
            else if (upper.StartsWith("AUTH PLAIN"))
            {
                SendLine(stream, "334 ");
                LastAuthPlainBase64 = ReadLine(stream);
                SendLine(stream, "235 2.7.0 authenticated");
            }
            else if (upper.StartsWith("MAIL FROM:")) SendLine(stream, "250 2.1.0 sender ok");
            else if (upper.StartsWith("RCPT TO:")) SendLine(stream, "250 2.1.5 recipient ok");
            else if (upper == "DATA") { inData = true; SendLine(stream, "354 end with dot"); }
            else if (upper == "QUIT") { SendLine(stream, "221 2.0.0 bye"); return; }
            else SendLine(stream, "250 ok");
        }
    }

    private void HandleImap(Stream stream)
    {
        SendLine(stream, "* OK fake IMAP ready");
        while (true)
        {
            var line = ReadLine(stream);
            if (line == null) return;
            var parts = line.Split(new[] { ' ' }, 3);
            if (parts.Length < 2) continue;
            var tag = parts[0];
            var command = parts[1].ToUpperInvariant();
            if (command == "CAPABILITY")
            {
                SendLine(stream, "* CAPABILITY IMAP4rev1" + (advertiseRev2 ? " IMAP4rev2" : "") + " AUTH=PLAIN AUTH=LOGIN");
                SendLine(stream, tag + " OK CAPABILITY completed");
            }
            else if (command == "AUTHENTICATE")
            {
                var data = parts.Length > 2 ? parts[2] : "";
                if (data.StartsWith("PLAIN ", StringComparison.OrdinalIgnoreCase)) LastAuthPlainBase64 = data.Substring(6).Trim();
                SendLine(stream, tag + " OK authenticated");
            }
            else if (command == "LOGIN") SendLine(stream, tag + " OK authenticated");
            else if (command == "EXAMINE")
            {
                SendLine(stream, "* 0 EXISTS");
                SendLine(stream, "* 0 RECENT");
                SendLine(stream, tag + " OK [READ-ONLY] EXAMINE completed");
            }
            else if (command == "LOGOUT")
            {
                SendLine(stream, "* BYE logging out");
                SendLine(stream, tag + " OK LOGOUT completed");
                return;
            }
            else SendLine(stream, tag + " OK completed");
        }
    }

    private void HandleHttp(Stream stream)
    {
        var requestLine = ReadLine(stream);
        if (requestLine == null) return;
        var headers = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string line;
        while (!String.IsNullOrEmpty(line = ReadLine(stream)))
        {
            var colon = line.IndexOf(':');
            if (colon > 0) headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
        }
        var requestParts = requestLine.Split(' ');
        var method = requestParts[0];
        var path = requestParts.Length > 1 ? requestParts[1] : "/";
        var length = headers.ContainsKey("Content-Length") ? Int32.Parse(headers["Content-Length"]) : 0;
        var bodyBytes = new byte[length];
        var read = 0;
        while (read < length) { var count = stream.Read(bodyBytes, read, length - read); if (count <= 0) break; read += count; }
        var requestBody = Encoding.UTF8.GetString(bodyBytes, 0, read);
        var host = headers.ContainsKey("Host") ? headers["Host"] : "";
        var auth = headers.ContainsKey("Authorization") ? headers["Authorization"] : "";
        var status = 200;
        var responseBody = "";
        var contentType = "text/plain; charset=utf-8";
        if (path == "/api/v1/auth/login" && method == "POST")
        {
            if (!headers.ContainsKey("Idempotency-Key")) status = 400;
            else if (mfaRequired) { status = 401; responseBody = "{\"message\":\"MFA required\"}"; contentType = "application/json"; }
            else { responseBody = "{\"token\":\"FAKE_ACCESS_TOKEN_01\",\"refreshToken\":\"FAKE_REFRESH_TOKEN_01\"}"; contentType = "application/json"; }
        }
        else if (path == "/api/v1/auth/logout" && method == "POST")
        {
            loggedOut = true;
            status = 204;
        }
        else if (path == "/api/v1/auth/me")
        {
            var bearerValid = auth == "Bearer FAKE_ACCESS_TOKEN_01" && !loggedOut;
            status = bearerValid ? 200 : anonymousStatus;
            if (status == 200) { responseBody = "{\"data\":{\"email\":\"smoke@example.test\"}}"; contentType = "application/json"; }
        }
        else if (host.StartsWith("autoconfig.", StringComparison.OrdinalIgnoreCase))
        {
            responseBody = "<?xml version=\"1.0\"?><clientConfig><emailProvider><incomingServer type=\"imap\"/><outgoingServer type=\"smtp\"/></emailProvider></clientConfig>";
            contentType = "application/xml";
        }
        else if (host.StartsWith("mta-sts.", StringComparison.OrdinalIgnoreCase))
        {
            if (mtaStsMissing) status = 404;
            else { responseBody = "version: STSv1\nmode: testing\nmax_age: 86400\nmx: mail.example.test\n"; contentType = "text/plain"; }
        }
        else if (path == "/" || path == "/admin/") responseBody = "<html>ok</html>";
        var reason = status == 200 ? "OK" : status == 204 ? "No Content" : status == 401 ? "Unauthorized" : status == 404 ? "Not Found" : "Bad Request";
        var payload = Encoding.UTF8.GetBytes(responseBody);
        var headersOut = "HTTP/1.1 " + status + " " + reason + "\r\n" +
            "Strict-Transport-Security: max-age=31536000\r\n" +
            "Content-Type: " + contentType + "\r\n" +
            "Content-Length: " + payload.Length + "\r\n" +
            "Connection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(headersOut);
        stream.Write(headerBytes, 0, headerBytes.Length);
        if (payload.Length > 0) stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }

    public void Dispose()
    {
        stopping = true;
        listener.Stop();
        thread.Join(2000);
    }
}
'@
}

function New-TestCertificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$DnsName,
        [Parameter(Mandatory)][datetime]$NotAfter,
        [string[]]$AdditionalDnsName = @()
    )

    $key = [System.Security.Cryptography.RSA]::Create(2048)
    $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        "CN=$DnsName",
        $key,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
    )
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true)
    )
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -bor [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign,
            $true
        )
    )
    $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
    $san.AddDnsName($DnsName)
    foreach ($additionalName in $AdditionalDnsName) {
        $san.AddDnsName($additionalName)
    }
    $request.CertificateExtensions.Add($san.Build())
    $eku = [System.Security.Cryptography.OidCollection]::new()
    $eku.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1')) | Out-Null
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $true)
    )
    $notAfterUtc = $NotAfter.ToUniversalTime()
    $notBefore = [datetime]::UtcNow.AddDays(-2)
    if ($notAfterUtc -le $notBefore) {
        $notBefore = $notAfterUtc.AddDays(-2)
    }
    $certificate = $request.CreateSelfSigned($notBefore, $notAfterUtc)
    $certificateCopy = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx)
    )
    $certificate.Dispose()
    $key.Dispose()
    return $certificateCopy
}

function Add-TestCertificateTrust {
    param([System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
    )
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $store.Add($Certificate)
    $store.Close()
}

function Remove-TestCertificateTrust {
    param([System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
    )
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $store.Remove($Certificate)
    $store.Close()
}

function Start-FakeSmtp {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('StartTls', 'Implicit')][string]$Mode,
        [bool]$AdvertiseAuthBeforeTls = $false,
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [ValidateRange(0,65535)][int]$Port = 0
    )
    $serverMode = if ($Mode -eq 'Implicit') { 'SmtpImplicit' } else { 'SmtpStartTls' }
    return [MiautrixSmokeFakeHost]::new($serverMode, $Certificate, $AdvertiseAuthBeforeTls, $true, 401, $false, $false, $Port)
}

function Start-FakeImap {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [string[]]$Capabilities = @('IMAP4rev1', 'IMAP4rev2'),
        [ValidateRange(0,65535)][int]$Port = 0
    )
    $rev2 = $Capabilities -contains 'IMAP4rev2'
    return [MiautrixSmokeFakeHost]::new('Imap', $Certificate, $false, $rev2, 401, $false, $false, $Port)
}

function Start-FakeHttps {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [ValidateSet(200, 401)][int]$AuthMeAnonymousStatus = 401,
        [switch]$MtaStsMissing,
        [switch]$MfaRequired,
        [ValidateRange(0,65535)][int]$Port = 0
    )
    return [MiautrixSmokeFakeHost]::new('Https', $Certificate, $false, $true, $AuthMeAnonymousStatus, [bool]$MtaStsMissing, [bool]$MfaRequired, $Port)
}

function Stop-FakeServer {
    param([IDisposable]$Server)
    if ($Server) { $Server.Dispose() }
}

Export-ModuleMember -Function New-TestCertificate, Add-TestCertificateTrust, Remove-TestCertificateTrust, Start-FakeSmtp, Start-FakeImap, Start-FakeHttps, Stop-FakeServer
