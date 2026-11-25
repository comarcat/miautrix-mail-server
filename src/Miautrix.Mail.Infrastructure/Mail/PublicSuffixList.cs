using System.Reflection;

namespace Miautrix.Mail.Infrastructure.Mail;

/// <summary>Offline Public Suffix List snapshot. Implements exact, wildcard and exception rules.</summary>
public sealed class PublicSuffixList
{
    public const string SnapshotSource = "https://publicsuffix.org/list/public_suffix_list.dat";
    public const string SnapshotDate = "2026-11-25";
    private readonly HashSet<string> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _wildcard = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _exceptions = new(StringComparer.OrdinalIgnoreCase);

    public PublicSuffixList()
    {
        using var stream = typeof(PublicSuffixList).Assembly.GetManifestResourceStream("Miautrix.Mail.Infrastructure.Mail.public_suffix_list.dat")
            ?? throw new InvalidOperationException("Embedded PSL snapshot is missing.");
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } row)
        {
            var rule = row.Trim();
            if (rule.Length == 0 || rule.StartsWith("//", StringComparison.Ordinal)) continue;
            if (rule.StartsWith('!')) _exceptions.Add(rule[1..]);
            else if (rule.StartsWith("*.")) _wildcard.Add(rule[2..]);
            else _exact.Add(rule);
        }
    }

    public string? GetOrganizationalDomain(string domain)
    {
        var labels = domain.Trim().TrimEnd('.').ToLowerInvariant().Split('.');
        if (labels.Length < 2 || labels.Any(string.IsNullOrWhiteSpace)) return null;
        var exceptionLabels = 0;
        var publicSuffixLabels = 1; // PSL prevailing rule is *.
        for (var i = 0; i < labels.Length; i++)
        {
            var candidate = string.Join('.', labels.Skip(i));
            if (_exceptions.Contains(candidate)) exceptionLabels = Math.Max(exceptionLabels, labels.Length - i);
            if (_exact.Contains(candidate)) publicSuffixLabels = Math.Max(publicSuffixLabels, labels.Length - i);
            if (i + 1 < labels.Length && _wildcard.Contains(string.Join('.', labels.Skip(i + 1))))
                publicSuffixLabels = Math.Max(publicSuffixLabels, labels.Length - i);
        }
        if (exceptionLabels > 0) publicSuffixLabels = exceptionLabels - 1;
        if (labels.Length <= publicSuffixLabels) return null;
        return string.Join('.', labels.Skip(labels.Length - publicSuffixLabels - 1));
    }
}
