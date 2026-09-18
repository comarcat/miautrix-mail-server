using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Api;

[Trait("Category", "Api")]
public sealed class MailQueueApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MailQueueApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            conn = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";
        }
        return conn;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task When_retry_request_omits_required_field_returns_422_with_field_details()
    {
        // ARRANGE — the required `reason` field is deliberately absent from the body.
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/mail/queue/{Guid.NewGuid()}/retry")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-User-Id", Guid.NewGuid().ToString());

        // ACT
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // ASSERT — 422 with a field-level `error.details`, never a 200 success-false body.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("error", out var error), "Error envelope is missing 'error'.");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.True(error.TryGetProperty("details", out var details), "Error envelope is missing 'details'.");
        Assert.Equal(JsonValueKind.Object, details.ValueKind);
        Assert.True(details.EnumerateObject().Any(), "Field-level details must not be empty.");
        Assert.True(error.TryGetProperty("request_id", out var requestId) && requestId.GetString() is { Length: > 0 }, "request_id must be present.");
    }

    [Fact]
    public async Task When_mutating_request_omits_idempotency_key_returns_400()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/mail/queue/{Guid.NewGuid()}");
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-User-Id", Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.Equal("idempotency_key_required", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task When_listing_queue_returns_envelope_with_data_and_meta()
    {
        // ARRANGE — a tenant whose owner role holds the queue.view permission.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedOwnerTenantAsync(tenantId, userId);

        try
        {
            await using var context = CreateContext();
            context.SmtpQueue.Add(new SmtpQueueItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Sender = "sender@example.com",
                Recipient = "recipient@example.com",
                Subject = "Hello",
                RawMessage = "MIME body",
                Status = "Pending",
                Attempts = 0,
                NextAttemptAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/mail/queue");
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Headers.Add("X-User-Id", userId.ToString());

            // ACT
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            // ASSERT
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("data").ValueKind);
            Assert.Equal(1, json.RootElement.GetProperty("data").GetArrayLength());
            Assert.Equal("queued", json.RootElement.GetProperty("data")[0].GetProperty("status").GetString());
            Assert.False(json.RootElement.GetProperty("meta").GetProperty("has_more").GetBoolean());
        }
        finally
        {
            await DeleteTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task When_cross_tenant_retry_is_attempted_returns_404_not_found()
    {
        var tenantA = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedOwnerTenantAsync(tenantA, userA);
        await SeedOwnerTenantAsync(tenantB, Guid.NewGuid());

        var foreignItemId = Guid.NewGuid();
        try
        {
            await using var context = CreateContext();
            context.SmtpQueue.Add(new SmtpQueueItem
            {
                Id = foreignItemId,
                TenantId = tenantB,
                Sender = "sender@example.com",
                Recipient = "recipient@example.com",
                RawMessage = "MIME body",
                Status = "Pending",
                Attempts = 0,
                NextAttemptAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/mail/queue/{foreignItemId}/retry")
            {
                Content = new StringContent("{\"reason\":\"manual\"}", Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            request.Headers.Add("X-Tenant-Id", tenantA.ToString());
            request.Headers.Add("X-User-Id", userA.ToString());

            // ACT
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            // ASSERT — cross-tenant access surfaces as 404, never 403.
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("not_found", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
        finally
        {
            await DeleteTenantAsync(tenantB);
            await DeleteTenantAsync(tenantA);
        }
    }

    [Fact]
    public async Task When_openapi_document_is_requested_returns_200()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("openapi").ValueKind);
    }

    private static async Task SeedOwnerTenantAsync(Guid tenantId, Guid userId)
    {
        await using var context = CreateContext();

        var ownerRoleId = Guid.NewGuid();
        var viewPermId = Guid.NewGuid();
        var retryPermId = Guid.NewGuid();
        var purgePermId = Guid.NewGuid();

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Slug = $"api-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        context.Roles.Add(new Role
        {
            Id = ownerRoleId,
            TenantId = tenantId,
            Code = "owner",
            Name = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        context.Permissions.AddRange(
            new Permission { Id = viewPermId, TenantId = tenantId, Code = "queue.view", Name = "queue.view", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Permission { Id = retryPermId, TenantId = tenantId, Code = "queue.retry", Name = "queue.retry", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Permission { Id = purgePermId, TenantId = tenantId, Code = "queue.purge", Name = "queue.purge", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        context.RolePermissions.AddRange(
            new RolePermission { Id = Guid.NewGuid(), TenantId = tenantId, RoleId = ownerRoleId, PermissionId = viewPermId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new RolePermission { Id = Guid.NewGuid(), TenantId = tenantId, RoleId = ownerRoleId, PermissionId = retryPermId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new RolePermission { Id = Guid.NewGuid(), TenantId = tenantId, RoleId = ownerRoleId, PermissionId = purgePermId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        context.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            RoleId = ownerRoleId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();
    }

    private static async Task DeleteTenantAsync(Guid tenantId)
    {
        await using var context = CreateContext();

        await context.SmtpDeliveryAttempts.Where(a => a.TenantId == tenantId).ExecuteDeleteAsync();
        await context.SmtpQueue.Where(q => q.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Memberships.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
        await context.RolePermissions.Where(rp => rp.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Permissions.Where(p => p.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Roles.Where(r => r.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Users.Where(u => u.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync();
    }
}
