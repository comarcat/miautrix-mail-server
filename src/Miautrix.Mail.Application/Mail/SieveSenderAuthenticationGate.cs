using System.Text.RegularExpressions;

namespace Miautrix.Mail.Application.Mail;

/// <summary>Trusts only this server's first Authentication-Results field before generating automated replies.</summary>
public sealed class SieveSenderAuthenticationGate(string authservId)
{
    private static readonly Regex SpfPass = new(@"(?:^|;)\s*spf\s*=\s*pass\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DmarcPass = new(@"(?:^|;)\s*dmarc\s*=\s*pass\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DkimPass = new(@"^\s*dkim\s*=\s*pass\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex HeaderDomain = new(@"(?:^|\s)header\.d\s*=\s*([^\s;]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public bool IsAuthenticated(string rawHeaders, string? envelopeSender)
    {
        var first = FirstHeader(rawHeaders, "Authentication-Results");
        if (first is null) return false;
        var clauses = first.Split(';');
        if (clauses.Length < 2) return false;
        var id = clauses[0].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.Equals(id, authservId, StringComparison.OrdinalIgnoreCase)) return false;
        if (SpfPass.IsMatch(first) || DmarcPass.IsMatch(first)) return true;
        var envelopeDomain = GetDomain(envelopeSender);
        if (envelopeDomain is null) return false;
        return clauses.Skip(1).Any(clause =>
        {
            if (!DkimPass.IsMatch(clause)) return false;
            var match = HeaderDomain.Match(clause);
            return match.Success && string.Equals(match.Groups[1].Value.TrimEnd('.'), envelopeDomain, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string? FirstHeader(string rawHeaders, string name)
    {
        var lines = rawHeaders.Split(["\r\n", "\n"], StringSplitOptions.None);
        string? value = null;
        foreach (var line in lines)
        {
            if (line.Length == 0) break;
            if ((line[0] == ' ' || line[0] == '\t') && value is not null)
            {
                value += " " + line.Trim();
                continue;
            }
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            if (value is not null) return value;
            if (line[..colon].Equals(name, StringComparison.OrdinalIgnoreCase)) value = line[(colon + 1)..].Trim();
        }
        return value;
    }

    private static string? GetDomain(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var normalized = address.Trim().Trim('<', '>');
        var at = normalized.LastIndexOf('@');
        if (at <= 0 || at == normalized.Length - 1) return null;
        return normalized[(at + 1)..].TrimEnd('.');
    }
}
