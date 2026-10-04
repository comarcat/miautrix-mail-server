using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmtpListenerService> _logger;
    private readonly int _port;
    private readonly SmtpListenerMode _mode;
    private readonly X509Certificate2? _tlsCert;
    private readonly string _hostname;

    public SmtpListenerService(
        IServiceScopeFactory scopeFactory,
        ILogger<SmtpListenerService> logger,
        int port,
        SmtpListenerMode mode,
        X509Certificate2? tlsCert,
        string hostname)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _port = port;
        _mode = mode;
        _tlsCert = tlsCert;
        _hostname = hostname;
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
        StreamReader reader;
        StreamWriter writer;

        if (_mode == SmtpListenerMode.SubmissionImplicitTls)
        {
            var ssl = new SslStream(stream, false);
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _tlsCert,
                ClientCertificateRequired = false,
            }, ct);
            stream = ssl;
            isTlsActive = true;
        }

        reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };

        // Scoped handlers for this connection. Each SMTP session gets its own DbContext.
        using var scope = _scopeFactory.CreateScope();
        var submissionHandler = scope.ServiceProvider.GetRequiredService<ISmtpSubmissionHandler>();
        var inboundHandler = scope.ServiceProvider.GetRequiredService<ISmtpInboundHandler>();

        // State
        string? authenticatedUsername = null;
        Guid? tenantId = null;
        Guid? userId = null;
        string? mailFrom = null;
        string? rcptTo = null;

        await writer.WriteLineAsync($"220 {_hostname} ESMTP Miautrix");

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) != null)
        {
            var upper = line.TrimStart().ToUpperInvariant();

            // ── EHLO/HELO ──────────────────────────────────────────────────
            if (upper.StartsWith("EHLO") || upper.StartsWith("HELO"))
            {
                await writer.WriteLineAsync($"250-{_hostname}");
                if (_mode == SmtpListenerMode.SubmissionStartTls && !isTlsActive)
                {
                    await writer.WriteLineAsync("250-STARTTLS");
                }
                if (isTlsActive || _mode == SmtpListenerMode.SubmissionImplicitTls)
                {
                    await writer.WriteLineAsync("250-AUTH LOGIN PLAIN");
                }
                await writer.WriteLineAsync("250-SIZE 26214400");
                await writer.WriteLineAsync("250 OK");
                continue;
            }

            // ── STARTTLS ───────────────────────────────────────────────────
            if (upper.StartsWith("STARTTLS"))
            {
                if (_mode == SmtpListenerMode.Inbound || isTlsActive)
                {
                    await writer.WriteLineAsync("502 5.5.1 Command not implemented");
                    continue;
                }

                await writer.WriteLineAsync("220 2.0.0 Go ahead");

                var ssl = new SslStream(stream, false);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = _tlsCert,
                    ClientCertificateRequired = false,
                }, ct);
                stream = ssl;
                isTlsActive = true;

                await writer.FlushAsync(ct);
                reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
                continue;
            }

            // ── AUTH LOGIN ─────────────────────────────────────────────────
            if (upper.StartsWith("AUTH LOGIN"))
            {
                if (!isTlsActive)
                {
                    // Security rule: reject AUTH without encryption.
                    await writer.WriteLineAsync("538 5.7.11 Encryption required for requested authentication mechanism");
                    continue;
                }

                await writer.WriteLineAsync("334 VXNlcm5hbWU6"); // base64("Username:")
                var userB64 = await reader.ReadLineAsync(ct) ?? string.Empty;
                await writer.WriteLineAsync("334 UGFzc3dvcmQ6"); // base64("Password:")
                var passB64 = await reader.ReadLineAsync(ct) ?? string.Empty;

                string username, password;
                try
                {
                    username = Encoding.UTF8.GetString(Convert.FromBase64String(userB64.Trim()));
                    password = Encoding.UTF8.GetString(Convert.FromBase64String(passB64.Trim()));
                }
                catch
                {
                    await writer.WriteLineAsync("501 5.5.4 Invalid base64 encoding");
                    continue;
                }

                var response = submissionHandler.HandleAuth(
                    username, password, isTlsActive,
                    out var authTenant, out var authUser);

                if (response.IsSuccess)
                {
                    authenticatedUsername = username;
                    tenantId = authTenant;
                    userId = authUser;
                    await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                }
                else
                {
                    await writer.WriteLineAsync($"{response.ToSmtpString()}");
                }

                continue;
            }

            // ── AUTH PLAIN ─────────────────────────────────────────────────
            if (upper.StartsWith("AUTH PLAIN"))
            {
                if (!isTlsActive)
                {
                    await writer.WriteLineAsync("538 5.7.11 Encryption required for requested authentication mechanism");
                    continue;
                }

                // AUTH PLAIN may come with inline credentials or as a two-step
                var parts = line.Split(' ');
                string credential64 = parts.Length > 2
                    ? parts[2]
                    : (await reader.ReadLineAsync(ct) ?? string.Empty);

                string username, password;
                try
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(credential64.Trim()));
                    // Format: \0authcid\0passwd or authzid\0authcid\0passwd
                    var segments = decoded.Split('\0');
                    username = segments.Length >= 3 ? segments[1] : (segments.Length == 2 ? segments[0] : string.Empty);
                    password = segments.Length >= 3 ? segments[2] : (segments.Length == 2 ? segments[1] : string.Empty);
                }
                catch
                {
                    await writer.WriteLineAsync("501 5.5.4 Invalid base64 encoding");
                    continue;
                }

                var response = submissionHandler.HandleAuth(
                    username, password, isTlsActive,
                    out var authTenant, out var authUser);

                if (response.IsSuccess)
                {
                    authenticatedUsername = username;
                    tenantId = authTenant;
                    userId = authUser;
                    await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                }
                else
                {
                    await writer.WriteLineAsync($"{response.ToSmtpString()}");
                }

                continue;
            }

            // ── MAIL FROM ──────────────────────────────────────────────────
            if (upper.StartsWith("MAIL FROM"))
            {
                mailFrom = ExtractAngleAddress(line);
                await writer.WriteLineAsync("250 2.1.0 OK");
                continue;
            }

            // ── RCPT TO ────────────────────────────────────────────────────
            if (upper.StartsWith("RCPT TO"))
            {
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

                rcptTo = recipient;
                await writer.WriteLineAsync("250 2.1.5 OK");
                continue;
            }

            // ── DATA ───────────────────────────────────────────────────────
            if (upper.StartsWith("DATA"))
            {
                if (string.IsNullOrEmpty(mailFrom) || string.IsNullOrEmpty(rcptTo))
                {
                    await writer.WriteLineAsync("503 5.5.1 Need MAIL and RCPT first");
                    continue;
                }

                await writer.WriteLineAsync("354 Start mail input; end with <CRLF>.<CRLF>");

                var body = new StringBuilder();
                string? dataLine;
                while ((dataLine = await reader.ReadLineAsync(ct)) != null)
                {
                    if (dataLine == ".")
                    {
                        break;
                    }
                    // Un-dot-stuff
                    body.AppendLine(dataLine.StartsWith("..") ? dataLine[1..] : dataLine);
                }

                var rawMessage = body.ToString();

                if (_mode == SmtpListenerMode.Inbound)
                {
                    var (resp, _) = await inboundHandler.HandleDataAsync(
                        tenantId!.Value, mailFrom!, rcptTo!, rawMessage, cancellationToken: ct);
                    await writer.WriteLineAsync(resp.ToSmtpString());
                }
                else
                {
                    var (resp, _) = await submissionHandler.HandleSubmissionAsync(
                        isAuthenticated: authenticatedUsername is not null,
                        isTlsEncrypted: isTlsActive,
                        tenantId: tenantId,
                        sender: mailFrom!,
                        recipient: rcptTo!,
                        rawMessage: rawMessage,
                        cancellationToken: ct);
                    await writer.WriteLineAsync(resp.ToSmtpString());
                }

                // Reset envelope state for potential next message in same session.
                mailFrom = null;
                rcptTo = null;
                continue;
            }

            // ── NOOP ───────────────────────────────────────────────────────
            if (upper.StartsWith("NOOP"))
            {
                await writer.WriteLineAsync("250 2.0.0 OK");
                continue;
            }

            // ── RSET ───────────────────────────────────────────────────────
            if (upper.StartsWith("RSET"))
            {
                mailFrom = null;
                rcptTo = null;
                await writer.WriteLineAsync("250 2.0.0 OK");
                continue;
            }

            // ── QUIT ───────────────────────────────────────────────────────
            if (upper.StartsWith("QUIT"))
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
}
