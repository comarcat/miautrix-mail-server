using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Web;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.IntegrationTests.Cloudflare;

/// <summary>
/// The Cloudflare option is additive: a domain is switched to it explicitly, verification then
/// proves the Worker answers instead of proving TXT records match, and the inbound webhook routes
/// through the same handler and the same open-relay guard port 25 uses.
/// </summary>
[Trait("Category", "Cloudflare")]
public sealed class CloudflareTransportTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private const string CloudflareDomain = "cf-transport.test";
    private const string LocalDomain = "local-transport.test";
    private const string InboundToken = "test-inbound-token-0123456789abcdef";

    private static readonly string[] DomainPermissions = ["domain.view", "domain.manage"];

    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;

    public CloudflareTransportTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------------------
    // Verification branches
    // ------------------------------------------------------------------

    [Fact]
    public async Task Verify_on_a_cloudflare_domain_probes_the_worker_and_leaves_the_dns_statuses_null()
    {
        var tenant = await SeedTenantAsync();
        try
        {
            var domainId = await SeedDomainAsync(
                tenant,
                CloudflareDomain,
                "cloudflare",
                isVerified: false,
                workerUrl: "https://this-worker-does-not-exist.invalid");

            var body = await PostJsonAsync(tenant, $"/api/v1/domains/{domainId}/verify");
            var data = body.GetProperty("data");

            Assert.Equal("cloudflare", data.GetProperty("transport_mode").GetString());

            // The three DNS verdicts must be absent, not empty: "" would render in the admin UI as
            // "checked and failed", which is a different claim from "not applicable". The serializer
            // drops nulls, so "absent" is how the distinction reaches the client.
            Assert.False(data.TryGetProperty("dkim_status", out _));
            Assert.False(data.TryGetProperty("spf_status", out _));
            Assert.False(data.TryGetProperty("dmarc_status", out _));

            // An unreachable Worker must not read as verified.
            Assert.False(data.GetProperty("is_verified").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("worker_status").GetString()));
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    [Fact]
    public async Task Verify_on_a_local_domain_keeps_the_dns_branch_and_never_reports_a_worker()
    {
        var tenant = await SeedTenantAsync();
        try
        {
            var domainId = await SeedDomainAsync(tenant, LocalDomain, "local", isVerified: false);

            var body = await PostJsonAsync(tenant, $"/api/v1/domains/{domainId}/verify");
            var data = body.GetProperty("data");

            Assert.Equal("local", data.GetProperty("transport_mode").GetString());
            Assert.False(data.TryGetProperty("worker_status", out _));

            // When the resolver answers, the three DNS verdicts come back as strings. When it does
            // not — a sandbox with no reachable resolver, or a real DNS outage — the branch reports
            // the failure in the message rather than throwing. Either way the mode is unchanged and
            // the call is not a server error.
            if (data.TryGetProperty("dkim_status", out var dkim))
            {
                Assert.Equal(JsonValueKind.String, dkim.ValueKind);
                Assert.Equal(JsonValueKind.String, data.GetProperty("spf_status").ValueKind);
                Assert.Equal(JsonValueKind.String, data.GetProperty("dmarc_status").ValueKind);
            }
            else
            {
                Assert.Contains("DNS lookup failed", data.GetProperty("message").GetString());
            }
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    // ------------------------------------------------------------------
    // Transport mode is validated and invalidates a stale verification
    // ------------------------------------------------------------------

    [Fact]
    public async Task Update_rejects_an_unknown_transport_mode()
    {
        var tenant = await SeedTenantAsync();
        try
        {
            var domainId = await SeedDomainAsync(tenant, LocalDomain, "local", isVerified: true);

            using var request = BuildRequest(tenant, HttpMethod.Put, $"/api/v1/domains/{domainId}",
                new { transport_mode = "aws-ses" }, "Idempotency-Key");
            var response = await _factory.CreateClient().SendAsync(request);

            // A caller-supplied value that is not one of the two known modes is a bad request. It
            // must not surface as a 500, which would hide the reason in the server log.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            await using var context = CreateContext();
            var unchanged = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId);
            Assert.Equal("local", unchanged.TransportMode);
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    [Fact]
    public async Task Switching_transport_mode_clears_the_previous_verification()
    {
        var tenant = await SeedTenantAsync();
        try
        {
            var domainId = await SeedDomainAsync(tenant, LocalDomain, "local", isVerified: true);

            await PutJsonAsync(tenant, $"/api/v1/domains/{domainId}",
                new { transport_mode = "cloudflare", cloudflare_worker_url = "https://worker.invalid" });

            await using var context = CreateContext();
            var domain = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId);

            Assert.Equal("cloudflare", domain.TransportMode);

            // The old verification proved a set of TXT records. That claim says nothing about
            // whether the Worker answers, so it cannot survive the mode change.
            Assert.False(domain.IsVerified);
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    // ------------------------------------------------------------------
    // Inbound webhook: authentication
    // ------------------------------------------------------------------

    [Fact]
    public async Task Inbound_webhook_is_fail_closed_when_no_token_is_configured()
    {
        // The default host has no MIAUTRIX_INBOUND_TOKEN, which is the state of a deployment that
        // has not adopted Cloudflare. Every request must be refused rather than trusted.
        using var request = BuildInboundRequest("anyone@example.com", "someone@example.com", "Subject: x\r\n\r\nbody");
        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Inbound_webhook_rejects_a_missing_token()
    {
        var client = CreateInboundClient().CreateClient();

        using var request = BuildInboundRequest("someone@example.com", $"user@{LocalDomain}", "Subject: x\r\n\r\nbody");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inbound_webhook_rejects_a_wrong_token()
    {
        var client = CreateInboundClient().CreateClient();

        using var request = BuildInboundRequest("someone@example.com", $"user@{LocalDomain}", "Subject: x\r\n\r\nbody");
        request.Headers.Add("X-Miautrix-Inbound-Token", "not-the-right-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Inbound webhook: open-relay guard and delivery
    // ------------------------------------------------------------------

    [Fact]
    public async Task Inbound_webhook_denies_a_recipient_outside_the_tenant_domains()
    {
        var client = CreateInboundClient().CreateClient();

        using var request = BuildInboundRequest("someone@example.com", "victim@not-our-domain.invalid", "Subject: x\r\n\r\nbody");
        request.Headers.Add("X-Miautrix-Inbound-Token", InboundToken);

        var response = await client.SendAsync(request);

        // Relay denied: the webhook must not become an open relay simply because Cloudflare
        // authenticated itself with the shared secret.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Inbound_webhook_queues_a_message_for_a_verified_local_domain()
    {
        var tenant = await SeedTenantAsync();
        try
        {
            await SeedDomainAsync(tenant, LocalDomain, "local", isVerified: true);

            var client = CreateInboundClient().CreateClient();
            const string rawMime =
                "From: sender@example.com\r\nTo: user@local-transport.test\r\n" +
                "Subject: relayed by cloudflare\r\nMessage-Id: <cf-1@local-transport.test>\r\n\r\nHello.";

            using var request = BuildInboundRequest("sender@example.com", $"user@{LocalDomain}", rawMime);
            request.Headers.Add("X-Miautrix-Inbound-Token", InboundToken);

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.True(HttpStatusCode.OK == response.StatusCode, $"Expected 200, got {(int)response.StatusCode}: {body}");

            var queueItemId = JsonDocument.Parse(body).RootElement
                .GetProperty("data").GetProperty("queue_item_id").GetGuid();

            await using var context = CreateContext();
            var queued = await context.SmtpQueue.AsNoTracking().FirstAsync(q => q.Id == queueItemId);

            Assert.Equal(tenant.TenantId, queued.TenantId);
            Assert.Equal($"user@{LocalDomain}", queued.Recipient);
            Assert.Equal("Pending", queued.Status);
            Assert.Contains("Hello.", queued.RawMessage);
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    // ------------------------------------------------------------------
    // HTTP helpers
    // ------------------------------------------------------------------

    private WebApplicationFactory<Miautrix.Mail.Web.Program> CreateInboundClient() =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton(new InboundWebhookOptions { Token = InboundToken })));

    private static HttpRequestMessage BuildInboundRequest(string envelopeFrom, string envelopeTo, string rawMime)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/inbound/cloudflare")
        {
            Content = new StringContent(rawMime, Encoding.UTF8, "message/rfc822")
        };
        request.Headers.Add("X-Miautrix-Envelope-From", envelopeFrom);
        request.Headers.Add("X-Miautrix-Envelope-To", envelopeTo);
        return request;
    }

    private static HttpRequestMessage BuildRequest(TenantScope tenant, HttpMethod method, string path, object? payload = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Tenant-Id", tenant.TenantId.ToString());
        request.Headers.Add("X-User-Id", tenant.AdminUserId.ToString());

        if (payload is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return request;
    }

    private async Task<JsonElement> PostJsonAsync(TenantScope tenant, string path)
    {
        using var request = BuildRequest(tenant, HttpMethod.Post, path, null, "Idempotency-Key");
        var response = await _factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task PutJsonAsync(TenantScope tenant, string path, object payload)
    {
        using var request = BuildRequest(tenant, HttpMethod.Put, path, payload, "Idempotency-Key");
        var response = await _factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {body}");
    }

    // ------------------------------------------------------------------
    // Seeding
    // ------------------------------------------------------------------

    private sealed record TenantScope(Guid TenantId, Guid AdminUserId);

    private static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            conn = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";
        }
        return conn;
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(GetConnectionString()).Options);

    private static async Task<TenantScope> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();

        context.Tenants.Add(new Tenant
        {
            Id = tenantId, Slug = $"cf-{Guid.NewGuid():N}", CreatedAt = now, UpdatedAt = now
        });

        context.Users.Add(new User
        {
            Id = adminUserId, TenantId = tenantId, Email = $"admin@{Guid.NewGuid():N}.test",
            Name = "Tenant Admin", IsActive = true, CreatedAt = now, UpdatedAt = now
        });

        context.Roles.Add(new Role
        {
            Id = roleId, TenantId = tenantId, Code = "tenant_admin", Name = "Tenant Admin",
            CreatedAt = now, UpdatedAt = now
        });

        foreach (var code in DomainPermissions)
        {
            var permissionId = Guid.NewGuid();
            context.Permissions.Add(new Permission
            {
                Id = permissionId, TenantId = tenantId, Code = code, Name = code, CreatedAt = now, UpdatedAt = now
            });
            context.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(), TenantId = tenantId, RoleId = roleId, PermissionId = permissionId,
                CreatedAt = now, UpdatedAt = now
            });
        }

        context.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = adminUserId, RoleId = roleId,
            CreatedAt = now, UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return new TenantScope(tenantId, adminUserId);
    }

    private static async Task<Guid> SeedDomainAsync(
        TenantScope tenant,
        string name,
        string transportMode,
        bool isVerified,
        string? workerUrl = null)
    {
        var domainId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();
        context.Domains.Add(new DomainEntity
        {
            Id = domainId,
            TenantId = tenant.TenantId,
            Name = name,
            IsVerified = isVerified,
            TransportMode = transportMode,
            CloudflareWorkerUrl = workerUrl,
            CloudflareZoneId = transportMode == "cloudflare" ? "023e105f4ecef8ad9ca31a8372d0c353" : null,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return domainId;
    }

    private static async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var context = CreateContext();

        await context.SmtpQueue.Where(q => q.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Domains.Where(d => d.TenantId == tenantId).ExecuteDeleteAsync();
        await context.RolePermissions.Where(rp => rp.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Memberships.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Permissions.Where(p => p.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Roles.Where(r => r.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Users.Where(u => u.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync();
    }
}
