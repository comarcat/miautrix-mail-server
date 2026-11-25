using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Security;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Application.Mail.Mime;

namespace Miautrix.Mail.Worker;

/// <summary>
/// Listens on port 993 (IMAPS — implicit TLS) and dispatches each
/// connection to an <see cref="ImapSession"/> for per-command processing.
/// Each session gets a scoped <see cref="AppDbContext"/> so EF change tracking
/// is isolated between connections.
/// </summary>
public sealed class ImapListenerService : BackgroundService
{
    private const int ImapsPort = 993;
    private readonly int _port;
    private readonly TimeSpan _idleTimeout;
    private readonly TimeSpan _idlePollInterval;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMailStorage _storage;
    private readonly ILogger<ImapListenerService> _logger;
    private readonly X509Certificate2? _tlsCert;
    public int BoundPort { get; private set; }

    public ImapListenerService(
        IServiceScopeFactory scopeFactory,
        IMailStorage storage,
        ILogger<ImapListenerService> logger,
        TlsCertificateProvider tlsCertificateProvider,
        int port = ImapsPort,
        TimeSpan? idleTimeout = null,
        TimeSpan? idlePollInterval = null)
    {
        _scopeFactory = scopeFactory;
        _storage = storage;
        _logger = logger;
        _tlsCert = tlsCertificateProvider.Certificate;
        _port = port;
        _idleTimeout = idleTimeout is { } configuredTimeout && configuredTimeout > TimeSpan.Zero
            ? configuredTimeout : TimeSpan.FromMinutes(30);
        _idlePollInterval = idlePollInterval ?? TimeSpan.FromSeconds(2);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_tlsCert is null)
        {
            _logger.LogWarning(
                "IMAP listener requires a TLS certificate. " +
                "Set MIAUTRIX_TLS_CERT_PATH and MIAUTRIX_TLS_KEY_PATH. Skipping this listener.");
            return;
        }

        var listener = new TcpListener(IPAddress.Any, _port);
        try
        {
            listener.Start();
            BoundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        catch (SocketException ex)
        {
            // Port 993 is privileged: needs CAP_NET_BIND_SERVICE. Log and leave the SMTP
            // listeners running rather than taking the whole host down.
            _logger.LogError(
                ex,
                "Cannot bind IMAP port {Port}: {Reason}. " +
                "For EACCES, the unit needs AmbientCapabilities=CAP_NET_BIND_SERVICE. " +
                "For EADDRINUSE, another process holds the port (check: ss -lntup | grep :{Port}).",
                _port, ex.SocketErrorCode, _port);
            return;
        }

        _logger.LogInformation("IMAP listener started on port {Port} (IMAPS)", BoundPort);

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
                    _logger.LogError(ex, "Error accepting IMAP connection");
                    continue;
                }

