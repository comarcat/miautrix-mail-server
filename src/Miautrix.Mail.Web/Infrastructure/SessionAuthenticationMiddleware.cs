using System.Security.Cryptography;
using System.Text;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Miautrix.Mail.Web.Infrastructure;

public sealed class SessionAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db, ISessionManager tokens)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>() is null ||
            endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() is not null)
        { await next(context); return; }
        var auth = context.Request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) { await Unauthorized(context); return; }
        var raw = auth[7..].Trim();
        if (raw.Length == 0) { await Unauthorized(context); return; }
        var hash = tokens.HashToken(raw);
        var session = await db.Sessions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.AccessTokenHash == hash);
        if (session is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(session.AccessTokenHash), Encoding.UTF8.GetBytes(hash)) || session.RevokedAt != null || session.ExpiresAt <= DateTimeOffset.UtcNow || session.LastSeenAt < DateTimeOffset.UtcNow.AddDays(-30))
        { await Unauthorized(context); return; }
        session.LastSeenAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(context.RequestAborted);
        context.Items["tenant_id"] = session.TenantId;
        context.Items["user_id"] = session.UserId;
        context.Items["session_id"] = session.Id;
        if ((context.Request.Headers.TryGetValue("X-Tenant-Id", out var t) && (!Guid.TryParse(t, out var tenant) || tenant != session.TenantId)) ||
            (context.Request.Headers.TryGetValue("X-User-Id", out var u) && (!Guid.TryParse(u, out var user) || user != session.UserId)))
        {
            db.AuditLogs.Add(new Miautrix.Mail.Domain.AuditLog { TenantId = session.TenantId, ActorId = session.UserId, Action = "authorization.failed", TargetType = "identity_header_mismatch", TargetId = session.UserId, DetailsJson = "Client supplied identity headers did not match authenticated session.", IpAddress = context.Connection.RemoteIpAddress?.ToString() });
            await db.SaveChangesAsync(context.RequestAborted);
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
