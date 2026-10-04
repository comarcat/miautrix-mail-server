using System.Text.Json;
using Miautrix.Mail.Domain;
using Miautrix.Mail.MailFlow;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests.MailFlow;

[Trait("Category", "Rules")]
public sealed class MailFlowTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly MailFlowEngine _engine;

    public MailFlowTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _dbContext = new AppDbContext(options);
        _engine = new MailFlowEngine(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task MailFlow_Simulation_ReportsMatchesAndProposedActions_WithoutMutatingMessages()
    {
        // Arrange: Create tenant and mail flow rules
        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Slug = $"tenant-flow-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Tenants.Add(tenant);

        var rule1Conditions = new List<RuleCondition>
        {
            new("Subject", "Contains", "Invoice")
        };
        var rule1Actions = new List<RuleAction>
        {
            new("PrependSubject", "[FINANCE] "),
            new("AddHeader", "X-Miautrix-Category: Accounting")
        };

        var rule2Conditions = new List<RuleCondition>
        {
            new("SpamScore", "GreaterThan", "4.0")
        };
        var rule2Actions = new List<RuleAction>
        {
            new("Quarantine", "SpamScoreThresholdExceeded")
        };

        var rule1 = new MailFlowRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Finance Tagging Rule",
            Priority = 1,
            IsEnabled = true,
            ConditionsJson = JsonSerializer.Serialize(rule1Conditions),
            ActionsJson = JsonSerializer.Serialize(rule1Actions),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var rule2 = new MailFlowRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Spam Quarantine Rule",
            Priority = 2,
            IsEnabled = true,
            ConditionsJson = JsonSerializer.Serialize(rule2Conditions),
            ActionsJson = JsonSerializer.Serialize(rule2Actions),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.MailFlowRules.AddRange(rule1, rule2);
        await _dbContext.SaveChangesAsync();

        // Sample message context
        var originalSubject = "Urgent: January Invoice Due";
        var originalHeaders = new Dictionary<string, string> { { "X-Priority", "High" } };

        var sampleMessage = new MailFlowMessageContext(
            Sender: "billing@vendor.com",
            Recipient: "accounting@company.local",
            Subject: originalSubject,
            Body: "Please find attached the January invoice.",
            Headers: originalHeaders,
            HasAttachment: true,
            SpamScore: 2.1
        );

        // Count existing messages in DB before simulation (scoped to this tenant so
        // parallel tests writing to the shared database cannot skew the assertion).
        int initialMessageCount = await _dbContext.Messages.CountAsync(m => m.TenantId == tenantId);

        // Act: Run dry-run simulation
        var simulationResult = await _engine.SimulateAsync(tenantId, sampleMessage);

        // Assert:
        // 1. Simulation reports match for Rule 1 (Finance Tagging)
        Assert.True(simulationResult.HasMatches);
        Assert.Equal(2, simulationResult.Matches.Count);

        var match1 = simulationResult.Matches.First(m => m.RuleId == rule1.Id);
        Assert.True(match1.IsMatch);
        Assert.Equal(2, match1.ProposedActions.Count);
        Assert.Contains(match1.ProposedActions, a => a.ActionType == "PrependSubject" && a.Parameter == "[FINANCE] ");
        Assert.Contains(match1.ProposedActions, a => a.ActionType == "AddHeader" && a.Parameter == "X-Miautrix-Category: Accounting");

        // 2. Simulation reports no match for Rule 2 (Spam score 2.1 <= 4.0)
        var match2 = simulationResult.Matches.First(m => m.RuleId == rule2.Id);
        Assert.False(match2.IsMatch);
        Assert.Empty(match2.ProposedActions);

        // 3. Fired actions list matches Rule 1 actions
        Assert.Equal(2, simulationResult.FiredActions.Count);

        // 4. Input message and database messages are untouched / zero mutation
        Assert.Equal(originalSubject, sampleMessage.Subject);
        Assert.Single(sampleMessage.Headers);
        int finalMessageCount = await _dbContext.Messages.CountAsync(m => m.TenantId == tenantId);
        Assert.Equal(initialMessageCount, finalMessageCount);
    }
}
