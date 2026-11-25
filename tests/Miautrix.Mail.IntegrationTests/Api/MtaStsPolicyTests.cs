using System.Net;
using System.Text;
using System.Text.Json;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests.Api;

[Trait("Category", "MtaSts")]
public sealed class MtaStsPolicyTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;
    public MtaStsPolicyTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        Environment.SetEnvironmentVariable("MIAUTRIX_PUBLIC_SMTP_HOST", "smtp.mailhost.test");
        _factory = factory;
    }

    [Theory]
    [InlineData("testing")]
    [InlineData("enforce")]
    public async Task Policy_text_is_exact_and_routed_by_host(string mode)
    {
        var domain = $"sts-{Guid.NewGuid():N}.test";
        await SeedDomainAsync(domain, mode, 86400);
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/mta-sts.txt");
        request.Headers.Host = $"mta-sts.{domain}";
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        var expected = $"version: STSv1\nmode: {mode}\nmx: smtp.mailhost.test\nmax_age: 86400\n";
        Assert.Equal(Encoding.UTF8.GetBytes(expected), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task None_and_unknown_host_return_not_found()
    {
        var domain = $"sts-none-{Guid.NewGuid():N}.test";
        await SeedDomainAsync(domain, "none", 604800);
        using var client = _factory.CreateClient();
        using var none = new HttpRequestMessage(HttpMethod.Get, "/.well-known/mta-sts.txt");
        none.Headers.Host = $"mta-sts.{domain}";
        using var noneResponse = await client.SendAsync(none);
        Assert.Equal(HttpStatusCode.NotFound, noneResponse.StatusCode);
        using var unknown = new HttpRequestMessage(HttpMethod.Get, "/.well-known/mta-sts.txt");
        unknown.Headers.Host = "mta-sts.unknown.test";
        using var unknownResponse = await client.SendAsync(unknown);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_policy_id_changes_when_policy_is_updated()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var name = $"sts-edit-{Guid.NewGuid():N}.test";
        var now = DateTimeOffset.UtcNow;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")).Options;
        await using (var db = new AppDbContext(options))
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Slug = $"sts-edit-{Guid.NewGuid():N}" });
            db.Users.Add(new User { Id = userId, TenantId = tenantId, Email = $"admin@{name}", Name = "Admin", CreatedAt = now, UpdatedAt = now });
            db.Roles.Add(new Role { Id = roleId, TenantId = tenantId, Code = "admin", Name = "Admin", CreatedAt = now, UpdatedAt = now });
            foreach (var code in new[] { "domain.view", "domain.manage" })
            {
                var permissionId = Guid.NewGuid();
                db.Permissions.Add(new Permission { Id = permissionId, TenantId = tenantId, Code = code, Name = code, CreatedAt = now, UpdatedAt = now });
                db.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), TenantId = tenantId, RoleId = roleId, PermissionId = permissionId, CreatedAt = now, UpdatedAt = now });
            }
            db.Memberships.Add(new Membership { TenantId = tenantId, UserId = userId, RoleId = roleId, CreatedAt = now, UpdatedAt = now });
            db.Domains.Add(new Miautrix.Mail.Domain.Domain { Id = domainId, TenantId = tenantId, Name = name, MtaStsMode = "none" });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        async Task<string> UpdatePolicyAsync(string mode)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/domains/{domainId}")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { mta_sts_mode = mode }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Headers.Add("X-User-Id", userId.ToString());
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("data").GetProperty("mta_sts_id").GetString()!;
        }

        var testingId = await UpdatePolicyAsync("testing");
        var enforceId = await UpdatePolicyAsync("enforce");
        Assert.NotEqual(testingId, enforceId);
    }

    private static async Task SeedDomainAsync(string name, string mode, int maxAge)
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")).Options;
        await using var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant { Id = tenantId, Slug = $"sts-{Guid.NewGuid():N}" });
        db.Domains.Add(new Miautrix.Mail.Domain.Domain { TenantId = tenantId, Name = name, MtaStsMode = mode, MtaStsMaxAge = maxAge });
        await db.SaveChangesAsync();
    }
}
