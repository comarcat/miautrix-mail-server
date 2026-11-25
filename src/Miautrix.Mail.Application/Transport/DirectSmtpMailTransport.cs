using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using DnsClient;
using Miautrix.Mail.Domain;

namespace Miautrix.Mail.Application.Transport;

public sealed record MxHost(string Host, ushort Preference);

/// <summary>DNS seam used by direct delivery, allowing deterministic MX fixtures in tests.</summary>
public interface IDnsResolver
{
    Task<IReadOnlyList<MxHost>> GetMxAsync(string domain, CancellationToken ct = default);
    Task<IReadOnlyList<IPAddress>> GetAddressesAsync(string host, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetTxtAsync(string name, CancellationToken ct = default);
}

public sealed class DnsClientResolver : IDnsResolver
{
    private readonly LookupClient _client = new();

    public async Task<IReadOnlyList<MxHost>> GetMxAsync(string domain, CancellationToken ct = default)
    {
        var response = await _client.QueryAsync(domain, QueryType.MX, cancellationToken: ct);
        return response.Answers.MxRecords().Select(r => new MxHost(r.Exchange.Value.TrimEnd('.'), r.Preference)).ToArray();
    }

    public async Task<IReadOnlyList<IPAddress>> GetAddressesAsync(string host, CancellationToken ct = default)
    {
        var result = new List<IPAddress>();
        var a = await _client.QueryAsync(host, QueryType.A, cancellationToken: ct);
        result.AddRange(a.Answers.ARecords().Select(r => r.Address));
        var aaaa = await _client.QueryAsync(host, QueryType.AAAA, cancellationToken: ct);
        result.AddRange(aaaa.Answers.AaaaRecords().Select(r => r.Address));
        return result;
    }

    public async Task<IReadOnlyList<string>> GetTxtAsync(string name, CancellationToken ct = default)
    {
        var response = await _client.QueryAsync(name, QueryType.TXT, cancellationToken: ct);
        return response.Answers.TxtRecords().Select(record => string.Concat(record.Text)).ToArray();
    }
}

public sealed class DirectSmtpOptions
{
    public string EhloName { get; init; } = string.Empty;
    public int ConnectTimeoutSeconds { get; init; } = 20;
    public int CommandTimeoutSeconds { get; init; } = 30;
    public int Port { get; init; } = 25;
    internal RemoteCertificateValidationCallback? TestCertificateValidationCallback { get; init; }

    public static DirectSmtpOptions FromEnvironment()
    {
        // Falls back to MIAUTRIX_HOSTNAME (already mandatory for the Worker), so existing
        // deployments start without a new .env entry. A value that is set must be valid.
        var name = ResolveEhloName();
        if (string.IsNullOrWhiteSpace(name) || !Uri.CheckHostName(name).Equals(UriHostNameType.Dns))
            throw new InvalidOperationException("OUTBOUND_EHLO_NAME (or MIAUTRIX_HOSTNAME) must be a valid DNS hostname.");
        var connect = ReadTimeout("OUTBOUND_SMTP_CONNECT_TIMEOUT_SECONDS", 20);
        var command = ReadTimeout("OUTBOUND_SMTP_COMMAND_TIMEOUT_SECONDS", 30);
        return new DirectSmtpOptions { EhloName = name, ConnectTimeoutSeconds = connect, CommandTimeoutSeconds = command };
    }

    /// <summary>OUTBOUND_EHLO_NAME when set, otherwise MIAUTRIX_HOSTNAME.</summary>
    public static string? ResolveEhloName()
    {
        var explicitName = Environment.GetEnvironmentVariable("OUTBOUND_EHLO_NAME");
        return string.IsNullOrWhiteSpace(explicitName)
            ? Environment.GetEnvironmentVariable("MIAUTRIX_HOSTNAME")?.Trim()
            : explicitName.Trim();
    }

    private static int ReadTimeout(string key, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (raw is null) return fallback;
        if (!int.TryParse(raw, out var value) || value is < 1 or > 300)
            throw new InvalidOperationException($"{key} must be between 1 and 300 seconds.");
        return value;
    }
}

/// <summary>SMTP delivery to recipient MX hosts with opportunistic STARTTLS.</summary>
public sealed class DirectSmtpMailTransport(IDnsResolver dns, DirectSmtpOptions options, IMtaStsPolicyProvider policyProvider) : IOutboundMailTransport
{
    public string Mode => DomainTransportModes.Direct;

