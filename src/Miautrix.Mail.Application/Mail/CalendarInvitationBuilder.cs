using System.Net;
using System.Text;
using Miautrix.Mail.Domain;

namespace Miautrix.Mail.Application.Mail;

/// <summary>
/// The full scheduling message: subject, plain text, HTML, and the <c>.ics</c> bytes that a
/// calendar client opens. Nothing here decides who receives it — the service does.
/// </summary>
public sealed record CalendarInvitationMessage(
    string Subject,
    string BodyText,
    string BodyHtml,
    byte[] IcalBytes,
    string IcalFileName);

public static class CalendarInvitationBuilder
{
    public const string IcalFileName = "invitation.ics";

    public static CalendarInvitationMessage BuildInvitation(
        CalendarEvent calendarEvent,
        CalendarEventAttendee attendee,
        string? rawToken,
        string publicBaseUrl,
        IReadOnlyList<CalendarEventAttendee> attendees,
        string organizerEmail,
        CalendarIcalMethod method)
    {
        var isCancel = method == CalendarIcalMethod.Cancel;
        var baseUrl = publicBaseUrl.TrimEnd('/');
        var link = string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : $"{baseUrl}/api/v1/public/calendar/invitations/{rawToken}";

        var title = string.IsNullOrWhiteSpace(calendarEvent.Title) ? "(No title)" : calendarEvent.Title;
        var subject = isCancel ? $"Cancelled: {title}" : $"Invitation: {title}";
        var when = FormatRange(calendarEvent.StartTime, calendarEvent.EndTime);
        var localWhenHint = "Meeting time above is UTC. Calendar clients and the web RSVP page show this in your local PC timezone.";
        var duration = FormatDuration(calendarEvent.StartTime, calendarEvent.EndTime);
        var location = string.IsNullOrWhiteSpace(calendarEvent.Location) ? "Remote" : calendarEvent.Location;
        var organizer = string.IsNullOrWhiteSpace(organizerEmail)
            ? (string.IsNullOrWhiteSpace(calendarEvent.Organizer) ? "Organizer" : calendarEvent.Organizer)
            : organizerEmail;
        var icalBytes = CalendarIcalBuilder.BuildBytes(calendarEvent, organizerEmail, attendees, method);

        var bodyText = BuildText(
            isCancel, title, when, duration, localWhenHint, location, organizer, calendarEvent.Description,
            organizerEmail, attendee, attendees, link);
        var bodyHtml = BuildHtml(
            isCancel, title, when, duration, localWhenHint, location, organizer, calendarEvent.Description,
            organizerEmail, attendee, attendees, link);

        return new CalendarInvitationMessage(subject, bodyText, bodyHtml, icalBytes, IcalFileName);
    }

