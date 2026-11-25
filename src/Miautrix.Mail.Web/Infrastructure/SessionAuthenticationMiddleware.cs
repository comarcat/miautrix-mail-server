using System.Security.Cryptography;
using System.Text;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Web.Infrastructure;

public sealed class SessionAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db, ISessionManager tokens, IWebHostEnvironment environment)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api")) { await next(context); return; }
        var anonymous = HttpMethods.IsPost(context.Request.Method) &&
                        (path.Equals("/api/v1/auth/login") || path.Equals("/api/v1/auth/refresh")) ||
                        path.StartsWithSegments("/api/v1/webhooks/inbound") ||
                        path.StartsWithSegments("/api/v1/health");
        if (anonymous) { await next(context); return; }
        var auth = context.Request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) { await Unauthorized(context); return; }
        var raw = auth[7..].Trim();
        if (raw.Length == 0) { await Unauthorized(context); return; }
        var hash = tokens.HashToken(raw);
        var session = await db.Sessions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.AccessTokenHash == hash);
        if (session is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(session.AccessTokenHash), Encoding.UTF8.GetBytes(hash)) || session.RevokedAt != null || session.ExpiresAt <= DateTimeOffset.UtcNow)
        { await Unauthorized(context); return; }
        session.LastSeenAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(context.RequestAborted);
        context.Items["tenant_id"] = session.TenantId;
        context.Items["user_id"] = session.UserId;
        if ((context.Request.Headers.TryGetValue("X-Tenant-Id", out var t) && (!Guid.TryParse(t, out var tenant) || tenant != session.TenantId)) ||
            (context.Request.Headers.TryGetValue("X-User-Id", out var u) && (!Guid.TryParse(u, out var user) || user != session.UserId)))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await ApiResults.Error(context, 404, "not_found", "Resource not found.").ExecuteAsync(context);
            return;
        }
        await next(context);
    }

    private static async Task Unauthorized(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ApiResults.Error(context, 401, "unauthorized", "A valid bearer session is required.").ExecuteAsync(context);
    }
}
