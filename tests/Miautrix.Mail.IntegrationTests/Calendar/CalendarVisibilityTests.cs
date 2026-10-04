using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Calendar;

/// <summary>
/// Who sees whose calendar: own events always, a subscribed colleague's events only as
/// "Busy", and everything for an owner or admin.
/// </summary>
[Trait("Category", "Calendar")]
public sealed class CalendarVisibilityTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;

    public CalendarVisibilityTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_events_returns_only_own_events_until_a_subscription_exists()
    {
        var tenantId = Guid.NewGuid();
        var requester = CalendarTestHelpers.NewUser();
        var colleague = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { requester, colleague });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            await CalendarTestHelpers.SeedEventAsync(tenantId, colleague.Id, "Colleague planning", start, start.AddHours(1));

            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(
                CalendarTestHelpers.Request(HttpMethod.Get, "/api/v1/calendar/events", tenantId, requester.Id));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
            var data = await CalendarTestHelpers.ReadDataAsync(response);

            Assert.Equal(0, data.GetArrayLength());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Subscribed_colleague_event_is_redacted_to_busy()
    {
        var tenantId = Guid.NewGuid();
        var requester = CalendarTestHelpers.NewUser();
        var colleague = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { requester, colleague });
        await CalendarTestHelpers.SeedSubscriptionAsync(tenantId, requester.Id, colleague.Id);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            var eventId = await CalendarTestHelpers.SeedEventAsync(
                tenantId,
                colleague.Id,
                "Salary review",
                start,
                start.AddHours(1),
                location: "Board room",
                description: "Compensation details",
                organizer: colleague.Email);

            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(
                CalendarTestHelpers.Request(HttpMethod.Get, "/api/v1/calendar/events", tenantId, requester.Id));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
            var data = await CalendarTestHelpers.ReadDataAsync(response);

            Assert.Equal(1, data.GetArrayLength());
            var visible = data[0];
            Assert.Equal(eventId.ToString(), visible.GetProperty("id").GetString());
            Assert.Equal(colleague.Id.ToString(), visible.GetProperty("user_id").GetString());

            // Nothing but the time window crosses the colleague boundary.
            Assert.Equal("Busy", visible.GetProperty("title").GetString());
            Assert.Equal("busy", visible.GetProperty("show_as").GetString());
            Assert.Equal("private", visible.GetProperty("visibility").GetString());
            Assert.False(visible.GetProperty("is_own").GetBoolean());
            Assert.False(visible.GetProperty("can_edit").GetBoolean());
            Assert.False(visible.TryGetProperty("location", out _));
            Assert.False(visible.TryGetProperty("description", out _));
            Assert.False(visible.TryGetProperty("organizer", out _));
            Assert.Equal(0, visible.GetProperty("attendees").GetArrayLength());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Owner_sees_full_details_of_another_users_private_event()
    {
        var tenantId = Guid.NewGuid();
        var owner = CalendarTestHelpers.NewUser();
        var colleague = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "owner", new[] { owner, colleague });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            await CalendarTestHelpers.SeedEventAsync(
                tenantId,
                colleague.Id,
                "Board review",
                start,
                start.AddHours(1),
                location: "Board room");

            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(
                CalendarTestHelpers.Request(HttpMethod.Get, "/api/v1/calendar/events", tenantId, owner.Id));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
            var data = await CalendarTestHelpers.ReadDataAsync(response);

            Assert.Equal(1, data.GetArrayLength());
            Assert.Equal("Board review", data[0].GetProperty("title").GetString());
            Assert.Equal("Board room", data[0].GetProperty("location").GetString());
            Assert.False(data[0].GetProperty("is_own").GetBoolean());
            Assert.False(data[0].GetProperty("can_edit").GetBoolean());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Public_colleague_event_keeps_its_details_without_a_subscription()
    {
        var tenantId = Guid.NewGuid();
        var requester = CalendarTestHelpers.NewUser();
        var colleague = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { requester, colleague });
        await CalendarTestHelpers.SeedSubscriptionAsync(tenantId, requester.Id, colleague.Id);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            await CalendarTestHelpers.SeedEventAsync(
                tenantId,
                colleague.Id,
                "Team standup",
                start,
                start.AddHours(1),
                visibility: "public");

            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(
                CalendarTestHelpers.Request(HttpMethod.Get, "/api/v1/calendar/events", tenantId, requester.Id));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
            var data = await CalendarTestHelpers.ReadDataAsync(response);

            Assert.Equal("Team standup", data[0].GetProperty("title").GetString());
            Assert.False(data[0].GetProperty("can_edit").GetBoolean());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Events_outside_the_requested_window_are_not_returned()
    {
        var tenantId = Guid.NewGuid();
        var requester = CalendarTestHelpers.NewUser();

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { requester });

        try
        {
            var farFuture = DateTimeOffset.UtcNow.AddDays(120);
            await CalendarTestHelpers.SeedEventAsync(tenantId, requester.Id, "Far away", farFuture, farFuture.AddHours(1));

            var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
            var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(30).ToString("O"));

            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(
                CalendarTestHelpers.Request(HttpMethod.Get, $"/api/v1/calendar/events?from={from}&to={to}", tenantId, requester.Id));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
            var data = await CalendarTestHelpers.ReadDataAsync(response);

            Assert.Equal(0, data.GetArrayLength());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }
}
