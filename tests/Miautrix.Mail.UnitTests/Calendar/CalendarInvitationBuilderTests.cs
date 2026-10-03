using System;
using System.Collections.Generic;
using System.Text;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Xunit;

namespace Miautrix.Mail.UnitTests.Calendar;

[Trait("Category", "Calendar")]
public sealed class CalendarInvitationBuilderTests
{
    private static CalendarEvent Event(string title = "Roadmap sync", string? description = "Discuss Q4 roadmap") => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Title = title,
        StartTime = new DateTimeOffset(2026, 10, 5, 14, 0, 0, TimeSpan.Zero),
        EndTime = new DateTimeOffset(2026, 10, 5, 15, 30, 0, TimeSpan.Zero),
        Location = "Boardroom",
        Description = description,
        Organizer = "org@example.com",
        Status = "confirmed",
        ShowAs = "busy",
    };

    private static CalendarEventAttendee Attendee(string email = "guest@example.com") => new()
    {
        Email = email,
        DisplayName = "Guest One",
        Role = "required",
        ResponseStatus = "needs_action",
    };

    [Fact]
    public void Invitation_subject_and_bodies_include_details()
    {
        var evt = Event();
        var attendee = Attendee();
        var msg = CalendarInvitationBuilder.BuildInvitation(
            evt, attendee, "raw-token", "https://mail.example.com",
            new List<CalendarEventAttendee> { attendee }, "org@example.com", CalendarIcalMethod.Request);

        Assert.Equal("Invitation: Roadmap sync", msg.Subject);
        Assert.Contains("Roadmap sync", msg.BodyText, StringComparison.Ordinal);
        Assert.Contains("Boardroom", msg.BodyText, StringComparison.Ordinal);
        Assert.Contains("Discuss Q4 roadmap", msg.BodyText, StringComparison.Ordinal);
        Assert.Contains("1 h 30 min", msg.BodyText, StringComparison.Ordinal);
        Assert.Contains("Roadmap sync", msg.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Rsvp_links_are_present_for_request()
    {
        var evt = Event();
        var attendee = Attendee();
        var msg = CalendarInvitationBuilder.BuildInvitation(
            evt, attendee, "raw-token", "https://mail.example.com/",
            new List<CalendarEventAttendee> { attendee }, "org@example.com", CalendarIcalMethod.Request);

        Assert.Contains("https://mail.example.com/api/v1/public/calendar/invitations/raw-token?response=accept", msg.BodyText, StringComparison.Ordinal);
        Assert.Contains("response=decline", msg.BodyText, StringComparison.Ordinal);
        Assert.Contains("response=reschedule", msg.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_subject_and_no_rsvp_links()
    {
        var evt = Event();
        var attendee = Attendee();
        var msg = CalendarInvitationBuilder.BuildInvitation(
            evt, attendee, "raw-token", "https://mail.example.com",
            new List<CalendarEventAttendee> { attendee }, "org@example.com", CalendarIcalMethod.Cancel);

        Assert.Equal("Cancelled: Roadmap sync", msg.Subject);
        Assert.DoesNotContain("response=accept", msg.BodyText, StringComparison.Ordinal);
        Assert.Contains("cancelled", msg.BodyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Html_body_encodes_dynamic_values()
    {
        var evt = Event(title: "R&D <review>");
        var attendee = Attendee();
        var msg = CalendarInvitationBuilder.BuildInvitation(
            evt, attendee, null, "https://mail.example.com",
            new List<CalendarEventAttendee> { attendee }, "org@example.com", CalendarIcalMethod.Request);

        Assert.Contains("R&amp;D &lt;review&gt;", msg.BodyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<review>", msg.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ical_bytes_are_a_decodable_vevent()
    {
        var evt = Event();
        var attendee = Attendee();
        var msg = CalendarInvitationBuilder.BuildInvitation(
            evt, attendee, "raw-token", "https://mail.example.com",
            new List<CalendarEventAttendee> { attendee }, "org@example.com", CalendarIcalMethod.Request);

        var ical = Encoding.UTF8.GetString(msg.IcalBytes);
        Assert.Contains("BEGIN:VEVENT", ical, StringComparison.Ordinal);
        Assert.Contains("METHOD:REQUEST", ical, StringComparison.Ordinal);
        Assert.Equal("invitation.ics", msg.IcalFileName);
    }

    [Fact]
    public void Also_invited_lists_other_attendees_only()
    {
        var evt = Event();
        var self = Attendee("self@example.com");
        var other = Attendee("other@example.com");
        var msg = CalendarInvitationBuilder.BuildInvitation(
            evt, self, "raw-token", "https://mail.example.com",
            new List<CalendarEventAttendee> { self, other }, "org@example.com", CalendarIcalMethod.Request);

        Assert.Contains("other@example.com", msg.BodyText, StringComparison.Ordinal);
        Assert.Contains("Also invited", msg.BodyText, StringComparison.Ordinal);
    }
}
