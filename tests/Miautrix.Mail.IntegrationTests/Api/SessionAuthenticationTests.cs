using System.Net;
using System.Net.Http.Headers;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Web;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests.Api;

public sealed class SessionAuthenticationTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/system/info")]
    [InlineData("/api/v1/mailboxes")]
    [InlineData("/api/v1/users")]
    public async Task Protected_route_categories_reject_missing_token(string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Controller_route_outside_api_prefix_still_requires_a_session()
    {
        using var testFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(OutsideApiProtectedTestController).Assembly)));
        using var client = testFactory.CreateClient();
        using var response = await client.GetAsync("/test/protected-controller");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_revoked_and_idle_sessions_are_rejected()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var expired = TestSessionHelper.CreateSession(tenant, user, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var revoked = TestSessionHelper.CreateSession(tenant, user, revokedAt: DateTimeOffset.UtcNow);
        var idle = TestSessionHelper.CreateSession(tenant, user, lastSeenAt: DateTimeOffset.UtcNow.AddDays(-31));
        using var client = factory.CreateClient();
        foreach (var raw in new[] { expired, revoked, idle })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", raw);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public void Request_context_has_no_default_admin_or_tenant()
    {
        var accessor = new HeaderRequestContextAccessor(new HttpContextAccessor());
        Assert.Throws<InvalidOperationException>(() => accessor.CurrentTenantId);
        Assert.Throws<InvalidOperationException>(() => accessor.CurrentUserId);
    }

    [Fact]
    public async Task Webmail_session_cannot_call_admin_user_api()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var raw = TestSessionHelper.CreateSession(tenantId, userId);
        var connection = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")!;
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options))
        {
            var role = new Role { TenantId = tenantId, Code = "mail_user", Name = "Webmail user" };
            db.Users.Add(new User { Id = userId, TenantId = tenantId, Email = $"webmail-{Guid.NewGuid():N}@example.test", Name = "Webmail user" });
            db.Roles.Add(role);
            db.Memberships.Add(new Membership { TenantId = tenantId, UserId = userId, RoleId = role.Id });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", raw);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Forged_identity_header_returns_404_and_writes_audit_row()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var raw = TestSessionHelper.CreateSession(tenantId, userId);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", raw);
        request.Headers.Add("X-User-Id", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var connection = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")!;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.TenantId == tenantId && x.ActorId == userId && x.Action == "authorization.failed"));
    }
}

[ApiController]
[Route("/test/protected-controller")]
public sealed class OutsideApiProtectedTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
