namespace Miautrix.Mail.Queue;

public sealed class QueueRetryOptions
{
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(5);

    public static QueueRetryOptions FromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("OUTBOUND_RETRY_LIFETIME_DAYS");
        if (raw is null) return new QueueRetryOptions();
        if (!int.TryParse(raw, out var days) || days is < 1 or > 30)
            throw new InvalidOperationException("OUTBOUND_RETRY_LIFETIME_DAYS must be between 1 and 30.");
        return new QueueRetryOptions { Lifetime = TimeSpan.FromDays(days) };
    }

    public bool IsExpired(DateTimeOffset createdAt, DateTimeOffset now) => now >= createdAt + Lifetime;
}

public interface IRetryPolicy
{
    TimeSpan GetNextRetryDelay(int attemptNumber, Random? random = null);
}

public sealed class ExponentialBackoffWithJitterRetryPolicy : IRetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _jitterRatio;

    public ExponentialBackoffWithJitterRetryPolicy(
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        double jitterRatio = 0.25)
    {
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(5);
        _maxDelay = maxDelay ?? TimeSpan.FromHours(24);
        _jitterRatio = Math.Clamp(jitterRatio, 0.0, 0.5);
    }

    public TimeSpan GetNextRetryDelay(int attemptNumber, Random? random = null)
    {
        if (attemptNumber <= 0)
        {
            attemptNumber = 1;
        }

        // Exponential term: 2^(attempt - 1)
        double multiplier = Math.Pow(2, attemptNumber - 1);
        double baseSeconds = _baseDelay.TotalSeconds * multiplier;

        // Bounded jitter: [0, baseSeconds * jitterRatio]
        // Since jitterRatio <= 0.5 and 2^k - 2^(k-1) = 2^(k-1) >= 1 > jitterRatio,
        // min(attempt + 1) is guaranteed strictly greater than max(attempt).
        var rng = random ?? Random.Shared;
        double jitter = rng.NextDouble() * (baseSeconds * _jitterRatio);
        double totalSeconds = Math.Min(baseSeconds + jitter, _maxDelay.TotalSeconds);

        return TimeSpan.FromSeconds(totalSeconds);
    }
}
