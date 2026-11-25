using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Miautrix.Mail.Protocols.Smtp;

namespace Miautrix.Mail.Worker;

/// <summary>
/// Modes for the SMTP listener — controls TLS and handler behaviour.
/// </summary>
public enum SmtpListenerMode
{
    /// <summary>Port 587 — plain TCP, STARTTLS upgrade required for AUTH.</summary>
    SubmissionStartTls,
    /// <summary>Port 465 — implicit TLS immediately on connect.</summary>
    SubmissionImplicitTls,
    /// <summary>Port 25 — inbound MX traffic, STARTTLS opportunistic, no AUTH required.</summary>
    Inbound
}

/// <summary>
/// BackgroundService that binds a TCP port and runs the SMTP wire protocol,
/// dispatching validated messages to <see cref="ISmtpSubmissionHandler"/> or
/// <see cref="ISmtpInboundHandler"/> depending on <see cref="SmtpListenerMode"/>.
/// </summary>
public class SmtpListenerService : BackgroundService
{
    private const int MaximumCommandOctets = 512;
    private const int MaximumDataLineOctets = 1000;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmtpListenerService> _logger;
    private readonly int _port;
    private readonly SmtpListenerMode _mode;
    private readonly X509Certificate2? _tlsCert;
    private readonly string _hostname;
    private readonly SmtpListenerOptions _options;

    public SmtpListenerService(
        IServiceScopeFactory scopeFactory,
        ILogger<SmtpListenerService> logger,
        int port,
        SmtpListenerMode mode,
        X509Certificate2? tlsCert,
        string hostname,
        SmtpListenerOptions? options = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _port = port;
        _mode = mode;
        _tlsCert = tlsCert;
        _hostname = hostname;
        _options = options ?? SmtpListenerOptions.Default;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Submission ports require a TLS cert; skip binding with a warning so other
        // listeners can still start.
        if (_mode != SmtpListenerMode.Inbound && _tlsCert is null)
        {
            _logger.LogWarning(
                "SMTP listener for port {Port} ({Mode}) requires a TLS certificate. " +
                "Set MIAUTRIX_TLS_CERT_PATH and MIAUTRIX_TLS_KEY_PATH. Skipping this listener.",
                _port, _mode);
            return;
        }

        var listener = new TcpListener(IPAddress.Any, _port);
        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            // Ports below 1024 need CAP_NET_BIND_SERVICE; an occupied port needs the other
            // process stopped. Either way, log it and let the sibling listeners keep running
            // rather than taking the whole host down.
            _logger.LogError(
                ex,
                "Cannot bind SMTP port {Port} ({Mode}): {Reason}. " +
                "For EACCES, the unit needs AmbientCapabilities=CAP_NET_BIND_SERVICE. " +
                "For EADDRINUSE, another process holds the port (check: ss -lntup | grep :{Port}). " +
                "Other listeners continue without this one.",
                _port, _mode, ex.SocketErrorCode, _port);
            return;
        }

