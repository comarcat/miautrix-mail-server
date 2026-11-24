using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.UnitTests.Imap;

public sealed class FolderRenameServiceTests
{
    [Fact]
    public async Task Rename_resolves_hierarchical_parent_and_keeps_uid_validity()
    {
        var tenant = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        using var db = CreateDb();
        db.Mailboxes.Add(new Mailbox { Id = mailboxId, TenantId = tenant, DomainId = Guid.NewGuid(), Address = "u@example.test", Name = "u" });
        db.Folders.AddRange(
            new Folder { Id = parentId, TenantId = tenant, MailboxId = mailboxId, Name = "Projects", Role = "custom" },
            new Folder { Id = folderId, TenantId = tenant, MailboxId = mailboxId, Name = "Old", Role = "custom", UidValidity = 77 });
        await db.SaveChangesAsync();
        var service = new MailboxService(db, new AllowMailboxAccess(), new NoopPermissions());

        var result = await service.RenameFolderAsync(tenant, Guid.NewGuid(), mailboxId, folderId, "Projects/New");

        Assert.Equal("New", result.Name);
        Assert.Equal(parentId, result.ParentId);
        Assert.Equal((uint)77, (await db.Folders.FindAsync(folderId))!.UidValidity);
    }

    [Fact]
    public async Task Rename_rejects_system_folder_and_sibling_collision()
    {
        var tenant = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var customId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        using var db = CreateDb();
        db.Mailboxes.Add(new Mailbox { Id = mailboxId, TenantId = tenant, DomainId = Guid.NewGuid(), Address = "u@example.test", Name = "u" });
        db.Folders.AddRange(
            new Folder { Id = inboxId, TenantId = tenant, MailboxId = mailboxId, Name = "INBOX", Role = "inbox" },
            new Folder { Id = customId, TenantId = tenant, MailboxId = mailboxId, Name = "Old", Role = "custom" },
            new Folder { Id = targetId, TenantId = tenant, MailboxId = mailboxId, Name = "Existing", Role = "custom" });
        await db.SaveChangesAsync();
        var service = new MailboxService(db, new AllowMailboxAccess(), new NoopPermissions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RenameFolderAsync(tenant, Guid.NewGuid(), mailboxId, inboxId, "New"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RenameFolderAsync(tenant, Guid.NewGuid(), mailboxId, customId, "Existing"));
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class AllowMailboxAccess : ITenantAuthorizationHelper
    {
        public TResource AuthorizeAccess<TResource>(Guid currentTenantId, Guid currentUserId, TResource? resource, string requiredPermissionCode) where TResource : TenantScopedEntityBase => resource!;
        public void AssertPermission(Guid tenantId, Guid userId, string permissionCode) { }
        public void AssertMailboxAccess(Guid tenantId, Guid userId, Mailbox? mailbox, bool requireWrite) { if (mailbox is null) throw new ResourceNotFoundException(); }
        public void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId) { }
    }

    private sealed class NoopPermissions : IPermissionRepository
    {
        public bool HasPermission(Guid tenantId, Guid userId, string permissionCode) => false;
        public int GetTenantOwnerCount(Guid tenantId) => 0;
        public bool IsUserTenantOwner(Guid tenantId, Guid userId) => false;
        public bool IsMailboxOwner(Guid tenantId, Guid userId, Guid mailboxId) => false;
        public string? GetMailboxDelegateAccess(Guid tenantId, Guid userId, Guid mailboxId) => null;
        public string GetMailboxEffectiveAccess(Guid tenantId, Guid userId, Mailbox mailbox) => "read";
    }
}
