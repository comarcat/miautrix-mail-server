using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Worker;
using Microsoft.EntityFrameworkCore;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.ProtocolTests.Imap;

/// <summary>
/// Credential verification for IMAP LOGIN. Regression cover for the defect where the command
/// handler accepted any password for an existing mailbox address.
/// </summary>
[Trait("Category", "Imap")]
public sealed class ImapAuthenticatorTests : IDisposable
{
    private const string CorrectPassword = "correct-horse-battery-staple";

    private readonly AppDbContext _dbContext;
    private readonly ImapAuthenticator _authenticator;
    private readonly List<Guid> _tenantIds = new();

    public ImapAuthenticatorTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _dbContext = new AppDbContext(options);
        _authenticator = new ImapAuthenticator(_dbContext, new Argon2idPasswordHasher());
    }

    public void Dispose()
    {
        // Leave the shared database as we found it.
        foreach (var tenantId in _tenantIds)
        {
            _dbContext.Mailboxes.RemoveRange(_dbContext.Mailboxes.Where(m => m.TenantId == tenantId));
            _dbContext.UserCredentials.RemoveRange(_dbContext.UserCredentials.Where(c => c.TenantId == tenantId));
            _dbContext.Users.RemoveRange(_dbContext.Users.Where(u => u.TenantId == tenantId));
            _dbContext.Domains.RemoveRange(_dbContext.Domains.Where(d => d.TenantId == tenantId));
            _dbContext.Tenants.RemoveRange(_dbContext.Tenants.Where(t => t.Id == tenantId));
            _dbContext.SaveChanges();
        }

        _dbContext.Dispose();
    }

    [Fact]
    public async Task Accepts_CorrectPassword()
    {
        var seed = await SeedAsync();

        var result = await _authenticator.AuthenticateAsync(seed.Address, CorrectPassword);

        Assert.True(result.IsSuccess);
        Assert.Equal(seed.TenantId, result.TenantId);
        Assert.Equal(seed.MailboxId, result.MailboxId);
    }

    [Fact]
    public async Task Rejects_WrongPassword()
    {
        var seed = await SeedAsync();

        var result = await _authenticator.AuthenticateAsync(seed.Address, "not-the-password");

        Assert.False(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.MailboxId);
    }

    [Fact]
    public async Task Rejects_EmptyPassword()
    {
        var seed = await SeedAsync();

        var result = await _authenticator.AuthenticateAsync(seed.Address, "");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Rejects_UnknownMailbox()
    {
        await SeedAsync();

        var result = await _authenticator.AuthenticateAsync($"ghost_{Guid.NewGuid():N}@miautrix.local", CorrectPassword);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Rejects_InactiveUser()
    {
        var seed = await SeedAsync();
        var user = await _dbContext.Users.FirstAsync(u => u.TenantId == seed.TenantId);
        user.IsActive = false;
        await _dbContext.SaveChangesAsync();

        var result = await _authenticator.AuthenticateAsync(seed.Address, CorrectPassword);

        Assert.False(result.IsSuccess);
    }

    /// <summary>
    /// A mailbox that exists in another tenant must never be reachable, even with valid
    /// credentials for a user in the first tenant.
    /// </summary>
    [Fact]
    public async Task Rejects_MailboxInAnotherTenant()
    {
        var seed = await SeedAsync();
        var other = await SeedAsync(address: seed.Address);

        // The address now exists in two tenants; each user must resolve to their own mailbox.
        var result = await _authenticator.AuthenticateAsync(seed.Address, CorrectPassword);

        Assert.True(result.IsSuccess);
        Assert.Equal(seed.TenantId, result.TenantId);
        Assert.Equal(seed.MailboxId, result.MailboxId);
        Assert.NotEqual(other.MailboxId, result.MailboxId);
    }

    private async Task<SeedResult> SeedAsync(string? address = null)
    {
        var tenantId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        address ??= $"user_{Guid.NewGuid():N}@miautrix.local";

        var now = DateTimeOffset.UtcNow;

        _dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Slug = $"tenant-{Guid.NewGuid():N}",
            CreatedAt = now,
            UpdatedAt = now,
        });

        _dbContext.Domains.Add(new DomainEntity
        {
            Id = domainId,
            TenantId = tenantId,
            Name = "miautrix.local",
            IsVerified = true,
            CreatedAt = now,
            UpdatedAt = now,
        });

        _dbContext.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = address,
            Name = "IMAP Test User",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });

        _dbContext.UserCredentials.Add(new UserCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Algorithm = "argon2id",
            PasswordHash = new Argon2idPasswordHasher().HashPassword(CorrectPassword),
            MustChangePassword = false,
            CreatedAt = now,
            UpdatedAt = now,
        });

        _dbContext.Mailboxes.Add(new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            DomainId = domainId,
            Address = address,
            Name = "IMAP Test Mailbox",
            Kind = "user",
            QuotaBytes = 1000000,
            UsedBytes = 0,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });

        await _dbContext.SaveChangesAsync();
        _tenantIds.Add(tenantId);

        return new SeedResult(tenantId, mailboxId, address);
    }

    private sealed record SeedResult(Guid TenantId, Guid MailboxId, string Address);
}
