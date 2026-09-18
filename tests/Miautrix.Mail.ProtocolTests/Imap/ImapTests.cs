using System.Text;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Storage;
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
            ?? "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";

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
        await _dbContext.SaveChangesAsync();

        var session = new ImapSession(_dbContext, _storage);

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
        Assert.NotNull(fetchResult.BinaryPayload);

        var retrievedBody = Encoding.UTF8.GetString(fetchResult.BinaryPayload);
        Assert.Equal(rawMime, retrievedBody);

        // Verify untagged responses contain preserved flags
        var fetchUntagged = string.Join("\n", fetchResult.UntaggedResponses);
        Assert.Contains("\\Seen", fetchUntagged);
        Assert.Contains("\\Flagged", fetchUntagged);
    }
}
