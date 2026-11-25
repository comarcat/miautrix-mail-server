using System.Net;
using System.Net.Sockets;
using Miautrix.Mail.Application.Transport;
using System.Net.Security;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Miautrix.Mail.UnitTests.Transport;

public sealed class DirectSmtpSocketTests
{
    [Fact]
    public async Task First_mx_connection_failure_falls_through_to_next_mx()
    {
        await using var server = new ScriptedSmtpServer(2, (client, index, record) => index == 0
            ? ScriptedSmtpServer.RejectGreetingAsync(client)
            : ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, rcptCode: 250, record));
        var result = await Create(server.Port, [new MxHost("127.0.0.1", 10), new MxHost("127.0.0.1", 20)])
            .SendAsync(Message());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, server.AcceptedConnections);
    }

    [Fact]
    public async Task Mx_hosts_are_attempted_in_preference_order()
    {
        await using var server = new ScriptedSmtpServer(1,
            (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, 250, record));
        var dns = new FakeDns([new MxHost("mx-high.test", 20), new MxHost("mx-low.test", 10)],
            new Dictionary<string, IPAddress>
            {
                ["mx-high.test"] = IPAddress.Parse("127.0.0.1"),
                ["mx-low.test"] = IPAddress.Parse("127.0.0.2")
            });

        var result = await new DirectSmtpMailTransport(dns,
            new DirectSmtpOptions { EhloName = "sender.test", Port = server.Port }, new FixedPolicyProvider(null)).SendAsync(Message());

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "mx-low.test", "mx-high.test" }, dns.AddressLookupOrder);
    }

    [Fact]
    public async Task No_mx_uses_a_and_aaaa_answers_from_the_dns_abstraction()
    {
        await using var server = new ScriptedSmtpServer(1,
            (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, 250, record));
        var dns = new FakeDns([], fallbackAddresses: [IPAddress.Loopback]);

        var result = await new DirectSmtpMailTransport(dns,
            new DirectSmtpOptions { EhloName = "sender.test", Port = server.Port }, new FixedPolicyProvider(null)).SendAsync(Message());

        Assert.True(result.IsSuccess);
        Assert.Equal(IPAddress.Loopback, Assert.Single(server.RemoteAddresses));
    }

    [Fact]
    public async Task Multiple_recipients_in_one_domain_are_delivered_independently()
    {
        await using var server = new ScriptedSmtpServer(2,
            (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, 250, record));
        var transport = Create(server.Port, [new MxHost("127.0.0.1", 0)]);

        var first = await transport.SendAsync(Message() with { Recipient = "first@recipient.test" });
        var second = await transport.SendAsync(Message() with { Recipient = "second@recipient.test" });

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, server.AcceptedConnections);
    }

    [Fact]
    public async Task A_server_without_starttls_accepts_opportunistic_plaintext_delivery()
    {
        await using var server = new ScriptedSmtpServer(1,
            (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, rcptCode: 250, record));

        var result = await Create(server.Port, [new MxHost("127.0.0.1", 0)]).SendAsync(Message());

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("STARTTLS", server.Commands);
    }

    [Fact]
    public async Task A_server_offering_starttls_is_attempted_and_temporary_refusal_allows_opportunistic_delivery()
    {
        await using var server = new ScriptedSmtpServer(1,
            (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: true, rcptCode: 250, record));

        var result = await Create(server.Port, [new MxHost("127.0.0.1", 0)]).SendAsync(Message());

        Assert.True(result.IsSuccess);
        Assert.Contains("STARTTLS", server.Commands);
    }

    [Fact]
    public async Task Starttls_is_negotiated_and_smtp_session_restarts_with_ehlo()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=mx.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("mx.test");
        request.CertificateExtensions.Add(san.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx, "smtp-test"), "smtp-test",
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
        await using var server = new ScriptedSmtpServer(1,
            (client, _, record) => ScriptedSmtpServer.AcceptTlsMessageAsync(client, certificate, record));
        var dns = new FakeDns([new MxHost("mx.test", 0)], new Dictionary<string, IPAddress> { ["mx.test"] = IPAddress.Loopback });
        var options = new DirectSmtpOptions
        {
            EhloName = "sender.test",
            Port = server.Port,
            TestCertificateValidationCallback = (_, _, _, _) => true
        };

        var result = await new DirectSmtpMailTransport(dns, options, new FixedPolicyProvider(null)).SendAsync(Message());

        Assert.True(result.IsSuccess);
        Assert.Contains("STARTTLS", server.Commands);
        Assert.Equal(2, server.Commands.Count(command => command.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Validated_dane_ee_spki_sha256_match_delivers()
    {
        using var certificate = CreateCertificate();
        await using var server = new ScriptedSmtpServer(1, (client, _, record) => ScriptedSmtpServer.AcceptTlsMessageAsync(client, certificate, record));
        var spki = certificate.GetRSAPublicKey()!.ExportSubjectPublicKeyInfo();
        var answer = new ValidatedTlsaAnswer(true, [new TlsaRecord(3, 1, 1, SHA256.HashData(spki))]);
        var transport = CreateDaneTransport(server.Port, answer);

        var result = await transport.SendAsync(Message());

        Assert.True(result.IsSuccess);
        Assert.Contains("STARTTLS", server.Commands);
    }

    [Fact]
    public async Task Validated_dane_mismatch_fails_without_plaintext_fallback()
    {
        using var certificate = CreateCertificate();
        await using var server = new ScriptedSmtpServer(1, (client, _, record) => ScriptedSmtpServer.AcceptTlsMessageAsync(client, certificate, record));
        var answer = new ValidatedTlsaAnswer(true, [new TlsaRecord(3, 1, 1, new byte[32])]);
        var result = await CreateDaneTransport(server.Port, answer).SendAsync(Message());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPermanentFailure);
        Assert.DoesNotContain(server.Commands, command => command.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Unvalidated_dane_answer_is_ignored()
    {
        await using var server = new ScriptedSmtpServer(1, (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, 250, record));
        var answer = new ValidatedTlsaAnswer(false, [new TlsaRecord(3, 1, 1, new byte[32])]);
        var result = await CreateDaneTransport(server.Port, answer).SendAsync(Message());
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("STARTTLS", server.Commands);
    }

    [Fact]
    public async Task No_tlsa_keeps_opportunistic_tls_behavior()
    {
        await using var server = new ScriptedSmtpServer(1, (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, 250, record));
        var answer = new ValidatedTlsaAnswer(true, []);
        var result = await CreateDaneTransport(server.Port, answer).SendAsync(Message());
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("STARTTLS", server.Commands);
    }

    [Fact]
    public async Task Dane_mode_off_ignores_tlsa()
    {
        await using var server = new ScriptedSmtpServer(1, (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, 250, record));
        var result = await CreateDaneTransport(server.Port, new ValidatedTlsaAnswer(true, [new TlsaRecord(3, 1, 1, new byte[32])]), DaneMode.Off).SendAsync(Message());
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("STARTTLS", server.Commands);
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=mx.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("mx.test"); request.CertificateExtensions.Add(san.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx, "smtp-test"), "smtp-test", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
    }

    private static DirectSmtpMailTransport CreateDaneTransport(int port, ValidatedTlsaAnswer answer, DaneMode mode = DaneMode.Opportunistic) =>
        new(new FakeDns([new MxHost("mx.test", 0)], new Dictionary<string, IPAddress> { ["mx.test"] = IPAddress.Loopback }),
            new DirectSmtpOptions { EhloName = "sender.test", Port = port, DaneMode = mode, DaneResolver = new FixedDaneResolver(answer) }, new FixedPolicyProvider(null));

    [Fact]
    public async Task Mta_sts_enforce_rejects_mx_names_outside_the_published_policy()
    {
        var transport = new DirectSmtpMailTransport(new FakeDns([new MxHost("bad.mx.test", 0)]),
            new DirectSmtpOptions { EhloName = "sender.test", Port = 25 },
            new FixedPolicyProvider(new MtaStsPolicy(MtaStsMode.Enforce, ["good.mx.test"], TimeSpan.FromMinutes(5))));

        var result = await transport.SendAsync(Message());

        Assert.False(result.IsSuccess);
        Assert.Equal(451, result.ResponseCode);
        Assert.False(result.IsPermanentFailure);
    }

    [Fact]
    public async Task Mta_sts_testing_does_not_require_starttls()
    {
        await using var server = new ScriptedSmtpServer(1,
            (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, 250, record));
        var transport = Create(server.Port, [new MxHost("127.0.0.1", 0)],
            new MtaStsPolicy(MtaStsMode.Testing, ["127.0.0.1"], TimeSpan.FromMinutes(5)));

        var result = await transport.SendAsync(Message());

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(450, false)]
    [InlineData(550, true)]
    public async Task Recipient_reply_is_classified_by_smtp_reply_class(int rcptCode, bool permanent)
    {
        await using var server = new ScriptedSmtpServer(1,
            (client, _, record) => ScriptedSmtpServer.AcceptMessageAsync(client, offerStartTls: false, rcptCode, record));

        var result = await Create(server.Port, [new MxHost("127.0.0.1", 0)]).SendAsync(Message());

        Assert.False(result.IsSuccess);
        Assert.Equal(rcptCode, result.ResponseCode);
        Assert.Equal(permanent, result.IsPermanentFailure);
    }

    private static DirectSmtpMailTransport Create(int port, IReadOnlyList<MxHost> hosts, MtaStsPolicy? policy = null) =>
        new(new FakeDns(hosts), new DirectSmtpOptions
        {
            EhloName = "sender.test",
            ConnectTimeoutSeconds = 2,
            CommandTimeoutSeconds = 2,
            Port = port
        }, new FixedPolicyProvider(policy));

    private static OutboundMessage Message() => new(Guid.NewGuid(), "sender@sender.test", "user@recipient.test",
        "From: sender@sender.test\r\nSubject: test\r\n\r\nbody");

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task Cloudflare_http_errors_are_classified_for_retry(HttpStatusCode code, bool permanent)
    {
        var handler = new StaticResponseHandler(code);
        var transport = new CloudflareApiMailTransport(new SingleClientFactory(handler),
            new CloudflareEmailOptions { ApiToken = "test" });
        var result = await transport.SendAsync(Message() with { WorkerUrl = "https://worker.example.test" });

        Assert.False(result.IsSuccess);
        Assert.Equal((int)code, result.ResponseCode);
        Assert.Equal(permanent, result.IsPermanentFailure);
    }

    private sealed class FakeDns(
        IReadOnlyList<MxHost> hosts,
        IReadOnlyDictionary<string, IPAddress>? mxAddresses = null,
        IReadOnlyList<IPAddress>? fallbackAddresses = null) : IDnsResolver
    {
        private readonly List<string> _addressLookupOrder = [];
        public string[] AddressLookupOrder => [.. _addressLookupOrder];
        public Task<IReadOnlyList<MxHost>> GetMxAsync(string domain, CancellationToken ct = default) => Task.FromResult(hosts);
        public Task<IReadOnlyList<IPAddress>> GetAddressesAsync(string host, CancellationToken ct = default)
        {
            _addressLookupOrder.Add(host);
            if (IPAddress.TryParse(host, out var address)) return Task.FromResult<IReadOnlyList<IPAddress>>([address]);
            if (mxAddresses is not null && mxAddresses.TryGetValue(host, out address)) return Task.FromResult<IReadOnlyList<IPAddress>>([address]);
            return Task.FromResult(fallbackAddresses ?? (IReadOnlyList<IPAddress>)[]);
        }
        public Task<IReadOnlyList<string>> GetTxtAsync(string name, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class NoPolicyProvider : IMtaStsPolicyProvider
    {
        public Task<MtaStsPolicy?> GetPolicyAsync(string domain, CancellationToken ct = default) => Task.FromResult<MtaStsPolicy?>(null);
    }

    private sealed class FixedPolicyProvider(MtaStsPolicy? policy) : IMtaStsPolicyProvider
    {
        public Task<MtaStsPolicy?> GetPolicyAsync(string domain, CancellationToken ct = default) => Task.FromResult(policy);
    }

    private sealed class FixedDaneResolver(ValidatedTlsaAnswer answer) : IDaneResolver
    {
        public Task<ValidatedTlsaAnswer> GetTlsaAsync(string mxHost, CancellationToken ct = default) => Task.FromResult(answer);
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StaticResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent("{}") });
    }

    private sealed class ScriptedSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Any, 0);
        private readonly Task _loop;
        private readonly Func<NetworkStream, int, Action<string>, Task> _script;
        private int _accepted;
        private readonly List<string> _commands = [];
        private readonly List<IPAddress> _remoteAddresses = [];
        private readonly object _gate = new();

        public ScriptedSmtpServer(int expectedConnections, Func<NetworkStream, int, Action<string>, Task> script)
        {
            _script = script;
            _listener.Start();
            _loop = ServeAsync(expectedConnections);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public int AcceptedConnections => Volatile.Read(ref _accepted);
        public string[] Commands { get { lock (_gate) return [.. _commands]; } }
        public IPAddress[] RemoteAddresses { get { lock (_gate) return [.. _remoteAddresses]; } }

        private async Task ServeAsync(int expectedConnections)
        {
            for (int i = 0; i < expectedConnections; i++)
            {
                using var client = await _listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Interlocked.Increment(ref _accepted);
                if (client.Client.RemoteEndPoint is IPEndPoint remote) { lock (_gate) _remoteAddresses.Add(remote.Address); }
                await _script(client.GetStream(), i, value => { lock (_gate) _commands.Add(value); });
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _loop;
        }

        public static async Task AcceptMessageAsync(NetworkStream stream, bool offerStartTls, int rcptCode, Action<string> record)
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            using var writer = new StreamWriter(stream, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 mx.test ready");
            var ehlo = await reader.ReadLineAsync(); if (ehlo is not null) record(ehlo);
            if (offerStartTls)
            {
                await writer.WriteLineAsync("250-mx.test");
                await writer.WriteLineAsync("250-STARTTLS");
                await writer.WriteLineAsync("250 OK");
            }
            else await writer.WriteLineAsync("250 mx.test");
            var command = await reader.ReadLineAsync(); if (command is not null) record(command);
            if (command?.StartsWith("STARTTLS", StringComparison.OrdinalIgnoreCase) == true)
            {
                await writer.WriteLineAsync("454 TLS temporarily unavailable");
                command = await reader.ReadLineAsync(); if (command is not null) record(command);
            }
            await writer.WriteLineAsync("250 sender ok");
            command = await reader.ReadLineAsync(); if (command is not null) record(command);
            await writer.WriteLineAsync($"{rcptCode} recipient response");
            if (rcptCode >= 400) return;
            command = await reader.ReadLineAsync(); if (command is not null) record(command);
            await writer.WriteLineAsync("354 send data");
            while (await reader.ReadLineAsync() is { } line && line != ".") { }
            await writer.WriteLineAsync("250 queued");
            command = await reader.ReadLineAsync(); if (command is not null) record(command);
            await writer.WriteLineAsync("221 bye");
        }

        public static async Task AcceptTlsMessageAsync(NetworkStream stream, X509Certificate2 certificate, Action<string> record)
        {
            var reader = new StreamReader(stream, leaveOpen: true);
            var writer = new StreamWriter(stream, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 mx.test ready");
            record((await reader.ReadLineAsync())!);
            await writer.WriteLineAsync("250-mx.test");
            await writer.WriteLineAsync("250-STARTTLS");
            await writer.WriteLineAsync("250 OK");
            record((await reader.ReadLineAsync())!);
            await writer.WriteLineAsync("220 begin TLS");
            reader.Dispose(); writer.Dispose();

            using var tls = new SslStream(stream, leaveInnerStreamOpen: true);
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            });
            try
            {
                using var tlsReader = new StreamReader(tls, leaveOpen: true);
                using var tlsWriter = new StreamWriter(tls, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
                record((await tlsReader.ReadLineAsync())!);
                await tlsWriter.WriteLineAsync("250 mx.test secured");
                await ReceiveAndAcceptAsync(tlsReader, tlsWriter, record);
            }
            catch (IOException) { /* The client is expected to abort on a DANE certificate mismatch. */ }
        }

        private static async Task ReceiveAndAcceptAsync(StreamReader reader, StreamWriter writer, Action<string> record)
        {
            record((await reader.ReadLineAsync())!);
            await writer.WriteLineAsync("250 sender ok");
            var command = (await reader.ReadLineAsync())!; record(command);
            await writer.WriteLineAsync("250 recipient ok");
            command = (await reader.ReadLineAsync())!; record(command);
            await writer.WriteLineAsync("354 send data");
            while (await reader.ReadLineAsync() is { } line && line != ".") { }
            await writer.WriteLineAsync("250 queued");
            record((await reader.ReadLineAsync())!);
            await writer.WriteLineAsync("221 bye");
        }

        public static Task RejectGreetingAsync(NetworkStream stream) =>
            stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("421 temporary unavailable\r\n")).AsTask();
    }
}

