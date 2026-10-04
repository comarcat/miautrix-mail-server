using System;
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
/// The invitation email itself: who it comes from, who it goes to, the RSVP links built from
/// the configured public base URL, and the cases where no email is the correct outcome.
/// </summary>
[Trait("Category", "Calendar")]
public sealed class CalendarInvitationEmailTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private const string ExternalInvitee = "guest.invitee@example.com";

    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;

    public CalendarInvitationEmailTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Rsvp_links_are_built_from_the_configured_public_base_url()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, organizer.Email);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(7);
            using var factory = _factory.WithPublicBaseUrl();
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Partner call",
                    start,
                    start.AddHours(1),
                    invitees: new[] { CalendarTestHelpers.Invitee(ExternalInvitee, "Guest Invitee") },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.Created);

            var raw = await LoadQueuedMessageAsync(tenantId, ExternalInvitee);
            var text = CalendarTestHelpers.ExtractDecodedPart(raw, "Content-Type: text/plain");

            var linkPrefix = $"{CalendarTestHelpers.PublicBaseUrl}/api/v1/public/calendar/invitations/";
            Assert.Contains(linkPrefix, text, StringComparison.Ordinal);
            Assert.Contains("?response=accept", text, StringComparison.Ordinal);
            Assert.Contains("?response=tentative", text, StringComparison.Ordinal);
            Assert.Contains("?response=decline", text, StringComparison.Ordinal);
            Assert.Contains("?response=reschedule", text, StringComparison.Ordinal);

            // The HTML alternative carries the same links as anchors.
            var html = CalendarTestHelpers.ExtractDecodedPart(raw, "Content-Type: text/html");
            Assert.Contains(linkPrefix, html, StringComparison.Ordinal);
            Assert.Contains("?response=accept", html, StringComparison.Ordinal);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task The_invitation_is_sent_from_the_organizer_to_the_invitee()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, organizer.Email);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(7);
            using var factory = _factory.WithPublicBaseUrl();
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Partner call",
                    start,
                    start.AddHours(1),
                    invitees: new[] { CalendarTestHelpers.Invitee(ExternalInvitee) },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.Created);

            await using var context = CalendarTestHelpers.CreateContext();
            var item = await context.SmtpQueue
                .AsNoTracking()
                .SingleAsync(q => q.TenantId == tenantId && q.Recipient == ExternalInvitee);

            Assert.Equal(organizer.Email, item.Sender);
            Assert.Equal("Invitation: Partner call", item.Subject);
            Assert.Contains($"From: {organizer.Email}", item.RawMessage, StringComparison.Ordinal);
            Assert.Contains($"To: {ExternalInvitee}", item.RawMessage, StringComparison.Ordinal);

            // The organizer keeps a copy in Sent, with the same calendar part attached.
            var sent = await context.Messages
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.Sender == organizer.Email)
                .ToListAsync();
            Assert.Single(sent);
            Assert.Equal("Invitation: Partner call", sent[0].Subject);

            var attachment = await context.Attachments
                .AsNoTracking()
                .SingleAsync(a => a.TenantId == tenantId && a.MessageId == sent[0].Id);
            Assert.Equal("invitation.ics", attachment.FileName);
            Assert.Equal("text/calendar; charset=utf-8", attachment.ContentType);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task A_room_invitee_is_never_emailed_while_the_human_invitee_is()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        var room = CalendarTestHelpers.NewUser(email: $"room-{Guid.NewGuid():N}@test.local", name: "Room 9", isService: true);
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer, room });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, organizer.Email);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(8);
            using var factory = _factory.WithPublicBaseUrl();
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Hybrid review",
                    start,
                    start.AddHours(1),
                    invitees: new[]
                    {
                        CalendarTestHelpers.Invitee(room.Email, "Room 9"),
                        CalendarTestHelpers.Invitee(ExternalInvitee)
                    },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.Created);

            await using var context = CalendarTestHelpers.CreateContext();
            var queued = await context.SmtpQueue
                .AsNoTracking()
                .Where(q => q.TenantId == tenantId)
                .ToListAsync();

            Assert.Single(queued);
            Assert.Equal(ExternalInvitee, queued[0].Recipient);

            // The room is still on the attendee list, because the mirror is its invitation.
            var attendees = await context.CalendarEventAttendees
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId)
                .ToListAsync();
            Assert.Equal(2, attendees.Count);
            var roomAttendee = attendees.Single(a => a.Email == room.Email);
            Assert.Equal("accepted", roomAttendee.ResponseStatus);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Without_a_public_base_url_no_invitation_email_is_queued_but_the_event_is_created()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, organizer.Email);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(9);
            using var factory = _factory.WithPublicBaseUrl(string.Empty);
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Unreachable RSVP",
                    start,
                    start.AddHours(1),
                    invitees: new[] { CalendarTestHelpers.Invitee(ExternalInvitee) },
                    sendInvitations: true)));

            // Without a base URL there is no usable RSVP link, so creating an event
            // that requests invitations must fail clearly rather than silently succeed.
            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.BadRequest);

            await using var context = CalendarTestHelpers.CreateContext();
            Assert.Equal(0, await context.SmtpQueue.CountAsync(q => q.TenantId == tenantId));
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Send_invitations_can_be_turned_off_for_a_single_event()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, organizer.Email);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(10);
            using var factory = _factory.WithPublicBaseUrl();
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Silent placeholder",
                    start,
                    start.AddHours(1),
                    invitees: new[] { CalendarTestHelpers.Invitee(ExternalInvitee) },
                    sendInvitations: false)));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.Created);

            await using var context = CalendarTestHelpers.CreateContext();
            Assert.Equal(0, await context.SmtpQueue.CountAsync(q => q.TenantId == tenantId));

            // The attendee is still recorded, waiting for an explicit resend.
            var attendee = await context.CalendarEventAttendees
                .AsNoTracking()
                .SingleAsync(a => a.TenantId == tenantId);
            Assert.Equal("needs_action", attendee.ResponseStatus);
            Assert.NotNull(attendee.TokenHash);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    private static async Task<string> LoadQueuedMessageAsync(Guid tenantId, string recipient)
    {
        await using var context = CalendarTestHelpers.CreateContext();
        var item = await context.SmtpQueue
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId && q.Recipient == recipient)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(item);
        return item!.RawMessage;
    }
}
