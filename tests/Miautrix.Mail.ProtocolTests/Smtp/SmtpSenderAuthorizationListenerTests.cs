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
    private sealed class CapturingQueue : ISmtpQueueManager
    {
        public List<SmtpQueueItem> Items { get; } = [];
        public Task<SmtpQueueItem> EnqueueAsync(Guid tenantId, string sender, string recipient, string rawMessage, QueueDirection direction, string? subject = null, CancellationToken cancellationToken = default)
        {
            var item = new SmtpQueueItem { Id = Guid.NewGuid(), TenantId = tenantId, Direction = direction, Sender = sender, Recipient = recipient, RawMessage = rawMessage, Status = "Pending" };
            Items.Add(item); return Task.FromResult(item);
        }
        public Task<SmtpDeliveryAttempt> RecordDeliveryAttemptAsync(Guid queueItemId, bool success, string? errorMessage = null, int? responseCode = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
            async Task<(string Mail, string Data, string? QueuedSender, string? QueuedMessage)> Send(string mailFrom, string from, string? sender = null)
            {
                using var client = new TcpClient();
                for (var i = 0; ; i++) { try { await client.ConnectAsync(IPAddress.Loopback, port); break; } catch when (i < 30) { await Task.Delay(50); } }
                var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
                try { await ssl.AuthenticateAsClientAsync("localhost"); } catch (Exception ex) { throw new InvalidOperationException(captureLogger.LastException?.ToString() ?? "No server exception logged.", ex); }
                Stream stream = ssl;
                var tlsReader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true); var tlsWriter = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
                async Task<string> TlsReply() { var first = await tlsReader.ReadLineAsync() ?? ""; while (first.Length > 3 && first[3] == '-') first = await tlsReader.ReadLineAsync() ?? ""; return first; }
                _ = await TlsReply();
                await tlsWriter.WriteLineAsync("EHLO localhost"); _ = await TlsReply();
                await tlsWriter.WriteLineAsync("AUTH LOGIN"); _ = await TlsReply(); await tlsWriter.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes("delegate@sender-test.invalid"))); _ = await TlsReply(); await tlsWriter.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes("test-password"))); _ = await TlsReply();
                await tlsWriter.WriteLineAsync($"MAIL FROM:<{mailFrom}>"); var mailReply = await TlsReply();
                if (!mailReply.StartsWith("250")) return (mailReply, "", queue.Items.LastOrDefault()?.Sender, queue.Items.LastOrDefault()?.RawMessage);
                await tlsWriter.WriteLineAsync("RCPT TO:<dest@outside.invalid>"); _ = await TlsReply(); await tlsWriter.WriteLineAsync("DATA"); _ = await TlsReply();
                await tlsWriter.WriteLineAsync($"From: {from}"); if (sender is not null) await tlsWriter.WriteLineAsync($"Sender: {sender}"); await tlsWriter.WriteLineAsync("Subject: test"); await tlsWriter.WriteLineAsync(""); await tlsWriter.WriteLineAsync("body"); await tlsWriter.WriteLineAsync("."); var dataReply = await TlsReply();
                var item = queue.Items.LastOrDefault(); return (mailReply, dataReply, item?.Sender, item?.RawMessage);
            }

            var ownResult = await Send(own, own); Assert.StartsWith("250", ownResult.Mail); Assert.StartsWith("250", ownResult.Data); Assert.Equal(own, ownResult.QueuedSender);
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
}