    private static string BuildText(
        bool isCancel,
        string title,
        string when,
        string duration,
        string localWhenHint,
        string location,
        string organizer,
        string? description,
        string organizerEmail,
        CalendarEventAttendee attendee,
        IReadOnlyList<CalendarEventAttendee> attendees,
        string? link)
    {
        var builder = new StringBuilder();
        builder.AppendLine(isCancel
            ? "This meeting has been cancelled."
            : "You have been invited to a meeting.");
        builder.AppendLine();
        builder.AppendLine($"Title: {title}");
        builder.AppendLine($"When: {when} ({duration})");
        builder.AppendLine(localWhenHint);
        builder.AppendLine($"Location: {location}");
        builder.AppendLine($"Organizer: {organizer}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.AppendLine();
            builder.AppendLine("Agenda:");
            builder.AppendLine(description);
        }

        var others = attendees
            .Where(a => !a.Email.Equals(attendee.Email, StringComparison.OrdinalIgnoreCase))
            .Select(a => string.IsNullOrWhiteSpace(a.DisplayName) ? a.Email : $"{a.DisplayName} <{a.Email}>")
            .ToList();
        if (others.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(others.Count == 1 ? "Also invited:" : "Also invited:");
            foreach (var other in others)
            {
                builder.AppendLine($"  - {other}");
            }
        }

        if (!isCancel)
        {
            builder.AppendLine();
            if (link is not null)
            {
                builder.AppendLine("Respond:");
                builder.AppendLine($"  Accept: {link}?response=accept");
                builder.AppendLine($"  Tentative: {link}?response=tentative");
                builder.AppendLine($"  Decline: {link}?response=decline");
                builder.AppendLine($"  Request a different time: {link}?response=reschedule");
            }
            else
            {
                builder.AppendLine("Open the attached calendar file to add or respond to this meeting.");
            }
        }

        if (!string.IsNullOrWhiteSpace(organizerEmail))
        {
            builder.AppendLine();
            builder.AppendLine(
                isCancel
                    ? $"Contact the organizer at {organizerEmail} with any questions."
                    : $"Replies to this message go to {organizerEmail}.");
        }

        builder.AppendLine();
        builder.AppendLine($"A calendar file ({IcalFileName}) is attached. Open it to add this to your calendar.");
        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string BuildHtml(
        bool isCancel,
        string title,
        string when,
        string duration,
        string localWhenHint,
        string location,
        string organizer,
        string? description,
        string organizerEmail,
        CalendarEventAttendee attendee,
        IReadOnlyList<CalendarEventAttendee> attendees,
        string? link)
    {
        var builder = new StringBuilder();
        builder.Append("<div style=\"font-family:system-ui,Segoe UI,Arial,sans-serif;font-size:14px;color:#1a1a1a;line-height:1.5\">");
        builder.Append("<p style=\"margin:0 0 12px\">")
            .Append(isCancel ? "This meeting has been <strong>cancelled</strong>." : "You have been invited to a meeting.")
            .Append("</p>");
        builder.Append("<h2 style=\"margin:0 0 12px;font-size:18px\">").Append(E(title)).Append("</h2>");
        builder.Append("<table cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;margin:0 0 12px\">");
        AppendRow(builder, "When", $"{when} ({duration})");
        AppendRow(builder, "Timezone", localWhenHint);
        AppendRow(builder, "Location", location);
        AppendRow(builder, "Organizer", organizer);
        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.Append("<tr><td style=\"vertical-align:top;padding-right:12px;color:#5a5a5a\">Agenda</td><td>")
                .Append(E(description).Replace("\n", "<br />", StringComparison.Ordinal))
                .Append("</td></tr>");
        }

        builder.Append("</table>");

        var others = attendees
            .Where(a => !a.Email.Equals(attendee.Email, StringComparison.OrdinalIgnoreCase))
            .Select(a => string.IsNullOrWhiteSpace(a.DisplayName) ? a.Email : $"{a.DisplayName} ({a.Email})")
            .ToList();
        if (others.Count > 0)
        {
            builder.Append("<p style=\"margin:0 0 6px;color:#5a5a5a\">Also invited</p><ul style=\"margin:0 0 12px;padding-left:20px\">");
            foreach (var other in others)
            {
                builder.Append("<li>").Append(E(other)).Append("</li>");
            }

            builder.Append("</ul>");
        }

        if (!isCancel)
        {
            if (link is not null)
            {
                builder.Append("<p style=\"margin:0 0 12px\">")
                    .Append(Link(link + "?response=accept", "Accept", "#1f7a3f"))
                    .Append(" &nbsp; ")
                    .Append(Link(link + "?response=tentative", "Tentative", "#8a6d1f"))
                    .Append(" &nbsp; ")
                    .Append(Link(link + "?response=decline", "Decline", "#a12b2b"))
                    .Append(" &nbsp; ")
                    .Append(Link(link + "?response=reschedule", "Request a different time", "#2f5d8a"))
                    .Append("</p>");
            }
            else
            {
                builder.Append("<p style=\"margin:0 0 12px;color:#5a5a5a\">Open the attached calendar file to add or respond to this meeting.</p>");
            }
        }

        if (!string.IsNullOrWhiteSpace(organizerEmail))
        {
            builder.Append("<p style=\"margin:0 0 12px;color:#5a5a5a\">")
                .Append(isCancel ? "Questions: " : "Replies go to ")
                .Append(E(organizerEmail))
                .Append("</p>");
        }

        builder.Append("<p style=\"margin:0;color:#5a5a5a;font-size:12px\">")
            .Append("The calendar file <strong>").Append(E(IcalFileName)).Append("</strong> is attached.")
            .Append("</p>");
        builder.Append("</div>");
        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, string label, string value) =>
        builder.Append("<tr><td style=\"vertical-align:top;padding-right:12px;color:#5a5a5a\">")
            .Append(E(label))
            .Append("</td><td>")
            .Append(E(value))
            .Append("</td></tr>");

    private static string Link(string href, string label, string color) =>
        $"<a href=\"{E(href)}\" style=\"color:{color};font-weight:600\" target=\"_blank\">{E(label)}</a>";

    private static string FormatRange(DateTimeOffset start, DateTimeOffset end)
    {
        var utcStart = start.ToUniversalTime();
        var utcEnd = end.ToUniversalTime();
        return $"{utcStart:dddd, d MMMM yyyy HH:mm} - {utcEnd:HH:mm} UTC";
    }

    private static string FormatDuration(DateTimeOffset start, DateTimeOffset end)
    {
        var span = end - start;
        if (span.TotalMinutes < 60)
        {
            return $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} min";
        }

        var hours = (int)span.TotalHours;
        var minutes = span.Minutes;
        return minutes == 0 ? $"{hours} h" : $"{hours} h {minutes} min";
    }

    private static string FormatOffset(TimeSpan offset) =>
        offset == TimeSpan.Zero
            ? "+00:00"
            : $"{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00}";

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
