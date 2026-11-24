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

        var session = new ImapSession(_dbContext, _storage, new StubImapAuthenticator(tenantId, mailboxId), new AllowTestAuthorization(),
            uidAllocator: new Miautrix.Mail.Application.Mail.MessageUidAllocator(_dbContext));

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
        var session = new ImapSession(_dbContext, _storage, new StubImapAuthenticator(tenantId, ownMailboxId), authz, mailboxes,
            uidAllocator: new Miautrix.Mail.Application.Mail.MessageUidAllocator(_dbContext));
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

    /// <summary>
    /// Stands in for credential verification so the APPEND/FETCH tests stay focused on
    /// protocol behaviour. Credential checks themselves are covered by
    /// <see cref="ImapAuthenticatorTests"/>.
    /// </summary>
    private sealed class StubImapAuthenticator : IImapAuthenticator
    {
        private readonly Guid _tenantId;
        private readonly Guid _mailboxId;

        public StubImapAuthenticator(Guid tenantId, Guid mailboxId)
        {
            _tenantId = tenantId;
            _mailboxId = mailboxId;
        }

        public Task<ImapAuthenticationResult> AuthenticateAsync(
            string username, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(new ImapAuthenticationResult(true, _tenantId, _mailboxId, Guid.NewGuid()));
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
}
