using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.AntiMalware;
using Miautrix.Mail.AntiSpam;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.IntegrationTests.MailFlow;

/// <summary>
/// Regression coverage for MIA-92: an inbound email whose <c>Date:</c> header carries a non-UTC
/// offset used to throw inside the single shared <c>SaveChangesAsync</c> call at the end of a
/// dispatch batch. Because that one failure happened outside every item's own <c>try/catch</c>,
/// it silently rolled back (and hid the failure of) every row in the same batch -- the exact
/// symptom reported: messages stuck <c>Pending</c> with zero attempts and no <c>LastError</c>.
/// </summary>
[Trait("Category", "MailFlow")]
public sealed class InboundQueueDispatcherTests
{
    private const string TestDomain = "inbound-dispatch.test";

    [Fact]
    public async Task A_non_utc_date_header_is_normalized_and_does_not_throw()
    {
        var tenantId = await SeedTenantAsync();
        try
        {
            var mailboxId = await SeedDomainAndMailboxAsync(tenantId, $"user@{TestDomain}");
            var queueItemId = await SeedQueueItemAsync(
                tenantId,
                $"user@{TestDomain}",
                rawMessage: "From: sender@example.com\r\n" +
                            "Subject: Non-UTC date header\r\n" +
                            "Date: Mon, 5 Oct 2026 17:00:00 -0700\r\n" +
                            "\r\n" +
                            "Body.");

            await RunOnePassAsync();

            await using var context = CreateContext();
            var item = await context.SmtpQueue.AsNoTracking().FirstAsync(q => q.Id == queueItemId);
            Assert.Equal("Delivered", item.Status);
            Assert.Null(item.LastError);

            var message = await context.Messages.AsNoTracking()
                .FirstAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId);

            // -0700 on 2026-10-05 17:00 is 2026-10-06 00:00 UTC. Postgres' timestamptz column only
            // accepts Kind=Utc / offset-zero values; this assertion fails with the old unconverted
            // behaviour because the write itself would have thrown first.
            Assert.Equal(TimeSpan.Zero, message.Date.Offset);
            Assert.Equal(new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero), message.Date);
        }
        finally
        {
            await CleanupAsync(tenantId);
        }
    }

    [Fact]
    public async Task One_bad_row_in_a_batch_does_not_block_or_hide_the_other_rows()
    {
        const string poisonSubjectMarker = "[[POISON-ROW]]";
        var tenantId = await SeedTenantAsync();
        try
        {
            await SeedDomainAndMailboxAsync(tenantId, $"user@{TestDomain}");

            // The bad row's spam evaluation is made to throw (standing in for any persistence/
            // processing failure for a single item) by tagging it with a marker that the fault
            // injecting spam provider below recognises. Before this fix, this failure would have
            // been swallowed by the single shared SaveChangesAsync at the end of the batch and
            // would have rolled back/hidden every other row's result too.
            var badItemId = await SeedQueueItemAsync(
                tenantId,
                $"user@{TestDomain}",
                rawMessage: $"From: sender@example.com\r\nSubject: {poisonSubjectMarker}\r\n\r\nBody.");

            var goodItemId = await SeedQueueItemAsync(
                tenantId,
                $"user@{TestDomain}",
                rawMessage: "From: sender@example.com\r\nSubject: Good row\r\n\r\nBody.");

            await RunOnePassAsync(poisonSubjectMarker);

            await using var context = CreateContext();

            var badItem = await context.SmtpQueue.AsNoTracking().FirstAsync(q => q.Id == badItemId);
            Assert.Equal("Failed", badItem.Status);
            Assert.False(string.IsNullOrWhiteSpace(badItem.LastError));

            var goodItem = await context.SmtpQueue.AsNoTracking().FirstAsync(q => q.Id == goodItemId);
            Assert.Equal("Delivered", goodItem.Status);
            Assert.Null(goodItem.LastError);
        }
        finally
        {
            await CleanupAsync(tenantId);
        }
    }

    private sealed class FaultInjectingSpamProvider : ISpamProvider
    {
        private readonly string _poisonSubjectMarker;
        private readonly ISpamProvider _inner = new RuleBasedSpamProvider();

        public FaultInjectingSpamProvider(string poisonSubjectMarker)
        {
            _poisonSubjectMarker = poisonSubjectMarker;
        }

        public Task<SpamVerdict> EvaluateAsync(Guid tenantId, InboundMailContext mail, double threshold = 5.0, CancellationToken cancellationToken = default)
        {
            if (mail.Subject?.Contains(_poisonSubjectMarker, StringComparison.Ordinal) == true)
            {
                throw new InvalidOperationException("Simulated processing failure for the poisoned row.");
            }

            return _inner.EvaluateAsync(tenantId, mail, threshold, cancellationToken);
        }
    }

    // ------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------

    private static async Task RunOnePassAsync(string? poisonSubjectMarker = null)
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(GetConnectionString()));
        services.AddSingleton<IMailStorage>(new FileSystemMailStorage(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "miautrix-inbound-dispatch-tests", Guid.NewGuid().ToString("N"))));
        services.AddSingleton<IAntiMalwareScanner, ClamAvScanner>();
        services.AddSingleton(new AntiMalwareOptions("/run/clamav/this-socket-does-not-exist.ctl", Required: false, ScanTimeoutSeconds: 1));

        if (poisonSubjectMarker is null)
        {
            services.AddScoped<ISpamProvider, RuleBasedSpamProvider>();
        }
        else
        {
            services.AddScoped<ISpamProvider>(_ => new FaultInjectingSpamProvider(poisonSubjectMarker));
        }

        services.AddScoped<IQuarantineService, QuarantineService>();

        await using var provider = services.BuildServiceProvider();

        var dispatcher = new InboundQueueDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<InboundQueueDispatcher>.Instance);

        await dispatcher.ProcessPendingMessagesAsync(CancellationToken.None);
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

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(GetConnectionString()).Options);

    private static async Task<Guid> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();
        context.Tenants.Add(new Tenant
        {
            Id = tenantId, Slug = $"inbound-dispatch-{Guid.NewGuid():N}", CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();

        return tenantId;
    }

    private static async Task<Guid> SeedDomainAndMailboxAsync(Guid tenantId, string address)
    {
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();
        context.Domains.Add(new DomainEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = TestDomain,
            IsVerified = true,
            MalwareScanningEnabled = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        var mailboxId = Guid.NewGuid();
        context.Mailboxes.Add(new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            DomainId = Guid.NewGuid(),
            Address = address,
            Name = "Test User",
            QuotaBytes = 10L * 1024 * 1024 * 1024,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return mailboxId;
    }

    private static async Task<Guid> SeedQueueItemAsync(Guid tenantId, string recipient, string rawMessage)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();
        context.SmtpQueue.Add(new SmtpQueueItem
        {
            Id = id,
            TenantId = tenantId,
            Direction = QueueDirection.Inbound,
            Sender = "sender@example.com",
            Recipient = recipient,
            Subject = null,
            RawMessage = rawMessage,
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

        var mailboxIds = await context.Mailboxes.Where(m => m.TenantId == tenantId).Select(m => m.Id).ToListAsync();
        await context.Attachments.Where(a => a.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Messages.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Folders.Where(f => f.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Mailboxes.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
        await context.SpamVerdicts.Where(v => v.TenantId == tenantId).ExecuteDeleteAsync();
        await context.SmtpQueue.Where(q => q.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Domains.Where(d => d.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync();
        _ = mailboxIds;
    }
}
