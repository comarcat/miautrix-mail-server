using System.Net;
using System.Text;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/public/calendar")]
public sealed class PublicCalendarController : ControllerBase
{
    private readonly ICalendarService _calendar;

    public PublicCalendarController(ICalendarService calendar)
    {
        _calendar = calendar;
    }

    [HttpGet("invitations/{token}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> ViewInvitation(string token, [FromQuery] string? response, CancellationToken cancellationToken = default)
    {
        var invitation = await _calendar.GetInvitationByTokenAsync(token, cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        ApplyNoStoreHeaders();
        return Results.Content(RenderInvitationPage(token, invitation, response), "text/html", Encoding.UTF8, StatusCodes.Status200OK);
    }

    [HttpPost("invitations/{token}/respond")]
    [ProducesResponseType(typeof(ApiResponse<CalendarInvitationViewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Respond(string token, [FromBody] CalendarRsvpRequest request, CancellationToken cancellationToken = default)
    {
        var invitation = await _calendar.RespondToInvitationAsync(token, request, cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        ApplyNoStoreHeaders();
        return Results.Json(new ApiResponse<CalendarInvitationViewDto>(invitation), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    private void ApplyNoStoreHeaders()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    private static string RenderInvitationPage(string token, CalendarInvitationViewDto invitation, string? response)
    {
        var selected = WebUtility.HtmlEncode(response ?? string.Empty);
        var title = WebUtility.HtmlEncode(invitation.Title);
        var time = WebUtility.HtmlEncode($"{invitation.StartTime:u} - {invitation.EndTime:u}");
        var location = WebUtility.HtmlEncode(invitation.Location ?? "No location");
        var organizer = WebUtility.HtmlEncode(invitation.OrganizerEmail ?? "Organizer");
        var status = WebUtility.HtmlEncode(invitation.ResponseStatus);
        var encodedToken = WebUtility.HtmlEncode(token);

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <meta name=\"robots\" content=\"noindex\">");
        sb.AppendLine("  <title>Meeting invitation</title>");
        sb.AppendLine("  <style>body{font-family:system-ui,sans-serif;max-width:720px;margin:40px auto;padding:0 20px;color:#111827}button,input,textarea{font:inherit}button{margin:6px 6px 6px 0;padding:8px 14px}label{display:block;margin-top:12px}.card{border:1px solid #d1d5db;border-radius:8px;padding:20px}</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <main class=\"card\">");
        sb.Append("    <h1>").Append(title).AppendLine("</h1>");
        sb.Append("    <p><strong>When:</strong> ").Append(time).AppendLine("</p>");
        sb.Append("    <p><strong>Location:</strong> ").Append(location).AppendLine("</p>");
        sb.Append("    <p><strong>Organizer:</strong> ").Append(organizer).AppendLine("</p>");
        sb.Append("    <p><strong>Your response:</strong> ").Append(status).AppendLine("</p>");
        sb.AppendLine("    <form id=\"rsvp\">");
        sb.Append("      <input type=\"hidden\" name=\"response\" value=\"").Append(selected).AppendLine("\">");
        sb.AppendLine("      <div>");
        sb.AppendLine("        <button type=\"button\" data-response=\"accept\">Accept</button>");
        sb.AppendLine("        <button type=\"button\" data-response=\"tentative\">Tentative</button>");
        sb.AppendLine("        <button type=\"button\" data-response=\"decline\">Decline</button>");
        sb.AppendLine("        <button type=\"button\" data-response=\"reschedule\">Request reschedule</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <label>Proposed start <input name=\"proposed_start_time\" type=\"datetime-local\"></label>");
        sb.AppendLine("      <label>Proposed end <input name=\"proposed_end_time\" type=\"datetime-local\"></label>");
        sb.AppendLine("      <label>Note <textarea name=\"note\" rows=\"3\"></textarea></label>");
        sb.AppendLine("    </form>");
        sb.AppendLine("    <p id=\"status\"></p>");
        sb.AppendLine("  </main>");
        sb.AppendLine("  <script>");
        sb.AppendLine("    const form = document.getElementById('rsvp');");
        sb.AppendLine("    const status = document.getElementById('status');");
        sb.AppendLine("    async function submit(response) {");
        sb.AppendLine("      const data = new FormData(form);");
        sb.AppendLine("      const payload = { response };");
        sb.AppendLine("      if (response === 'reschedule') {");
        sb.AppendLine("        payload.proposed_start_time = data.get('proposed_start_time');");
        sb.AppendLine("        payload.proposed_end_time = data.get('proposed_end_time');");
        sb.AppendLine("        payload.note = data.get('note');");
        sb.AppendLine("      }");
        sb.Append("      const res = await fetch('/api/v1/public/calendar/invitations/").Append(encodedToken).AppendLine("/respond', {");
        sb.AppendLine("        method: 'POST',");
        sb.AppendLine("        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },");
        sb.AppendLine("        body: JSON.stringify(payload)");
        sb.AppendLine("      });");
        sb.AppendLine("      status.textContent = res.ok ? 'Response saved.' : 'The invitation could not be updated.';");
        sb.AppendLine("    }");
        sb.AppendLine("    document.querySelectorAll('button[data-response]').forEach(b => b.addEventListener('click', () => submit(b.dataset.response)));");
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
