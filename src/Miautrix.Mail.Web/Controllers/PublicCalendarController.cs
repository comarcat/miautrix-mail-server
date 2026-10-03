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

    [HttpGet("invitations/{token}/proposal")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> ResolveProposal(string token, [FromQuery] string action, CancellationToken cancellationToken = default)
    {
        var invitation = await _calendar.ResolveRescheduleProposalAsync(token, action, cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        ApplyNoStoreHeaders();
        var encodedAction = WebUtility.HtmlEncode(action.Equals("accept", StringComparison.OrdinalIgnoreCase) ? "accepted" : "declined");
        return Results.Content(RenderMessagePage($"Reschedule proposal {encodedAction}.", invitation), "text/html", Encoding.UTF8, StatusCodes.Status200OK);
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

    private static string RenderMessagePage(string message, CalendarInvitationViewDto invitation)
    {
        var safeMessage = WebUtility.HtmlEncode(message);
        var title = WebUtility.HtmlEncode(invitation.Title);
        var startIso = WebUtility.HtmlEncode(invitation.StartTime.ToUniversalTime().ToString("O"));
        var endIso = WebUtility.HtmlEncode(invitation.EndTime.ToUniversalTime().ToString("O"));
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\"><title>Meeting invitation</title>");
        sb.AppendLine("<style>body{font-family:system-ui,sans-serif;max-width:720px;margin:40px auto;padding:0 20px;color:#111827}.card{border:1px solid #d1d5db;border-radius:8px;padding:20px}</style></head><body><main class=\"card\">");
        sb.Append("<h1>").Append(safeMessage).AppendLine("</h1>");
        sb.Append("<p><strong>Meeting:</strong> ").Append(title).AppendLine("</p>");
        sb.Append("<p><strong>When:</strong> <span id=\"when\" data-start=\"").Append(startIso).Append("\" data-end=\"").Append(endIso).AppendLine("\"></span></p>");
        sb.AppendLine("</main><script>const w=document.getElementById('when');const s=new Date(w.dataset.start);const e=new Date(w.dataset.end);const f=new Intl.DateTimeFormat(undefined,{dateStyle:'full',timeStyle:'short'});w.textContent=`${f.format(s)} - ${e.toLocaleTimeString(undefined,{hour:'numeric',minute:'2-digit'})}`;</script></body></html>");
        return sb.ToString();
    }

    private static string RenderInvitationPage(string token, CalendarInvitationViewDto invitation, string? response)
    {
        var selected = WebUtility.HtmlEncode(response ?? string.Empty);
        var title = WebUtility.HtmlEncode(invitation.Title);
        var startIso = WebUtility.HtmlEncode(invitation.StartTime.ToUniversalTime().ToString("O"));
        var endIso = WebUtility.HtmlEncode(invitation.EndTime.ToUniversalTime().ToString("O"));
        var location = WebUtility.HtmlEncode(invitation.Location ?? "Remote");
        var organizer = WebUtility.HtmlEncode(invitation.OrganizerEmail ?? "Organizer");
        var status = WebUtility.HtmlEncode(invitation.ResponseStatus.Replace('_', ' '));

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\"><title>Meeting invitation</title>");
        sb.AppendLine("<style>body{font-family:system-ui,Segoe UI,Arial,sans-serif;background:#f6f7fb;margin:0;padding:32px;color:#1a1a1a}.card{max-width:720px;margin:0 auto;background:#fff;border:1px solid #d1d5db;border-radius:14px;box-shadow:0 12px 30px rgba(15,23,42,.08);padding:24px}h1{margin:0 0 12px;font-size:22px}.meta{border-collapse:collapse;margin:0 0 18px;width:100%}.meta td{padding:5px 8px 5px 0;vertical-align:top}.label{color:#5a5a5a;width:110px}.actions{display:flex;flex-wrap:wrap;gap:16px;margin:18px 0}.btn{border:0;border-radius:8px;color:#fff;cursor:pointer;font:inherit;font-weight:700;padding:10px 14px}.link{cursor:pointer;font-weight:700;color:#2f5d8a;text-decoration:underline}.reschedule{background:#2f5d8a}.panel{display:none;border-top:1px solid #e5e7eb;margin-top:12px;padding-top:12px}label{display:block;color:#5a5a5a;font-size:13px;margin:8px 0 4px}input,textarea{box-sizing:border-box;width:100%;border:1px solid #cbd5e1;border-radius:8px;font:inherit;padding:9px}#status{color:#5a5a5a}.close{display:none;color:#5a5a5a;font-size:13px}</style></head><body><main class=\"card\">");
        sb.Append("<p style=\"margin:0 0 12px\">You have been invited to a meeting.</p><h1>").Append(title).AppendLine("</h1>");
        sb.AppendLine("<table class=\"meta\">");
        sb.Append("<tr><td class=\"label\">When</td><td><span id=\"when\" data-start=\"").Append(startIso).Append("\" data-end=\"").Append(endIso).AppendLine("\"></span></td></tr>");
        sb.AppendLine("<tr><td class=\"label\">Timezone</td><td>Meeting time above is UTC. Calendar clients and the web RSVP page show this in your local PC timezone.</td></tr>");
        sb.Append("<tr><td class=\"label\">Location</td><td>").Append(location).AppendLine("</td></tr>");
        sb.Append("<tr><td class=\"label\">Organizer</td><td>").Append(organizer).AppendLine("</td></tr>");
        sb.Append("<tr><td class=\"label\">Your response</td><td id=\"current-status\">").Append(status).AppendLine("</td></tr></table>");
        sb.Append("<input type=\"hidden\" id=\"selected-response\" value=\"").Append(selected).AppendLine("\">");
        sb.AppendLine("<div class=\"actions\"><a class=\"link\" data-response=\"accept\">Accept</a><a class=\"link\" data-response=\"tentative\">Tentative</a><a class=\"link\" data-response=\"decline\">Decline</a><button class=\"btn reschedule\" id=\"show-reschedule\" type=\"button\">Request a different time</button></div>");
        sb.AppendLine("<section class=\"panel\" id=\"reschedule-panel\"><label>Proposed start</label><input id=\"proposed-start\" type=\"datetime-local\"><label>Proposed end</label><input id=\"proposed-end\" type=\"datetime-local\"><label>Note</label><textarea id=\"note\" rows=\"3\"></textarea><div class=\"actions\"><button class=\"btn reschedule\" type=\"button\" data-response=\"reschedule\">Send reschedule request</button></div></section>");
        sb.AppendLine("<p id=\"status\"></p><p id=\"close\" class=\"close\">You can close this window now.</p>");
        sb.AppendLine("</main><script>");
        sb.AppendLine("const when=document.getElementById('when');const status=document.getElementById('status');const closeHint=document.getElementById('close');const currentStatus=document.getElementById('current-status');const fmt=new Intl.DateTimeFormat(undefined,{dateStyle:'full',timeStyle:'short'});const s=new Date(when.dataset.start);const e=new Date(when.dataset.end);when.textContent=`${fmt.format(s)} - ${e.toLocaleTimeString(undefined,{hour:'numeric',minute:'2-digit'})}`;");
        sb.AppendLine("function localDateTimeToIso(value){if(!value)return null;const parsed=new Date(value);return Number.isNaN(parsed.getTime())?null:parsed.toISOString();}");
        sb.AppendLine("document.getElementById('show-reschedule').addEventListener('click',()=>{document.getElementById('reschedule-panel').style.display='block';});");
        var encodedToken = Uri.EscapeDataString(token);
        sb.Append("async function submit(response){const payload={response};if(response==='reschedule'){const proposedStart=localDateTimeToIso(document.getElementById('proposed-start').value);const proposedEnd=localDateTimeToIso(document.getElementById('proposed-end').value);if(!proposedStart||!proposedEnd){status.textContent='Choose a proposed start and end time.';return;}payload.proposed_start_time=proposedStart;payload.proposed_end_time=proposedEnd;payload.note=document.getElementById('note').value;}status.textContent='Saving response...';document.querySelectorAll('button,.link').forEach(b=>b.disabled=true);try{const res=await fetch('/api/v1/public/calendar/invitations/");
        sb.Append(encodedToken).Append("/respond',{method:'POST',headers:{'Content-Type':'application/json','Accept':'application/json'},body:JSON.stringify(payload)});if(res.ok){currentStatus.textContent=response.replace('_',' ');status.textContent='Response saved.';closeHint.style.display='block';setTimeout(()=>{try{window.close();}catch{}},1200);}else{status.textContent=res.status===404?'The invitation link is invalid, expired, or no longer active.':'The invitation could not be updated.';document.querySelectorAll('button,.link').forEach(b=>b.disabled=false);}}catch{status.textContent='Network error while saving response. Please try again.';document.querySelectorAll('button,.link').forEach(b=>b.disabled=false);}}");
        sb.AppendLine("document.querySelectorAll('.link').forEach(a=>a.addEventListener('click',e=>{e.preventDefault();submit(a.dataset.response);}));document.querySelectorAll('button[data-response]').forEach(b=>b.addEventListener('click',()=>submit(b.dataset.response)));const selected=document.getElementById('selected-response').value;if(['accept','tentative','decline'].includes(selected))submit(selected);");
        sb.AppendLine("</script></body></html>");
        return sb.ToString();
    }
}