    public async Task<TransportSendResult> SendAsync(OutboundMessage message, CancellationToken ct = default)
    {
        var recipient = GetAddress(message.Recipient);
        var sender = string.IsNullOrWhiteSpace(message.Sender) ? string.Empty : GetAddress(message.Sender);
        IReadOnlyList<MxHost> mx;
        var recipientDomain = recipient[(recipient.LastIndexOf('@') + 1)..];
        try { mx = await dns.GetMxAsync(recipientDomain, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return TransportSendResult.Fail("Recipient MX lookup failed: " + ex.GetType().Name); }

        if (mx.Any(x => x.Host == string.Empty || x.Host == "."))
            return TransportSendResult.Fail("Recipient domain publishes a null MX record.", 556, permanent: true);

        var targets = new List<DeliveryTarget>();
        if (mx.Count == 0)
        {
            try
            {
                var addresses = await dns.GetAddressesAsync(recipientDomain, ct);
                if (addresses.Count == 0) return TransportSendResult.Fail("Recipient domain has no MX or address records.", 450);
                targets.AddRange(addresses.Select(address => new DeliveryTarget(address.ToString(), recipientDomain)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { return TransportSendResult.Fail("Recipient address lookup failed: " + ex.GetType().Name); }
        }
        else
        {
            try
            {
                foreach (var mxHost in mx.OrderBy(x => x.Preference))
                {
                    var addresses = await dns.GetAddressesAsync(mxHost.Host, ct);
                    targets.AddRange(addresses.Select(address => new DeliveryTarget(address.ToString(), mxHost.Host)));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return TransportSendResult.Fail("Recipient MX address lookup failed: " + ex.GetType().Name, 451);
            }
            if (targets.Count == 0) return TransportSendResult.Fail("Recipient MX hosts have no address records.", 451);
        }

        MtaStsPolicy? policy;
        try { policy = await policyProvider.GetPolicyAsync(recipientDomain, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TransportSendResult.Fail("MTA-STS policy lookup failed: " + ex.GetType().Name, 451);
        }

        if (policy?.Mode == MtaStsMode.Enforce)
        {
            targets = targets.Where(target => policy.MatchesMx(target.PolicyHost)).ToList();
            if (targets.Count == 0)
                return TransportSendResult.Fail("MTA-STS enforce policy has no matching MX host.", 451);
        }
        var requireTls = policy?.Mode == MtaStsMode.Enforce;
        if (policy?.Mode == MtaStsMode.Testing)
            System.Diagnostics.Trace.TraceWarning("Recipient domain {0} has an MTA-STS testing policy.", recipientDomain);

        var transient = false;
        int? temporaryCode = null;
        foreach (var target in targets)
        {
            try
            {
                await SendToHostAsync(target.ConnectAddress, target.PolicyHost, sender, recipient, message.RawMessage, requireTls, ct);
                return TransportSendResult.Ok(250);
            }
            catch (SmtpReplyException ex)
            {
                if (ex.Code is >= 500 and < 600) return TransportSendResult.Fail("Recipient server permanently rejected delivery.", ex.Code, permanent: true);
                transient = true;
                temporaryCode ??= ex.Code;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                transient = true;
            }
        }
        return TransportSendResult.Fail(transient ? "All recipient MX hosts failed temporarily or could not be reached." : "Delivery failed.", temporaryCode ?? 451);
    }

    private async Task SendToHostAsync(string connectAddress, string tlsTargetHost, string sender, string recipient, string rawMessage, bool requireTls, CancellationToken ct)
    {
        using var tcp = new TcpClient();
        using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            connectTimeout.CancelAfter(TimeSpan.FromSeconds(options.ConnectTimeoutSeconds));
            await tcp.ConnectAsync(IPAddress.Parse(connectAddress), options.Port, connectTimeout.Token);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.CommandTimeoutSeconds));
        Stream stream = tcp.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
        await ExpectAsync(reader, 220, timeout.Token);
        var ehlo = await CommandAsync(reader, writer, $"EHLO {options.EhloName}", timeout.Token);
        if (ehlo.Code != 250) throw new SmtpReplyException(ehlo.Code);
        if (ehlo.Lines.Any(line => line.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase)))
        {
            var startTls = await CommandAsync(reader, writer, "STARTTLS", timeout.Token);
            if (startTls.Code == 220)
            {
                var tls = new SslStream(stream, false);
                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = tlsTargetHost,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.Online,
                    RemoteCertificateValidationCallback = options.TestCertificateValidationCallback
                }, timeout.Token);
                stream = tls;
                reader.Dispose(); writer.Dispose();
                // Rebind protocol wrappers to the negotiated stream.
                await SendAfterTlsAsync(stream, sender, recipient, rawMessage, timeout.Token);
                return;
            }
            if (requireTls) throw new SmtpReplyException(450);
        }
        else if (requireTls) throw new SmtpReplyException(450);
        await EnvelopeAndDataAsync(reader, writer, sender, recipient, rawMessage, timeout.Token);
    }

    private async Task SendAfterTlsAsync(Stream stream, string sender, string recipient, string rawMessage, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
        var ehlo = await CommandAsync(reader, writer, $"EHLO {options.EhloName}", ct);
        if (ehlo.Code != 250) throw new SmtpReplyException(ehlo.Code);
        await EnvelopeAndDataAsync(reader, writer, sender, recipient, rawMessage, ct);
    }

    private static async Task EnvelopeAndDataAsync(StreamReader reader, StreamWriter writer, string sender, string recipient, string rawMessage, CancellationToken ct)
    {
        var mail = await CommandAsync(reader, writer, sender.Length == 0 ? "MAIL FROM:<>" : $"MAIL FROM:<{sender}>", ct);
        if (mail.Code != 250) throw new SmtpReplyException(mail.Code);
        var rcpt = await CommandAsync(reader, writer, $"RCPT TO:<{recipient}>", ct);
        if (rcpt.Code is not (250 or 251 or 252)) throw new SmtpReplyException(rcpt.Code);
        var data = await CommandAsync(reader, writer, "DATA", ct);
        if (data.Code != 354) throw new SmtpReplyException(data.Code);
        var payload = rawMessage.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var line in payload)
            await writer.WriteLineAsync(((line.StartsWith('.') ? "." : string.Empty) + line).AsMemory(), ct);
        await writer.WriteLineAsync(".".AsMemory(), ct);
        var accepted = await ReadReplyAsync(reader, ct);
        if (accepted.Code != 250) throw new SmtpReplyException(accepted.Code);
        await CommandAsync(reader, writer, "QUIT", ct);
    }

