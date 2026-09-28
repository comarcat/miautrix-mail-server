using System.Text.Json;
using System.Text.RegularExpressions;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Sieve;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Application.Mail;

public sealed class SieveRuleService : ISieveRuleService
{
    private const string BeginMarker = "# MIAUTRIX-MANAGED-RULES-BEGIN";
    private const string EndMarker = "# MIAUTRIX-MANAGED-RULES-END";
    private const string RulePrefix = "# MIAUTRIX-RULE ";
    private static readonly Regex ManagedBlock = new(
        $"{Regex.Escape(BeginMarker)}.*?{Regex.Escape(EndMarker)}",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _auth;
    private readonly SieveScriptService _sieveScripts;

    public SieveRuleService(AppDbContext db, ITenantAuthorizationHelper auth, SieveScriptService sieveScripts)
    {
        _db = db;
        _auth = auth;
        _sieveScripts = sieveScripts;
    }

    public async Task<IReadOnlyList<SieveFilterRuleDto>> ListRulesAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken cancellationToken = default)
    {
        await AssertMailboxAccessAsync(tenantId, userId, mailboxId, requireWrite: false, cancellationToken);
        var active = await _sieveScripts.GetActiveScriptAsync(mailboxId, cancellationToken);
        return active is null ? [] : ReadManagedRules(active.Content);
    }

    public async Task<SieveFilterRuleDto> CreateRuleAsync(Guid tenantId, Guid userId, Guid mailboxId, SieveFilterRuleRequest request, CancellationToken cancellationToken = default)
    {
        await AssertMailboxAccessAsync(tenantId, userId, mailboxId, requireWrite: true, cancellationToken);
        Validate(request);
        var rules = await GetMutableRulesAsync(mailboxId, cancellationToken);
        var rule = ToDto(Guid.NewGuid(), request);
        rules.Add(rule);
        await SaveRulesAsync(tenantId, mailboxId, rules, cancellationToken);
        return rule;
    }

