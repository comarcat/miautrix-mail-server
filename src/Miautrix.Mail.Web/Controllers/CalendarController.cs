using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/calendar")]
public sealed class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendar;
    private readonly IRequestContextAccessor _context;

    public CalendarController(ICalendarService calendar, IRequestContextAccessor context)
    {
        _calendar = calendar;
        _context = context;
    }

    [HttpGet("events")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CalendarEventDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> ListEvents([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        var events = await _calendar.ListEventsAsync(_context.CurrentTenantId, _context.CurrentUserId, from, to, cancellationToken);
        return Results.Json(new ApiResponse<IReadOnlyList<CalendarEventDto>>(events), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("availability")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CalendarAvailabilityDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> Availability([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery(Name = "user_ids")] string? userIds, CancellationToken cancellationToken = default)
    {
        var parsedUserIds = string.IsNullOrWhiteSpace(userIds)
            ? null
            : userIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Guid.Parse).ToList();
        var availability = await _calendar.ListAvailabilityAsync(_context.CurrentTenantId, _context.CurrentUserId, from, to, parsedUserIds, cancellationToken);
        return Results.Json(new ApiResponse<IReadOnlyList<CalendarAvailabilityDto>>(availability), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpPost("events")]
    [ProducesResponseType(typeof(ApiResponse<CalendarEventDto>), StatusCodes.Status201Created)]
    public async Task<IResult> Create([FromBody] CalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        var calendarEvent = await _calendar.CreateEventAsync(_context.CurrentTenantId, _context.CurrentUserId, request, cancellationToken);
        return Results.Json(new ApiResponse<CalendarEventDto>(calendarEvent), ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("events/{eventId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CalendarEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(Guid eventId, [FromBody] CalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        var calendarEvent = await _calendar.UpdateEventAsync(_context.CurrentTenantId, _context.CurrentUserId, eventId, request, cancellationToken);
        return calendarEvent is null
            ? Results.NotFound()
            : Results.Json(new ApiResponse<CalendarEventDto>(calendarEvent), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("events/{eventId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid eventId, CancellationToken cancellationToken = default)
    {
        var deleted = await _calendar.DeleteEventAsync(_context.CurrentTenantId, _context.CurrentUserId, eventId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    [HttpGet("events/{eventId:guid}/attendees")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CalendarAttendeeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> ListAttendees(Guid eventId, CancellationToken cancellationToken = default)
    {
        var attendees = await _calendar.ListAttendeesAsync(_context.CurrentTenantId, _context.CurrentUserId, eventId, cancellationToken);
        return attendees is null
            ? Results.NotFound()
            : Results.Json(new ApiResponse<IReadOnlyList<CalendarAttendeeDto>>(attendees), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpPost("events/{eventId:guid}/invitations")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> ResendInvitations(Guid eventId, CancellationToken cancellationToken = default)
    {
        var sent = await _calendar.ResendInvitationsAsync(_context.CurrentTenantId, _context.CurrentUserId, eventId, cancellationToken);
        return sent ? Results.NoContent() : Results.NotFound();
    }

    [HttpPost("events/{eventId:guid}/attendees/{attendeeId:guid}/accept-proposal")]
    [ProducesResponseType(typeof(ApiResponse<CalendarEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> AcceptProposal(Guid eventId, Guid attendeeId, CancellationToken cancellationToken = default)
    {
        var calendarEvent = await _calendar.AcceptRescheduleProposalAsync(_context.CurrentTenantId, _context.CurrentUserId, eventId, attendeeId, cancellationToken);
        return calendarEvent is null
            ? Results.NotFound()
            : Results.Json(new ApiResponse<CalendarEventDto>(calendarEvent), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }
}
