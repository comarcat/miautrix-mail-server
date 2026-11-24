using System.Security.Cryptography;
using System.Text;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Miautrix.Mail.IntegrationTests.Mail;

public sealed class SenderAuthorizationWiringTests : IClassFixture<UidPostgresFixture>
{
    private readonly UidPostgresFixture _fixture;
    public SenderAuthorizationWiringTests(UidPostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Send_as_only_delegate_cannot_mark_shared_mailbox_message_read()
    {
        var (db, tenant, user, shared, folder, storagePath) = await SetupAsync();
        await using (db)
        {
            db.MailboxDelegates.Add(new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenant.Id, UserId = user.Id, MailboxId = shared.Id, AccessLevel = "none", CanSendAs = true });
            var message = NewMessage(tenant.Id, shared.Id, folder.Id);
            db.Messages.Add(message);
            await db.SaveChangesAsync();
            var policy = new SenderAuthorizationService(db);
            Assert.True(await policy.HasSharedMailboxSendPermissionAsync(tenant.Id, user.Id, shared.Id));
            var auth = new TenantAuthorizationHelper(new EfPermissionRepository(db), new InMemorySecurityEventSink());
            var service = CreateMessageService(db, auth, storagePath, policy);
            await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.MarkReadAsync(tenant.Id, user.Id, shared.Id, message.Id, true));
        }
        Cleanup(storagePath);
    }

    [Fact]
    public async Task Rejected_webmail_send_leaves_no_message_attachment_or_queue_rows()
    {
        var (db, tenant, user, shared, folder, storagePath) = await SetupAsync();
        await using (db)
        {
            var policy = new SenderAuthorizationService(db);
            var service = CreateMessageService(db, new AllowAuthorization(), storagePath,
                new PendingRowsOnRejectPolicy(db, policy, shared.Id, folder.Id));
            var result = await service.SendMessageAsync(tenant.Id, user.Id, shared.Id,
                new SendMessageRequest("sales@sender.test", ["outside@remote.test"], null, null, "reject", "body", null,
                    [new Miautrix.Mail.Application.Mail.Mime.MimeAttachment("a.txt", "text/plain", Encoding.UTF8.GetBytes("attachment"))]));
            Assert.False(result.Success);
            Assert.Empty(await db.Messages.AsNoTracking().Where(m => m.TenantId == tenant.Id).ToListAsync());
            Assert.Empty(await db.Attachments.AsNoTracking().Where(a => a.TenantId == tenant.Id).ToListAsync());
            Assert.Empty(await db.SmtpQueue.AsNoTracking().Where(q => q.TenantId == tenant.Id).ToListAsync());
        }
        Cleanup(storagePath);
    }

    [Fact]
    public async Task On_behalf_stores_final_eml_bytes_size_and_hash()
    {
        var (db, tenant, user, shared, _, storagePath) = await SetupAsync();
        await using (db)
        {
            db.MailboxDelegates.Add(new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenant.Id, UserId = user.Id, MailboxId = shared.Id, AccessLevel = "none", CanSendOnBehalf = true });
            await db.SaveChangesAsync();
            var policy = new SenderAuthorizationService(db);
            var service = CreateMessageService(db, new AllowAuthorization(), storagePath, policy);
            var result = await service.SendMessageAsync(tenant.Id, user.Id, shared.Id,
                new SendMessageRequest(shared.Address, ["outside@remote.test"], null, null, "rewrite", "body", null));
            Assert.True(result.Success);
            var message = await db.Messages.AsNoTracking().SingleAsync(m => m.Id == result.MessageId);
            var queue = await db.SmtpQueue.AsNoTracking().SingleAsync(q => q.Id == result.QueueItemId);
            var bytes = Encoding.UTF8.GetBytes(queue.RawMessage);
            Assert.Contains("Sender: delegate@sender.test", queue.RawMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(bytes.Length, message.SizeBytes);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), message.ContentHash);
            await using var stored = await new FileSystemMailStorage(storagePath).OpenReadAsync(message.ContentHash);
            Assert.NotNull(stored);
            using var output = new MemoryStream();
            await stored!.CopyToAsync(output);
            Assert.Equal(bytes, output.ToArray());
            Assert.Equal(bytes.Length, message.SizeBytes); // RFC822.SIZE is the stored byte count.
        }
        Cleanup(storagePath);
    }

    private async Task<(AppDbContext Db, Tenant Tenant, User User, Mailbox Shared, Folder Folder, string StoragePath)> SetupAsync()
    {
        var connection = await _fixture.CreateDatabaseAsync();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        var tenant = new Tenant { Id = Guid.NewGuid(), Slug = "wire-" + Guid.NewGuid().ToString("N") };
        var domain = new Miautrix.Mail.Domain.Domain { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "sender.test" };
        var user = new User { Id = Guid.NewGuid(), TenantId = tenant.Id, Email = "delegate@sender.test", Name = "Delegate" };
        var personal = new Mailbox { Id = Guid.NewGuid(), TenantId = tenant.Id, DomainId = domain.Id, Address = user.Email, Kind = "user", Name = "Delegate" };
        var shared = new Mailbox { Id = Guid.NewGuid(), TenantId = tenant.Id, DomainId = domain.Id, Address = "sales@sender.test", Kind = "shared", Name = "Sales" };
        var folder = new Folder { Id = Guid.NewGuid(), TenantId = tenant.Id, MailboxId = shared.Id, Name = "INBOX", Role = "inbox" };
        var sharedSent = new Folder { Id = Guid.NewGuid(), TenantId = tenant.Id, MailboxId = shared.Id, Name = "Sent", Role = "sent" };
        var personalSent = new Folder { Id = Guid.NewGuid(), TenantId = tenant.Id, MailboxId = personal.Id, Name = "Sent", Role = "sent" };
        db.AddRange(tenant, domain, user, personal, shared, folder, sharedSent, personalSent);
        await db.SaveChangesAsync();
        return (db, tenant, user, shared, folder, Path.Combine(Path.GetTempPath(), "sender-auth-" + Guid.NewGuid().ToString("N")));
    }

    private static MessageService CreateMessageService(AppDbContext db, ITenantAuthorizationHelper auth, string storagePath, ISenderAuthorizationService policy) =>
        new(db, auth, null!, new MessageUidAllocator(db), new FileSystemMailStorage(storagePath), NullLogger<MessageService>.Instance, policy);

    private static Message NewMessage(Guid tenantId, Guid mailboxId, Guid folderId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId, Uid = 1,
        Sender = "sender@sender.test", Recipient = "recipient@sender.test", Subject = "test", Date = DateTimeOffset.UtcNow,
        ContentHash = "testhash", StoragePath = "/test/message.eml", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
    };

    private static void Cleanup(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); }

    private sealed class AllowAuthorization : ITenantAuthorizationHelper
    {
        public TResource AuthorizeAccess<TResource>(Guid t, Guid u, TResource? r, string p) where TResource : TenantScopedEntityBase => r ?? throw new ResourceNotFoundException();
        public void AssertPermission(Guid t, Guid u, string p) { }
        public void AssertMailboxAccess(Guid t, Guid u, Mailbox? m, bool requireWrite) { if (m is null) throw new ResourceNotFoundException(); }
        public void ValidateOwnerDemotion(Guid t, Guid u) { }
    }

    private sealed class PendingRowsOnRejectPolicy(AppDbContext db, ISenderAuthorizationService inner, Guid mailboxId, Guid folderId) : ISenderAuthorizationService
    {
        public Task<bool> HasSharedMailboxSendPermissionAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken ct = default) => inner.HasSharedMailboxSendPermissionAsync(tenantId, userId, mailboxId, ct);
        public Task<bool> IsEnvelopeSenderAllowedAsync(Guid tenantId, Guid userId, string sender, CancellationToken ct = default) => inner.IsEnvelopeSenderAllowedAsync(tenantId, userId, sender, ct);
        public Task<SenderAuthorizationResult> AuthorizeMessageAsync(Guid tenantId, Guid userId, string envelopeSender, string rawMessage, CancellationToken ct = default) => inner.AuthorizeMessageAsync(tenantId, userId, envelopeSender, rawMessage, ct);

        public Task<SenderAuthorizationResult> AuthorizeWebmailAsync(Guid tenantId, Guid userId, string requestedFrom, string rawMessage, CancellationToken ct = default)
        {
            var messageId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.Messages.Add(new Message { Id = messageId, TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId, Uid = 90, Sender = requestedFrom, Recipient = "outside@remote.test", Subject = "pending", Date = now, ContentHash = "pending", StoragePath = "/pending.eml", CreatedAt = now, UpdatedAt = now });
            db.Attachments.Add(new Attachment { Id = Guid.NewGuid(), TenantId = tenantId, MessageId = messageId, FileName = "pending.txt", ContentType = "text/plain", SizeBytes = 1, ContentHash = "pending", StoragePath = "/pending.txt", CreatedAt = now, UpdatedAt = now });
            db.SmtpQueue.Add(new SmtpQueueItem { Id = Guid.NewGuid(), TenantId = tenantId, Direction = QueueDirection.Outbound, Sender = requestedFrom, Recipient = "outside@remote.test", RawMessage = rawMessage, Status = "Pending", CreatedAt = now, UpdatedAt = now });
            return inner.AuthorizeWebmailAsync(tenantId, userId, requestedFrom, rawMessage, ct);
        }
    }

}