    private static async Task<(int Code, List<string> Lines)> CommandAsync(StreamReader reader, StreamWriter writer, string command, CancellationToken ct)
    {
        await writer.WriteLineAsync(command.AsMemory(), ct);
        return await ReadReplyAsync(reader, ct);
    }

    private static async Task ExpectAsync(StreamReader reader, int code, CancellationToken ct)
    {
        var reply = await ReadReplyAsync(reader, ct);
        if (reply.Code != code) throw new SmtpReplyException(reply.Code);
    }

    private static async Task<(int Code, List<string> Lines)> ReadReplyAsync(StreamReader reader, CancellationToken ct)
    {
        var lines = new List<string>();
        int code = 0;
        while (true)
        {
            var line = await reader.ReadLineAsync(ct) ?? throw new IOException("SMTP server closed the connection.");
            lines.Add(line);
            if (line.Length < 3 || !int.TryParse(line.AsSpan(0, 3), out code)) throw new IOException("Invalid SMTP reply.");
            if (line.Length < 4 || line[3] != '-') break;
        }
        return (code, lines);
    }

    private static string GetAddress(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('<') && trimmed.EndsWith('>')) trimmed = trimmed[1..^1];
        var separator = trimmed.LastIndexOf('@');
        if (separator <= 0 || separator == trimmed.Length - 1) throw new FormatException("SMTP address is invalid.");
        return trimmed;
    }

    private sealed class SmtpReplyException(int code) : Exception { public int Code { get; } = code; }
    private sealed record DeliveryTarget(string ConnectAddress, string PolicyHost);
}
