using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
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
    Task<IReadOnlyList<string>> GetPtrAsync(IPAddress address, CancellationToken ct = default);
    async Task<IReadOnlyList<IPAddress>> GetAAsync(string host, CancellationToken ct = default) =>
        (await GetAddressesAsync(host, ct)).Where(address => address.AddressFamily == AddressFamily.InterNetwork).ToArray();
    async Task<IReadOnlyList<IPAddress>> GetAaaaAsync(string host, CancellationToken ct = default) =>
        (await GetAddressesAsync(host, ct)).Where(address => address.AddressFamily == AddressFamily.InterNetworkV6).ToArray();
}

public sealed record TlsaRecord(byte Usage, byte Selector, byte MatchingType, byte[] AssociationData);
public sealed record ValidatedTlsaAnswer(bool DnssecValidated, IReadOnlyList<TlsaRecord> Records);
public interface IDaneResolver
{
    Task<ValidatedTlsaAnswer> GetTlsaAsync(string mxHost, CancellationToken ct = default);
}

/// <summary>Queries TLSA through the operator-selected validating resolver. AD is trusted only because the operator explicitly chose this resolver.</summary>
public sealed class DnsSecTlsaResolver(IPAddress resolverAddress) : IDaneResolver
{
    private readonly LookupClient _client = new(new LookupClientOptions(resolverAddress) { RequestDnsSecRecords = true, UseCache = false });
    public async Task<ValidatedTlsaAnswer> GetTlsaAsync(string mxHost, CancellationToken ct = default)
    {
        var response = await _client.QueryAsync($"_25._tcp.{mxHost}", QueryType.TLSA, cancellationToken: ct);
        var validated = response.Header.IsAuthenticData;
        var records = response.Answers.TlsaRecords().Select(x => new TlsaRecord((byte)x.CertificateUsage, (byte)x.Selector,
            (byte)x.MatchingType, x.CertificateAssociationData.ToArray())).ToArray();
        return new ValidatedTlsaAnswer(validated, records);
    }
}

