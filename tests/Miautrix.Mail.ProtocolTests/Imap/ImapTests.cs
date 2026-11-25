using System.Text;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.ProtocolTests.Imap;

[Trait("Category", "Imap")]
public sealed class ImapTests : IDisposable
{
    private readonly string _testStorageDir;
    private readonly AppDbContext _dbContext;
    private readonly FileSystemMailStorage _storage;

    public ImapTests()
    {
        _testStorageDir = Path.Combine(Path.GetTempPath(), $"miautrix_test_storage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStorageDir);
        _storage = new FileSystemMailStorage(_testStorageDir);

        var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION environment variable is required.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _dbContext = new AppDbContext(options);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        if (Directory.Exists(_testStorageDir))
        {
            try { Directory.Delete(_testStorageDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Storage_DeduplicatesIdenticalPayloads()
    {
        // Arrange
        var content = "MIME-Version: 1.0\r\nSubject: Deduplication Test\r\n\r\nHello World Duplicate Content";
        var bytes = Encoding.UTF8.GetBytes(content);

        using var stream1 = new MemoryStream(bytes);
        using var stream2 = new MemoryStream(bytes);

        // Act
        var result1 = await _storage.StoreAsync(stream1);
        var result2 = await _storage.StoreAsync(stream2);

        // Assert
        Assert.Equal(result1.ContentHash, result2.ContentHash);
        Assert.Equal(result1.SizeBytes, result2.SizeBytes);
        Assert.Equal(result1.StoragePath, result2.StoragePath);
        Assert.True(await _storage.ExistsAsync(result1.ContentHash));
    }

    [Fact]
    public async Task Imap_AppendAndFetch_PreservesBytePayloadAndFlags()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var address = $"user_{Guid.NewGuid():N}@miautrix.local";

        var tenant = new Tenant
        {
            Id = tenantId,
            Slug = $"tenant-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var domain = new DomainEntity
        {
            Id = domainId,
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var mailbox = new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            DomainId = domainId,
            Address = address,
            QuotaBytes = 1000000,
            UsedBytes = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Domains.Add(domain);
        _dbContext.Mailboxes.Add(mailbox);
        _dbContext.Folders.Add(new Folder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "INBOX", Role = "inbox",
            UidNext = 1, UidValidity = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var authorization = new AllowTestAuthorization();
        var allocator = new Miautrix.Mail.Application.Mail.MessageUidAllocator(_dbContext);
        var mailboxes = new Miautrix.Mail.Application.Mail.MailboxService(_dbContext, authorization, new Miautrix.Mail.Persistence.EfPermissionRepository(_dbContext));
        var messageService = new Miautrix.Mail.Application.Mail.MessageService(_dbContext, authorization, mailboxes, allocator, _storage,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Miautrix.Mail.Application.Mail.MessageService>.Instance,
            mimeParser: new Miautrix.Mail.Infrastructure.Mail.MimeKitMimeMessageParser());
        var session = new ImapSession(_dbContext, _storage, new StubImapAuthenticator(tenantId, mailboxId), authorization,
            mailboxService: mailboxes, messageService: messageService, uidAllocator: allocator);

        // Act 1: Login
        var loginResult = await session.ExecuteCommandAsync($"a001 LOGIN \"{address}\" \"secret\"");
        Assert.Equal("OK", loginResult.Status);

        // Act 2: Select INBOX
        var selectResult = await session.ExecuteCommandAsync("a002 SELECT INBOX");
        Assert.Equal("OK", selectResult.Status);
        Assert.NotNull(session.SelectedFolder);

        // Act 3: Append Message with custom flags (\Seen \Flagged)
        var rawMime = "From: sender@example.com\r\nTo: " + address + "\r\nSubject: Test Subject\r\n\r\nHello IMAP body fidelity!";
        var rawMimeBytes = Encoding.UTF8.GetBytes(rawMime);

        using var appendStream = new MemoryStream(rawMimeBytes);
        var appendResult = await session.ExecuteCommandAsync(
            $"a003 APPEND \"INBOX\" (\\Seen \\Flagged) {{{rawMimeBytes.Length}}}",
            literalPayload: appendStream);

        Assert.Equal("OK", appendResult.Status);
        Assert.Contains("APPENDUID", appendResult.Message);

        // Act 4: UID FETCH 1 (FLAGS RFC822)
        var fetchResult = await session.ExecuteCommandAsync("a004 UID FETCH 1 (FLAGS RFC822)");
        Assert.Equal("OK", fetchResult.Status);
        var literalSegment = Assert.Single(fetchResult.ResponseSegments, s => s.Bytes.SequenceEqual(rawMimeBytes));
        var retrievedBody = Encoding.UTF8.GetString(literalSegment.Bytes);
        Assert.Equal(rawMime, retrievedBody);

        // Verify untagged responses contain preserved flags
        var fetchUntagged = Encoding.UTF8.GetString(fetchResult.ResponseSegments[0].Bytes);
        Assert.Contains("\\Seen", fetchUntagged);
        Assert.Contains("\\Flagged", fetchUntagged);
    }

    [Fact]
    public async Task Imap_SharedNamespace_SelectsTheDelegatedMailbox_NotTheLoginMailbox()
    {
        var tenantId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var ownMailboxId = Guid.NewGuid();
        var sharedMailboxId = Guid.NewGuid();
        var otherSharedMailboxId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant { Id = tenantId, Slug = $"tenant-{Guid.NewGuid():N}", CreatedAt = now, UpdatedAt = now };
        var domain = new DomainEntity { Id = domainId, TenantId = tenantId, Name = $"imap-{Guid.NewGuid():N}.local", CreatedAt = now, UpdatedAt = now };
        var own = new Mailbox { Id = ownMailboxId, TenantId = tenantId, DomainId = domainId, Address = $"user@{domain.Name}", Kind = "user", QuotaBytes = 1000000, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var shared = new Mailbox { Id = sharedMailboxId, TenantId = tenantId, DomainId = domainId, Address = $"sales@{domain.Name}", Kind = "shared", QuotaBytes = 1000000, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var otherShared = new Mailbox { Id = otherSharedMailboxId, TenantId = tenantId, DomainId = domainId, Address = $"other@{domain.Name}", Kind = "shared", QuotaBytes = 1000000, IsActive = true, CreatedAt = now, UpdatedAt = now };
        _dbContext.AddRange(tenant, domain, own, shared,
            new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = ownMailboxId, Name = "INBOX", Role = "inbox", UidNext = 1, UidValidity = 11, CreatedAt = now, UpdatedAt = now },
            otherShared,
            new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = sharedMailboxId, Name = "INBOX", Role = "inbox", UidNext = 1, UidValidity = 22, CreatedAt = now, UpdatedAt = now },
            new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = otherSharedMailboxId, Name = "INBOX", Role = "inbox", UidNext = 1, UidValidity = 33, CreatedAt = now, UpdatedAt = now });
        await _dbContext.SaveChangesAsync();

        var authz = new RecordingMailboxAuthorization(sharedMailboxId);
        var mailboxes = new Miautrix.Mail.Application.Mail.MailboxService(_dbContext, authz, new EfPermissionRepository(_dbContext));
        var allocator = new Miautrix.Mail.Application.Mail.MessageUidAllocator(_dbContext);
        var messageService = new Miautrix.Mail.Application.Mail.MessageService(_dbContext, authz, mailboxes, allocator, _storage,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Miautrix.Mail.Application.Mail.MessageService>.Instance,
            mimeParser: new Miautrix.Mail.Infrastructure.Mail.MimeKitMimeMessageParser());
        var session = new ImapSession(_dbContext, _storage, new StubImapAuthenticator(tenantId, ownMailboxId), authz, mailboxes,
            messageService: messageService, uidAllocator: allocator);
        Assert.Equal("OK", (await session.ExecuteCommandAsync($"a001 LOGIN \"{own.Address}\" \"secret\"")).Status);

        var list = await session.ExecuteCommandAsync("a001b LIST \"\" \"*\"");
        var listed = string.Join("\n", list.ResponseSegments.Select(segment => Encoding.UTF8.GetString(segment.Bytes)));
        Assert.Contains($"Shared/{shared.Address}/INBOX", listed);
        Assert.DoesNotContain($"Shared/{otherShared.Address}/INBOX", listed);

        var denied = await session.ExecuteCommandAsync($"a002 SELECT Shared/unknown@{domain.Name}/INBOX");
        Assert.Equal("NO", denied.Status);
        Assert.Contains("Mailbox does not exist", denied.Message);

        var unauthorized = await session.ExecuteCommandAsync($"a002b SELECT Shared/{otherShared.Address}/INBOX");
        Assert.Equal("NO", unauthorized.Status);
        Assert.Contains("Mailbox does not exist", unauthorized.Message);

        var selected = await session.ExecuteCommandAsync($"a003 SELECT Shared/{shared.Address}/INBOX");
        Assert.Equal("OK", selected.Status);
        Assert.Equal(sharedMailboxId, session.SelectedFolder!.MailboxId);
        Assert.Equal(ownMailboxId, session.MailboxId);
        Assert.Contains((sharedMailboxId, false), authz.Checks);

        var raw = Encoding.UTF8.GetBytes($"From: sender@example.test\r\nTo: {shared.Address}\r\nSubject: Shared case\r\n\r\nshared body");
        using var literal = new MemoryStream(raw);
        var append = await session.ExecuteCommandAsync($"a004 APPEND Shared/{shared.Address}/INBOX {{{raw.Length}}}", literal);
        Assert.Equal("OK", append.Status);
        var search = await session.ExecuteCommandAsync("a005 SEARCH SUBJECT \"Shared case\"");
        Assert.Equal("OK", search.Status);
        Assert.Contains("* SEARCH 1", string.Join("\n", search.ResponseSegments.Select(segment => Encoding.UTF8.GetString(segment.Bytes))));
        var fetch = await session.ExecuteCommandAsync("a006 FETCH 1 (UID RFC822.SIZE BODY.PEEK[])");
        Assert.Equal("OK", fetch.Status);
        Assert.Contains(fetch.ResponseSegments, segment => segment.Bytes.SequenceEqual(raw));
        var fetchHeader = string.Join("", fetch.ResponseSegments.Select(segment => Encoding.UTF8.GetString(segment.Bytes)));
        Assert.Contains($"RFC822.SIZE {raw.Length}", fetchHeader);
        Assert.Contains("UID 1", fetchHeader);
    }

    [Fact]
    public async Task Imap_AppendDrafts_PopulatesWebmailDetailForMimeVariantsAndSharedDrafts()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var personalId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var address = $"drafts-{Guid.NewGuid():N}@miautrix.local";
        var sharedAddress = $"shared-{Guid.NewGuid():N}@miautrix.local";
        var now = DateTimeOffset.UtcNow;
        _dbContext.AddRange(
            new Tenant { Id = tenantId, Slug = "append-drafts-" + Guid.NewGuid().ToString("N") },
            new DomainEntity { Id = domainId, TenantId = tenantId, Name = "miautrix.local" },
            new Mailbox { Id = personalId, TenantId = tenantId, DomainId = domainId, Address = address, Kind = "user", IsActive = true },
            new Mailbox { Id = sharedId, TenantId = tenantId, DomainId = domainId, Address = sharedAddress, Kind = "shared", IsActive = true },
            new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = personalId, Name = "Drafts", Role = "drafts", UidNext = 1, UidValidity = 101 },
            new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = sharedId, Name = "Drafts", Role = "drafts", UidNext = 1, UidValidity = 102 },
            new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = personalId, Name = "INBOX", Role = "inbox", UidNext = 1, UidValidity = 103 });
        await _dbContext.SaveChangesAsync();

        var auth = new AllowTestAuthorization();
        var allocator = new Miautrix.Mail.Application.Mail.MessageUidAllocator(_dbContext);
        var mailboxes = new Miautrix.Mail.Application.Mail.MailboxService(_dbContext, auth, new Miautrix.Mail.Persistence.EfPermissionRepository(_dbContext));
        var service = new Miautrix.Mail.Application.Mail.MessageService(_dbContext, auth, mailboxes, allocator, _storage,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Miautrix.Mail.Application.Mail.MessageService>.Instance,
            mimeParser: new Miautrix.Mail.Infrastructure.Mail.MimeKitMimeMessageParser());
        var session = new ImapSession(_dbContext, _storage, new StubImapAuthenticator(tenantId, personalId, userId), auth,
            mailboxes, service, uidAllocator: allocator);
        Assert.Equal("OK", (await session.ExecuteCommandAsync($"d001 LOGIN \"{address}\" secret")).Status);

        var plain = $"From: sender@example.test\r\nTo: {address}\r\nCc: copy@example.test\r\nSubject: Plain draft\r\n\r\nplain body π";
        var html = $"From: sender@example.test\r\nTo: {address}\r\nSubject: HTML draft\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<b>html body</b>";
        var attachment = $"From: sender@example.test\r\nTo: {address}\r\nSubject: Multipart\r\nMIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=part42\r\n\r\n--part42\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nmultipart body\r\n--part42\r\nContent-Type: text/plain; name=note.txt\r\nContent-Disposition: attachment; filename=note.txt\r\nContent-Transfer-Encoding: base64\r\n\r\ndGVzdCBmaWxl\r\n--part42--\r\n";
        var folded = $"From: sender@example.test\r\nTo: {address},\r\n other@example.test\r\nSubject: Café folded\r\n\r\nfolded body";
        await AppendToDraftsAsync("Drafts", plain, "\\Draft");
        await AppendToDraftsAsync("Drafts", html, "\\Draft");
        await AppendToDraftsAsync("Drafts", attachment, "\\Draft");
        await AppendToDraftsAsync("Drafts", folded, "\\Draft");
        await AppendToDraftsAsync($"Shared/{sharedAddress}/Drafts", plain.Replace("Plain draft", "Shared draft", StringComparison.Ordinal), "\\Draft");

        var personalDrafts = await _dbContext.Folders.SingleAsync(f => f.MailboxId == personalId && f.Role == "drafts");
        var messages = await _dbContext.Messages.Where(m => m.FolderId == personalDrafts.Id).OrderBy(m => m.Uid).ToListAsync();
        Assert.Equal(new[] { "Plain draft", "HTML draft", "Multipart", "Café folded" }, messages.Select(m => m.Subject));
        Assert.Equal("plain body π", messages[0].BodyText);
        Assert.Null(messages[1].BodyText);
        Assert.Contains("<b>html body</b>", messages[1].BodyHtml ?? string.Empty);
        Assert.Equal("multipart body", messages[2].BodyText);
        Assert.Equal("Café folded", messages[3].Subject);
        Assert.Equal("other@example.test", messages[3].Recipient.Split(',')[1].Trim());
        await using (var storedMessage = await _storage.OpenReadAsync(messages[2].ContentHash))
            Assert.Equal(messages[2].SizeBytes, storedMessage!.Length);
        var detail = await service.GetMessageAsync(tenantId, userId, personalId, messages[2].Id);
        Assert.NotNull(detail);
        Assert.Equal("multipart body", detail.BodyText);
        Assert.Contains("copy@example.test", (await service.GetMessageAsync(tenantId, userId, personalId, messages[0].Id))!.Cc!);
        Assert.Single(detail.Attachments);
        Assert.Equal("note.txt", detail.Attachments[0].FileName);
        var sharedDraft = await _dbContext.Messages.SingleAsync(m => m.MailboxId == sharedId);
        var sharedDetail = await service.GetMessageAsync(tenantId, userId, sharedId, sharedDraft.Id);
        Assert.NotNull(sharedDetail);
        Assert.Equal("Shared draft", sharedDetail.Subject);
        Assert.Contains(address, sharedDetail.Recipient);
        Assert.Contains("plain body", sharedDetail.BodyText);

        async Task AppendToDraftsAsync(string folderPath, string raw, string flags)
        {
            var bytes = Encoding.UTF8.GetBytes(raw);
            using var literal = new MemoryStream(bytes);
            var result = await session.ExecuteCommandAsync($"APP{Guid.NewGuid():N}"[..4] + $" APPEND \"{folderPath}\" ({flags}) {{{bytes.Length}}}", literal);
            Assert.Equal("OK", result.Status);
        }
    }

    [Fact]
    public async Task MessageReparse_BackfillsLegacyAppendDraftBodyFields()
    {
        var tenantId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var address = $"reparse-{Guid.NewGuid():N}@miautrix.local";
        _dbContext.AddRange(
            new Tenant { Id = tenantId, Slug = "reparse-" + Guid.NewGuid().ToString("N") },
            new DomainEntity { Id = domainId, TenantId = tenantId, Name = "miautrix.local" },
            new Mailbox { Id = mailboxId, TenantId = tenantId, DomainId = domainId, Address = address, IsActive = true },
            new Folder { Id = folderId, TenantId = tenantId, MailboxId = mailboxId, Name = "Drafts", Role = "drafts", UidNext = 2, UidValidity = 201 });
        await _dbContext.SaveChangesAsync();
        var raw = Encoding.UTF8.GetBytes($"From: sender@example.test\r\nTo: {address}\r\nSubject: old draft\r\n\r\nbackfilled body");
        await using var rawStream = new MemoryStream(raw, writable: false);
        var stored = await _storage.StoreAsync(rawStream);
        _dbContext.Messages.Add(new Message { Id = messageId, TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId,
            Sender = "sender@example.test", Recipient = address, Subject = "old draft", Date = now, ContentHash = stored.ContentHash,
            StoragePath = stored.StoragePath, SizeBytes = stored.SizeBytes, Flags = "\\Draft", Uid = 1,
            BodyText = null, BodyHtml = null, CreatedAt = now, UpdatedAt = now });
        await _dbContext.SaveChangesAsync();

        var parser = new Miautrix.Mail.Infrastructure.Mail.MimeKitMimeMessageParser();
        var reparse = new Miautrix.Mail.Application.Mail.MessageReparseService(_dbContext, _storage, parser, new AllowTestAuthorization());
        var result = await reparse.ReparseAsync(tenantId, null, null, dryRun: false);

        Assert.Equal(1, result.Updated);
        var backfilled = await _dbContext.Messages.SingleAsync(m => m.Id == messageId);
        Assert.Equal("backfilled body", backfilled.BodyText?.TrimEnd());
    }

    [Fact]
    public async Task SharedDraftSave_RequiresWriteDelegateButDoesNotRequireSendAs()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var address = $"shared-drafts-{Guid.NewGuid():N}@miautrix.local";
        _dbContext.AddRange(new Tenant { Id = tenantId, Slug = "shared-drafts-" + Guid.NewGuid().ToString("N") },
            new DomainEntity { Id = domainId, TenantId = tenantId, Name = "miautrix.local" },
            new Mailbox { Id = mailboxId, TenantId = tenantId, DomainId = domainId, Address = address, Kind = "shared", IsActive = true },
            new Folder { Id = folderId, TenantId = tenantId, MailboxId = mailboxId, Name = "Drafts", Role = "drafts", UidNext = 1, UidValidity = 301 });
        await _dbContext.SaveChangesAsync();
        var access = new TestDelegatePermissionRepository("read");
        var auth = new Miautrix.Mail.Security.TenantAuthorizationHelper(access, new Miautrix.Mail.Identity.InMemorySecurityEventSink());
        var allocator = new Miautrix.Mail.Application.Mail.MessageUidAllocator(_dbContext);
        var mailboxes = new Miautrix.Mail.Application.Mail.MailboxService(_dbContext, auth, new Miautrix.Mail.Persistence.EfPermissionRepository(_dbContext));
        var service = new Miautrix.Mail.Application.Mail.MessageService(_dbContext, auth, mailboxes, allocator, _storage,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Miautrix.Mail.Application.Mail.MessageService>.Instance,
            mimeParser: new Miautrix.Mail.Infrastructure.Mail.MimeKitMimeMessageParser());
        var request = new Miautrix.Mail.Application.Mail.DraftMessageRequest(null, address, Array.Empty<string>(), null, null, "Shared draft", "saved without Send As", null);

        await Assert.ThrowsAsync<Miautrix.Mail.Security.ResourceNotFoundException>(() => service.UpsertDraftAsync(tenantId, userId, mailboxId, request));
        Assert.Empty(await _dbContext.Messages.Where(m => m.MailboxId == mailboxId).ToListAsync());
        access.Access = "write";
        var saved = await service.UpsertDraftAsync(tenantId, userId, mailboxId, request);
        Assert.True(saved.Success);
        Assert.Equal(1, await _dbContext.Messages.CountAsync(m => m.MailboxId == mailboxId && m.FolderId == folderId));
    }

    /// <summary>
    /// Stands in for credential verification so the APPEND/FETCH tests stay focused on
    /// protocol behaviour. Credential checks themselves are covered by
    /// <see cref="ImapAuthenticatorTests"/>.
    /// </summary>
    private sealed class StubImapAuthenticator : IImapAuthenticator
    {
        private readonly Guid _tenantId;
        private readonly Guid _mailboxId;
        private readonly Guid _userId;

        public StubImapAuthenticator(Guid tenantId, Guid mailboxId, Guid? userId = null)
        {
            _tenantId = tenantId;
            _mailboxId = mailboxId;
            _userId = userId ?? Guid.NewGuid();
        }

        public Task<ImapAuthenticationResult> AuthenticateAsync(
            string username, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(new ImapAuthenticationResult(true, _tenantId, _mailboxId, _userId));
    }

    private sealed class AllowTestAuthorization : ITenantAuthorizationHelper
    {
        public TResource AuthorizeAccess<TResource>(Guid currentTenantId, Guid currentUserId, TResource? resource, string requiredPermissionCode) where TResource : TenantScopedEntityBase => resource!;
        public void AssertPermission(Guid tenantId, Guid userId, string permissionCode) { }
        public void AssertMailboxAccess(Guid tenantId, Guid userId, Mailbox? mailbox, bool requireWrite) { if (mailbox is null) throw new ResourceNotFoundException(); }
        public void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId) { }
    }

    private sealed class RecordingMailboxAuthorization(Guid allowedMailboxId) : ITenantAuthorizationHelper
    {
        public List<(Guid MailboxId, bool RequireWrite)> Checks { get; } = new();
        public TResource AuthorizeAccess<TResource>(Guid currentTenantId, Guid currentUserId, TResource? resource, string requiredPermissionCode) where TResource : TenantScopedEntityBase => resource!;
        public void AssertPermission(Guid tenantId, Guid userId, string permissionCode) { }
        public void AssertMailboxAccess(Guid tenantId, Guid userId, Mailbox? mailbox, bool requireWrite)
        {
            if (mailbox is null || mailbox.Id != allowedMailboxId) throw new ResourceNotFoundException();
            Checks.Add((mailbox.Id, requireWrite));
        }
        public void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId) { }
    }

    private sealed class TestDelegatePermissionRepository(string? access) : Miautrix.Mail.Security.IPermissionRepository
    {
        public string? Access { get; set; } = access;
        public bool HasPermission(Guid tenantId, Guid userId, string permissionCode) => false;
        public int GetTenantOwnerCount(Guid tenantId) => 0;
        public bool IsUserTenantOwner(Guid tenantId, Guid userId) => false;
        public bool IsMailboxOwner(Guid tenantId, Guid userId, Guid mailboxId) => false;
        public string? GetMailboxDelegateAccess(Guid tenantId, Guid userId, Guid mailboxId) => Access;
        public string GetMailboxEffectiveAccess(Guid tenantId, Guid userId, Mailbox mailbox) => Access ?? "none";
    }
}
