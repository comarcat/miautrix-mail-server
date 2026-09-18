using System.Diagnostics;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Search;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests.Search;

[Trait("Category", "Search")]
public sealed class SearchTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly PostgreSqlSearchProvider _searchProvider;

    public SearchTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _dbContext = new AppDbContext(options);
        _searchProvider = new PostgreSqlSearchProvider(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Message_WhenDelivered_IsFindableByFullTextQueryWithinOneSecond()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var uniqueKeyword = $"ProjectZeus_{Guid.NewGuid():N}";

        var tenant = new Tenant
        {
            Id = tenantId,
            Slug = $"tenant-search-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var mailbox = new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            Address = $"user_{Guid.NewGuid():N}@miautrix.local",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var folder = new Folder
        {
            Id = folderId,
            TenantId = tenantId,
            MailboxId = mailboxId,
            Name = "INBOX",
            Role = "inbox",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Mailboxes.Add(mailbox);
        _dbContext.Folders.Add(folder);
        await _dbContext.SaveChangesAsync();

        var message = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailboxId,
            FolderId = folderId,
            Sender = "ceo@enterprise.com",
            Recipient = mailbox.Address,
            Subject = $"Confidential Roadmap: {uniqueKeyword}",
            BodyText = $"Hello, please find the quarterly deliverables regarding {uniqueKeyword} attached.",
            ContentHash = "dummyhash123",
            StoragePath = "/tmp/dummy",
            SizeBytes = 1024,
            Date = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Act: Deliver / Index message
        var stopwatch = Stopwatch.StartNew();
        await _searchProvider.IndexMessageAsync(tenantId, message);

        // Query by full-text token
        var searchResults = await _searchProvider.SearchAsync(tenantId, mailboxId, uniqueKeyword);
        stopwatch.Stop();

        // Assert: Findable within 1 second (1000 ms)
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Search took {stopwatch.ElapsedMilliseconds} ms, expected < 1000 ms");
        Assert.NotEmpty(searchResults);
        Assert.Single(searchResults);
        Assert.Equal(message.Id, searchResults[0].Id);
        Assert.Contains(uniqueKeyword, searchResults[0].Subject);
    }
}
