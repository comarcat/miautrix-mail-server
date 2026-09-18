using System.Text.Json;
using System.Text.RegularExpressions;
using Miautrix.Mail.Domain;

namespace Miautrix.Mail.AntiSpam;

public interface ISpamProvider
{
    Task<SpamVerdict> EvaluateAsync(Guid tenantId, InboundMailContext mail, double threshold = 5.0, CancellationToken cancellationToken = default);
}

public sealed partial class RuleBasedSpamProvider : ISpamProvider
{
    private static readonly HashSet<string> KnownDnsblIps = new(StringComparer.OrdinalIgnoreCase)
    {
        "198.51.100.99",
        "203.0.113.55",
        "192.0.2.200"
    };

    [GeneratedRegex(@"(?i)\b(viagra|cialis|lottery\s+winner|crypto\s+giveaway|free\s+money|wire\s+transfer\s+urgently|account\s+suspended\s+verify\s+now|claim\s+your\s+prize|nigerian\s+prince)\b", RegexOptions.Compiled)]
    private static partial Regex HighRiskKeywordsRegex();

    [GeneratedRegex(@"[!$?]{3,}", RegexOptions.Compiled)]
    private static partial Regex ExcessivePunctuationRegex();

    public Task<SpamVerdict> EvaluateAsync(Guid tenantId, InboundMailContext mail, double threshold = 5.0, CancellationToken cancellationToken = default)
    {
        var rules = new List<RuleMatch>();
        double totalScore = 0.0;
        bool dnsblListed = false;

        // 1. DNSBL lookup check
        if (!string.IsNullOrWhiteSpace(mail.ClientIp) && KnownDnsblIps.Contains(mail.ClientIp))
        {
            dnsblListed = true;
            rules.Add(new RuleMatch("DNSBL_LISTED", 5.5, $"IP {mail.ClientIp} is listed on real-time DNSBL blocklist"));
            totalScore += 5.5;
        }

        // 2. SPF Result check
        if (string.Equals(mail.SpfResult, "Fail", StringComparison.OrdinalIgnoreCase))
        {
            rules.Add(new RuleMatch("SPF_FAIL", 2.5, "Sender SPF validation failed"));
            totalScore += 2.5;
        }
        else if (string.Equals(mail.SpfResult, "SoftFail", StringComparison.OrdinalIgnoreCase))
        {
            rules.Add(new RuleMatch("SPF_SOFTFAIL", 1.0, "Sender SPF returned SoftFail"));
            totalScore += 1.0;
        }

        // 3. DKIM Result check
        if (string.Equals(mail.DkimResult, "Fail", StringComparison.OrdinalIgnoreCase))
        {
            rules.Add(new RuleMatch("DKIM_FAIL", 2.5, "DKIM cryptographic signature verification failed"));
            totalScore += 2.5;
        }

        // 4. DMARC Result check
        if (string.Equals(mail.DmarcResult, "Fail", StringComparison.OrdinalIgnoreCase))
        {
            rules.Add(new RuleMatch("DMARC_FAIL", 3.0, "DMARC domain alignment policy failed"));
            totalScore += 3.0;
        }

        // 5. Subject & Body keyword heuristics
        var textToScan = $"{mail.Subject}\n{mail.RawMessage}";
        var keywordMatches = HighRiskKeywordsRegex().Matches(textToScan);
        if (keywordMatches.Count > 0)
        {
            double keywordScore = Math.Min(keywordMatches.Count * 2.5, 7.5);
            var words = string.Join(", ", keywordMatches.Select(m => m.Value).Distinct());
            rules.Add(new RuleMatch("SPAM_KEYWORDS", keywordScore, $"Suspicious keywords matched: {words}"));
            totalScore += keywordScore;
        }

        // 6. Excessive punctuation / shouting
        if (ExcessivePunctuationRegex().IsMatch(textToScan))
        {
            rules.Add(new RuleMatch("EXCESSIVE_PUNCTUATION", 1.5, "Excessive punctuation (!!!, ???, $$$) detected"));
            totalScore += 1.5;
        }

        if (!string.IsNullOrWhiteSpace(mail.Subject) && mail.Subject.Length > 10)
        {
            int uppercaseCount = mail.Subject.Count(char.IsUpper);
            double uppercaseRatio = (double)uppercaseCount / mail.Subject.Length;
            if (uppercaseRatio > 0.6)
            {
                rules.Add(new RuleMatch("ALL_CAPS_SUBJECT", 2.0, "Subject contains > 60% uppercase characters"));
                totalScore += 2.0;
            }
        }

        bool isSpam = totalScore >= threshold;

        var verdict = new SpamVerdict
        {
            TenantId = tenantId,
            Sender = mail.Sender,
            Recipient = mail.Recipient,
            Score = totalScore,
            Threshold = threshold,
            IsSpam = isSpam,
            ReasonsJson = JsonSerializer.Serialize(rules),
            DnsblListed = dnsblListed,
            Greylisted = false,
            SpfResult = mail.SpfResult,
            DkimResult = mail.DkimResult,
            DmarcResult = mail.DmarcResult,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(verdict);
    }
}
