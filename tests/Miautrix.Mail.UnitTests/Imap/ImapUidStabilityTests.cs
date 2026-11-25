using System.Text;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Infrastructure.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Miautrix.Mail.UnitTests.Imap;

public sealed class ImapUidStabilityTests
{
    [Fact]
    public async Task Send_local_delivery_and_draft_creation_allocate_nonzero_uids()
    {
        var tenantId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var sentId = Guid.NewGuid();
        var draftsId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var address = "path-tests@example.test";
        var storagePath = Path.Combine(Path.GetTempPath(), "message_uid_paths_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
            await using var db = new AppDbContext(options);
            db.Tenants.Add(new Tenant { Id = tenantId, Slug = "path-tests" });
            db.Domains.Add(new Miautrix.Mail.Domain.Domain { Id = domainId, TenantId = tenantId, Name = "example.test" });
            db.Users.Add(new User { Id = userId, TenantId = tenantId, Email = address, Name = "Path Test" });
            db.Mailboxes.Add(new Mailbox { Id = mailboxId, TenantId = tenantId, DomainId = domainId, Address = address, Name = address });
            db.Folders.AddRange(
                new Folder { Id = inboxId, TenantId = tenantId, MailboxId = mailboxId, Name = "INBOX", Role = "inbox" },
                new Folder { Id = sentId, TenantId = tenantId, MailboxId = mailboxId, Name = "Sent", Role = "sent" },
                new Folder { Id = draftsId, TenantId = tenantId, MailboxId = mailboxId, Name = "Drafts", Role = "drafts" });
            await db.SaveChangesAsync();
            var allocator = new InMemoryUidAllocator(db);
            var senderAuthorization = new SenderAuthorizationService(db);
            var storage = new FileSystemMailStorage(storagePath);
            var parser = new MimeKitMimeMessageParser();
            var service = new MessageService(db, new AllowAuthorization(), null!, allocator, storage, NullLogger<MessageService>.Instance, senderAuthorization, parser);

            var send = await service.SendMessageAsync(tenantId, userId, mailboxId,
                new SendMessageRequest(address, new[] { address }, null, null, "Local send", "body", null));
            Assert.True(send.Success);
            var sentMessage = await db.Messages.SingleAsync(m => m.FolderId == sentId);
            var deliveredMessage = await db.Messages.SingleAsync(m => m.FolderId == inboxId);
            Assert.True(sentMessage.Uid > 0);
            Assert.True(deliveredMessage.Uid > 0);

            var draftRequest = new DraftMessageRequest(null, address, Array.Empty<string>(), null, null, "Draft", "draft body", null);
            var draft = await service.UpsertDraftAsync(tenantId, userId, mailboxId, draftRequest);
            Assert.True(draft.Success);
            var draftMessage = await db.Messages.SingleAsync(m => m.FolderId == draftsId);
            Assert.True(draftMessage.Uid > 0);
            var originalUid = draftMessage.Uid;
            await using (var rawStream = await storage.OpenReadAsync(draftMessage.ContentHash))
            using (var reader = new StreamReader(rawStream!))
                Assert.Contains("draft body", await reader.ReadToEndAsync());
            var unchanged = await service.UpsertDraftAsync(tenantId, userId, mailboxId, draftRequest with { DraftId = draft.DraftId });
            Assert.True(unchanged.Success);
            Assert.Equal(originalUid, (await db.Messages.SingleAsync(m => m.Id == draft.DraftId)).Uid);
            var edited = await service.UpsertDraftAsync(tenantId, userId, mailboxId,
                draftRequest with { DraftId = draft.DraftId, Subject = "Edited", BodyText = "edited body" });
            Assert.True(edited.Success);
            Assert.Equal(draft.DraftId, edited.DraftId);
            var savedDraft = await db.Messages.SingleAsync(m => m.FolderId == draftsId);
            Assert.NotEqual(originalUid, savedDraft.Uid);
            Assert.Equal("edited body", parser.Parse(await ReadStoredAsync(storage, savedDraft.ContentHash)).BodyText?.TrimEnd());
            var secondAutosave = await service.UpsertDraftAsync(tenantId, userId, mailboxId,
                draftRequest with { DraftId = draft.DraftId, Subject = "Edited", BodyText = "edited body" });
            Assert.Equal(draft.DraftId, secondAutosave.DraftId);
            Assert.Single(await db.Messages.Where(m => m.FolderId == draftsId).ToListAsync());
            Assert.Equal(savedDraft.Uid, (await db.Messages.SingleAsync(m => m.Id == draft.DraftId)).Uid);
            var imap = NewSession(db, storage, tenantId, mailboxId, userId, service, allocator);
            Assert.Equal("OK", (await imap.ExecuteCommandAsync($"A1 LOGIN \"{address}\" secret")).Status);
            Assert.Equal("OK", (await imap.ExecuteCommandAsync("A2 SELECT Drafts")).Status);
            var oldUid = await imap.ExecuteCommandAsync($"A3 UID FETCH {originalUid} (BODY.PEEK[])");
            Assert.DoesNotContain(oldUid.ResponseSegments, segment => Encoding.UTF8.GetString(segment.Bytes).Contains("draft body", StringComparison.Ordinal));
            var fetched = await imap.ExecuteCommandAsync($"A4 UID FETCH {savedDraft.Uid} (BODY.PEEK[])");
            Assert.Contains(fetched.ResponseSegments, segment => Encoding.UTF8.GetString(segment.Bytes).Contains("edited body", StringComparison.Ordinal));
            Assert.Equal(2u, (await db.Folders.SingleAsync(f => f.Id == sentId)).UidNext);
            Assert.Equal(2u, (await db.Folders.SingleAsync(f => f.Id == inboxId)).UidNext);
            Assert.Equal(3u, (await db.Folders.SingleAsync(f => f.Id == draftsId)).UidNext);
        }

        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }

