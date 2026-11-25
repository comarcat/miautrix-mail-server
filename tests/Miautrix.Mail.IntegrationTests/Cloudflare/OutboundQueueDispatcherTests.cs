using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Queue;
using Miautrix.Mail.Protocols.Smtp;
using Miautrix.Mail.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.IntegrationTests.Cloudflare;

/// <summary>
/// The dispatcher is the piece that was missing entirely: queue rows were being written and never
/// read. These tests drive one dispatch pass against the real database and assert that a
/// Cloudflare-mode recipient reaches a transport while local-mode mail is left exactly as it was.
/// </summary>
[Trait("Category", "Cloudflare")]
public sealed class OutboundQueueDispatcherTests
{
    private const string CloudflareDomain = "dispatch-cf.test";
    private const string LocalDomain = "dispatch-local.test";
    private const string DirectDomain = "dispatch-direct.test";

    // ------------------------------------------------------------------
    // Routing
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_cloudflare_mode_recipient_is_handed_to_the_cloudflare_transport_and_the_attempt_is_recorded()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            await SeedDomainAsync(tenantId, CloudflareDomain, "cloudflare", "https://worker.example.workers.dev");
            var queueItemId = await SeedQueueItemAsync(tenantId, $"user@{CloudflareDomain}", $"sender@{CloudflareDomain}");

            var transport = new RecordingTransport();
            await RunOnePassAsync(transport);

            var sent = Assert.Single(transport.Sent);
            Assert.Equal($"user@{CloudflareDomain}", sent.Recipient);
            Assert.Equal("https://worker.example.workers.dev", sent.WorkerUrl);
            Assert.Contains("Subject:", sent.RawMessage);

            await using var context = CreateContext();
            var item = await context.SmtpQueue.AsNoTracking().FirstAsync(q => q.Id == queueItemId);

            Assert.Equal("Delivered", item.Status);
            Assert.Equal(1, item.Attempts);

