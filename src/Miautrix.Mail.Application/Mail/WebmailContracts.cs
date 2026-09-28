namespace Miautrix.Mail.Application.Mail;

public sealed record ContactDto(
    Guid Id,
    string Name,
    string Email,
    string? Organization,
    string? Department,
    string? Phone,
    string Book,
    string? Kind = null,
    bool CanEdit = false);

public sealed record ContactRequest(
    string Name,
    string Email,
    string? Organization,
    string? Department,
    string? Phone);

public sealed record CalendarInviteeRequest(
    string Email,
    string? DisplayName = null,
    string Role = "required");

public sealed record CalendarAttendeeDto(
    Guid Id,
    string Email,
    string? DisplayName,
    string Role,
    bool IsExternal,
    string ResponseStatus,
    DateTimeOffset? RespondedAt,
    DateTimeOffset? ProposedStartTime,
    DateTimeOffset? ProposedEndTime,
    string? ProposalNote);

public sealed record CalendarEventDto(
    Guid Id,
    Guid UserId,
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Location,
    string? Organizer,
    string Status,
    string Visibility,
    string ShowAs,
    IReadOnlyList<CalendarAttendeeDto>? Attendees = null);

public sealed record CalendarEventRequest(
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Location,
    string? Organizer,
    string Status,
    string Visibility,
    string ShowAs,
    IReadOnlyList<CalendarInviteeRequest>? Invitees = null,
    bool? SendInvitations = null);

public sealed record CalendarInvitationViewDto(
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Location,
    string? OrganizerEmail,
    string InviteeEmail,
    string ResponseStatus,
    bool CanRespond);

public sealed record CalendarRsvpRequest(
    string Response,
    DateTimeOffset? ProposedStartTime = null,
    DateTimeOffset? ProposedEndTime = null,
    string? Note = null);

public sealed record CalendarAvailabilityDto(
    Guid UserId,
    string DisplayName,
    string Email,
    IReadOnlyList<CalendarBusyBlockDto> Busy);

public sealed record CalendarBusyBlockDto(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string ShowAs,
    string? Title);

public sealed record SieveFilterRuleDto(
    Guid Id,
    string Name,
    string Field,
    string Comparator,
    string Value,
    string Action,
    string? TargetFolder,
    bool Active);

public sealed record SieveFilterRuleRequest(
    string Name,
    string Field,
    string Comparator,
    string Value,
    string Action,
    string? TargetFolder,
    bool Active);

public sealed record SieveRuleActiveRequest(bool Active);
