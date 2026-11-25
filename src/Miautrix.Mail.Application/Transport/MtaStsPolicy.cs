using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;

namespace Miautrix.Mail.Application.Transport;

public enum MtaStsMode { None, Testing, Enforce }

public sealed record MtaStsPolicy(MtaStsMode Mode, IReadOnlyList<string> MxPatterns, TimeSpan MaxAge)
{
    public bool MatchesMx(string host)
    {
        foreach (var pattern in MxPatterns)
        {
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = pattern[1..];
                if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && host.Length > suffix.Length)
                    return true;
            }
            else if (string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

public interface IMtaStsPolicyProvider
{
    Task<MtaStsPolicy?> GetPolicyAsync(string domain, CancellationToken ct = default);
}

/// <summary>Fetches a recipient domain's published MTA-STS policy; HTTP certificate validation remains enabled.</summary>
public interface IMtaStsPolicyFetcher
{
    Task<MtaStsPolicy?> FetchAsync(string domain, CancellationToken ct = default);
}

public sealed class MtaStsPolicyFetcher(IDnsResolver dns, IHttpClientFactory clients) : IMtaStsPolicyFetcher
{
    public async Task<MtaStsPolicy?> FetchAsync(string domain, CancellationToken ct = default)
    {
        var signals = await dns.GetTxtAsync($"_mta-sts.{domain}", ct);
        if (!signals.Any(value => value.Split(';').Any(part => part.Trim().Equals("v=STSv1", StringComparison.OrdinalIgnoreCase))))
            return null;

        using var response = await clients.CreateClient(nameof(MtaStsPolicyFetcher))
            .GetAsync($"https://mta-sts.{domain}/.well-known/mta-sts.txt", ct);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(ct);
        return Parse(text);
    }

    public static MtaStsPolicy Parse(string text)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) throw new FormatException("MTA-STS policy line is invalid.");
            var key = line[..colon].Trim();
            if (!values.TryGetValue(key, out var list)) values[key] = list = [];
            list.Add(line[(colon + 1)..].Trim().TrimEnd('\r'));
        }

        var version = Single(values, "version");
        if (!string.Equals(version, "STSv1", StringComparison.Ordinal)) throw new FormatException("Unsupported MTA-STS policy version.");
        var mode = Single(values, "mode") switch
        {
            "enforce" => MtaStsMode.Enforce,
            "testing" => MtaStsMode.Testing,
            "none" => MtaStsMode.None,
            _ => throw new FormatException("MTA-STS policy mode is invalid.")
        };
        if (!long.TryParse(Single(values, "max_age"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds < 0)
            throw new FormatException("MTA-STS max_age is invalid.");
        values.TryGetValue("mx", out var mx);
        return new MtaStsPolicy(mode, mx ?? [], TimeSpan.FromSeconds(Math.Min(seconds, 31_557_600)));
    }

    private static string Single(Dictionary<string, List<string>> values, string key) =>
        values.TryGetValue(key, out var list) && list.Count == 1 ? list[0] : throw new FormatException($"MTA-STS policy requires one {key} field.");
}

/// <summary>Domain-keyed cache; successful policies expire at their advertised max_age.</summary>
public sealed class CachedMtaStsPolicyProvider(IMtaStsPolicyFetcher fetcher, TimeProvider? clock = null) : IMtaStsPolicyProvider
{
    private sealed record Entry(MtaStsPolicy? Policy, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<MtaStsPolicy?> GetPolicyAsync(string domain, CancellationToken ct = default)
    {
        var key = domain.TrimEnd('.');
        if (_cache.TryGetValue(key, out var existing) && existing.ExpiresAt > _clock.GetUtcNow()) return existing.Policy;
        var policy = await fetcher.FetchAsync(key, ct);
        var maxAge = policy?.MaxAge ?? TimeSpan.FromMinutes(5);
        _cache[key] = new Entry(policy, _clock.GetUtcNow().Add(maxAge));
        return policy;
    }
}
