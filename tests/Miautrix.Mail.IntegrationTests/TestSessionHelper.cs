using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests;

internal static class TestSessionHelper
{
    public static void Attach(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        var raw = CreateSession(tenantId, userId);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", raw);
    }

    public static string CreateSession(Guid tenantId, Guid userId, DateTimeOffset? expiresAt = null, DateTimeOffset? lastSeenAt = null, DateTimeOffset? revokedAt = null)
    {
        var raw = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var manager = new SessionManager();
        var session = new Session
        {
            TenantId = tenantId,
            UserId = userId,
            AccessTokenHash = manager.HashToken(raw),
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
            LastSeenAt = lastSeenAt ?? DateTimeOffset.UtcNow,
            RevokedAt = revokedAt
        };
        var connection = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION is required.");
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        if (!db.Tenants.Any(t => t.Id == tenantId))
            db.Tenants.Add(new Tenant { Id = tenantId, Slug = $"test-session-{Guid.NewGuid():N}" });
        db.Sessions.Add(session);
        db.SaveChanges();
        return raw;
    }
}
