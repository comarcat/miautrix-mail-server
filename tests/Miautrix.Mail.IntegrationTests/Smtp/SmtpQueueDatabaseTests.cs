using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Queue;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Smtp;

[Trait("Category", "Smtp")]
public class SmtpQueueDatabaseTests
{
    [Fact]
    public async Task Permanent_failure_dead_letters_immediately_and_retry_lifetime_uses_injected_clock()
    {
        var tenantId = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var clock = new TestTimeProvider(start);
        var context = CreateContext();
        context.Tenants.Add(new Tenant { Id = tenantId, Slug = $"retry-{Guid.NewGuid():N}" });
        await context.SaveChangesAsync();
        var manager = new SmtpQueueManager(context, new ExponentialBackoffWithJitterRetryPolicy(),
            retryOptions: new QueueRetryOptions(), clock: clock);
        var permanent = await manager.EnqueueAsync(tenantId, "sender@local.test", "one@remote.test", "raw", QueueDirection.Outbound);
        await manager.RecordDeliveryAttemptAsync(permanent.Id, false, "550 rejected", 550, permanentFailure: true);
        Assert.Equal("DeadLetter", permanent.Status);

        var expiring = await manager.EnqueueAsync(tenantId, "sender@local.test", "two@remote.test", "raw", QueueDirection.Outbound);
        clock.Advance(TimeSpan.FromDays(5));
        await manager.RecordDeliveryAttemptAsync(expiring.Id, false, "451 temporary", 451);
        Assert.Equal("DeadLetter", expiring.Status);

        var attempts = await context.SmtpDeliveryAttempts.Where(a => a.TenantId == tenantId).ToListAsync();
        context.SmtpDeliveryAttempts.RemoveRange(attempts);
        var items = await context.SmtpQueue.Where(q => q.TenantId == tenantId).ToListAsync();
        context.SmtpQueue.RemoveRange(items);
        var tenant = await context.Tenants.SingleAsync(t => t.Id == tenantId);
        context.Tenants.Remove(tenant);
        await context.SaveChangesAsync();
        await context.DisposeAsync();
    }

    private static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION environment variable is required.");
        }
        return conn;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    [Fact]
    public async Task When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry()
    {
        // ARRANGE
        await using var context = CreateContext();

        var tenantId = Guid.NewGuid();
        var testTenant = new Tenant
        {
            Id = tenantId,
            Slug = $"test-smtp-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Tenants.Add(testTenant);
        await context.SaveChangesAsync();

        var retryPolicy = new ExponentialBackoffWithJitterRetryPolicy(
            baseDelay: TimeSpan.FromSeconds(2),
            jitterRatio: 0.1);
        var queueManager = new SmtpQueueManager(context, retryPolicy);

        // ACT 1: Enqueue message
        var queueItem = await queueManager.EnqueueAsync(
            tenantId,
            "sender@example.com",
            "recipient@example.com",
            "MIME message body",
            QueueDirection.Outbound,
            "Test Subject");

        Assert.NotNull(queueItem);
        Assert.Equal("Pending", queueItem.Status);
        Assert.Equal(0, queueItem.Attempts);

        // ACT 2: Record failing delivery attempt #1
        var attempt1 = await queueManager.RecordDeliveryAttemptAsync(
            queueItem.Id,
            success: false,
            errorMessage: "421 Service unavailable",
            responseCode: 421);

        // ACT 3: Record failing delivery attempt #2
        var attempt2 = await queueManager.RecordDeliveryAttemptAsync(
            queueItem.Id,
            success: false,
            errorMessage: "450 Mailbox busy",
            responseCode: 450);

        // ASSERT: Delay of attempt 2 strictly greater than attempt 1
        Assert.NotNull(attempt1.NextRetryDelaySeconds);
        Assert.NotNull(attempt2.NextRetryDelaySeconds);
        Assert.True(
            attempt2.NextRetryDelaySeconds.Value > attempt1.NextRetryDelaySeconds.Value,
            $"Attempt 2 delay ({attempt2.NextRetryDelaySeconds}) must be strictly greater than Attempt 1 delay ({attempt1.NextRetryDelaySeconds})");

        // Verify in fresh DB context
        await using var verifyContext = CreateContext();
        var persistedItem = await verifyContext.SmtpQueue.FirstOrDefaultAsync(q => q.Id == queueItem.Id);
        var attempts = await verifyContext.SmtpDeliveryAttempts
            .Where(a => a.QueueItemId == queueItem.Id)
            .OrderBy(a => a.AttemptNumber)
            .ToListAsync();

        Assert.NotNull(persistedItem);
        Assert.Equal("Failed", persistedItem.Status);
        Assert.Equal(2, persistedItem.Attempts);
        Assert.Equal(2, attempts.Count);
        Assert.Equal(1, attempts[0].AttemptNumber);
        Assert.Equal(2, attempts[1].AttemptNumber);

        // Clean up
        verifyContext.SmtpDeliveryAttempts.RemoveRange(attempts);
        verifyContext.SmtpQueue.Remove(persistedItem);
        var tenantToRemove = await verifyContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenantToRemove != null)
        {
            verifyContext.Tenants.Remove(tenantToRemove);
        }
        await verifyContext.SaveChangesAsync();
    }
}
