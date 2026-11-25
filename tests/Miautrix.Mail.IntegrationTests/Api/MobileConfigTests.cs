using System.Net;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests.Api;

[Trait("Category", "MobileConfig")]
public sealed class MobileConfigTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;
    public MobileConfigTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        Environment.SetEnvironmentVariable("MIAUTRIX_PUBLIC_IMAP_HOST", "imap.mailhost.test");
        Environment.SetEnvironmentVariable("MIAUTRIX_PUBLIC_SMTP_HOST", "smtp.mailhost.test");
        _factory = factory;
    }

    [Fact]
    public async Task Mobileconfig_requires_request_identity()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/webmail/mobileconfig");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mobileconfig_only_contains_the_callers_email_and_no_password()
    {
        var (tenantId, userId, email) = await SeedUserAsync();
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/webmail/mobileconfig");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add("X-User-Id", userId.ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-apple-aspen-config", response.Content.Headers.ContentType!.MediaType);
        var profile = await response.Content.ReadAsStringAsync();
        Assert.Contains(email, profile);
        Assert.Contains("IncomingMailServerHostName", profile);
        Assert.Contains("OutgoingMailServerHostName", profile);
        Assert.DoesNotContain("password", profile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mobileconfig_rejects_cross_tenant_user_identity()
    {
        var (tenantId, userId, _) = await SeedUserAsync();
        var otherTenant = Guid.NewGuid();
        await using (var db = CreateContext())
        {
            db.Tenants.Add(new Tenant { Id = otherTenant, Slug = $"other-{Guid.NewGuid():N}" });
            await db.SaveChangesAsync();
        }
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/webmail/mobileconfig");
        request.Headers.Add("X-Tenant-Id", otherTenant.ToString());
        request.Headers.Add("X-User-Id", userId.ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(tenantId, otherTenant);
    }

    private static async Task<(Guid Tenant, Guid User, string Email)> SeedUserAsync()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"user-{Guid.NewGuid():N}@mobile-profile.test";
        await using var db = CreateContext();
        db.Tenants.Add(new Tenant { Id = tenantId, Slug = $"mobile-{Guid.NewGuid():N}" });
        db.Users.Add(new User { Id = userId, TenantId = tenantId, Email = email, Name = "Mobile Test User" });
        await db.SaveChangesAsync();
        return (tenantId, userId, email);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")).Options);
}
