using System.Text;
using Miautrix.Mail.Domain;

namespace Miautrix.Mail.Application.Mail;

/// <summary>
/// The iCalendar method used for an outbound scheduling message. A cancellation is the same
/// VEVENT shape with a different method and status, which is what makes Outlook, Google
/// Calendar and Apple Calendar withdraw the entry instead of adding a duplicate.
/// </summary>
public enum CalendarIcalMethod
{
    Request,
    Cancel
}

/// <summary>
/// Builds RFC 5545 <c>VCALENDAR</c>/<c>VEVENT</c> payloads. Kept separate from the email body
/// builder because a calendar client reads this part directly and must not find HTML in it.
/// </summary>
public static class CalendarIcalBuilder
{
    private const string ProductId = "-//Miautrix//Miautrix Mail Server//EN";
    private const int FoldOctetLimit = 75;

    public static byte[] BuildBytes(
        CalendarEvent calendarEvent,
        string organizerEmail,
        IReadOnlyList<CalendarEventAttendee> attendees,
        CalendarIcalMethod method) =>
        Encoding.UTF8.GetBytes(Build(calendarEvent, organizerEmail, attendees, method));

    public static string Build(
        CalendarEvent calendarEvent,
        string organizerEmail,
        IReadOnlyList<CalendarEventAttendee> attendees,
        CalendarIcalMethod method)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            $"PRODID:{ProductId}",
            "CALSCALE:GREGORIAN",
            method == CalendarIcalMethod.Cancel ? "METHOD:CANCEL" : "METHOD:REQUEST",
            "BEGIN:VEVENT",
            $"UID:{BuildUid(calendarEvent.Id)}",
            $"DTSTAMP:{FormatUtc(DateTimeOffset.UtcNow)}",
            $"DTSTART:{FormatUtc(calendarEvent.StartTime)}",
            $"DTEND:{FormatUtc(calendarEvent.EndTime)}"
        };

        if (!string.IsNullOrWhiteSpace(calendarEvent.RecurrenceFrequency))
        {
            var rrule = $"RRULE:FREQ={calendarEvent.RecurrenceFrequency.ToUpperInvariant()};INTERVAL={calendarEvent.RecurrenceInterval}";
            if (calendarEvent.RecurrenceUntil.HasValue)
            {
                rrule += $";UNTIL={FormatUtc(calendarEvent.RecurrenceUntil.Value)}";
            }
            lines.Add(rrule);
        }

        lines.Add($"SEQUENCE:{calendarEvent.Sequence}");
        lines.Add($"SUMMARY:{Escape(calendarEvent.Title)}");

        if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
        {
            lines.Add($"DESCRIPTION:{Escape(calendarEvent.Description)}");
        }

        if (!string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            lines.Add($"LOCATION:{Escape(calendarEvent.Location)}");
        }

        if (!string.IsNullOrWhiteSpace(organizerEmail))
        {
            lines.Add($"ORGANIZER;CN={EscapeParameter(organizerEmail)}:mailto:{organizerEmail}");
        }

        if (method == CalendarIcalMethod.Cancel)
        {
            lines.Add("STATUS:CANCELLED");
        }
        else
        {
            lines.Add($"STATUS:{IcalStatus(calendarEvent.Status)}");
            lines.Add($"TRANSP:{(string.Equals(calendarEvent.ShowAs, "free", StringComparison.OrdinalIgnoreCase) ? "TRANSPARENT" : "OPAQUE")}");
        }

        foreach (var attendee in attendees)
        {
            lines.Add(BuildAttendeeLine(attendee, method));
        }

        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        // The trailing CRLF after END:VCALENDAR is required by RFC 5545 section 3.1.
        return string.Concat(lines.Select(Fold).Select(line => line + "\r\n"));
    }

    public static string BuildUid(Guid eventId) => $"{eventId:D}@miautrix.mail";

    private static string BuildAttendeeLine(CalendarEventAttendee attendee, CalendarIcalMethod method)
    {
        var name = string.IsNullOrWhiteSpace(attendee.DisplayName) ? attendee.Email : attendee.DisplayName;
        var partStat = method == CalendarIcalMethod.Cancel ? "NEEDS-ACTION" : IcalPartStat(attendee.ResponseStatus);
        var rsvp = method == CalendarIcalMethod.Cancel || partStat != "NEEDS-ACTION" ? "FALSE" : "TRUE";
        var role = string.Equals(attendee.Role, "optional", StringComparison.OrdinalIgnoreCase)
            ? "OPT-PARTICIPANT"
            : "REQ-PARTICIPANT";

        return $"ATTENDEE;CN={EscapeParameter(name)};ROLE={role};PARTSTAT={partStat};RSVP={rsvp}:mailto:{attendee.Email}";
    }

    private static string IcalStatus(string status) => status.ToLowerInvariant() switch
    {
        "cancelled" => "CANCELLED",
        "tentative" => "TENTATIVE",
        _ => "CONFIRMED"
    };

    private static string IcalPartStat(string responseStatus) => responseStatus.ToLowerInvariant() switch
    {
        "accepted" => "ACCEPTED",
        "tentative" => "TENTATIVE",
        "declined" => "DECLINED",
        _ => "NEEDS-ACTION"
    };

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case ';':
                    builder.Append("\\;");
                    break;
                case ',':
                    builder.Append("\\,");
                    break;
                case '\r':
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string EscapeParameter(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Folds a content line at 75 octets without splitting a UTF-8 sequence. A continuation
    /// line starts with a single space, which the reading client strips.
    /// </summary>
    private static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= FoldOctetLimit)
        {
            return line;
        }

        var builder = new StringBuilder();
        var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var runeOctets = Encoding.UTF8.GetByteCount(rune.ToString());
            if (octets + runeOctets > FoldOctetLimit)
            {
                builder.Append("\r\n ");
                octets = 1;
            }

            builder.Append(rune.ToString());
            octets += runeOctets;
        }

        return builder.ToString();
    }
}
