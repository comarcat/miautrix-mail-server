using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Smtp;
using Miautrix.Mail.Queue;
using Miautrix.Mail.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Miautrix.Mail.ProtocolTests.Smtp;

public sealed class SmtpSenderAuthorizationListenerTests
{
    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public Exception? LastException { get; private set; }
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
        public void Dispose() { }
        private sealed class CaptureLogger(CaptureLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { if (exception is not null) owner.LastException = exception; }
        }
    }
    private sealed class TestAuth(Guid tenant, Guid user) : ISmtpAuthenticator
    {
        public bool Authenticate(string username, string password, bool isTlsEncrypted, out Guid tenantId, out Guid userId)
        { tenantId = tenant; userId = user; return isTlsEncrypted && username == "delegate@sender-test.invalid" && password == "test-password"; }
    }
    private sealed class TestDomains(Guid tenant) : ISmtpDomainValidator
    { public bool IsDomainLocal(string address, out Guid tenantId) { tenantId = tenant; return false; } }
    private sealed class MappedDomains(IReadOnlyDictionary<string, Guid> tenants) : ISmtpDomainValidator
    {
        public bool IsDomainLocal(string address, out Guid tenantId) => tenants.TryGetValue(address, out tenantId);
    }
    private sealed class UnusedSubmissionHandler : ISmtpSubmissionHandler
    {
        public SmtpResponse HandleAuth(string username, string password, bool isTlsEncrypted, out Guid? tenantId, out Guid? userId) { tenantId = null; userId = null; return SmtpResponse.AuthenticationRequired; }
        public Task<bool> IsEnvelopeSenderAllowedAsync(Guid tenantId, Guid userId, string sender, CancellationToken ct = default) => Task.FromResult(false);
        public Task<(SmtpResponse Response, SmtpQueueItem? QueueItem)> HandleSubmissionAsync(bool isAuthenticated, bool isTlsEncrypted, Guid? tenantId, Guid? userId, string sender, string recipient, string rawMessage, string? subject = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class CapturingQueue : ISmtpQueueManager
    {
        public List<SmtpQueueItem> Items { get; } = [];
        public Task<SmtpQueueItem> EnqueueAsync(Guid tenantId, string sender, string recipient, string rawMessage, QueueDirection direction, string? subject = null, CancellationToken cancellationToken = default)
        {
            var item = new SmtpQueueItem { Id = Guid.NewGuid(), TenantId = tenantId, Direction = direction, Sender = sender, Recipient = recipient, RawMessage = rawMessage, Status = "Pending" };
            Items.Add(item); return Task.FromResult(item);
        }
        public Task<SmtpDeliveryAttempt> RecordDeliveryAttemptAsync(Guid queueItemId, bool success, string? errorMessage = null, int? responseCode = null, CancellationToken cancellationToken = default, bool permanentFailure = false) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Submission_over_starttls_enforces_all_sender_cases_and_queues_authorized_content()
    {
        var dbString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION") ?? throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION required.");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(dbString).Options;
        var tenantId = Guid.NewGuid(); var foreignTenantId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var own = "delegate@sender-test.invalid"; var ownAlias = "d@sender-test.invalid"; var sharedAddress = "sales@sender-test.invalid";
        var domainId = Guid.NewGuid(); var foreignDomainId = Guid.NewGuid(); var ownMailboxId = Guid.NewGuid(); var sharedMailboxId = Guid.NewGuid();
        var grant = new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, MailboxId = sharedMailboxId, AccessLevel = "read" };
        await using (var db = new AppDbContext(options))
        {
            db.Tenants.AddRange(new Tenant { Id = tenantId, Slug = "smtp-" + Guid.NewGuid().ToString("N") }, new Tenant { Id = foreignTenantId, Slug = "smtp-foreign-" + Guid.NewGuid().ToString("N") });
            db.Domains.AddRange(new Miautrix.Mail.Domain.Domain { Id = domainId, TenantId = tenantId, Name = "sender-test.invalid" }, new Miautrix.Mail.Domain.Domain { Id = foreignDomainId, TenantId = foreignTenantId, Name = "other-test.invalid" });
            db.Users.Add(new User { Id = userId, TenantId = tenantId, Email = own, Name = "Delegate" });
            db.Mailboxes.AddRange(new Mailbox { Id = ownMailboxId, TenantId = tenantId, DomainId = domainId, Address = own, Name = "Delegate" }, new Mailbox { Id = sharedMailboxId, TenantId = tenantId, DomainId = domainId, Address = sharedAddress, Name = "Sales", Kind = "shared" }, new Mailbox { Id = Guid.NewGuid(), TenantId = foreignTenantId, DomainId = foreignDomainId, Address = "other@other-test.invalid", Name = "Foreign", Kind = "shared" });
            db.Aliases.Add(new Alias { Id = Guid.NewGuid(), TenantId = tenantId, Address = ownAlias, TargetAddress = own });
            db.MailboxDelegates.Add(grant);
            await db.SaveChangesAsync();
        }

        var queue = new CapturingQueue();
        using var certificateKey = RSA.Create(2048);
        var certReq = new CertificateRequest("CN=localhost", certificateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generatedCertificate = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generatedCertificate.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        var captureLogger = new CaptureLoggerProvider();
        using var provider = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(captureLogger))
            .AddScoped(_ => new AppDbContext(options))
            .AddScoped<ISmtpAuthenticator>(_ => new TestAuth(tenantId, userId))
            .AddScoped<ISmtpQueueManager>(_ => queue)
            .AddScoped<ISenderAuthorizationService, SenderAuthorizationService>()
            .AddScoped<ISmtpSubmissionHandler, SmtpSubmissionHandler>()
            .AddScoped<ISmtpDomainValidator>(_ => new TestDomains(tenantId))
            .AddScoped<ISmtpInboundHandler, SmtpInboundHandler>()
            .BuildServiceProvider();
        var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start(); var port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
        var listener = new SmtpListenerService(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<ILogger<SmtpListenerService>>(), port, SmtpListenerMode.SubmissionImplicitTls, certificate, "localhost");
        await listener.StartAsync(CancellationToken.None);
        try
        {
            async Task<(string Mail, string Data, string? QueuedSender, string? QueuedMessage)> Send(string mailFrom, string from, string? sender = null, int recipientCount = 1)
            {
                using var client = new TcpClient();
                for (var i = 0; ; i++) { try { await client.ConnectAsync(IPAddress.Loopback, port); break; } catch when (i < 30) { await Task.Delay(50); } }
                var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
                try { await ssl.AuthenticateAsClientAsync("localhost"); } catch (Exception ex) { throw new InvalidOperationException(captureLogger.LastException?.ToString() ?? "No server exception logged.", ex); }
                Stream stream = ssl;
                var tlsReader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true); var tlsWriter = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
                async Task<string> TlsReply() { var first = await tlsReader.ReadLineAsync() ?? ""; while (first.Length > 3 && first[3] == '-') first = await tlsReader.ReadLineAsync() ?? ""; return first; }
                async Task<List<string>> TlsReplyLines() { var lines = new List<string>(); var line = await tlsReader.ReadLineAsync() ?? ""; lines.Add(line); while (line.Length > 3 && line[3] == '-') { line = await tlsReader.ReadLineAsync() ?? ""; lines.Add(line); } return lines; }
                _ = await TlsReply();
                await tlsWriter.WriteLineAsync("EHLO localhost"); Assert.Equal(new[] { "250-localhost", "250-AUTH LOGIN PLAIN", "250-SIZE 26214400", "250 2.0.0 OK" }, await TlsReplyLines());
                await tlsWriter.WriteLineAsync("AUTH LOGIN"); _ = await TlsReply(); await tlsWriter.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes("delegate@sender-test.invalid"))); _ = await TlsReply(); await tlsWriter.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes("test-password"))); _ = await TlsReply();
                await tlsWriter.WriteLineAsync($"MAIL FROM:<{mailFrom}>"); var mailReply = await TlsReply();
                if (!mailReply.StartsWith("250")) return (mailReply, "", queue.Items.LastOrDefault()?.Sender, queue.Items.LastOrDefault()?.RawMessage);
                for (var recipientIndex = 0; recipientIndex < recipientCount; recipientIndex++)
                {
                    await tlsWriter.WriteLineAsync($"RCPT TO:<dest{recipientIndex}@outside.invalid>"); _ = await TlsReply();
                }
                await tlsWriter.WriteLineAsync("DATA"); _ = await TlsReply();
                await tlsWriter.WriteLineAsync($"From: {from}"); if (sender is not null) await tlsWriter.WriteLineAsync($"Sender: {sender}"); await tlsWriter.WriteLineAsync("Subject: test"); await tlsWriter.WriteLineAsync(""); await tlsWriter.WriteLineAsync("body"); await tlsWriter.WriteLineAsync("."); var dataReply = await TlsReply();
                var item = queue.Items.LastOrDefault(); return (mailReply, dataReply, item?.Sender, item?.RawMessage);
            }

            var beforeMulti = queue.Items.Count;
            var ownResult = await Send(own, own, recipientCount: 3); Assert.StartsWith("250", ownResult.Mail); Assert.StartsWith("250", ownResult.Data); Assert.Equal(own, ownResult.QueuedSender);
            Assert.Contains("Date:", ownResult.QueuedMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Message-ID:", ownResult.QueuedMessage, StringComparison.OrdinalIgnoreCase);
            var multiItems = queue.Items.Skip(beforeMulti).ToArray();
            Assert.Equal(3, multiItems.Length);
            Assert.Equal(new[] { "dest0@outside.invalid", "dest1@outside.invalid", "dest2@outside.invalid" }, multiItems.Select(item => item.Recipient));
            Assert.All(multiItems, item => Assert.Equal(multiItems[0].RawMessage, item.RawMessage));
            var alias = await Send(ownAlias, ownAlias); Assert.StartsWith("250", alias.Data); Assert.Equal(ownAlias, alias.QueuedSender);
            grant.CanSendAs = true; await using (var db = new AppDbContext(options)) { db.MailboxDelegates.Update(grant); await db.SaveChangesAsync(); }
            var asResult = await Send(own, sharedAddress); Assert.StartsWith("250", asResult.Data); Assert.Equal(own, asResult.QueuedSender); Assert.DoesNotContain("Sender:", asResult.QueuedMessage, StringComparison.OrdinalIgnoreCase);
            grant.CanSendAs = false; grant.CanSendOnBehalf = true; await using (var db = new AppDbContext(options)) { db.MailboxDelegates.Update(grant); await db.SaveChangesAsync(); }
            var behalf = await Send(own, sharedAddress, "forged@sender-test.invalid"); Assert.StartsWith("250", behalf.Data); Assert.Equal(own, behalf.QueuedSender); Assert.Contains("Sender: delegate@sender-test.invalid", behalf.QueuedMessage, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("forged@", behalf.QueuedMessage);
            grant.CanSendOnBehalf = false; grant.AccessLevel = "write"; await using (var db = new AppDbContext(options)) { db.MailboxDelegates.Update(grant); await db.SaveChangesAsync(); }
            var rejectedCount = queue.Items.Count; var fullAccess = await Send(own, sharedAddress); Assert.StartsWith("550 5.7.1", fullAccess.Data); Assert.Equal(rejectedCount, queue.Items.Count);
            await using (var db = new AppDbContext(options)) { db.MailboxDelegates.RemoveRange(db.MailboxDelegates.Where(d => d.MailboxId == sharedMailboxId)); await db.SaveChangesAsync(); }
            rejectedCount = queue.Items.Count; var noGrant = await Send(own, sharedAddress); Assert.StartsWith("550 5.7.1", noGrant.Data); Assert.Equal(rejectedCount, queue.Items.Count);
            rejectedCount = queue.Items.Count; var foreign = await Send("other@other-test.invalid", "other@other-test.invalid"); var unknown = await Send("unknown@other-test.invalid", "unknown@other-test.invalid"); Assert.Equal(unknown.Mail, foreign.Mail); Assert.StartsWith("550 5.7.1", foreign.Mail); Assert.Equal(rejectedCount, queue.Items.Count);
            rejectedCount = queue.Items.Count; var external = await Send("outside@external.invalid", "outside@external.invalid"); Assert.StartsWith("550 5.7.1", external.Mail); Assert.Equal(rejectedCount, queue.Items.Count);
            rejectedCount = queue.Items.Count; var nullSender = await Send("", own); Assert.StartsWith("550 5.7.1", nullSender.Mail); Assert.Equal(rejectedCount, queue.Items.Count);
            rejectedCount = queue.Items.Count; var forged = await Send(own, "other@sender-test.invalid"); Assert.StartsWith("550 5.7.1", forged.Data); Assert.Equal(rejectedCount, queue.Items.Count);
        }
        finally
        {
            await listener.StopAsync(CancellationToken.None);
            await using var cleanup = new AppDbContext(options);
            cleanup.AuditLogs.RemoveRange(cleanup.AuditLogs.Where(a => a.TenantId == tenantId));
            cleanup.MailboxDelegates.RemoveRange(cleanup.MailboxDelegates.Where(d => d.TenantId == tenantId));
            cleanup.Aliases.RemoveRange(cleanup.Aliases.Where(a => a.TenantId == tenantId));
            cleanup.Mailboxes.RemoveRange(cleanup.Mailboxes.Where(m => m.TenantId == tenantId || m.TenantId == foreignTenantId));
            cleanup.Users.RemoveRange(cleanup.Users.Where(u => u.TenantId == tenantId));
            cleanup.Domains.RemoveRange(cleanup.Domains.Where(d => d.TenantId == tenantId || d.TenantId == foreignTenantId));
            cleanup.Tenants.RemoveRange(cleanup.Tenants.Where(t => t.Id == tenantId || t.Id == foreignTenantId));
            await cleanup.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Inbound_multi_recipient_queues_each_message_in_its_resolved_tenant()
    {
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
        var queue = new CapturingQueue();
        var domains = new MappedDomains(new Dictionary<string, Guid>
        {
            ["alice@tenant-a.invalid"] = tenantA,
            ["bob@tenant-b.invalid"] = tenantB
        });
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddScoped<ISmtpQueueManager>(_ => queue)
            .AddScoped<ISmtpDomainValidator>(_ => domains)
            .AddScoped<ISmtpInboundHandler, SmtpInboundHandler>()
            .AddScoped<ISmtpSubmissionHandler, UnusedSubmissionHandler>()
            .BuildServiceProvider();
        var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start(); var port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
        var listener = new SmtpListenerService(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<ILogger<SmtpListenerService>>(), port, SmtpListenerMode.Inbound, null, "localhost");
        await listener.StartAsync(CancellationToken.None);
        try
        {
            using var client = new TcpClient();
            for (var i = 0; ; i++) { try { await client.ConnectAsync(IPAddress.Loopback, port); break; } catch when (i < 30) { await Task.Delay(50); } }
            using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, leaveOpen: true);
            using var writer = new StreamWriter(client.GetStream(), Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
            async Task<string> Reply() { var first = await reader.ReadLineAsync() ?? ""; while (first.Length > 3 && first[3] == '-') first = await reader.ReadLineAsync() ?? ""; return first; }
            _ = await Reply();
            await writer.WriteLineAsync("EHLO client"); _ = await Reply();
            await writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("250", await Reply());
            await writer.WriteLineAsync("RCPT TO:<alice@tenant-a.invalid>"); Assert.StartsWith("250", await Reply());
            await writer.WriteLineAsync("RCPT TO:<bob@tenant-b.invalid>"); Assert.StartsWith("250", await Reply());
            await writer.WriteLineAsync("DATA"); Assert.StartsWith("354", await Reply());
            await writer.WriteLineAsync("Subject: shared raw bytes"); await writer.WriteLineAsync(""); await writer.WriteLineAsync("..leading-dot"); await writer.WriteLineAsync(".");
            Assert.StartsWith("250", await Reply());
            Assert.Equal(2, queue.Items.Count);
            Assert.Equal(new[] { tenantA, tenantB }, queue.Items.Select(item => item.TenantId));
            Assert.Equal(new[] { "alice@tenant-a.invalid", "bob@tenant-b.invalid" }, queue.Items.Select(item => item.Recipient));
            Assert.Equal(queue.Items[0].RawMessage, queue.Items[1].RawMessage);
            Assert.Contains(".leading-dot", queue.Items[0].RawMessage);
            Assert.DoesNotContain("..leading-dot", queue.Items[0].RawMessage);
            await writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("250", await Reply());
            await writer.WriteLineAsync("RCPT TO:<alice@tenant-a.invalid>"); Assert.StartsWith("250", await Reply());
            await writer.WriteLineAsync("DATA"); Assert.StartsWith("354", await Reply());
            await writer.WriteLineAsync("Subject: second transaction"); await writer.WriteLineAsync(""); await writer.WriteLineAsync("second"); await writer.WriteLineAsync(".");
            Assert.StartsWith("250", await Reply());
            Assert.Equal(3, queue.Items.Count);
        }
        finally { await listener.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task Listener_enforces_line_size_message_size_and_timeouts_without_queueing_truncated_data()
    {
        var tenant = Guid.NewGuid();
        var queue = new CapturingQueue();
        var domains = new MappedDomains(new Dictionary<string, Guid> { ["local@limits.invalid"] = tenant });
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddScoped<ISmtpQueueManager>(_ => queue)
            .AddScoped<ISmtpDomainValidator>(_ => domains)
            .AddScoped<ISmtpInboundHandler, SmtpInboundHandler>()
            .AddScoped<ISmtpSubmissionHandler, UnusedSubmissionHandler>()
            .BuildServiceProvider();
        var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start(); var port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
        var smtpOptions = new SmtpListenerOptions(32, 2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        var listener = new SmtpListenerService(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<ILogger<SmtpListenerService>>(), port, SmtpListenerMode.Inbound, null, "localhost", smtpOptions);
        await listener.StartAsync(CancellationToken.None);
        async Task<(TcpClient Client, StreamReader Reader, StreamWriter Writer)> Connect()
        {
            var client = new TcpClient();
            for (var i = 0; ; i++) { try { await client.ConnectAsync(IPAddress.Loopback, port); break; } catch when (i < 30) { await Task.Delay(50); } }
            var reader = new StreamReader(client.GetStream(), Encoding.ASCII, leaveOpen: true);
            var writer = new StreamWriter(client.GetStream(), Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
            return (client, reader, writer);
        }
        async Task Greet((TcpClient Client, StreamReader Reader, StreamWriter Writer) session)
        {
            await session.Writer.WriteLineAsync("EHLO test");
            var line = await session.Reader.ReadLineAsync();
            while (line is { Length: > 3 } && line[3] == '-') line = await session.Reader.ReadLineAsync();
        }
        try
        {
            var session = await Connect();
            Assert.StartsWith("220", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); Assert.StartsWith("503 5.5.1", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("DATA"); Assert.StartsWith("503 5.5.1", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("503 5.5.1", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync(new string('X', 511)); // 513 octets including CRLF
            Assert.StartsWith("500 5.5.2", await session.Reader.ReadLineAsync());
            await Greet(session);
            await session.Writer.WriteLineAsync("VRFY someone"); Assert.Equal("252 2.1.5 Cannot verify user; will accept message and attempt delivery", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("EXPN list"); Assert.Equal("252 2.1.5 Cannot verify user; will accept message and attempt delivery", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("HELP"); Assert.StartsWith("214 ", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("UNKNOWN"); Assert.StartsWith("500 5.5.2", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("NOOP"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid> SIZE=33"); Assert.StartsWith("552 5.3.4", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("NOOP"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("DATA"); Assert.StartsWith("503 5.5.1", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("503 5.5.1", await session.Reader.ReadLineAsync());
            await Greet(session); // A new EHLO resets the current transaction.
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); Assert.StartsWith("503 5.5.1", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("503 5.5.1", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("RSET"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); Assert.StartsWith("503 5.5.1", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); Assert.StartsWith("452 4.5.3", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("DATA"); Assert.StartsWith("354", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync(new string('A', 999)); // 1001 octets including CRLF
            await session.Writer.WriteLineAsync(".");
            Assert.StartsWith("552 5.3.4", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("NOOP"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            Assert.Empty(queue.Items);

            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("DATA"); Assert.StartsWith("354", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("123456789012345678");
            await session.Writer.WriteLineAsync("1234567890123");
            await session.Writer.WriteLineAsync(".");
            Assert.StartsWith("552 5.3.4", await session.Reader.ReadLineAsync());
            await session.Writer.WriteLineAsync("NOOP"); Assert.StartsWith("250", await session.Reader.ReadLineAsync());
            Assert.Empty(queue.Items);
            session.Client.Dispose();

            session = await Connect();
            Assert.StartsWith("220", await session.Reader.ReadLineAsync());
            await Greet(session);
            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); _ = await session.Reader.ReadLineAsync();
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); _ = await session.Reader.ReadLineAsync();
            await session.Writer.WriteLineAsync("DATA"); _ = await session.Reader.ReadLineAsync();
            session.Client.Dispose(); // EOF mid-DATA must never queue the partial message.
            await Task.Delay(100);
            Assert.Empty(queue.Items);

            session = await Connect();
            Assert.StartsWith("220", await session.Reader.ReadLineAsync());
            await Greet(session);
            await session.Writer.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); _ = await session.Reader.ReadLineAsync();
            await session.Writer.WriteLineAsync("RCPT TO:<local@limits.invalid>"); _ = await session.Reader.ReadLineAsync();
            await session.Writer.WriteLineAsync("DATA"); _ = await session.Reader.ReadLineAsync();
            Assert.StartsWith("421 4.4.2 DATA timeout", await session.Reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3)));
            session.Client.Dispose();

            session = await Connect();
            Assert.StartsWith("220", await session.Reader.ReadLineAsync());
            Assert.StartsWith("421 4.4.2 Idle timeout", await session.Reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3)));
            session.Client.Dispose();

            session = await Connect();
            Assert.StartsWith("220", await session.Reader.ReadLineAsync());
            await Greet(session);
            await session.Writer.WriteLineAsync("HELO legacy-client"); Assert.Equal("250 2.0.0 localhost", await session.Reader.ReadLineAsync());
            await session.Writer.WriteAsync("NOOP\r\nHELP\r\nQUIT\r\n");
            Assert.StartsWith("250 2.0.0", await session.Reader.ReadLineAsync());
            Assert.StartsWith("214 2.0.0", await session.Reader.ReadLineAsync());
            Assert.Equal("221 2.0.0 Bye", await session.Reader.ReadLineAsync());
            session.Client.Dispose();
        }
        finally { await listener.StopAsync(CancellationToken.None); }
    }

    [Theory]
    [InlineData(SmtpListenerMode.Inbound)]
    [InlineData(SmtpListenerMode.SubmissionStartTls)]
    public async Task Starttls_uses_tls12_or_newer_and_resets_the_greeting_state(SmtpListenerMode mode)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        var tenant = Guid.NewGuid();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddScoped<ISmtpQueueManager>(_ => new CapturingQueue())
            .AddScoped<ISmtpDomainValidator>(_ => new MappedDomains(new Dictionary<string, Guid> { ["local@tls.invalid"] = tenant }))
            .AddScoped<ISmtpInboundHandler, SmtpInboundHandler>()
            .AddScoped<ISmtpSubmissionHandler, UnusedSubmissionHandler>()
            .BuildServiceProvider();
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        var listener = new SmtpListenerService(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<ILogger<SmtpListenerService>>(), port, mode, certificate, "localhost");
        await listener.StartAsync(CancellationToken.None);
        try
        {
            using var client = new TcpClient();
            for (var i = 0; ; i++) { try { await client.ConnectAsync(IPAddress.Loopback, port); break; } catch when (i < 30) { await Task.Delay(50); } }
            using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, leaveOpen: true);
            using var writer = new StreamWriter(client.GetStream(), Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
            async Task<string> Reply() { var first = await reader.ReadLineAsync() ?? ""; while (first.Length > 3 && first[3] == '-') first = await reader.ReadLineAsync() ?? ""; return first; }
            _ = await Reply();
            await writer.WriteLineAsync("EHLO client");
            var ehloLines = new List<string>();
            var ehloLine = await reader.ReadLineAsync() ?? ""; ehloLines.Add(ehloLine);
            while (ehloLine.Length > 3 && ehloLine[3] == '-') { ehloLine = await reader.ReadLineAsync() ?? ""; ehloLines.Add(ehloLine); }
            Assert.Equal(new[] { "250-localhost", "250-STARTTLS", "250-SIZE 26214400", "250 2.0.0 OK" }, ehloLines);
            await writer.WriteLineAsync("AUTH LOGIN"); Assert.StartsWith("538 5.7.11", await reader.ReadLineAsync());
            await writer.WriteLineAsync("STARTTLS"); Assert.StartsWith("220", await reader.ReadLineAsync());
            var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost", EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13 });
            using var tlsReader = new StreamReader(ssl, Encoding.ASCII, leaveOpen: true);
            using var tlsWriter = new StreamWriter(ssl, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
            await tlsWriter.WriteLineAsync("MAIL FROM:<sender@remote.invalid>"); Assert.StartsWith("503 5.5.1", await tlsReader.ReadLineAsync());
            await tlsWriter.WriteLineAsync("EHLO client");
            var postTlsLines = new List<string>();
            var final = await tlsReader.ReadLineAsync() ?? ""; postTlsLines.Add(final);
            while (final.Length > 3 && final[3] == '-') { final = await tlsReader.ReadLineAsync() ?? ""; postTlsLines.Add(final); }
            var expectedPostTls = mode == SmtpListenerMode.SubmissionStartTls
                ? new[] { "250-localhost", "250-AUTH LOGIN PLAIN", "250-SIZE 26214400", "250 2.0.0 OK" }
                : new[] { "250-localhost", "250-SIZE 26214400", "250 2.0.0 OK" };
            Assert.Equal(expectedPostTls, postTlsLines);
            if (mode == SmtpListenerMode.SubmissionStartTls)
            {
                await tlsWriter.WriteLineAsync("AUTH PLAIN"); Assert.StartsWith("334", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("*"); Assert.StartsWith("501 5.7.0", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("AUTH PLAIN invalid!"); Assert.StartsWith("501 5.5.4", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("AUTH PLAIN " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user\0password")));
                Assert.StartsWith("501 5.5.4", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("AUTH PLAIN " + Convert.ToBase64String(Encoding.UTF8.GetBytes("\0user\0password")));
                Assert.StartsWith("530 5.7.0", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("AUTH LOGIN"); Assert.StartsWith("334", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("*"); Assert.StartsWith("501 5.7.0", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("AUTH LOGIN"); Assert.StartsWith("334", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("%%% "); Assert.StartsWith("501 5.5.4", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("AUTH LOGIN"); Assert.StartsWith("334", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes("user"))); Assert.StartsWith("334", await tlsReader.ReadLineAsync());
                await tlsWriter.WriteLineAsync("%%% "); Assert.StartsWith("501 5.5.4", await tlsReader.ReadLineAsync());
            }
            await tlsWriter.WriteLineAsync("MAIL FROM:<sender@remote.invalid>");
            var postTlsMail = await tlsReader.ReadLineAsync();
            Assert.StartsWith(mode == SmtpListenerMode.Inbound ? "250" : "550", postTlsMail);

            foreach (var legacyProtocol in new[] { (System.Security.Authentication.SslProtocols)192, (System.Security.Authentication.SslProtocols)768 })
            {
                using var legacyClient = new TcpClient();
                for (var i = 0; ; i++) { try { await legacyClient.ConnectAsync(IPAddress.Loopback, port); break; } catch when (i < 30) { await Task.Delay(50); } }
                using var legacyReader = new StreamReader(legacyClient.GetStream(), Encoding.ASCII, leaveOpen: true);
                using var legacyWriter = new StreamWriter(legacyClient.GetStream(), Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
                _ = await legacyReader.ReadLineAsync(); await legacyWriter.WriteLineAsync("EHLO old-client");
                var capability = await legacyReader.ReadLineAsync() ?? "";
                while (capability.Length > 3 && capability[3] == '-') capability = await legacyReader.ReadLineAsync() ?? "";
                await legacyWriter.WriteLineAsync("STARTTLS"); _ = await legacyReader.ReadLineAsync();
                using var legacySsl = new SslStream(legacyClient.GetStream(), false, (_, _, _, _) => true);
                var legacyRejected = false;
                try { await legacySsl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost", EnabledSslProtocols = legacyProtocol }); }
                catch (Exception) { legacyRejected = true; }
                Assert.True(legacyRejected, $"Legacy TLS protocol {legacyProtocol} must not negotiate with the SMTP listener.");
            }
        }
        finally { await listener.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public void Submission_header_completion_preserves_existing_headers_byte_for_byte()
    {
        const string existing = "From: sender@example.invalid\r\nDate: Tue, 24 Nov 2026 12:00:00 +0000\r\nMessage-ID: <existing@example.invalid>\r\n\r\nbody\r\n";
        Assert.Same(existing, SmtpSubmissionHandler.EnsureSubmissionHeaders(existing));
        var completed = SmtpSubmissionHandler.EnsureSubmissionHeaders("From: sender@example.invalid\r\n\r\nbody\r\n");
        Assert.Contains("Date: ", completed, StringComparison.Ordinal);
        Assert.Contains("Message-ID: <", completed, StringComparison.Ordinal);
        Assert.EndsWith("\r\n\r\nbody\r\n", completed, StringComparison.Ordinal);
    }
}