public enum DaneMode { Off, Opportunistic }

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

    public async Task<IReadOnlyList<IPAddress>> GetAAsync(string host, CancellationToken ct = default)
    {
        var response = await _client.QueryAsync(host, QueryType.A, cancellationToken: ct);
        return response.Answers.ARecords().Select(record => record.Address).ToArray();
    }

    public async Task<IReadOnlyList<IPAddress>> GetAaaaAsync(string host, CancellationToken ct = default)
    {
        var response = await _client.QueryAsync(host, QueryType.AAAA, cancellationToken: ct);
        return response.Answers.AaaaRecords().Select(record => record.Address).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetTxtAsync(string name, CancellationToken ct = default)
    {
        var response = await _client.QueryAsync(name, QueryType.TXT, cancellationToken: ct);
        return response.Answers.TxtRecords().Select(record => string.Concat(record.Text)).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetPtrAsync(IPAddress address, CancellationToken ct = default)
    {
        var response = await _client.QueryReverseAsync(address, cancellationToken: ct);
        return response.Answers.PtrRecords().Select(record => record.PtrDomainName.Value.TrimEnd('.')).ToArray();
    }
}

public sealed class DirectSmtpOptions
{
    public string EhloName { get; init; } = string.Empty;
    public int ConnectTimeoutSeconds { get; init; } = 20;
    public int CommandTimeoutSeconds { get; init; } = 30;
    public int Port { get; init; } = 25;
    public DaneMode DaneMode { get; init; } = DaneMode.Off;
    public IDaneResolver? DaneResolver { get; init; }
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
        var modeRaw = Environment.GetEnvironmentVariable("OUTBOUND_DNSSEC_RESOLVER");
        IPAddress? resolver = null;
        if (modeRaw is not null && (!IPAddress.TryParse(modeRaw, out resolver) || resolver.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)))
            throw new InvalidOperationException("OUTBOUND_DNSSEC_RESOLVER must be an IP address.");
        var daneRaw = Environment.GetEnvironmentVariable("OUTBOUND_DANE_MODE") ?? "off";
        var daneMode = daneRaw.Trim().ToLowerInvariant() switch
        {
            "off" => DaneMode.Off,
            "opportunistic" => DaneMode.Opportunistic,
            _ => throw new InvalidOperationException("OUTBOUND_DANE_MODE must be off or opportunistic.")
        };
        if (daneMode == DaneMode.Opportunistic && resolver is null)
            System.Diagnostics.Trace.TraceWarning("OUTBOUND_DANE_MODE is opportunistic but OUTBOUND_DNSSEC_RESOLVER is unset; DANE is inactive.");
        return new DirectSmtpOptions { EhloName = name, ConnectTimeoutSeconds = connect, CommandTimeoutSeconds = command,
            DaneMode = daneMode, DaneResolver = resolver is null ? null : new DnsSecTlsaResolver(resolver) };
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

        var daneByTarget = new Dictionary<string, IReadOnlyList<TlsaRecord>>(StringComparer.OrdinalIgnoreCase);
        if (options.DaneMode == DaneMode.Opportunistic && options.DaneResolver is not null)
        {
            foreach (var host in targets.Select(t => t.PolicyHost).Distinct(StringComparer.OrdinalIgnoreCase))
                daneByTarget[host] = await GetUsableDaneRecordsAsync(host, ct);
        }

        MtaStsPolicy? policy;
        try { policy = await policyProvider.GetPolicyAsync(recipientDomain, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (daneByTarget.Values.All(records => records.Count == 0))
                return TransportSendResult.Fail("MTA-STS policy lookup failed: " + ex.GetType().Name, 451);
            policy = null; // DNSSEC-validated DANE takes precedence over MTA-STS (RFC 8461 §2).
        }

        if (policy?.Mode == MtaStsMode.Enforce)
        {
            targets = targets.Where(target => daneByTarget.GetValueOrDefault(target.PolicyHost)?.Count > 0 || policy.MatchesMx(target.PolicyHost)).ToList();
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
                var daneRecords = daneByTarget.GetValueOrDefault(target.PolicyHost) ?? [];
                var daneRequired = daneRecords.Count > 0;
                await SendToHostAsync(target.ConnectAddress, target.PolicyHost, sender, recipient, message.RawMessage, requireTls || daneRequired, daneRecords, ct);
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

    private async Task<IReadOnlyList<TlsaRecord>> GetUsableDaneRecordsAsync(string mxHost, CancellationToken ct)
    {
        if (options.DaneMode != DaneMode.Opportunistic || options.DaneResolver is null) return [];
        try
        {
            var answer = await options.DaneResolver.GetTlsaAsync(mxHost, ct);
            if (!answer.DnssecValidated) return [];
            // PKIX-TA/EE usages are not usable for SMTP DANE (RFC 7672 §3.1.3).
            return answer.Records.Where(r => r.Usage is 2 or 3 && r.Selector is 0 or 1 && r.MatchingType is 0 or 1 or 2).ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Trace.TraceWarning("DANE TLSA lookup failed for {0}: {1}", mxHost, ex.GetType().Name);
            return [];
        }
    }

    private async Task SendToHostAsync(string connectAddress, string tlsTargetHost, string sender, string recipient, string rawMessage, bool requireTls, IReadOnlyList<TlsaRecord> daneRecords, CancellationToken ct)
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
                    RemoteCertificateValidationCallback = options.TestCertificateValidationCallback ?? (daneRecords.Count > 0
                        ? (_, certificate, chain, errors) => ValidateDaneCertificate(certificate, chain, errors, daneRecords)
                        : null)
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

    private static bool ValidateDaneCertificate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors, IReadOnlyList<TlsaRecord> records)
    {
        if (certificate is null || (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
        using var leaf = new X509Certificate2(certificate);
        foreach (var record in records)
        {
            IEnumerable<X509Certificate2> candidates = record.Usage == 3
                ? [leaf]
                : (chain?.ChainElements.Cast<X509ChainElement>().Select(e => e.Certificate) ?? []).ToArray();
            foreach (var candidate in candidates)
            {
                byte[] selected;
                try
                {
                    if (record.Selector == 0) selected = candidate.RawData;
                    else using (var rsa = candidate.GetRSAPublicKey())
                    using (var ecdsa = rsa is null ? candidate.GetECDsaPublicKey() : null)
                        selected = rsa?.ExportSubjectPublicKeyInfo() ?? ecdsa?.ExportSubjectPublicKeyInfo() ?? [];
                }
                catch (CryptographicException) { continue; }
                byte[] actual = record.MatchingType switch
                {
                    0 => selected,
                    1 => SHA256.HashData(selected),
                    2 => SHA512.HashData(selected),
                    _ => []
                };
                if (actual.Length > 0 && CryptographicOperations.FixedTimeEquals(actual, record.AssociationData))
                {
                    // DANE-EE/TA relaxes PKIX trust, never the MX reference-name check.
                    return (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None;
                }
            }
        }
        return false;
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