            var attempt = await context.SmtpDeliveryAttempts.AsNoTracking()
                .FirstAsync(a => a.QueueItemId == queueItemId);
            Assert.True(attempt.Success);
        }
        finally
        {
            await CleanupAsync(tenantId);
        }
    }

    [Fact]
    public async Task A_direct_sending_domain_is_handed_to_the_direct_transport()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            await SeedDomainAsync(tenantId, DirectDomain, "direct", workerUrl: null);
            var queueItemId = await SeedQueueItemAsync(tenantId, "recipient@outside.test", $"sender@{DirectDomain}");
            var transport = new RecordingTransport(DomainTransportModes.Direct);

            await RunOnePassAsync(transport);

            Assert.Single(transport.Sent);
            await using var context = CreateContext();
            var item = await context.SmtpQueue.AsNoTracking().SingleAsync(q => q.Id == queueItemId);
            Assert.Equal("Delivered", item.Status);
        }
        finally { await CleanupAsync(tenantId); }
    }

    [Theory]
    [InlineData("vacation", "cloudflare")]
    [InlineData("reject", "cloudflare")]
    [InlineData("redirect", "cloudflare")]
    [InlineData("vacation", "direct")]
    [InlineData("reject", "direct")]
    [InlineData("redirect", "direct")]
    public async Task Sieve_system_mail_uses_mailbox_domain_transport_and_mode_specific_envelope(string kind, string mode)
    {
        var tenantId = await SeedTenantAsync();
        var domain = mode == "direct" ? DirectDomain : CloudflareDomain;
        var mailboxAddress = $"mailbox@{domain}";
        try
        {
            await SeedDomainAsync(tenantId, domain, mode, mode == "cloudflare" ? "https://worker.example.workers.dev" : null, isPrimary: true);
            var sender = kind == "redirect" ? "original@remote.test" : string.Empty;
            var from = kind == "reject" ? $"From: Mail Delivery Subsystem <MAILER-DAEMON@{domain}>\r\n" :
                kind == "redirect" ? "From: original@remote.test\r\n" : $"From: {mailboxAddress}\r\n";
            var resent = kind == "redirect" ? $"Resent-From: {mailboxAddress}\r\nResent-To: recipient@remote.test\r\n" : string.Empty;
            var raw = $"{from}{resent}Subject: system message\r\n\r\nBody\r\n";
            var queueItemId = await SeedSystemQueueItemAsync(tenantId, "recipient@remote.test", sender, raw, mailboxAddress);
            var transport = new RecordingTransport(mode == "direct" ? DomainTransportModes.Direct : DomainTransportModes.Cloudflare);

            await RunOnePassAsync(transport);

            var sent = Assert.Single(transport.Sent);
            Assert.Equal(mode == "cloudflare" ? mailboxAddress : sender, sent.Sender);
            if (kind == "redirect")
            {
                Assert.Contains("From: original@remote.test", sent.RawMessage);
                Assert.Contains($"Resent-From: {mailboxAddress}", sent.RawMessage);
                Assert.Contains("Resent-To: recipient@remote.test", sent.RawMessage);
            }
            await using var verify = CreateContext();
            Assert.Equal("Delivered", (await verify.SmtpQueue.SingleAsync(q => q.Id == queueItemId)).Status);
        }
        finally { await CleanupAsync(tenantId); }
    }

    [Fact]
    public async Task A_failed_send_is_recorded_so_the_row_retries_instead_of_looping_forever()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            await SeedDomainAsync(tenantId, CloudflareDomain, "cloudflare", "https://worker.example.workers.dev");
            var queueItemId = await SeedQueueItemAsync(tenantId, $"user@{CloudflareDomain}", $"sender@{CloudflareDomain}");

            var transport = new RecordingTransport
            {
                Result = TransportSendResult.Fail("Worker rejected the message with HTTP 503.", 503)
            };
            await RunOnePassAsync(transport);

            await using var context = CreateContext();
            var item = await context.SmtpQueue.AsNoTracking().FirstAsync(q => q.Id == queueItemId);

            // Not delivered, but not stuck either: the retry policy owns what happens next.
            Assert.Equal("Failed", item.Status);
            Assert.Equal(1, item.Attempts);
            Assert.True(item.NextAttemptAt > DateTimeOffset.UtcNow);
            Assert.Equal("Worker rejected the message with HTTP 503.", item.LastError);
        }
        finally
        {
            await CleanupAsync(tenantId);
        }
    }

    [Fact]
    public async Task Cloudflare_transport_preserves_raw_message_even_when_a_dkim_key_exists()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            const string selector = "outbound-test";
            var domain = "dispatch-cf.test";
            var dkim = new DkimService();
            var key = dkim.GenerateKeyPair();
            await SeedDomainAsync(tenantId, domain, "cloudflare", "https://worker.example.workers.dev", selector);
            await SeedDkimKeyAsync(tenantId, domain, selector, key.PrivateKeyPem, key.PublicKeyPem);
            var rawMessage = $"From: sender@{domain}\r\nSubject: queued\r\n\r\nBody.";
            await SeedQueueItemAsync(tenantId, $"user@{domain}", $"sender@{domain}");

            var transport = new RecordingTransport();
            await RunOnePassAsync(transport);

            var sent = Assert.Single(transport.Sent);
            Assert.Equal(rawMessage, sent.RawMessage);
            Assert.DoesNotContain("DKIM-Signature:", sent.RawMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally { await CleanupAsync(tenantId); }
    }

    [Fact]
    public async Task Direct_transport_signs_with_the_configured_publicly_verifiable_key()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            const string selector = "outbound-test";
            var domain = DirectDomain;
            var dkim = new DkimService();
            var key = dkim.GenerateKeyPair();
            await SeedDomainAsync(tenantId, domain, "direct", workerUrl: null, selector: selector);
            await SeedDkimKeyAsync(tenantId, domain, selector, key.PrivateKeyPem, key.PublicKeyPem);
            await SeedQueueItemAsync(tenantId, "user@recipient.test", $"sender@{domain}");

            var transport = new RecordingTransport(DomainTransportModes.Direct);
            await RunOnePassAsync(transport);

            var sent = Assert.Single(transport.Sent);
            Assert.True(dkim.VerifyMessage(sent.RawMessage, key.PublicKeyBase64));
        }
        finally { await CleanupAsync(tenantId); }
    }

    [Fact]
    public async Task Permanent_failure_enqueues_a_null_reverse_path_delivery_status_report_to_local_sender()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            await SeedDomainAsync(tenantId, CloudflareDomain, "cloudflare", "https://worker.example.workers.dev");
            await SeedMailboxAsync(tenantId, CloudflareDomain, $"sender@{CloudflareDomain}");
            var queueId = await SeedQueueItemAsync(tenantId, $"outside@remote.test", $"sender@{CloudflareDomain}");
            var transport = new RecordingTransport
            {
                Result = TransportSendResult.Fail("recipient rejected", 550, permanent: true)
            };

            await RunOnePassAsync(transport);

            await using var context = CreateContext();
            var original = await context.SmtpQueue.SingleAsync(q => q.Id == queueId);
            var dsn = await context.SmtpQueue.SingleAsync(q => q.TenantId == tenantId && q.Direction == QueueDirection.Inbound);
            Assert.Equal("DeadLetter", original.Status);
            Assert.Equal(string.Empty, dsn.Sender);
            Assert.Equal($"sender@{CloudflareDomain}", dsn.Recipient);
            Assert.Contains("multipart/report; report-type=delivery-status", dsn.RawMessage);
            Assert.Contains("Content-Type: message/delivery-status", dsn.RawMessage);
            Assert.Contains("Final-Recipient: rfc822; outside@remote.test", dsn.RawMessage);
            Assert.Contains("Action: failed", dsn.RawMessage);
            Assert.Contains("Status: 5.0.0", dsn.RawMessage);
            Assert.DoesNotContain("recipient rejected", dsn.RawMessage);
        }
        finally { await CleanupAsync(tenantId); }
    }

    [Fact]
    public async Task Permanent_failure_suppresses_dsn_for_null_reverse_path_and_external_sender()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            await SeedDomainAsync(tenantId, CloudflareDomain, "cloudflare", "https://worker.example.workers.dev", isPrimary: true);
            await SeedQueueItemAsync(tenantId, "one@remote.test", "<>");
            await SeedQueueItemAsync(tenantId, "two@remote.test", "sender@external.test");
            var transport = new RecordingTransport
            {
                Result = TransportSendResult.Fail("recipient rejected", 550, permanent: true)
            };

            await RunOnePassAsync(transport);

            await using var context = CreateContext();
            Assert.Equal(2, transport.Sent.Count);
            Assert.Empty(await context.SmtpQueue.Where(q => q.TenantId == tenantId && q.Direction == QueueDirection.Inbound).ToListAsync());
        }
        finally { await CleanupAsync(tenantId); }
    }

    [Fact]
    public async Task Expired_outbound_message_enqueues_a_delivery_status_notification()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            await SeedDomainAsync(tenantId, CloudflareDomain, "cloudflare", "https://worker.example.workers.dev");
            await SeedMailboxAsync(tenantId, CloudflareDomain, $"sender@{CloudflareDomain}");
            var queueId = await SeedQueueItemAsync(tenantId, "outside@remote.test", $"sender@{CloudflareDomain}",
                createdAt: DateTimeOffset.UtcNow.AddDays(-6));

            await RunOnePassAsync(new RecordingTransport());

            await using var context = CreateContext();
            var original = await context.SmtpQueue.SingleAsync(q => q.Id == queueId);
            var dsn = await context.SmtpQueue.SingleAsync(q => q.TenantId == tenantId && q.Direction == QueueDirection.Inbound);
            Assert.Equal("DeadLetter", original.Status);
            Assert.Equal(string.Empty, dsn.Sender);
            Assert.Contains("Status: 4.4.7", dsn.RawMessage);
        }
        finally { await CleanupAsync(tenantId); }
    }

    [Fact]
    public async Task Local_mode_mail_is_left_pending_and_never_touches_the_cloudflare_transport()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            // No Cloudflare domain exists for this tenant at all. The dispatcher must not invent a
            // route for a domain nobody opted in.
            await SeedDomainAsync(tenantId, LocalDomain, "local", workerUrl: null);
            var queueItemId = await SeedQueueItemAsync(tenantId, $"user@{LocalDomain}", $"sender@{LocalDomain}");

            var transport = new RecordingTransport();
            await RunOnePassAsync(transport);

            Assert.Empty(transport.Sent);

            await using var context = CreateContext();
            var item = await context.SmtpQueue.AsNoTracking().FirstAsync(q => q.Id == queueItemId);

            // Exactly the pre-Cloudflare behaviour: queued, undelivered, no attempt recorded.
            Assert.Equal("Pending", item.Status);
            Assert.Equal(0, item.Attempts);
        }
        finally
        {
            await CleanupAsync(tenantId);
        }
    }

    [Fact]
    public async Task A_transport_that_throws_still_records_the_attempt()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            await SeedDomainAsync(tenantId, CloudflareDomain, "cloudflare", "https://worker.example.workers.dev");
            var queueItemId = await SeedQueueItemAsync(tenantId, $"user@{CloudflareDomain}", $"sender@{CloudflareDomain}");

            await RunOnePassAsync(new ThrowingTransport());

            await using var context = CreateContext();
            var item = await context.SmtpQueue.AsNoTracking().FirstAsync(q => q.Id == queueItemId);

            // A row that is never recorded is retried forever and never reaches dead-letter.
            Assert.Equal(1, item.Attempts);
            Assert.Equal("Failed", item.Status);
            Assert.Equal("Transport threw before returning a result.", item.LastError);
        }
        finally
        {
            await CleanupAsync(tenantId);
        }
    }

    // ------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------

    private static async Task RunOnePassAsync(IOutboundMailTransport transport)
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(GetConnectionString()));
        services.AddSingleton<IRetryPolicy, ExponentialBackoffWithJitterRetryPolicy>();
        services.AddScoped<ISmtpQueueManager, SmtpQueueManager>();

        await using var provider = services.BuildServiceProvider();

        var dispatcher = new OutboundQueueDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            [transport],
            new CloudflareEmailOptions { ApiToken = "test-token" },
            new DkimService(),
            new QueueRetryOptions(),
            NullLogger<OutboundQueueDispatcher>.Instance);

        await dispatcher.DispatchBatchAsync(CancellationToken.None);
    }

    private sealed class RecordingTransport : IOutboundMailTransport
    {
        private readonly string _mode;
        public RecordingTransport(string mode = DomainTransportModes.Cloudflare) => _mode = mode;
        public string Mode => _mode;

        public List<OutboundMessage> Sent { get; } = [];

        public TransportSendResult Result { get; init; } = TransportSendResult.Ok(200);

        public Task<TransportSendResult> SendAsync(OutboundMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.FromResult(Result);
        }
    }

    private sealed class ThrowingTransport : IOutboundMailTransport
    {
        public string Mode => DomainTransportModes.Cloudflare;

        public Task<TransportSendResult> SendAsync(OutboundMessage message, CancellationToken ct = default) =>
            throw new HttpRequestException("socket reset");
    }

    // ------------------------------------------------------------------
    // Seeding
    // ------------------------------------------------------------------

    private static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION environment variable is required.");
        }
        return conn;
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(GetConnectionString()).Options);

    private static async Task<Guid> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();
        context.Tenants.Add(new Tenant
        {
            Id = tenantId, Slug = $"dispatch-{Guid.NewGuid():N}", CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();

        return tenantId;
    }

    private static async Task SeedDomainAsync(Guid tenantId, string name, string transportMode, string? workerUrl, string? selector = null, bool isPrimary = false)
    {
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();
        context.Domains.Add(new DomainEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            IsVerified = true,
            TransportMode = transportMode,
            DkimSelector = selector,
            IsPrimary = isPrimary,
            CloudflareWorkerUrl = workerUrl,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedMailboxAsync(Guid tenantId, string domainName, string address)
    {
        await using var context = CreateContext();
        var domain = await context.Domains.SingleAsync(d => d.TenantId == tenantId && d.Name == domainName);
        context.Mailboxes.Add(new Mailbox
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domain.Id, Address = address,
            Name = "Outbound DSN target", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedDkimKeyAsync(Guid tenantId, string domain, string selector, string privateKey, string publicKey)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = CreateContext();
        context.DkimKeys.Add(new DkimKey
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DomainName = domain, Selector = selector,
            PrivateKeyPem = privateKey, PublicKeyPem = publicKey, CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedQueueItemAsync(Guid tenantId, string recipient, string sender, DateTimeOffset? createdAt = null)
    {
        var id = Guid.NewGuid();
        var now = createdAt ?? DateTimeOffset.UtcNow;

        await using var context = CreateContext();
        context.SmtpQueue.Add(new SmtpQueueItem
        {
            Id = id,
            TenantId = tenantId,
            Direction = QueueDirection.Outbound,
            Sender = sender,
            Recipient = recipient,
            Subject = "queued before the dispatcher existed",
            RawMessage = $"From: {sender}\r\nSubject: queued\r\n\r\nBody.",
            Status = "Pending",
            Attempts = 0,
            NextAttemptAt = now.AddSeconds(-1),
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();

        return id;
    }

    private static async Task<Guid> SeedSystemQueueItemAsync(Guid tenantId, string recipient, string sender, string rawMessage, string mailboxAddress)
    {
        var id = await SeedQueueItemAsync(tenantId, recipient, sender);
        await using var context = CreateContext();
        var item = await context.SmtpQueue.SingleAsync(q => q.Id == id);
        item.RawMessage = rawMessage;
        item.TransportSenderOverride = mailboxAddress;
        await context.SaveChangesAsync();
        return id;
    }

    private static async Task CleanupAsync(Guid tenantId)
    {
        await using var context = CreateContext();

        var queueIds = await context.SmtpQueue.Where(q => q.TenantId == tenantId).Select(q => q.Id).ToListAsync();
        await context.SmtpDeliveryAttempts.Where(a => queueIds.Contains(a.QueueItemId)).ExecuteDeleteAsync();
        await context.SmtpQueue.Where(q => q.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Domains.Where(d => d.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync();
    }
}