    public async Task<SieveFilterRuleDto?> UpdateRuleAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid ruleId, SieveFilterRuleRequest request, CancellationToken cancellationToken = default)
    {
        await AssertMailboxAccessAsync(tenantId, userId, mailboxId, requireWrite: true, cancellationToken);
        Validate(request);
        var rules = await GetMutableRulesAsync(mailboxId, cancellationToken);
        var index = rules.FindIndex(r => r.Id == ruleId);
        if (index < 0)
        {
            return null;
        }

        var rule = ToDto(ruleId, request);
        rules[index] = rule;
        await SaveRulesAsync(tenantId, mailboxId, rules, cancellationToken);
        return rule;
    }

    public async Task<bool> DeleteRuleAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        await AssertMailboxAccessAsync(tenantId, userId, mailboxId, requireWrite: true, cancellationToken);
        var rules = await GetMutableRulesAsync(mailboxId, cancellationToken);
        if (rules.RemoveAll(r => r.Id == ruleId) == 0)
        {
            return false;
        }

        await SaveRulesAsync(tenantId, mailboxId, rules, cancellationToken);
        return true;
    }

    public async Task<bool> SetRuleActiveAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid ruleId, bool active, CancellationToken cancellationToken = default)
    {
        await AssertMailboxAccessAsync(tenantId, userId, mailboxId, requireWrite: true, cancellationToken);
        var rules = await GetMutableRulesAsync(mailboxId, cancellationToken);
        var index = rules.FindIndex(r => r.Id == ruleId);
        if (index < 0)
        {
            return false;
        }

        rules[index] = rules[index] with { Active = active };
        await SaveRulesAsync(tenantId, mailboxId, rules, cancellationToken);
        return true;
    }

    private async Task<List<SieveFilterRuleDto>> GetMutableRulesAsync(Guid mailboxId, CancellationToken cancellationToken)
    {
        var active = await _sieveScripts.GetActiveScriptAsync(mailboxId, cancellationToken);
        return active is null ? [] : ReadManagedRules(active.Content).ToList();
    }

    private async Task SaveRulesAsync(Guid tenantId, Guid mailboxId, IReadOnlyList<SieveFilterRuleDto> rules, CancellationToken cancellationToken)
    {
        var active = await _sieveScripts.GetActiveScriptAsync(mailboxId, cancellationToken);
        var existingContent = active?.Content ?? string.Empty;
        var content = ReplaceManagedBlock(existingContent, rules);
        var result = await _sieveScripts.SetActiveScriptAsync(tenantId, mailboxId, "webmail-rules", content, cancellationToken);
        if (!result.Success)
        {
            throw new ArgumentException(result.ErrorMessage ?? "Generated Sieve script is invalid.");
        }
    }

    private async Task AssertMailboxAccessAsync(Guid tenantId, Guid userId, Guid mailboxId, bool requireWrite, CancellationToken cancellationToken)
    {
        var mailbox = await _db.Mailboxes.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite);
    }

    private static IReadOnlyList<SieveFilterRuleDto> ReadManagedRules(string content)
    {
        var block = ManagedBlock.Match(content);
        if (!block.Success)
        {
            return [];
        }

        var rules = new List<SieveFilterRuleDto>();
        foreach (var line in block.Value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith(RulePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var rule = JsonSerializer.Deserialize<SieveFilterRuleDto>(line[RulePrefix.Length..]);
                if (rule is not null)
                {
                    rules.Add(rule);
                }
            }
            catch (JsonException)
            {
                // A malformed managed comment is ignored instead of exposing arbitrary script text as a rule.
            }
        }

        return rules;
    }

    private static string ReplaceManagedBlock(string content, IReadOnlyList<SieveFilterRuleDto> rules)
    {
        var block = BuildManagedBlock(rules);
        if (ManagedBlock.IsMatch(content))
        {
            return ManagedBlock.Replace(content, block);
        }

        return string.IsNullOrWhiteSpace(content) ? block : $"{content.TrimEnd()}\n\n{block}";
    }

    private static string BuildManagedBlock(IReadOnlyList<SieveFilterRuleDto> rules)
    {
        var lines = new List<string> { BeginMarker };
        foreach (var rule in rules)
        {
            lines.Add($"{RulePrefix}{JsonSerializer.Serialize(rule)}");
            if (rule.Active)
            {
                lines.AddRange(Compile(rule));
            }
        }

        lines.Add(EndMarker);
        return string.Join("\n", lines);
    }

    private static IEnumerable<string> Compile(SieveFilterRuleDto rule)
    {
        var comparator = rule.Comparator switch
        {
            "contains" => ":contains",
            "is" => ":is",
            "matches" => ":matches",
            "exists" => null,
            _ => throw new ArgumentException("Unsupported Sieve comparator.")
        };
        var value = Escape(rule.Value);
        var test = rule.Field switch
        {
            "from" => comparator is null ? "exists [\"From\"]" : $"address {comparator} [\"From\"] \"{value}\"",
            "to" => comparator is null ? "exists [\"To\"]" : $"address {comparator} [\"To\"] \"{value}\"",
            "subject" => comparator is null ? "exists [\"Subject\"]" : $"header {comparator} \"Subject\" \"{value}\"",
            "header" => comparator is null ? "exists [\"{value}\"]" : $"header {comparator} \"Subject\" \"{value}\"",
            _ => throw new ArgumentException("Unsupported Sieve field.")
        };
        var action = rule.Action switch
        {
            "fileinto" when !string.IsNullOrWhiteSpace(rule.TargetFolder) => $"fileinto \"{Escape(rule.TargetFolder)}\";",
            "redirect" when !string.IsNullOrWhiteSpace(rule.TargetFolder) => $"redirect \"{Escape(rule.TargetFolder)}\";",
            "reject" => $"reject \"{Escape(rule.TargetFolder ?? "Rejected by rule")}\";",
            "addflag" when !string.IsNullOrWhiteSpace(rule.TargetFolder) => $"addflag \"{Escape(rule.TargetFolder)}\";",
            "discard" => "discard;",
            _ => throw new ArgumentException("Sieve action requires a target.")
        };

        return [$"if {test} {{", $"  {action}", "}"];
    }

    private static void Validate(SieveFilterRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Value))
        {
            throw new ArgumentException("Rule name and value are required.");
        }

        _ = Compile(ToDto(Guid.Empty, request));
    }

    private static SieveFilterRuleDto ToDto(Guid id, SieveFilterRuleRequest request) => new(
        id,
        request.Name.Trim(),
        request.Field.Trim().ToLowerInvariant(),
        request.Comparator.Trim().ToLowerInvariant(),
        request.Value.Trim(),
        request.Action.Trim().ToLowerInvariant(),
        string.IsNullOrWhiteSpace(request.TargetFolder) ? null : request.TargetFolder.Trim(),
        request.Active);

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
