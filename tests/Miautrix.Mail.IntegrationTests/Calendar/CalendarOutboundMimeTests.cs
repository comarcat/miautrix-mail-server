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
/// What actually leaves the server: a multipart message whose calendar part is a real
/// <c>VEVENT</c> carrying <c>METHOD:REQUEST</c> (or <c>CANCEL</c> on a withdrawal).
/// </summary>
[Trait("Category", "Calendar")]
public sealed class CalendarOutboundMimeTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private const string ExternalInvitee = "external.guest@example.com";

    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;

    public CalendarOutboundMimeTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task An_external_invitee_receives_a_multipart_message_with_the_calendar_part()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, organizer.Email);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(4);
            using var factory = _factory.WithPublicBaseUrl();
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Vendor kickoff",
                    start,
                    start.AddHours(1),
                    location: "Video call",
                    description: "Agenda: scope and timeline",
                    invitees: new[] { CalendarTestHelpers.Invitee(ExternalInvitee, "External Guest") },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.Created);

            var raw = await LoadQueuedMessageAsync(tenantId, ExternalInvitee);

            Assert.Contains("multipart/mixed", raw, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "Content-Type: text/calendar; charset=utf-8; method=REQUEST; component=VEVENT; name=\"invitation.ics\"",
                raw,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Content-Disposition: attachment; filename=\"invitation.ics\"", raw, StringComparison.OrdinalIgnoreCase);

            var ical = CalendarTestHelpers.UnfoldCalendar(
                CalendarTestHelpers.ExtractDecodedPart(raw, "filename=\"invitation.ics\""));
            Assert.Contains("BEGIN:VCALENDAR", ical, StringComparison.Ordinal);
            Assert.Contains("METHOD:REQUEST", ical, StringComparison.Ordinal);
            Assert.Contains("BEGIN:VEVENT", ical, StringComparison.Ordinal);
            Assert.Contains("SUMMARY:Vendor kickoff", ical, StringComparison.Ordinal);
            Assert.Contains("LOCATION:Video call", ical, StringComparison.Ordinal);
            Assert.Contains($"ATTENDEE;CN=External Guest;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE:mailto:{ExternalInvitee}", ical, StringComparison.Ordinal);
            Assert.Contains($"ORGANIZER;CN={organizer.Email}:mailto:{organizer.Email}", ical, StringComparison.Ordinal);
            Assert.Contains("END:VEVENT", ical, StringComparison.Ordinal);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task The_plain_text_part_is_decodable_and_names_the_agenda()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, organizer.Email);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(4);
            using var factory = _factory.WithPublicBaseUrl();
            using var client = factory.CreateClient();

            using var response = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Budget sync",
                    start,
                    start.AddHours(1),
                    description: "Bring the revised forecast",
                    invitees: new[] { CalendarTestHelpers.Invitee(ExternalInvitee) },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(response, HttpStatusCode.Created);

            var raw = await LoadQueuedMessageAsync(tenantId, ExternalInvitee);
            var text = CalendarTestHelpers.ExtractDecodedPart(raw, "Content-Type: text/plain");

            Assert.Contains("Invitation: Budget sync", raw, StringComparison.Ordinal);
            Assert.Contains("You have been invited to a meeting.", text, StringComparison.Ordinal);
            Assert.Contains("Bring the revised forecast", text, StringComparison.Ordinal);
            Assert.Contains("invitation.ics", text, StringComparison.Ordinal);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Deleting_a_meeting_queues_a_cancellation_without_an_rsvp_link()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, organizer.Email);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(5);
            using var factory = _factory.WithPublicBaseUrl();
            using var client = factory.CreateClient();

            using var createResponse = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Cancelled later",
                    start,
                    start.AddHours(1),
                    invitees: new[] { CalendarTestHelpers.Invitee(ExternalInvitee) },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(createResponse, HttpStatusCode.Created);
            var created = await CalendarTestHelpers.ReadDataAsync(createResponse);
            var eventId = Guid.Parse(created.GetProperty("id").GetString()!);

            using var deleteResponse = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Delete,
                $"/api/v1/calendar/events/{eventId}",
                tenantId,
                organizer.Id));

            await CalendarTestHelpers.AssertStatusAsync(deleteResponse, HttpStatusCode.NoContent);

            await using var context = CalendarTestHelpers.CreateContext();
            var queued = await context.SmtpQueue
                .AsNoTracking()
                .Where(q => q.TenantId == tenantId && q.Recipient == ExternalInvitee)
                .OrderBy(q => q.CreatedAt)
                .ToListAsync();

            Assert.Equal(2, queued.Count);
            var cancellationItem = queued.Single(q => q.Subject == "Cancelled: Cancelled later");
            var cancellation = cancellationItem.RawMessage;

            var ical = CalendarTestHelpers.UnfoldCalendar(
                CalendarTestHelpers.ExtractDecodedPart(cancellation, "filename=\"invitation.ics\""));
            Assert.Contains("METHOD:CANCEL", ical, StringComparison.Ordinal);
            Assert.Contains("STATUS:CANCELLED", ical, StringComparison.Ordinal);
            Assert.Contains("PARTSTAT=NEEDS-ACTION", ical, StringComparison.Ordinal);
            Assert.Contains("RSVP=FALSE", ical, StringComparison.Ordinal);

            // A cancellation is not an invitation: there is nothing to respond to.
            var text = CalendarTestHelpers.ExtractDecodedPart(cancellation, "Content-Type: text/plain");
            Assert.Contains("This meeting has been cancelled.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("?response=accept", text, StringComparison.Ordinal);
        }
        finally
        {
            await CalendarTestHelpers.CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Resending_invitations_queues_the_message_again()
    {
        var tenantId = Guid.NewGuid();
        var organizer = CalendarTestHelpers.NewUser();
        await CalendarTestHelpers.SeedTenantAsync(tenantId, "user", new[] { organizer });
        await CalendarTestHelpers.SeedMailboxAsync(tenantId, organizer.Email);

        try
        {
            var start = DateTimeOffset.UtcNow.AddDays(6);
            using var factory = _factory.WithPublicBaseUrl();
            using var client = factory.CreateClient();

            using var createResponse = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                "/api/v1/calendar/events",
                tenantId,
                organizer.Id,
                CalendarTestHelpers.EventRequest(
                    "Follow up",
                    start,
                    start.AddHours(1),
                    invitees: new[] { CalendarTestHelpers.Invitee(ExternalInvitee) },
                    sendInvitations: true)));

            await CalendarTestHelpers.AssertStatusAsync(createResponse, HttpStatusCode.Created);
            var created = await CalendarTestHelpers.ReadDataAsync(createResponse);
            var eventId = Guid.Parse(created.GetProperty("id").GetString()!);

            using var resendResponse = await client.SendAsync(CalendarTestHelpers.Request(
                HttpMethod.Post,
                $"/api/v1/calendar/events/{eventId}/invitations",
                tenantId,
                organizer.Id));

            await CalendarTestHelpers.AssertStatusAsync(resendResponse, HttpStatusCode.NoContent);

            await using var context = CalendarTestHelpers.CreateContext();
            var queued = await context.SmtpQueue
                .AsNoTracking()
                .CountAsync(q => q.TenantId == tenantId && q.Recipient == ExternalInvitee);

            Assert.Equal(2, queued);
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