        static async Task<string> ReadStoredAsync(IMailStorage storage, string hash)
        {
            await using var stream = await storage.OpenReadAsync(hash);
            using var reader = new StreamReader(stream!);
            return await reader.ReadToEndAsync();
        }
    }

    [Fact]
    public async Task Fetch_search_expunge_copy_restart_and_move_preserve_folder_uid_rules()
    {
        var tenantId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();
        var trashId = Guid.NewGuid();
        var movedId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var address = "uids@example.test";
        var storagePath = Path.Combine(Path.GetTempPath(), "imap_uid_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);
        try
        {
            var storage = new FileSystemMailStorage(storagePath);
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
            await using var db = new AppDbContext(options);
            var tenant = new Tenant { Id = tenantId, Slug = "uid-tests" };
            db.Tenants.Add(tenant);
            db.Domains.Add(new Miautrix.Mail.Domain.Domain { Id = domainId, TenantId = tenantId, Name = "example.test" });
            db.Mailboxes.Add(new Mailbox { Id = mailboxId, TenantId = tenantId, DomainId = domainId, Address = address, Name = address });
            db.Folders.AddRange(
                Folder(inboxId, "INBOX", "inbox", 21, 7),
                Folder(archiveId, "Archive", "archive", 5, 99),
                Folder(trashId, "Trash", "trash", 1, 8),
                Folder(movedId, "Moved", "user", 1, 9));
            db.Messages.AddRange(
                Message(Guid.NewGuid(), inboxId, 10, "first", string.Empty),
                Message(Guid.NewGuid(), inboxId, 20, "deleted", "\\Deleted"));
            await db.SaveChangesAsync();

            var allocator = new InMemoryUidAllocator(db);
            var service = new MessageService(db, new AllowAuthorization(), null!, allocator, storage, NullLogger<MessageService>.Instance, mimeParser: new MimeKitMimeMessageParser());
            var session = NewSession(db, storage, tenantId, mailboxId, userId, service, allocator);
            Assert.Equal("OK", (await session.ExecuteCommandAsync($"A1 LOGIN \"{address}\" secret")).Status);
            var selected = await session.ExecuteCommandAsync("A2 SELECT INBOX");
            Assert.Equal("OK", selected.Status);
            Assert.Contains("[UIDNEXT 21]", selected.ResponseSegments.Select(x => Encoding.UTF8.GetString(x.Bytes)).Aggregate(string.Concat));

            var fetch = await session.ExecuteCommandAsync("A3 FETCH 1:2 (UID FLAGS)");
            var fetchText = Text(fetch);
            Assert.Contains("UID 10", fetchText);
            Assert.Contains("UID 20", fetchText);
            Assert.DoesNotContain("UID 0", fetchText);
            var uidFetch = await session.ExecuteCommandAsync("A4 UID FETCH 20 (UID FLAGS)");
            Assert.Contains("UID 20", Text(uidFetch));
            var uidSearch = await session.ExecuteCommandAsync("A5 UID SEARCH ALL");
            Assert.Contains("* SEARCH 10 20", Text(uidSearch));

            var expunge = await session.ExecuteCommandAsync("A6 EXPUNGE");
            Assert.Equal("OK", expunge.Status);
            var afterExpunge = await db.Messages.Where(m => m.FolderId == inboxId).OrderBy(m => m.Uid).ToListAsync();
            Assert.Equal(new uint[] { 10 }, afterExpunge.Select(m => m.Uid));

            var copy = await session.ExecuteCommandAsync("A7 UID COPY 10 Archive");
            Assert.Contains("[COPYUID 99 10 5]", copy.Message);
            var copied = await db.Messages.SingleAsync(m => m.FolderId == archiveId);
            Assert.Equal(5u, copied.Uid);
            var status = await session.ExecuteCommandAsync("A8 STATUS Archive (UIDNEXT UIDVALIDITY)");
            Assert.Contains("UIDNEXT 6", Text(status));

            var restartedSession = NewSession(db, storage, tenantId, mailboxId, userId, service, allocator);
            Assert.Equal("OK", (await restartedSession.ExecuteCommandAsync($"B1 LOGIN \"{address}\" secret")).Status);
            var archiveSelect = await restartedSession.ExecuteCommandAsync("B2 EXAMINE Archive");
            Assert.Contains("UIDVALIDITY 99", Text(archiveSelect));
            var restartFetch = await restartedSession.ExecuteCommandAsync("B3 FETCH 1 (UID)");
            Assert.Contains("UID 5", Text(restartFetch));
            Assert.DoesNotContain("UID 0", Text(restartFetch));

            Assert.True(await service.MoveMessageAsync(tenantId, userId, mailboxId, copied.Id, movedId));
            Assert.Empty(await db.Messages.Where(m => m.FolderId == archiveId).ToListAsync());
            var movedMessage = await db.Messages.SingleAsync(m => m.Id == copied.Id);
            Assert.Equal(movedId, movedMessage.FolderId);
            Assert.Equal(1u, movedMessage.Uid);
            Assert.Equal(2u, (await db.Folders.SingleAsync(f => f.Id == movedId)).UidNext);

            var appendPayload = Encoding.UTF8.GetBytes($"From: sender@example.test\r\nTo: {address}\r\nSubject: appended\r\n\r\nbody");
            using var literal = new MemoryStream(appendPayload);
            var append = await restartedSession.ExecuteCommandAsync($"B4 APPEND Moved {{{appendPayload.Length}}}", literalPayload: literal);
            Assert.Contains("[APPENDUID 9 2]", append.Message);
            Assert.Equal(3u, (await db.Folders.SingleAsync(f => f.Id == movedId)).UidNext);
        }
        finally
        {
            if (Directory.Exists(storagePath)) Directory.Delete(storagePath, recursive: true);
        }

        Folder Folder(Guid id, string name, string role, uint uidNext, uint validity) => new()
        {
            Id = id, TenantId = tenantId, MailboxId = mailboxId, Name = name, Role = role, UidNext = uidNext, UidValidity = validity
        };

        Message Message(Guid id, Guid folderId, uint uid, string subject, string flags) => new()
        {
            Id = id, TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId, Uid = uid,
            Sender = "sender@example.test", Recipient = address, Subject = subject, Flags = flags,
            Date = DateTimeOffset.UtcNow, ContentHash = "hash-" + id.ToString("N"), StoragePath = "/message.eml"
        };
    }

    private static ImapSession NewSession(AppDbContext db, IMailStorage storage, Guid tenantId, Guid mailboxId, Guid userId, IMessageService service, IMessageUidAllocator allocator) =>
        new(db, storage, new StubAuthenticator(tenantId, mailboxId, userId), new AllowAuthorization(), messageService: service, uidAllocator: allocator);

    private static string Text(ImapCommandResult result) => string.Concat(result.ResponseSegments.Select(x => Encoding.UTF8.GetString(x.Bytes))) + result.Message;

    private sealed class InMemoryUidAllocator(AppDbContext db) : IMessageUidAllocator
    {
        public async Task<uint> AllocateAsync(Guid tenantId, Guid folderId, uint count = 1, CancellationToken cancellationToken = default)
        {
            var folder = await db.Folders.SingleOrDefaultAsync(f => f.TenantId == tenantId && f.Id == folderId, cancellationToken)
                ?? throw new ResourceNotFoundException();
            var first = folder.UidNext;
            folder.UidNext += count;
            return first;
        }
    }

    private sealed class StubAuthenticator(Guid tenantId, Guid mailboxId, Guid userId) : IImapAuthenticator
    {
        public Task<ImapAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImapAuthenticationResult(true, tenantId, mailboxId, userId));
    }

    private sealed class AllowAuthorization : ITenantAuthorizationHelper
    {
        public TResource AuthorizeAccess<TResource>(Guid currentTenantId, Guid currentUserId, TResource? resource, string requiredPermissionCode) where TResource : TenantScopedEntityBase => resource!;
        public void AssertPermission(Guid tenantId, Guid userId, string permissionCode) { }
        public void AssertMailboxAccess(Guid tenantId, Guid userId, Mailbox? mailbox, bool requireWrite) { if (mailbox is null) throw new ResourceNotFoundException(); }
        public void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId) { }
    }
}
