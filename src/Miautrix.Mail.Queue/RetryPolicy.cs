namespace Miautrix.Mail.Queue;

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
