using System.Text.Json.Serialization;

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
    bool CanEdit = false,
    bool IsService = false);

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
    string? Description,
    string? Organizer,
    string Status,
    string Visibility,
    string ShowAs,
    bool IsOwn,
    bool CanEdit,
    string? RecurrenceFrequency = null,
    int RecurrenceInterval = 1,
    DateTimeOffset? RecurrenceUntil = null,
    IReadOnlyList<CalendarAttendeeDto>? Attendees = null);

public sealed record DirectoryParticipantDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string Kind);

public sealed record SubscriptionDto(
    Guid UserId,
    string DisplayName,
    string Email);

public sealed record SubscriptionRequest(
    Guid UserId);

public sealed record AvailabilityCompareRequest(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    IReadOnlyList<Guid> ParticipantIds);

public sealed record AvailabilityCompareDto(
    IReadOnlyList<CalendarAvailabilityDto> Participants,
    IReadOnlyList<CalendarConflictDto> Conflicts,
    bool AllAvailable);

public sealed record CalendarConflictDto(
    Guid ParticipantId,
    string ParticipantName,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);

public sealed record CalendarEventRequest(
    string Title,
    string? Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Location,
    string? Organizer,
    string Status,
    string Visibility,
    string ShowAs,
    IReadOnlyList<CalendarInviteeRequest>? Invitees = null,
    bool? SendInvitations = null,
    string? RecurrenceFrequency = null,
    int RecurrenceInterval = 1,
    DateTimeOffset? RecurrenceUntil = null);

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
    [property: JsonPropertyName("response")] string Response,
    [property: JsonPropertyName("proposed_start_time")] DateTimeOffset? ProposedStartTime = null,
    [property: JsonPropertyName("proposed_end_time")] DateTimeOffset? ProposedEndTime = null,
    [property: JsonPropertyName("note")] string? Note = null);

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
