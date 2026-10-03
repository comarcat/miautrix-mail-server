using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Calendar;

/// <summary>
/// Rooms and other service accounts book themselves: they accept immediately, hold no RSVP
/// token, receive no email, and keep a private busy mirror on the resource's calendar.
/// </summary>
[Trait("Category", "Calendar")]
public sealed class CalendarServiceAccountTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;

    public CalendarServiceAccountTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Inviting_a_room_auto_accepts_it_and_mirrors_it_without_a_token()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        var room = CalendarTestHelpers.NewUser(email: $"room-{Guid.NewGuid():N}@test.local", name: "Room 1", isService: true);

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer, room });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(2);
            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Quarterly planning",
                    start,
                    start.AddHours(1),
                    location: room.Email,
                    invitees: new[] { CalendarTestHelpers.Invitee(room.Email, "Room 1") },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.Created);
            var created = await CalendarTestHelpers.ReadDataAsync(response);
            var eventId = Guid.Parse(created.GetProperty("id").GetString()!);

            var attendees = created.GetProperty("attendees");
            Assert.Equal(1, attendees.GetArrayLength());
            Assert.Equal("accepted", attendees[0].GetProperty("response_status").GetString());
            Assert.Equal(room.Email, attendees[0].GetProperty("email").GetString());

            await using var context = CalendarTestHelpers.CreateContext();
            var attendee = await context.CalendarEventAttendees
                .AsNoTracking()
                .FirstAsync(a => a.TenantId == tenantId && a.EventId == eventId);

            Assert.Equal("accepted", attendee.ResponseStatus);
            Assert.NotNull(attendee.RespondedAt);
            Assert.Null(attendee.TokenHash);
            Assert.Null(attendee.TokenExpiresAt);
            Assert.NotNull(attendee.MirroredEventId);

            var mirror = await context.CalendarEvents
                .AsNoTracking()
                .FirstAsync(e => e.TenantId == tenantId && e.Id == attendee.MirroredEventId!.Value);
            Assert.Equal(room.Id, mirror.UserId);
            Assert.Equal("Quarterly planning", mirror.Title);
            Assert.Equal("private", mirror.Visibility);
            Assert.Equal("busy", mirror.ShowAs);
            Assert.Equal("confirmed", mirror.Status);

            // A room is booked by the mirror, never by an email.
            var queued = await context.SmtpQueue.CountAsync(q => q.TenantId == tenantId);
            Assert.Equal(0, queued);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Updating_a_meeting_keeps_the_room_accepted_and_moves_the_mirror()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        var room = CalendarTestHelpers.NewUser(email: $"room-{Guid.NewGuid():N}@test.local", name: "Room 2", isService: true);

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer, room });

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(3);
            using var client = _factory.CreateClient();
            using var createResponse = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Design review",
                    start,
                    start.AddHours(1),
                    invitees: new[] { CalendarTestHelpers.Invitee(room.Email, "Room 2") },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(createResponse, HttpStatusCode.Created);
            var created = await CalendarTestHelpers.ReadDataAsync(createResponse);
            var eventId = Guid.Parse(created.GetProperty("id").GetString()!);

            var movedStart = start.AddDays(1);
            using var updateResponse = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Put,
                $"/api/v1/calendar/events/{eventId}",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Design review",
                    movedStart,
                    movedStart.AddHours(2),
                    invitees: new[] { CalendarTestHelpers.Invitee(room.Email, "Room 2") },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(updateResponse, HttpStatusCode.OK);
            var updated = await CalendarTestHelpers.ReadDataAsync(updateResponse);
            Assert.Equal("accepted", updated.GetProperty("attendees")[0].GetProperty("response_status").GetString());

            await using var context = CalendarTestHelpers.CreateContext();
            var attendee = await context.CalendarEventAttendees
                .AsNoTracking()
                .FirstAsync(a => a.TenantId == tenantId && a.EventId == eventId);
            Assert.Equal("accepted", attendee.ResponseStatus);
            Assert.Null(attendee.TokenHash);

            var mirror = await context.CalendarEvents
                .AsNoTracking()
                .FirstAsync(e => e.TenantId == tenantId && e.Id == attendee.MirroredEventId!.Value);

            // timestamptz keeps microseconds, so compare with a tolerance rather than ticks.
            Assert.Equal(movedStart.ToUniversalTime(), mirror.StartTime, TimeSpan.FromMilliseconds(1));
            Assert.Equal(movedStart.AddHours(2).ToUniversalTime(), mirror.EndTime, TimeSpan.FromMilliseconds(1));
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Inviting_a_shared_mailbox_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        var sharedAddress = $"team-{Guid.NewGuid():N}@test.local";

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, sharedAddress, kind: "shared");

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(2);
            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Support handover",
                    start,
                    start.AddHours(1),
                    invitees: new[] { CalendarTestHelpers.Invitee(sharedAddress) })));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Inviting_a_group_address_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        var groupAddress = $"all-{Guid.NewGuid():N}@test.local";

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedGroupAsync(tenantId, groupAddress);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(2);
            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "All hands",
                    start,
                    start.AddHours(1),
                    invitees: new[] { CalendarTestHelpers.Invitee(groupAddress) })));

            // A distribution list cannot answer an invitation, so accepting it would drop the meeting.
            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task The_resource_directory_lists_rooms_alongside_people()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        var colleague = CalendarTestHelpers.NewUser();
        var room = CalendarTestHelpers.NewUser(email: $"room-{Guid.NewGuid():N}@test.local", name: "Room 3", isService: true);

        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer, colleague, room });

        try
        {
            using var client = _factory.CreateClient();
            using var response = await client.SendAsync(
                CalendarTestHelpers.Request(HttpMethod.Get, "/api/v1/calendar/directory", tenantId, organizer.Id));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.OK);
            var data = await CalendarTestHelpers.ReadDataAsync(response);

            var entries = data.EnumerateArray().ToList();
            Assert.Contains(entries, e => e.GetProperty("email").GetString() == colleague.Email);
            var roomEntry = entries.Single(e => e.GetProperty("email").GetString() == room.Email);
            Assert.Equal("resource", roomEntry.GetProperty("kind").GetString());
            Assert.Equal(room.Id.ToString(), roomEntry.GetProperty("user_id").GetString());
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }
}
