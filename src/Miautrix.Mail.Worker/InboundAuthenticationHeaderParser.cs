namespace Miautrix.Mail.Worker;

internal static class InboundAuthenticationHeaderParser
{
    public static bool IsOwnDmarcQuarantine(string rawMessage, string? authservId)
    {
        if (string.IsNullOrWhiteSpace(authservId)) return false;
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
            {
                var segments = value.ToString().Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0 && segments[0].Equals(authservId, StringComparison.OrdinalIgnoreCase))
                {
                    var methodResult = segments.Skip(1).SelectMany(segment => segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                        .Select(token => token.Split('=', 2)).Where(parts => parts.Length == 2)
                        .GroupBy(parts => parts[0], StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.First()[1], StringComparer.OrdinalIgnoreCase);
                    return methodResult.GetValueOrDefault("dmarc")?.Equals("fail", StringComparison.OrdinalIgnoreCase) == true &&
                           methodResult.GetValueOrDefault("policy.dmarc")?.Equals("quarantine", StringComparison.OrdinalIgnoreCase) == true;
                }
            }
            var colon = line.IndexOf(':');
            name = colon > 0 ? line[..colon] : null;
            value.Clear();
            if (name is not null) value.Append(line[(colon + 1)..].Trim());
        }
        return false;
    }
}
