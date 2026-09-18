using Miautrix.Mail.AntiSpam;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.AntiSpam;

[Trait("Category", "AntiSpam")]
public class AntiSpamTests
{
    private static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            conn = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";
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

    [Fact]
    public async Task When_message_exceeds_configured_spam_threshold_it_is_quarantined_and_not_delivered()
    {
        // ARRANGE
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var testTenant = new Tenant
        {
            Id = tenantId,
            Slug = $"test-antispam-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Tenants.Add(testTenant);
        await context.SaveChangesAsync();

        var spamProvider = new RuleBasedSpamProvider();
        var quarantineService = new QuarantineService(context, spamProvider);

        // Blatant spam message: spam keywords + bad SPF + bad DMARC
        var spamMail = new InboundMailContext(
            Sender: "spammer@malicious.xyz",
            Recipient: "victim@miautrix.local",
            ClientIp: "198.51.100.99", // Known DNSBL IP (+5.5)
            Subject: "URGENT: CLAIM YOUR LOTTERY WINNER PRIZE NOW $$$", // High risk keywords + punctuation (+2.5 + +1.5 + caps)
            RawMessage: "Dear friend, you have won the crypto giveaway and lottery winner fund. Please wire transfer urgently to claim.",
            SpfResult: "Fail", // (+2.5)
            DkimResult: "Fail", // (+2.5)
            DmarcResult: "Fail" // (+3.0)
        );

        // ACT: Process message with spam threshold of 5.0
        var result = await quarantineService.ProcessInboundMessageAsync(tenantId, spamMail, threshold: 5.0);

        // ASSERT: Must NOT be delivered, must be quarantined
        Assert.False(result.Delivered, "Spam message exceeding threshold MUST NOT be delivered to mailbox");
        Assert.True(result.Quarantined, "Spam message exceeding threshold MUST be quarantined");
        Assert.NotNull(result.QuarantineItemId);
        Assert.True(result.Score >= 5.0, $"Score {result.Score} should exceed threshold 5.0");

        // Verify in database
        await using var verifyContext = CreateContext();
        var quarantinedItem = await verifyContext.Quarantine
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == result.QuarantineItemId.Value);

        Assert.NotNull(quarantinedItem);
        Assert.Equal("Quarantined", quarantinedItem.Status);
        Assert.False(quarantinedItem.IsDelivered);
        Assert.Equal(spamMail.Sender, quarantinedItem.Sender);
        Assert.Equal(spamMail.Recipient, quarantinedItem.Recipient);

        // ACT 2: Test release-and-train functionality
        var releaseSuccess = await quarantineService.ReleaseAndTrainAsync(tenantId, quarantinedItem.Id, isSpam: false);
        Assert.True(releaseSuccess);

        await using var verifyContext2 = CreateContext();
        var updatedItem = await verifyContext2.Quarantine
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quarantinedItem.Id);
        Assert.NotNull(updatedItem);
        Assert.Equal("TrainedHam", updatedItem.Status);
        Assert.True(updatedItem.IsDelivered);
        Assert.NotNull(updatedItem.TrainedAt);

        // CLEANUP
        verifyContext2.Quarantine.Remove(updatedItem);
        var spamVerdicts = await verifyContext2.SpamVerdicts.Where(v => v.TenantId == tenantId).ToListAsync();
        verifyContext2.SpamVerdicts.RemoveRange(spamVerdicts);
        var tenantToRemove = await verifyContext2.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenantToRemove != null)
        {
            verifyContext2.Tenants.Remove(tenantToRemove);
        }
        await verifyContext2.SaveChangesAsync();
    }

    [Fact]
    public async Task When_legitimate_clean_message_arrives_it_is_delivered_without_quarantine()
    {
        // ARRANGE
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var testTenant = new Tenant
        {
            Id = tenantId,
            Slug = $"test-clean-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Tenants.Add(testTenant);
        await context.SaveChangesAsync();

        var spamProvider = new RuleBasedSpamProvider();
        var quarantineService = new QuarantineService(context, spamProvider);

        var cleanMail = new InboundMailContext(
            Sender: "partner@legitimate-company.org",
            Recipient: "alice@miautrix.local",
            ClientIp: "203.0.113.1",
            Subject: "Q3 Project Review Meeting Notes",
            RawMessage: "Hi Alice, attached are the meeting notes from today's discussion on architecture.",
            SpfResult: "Pass",
            DkimResult: "Pass",
            DmarcResult: "Pass"
        );

        // ACT: Process clean message
        var result = await quarantineService.ProcessInboundMessageAsync(tenantId, cleanMail, threshold: 5.0);

        // ASSERT: Must be delivered, must NOT be quarantined
        Assert.True(result.Delivered, "Clean message MUST be delivered");
        Assert.False(result.Quarantined, "Clean message MUST NOT be quarantined");
        Assert.Null(result.QuarantineItemId);
        Assert.True(result.Score < 5.0, $"Score {result.Score} should be below threshold 5.0");

        // Verify database state: no quarantine row
        await using var verifyContext = CreateContext();
        var quarantineRows = await verifyContext.Quarantine.Where(q => q.TenantId == tenantId).ToListAsync();
        Assert.Empty(quarantineRows);

        // CLEANUP
        var spamVerdicts = await verifyContext.SpamVerdicts.Where(v => v.TenantId == tenantId).ToListAsync();
        verifyContext.SpamVerdicts.RemoveRange(spamVerdicts);
        var tenantToRemove = await verifyContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenantToRemove != null)
        {
            verifyContext.Tenants.Remove(tenantToRemove);
        }
        await verifyContext.SaveChangesAsync();
    }
}
