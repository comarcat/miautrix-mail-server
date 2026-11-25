using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.UnitTests.Transport;

public sealed class MtaStsPolicyTests
{
    [Fact]
    public void Policy_parser_reads_mode_max_age_and_wildcard_mx()
    {
        var policy = MtaStsPolicyFetcher.Parse("version: STSv1\nmode: enforce\nmx: *.example.test\nmax_age: 600\n");

        Assert.Equal(MtaStsMode.Enforce, policy.Mode);
        Assert.Equal(TimeSpan.FromMinutes(10), policy.MaxAge);
        Assert.True(policy.MatchesMx("mx.example.test"));
        Assert.False(policy.MatchesMx("example.test"));
    }

    [Fact]
    public async Task Policy_cache_keeps_value_until_advertised_max_age_then_refetches()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var fetcher = new CountingFetcher(new MtaStsPolicy(MtaStsMode.Testing, ["mx.example.test"], TimeSpan.FromMinutes(10)));
        var cache = new CachedMtaStsPolicyProvider(fetcher, clock);

        await cache.GetPolicyAsync("example.test");
        await cache.GetPolicyAsync("EXAMPLE.TEST.");
        Assert.Equal(1, fetcher.Calls);
        clock.Advance(TimeSpan.FromMinutes(11));
        await cache.GetPolicyAsync("example.test");
        Assert.Equal(2, fetcher.Calls);
    }

    private sealed class CountingFetcher(MtaStsPolicy policy) : IMtaStsPolicyFetcher
    {
        public int Calls { get; private set; }
        public Task<MtaStsPolicy?> FetchAsync(string domain, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult<MtaStsPolicy?>(policy);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
