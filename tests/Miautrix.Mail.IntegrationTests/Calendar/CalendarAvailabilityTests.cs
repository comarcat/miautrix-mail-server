using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Calendar;

/// <summary>
/// Availability is a free/busy view: "free" events never block, overlapping blocks produce
/// conflicts, and a private colleague's title stays out of the response.
/// </summary>
[Trait("Category", "Calendar")]
public sealed class CalendarAvailabilityTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;

    public CalendarAvailabilityTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Availability_excludes_free_events_and_keeps_busy_ones()
    {
        var tenantId = Guid.NewGuid();
        var user = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { user });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            await CalendarTestHelpers.SeedEventAsync(tenantId, user.Id, "Lunch", start, start.AddHours(1), showAs: "free");
            await CalendarTestHelpers.SeedEventAsync(tenantId, user.Id, "Standup", start.AddHours(2), start.AddHours(3), showAs: "busy");

            var data = await GetAvailabilityAsync(tenantId, user.Id, start.AddDays(-1), start.AddDays(1));

            Assert.Equal(1, data.GetArrayLength());
            var busy = data[0].GetProperty("busy");
            Assert.Equal(1, busy.GetArrayLength());
            Assert.Equal("busy", busy[0].GetProperty("show_as").GetString());

            // The caller's own block keeps its title so the webmail can label it.
            Assert.Equal("Standup", busy[0].GetProperty("title").GetString());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Availability_includes_out_of_office_blocks()
    {
        var tenantId = Guid.NewGuid();
        var user = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { user });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            await CalendarTestHelpers.SeedEventAsync(
                tenantId,
                user.Id,
                "Annual leave",
                start,
                start.AddHours(8),
                showAs: "out_of_office");

            var data = await GetAvailabilityAsync(tenantId, user.Id, start.AddDays(-1), start.AddDays(1));

            var busy = data[0].GetProperty("busy");
            Assert.Equal(1, busy.GetArrayLength());
            Assert.Equal("out_of_office", busy[0].GetProperty("show_as").GetString());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Default_availability_listing_leaves_out_service_accounts()
    {
        var tenantId = Guid.NewGuid();
        var user = CalendarTestHelpers.NewUser();
        var room = CalendarTestHelpers.NewUser(email: $"room-{Guid.NewGuid():N}@test.local", isService: true);

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { user, room });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            await CalendarTestHelpers.SeedEventAsync(tenantId, room.Id, "Room booking", start, start.AddHours(1));

            var data = await GetAvailabilityAsync(tenantId, user.Id, start.AddDays(-1), start.AddDays(1));

            Assert.Equal(1, data.GetArrayLength());
            Assert.Equal(user.Id.ToString(), data[0].GetProperty("user_id").GetString());

            // A room is booked by asking for the room, so naming it explicitly brings it back.
            var explicitData = await GetAvailabilityAsync(tenantId, user.Id, start.AddDays(-1), start.AddDays(1), new[] { room.Id });
            Assert.Equal(1, explicitData.GetArrayLength());
            Assert.Equal(room.Id.ToString(), explicitData[0].GetProperty("user_id").GetString());
            Assert.Equal(1, explicitData[0].GetProperty("busy").GetArrayLength());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Compare_reports_a_conflict_for_an_overlapping_busy_block()
    {
        var tenantId = Guid.NewGuid();
        var user = CalendarTestHelpers.NewUser();
        var colleague = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { user, colleague });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            await CalendarTestHelpers.SeedEventAsync(tenantId, colleague.Id, "Existing review", start.AddMinutes(30), start.AddMinutes(90));

            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/availability/compare",
                tenantId,
                user.Id,
                new Dictionary<string, object?>
                {
                    ["start_time"] = start,
                    ["end_time"] = start.AddHours(1),
                    ["participant_ids"] = new[] { colleague.Id }
                }));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
            var data = await CalendarTestHelpers.ReadDataAsync(response);

            Assert.False(data.GetProperty("all_available").GetBoolean());
            var conflicts = data.GetProperty("conflicts");
            Assert.Equal(1, conflicts.GetArrayLength());
            Assert.Equal(colleague.Id.ToString(), conflicts[0].GetProperty("participant_id").GetString());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Compare_reports_all_available_when_only_free_events_overlap()
    {
        var tenantId = Guid.NewGuid();
        var user = CalendarTestHelpers.NewUser();
        var colleague = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { user, colleague });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            await CalendarTestHelpers.SeedEventAsync(
                tenantId,
                colleague.Id,
                "Focus time",
                start.AddMinutes(30),
                start.AddMinutes(90),
                showAs: "free");

            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/availability/compare",
                tenantId,
                user.Id,
                new Dictionary<string, object?>
                {
                    ["start_time"] = start,
                    ["end_time"] = start.AddHours(1),
                    ["participant_ids"] = new[] { colleague.Id }
                }));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
            var data = await CalendarTestHelpers.ReadDataAsync(response);

            Assert.True(data.GetProperty("all_available").GetBoolean());
            Assert.Equal(0, data.GetProperty("conflicts").GetArrayLength());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Compare_never_leaks_a_private_colleagues_title()
    {
        var tenantId = Guid.NewGuid();
        var user = CalendarTestHelpers.NewUser();
        var colleague = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { user, colleague });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            await CalendarTestHelpers.SeedEventAsync(
                tenantId,
                colleague.Id,
                "Salary negotiation",
                start.AddMinutes(15),
                start.AddMinutes(45),
                description: "Confidential");

            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/availability/compare",
                tenantId,
                user.Id,
                new Dictionary<string, object?>
                {
                    ["start_time"] = start,
                    ["end_time"] = start.AddHours(1),
                    ["participant_ids"] = new[] { colleague.Id }
                }));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
            var data = await CalendarTestHelpers.ReadDataAsync(response);

            var busy = data.GetProperty("participants")[0].GetProperty("busy");
            Assert.Equal(1, busy.GetArrayLength());
            Assert.Equal("busy", busy[0].GetProperty("show_as").GetString());
            Assert.False(busy[0].TryGetProperty("title", out _), "A private colleague's block must not carry a title.");

            // The conflict is still reported so the organiser knows the slot is taken.
            Assert.Equal(1, data.GetProperty("conflicts").GetArrayLength());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Compare_with_a_participant_from_another_tenant_returns_404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = CalendarTestHelpers.NewUser();
        var userB = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantA, "user", new[] { userA });
        await CalendarTestHelpers.SeedTenantAsync(tenantB, "user", new[] { userB });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/availability/compare",
                tenantA,
                userA.Id,
                new Dictionary<string, object?>
                {
                    ["start_time"] = start,
                    ["end_time"] = start.AddHours(1),
                    ["participant_ids"] = new[] { userB.Id }
                }));

            // A 403 would confirm the id exists somewhere else.
            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.NotFound);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantA);
            await CalendarTestHelpers.CleanupTenantAsync(tenantB);
        }
    }

    private async Task<System.Text.Json.JsonElement> GetAvailabilityAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<Guid>? userIds = null)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["from"] = from.ToString("O");
        query["to"] = to.ToString("O");
        if (userIds is { Count: > 0 })
        {
            query["user_ids"] = string.Join(",", userIds);
        }

        using var client = _factory.CreateClient();
        using var response = await client.SendAsync(CalendarTestHelpers.Request(
            HttpMethod.Get,
            $"/api/v1/calendar/availability?{query}",
            tenantId,
            userId));

        await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
        return await CalendarTestHelpers.ReadDataAsync(response);
    }
}
