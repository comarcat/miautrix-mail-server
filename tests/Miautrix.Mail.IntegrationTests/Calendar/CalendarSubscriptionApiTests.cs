using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Calendar;

[Trait("Category", "Calendar")]
public sealed class CalendarSubscriptionApiTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;

    public CalendarSubscriptionApiTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        _factory = factory;
    }

    private static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION environment variable is required.");
        }
        return conn;
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(GetConnectionString()).Options);

    [Fact]
    public async Task Add_and_list_and_delete_subscriptions_work_idempotently()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await SeedTenantAndUsersAsync(tenantId, userA, userB);

        try
        {
            using var client = _factory.CreateClient();

            // 1. Add subscription to userB
            var addReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/calendar/subscriptions")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { user_id = userB }), Encoding.UTF8, "application/json")
            };
            addReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            addReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
            addReq.Headers.Add("X-User-Id", userA.ToString());

            var addRes = await client.SendAsync(addReq);
            Assert.Equal(HttpStatusCode.NoContent, addRes.StatusCode);

            // Idempotent retry with different idempotency key succeeds
            var addReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/calendar/subscriptions")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { user_id = userB }), Encoding.UTF8, "application/json")
            };
            addReq2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            addReq2.Headers.Add("X-Tenant-Id", tenantId.ToString());
            addReq2.Headers.Add("X-User-Id", userA.ToString());
            var addRes2 = await client.SendAsync(addReq2);
            Assert.Equal(HttpStatusCode.NoContent, addRes2.StatusCode);

            // 2. List subscriptions
            var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/calendar/subscriptions");
            listReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
            listReq.Headers.Add("X-User-Id", userA.ToString());

            var listRes = await client.SendAsync(listReq);
            Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
            var listBody = await listRes.Content.ReadAsStringAsync();
            using var listDoc = JsonDocument.Parse(listBody);
            var listData = listDoc.RootElement.GetProperty("data");
            Assert.Equal(1, listData.GetArrayLength());
            Assert.Equal(userB.ToString(), listData[0].GetProperty("user_id").GetString());

            // 3. Delete subscription
            var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/calendar/subscriptions/{userB}");
            delReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            delReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
            delReq.Headers.Add("X-User-Id", userA.ToString());

            var delRes = await client.SendAsync(delReq);
            Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

            // 4. Verify list is now empty
            var listReqAfter = new HttpRequestMessage(HttpMethod.Get, "/api/v1/calendar/subscriptions");
            listReqAfter.Headers.Add("X-Tenant-Id", tenantId.ToString());
            listReqAfter.Headers.Add("X-User-Id", userA.ToString());

            var listResAfter = await client.SendAsync(listReqAfter);
            var listBodyAfter = await listResAfter.Content.ReadAsStringAsync();
            using var listDocAfter = JsonDocument.Parse(listBodyAfter);
            Assert.Equal(0, listDocAfter.RootElement.GetProperty("data").GetArrayLength());
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Self_subscription_returns_bad_request()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        await SeedTenantAndUsersAsync(tenantId, userA);

        try
        {
            using var client = _factory.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/calendar/subscriptions")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { user_id = userA }), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            req.Headers.Add("X-Tenant-Id", tenantId.ToString());
            req.Headers.Add("X-User-Id", userA.ToString());

            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Foreign_tenant_subscription_returns_404_not_found()
    {
        var tenantA = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await SeedTenantAndUsersAsync(tenantA, userA);
        await SeedTenantAndUsersAsync(tenantB, userB);

        try
        {
            using var client = _factory.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/calendar/subscriptions")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { user_id = userB }), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            req.Headers.Add("X-Tenant-Id", tenantA.ToString());
            req.Headers.Add("X-User-Id", userA.ToString());

            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
        finally
        {
            await CleanupTenantAsync(tenantA);
            await CleanupTenantAsync(tenantB);
        }
    }

    [Fact]
    public async Task Repeated_subscriptions_never_create_a_duplicate_row()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await SeedTenantAndUsersAsync(tenantId, userA, userB);

        try
        {
            using var client = _factory.CreateClient();
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var request = CalendarTestHelpers.Request(
                    HttpMethod.Post,
                    "/api/v1/calendar/subscriptions",
                    tenantId,
                    userA,
                    new { user_id = userB });

                using var response = await client.SendAsync(request);
                await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.NoContent);
            }

            await using var context = CreateContext();
            var rows = context.CalendarSubscriptions.Count(s => s.TenantId == tenantId && s.UserId == userA);
            Assert.Equal(1, rows);
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Subscribing_to_a_service_account_returns_404()
    {
        var tenantId = Guid.NewGuid();
        var userA = CalendarTestHelpers.NewUser();
        var room = CalendarTestHelpers.NewUser(email: $"room-{Guid.NewGuid():N}@test.local", isService: true);
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { userA, room });

        try
        {
            using var client = _factory.CreateClient();
            using var request = CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/subscriptions",
                tenantId,
                userA.Id,
                new { user_id = room.Id });

            using var response = await client.SendAsync(request);

            // A room is booked, not followed, and a 403 would confirm the id exists.
            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.NotFound);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Deleting_a_subscription_that_does_not_exist_returns_404()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await SeedTenantAndUsersAsync(tenantId, userA, userB);

        try
        {
            using var client = _factory.CreateClient();
            using var request = CalendarTestHelpers.Request(
                HttpMethod.Delete,
                $"/api/v1/calendar/subscriptions/{userB}",
                tenantId,
                userA);

            using var response = await client.SendAsync(request);
            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task A_repeated_delete_idempotency_key_replays_the_first_answer()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await SeedTenantAndUsersAsync(tenantId, userA, userB);
        await CalendarTestHelpers.SeedSubscriptionAsync(tenantId, userA, userB);

        try
        {
            var idempotencyKey = Guid.NewGuid().ToString();
            using var client = _factory.CreateClient();

            using var first = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/calendar/subscriptions/{userB}");
            first.Headers.Add("Idempotency-Key", idempotencyKey);
            first.Headers.Add("X-Tenant-Id", tenantId.ToString());
            first.Headers.Add("X-User-Id", userA.ToString());
            using var firstResponse = await client.SendAsync(first);
            Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);

            // The retry is answered from the captured response, not by re-running the delete.
            using var retry = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/calendar/subscriptions/{userB}");
            retry.Headers.Add("Idempotency-Key", idempotencyKey);
            retry.Headers.Add("X-Tenant-Id", tenantId.ToString());
            retry.Headers.Add("X-User-Id", userA.ToString());
            using var retryResponse = await client.SendAsync(retry);
            Assert.Equal(HttpStatusCode.NoContent, retryResponse.StatusCode);

            // A fresh key sees the real state: the subscription is gone.
            using var freshKey = CalendarTestHelpers.Request(
                HttpMethod.Delete,
                $"/api/v1/calendar/subscriptions/{userB}",
                tenantId,
                userA);
            using var freshResponse = await client.SendAsync(freshKey);
            Assert.Equal(HttpStatusCode.NotFound, freshResponse.StatusCode);
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Deleting_a_foreign_tenants_subscription_returns_404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await SeedTenantAndUsersAsync(tenantA, userA);
        await SeedTenantAndUsersAsync(tenantB, userB);

        try
        {
            using var client = _factory.CreateClient();
            using var request = CalendarTestHelpers.Request(
                HttpMethod.Delete,
                $"/api/v1/calendar/subscriptions/{userB}",
                tenantA,
                userA);

            using var response = await client.SendAsync(request);
            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupTenantAsync(tenantA);
            await CleanupTenantAsync(tenantB);
        }
    }

    [Fact]
    public async Task A_mutating_request_without_an_idempotency_key_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await SeedTenantAndUsersAsync(tenantId, userA, userB);

        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/calendar/subscriptions")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { user_id = userB }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Headers.Add("X-User-Id", userA.ToString());

            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("idempotency_key_required", body, StringComparison.Ordinal);
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    private static Task SeedTenantAndUsersAsync(Guid tenantId, params Guid[] userIds) =>
        CalendarTestHelpers.SeedTenantAsync(
            tenantId,
            "user",
            userIds.Select(userId => new CalendarUser(
                userId,
                $"user-{userId:N}@test.local",
                $"User {userId:N}",
                false)).ToList());

    private static Task CleanupTenantAsync(Guid tenantId) =>
        CalendarTestHelpers.CleanupTenantAsync(tenantId);
}
