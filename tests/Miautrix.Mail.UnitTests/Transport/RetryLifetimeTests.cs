using Miautrix.Mail.Queue;

namespace Miautrix.Mail.UnitTests.Transport;

public sealed class RetryLifetimeTests
{
    [Fact]
    public void Retry_delay_grows_exponentially_and_stays_inside_configured_jitter_bounds()
    {
        var policy = new ExponentialBackoffWithJitterRetryPolicy(TimeSpan.FromSeconds(8), TimeSpan.FromMinutes(10), 0.25);

        var first = policy.GetNextRetryDelay(1, new Random(123));
        var second = policy.GetNextRetryDelay(2, new Random(123));

        Assert.InRange(first.TotalSeconds, 8, 10);
        Assert.InRange(second.TotalSeconds, 16, 20);
        Assert.True(second > first);
    }

    [Fact]
    public void Default_retry_lifetime_expires_at_five_days_using_supplied_clock_time()
    {
        var options = new QueueRetryOptions();
        var created = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        Assert.Equal(TimeSpan.FromDays(5), options.Lifetime);
        Assert.False(options.IsExpired(created, created.AddDays(5).AddTicks(-1)));
        Assert.True(options.IsExpired(created, created.AddDays(5)));
    }
}
