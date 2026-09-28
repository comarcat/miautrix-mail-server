using System.Net;
using Miautrix.Mail.Domain;

namespace Miautrix.Mail.Application.Mail;

public static class CalendarInvitationBuilder
{
    public static (string Subject, string BodyText, string BodyHtml) BuildInvitation(
        CalendarEvent calendarEvent,
        CalendarEventAttendee attendee,
        string rawToken,
        string publicBaseUrl)
    {
        var baseUrl = publicBaseUrl.TrimEnd('/');
        var link = $"{baseUrl}/api/v1/public/calendar/invitations/{rawToken}";
        var accept = $"{link}?response=accept";
        var tentative = $"{link}?response=tentative";
        var decline = $"{link}?response=decline";
        var reschedule = $"{link}?response=reschedule";
        var subject = $"Meeting invitation: {calendarEvent.Title}";
        var when = $"{calendarEvent.StartTime:u} - {calendarEvent.EndTime:u}";
        var location = string.IsNullOrWhiteSpace(calendarEvent.Location) ? "No location" : calendarEvent.Location;
        var organizer = string.IsNullOrWhiteSpace(calendarEvent.Organizer) ? "Organizer" : calendarEvent.Organizer;

        var bodyText = $"""
        You have been invited to a meeting.

        Title: {calendarEvent.Title}
        When: {when}
        Location: {location}
        Organizer: {organizer}

        Accept: {accept}
        Tentative: {tentative}
        Decline: {decline}
        Request reschedule: {reschedule}
        """;

        var bodyHtml = $"""
        <p>You have been invited to a meeting.</p>
        <dl>
          <dt>Title</dt><dd>{WebUtility.HtmlEncode(calendarEvent.Title)}</dd>
          <dt>When</dt><dd>{WebUtility.HtmlEncode(when)}</dd>
          <dt>Location</dt><dd>{WebUtility.HtmlEncode(location)}</dd>
          <dt>Organizer</dt><dd>{WebUtility.HtmlEncode(organizer)}</dd>
        </dl>
        <p>
          <a href="{WebUtility.HtmlEncode(accept)}">Accept</a> |
          <a href="{WebUtility.HtmlEncode(tentative)}">Tentative</a> |
          <a href="{WebUtility.HtmlEncode(decline)}">Decline</a> |
          <a href="{WebUtility.HtmlEncode(reschedule)}">Request reschedule</a>
        </p>
        """;

        return (subject, bodyText, bodyHtml);
    }
}
