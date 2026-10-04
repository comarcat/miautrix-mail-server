using System.Security.Cryptography;
using System.Text;

namespace Miautrix.Mail.Identity;

public sealed record AuthToken(string RawToken, string HashedToken, DateTimeOffset ExpiresAt);

public sealed record SessionInfo(Guid SessionId, Guid UserId, Guid TenantId, string SessionTokenHash, DateTimeOffset ExpiresAt, bool IsActive);

public sealed record RefreshTokenInfo(Guid TokenId, Guid SessionId, Guid UserId, Guid TenantId, string TokenHash, DateTimeOffset ExpiresAt, bool IsUsed, bool IsRevoked);

public interface ISessionManager
{
    AuthToken GenerateToken(TimeSpan lifetime);
    string HashToken(string rawToken);
    (AuthToken SessionToken, AuthToken RefreshToken) IssueSessionTokens(TimeSpan? sessionLifetime = null, TimeSpan? refreshLifetime = null);
    TimeSpan DefaultSessionLifetime { get; }
    TimeSpan DefaultRefreshLifetime { get; }
}

public sealed class SessionManager : ISessionManager
{
    private static readonly TimeSpan s_defaultSessionLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan s_defaultRefreshLifetime = TimeSpan.FromDays(30);

    public TimeSpan DefaultSessionLifetime => s_defaultSessionLifetime;
    public TimeSpan DefaultRefreshLifetime => s_defaultRefreshLifetime;

    public AuthToken GenerateToken(TimeSpan lifetime)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string rawToken = Convert.ToHexString(bytes).ToLowerInvariant();
        string hashedToken = HashToken(rawToken);
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(lifetime);

        return new AuthToken(rawToken, hashedToken, expiresAt);
    }

    public string HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public (AuthToken SessionToken, AuthToken RefreshToken) IssueSessionTokens(TimeSpan? sessionLifetime = null, TimeSpan? refreshLifetime = null)
    {
        var sessLife = sessionLifetime ?? DefaultSessionLifetime;
        var refLife = refreshLifetime ?? DefaultRefreshLifetime;

        var sessionToken = GenerateToken(sessLife);
        var refreshToken = GenerateToken(refLife);

        return (sessionToken, refreshToken);
    }
}
