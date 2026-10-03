using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Xunit;

namespace Miautrix.Mail.UnitTests.Calendar;

[Trait("Category", "Calendar")]
public sealed class CalendarIcalBuilderTests
{
    private static CalendarEvent Event(int sequence = 0, string title = "Sprint Review") => new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        TenantId = Guid.NewGuid(),
        Title = title,
        StartTime = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
        EndTime = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
        Location = "Room A",
        Description = "Quarterly review",
        Organizer = "org@example.com",
        Status = "confirmed",
        ShowAs = "busy",
        Sequence = sequence,
    };

    private static IReadOnlyList<CalendarEventAttendee> Attendees() => new List<CalendarEventAttendee>
    {
        new() { Email = "guest@example.com", DisplayName = "Guest One", Role = "required", ResponseStatus = "needs_action" },
    };

    [Fact]
    public void Uid_is_stable_for_event_id()
    {
        var evt = Event();
        var ical = CalendarIcalBuilder.Build(evt, "org@example.com", Attendees(), CalendarIcalMethod.Request);

        Assert.Contains($"UID:{CalendarIcalBuilder.BuildUid(evt.Id)}", ical, StringComparison.Ordinal);
        Assert.Contains("UID:11111111-2222-3333-4444-555555555555@miautrix.mail", ical, StringComparison.Ordinal);
    }

    [Fact]
    public void Sequence_is_emitted()
    {
        var ical = CalendarIcalBuilder.Build(Event(sequence: 3), "org@example.com", Attendees(), CalendarIcalMethod.Request);

        Assert.Contains("SEQUENCE:3", ical, StringComparison.Ordinal);
    }

    [Fact]
    public void Dates_are_utc()
    {
        var ical = CalendarIcalBuilder.Build(Event(), "org@example.com", Attendees(), CalendarIcalMethod.Request);

        Assert.Contains("DTSTART:20261001T090000Z", ical, StringComparison.Ordinal);
        Assert.Contains("DTEND:20261001T100000Z", ical, StringComparison.Ordinal);
    }

    [Fact]
    public void Request_method_produces_request_and_confirmed()
    {
        var ical = CalendarIcalBuilder.Build(Event(), "org@example.com", Attendees(), CalendarIcalMethod.Request);

        Assert.Contains("METHOD:REQUEST", ical, StringComparison.Ordinal);
        Assert.Contains("STATUS:CONFIRMED", ical, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_method_produces_cancel_and_cancelled()
    {
        var ical = CalendarIcalBuilder.Build(Event(), "org@example.com", Attendees(), CalendarIcalMethod.Cancel);

        Assert.Contains("METHOD:CANCEL", ical, StringComparison.Ordinal);
        Assert.Contains("STATUS:CANCELLED", ical, StringComparison.Ordinal);
    }

    [Fact]
    public void Attendee_line_is_present()
    {
        var ical = CalendarIcalBuilder.Build(Event(), "org@example.com", Attendees(), CalendarIcalMethod.Request);

        Assert.Contains("ATTENDEE;CN=Guest One", ical, StringComparison.Ordinal);
        Assert.Contains("mailto:guest@example.com", ical, StringComparison.Ordinal);
    }

    [Fact]
    public void Special_characters_are_escaped()
    {
        var evt = Event(title: "Review; plan, next\nsteps");
        var ical = CalendarIcalBuilder.Build(evt, "org@example.com", Attendees(), CalendarIcalMethod.Request);

        Assert.Contains(@"SUMMARY:Review\; plan\, next\nsteps", ical, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_summary_is_folded_at_75_octets()
    {
        var evt = Event(title: string.Join(" ", Enumerable.Repeat("scheduling", 20)));
        var ical = CalendarIcalBuilder.Build(evt, "org@example.com", Attendees(), CalendarIcalMethod.Request);

        foreach (var line in ical.Split("\r\n"))
        {
            Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, $"Line exceeds 75 octets: {line}");
        }
    }

    [Fact]
    public void Payload_terminates_with_crlf()
    {
        var ical = CalendarIcalBuilder.Build(Event(), "org@example.com", Attendees(), CalendarIcalMethod.Request);

        Assert.EndsWith("END:VCALENDAR\r\n", ical, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBytes_matches_build_string()
    {
        var evt = Event();
        var attendees = Attendees();
        var str = CalendarIcalBuilder.Build(evt, "org@example.com", attendees, CalendarIcalMethod.Request);
        var bytes = CalendarIcalBuilder.BuildBytes(evt, "org@example.com", attendees, CalendarIcalMethod.Request);

        Assert.Equal(Encoding.UTF8.GetBytes(str), bytes);
    }
}
