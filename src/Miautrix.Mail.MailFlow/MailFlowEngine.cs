using System.Text.Json;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.MailFlow;

public sealed record RuleCondition(
    string Field,        // "Subject", "Sender", "Recipient", "Header", "HasAttachment", "SpamScore"
    string Operator,     // "Contains", "Equals", "StartsWith", "EndsWith", "GreaterThan"
    string Value
);

public sealed record RuleAction(
    string ActionType,   // "AddHeader", "PrependSubject", "Redirect", "Reject", "Quarantine", "StopProcessing"
    string? Parameter
);

public sealed record MailFlowMessageContext(
    string Sender,
    string Recipient,
    string Subject,
    string? Body,
    IReadOnlyDictionary<string, string> Headers,
    bool HasAttachment,
    double SpamScore
);

public sealed record SimulatedAction(
    string RuleName,
    string ActionType,
    string? Parameter
);

public sealed record RuleMatchReport(
    Guid RuleId,
    string RuleName,
    int Priority,
    bool IsMatch,
    IReadOnlyList<SimulatedAction> ProposedActions
);

public sealed record MailFlowSimulationResult(
    bool HasMatches,
    IReadOnlyList<RuleMatchReport> Matches,
    IReadOnlyList<SimulatedAction> FiredActions
);

public interface IMailFlowSimulator
{
    Task<MailFlowSimulationResult> SimulateAsync(
        Guid tenantId,
        MailFlowMessageContext message,
        CancellationToken cancellationToken = default);
}

public interface IMailFlowEngine
{
    Task<IReadOnlyList<SimulatedAction>> ExecuteAsync(
        Guid tenantId,
        MailFlowMessageContext message,
        CancellationToken cancellationToken = default);
}

public sealed class MailFlowEngine : IMailFlowSimulator, IMailFlowEngine
{
    private readonly AppDbContext _dbContext;

    public MailFlowEngine(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MailFlowSimulationResult> SimulateAsync(
        Guid tenantId,
        MailFlowMessageContext message,
        CancellationToken cancellationToken = default)
    {
        var rules = await _dbContext.MailFlowRules
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsEnabled)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);

        var matchReports = new List<RuleMatchReport>();
        var allFiredActions = new List<SimulatedAction>();

        foreach (var rule in rules)
        {
            var conditions = ParseConditions(rule.ConditionsJson);
            var actions = ParseActions(rule.ActionsJson);

            bool isMatch = EvaluateConditions(conditions, message);
            var proposed = new List<SimulatedAction>();

            if (isMatch)
            {
                foreach (var action in actions)
                {
                    var simulated = new SimulatedAction(rule.Name, action.ActionType, action.Parameter);
                    proposed.Add(simulated);
                    allFiredActions.Add(simulated);
                }
            }

            matchReports.Add(new RuleMatchReport(rule.Id, rule.Name, rule.Priority, isMatch, proposed));

            if (isMatch && proposed.Any(p => p.ActionType.Equals("StopProcessing", StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }
        }

        return new MailFlowSimulationResult(
            matchReports.Any(m => m.IsMatch),
            matchReports,
            allFiredActions
        );
    }

    public async Task<IReadOnlyList<SimulatedAction>> ExecuteAsync(
        Guid tenantId,
        MailFlowMessageContext message,
        CancellationToken cancellationToken = default)
    {
        var simulation = await SimulateAsync(tenantId, message, cancellationToken);
        return simulation.FiredActions;
    }

    private static List<RuleCondition> ParseConditions(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return new List<RuleCondition>();
        try
        {
            return JsonSerializer.Deserialize<List<RuleCondition>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new List<RuleCondition>();
        }
        catch
        {
            return new List<RuleCondition>();
        }
    }

    private static List<RuleAction> ParseActions(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return new List<RuleAction>();
        try
        {
            return JsonSerializer.Deserialize<List<RuleAction>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new List<RuleAction>();
        }
        catch
        {
            return new List<RuleAction>();
        }
    }

    private static bool EvaluateConditions(List<RuleCondition> conditions, MailFlowMessageContext message)
    {
        if (conditions.Count == 0) return true; // Rule without conditions matches everything

        foreach (var cond in conditions)
        {
            string actualValue = cond.Field.ToLowerInvariant() switch
            {
                "subject" => message.Subject,
                "sender" => message.Sender,
                "recipient" => message.Recipient,
                "hasattachment" => message.HasAttachment.ToString(),
                "spamscore" => message.SpamScore.ToString(),
                _ => message.Headers.TryGetValue(cond.Field, out var headerVal) ? headerVal : string.Empty
            };

            bool condMatch = cond.Operator.ToLowerInvariant() switch
            {
                "contains" => actualValue.Contains(cond.Value, StringComparison.OrdinalIgnoreCase),
                "equals" => string.Equals(actualValue, cond.Value, StringComparison.OrdinalIgnoreCase),
                "startswith" => actualValue.StartsWith(cond.Value, StringComparison.OrdinalIgnoreCase),
                "endswith" => actualValue.EndsWith(cond.Value, StringComparison.OrdinalIgnoreCase),
                "greaterthan" => double.TryParse(actualValue, out var actNum) &&
                                 double.TryParse(cond.Value, out var tgtNum) &&
                                 actNum > tgtNum,
                _ => false
            };

            if (!condMatch) return false;
        }

        return true;
    }
}
