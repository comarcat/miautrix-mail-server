namespace Miautrix.Mail.Application.Mail;

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(Guid tenantId, Guid userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarAvailabilityDto>> ListAvailabilityAsync(Guid tenantId, Guid userId, DateTimeOffset from, DateTimeOffset to, IReadOnlyList<Guid>? userIds, CancellationToken cancellationToken = default);

    Task<CalendarEventDto> CreateEventAsync(Guid tenantId, Guid userId, CalendarEventRequest request, CancellationToken cancellationToken = default);

    Task<CalendarEventDto?> UpdateEventAsync(Guid tenantId, Guid userId, Guid eventId, CalendarEventRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteEventAsync(Guid tenantId, Guid userId, Guid eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarAttendeeDto>?> ListAttendeesAsync(Guid tenantId, Guid userId, Guid eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectoryParticipantDto>> ListDirectoryParticipantsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionDto>> ListSubscriptionsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> AddSubscriptionAsync(Guid tenantId, Guid userId, SubscriptionRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteSubscriptionAsync(Guid tenantId, Guid userId, Guid targetUserId, CancellationToken cancellationToken = default);

    Task<AvailabilityCompareDto> CompareAvailabilityAsync(Guid tenantId, Guid userId, AvailabilityCompareRequest request, CancellationToken cancellationToken = default);

    Task<bool> ResendInvitationsAsync(Guid tenantId, Guid userId, Guid eventId, CancellationToken cancellationToken = default);

    Task<CalendarEventDto?> AcceptRescheduleProposalAsync(Guid tenantId, Guid userId, Guid eventId, Guid attendeeId, CancellationToken cancellationToken = default);

    Task<CalendarEventDto?> DeclineRescheduleProposalAsync(Guid tenantId, Guid userId, Guid eventId, Guid attendeeId, CancellationToken cancellationToken = default);

    Task<CalendarEventDto?> RespondToEventAsync(Guid tenantId, Guid userId, Guid eventId, CalendarRsvpRequest request, CancellationToken cancellationToken = default);

    Task<CalendarInvitationViewDto?> GetInvitationByTokenAsync(string rawToken, CancellationToken cancellationToken = default);

    Task<CalendarInvitationViewDto?> RespondToInvitationAsync(string rawToken, CalendarRsvpRequest request, CancellationToken cancellationToken = default);

    Task<CalendarInvitationViewDto?> ResolveRescheduleProposalAsync(string rawToken, string action, CancellationToken cancellationToken = default);
}