                _ = Task.Run(() => HandleConnectionAsync(client, stoppingToken), stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
            _logger.LogInformation("IMAP listener on port {Port} stopped", BoundPort);
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            _logger.LogDebug("IMAP connection from {Remote}", remoteEndpoint);
            try
            {
                await RunImapSessionAsync(client, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "IMAP session error from {Remote}", remoteEndpoint);
            }
        }
    }

    private async Task RunImapSessionAsync(TcpClient client, CancellationToken ct)
    {
        // Implicit TLS for IMAPS — wrap stream immediately.
        var ssl = new SslStream(client.GetStream(), false);
        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = _tlsCert,
            ClientCertificateRequired = false,
        }, ct);

        // Greeting
        await WriteAsciiAsync(ssl, $"* OK [CAPABILITY {ImapSession.CapabilityLine[13..]}] Miautrix IMAP ready\r\n", ct);

        // Create a scoped DbContext for this connection's lifetime.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authenticator = scope.ServiceProvider.GetRequiredService<IImapAuthenticator>();
        var authorization = scope.ServiceProvider.GetRequiredService<ITenantAuthorizationHelper>();
        var mailboxService = scope.ServiceProvider.GetRequiredService<IMailboxService>();
        var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();
        var mimeFormatter = scope.ServiceProvider.GetRequiredService<IImapMimeFormatter>();
        var uidAllocator = scope.ServiceProvider.GetRequiredService<IMessageUidAllocator>();

        var session = new ImapSession(db, _storage, authenticator, authorization, mailboxService, messageService, mimeFormatter, uidAllocator);

        var maxLiteral = int.TryParse(Environment.GetEnvironmentVariable("MIAUTRIX_IMAP_MAX_LITERAL_BYTES"), out var configured) ? Math.Clamp(configured, 1024, 50 * 1024 * 1024) : 50 * 1024 * 1024;
        var connectionReader = new ImapConnectionReader(ssl, maxLineBytes: 64 * 1024, maxLiteralBytes: maxLiteral);
        while (!ct.IsCancellationRequested)
        {
            CollectedImapCommand? collected = null;
            ImapCommand? parsed = null;
            ImapCommandResult result;
            try
            {
                if (session.AwaitingSaslContinuation)
                {
                    var saslLine = await connectionReader.ReadLineAsync(ct);
                    if (saslLine is null) break;
                    result = await session.CompleteSaslAsync(Encoding.ASCII.GetString(saslLine), ct);
                    await ImapResponseWriter.WriteAsync(ssl, result, ct);
                    continue;
                }
                collected = await ImapCommandCollector.ReadAsync(connectionReader,
                    async (_, token) => await WriteAsciiAsync(ssl, "+ Ready for literal data\r\n", token),
                    maxLiteral, ct, literalMinusEnabled: session.IsImap4Rev2Enabled);
                parsed = ImapCommandTokenizer.Parse(collected.Text, collected.Literals);
                result = await session.ExecuteCommandAsync(parsed, ct);
            }
            catch (ImapLiteralTooLargeException ex)
            {
                _logger.LogDebug("IMAP command {CommandName} failed for tag {Tag} ({ExceptionType})", ex.CommandName, ex.Tag, ex.GetType().Name);
                if (ex.NonSynchronizing)
                {
                    await WriteAsciiAsync(ssl, "* BYE Literal too large\r\n", ct);
                    return;
                }
                await WriteAsciiAsync(ssl, $"{ex.Tag} NO [TOOBIG] Literal too large\r\n", ct);
                continue;
            }
            catch (Exception ex)
            {
                var safeTag = collected?.Text.Split(' ', 2)[0] ?? (ex as ImapCommandCollectionException)?.Tag ?? "A0";
                var safeCommand = "UNKNOWN";
                try { if (collected is not null) safeCommand = ImapCommandTokenizer.Parse(collected.Text, collected.Literals).Name; } catch (Exception) { }
                _logger.LogDebug("IMAP command {CommandName} failed for tag {Tag} ({ExceptionType})", safeCommand, safeTag, ex.GetType().Name);
                await WriteAsciiAsync(ssl, $"{safeTag} BAD Internal server error\r\n", ct);
                continue;
            }

            await ImapResponseWriter.WriteAsync(ssl, result, ct);

            if (session.IsIdling && !await RunIdleAsync(session, ssl, connectionReader, ct))
                break;

            if (result.Status == "BYE" || session.State == ImapState.LoggedOut)
            {
                break;
            }
        }
    }

    private async Task<bool> RunIdleAsync(ImapSession session, SslStream ssl, ImapConnectionReader reader, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + _idleTimeout;
        var input = reader.ReadLineAsync(ct);
        while (!ct.IsCancellationRequested)
        {
            if (input.IsCompleted)
            {
                var line = await input;
                if (line is null) return false;
                if (Encoding.ASCII.GetString(line).Trim().Equals("DONE", StringComparison.OrdinalIgnoreCase))
                {
                    await ImapResponseWriter.WriteAsync(ssl, session.EndIdle(), ct);
                    return true;
                }
                session.AbortIdle();
                await WriteAsciiAsync(ssl, "* BYE Expected DONE during IDLE\r\n", ct);
                return false;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                session.AbortIdle();
                await WriteAsciiAsync(ssl, "* BYE IDLE timeout\r\n", ct);
                return false;
            }
            var delay = remaining < _idlePollInterval ? remaining : _idlePollInterval;
            await Task.WhenAny(input, Task.Delay(delay, ct));
            if (input.IsCompleted) continue;

            if (DateTimeOffset.UtcNow >= deadline)
            {
                session.AbortIdle();
                await WriteAsciiAsync(ssl, "* BYE IDLE timeout\r\n", ct);
                return false;
            }
            var changes = await session.PollIdleChangesAsync(ct);
            await ImapResponseWriter.WriteAsync(ssl, changes, ct);
            if (changes.ResponseSegments.Any(segment => Encoding.ASCII.GetString(segment.Bytes).StartsWith("* BYE", StringComparison.Ordinal)))
                return false;
        }
        return false;
    }

    private static Task WriteAsciiAsync(Stream stream, string value, CancellationToken cancellationToken) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(value), cancellationToken).AsTask();

}
