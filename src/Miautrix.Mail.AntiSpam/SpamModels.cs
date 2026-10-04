using System.Text.Json.Serialization;

namespace Miautrix.Mail.AntiSpam;

public record InboundMailContext(
    string Sender,
    string Recipient,
    string? ClientIp,
    string? Subject,
    string RawMessage,
    string? SpfResult = null,
    string? DkimResult = null,
    string? DmarcResult = null);

public record RuleMatch(string RuleName, double Score, string Reason);

public record InboundProcessResult(
    bool Delivered,
    bool Quarantined,
    Guid? QuarantineItemId,
    double Score,
    double Threshold,
    IReadOnlyList<RuleMatch> MatchedRules);
