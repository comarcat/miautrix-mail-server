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

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMailStorage _storage;
    private readonly ILogger<ImapListenerService> _logger;
    private readonly X509Certificate2? _tlsCert;

    public ImapListenerService(
        IServiceScopeFactory scopeFactory,
        IMailStorage storage,
        ILogger<ImapListenerService> logger,
        TlsCertificateProvider tlsCertificateProvider)
    {
        _scopeFactory = scopeFactory;
        _storage = storage;
        _logger = logger;
        _tlsCert = tlsCertificateProvider.Certificate;
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

        var listener = new TcpListener(IPAddress.Any, ImapsPort);
        try
        {
            listener.Start();
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
                ImapsPort, ex.SocketErrorCode, ImapsPort);
            return;
        }

        _logger.LogInformation("IMAP listener started on port {Port} (IMAPS)", ImapsPort);

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
            _logger.LogInformation("IMAP listener on port {Port} stopped", ImapsPort);
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

        using var reader = new StreamReader(ssl, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        await using var writer = new StreamWriter(ssl, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };

        // Greeting
        await writer.WriteLineAsync("* OK [CAPABILITY IMAP4rev1 AUTH=PLAIN LITERAL+] Miautrix IMAP ready");

        // Create a scoped DbContext for this connection's lifetime.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authenticator = scope.ServiceProvider.GetRequiredService<IImapAuthenticator>();

        var session = new ImapSession(db, _storage, authenticator);

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) != null)
        {
            Stream? literalPayload = null;

            // Check for a literal size at end of command line: {nnn}
            var trimmed = line.TrimEnd();
            if (trimmed.EndsWith('}'))
            {
                var openBrace = trimmed.LastIndexOf('{');
                if (openBrace >= 0 && int.TryParse(trimmed[(openBrace + 1)..^1], out var literalSize))
                {
                    await writer.WriteLineAsync("+ Ready for literal data");

                    var buffer = new byte[literalSize];
                    var totalRead = 0;
                    while (totalRead < literalSize)
                    {
                        var read = await ssl.ReadAsync(buffer.AsMemory(totalRead, literalSize - totalRead), ct);
                        if (read == 0)
                        {
                            break;
                        }

                        totalRead += read;
                    }

                    literalPayload = new MemoryStream(buffer, 0, totalRead, writable: false);
                    line = trimmed[..openBrace]; // strip the {nnn} from the command line
                }
            }

            ImapCommandResult result;
            try
            {
                result = await session.ExecuteCommandAsync(line, literalPayload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "IMAP command error for line: {Line}", line);
                await writer.WriteLineAsync("* BAD Internal server error");
                continue;
            }
            finally
            {
                literalPayload?.Dispose();
            }

            // Write untagged responses
            foreach (var untagged in result.UntaggedResponses)
            {
                await writer.WriteLineAsync(untagged);
            }

            // Write binary payload inline (e.g. FETCH BODY[])
            if (result.BinaryPayload is { Length: > 0 })
            {
                await ssl.WriteAsync(result.BinaryPayload, ct);
                await writer.WriteLineAsync();
            }

            // Write tagged result
            await writer.WriteLineAsync($"{result.Tag} {result.Status} {result.Message}");

            if (result.Status == "BYE" || session.State == ImapState.LoggedOut)
            {
                break;
            }
        }
    }
}