        _logger.LogInformation("SMTP listener started on port {Port} ({Mode})", _port, _mode);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting SMTP connection on port {Port}", _port);
                    continue;
                }

                // Fire-and-forget; each connection is independent.
                _ = Task.Run(() => HandleConnectionAsync(client, stoppingToken), stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
            _logger.LogInformation("SMTP listener on port {Port} stopped", _port);
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            _logger.LogDebug("SMTP connection from {Remote} on port {Port}", remoteEndpoint, _port);
            try
            {
                await RunSmtpSessionAsync(client, remoteEndpoint, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "SMTP session error from {Remote}", remoteEndpoint);
            }
        }
    }

    private async Task RunSmtpSessionAsync(TcpClient client, string remoteEndpoint, CancellationToken ct)
    {
        Stream stream = client.GetStream();
        bool isTlsActive = false;
        StreamWriter writer;

        if (_mode == SmtpListenerMode.SubmissionImplicitTls)
        {
            var ssl = new SslStream(stream, false);
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _tlsCert,
                ClientCertificateRequired = false,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
            }, ct);
            stream = ssl;
            isTlsActive = true;
        }
        var lineReader = new SmtpLineReader(stream);

        writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };

        // Scoped handlers for this connection. Each SMTP session gets its own DbContext.
        using var scope = _scopeFactory.CreateScope();
        var submissionHandler = scope.ServiceProvider.GetRequiredService<ISmtpSubmissionHandler>();
        var inboundHandler = scope.ServiceProvider.GetRequiredService<ISmtpInboundHandler>();

        // State
        string? authenticatedUsername = null;
        Guid? tenantId = null;
        Guid? userId = null;
        var greeted = false;
        var extendedSmtp = false;
        string? mailFrom = null;
        var recipients = new List<(string Address, Guid TenantId)>();

        await writer.WriteLineAsync($"220 2.0.0 {_hostname} ESMTP Miautrix");

        while (!ct.IsCancellationRequested)
        {
            var commandRead = await lineReader.ReadLineAsync(MaximumCommandOctets, _options.IdleTimeout, ct);
            if (commandRead.TimedOut)
            {
                await writer.WriteLineAsync("421 4.4.2 Idle timeout");
                return;
            }
            if (commandRead.EndOfStream) return;
            if (commandRead.TooLong)
            {
                await writer.WriteLineAsync("500 5.5.2 Command line too long");
                continue;
            }
            var line = commandRead.Line!;
            var upper = line.TrimStart().ToUpperInvariant();
            var commandEnd = line.IndexOf(' ');
            var command = (commandEnd < 0 ? line : line[..commandEnd]).ToUpperInvariant();

            // ── EHLO/HELO ──────────────────────────────────────────────────
            if (command is "EHLO" or "HELO")
            {
                if (commandEnd < 0 || string.IsNullOrWhiteSpace(line[(commandEnd + 1)..]))
                {
                    await writer.WriteLineAsync("501 5.5.2 Greeting requires a domain argument");
                    continue;
                }
                mailFrom = null;
                recipients.Clear();
                greeted = true;
                extendedSmtp = command == "EHLO";
                if (!extendedSmtp)
                {
                    await writer.WriteLineAsync($"250 2.0.0 {_hostname}");
                    continue;
                }
                await writer.WriteLineAsync($"250-{_hostname}");
                if (!isTlsActive && _tlsCert is not null)
                {
                    await writer.WriteLineAsync("250-STARTTLS");
                }
                if (_mode != SmtpListenerMode.Inbound && (isTlsActive || _mode == SmtpListenerMode.SubmissionImplicitTls))
                {
                    await writer.WriteLineAsync("250-AUTH LOGIN PLAIN");
                }
                await writer.WriteLineAsync($"250-SIZE {_options.MaximumMessageSize}");
                await writer.WriteLineAsync("250 2.0.0 OK");
                continue;
            }

            // ── STARTTLS ───────────────────────────────────────────────────
            if (command == "STARTTLS")
            {
                if (!greeted || !extendedSmtp || mailFrom is not null || recipients.Count != 0)
                {
                    await writer.WriteLineAsync("503 5.5.1 EHLO required outside a mail transaction");
                    continue;
                }
                if (isTlsActive)
                {
                    await writer.WriteLineAsync("503 5.5.1 TLS already active");
                    continue;
                }
                if (_tlsCert is null)
                {
                    await writer.WriteLineAsync("454 4.7.0 TLS not available");
                    continue;
                }

                await writer.WriteLineAsync("220 2.0.0 Go ahead");

                var ssl = new SslStream(stream, false);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = _tlsCert,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                }, ct);
                stream = ssl;
                lineReader = new SmtpLineReader(stream);
                isTlsActive = true;
                greeted = false;
                extendedSmtp = false;
                authenticatedUsername = null;
                tenantId = null;
                userId = null;
                mailFrom = null;
                recipients.Clear();

                await writer.FlushAsync(ct);
                writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
                continue;
            }

            // ── AUTH ───────────────────────────────────────────────────────
            if (command == "AUTH")
            {
                if (!isTlsActive)
                {
                    await writer.WriteLineAsync("538 5.7.11 Encryption required for requested authentication mechanism");
                    continue;
                }
                if (_mode == SmtpListenerMode.Inbound)
                {
                    await writer.WriteLineAsync("502 5.5.1 AUTH is unavailable on this listener");
                    continue;
                }
                if (!greeted || !extendedSmtp)
                {
                    await writer.WriteLineAsync("503 5.5.1 EHLO required before AUTH");
                    continue;
                }
                if (mailFrom is not null || recipients.Count != 0 || authenticatedUsername is not null)
                {
                    await writer.WriteLineAsync("503 5.5.1 AUTH not permitted during or after a mail transaction");
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length is < 2 or > 3)
                {
                    await writer.WriteLineAsync("501 5.5.4 Invalid AUTH syntax");
                    continue;
                }

                var mechanism = parts[1].ToUpperInvariant();
                string username;
                string password;
                if (mechanism == "LOGIN")
                {
                    string userB64;
                    if (parts.Length == 3)
                    {
                        userB64 = parts[2];
                        if (userB64 == "*") { await writer.WriteLineAsync("501 5.7.0 Authentication cancelled"); continue; }
                    }
                    else
                    {
                        await writer.WriteLineAsync("334 VXNlcm5hbWU6");
                        var userRead = await lineReader.ReadLineAsync(MaximumCommandOctets, _options.IdleTimeout, ct);
                        if (userRead.EndOfStream) return;
                        if (userRead.TimedOut) { await writer.WriteLineAsync("421 4.4.2 Authentication timeout"); return; }
                        if (userRead.TooLong) { await writer.WriteLineAsync("501 5.5.4 Invalid base64 encoding"); continue; }
                        userB64 = userRead.Line!;
                        if (userB64 == "*") { await writer.WriteLineAsync("501 5.7.0 Authentication cancelled"); continue; }
                    }

                    if (!TryDecodeSaslBase64(userB64, out var userBytes) || !TryDecodeUtf8(userBytes, out username))
                    {
                        await writer.WriteLineAsync("501 5.5.4 Invalid base64 encoding");
                        continue;
                    }
                    await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
                    var passRead = await lineReader.ReadLineAsync(MaximumCommandOctets, _options.IdleTimeout, ct);
                    if (passRead.EndOfStream) return;
                    if (passRead.TimedOut) { await writer.WriteLineAsync("421 4.4.2 Authentication timeout"); return; }
                    if (passRead.TooLong) { await writer.WriteLineAsync("501 5.5.4 Invalid base64 encoding"); continue; }
                    if (passRead.Line == "*") { await writer.WriteLineAsync("501 5.7.0 Authentication cancelled"); continue; }
                    if (!TryDecodeSaslBase64(passRead.Line!, out var passBytes) || !TryDecodeUtf8(passBytes, out password))
                    {
                        await writer.WriteLineAsync("501 5.5.4 Invalid base64 encoding");
                        continue;
                    }
                }
                else if (mechanism == "PLAIN")
                {
                    string response64;
                    if (parts.Length == 3)
                    {
                        response64 = parts[2];
                        if (response64 == "*") { await writer.WriteLineAsync("501 5.7.0 Authentication cancelled"); continue; }
                    }
                    else
                    {
                        await writer.WriteLineAsync("334");
                        var responseRead = await lineReader.ReadLineAsync(MaximumCommandOctets, _options.IdleTimeout, ct);
                        if (responseRead.EndOfStream) return;
                        if (responseRead.TimedOut) { await writer.WriteLineAsync("421 4.4.2 Authentication timeout"); return; }
                        if (responseRead.TooLong) { await writer.WriteLineAsync("501 5.5.4 Invalid base64 encoding"); continue; }
                        response64 = responseRead.Line!;
                        if (response64 == "*") { await writer.WriteLineAsync("501 5.7.0 Authentication cancelled"); continue; }
                    }

                    if (!TryDecodeSaslBase64(response64, out var plainBytes) || !TryDecodeUtf8(plainBytes, out var plain))
                    {
                        await writer.WriteLineAsync("501 5.5.4 Invalid base64 encoding");
                        continue;
                    }
                    var fields = plain.Split('\0');
                    if (fields.Length != 3 || string.IsNullOrEmpty(fields[1]) || (fields[0].Length > 0 && !fields[0].Equals(fields[1], StringComparison.OrdinalIgnoreCase)))
                    {
                        await writer.WriteLineAsync("501 5.5.4 Invalid SASL PLAIN response");
                        continue;
                    }
                    username = fields[1];
                    password = fields[2];
                }
                else
                {
                    await writer.WriteLineAsync("504 5.5.4 Unsupported authentication mechanism");
                    continue;
                }

                var response = submissionHandler.HandleAuth(username, password, isTlsActive, out var authTenant, out var authUser);
                if (response.IsSuccess)
                {
                    authenticatedUsername = username;
                    tenantId = authTenant;
                    userId = authUser;
                    await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                }
                else await writer.WriteLineAsync(response.ToSmtpString());
                continue;
            }

            // ── MAIL FROM ──────────────────────────────────────────────────
            if (command == "MAIL")
            {
                if (!greeted || mailFrom is not null || recipients.Count != 0)
                {
                    await writer.WriteLineAsync("503 5.5.1 Bad sequence of commands");
                    continue;
                }
                if (!upper.StartsWith("MAIL FROM:", StringComparison.Ordinal))
                {
                    await writer.WriteLineAsync("501 5.5.2 Syntax: MAIL FROM:<address>");
                    continue;
                }
                mailFrom = ExtractAngleAddress(line);
                if (ExtractMailSize(line) is long declaredSize && declaredSize > _options.MaximumMessageSize)
                {
                    mailFrom = null;
                    await writer.WriteLineAsync("552 5.3.4 Message size exceeds fixed maximum message size");
                    continue;
                }
                if (_mode != SmtpListenerMode.Inbound)
                {
                    if (tenantId is null || userId is null || !await submissionHandler.IsEnvelopeSenderAllowedAsync(tenantId.Value, userId.Value, mailFrom, ct))
                    {
                        mailFrom = null;
                        await writer.WriteLineAsync(SmtpResponse.SenderRejected.ToSmtpString());
                        continue;
                    }
                }
                await writer.WriteLineAsync("250 2.1.0 OK");
                continue;
            }

            // ── RCPT TO ────────────────────────────────────────────────────
            if (command == "RCPT")
            {
                if (!greeted || mailFrom is null)
                {
                    await writer.WriteLineAsync("503 5.5.1 Need MAIL command first");
                    continue;
                }
                if (!upper.StartsWith("RCPT TO:", StringComparison.Ordinal))
                {
                    await writer.WriteLineAsync("501 5.5.2 Syntax: RCPT TO:<address>");
                    continue;
                }
                if (recipients.Count >= _options.MaximumRecipients)
                {
                    await writer.WriteLineAsync("452 4.5.3 Too many recipients");
                    continue;
                }

                var recipient = ExtractAngleAddress(line);

                if (_mode == SmtpListenerMode.Inbound)
                {
                    var check = inboundHandler.HandleRcptTo(recipient, out var rcptTenant);
                    if (!check.IsSuccess)
                    {
                        await writer.WriteLineAsync(check.ToSmtpString());
                        continue;
                    }
                    tenantId = rcptTenant;
                }
                else
                {
                    if (tenantId is null)
                    {
                        await writer.WriteLineAsync("530 5.7.0 Authentication required");
                        continue;
                    }
                }

                recipients.Add((recipient, tenantId!.Value));
                await writer.WriteLineAsync("250 2.1.5 OK");
                continue;
            }

            // ── DATA ───────────────────────────────────────────────────────
            if (command == "DATA")
            {
                if (!greeted || mailFrom is null || recipients.Count == 0)
                {
                    await writer.WriteLineAsync("503 5.5.1 Need MAIL and RCPT first");
                    continue;
                }

                await writer.WriteLineAsync("354 3.0.0 Start mail input; end with <CRLF>.<CRLF>");

                var body = new StringBuilder();
                var dataDeadline = DateTimeOffset.UtcNow + _options.DataTimeout;
                var messageOctets = 0;
                var rejectData = false;
                var dataComplete = false;
                while (!ct.IsCancellationRequested)
                {
                    var remainingDataTime = dataDeadline - DateTimeOffset.UtcNow;
                    var dataRead = remainingDataTime <= TimeSpan.Zero
                        ? new SmtpLineRead(null, false, false, true)
                        : await lineReader.ReadLineAsync(MaximumDataLineOctets, remainingDataTime, ct);
                    if (dataRead.TimedOut)
                    {
                        await writer.WriteLineAsync("421 4.4.2 DATA timeout");
                        return;
                    }
                    if (dataRead.EndOfStream) return;
                    if (dataRead.Line == ".")
                    {
                        dataComplete = true;
                        break;
                    }
                    if (dataRead.TooLong)
                    {
                        rejectData = true;
                        continue;
                    }

                    // Un-dot-stuff before measuring/storing the RFC 5321 message content.
                    var dataLine = dataRead.Line!;
                    var messageLine = dataLine.StartsWith("..", StringComparison.Ordinal) ? dataLine[1..] : dataLine;
                    var lineOctets = Encoding.ASCII.GetByteCount(messageLine) + 2;
                    if (messageOctets > _options.MaximumMessageSize - lineOctets)
                    {
                        rejectData = true;
                        continue;
                    }
                    messageOctets += lineOctets;
                    if (!rejectData) body.Append(messageLine).Append("\r\n");
                }

                if (!dataComplete) return;
                if (rejectData)
                {
                    await writer.WriteLineAsync("552 5.3.4 Message size exceeds fixed maximum message size");
                    mailFrom = null;
                    recipients.Clear();
                    continue;
                }

                var rawMessage = body.ToString();
                if (_mode != SmtpListenerMode.Inbound)
                    rawMessage = SmtpSubmissionHandler.EnsureSubmissionHeaders(rawMessage);

                SmtpResponse response = SmtpResponse.Queued;
                foreach (var recipient in recipients)
                {
                    if (_mode == SmtpListenerMode.Inbound)
                    {
                        var (resp, _) = await inboundHandler.HandleDataAsync(
                            recipient.TenantId, mailFrom!, recipient.Address, rawMessage, cancellationToken: ct);
                        if (!resp.IsSuccess) { response = resp; break; }
                    }
                    else
                    {
                        var (resp, _) = await submissionHandler.HandleSubmissionAsync(
                            isAuthenticated: authenticatedUsername is not null,
                            isTlsEncrypted: isTlsActive,
                            tenantId: tenantId,
                            userId: userId,
                            sender: mailFrom!,
                            recipient: recipient.Address,
                            rawMessage: rawMessage,
                            cancellationToken: ct);
                        if (!resp.IsSuccess) { response = resp; break; }
                    }
                }
                await writer.WriteLineAsync(response.ToSmtpString());

                // Reset envelope state for potential next message in same session.
                mailFrom = null;
                recipients.Clear();
                continue;
            }

            // ── NOOP ───────────────────────────────────────────────────────
            if (command == "NOOP")
            {
                await writer.WriteLineAsync("250 2.0.0 OK");
                continue;
            }

            if (command is "VRFY" or "EXPN")
            {
                await writer.WriteLineAsync("252 2.1.5 Cannot verify user; will accept message and attempt delivery");
                continue;
            }

            if (command == "HELP")
            {
                await writer.WriteLineAsync("214 2.0.0 Supported commands: EHLO HELO MAIL RCPT DATA RSET NOOP QUIT VRFY EXPN HELP");
                continue;
            }

            // ── RSET ───────────────────────────────────────────────────────
            if (command == "RSET")
            {
                mailFrom = null;
                recipients.Clear();
                await writer.WriteLineAsync("250 2.0.0 OK");
                continue;
            }

            // ── QUIT ───────────────────────────────────────────────────────
            if (command == "QUIT")
            {
                await writer.WriteLineAsync("221 2.0.0 Bye");
                return;
            }

            // ── Unknown ────────────────────────────────────────────────────
            await writer.WriteLineAsync("500 5.5.2 Syntax error, command unrecognized");
        }
    }

    private static string ExtractAngleAddress(string line)
    {
        var start = line.IndexOf('<');
        var end = line.IndexOf('>');
        if (start >= 0 && end > start)
        {
            return line[(start + 1)..end].Trim();
        }

        // No angle brackets — try the token after the colon.
        var colon = line.IndexOf(':');
        if (colon >= 0 && colon < line.Length - 1)
        {
            return line[(colon + 1)..].Trim();
        }

        return line.Trim();
    }

    private static long? ExtractMailSize(string line)
    {
        foreach (var parameter in line[(line.IndexOf(':') + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            if (!parameter.StartsWith("SIZE=", StringComparison.OrdinalIgnoreCase)) continue;
            return long.TryParse(parameter.AsSpan(5), NumberStyles.None, CultureInfo.InvariantCulture, out var size) ? size : long.MaxValue;
        }
        return null;
    }

    private static bool TryDecodeSaslBase64(string value, out byte[] bytes)
    {
        bytes = [];
        if (value.Length == 0 || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '+' or '/' or '='))) return false;
        try
        {
            bytes = Convert.FromBase64String(value);
            return Convert.ToBase64String(bytes) == value;
        }
        catch (FormatException) { return false; }
    }

    private static bool TryDecodeUtf8(byte[] bytes, out string value)
    {
        try { value = new UTF8Encoding(false, true).GetString(bytes); return true; }
        catch (DecoderFallbackException) { value = string.Empty; return false; }
    }

    private sealed record SmtpLineRead(string? Line, bool EndOfStream, bool TooLong, bool TimedOut);

    private sealed class SmtpLineReader(Stream stream)
    {
        private readonly byte[] _readBuffer = new byte[4096];
        private int _offset;
        private int _available;

        public async Task<SmtpLineRead> ReadLineAsync(int maximumOctets, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            var lineBytes = new byte[maximumOctets];
            var length = 0;
            var wireOctets = 0;
            var tooLong = false;
            try
            {
                while (true)
                {
                    if (_offset == _available)
                    {
                        _available = await stream.ReadAsync(_readBuffer.AsMemory(), timeoutCts.Token);
                        _offset = 0;
                        if (_available == 0) return new SmtpLineRead(null, true, false, false);
                    }
                    var octet = _readBuffer[_offset++];
                    if (octet == (byte)'\n') break;
                    wireOctets++;
                    if (length < lineBytes.Length) lineBytes[length++] = octet;
                    else tooLong = true;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new SmtpLineRead(null, false, false, true);
            }

            if (length > 0 && lineBytes[length - 1] == (byte)'\r') length--;
            else tooLong = true; // SMTP lines are CRLF terminated.
            if (wireOctets + 1 > maximumOctets) tooLong = true;
            return new SmtpLineRead(tooLong ? null : Encoding.ASCII.GetString(lineBytes, 0, length), false, tooLong, false);
        }
    }
}
