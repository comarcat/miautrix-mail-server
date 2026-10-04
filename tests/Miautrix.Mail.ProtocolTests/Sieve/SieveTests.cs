using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Sieve;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.ProtocolTests.Sieve;

[Trait("Category", "Sieve")]
public sealed class SieveTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly SieveParser _parser;
    private readonly SieveScriptService _service;

    public SieveTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _dbContext = new AppDbContext(options);
        _parser = new SieveParser();
        _service = new SieveScriptService(_dbContext, _parser);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void Parser_DetectsInvalidSyntaxWithAccurateLineNumber()
    {
        // Line 1: valid require
        // Line 2: comment
        // Line 3: invalid token
        var script = @"require [""fileinto""];
# valid comment
invalid_token_command :contains ""Subject"" ""Urgent"";";

        var result = _parser.Validate(script);

        Assert.False(result.Success);
        Assert.Equal(3, result.ErrorLine);
        Assert.Contains("invalid_token_command", result.ErrorMessage);
    }

    [Fact]
    public void Parser_DetectsUnclosedBrace()
    {
        var script = @"require [""fileinto""];
if header :contains ""Subject"" ""SPAM"" {
    fileinto ""Junk"";
";

        var result = _parser.Validate(script);

        Assert.False(result.Success);
        Assert.Equal(4, result.ErrorLine);
    }

    [Fact]
    public async Task Upload_InvalidScript_RejectsWithLineNumber_AndLeavesActiveScriptUnchanged()
    {
        // Arrange: Create tenant, mailbox, and initial valid active script
        var tenantId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Slug = $"tenant-sieve-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var mailbox = new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            Address = $"sieve_user_{Guid.NewGuid():N}@miautrix.local",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Mailboxes.Add(mailbox);
        await _dbContext.SaveChangesAsync();

        var validInitialScript = @"require [""fileinto""];
if header :contains ""Subject"" ""Work"" {
    fileinto ""WorkFolder"";
}";

        var initialActivation = await _service.SetActiveScriptAsync(
            tenantId, mailboxId, "work-rules.sieve", validInitialScript);
        Assert.True(initialActivation.Success);

        var activeBefore = await _service.GetActiveScriptAsync(mailboxId);
        Assert.NotNull(activeBefore);
        Assert.Equal("work-rules.sieve", activeBefore.Name);
        Assert.Equal(validInitialScript, activeBefore.Content);

        // Act: Attempt to upload broken script with syntax error on line 2
        var brokenScript = @"require [""fileinto""];
broken_command_syntax without terminating semicolon
if header :contains ""Subject"" ""Alert"" {
    fileinto ""Alerts"";
}";

        var brokenActivation = await _service.SetActiveScriptAsync(
            tenantId, mailboxId, "broken-rules.sieve", brokenScript);

        // Assert
        Assert.False(brokenActivation.Success);
        Assert.Equal(2, brokenActivation.ErrorLine);

        // Verify active script is unchanged in database
        var activeAfter = await _service.GetActiveScriptAsync(mailboxId);
        Assert.NotNull(activeAfter);
        Assert.Equal(activeBefore.Id, activeAfter.Id);
        Assert.Equal("work-rules.sieve", activeAfter.Name);
        Assert.Equal(validInitialScript, activeAfter.Content);
        Assert.True(activeAfter.IsActive);
    }
}
