using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.Infrastructure.Mail;

internal sealed record TrustedAuthenticationResults(
    SpfAuthenticationResult? Spf,
    IReadOnlyList<DkimAuthenticationResult> Dkim,
    string? DmarcResult,
    string? FromDomain);

internal static class TrustedAuthenticationResultsParser
{
    private static readonly HashSet<string> Results = new(StringComparer.OrdinalIgnoreCase)
    {
        "pass", "fail", "softfail", "neutral", "temperror", "permerror", "none"
    };

    public static bool TryParse(string? value, IReadOnlySet<string> trustedAuthservIds, out TrustedAuthenticationResults parsed)
    {
        parsed = new(null, [], null, null);
        if (string.IsNullOrWhiteSpace(value) || trustedAuthservIds.Count == 0) return false;
        var segments = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !trustedAuthservIds.Contains(segments[0])) return false;

        SpfAuthenticationResult? spf = null;
        var dkim = new List<DkimAuthenticationResult>();
        string? dmarc = null;
        string? fromDomain = null;
        foreach (var segment in segments.Skip(1))
        {
            var fields = segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0) continue;
            var equals = fields[0].IndexOf('=');
            if (equals <= 0 || equals == fields[0].Length - 1) continue;
            var method = fields[0][..equals];
            var result = fields[0][(equals + 1)..].ToLowerInvariant();
            if (!Results.Contains(result)) continue;
            var properties = fields.Skip(1).Select(field => field.Split('=', 2)).Where(parts => parts.Length == 2)
                .GroupBy(parts => parts[0], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First()[1].Trim('<', '>'), StringComparer.OrdinalIgnoreCase);
            if (method.Equals("spf", StringComparison.OrdinalIgnoreCase))
            {
                var domain = properties.GetValueOrDefault("smtp.mailfrom") ?? properties.GetValueOrDefault("smtp.helo");
                if (domain?.Contains('@') == true) domain = domain[(domain.LastIndexOf('@') + 1)..];
                spf ??= new(result, domain, "trusted Authentication-Results");
            }
            else if (method.Equals("dkim", StringComparison.OrdinalIgnoreCase))
            {
                dkim.Add(new(properties.GetValueOrDefault("header.d"), properties.GetValueOrDefault("header.s"), result, "trusted Authentication-Results"));
            }
            else if (method.Equals("dmarc", StringComparison.OrdinalIgnoreCase))
            {
                dmarc ??= result;
                fromDomain ??= properties.GetValueOrDefault("header.from");
            }
        }
        parsed = new(spf, dkim, dmarc, fromDomain);
        return spf is not null || dkim.Count > 0 || dmarc is not null;
    }

    public static string? ReadFirstTrustedHeader(string rawMessage, IReadOnlySet<string> trustedAuthservIds)
    {
        if (trustedAuthservIds.Count == 0) return null;
        var separator = rawMessage.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headerEnd = separator >= 0 ? separator : rawMessage.IndexOf("\n\n", StringComparison.Ordinal);
        var headerBlock = headerEnd >= 0 ? rawMessage[..headerEnd] : rawMessage;
        var lines = headerBlock.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        string? name = null;
        var value = new System.Text.StringBuilder();
        foreach (var line in lines.Append(string.Empty))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && name is not null)
            {
                value.Append(' ').Append(line.Trim());
                continue;
            }
            if (name?.Equals("Authentication-Results", StringComparison.OrdinalIgnoreCase) == true)
                return TryParse(value.ToString(), trustedAuthservIds, out _) ? value.ToString() : null;
            var colon = line.IndexOf(':');
            name = colon > 0 ? line[..colon] : null;
            value.Clear();
            if (name is not null) value.Append(line[(colon + 1)..].Trim());
        }
        return null;
    }
}
