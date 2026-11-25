using Miautrix.Mail.Domain;
using Miautrix.Mail.Licensing;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.IntegrationTests.Licensing;

[Trait("Category", "Licensing")]
public class LicensingQuotaTests : IClassFixture<IsolatedPostgresFixture>
{
    private readonly IsolatedPostgresFixture _database;

    public LicensingQuotaTests(IsolatedPostgresFixture database) => _database = database;

    [Fact]
    public async Task TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData()
    {
        var tenantId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var host = tenantId.ToString("N");
        await using var db = IsolatedPostgresFixture.CreateContext(_database.PrimaryConnectionString);
        db.Tenants.Add(new Tenant { Id = tenantId, Slug = $"acme-{host}" });
        db.Domains.Add(new DomainEntity { Id = domainId, TenantId = tenantId, Name = $"{host}.corp" });

        var mailbox1 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = $"user1@{host}.corp" };
        var mailbox2 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = $"user2@{host}.corp" };
        var mailbox3 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = $"user3@{host}.corp" };
        var mailbox4 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = $"user4@{host}.corp" };
        var mailbox5 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = $"user5@{host}.corp" };

        db.Mailboxes.AddRange(mailbox1, mailbox2, mailbox3, mailbox4, mailbox5);

        var folder1 = new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailbox1.Id, Name = "INBOX", UidNext = 2 };
        db.Folders.Add(folder1);

        var message1 = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailbox1.Id,
            FolderId = folder1.Id,
            Uid = 1,
            Subject = "Existing Important Mail",
            Sender = "ceo@partner.com",
            Recipient = mailbox1.Address
        };
        db.Messages.Add(message1);

        try
        {
        await db.SaveChangesAsync();

        var quotaEnforcer = new LicenseQuotaEnforcer(db);

        // 1. Verify that tenant is at capacity (allowance = 5)
        var canCreate = await quotaEnforcer.CanCreateMailboxAsync(tenantId);
        Assert.False(canCreate);

        // 2. Creating a 6th mailbox MUST be blocked and throw LicenseQuotaException
        await Assert.ThrowsAsync<LicenseQuotaException>(async () =>
        {
            await quotaEnforcer.CreateMailboxGuardedAsync(tenantId, domainId, "user6@acme.corp", 1024 * 1024);
        });

        // 3. Verify that delivering mail to an existing mailbox STILL works
        var incomingMessage = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailbox1.Id,
            FolderId = folder1.Id,
            Subject = "Urgent Client Order",
            Sender = "client@external.com",
            Recipient = mailbox1.Address
        };

        await quotaEnforcer.DeliverMessageAsync(incomingMessage);

        // 4. Verify that NO data was destroyed
        Assert.Equal(5, await db.Mailboxes.CountAsync(m => m.TenantId == tenantId));
        Assert.Equal(2, await db.Messages.CountAsync(m => m.TenantId == tenantId));

        var existingMsg = await db.Messages.FindAsync(message1.Id);
        Assert.NotNull(existingMsg);
        Assert.Equal("Existing Important Mail", existingMsg.Subject);

        var deliveredMsg = await db.Messages.FindAsync(incomingMessage.Id);
        Assert.NotNull(deliveredMsg);
        Assert.True(deliveredMsg.Uid > 0);
        Assert.Equal("Urgent Client Order", deliveredMsg.Subject);
        }
        finally
        {
            await db.Messages.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
            await db.Folders.Where(f => f.TenantId == tenantId).ExecuteDeleteAsync();
            await db.Mailboxes.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
            await db.Domains.Where(d => d.TenantId == tenantId).ExecuteDeleteAsync();
            await db.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync();
        }
    }
}
