namespace Miautrix.Mail.Identity;

/// <summary>
/// Configuration for account lockout policy.
/// These are currently compile-time constants that can be promoted
/// to runtime configuration (appsettings) in a future iteration.
/// </summary>
public sealed record LockoutOptions
{
    public int MaxFailedAttempts { get; init; } = 5;
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);
}
