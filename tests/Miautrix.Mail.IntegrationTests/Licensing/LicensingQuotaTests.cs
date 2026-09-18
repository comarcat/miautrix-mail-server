using Miautrix.Mail.Domain;
using Miautrix.Mail.Licensing;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.IntegrationTests.Licensing;

[Trait("Category", "Licensing")]
public class LicensingQuotaTests
{
    private AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData()
    {
        var dbName = $"license_db_{Guid.NewGuid():N}";
        using var db = CreateDbContext(dbName);

        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Slug = "acme" };
        db.Tenants.Add(tenant);

        var domainId = Guid.NewGuid();
        var domain = new DomainEntity { Id = domainId, TenantId = tenantId };
        db.Domains.Add(domain);

        var mailbox1 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = "user1@acme.corp" };
        var mailbox2 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = "user2@acme.corp" };
        var mailbox3 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = "user3@acme.corp" };
        var mailbox4 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = "user4@acme.corp" };
        var mailbox5 = new Mailbox { Id = Guid.NewGuid(), TenantId = tenantId, DomainId = domainId, Address = "user5@acme.corp" };

        db.Mailboxes.AddRange(mailbox1, mailbox2, mailbox3, mailbox4, mailbox5);

        var folder1 = new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailbox1.Id, Name = "INBOX" };
        db.Folders.Add(folder1);

        var message1 = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailbox1.Id,
            FolderId = folder1.Id,
            Subject = "Existing Important Mail",
            Sender = "ceo@partner.com",
            Recipient = mailbox1.Address
        };
        db.Messages.Add(message1);

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
        Assert.Equal("Urgent Client Order", deliveredMsg.Subject);
    }
}
