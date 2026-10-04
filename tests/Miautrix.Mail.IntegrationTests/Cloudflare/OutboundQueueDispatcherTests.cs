using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Queue;
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
            var queueItemId = await SeedQueueItemAsync(tenantId, $"user@{CloudflareDomain}");

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
    public async Task A_failed_send_is_recorded_so_the_row_retries_instead_of_looping_forever()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            await SeedDomainAsync(tenantId, CloudflareDomain, "cloudflare", "https://worker.example.workers.dev");
            var queueItemId = await SeedQueueItemAsync(tenantId, $"user@{CloudflareDomain}");

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
    public async Task Local_mode_mail_is_left_pending_and_never_touches_the_cloudflare_transport()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            // No Cloudflare domain exists for this tenant at all. The dispatcher must not invent a
            // route for a domain nobody opted in.
            await SeedDomainAsync(tenantId, LocalDomain, "local", workerUrl: null);
            var queueItemId = await SeedQueueItemAsync(tenantId, $"user@{LocalDomain}");

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
            var queueItemId = await SeedQueueItemAsync(tenantId, $"user@{CloudflareDomain}");

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
            NullLogger<OutboundQueueDispatcher>.Instance);

        await dispatcher.DispatchBatchAsync(CancellationToken.None);
    }

    private sealed class RecordingTransport : IOutboundMailTransport
    {
        public string Mode => DomainTransportModes.Cloudflare;

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
            conn = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";
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

    private static async Task SeedDomainAsync(Guid tenantId, string name, string transportMode, string? workerUrl)
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
            CloudflareWorkerUrl = workerUrl,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedQueueItemAsync(Guid tenantId, string recipient)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();
        context.SmtpQueue.Add(new SmtpQueueItem
        {
            Id = id,
            TenantId = tenantId,
            Sender = "sender@example.com",
            Recipient = recipient,
            Subject = "queued before the dispatcher existed",
            RawMessage = "From: sender@example.com\r\nSubject: queued\r\n\r\nBody.",
            Status = "Pending",
            Attempts = 0,
            NextAttemptAt = now.AddSeconds(-1),
            CreatedAt = now,
            UpdatedAt = now
        });
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
