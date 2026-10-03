using System.Net;
using System.Net.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Miautrix.Mail.Application.Mail;

public sealed class CalendarService : ICalendarService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase) { "confirmed", "tentative", "cancelled" };
    private static readonly HashSet<string> ValidVisibility = new(StringComparer.OrdinalIgnoreCase) { "private", "public" };
    private static readonly HashSet<string> ValidShowAs = new(StringComparer.OrdinalIgnoreCase) { "free", "busy", "tentative", "out_of_office" };
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase) { "required", "optional" };
    private static readonly HashSet<string> ValidResponses = new(StringComparer.OrdinalIgnoreCase) { "accept", "accepted", "tentative", "decline", "declined", "reschedule" };
    private static readonly HashSet<string> ValidRecurrenceFrequencies = new(StringComparer.OrdinalIgnoreCase) { "none", "daily", "weekly", "monthly" };

    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _auth;
    private readonly IPermissionRepository _permissions;
    private readonly IMessageService _messages;
    private readonly ISessionManager _sessions;
    private readonly CalendarInvitationOptions _invitationOptions;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(
        AppDbContext db,
        ITenantAuthorizationHelper auth,
        IPermissionRepository permissions,
        IMessageService messages,
        ISessionManager sessions,
        CalendarInvitationOptions invitationOptions,
        ILogger<CalendarService> logger)
    {
        _db = db;
        _auth = auth;
        _permissions = permissions;
        _messages = messages;
        _sessions = sessions;
        _invitationOptions = invitationOptions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(Guid tenantId, Guid userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        var fromUtc = from ?? DateTimeOffset.UtcNow.AddMonths(-1);
        var toUtc = to ?? DateTimeOffset.UtcNow.AddMonths(3);
        var canSeeAll = await IsOwnerOrAdminAsync(tenantId, userId, cancellationToken);

        var visibleUserIds = new HashSet<Guid> { userId };
        if (canSeeAll)
        {
            var tenantUserIds = await _db.Users
                .Where(u => u.TenantId == tenantId && u.IsActive && !u.IsService)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            visibleUserIds.UnionWith(tenantUserIds);
        }
        else
        {
            var subscribedUserIds = await _db.CalendarSubscriptions
                .Where(s => s.TenantId == tenantId && s.UserId == userId)
                .Join(
                    _db.Users.Where(u => u.TenantId == tenantId && u.IsActive && !u.IsService),
                    s => s.TargetUserId,
                    u => u.Id,
                    (s, _) => s.TargetUserId)
                .ToListAsync(cancellationToken);
            visibleUserIds.UnionWith(subscribedUserIds);
        }

        var events = await _db.CalendarEvents
            .Where(e => e.TenantId == tenantId && visibleUserIds.Contains(e.UserId) && e.StartTime < toUtc && e.EndTime > fromUtc)
            .OrderBy(e => e.StartTime)
            .ToListAsync(cancellationToken);
        events = ExpandRecurringEvents(events, fromUtc, toUtc);
        var eventIds = events.Select(e => e.Id).Distinct().ToList();
        var attendees = await _db.CalendarEventAttendees
            .Where(a => a.TenantId == tenantId && eventIds.Contains(a.EventId))
            .OrderBy(a => a.Email)
            .ToListAsync(cancellationToken);

        return events.Select(e => ToDto(e, userId, canSeeAll, attendees.Where(a => a.EventId == e.Id))).ToList();
    }

    public async Task<IReadOnlyList<CalendarAvailabilityDto>> ListAvailabilityAsync(Guid tenantId, Guid userId, DateTimeOffset from, DateTimeOffset to, IReadOnlyList<Guid>? userIds, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        if (to <= from)
        {
            throw new ArgumentException("Availability end must be after start.");
        }

        var canSeeAll = await IsOwnerOrAdminAsync(tenantId, userId, cancellationToken);

        // An explicit request may name service accounts, because a room is booked by
        // looking at the room. The default listing stays limited to people.
        var usersQuery = _db.Users.Where(u => u.TenantId == tenantId && u.IsActive);
        usersQuery = userIds is { Count: > 0 }
            ? usersQuery.Where(u => userIds.Contains(u.Id))
            : usersQuery.Where(u => !u.IsService);

        var users = await usersQuery
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Email)
            .ToListAsync(cancellationToken);

        var visibleUserIds = users.Select(u => u.Id).ToHashSet();
        var events = await _db.CalendarEvents
            .Where(e => e.TenantId == tenantId && visibleUserIds.Contains(e.UserId) && e.StartTime < to && e.EndTime > from && e.ShowAs != "free")
            .OrderBy(e => e.StartTime)
            .ToListAsync(cancellationToken);

        return users.Select(u => ToAvailabilityDto(u, userId, canSeeAll, events.Where(e => e.UserId == u.Id))).ToList();
    }

    public async Task<AvailabilityCompareDto> CompareAvailabilityAsync(Guid tenantId, Guid userId, AvailabilityCompareRequest request, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        var start = request.StartTime.ToUniversalTime();
        var end = request.EndTime.ToUniversalTime();
        if (end <= start)
        {
            throw new ArgumentException("Availability end must be after start.");
        }

        var participantIds = request.ParticipantIds.Distinct().ToList();
        if (participantIds.Count > 100)
        {
            throw new ArgumentException("Availability can be compared for up to 100 participants.");
        }

        var canSeeAll = await IsOwnerOrAdminAsync(tenantId, userId, cancellationToken);
        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive && participantIds.Contains(u.Id))
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Email)
            .ToListAsync(cancellationToken);
        if (users.Count != participantIds.Count)
        {
            // A participant from another tenant, or one that is gone, is not a 403: a 403
            // would confirm the id exists somewhere else.
            throw new ResourceNotFoundException();
        }

        var participantUserIds = users.Select(u => u.Id).ToHashSet();
        var events = await _db.CalendarEvents
            .Where(e => e.TenantId == tenantId && participantUserIds.Contains(e.UserId) && e.StartTime < end && e.EndTime > start && e.ShowAs != "free")
            .OrderBy(e => e.StartTime)
            .ToListAsync(cancellationToken);

        var participants = users
            .Select(u => ToAvailabilityDto(u, userId, canSeeAll, events.Where(e => e.UserId == u.Id)))
            .ToList();
        var conflicts = participants
            .Where(p => p.Busy.Count > 0)
            .SelectMany(p => p.Busy.Select(b => new CalendarConflictDto(p.UserId, p.DisplayName, b.StartTime, b.EndTime)))
            .ToList();

        return new AvailabilityCompareDto(participants, conflicts, conflicts.Count == 0);
    }

    public async Task<CalendarEventDto> CreateEventAsync(Guid tenantId, Guid userId, CalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        Validate(request);
        var now = DateTimeOffset.UtcNow;
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(calendarEvent, request, now);
        _db.CalendarEvents.Add(calendarEvent);
        var tokenByAttendee = await AddAttendeesAsync(tenantId, userId, calendarEvent, request.Invitees, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        if (request.SendInvitations != false)
        {
            await SendInvitationsAsync(tenantId, userId, calendarEvent, tokenByAttendee, CalendarIcalMethod.Request, cancellationToken);
        }

        // Read the attendees back rather than reusing the token map: a room holds no RSVP token,
        // so a room-only meeting would otherwise answer with an empty attendee list.
        var attendees = await _db.CalendarEventAttendees.Where(a => a.TenantId == tenantId && a.EventId == calendarEvent.Id).ToListAsync(cancellationToken);
        return ToDto(calendarEvent, userId, canSeeAll: false, attendees);
    }

    public async Task<CalendarEventDto?> UpdateEventAsync(Guid tenantId, Guid userId, Guid eventId, CalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        Validate(request);
        var calendarEvent = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.UserId == userId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return null;
        }

        var timeChanged = calendarEvent.StartTime != request.StartTime.ToUniversalTime() || calendarEvent.EndTime != request.EndTime.ToUniversalTime();
        Apply(calendarEvent, request, DateTimeOffset.UtcNow);
        if (timeChanged)
        {
            // A calendar client ignores an update that does not advance SEQUENCE.
            calendarEvent.Sequence++;
        }

        var tokenByAttendee = await SyncAttendeesAsync(tenantId, userId, calendarEvent, request.Invitees, timeChanged, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        if (request.SendInvitations != false)
        {
            await SendInvitationsAsync(tenantId, userId, calendarEvent, tokenByAttendee, CalendarIcalMethod.Request, cancellationToken);
        }

        var attendees = await _db.CalendarEventAttendees.Where(a => a.TenantId == tenantId && a.EventId == eventId).ToListAsync(cancellationToken);
        return ToDto(calendarEvent, userId, canSeeAll: false, attendees);
    }

    public async Task<bool> DeleteEventAsync(Guid tenantId, Guid userId, Guid eventId, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        var calendarEvent = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.UserId == userId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return false;
        }

        var attendees = await _db.CalendarEventAttendees.Where(a => a.TenantId == tenantId && a.EventId == eventId).ToListAsync(cancellationToken);

        // Cancellations must go out before the attendee rows and their mirrors are deleted,
        // because the rows that describe the recipients live on those rows. Cancellation
        // messages carry no RSVP link, so the empty token map is correct.
        calendarEvent.Sequence++;
        await SendInvitationsAsync(tenantId, userId, calendarEvent, new Dictionary<CalendarEventAttendee, string>(), CalendarIcalMethod.Cancel, cancellationToken);

        var mirroredIds = attendees.Where(a => a.MirroredEventId.HasValue).Select(a => a.MirroredEventId!.Value).ToList();
        if (mirroredIds.Count > 0)
        {
            var mirrors = await _db.CalendarEvents.Where(e => e.TenantId == tenantId && mirroredIds.Contains(e.Id)).ToListAsync(cancellationToken);
            _db.CalendarEvents.RemoveRange(mirrors);
        }

        _db.CalendarEventAttendees.RemoveRange(attendees);
        _db.CalendarEvents.Remove(calendarEvent);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CalendarAttendeeDto>?> ListAttendeesAsync(Guid tenantId, Guid userId, Guid eventId, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        var calendarEvent = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return null;
        }

        // Attendee identity is private event content, so a subscribed colleague calendar
        // exposes nothing here; only the organizer and tenant owner/admin may read it.
        var allowed = calendarEvent.UserId == userId ||
            calendarEvent.Visibility == "public" ||
            await IsOwnerOrAdminAsync(tenantId, userId, cancellationToken);
        if (!allowed)
        {
            return null;
        }

        var attendees = await _db.CalendarEventAttendees
            .Where(a => a.TenantId == tenantId && a.EventId == eventId)
            .OrderBy(a => a.Email)
            .ToListAsync(cancellationToken);
        return attendees.Select(ToAttendeeDto).ToList();
    }

    public async Task<IReadOnlyList<DirectoryParticipantDto>> ListDirectoryParticipantsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        var ownEmail = await _db.Users
            .Where(u => u.TenantId == tenantId && u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);
        var domain = GetDomainPart(ownEmail ?? string.Empty);

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Id != userId)
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Email)
            .Select(u => new DirectoryParticipantDto(
                u.Id,
                string.IsNullOrWhiteSpace(u.Name) ? u.Email : u.Name,
                u.Email,
                u.IsService ? "resource" : "user"))
            .ToListAsync(cancellationToken);

        // Shared mailboxes are not people and cannot be invited; only the users that can
        // actually answer are listed. Service accounts are listed as bookable resources.
        return domain.Length == 0
            ? users
            : users.Where(u => string.Equals(GetDomainPart(u.Email), domain, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<IReadOnlyList<SubscriptionDto>> ListSubscriptionsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");

        // EF Core translation limitation: computing DisplayName with string.IsNullOrWhiteSpace
        // inside the projection/order-by cannot be translated reliably for this provider.
        // Materialize the join first, then compute DisplayName in memory.
        var rows = await _db.CalendarSubscriptions
            .Where(s => s.TenantId == tenantId && s.UserId == userId)
            .Join(
                _db.Users.Where(u => u.TenantId == tenantId && u.IsActive && !u.IsService),
                s => s.TargetUserId,
                u => u.Id,
                (_, u) => new
                {
                    u.Id,
                    u.Name,
                    u.Email
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(u => new SubscriptionDto(
                u.Id,
                string.IsNullOrWhiteSpace(u.Name) ? u.Email : u.Name,
                u.Email))
            .OrderBy(s => s.DisplayName)
            .ToList();
    }

    public async Task<bool> AddSubscriptionAsync(Guid tenantId, Guid userId, SubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        if (request.UserId == userId)
        {
            throw new ArgumentException("A calendar cannot be subscribed to itself.");
        }

        var target = await _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenantId && u.Id == request.UserId && u.IsActive && !u.IsService,
            cancellationToken);
        if (target is null)
        {
            throw new ResourceNotFoundException();
        }

        var exists = await _db.CalendarSubscriptions.AnyAsync(
            s => s.TenantId == tenantId && s.UserId == userId && s.TargetUserId == request.UserId,
            cancellationToken);
        if (exists)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        _db.CalendarSubscriptions.Add(new CalendarSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            TargetUserId = request.UserId,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSubscriptionAsync(Guid tenantId, Guid userId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        var subscription = await _db.CalendarSubscriptions.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.UserId == userId && s.TargetUserId == targetUserId,
            cancellationToken);
        if (subscription is null)
        {
            return false;
        }

        _db.CalendarSubscriptions.Remove(subscription);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResendInvitationsAsync(Guid tenantId, Guid userId, Guid eventId, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        var calendarEvent = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.UserId == userId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return false;
        }

        var attendees = await _db.CalendarEventAttendees.Where(a => a.TenantId == tenantId && a.EventId == eventId).ToListAsync(cancellationToken);
        var tokenByAttendee = new Dictionary<CalendarEventAttendee, string>();
        var now = DateTimeOffset.UtcNow;
        foreach (var attendee in attendees)
        {
            var token = _sessions.GenerateToken(TimeSpan.FromDays(30));
            attendee.TokenHash = token.HashedToken;
            attendee.TokenExpiresAt = token.ExpiresAt;
            attendee.UpdatedAt = now;
            tokenByAttendee[attendee] = token.RawToken;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await SendInvitationsAsync(tenantId, userId, calendarEvent, tokenByAttendee, CalendarIcalMethod.Request, cancellationToken);
        return true;
    }

    public async Task<CalendarEventDto?> AcceptRescheduleProposalAsync(Guid tenantId, Guid userId, Guid eventId, Guid attendeeId, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        var calendarEvent = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.UserId == userId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return null;
        }

        var attendee = await _db.CalendarEventAttendees.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.EventId == eventId && a.Id == attendeeId, cancellationToken)
            ?? await _db.CalendarEventAttendees.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == attendeeId && a.ResponseStatus == "reschedule_proposed", cancellationToken);
        if (attendee?.ProposedStartTime is null || attendee.ProposedEndTime is null)
        {
            return null;
        }

        calendarEvent.StartTime = attendee.ProposedStartTime.Value.ToUniversalTime();
        calendarEvent.EndTime = attendee.ProposedEndTime.Value.ToUniversalTime();
        calendarEvent.UpdatedAt = DateTimeOffset.UtcNow;
        var attendees = await _db.CalendarEventAttendees.Where(a => a.TenantId == tenantId && a.EventId == eventId).ToListAsync(cancellationToken);
        foreach (var item in attendees)
        {
            item.ResponseStatus = "needs_action";
            item.ProposedStartTime = null;
            item.ProposedEndTime = null;
            item.ProposalNote = null;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            if (item.MirroredEventId is Guid mirrorId)
            {
                var mirror = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == mirrorId, cancellationToken);
                if (mirror is not null)
                {
                    mirror.StartTime = calendarEvent.StartTime;
                    mirror.EndTime = calendarEvent.EndTime;
                    mirror.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(calendarEvent, userId, canSeeAll: false, attendees);
    }

    public async Task<CalendarEventDto?> DeclineRescheduleProposalAsync(Guid tenantId, Guid userId, Guid eventId, Guid attendeeId, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        var calendarEvent = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.UserId == userId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return null;
        }

        var attendee = await _db.CalendarEventAttendees.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.EventId == eventId && a.Id == attendeeId && a.ResponseStatus == "reschedule_proposed", cancellationToken)
            ?? await _db.CalendarEventAttendees.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == attendeeId && a.ResponseStatus == "reschedule_proposed", cancellationToken);
        if (attendee is null)
        {
            return null;
        }

        attendee.ResponseStatus = "needs_action";
        attendee.ProposedStartTime = null;
        attendee.ProposedEndTime = null;
        attendee.ProposalNote = null;
        attendee.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var attendees = await _db.CalendarEventAttendees.Where(a => a.TenantId == tenantId && a.EventId == eventId).ToListAsync(cancellationToken);
        return ToDto(calendarEvent, userId, canSeeAll: false, attendees);
    }

    public async Task<CalendarInvitationViewDto?> GetInvitationByTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveInvitationAsync(rawToken, cancellationToken);
        return resolved is null ? null : ToInvitationView(resolved.Value.Event, resolved.Value.Attendee, canRespond: true);
    }

    public async Task<CalendarInvitationViewDto?> ResolveRescheduleProposalAsync(string rawToken, string action, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveInvitationAsync(rawToken, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        var (attendee, calendarEvent) = resolved.Value;
        if (attendee.ResponseStatus != "reschedule_proposed")
        {
            return null;
        }

        var normalized = action.Equals("accept", StringComparison.OrdinalIgnoreCase) ? "accept" :
            action.Equals("decline", StringComparison.OrdinalIgnoreCase) ? "decline" : null;
        if (normalized is null)
        {
            return null;
        }

        if (normalized == "accept")
        {
            if (attendee.ProposedStartTime is null || attendee.ProposedEndTime is null)
            {
                return null;
            }

            calendarEvent.StartTime = attendee.ProposedStartTime.Value.ToUniversalTime();
            calendarEvent.EndTime = attendee.ProposedEndTime.Value.ToUniversalTime();
            calendarEvent.UpdatedAt = DateTimeOffset.UtcNow;
            var attendees = await _db.CalendarEventAttendees.Where(a => a.TenantId == calendarEvent.TenantId && a.EventId == calendarEvent.Id).ToListAsync(cancellationToken);
            foreach (var item in attendees)
            {
                item.ResponseStatus = "needs_action";
                item.ProposedStartTime = null;
                item.ProposedEndTime = null;
                item.ProposalNote = null;
                item.UpdatedAt = DateTimeOffset.UtcNow;
                if (item.MirroredEventId is Guid mirrorId)
                {
                    var mirror = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == calendarEvent.TenantId && e.Id == mirrorId, cancellationToken);
                    if (mirror is not null)
                    {
                        mirror.StartTime = calendarEvent.StartTime;
                        mirror.EndTime = calendarEvent.EndTime;
                        mirror.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                }
            }
        }
        else
        {
            attendee.ResponseStatus = "needs_action";
            attendee.ProposedStartTime = null;
            attendee.ProposedEndTime = null;
            attendee.ProposalNote = null;
            attendee.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToInvitationView(calendarEvent, attendee, canRespond: false);
    }

    public async Task<CalendarInvitationViewDto?> RespondToInvitationAsync(string rawToken, CalendarRsvpRequest request, CancellationToken cancellationToken = default)
    {
        if (!ValidResponses.Contains(request.Response))
        {
            return null;
        }

        var resolved = await ResolveInvitationAsync(rawToken, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        var (attendee, calendarEvent) = resolved.Value;
        var response = NormalizeResponse(request.Response);
        if (response == "reschedule")
        {
            if (request.ProposedStartTime is null || request.ProposedEndTime is null || request.ProposedEndTime <= request.ProposedStartTime)
            {
                return null;
            }

            attendee.ResponseStatus = "reschedule_proposed";
            attendee.ProposedStartTime = request.ProposedStartTime.Value.ToUniversalTime();
            attendee.ProposedEndTime = request.ProposedEndTime.Value.ToUniversalTime();
            attendee.ProposalNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            attendee.RespondedAt = DateTimeOffset.UtcNow;
            attendee.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await NotifyOrganizerOfRescheduleAsync(calendarEvent.TenantId, calendarEvent, attendee, rawToken, cancellationToken);
            return ToInvitationView(calendarEvent, attendee, canRespond: true);
        }

        attendee.ResponseStatus = response;
        attendee.RespondedAt = DateTimeOffset.UtcNow;
        attendee.UpdatedAt = DateTimeOffset.UtcNow;

        if (response == "declined")
        {
            await RemoveMirrorAsync(calendarEvent.TenantId, attendee, cancellationToken);
        }
        else
        {
            await UpsertMirrorAsync(calendarEvent.TenantId, attendee, calendarEvent, response, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToInvitationView(calendarEvent, attendee, canRespond: true);
    }

    public async Task<CalendarEventDto?> RespondToEventAsync(Guid tenantId, Guid userId, Guid eventId, CalendarRsvpRequest request, CancellationToken cancellationToken = default)
    {
        var response = NormalizeResponse(request.Response);
        if (!ValidResponses.Contains(response)) return null;

        var myEmail = await _db.Users.Where(u => u.TenantId == tenantId && u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(myEmail)) return null;

        var attendee = await _db.CalendarEventAttendees.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.EventId == eventId && a.Email.ToLower() == myEmail.ToLower(), cancellationToken);
        if (attendee is null) return null;

        var calendarEvent = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null) return null;

        if (response == "reschedule")
        {
            if (request.ProposedStartTime is null || request.ProposedEndTime is null || request.ProposedEndTime <= request.ProposedStartTime) return null;

            attendee.ResponseStatus = "reschedule_proposed";
            attendee.ProposedStartTime = request.ProposedStartTime.Value.ToUniversalTime();
            attendee.ProposedEndTime = request.ProposedEndTime.Value.ToUniversalTime();
            attendee.ProposalNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            attendee.RespondedAt = DateTimeOffset.UtcNow;
            attendee.UpdatedAt = DateTimeOffset.UtcNow;

            var token = _sessions.GenerateToken(TimeSpan.FromDays(30));
            attendee.TokenHash = token.HashedToken;
            attendee.TokenExpiresAt = token.ExpiresAt;
            attendee.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await NotifyOrganizerOfRescheduleAsync(tenantId, calendarEvent, attendee, token.RawToken, cancellationToken);
        }
        else
        {
            attendee.ResponseStatus = response;
            attendee.RespondedAt = DateTimeOffset.UtcNow;
            attendee.UpdatedAt = DateTimeOffset.UtcNow;

            if (response == "declined") await RemoveMirrorAsync(tenantId, attendee, cancellationToken);
            else await UpsertMirrorAsync(tenantId, attendee, calendarEvent, response, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
        }

        var attendees = await _db.CalendarEventAttendees.Where(a => a.TenantId == tenantId && a.EventId == eventId).ToListAsync(cancellationToken);
        return ToDto(calendarEvent, userId, canSeeAll: false, attendees);
    }

    private async Task<Dictionary<CalendarEventAttendee, string>> AddAttendeesAsync(Guid tenantId, Guid userId, CalendarEvent calendarEvent, IReadOnlyList<CalendarInviteeRequest>? invitees, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalizedInvitees = await NormalizeInviteesAsync(tenantId, userId, invitees, cancellationToken);
        var tokenByAttendee = new Dictionary<CalendarEventAttendee, string>();
        foreach (var invitee in normalizedInvitees)
        {
            var attendee = new CalendarEventAttendee
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EventId = calendarEvent.Id,
                Email = invitee.Email,
                DisplayName = invitee.DisplayName,
                Role = invitee.Role,
                IsExternal = invitee.IsExternal,
                CreatedAt = now,
                UpdatedAt = now
            };

            if (invitee.IsResource)
            {
                // A room accepts immediately and keeps a private busy mirror, with no RSVP
                // token and no invitation email.
                attendee.ResponseStatus = "accepted";
                attendee.RespondedAt = now;
                _db.CalendarEventAttendees.Add(attendee);
                await UpsertResourceMirrorAsync(tenantId, attendee, calendarEvent, invitee.UserId!.Value, cancellationToken);
                continue;
            }

            var token = _sessions.GenerateToken(TimeSpan.FromDays(30));
            attendee.ResponseStatus = "needs_action";
            attendee.TokenHash = token.HashedToken;
            attendee.TokenExpiresAt = token.ExpiresAt;
            _db.CalendarEventAttendees.Add(attendee);
            tokenByAttendee[attendee] = token.RawToken;
        }

        return tokenByAttendee;
    }

    private async Task<Dictionary<CalendarEventAttendee, string>> SyncAttendeesAsync(Guid tenantId, Guid userId, CalendarEvent calendarEvent, IReadOnlyList<CalendarInviteeRequest>? invitees, bool resetResponses, CancellationToken cancellationToken)
    {
        var normalizedInvitees = await NormalizeInviteesAsync(tenantId, userId, invitees, cancellationToken);
        var existing = await _db.CalendarEventAttendees.Where(a => a.TenantId == tenantId && a.EventId == calendarEvent.Id).ToListAsync(cancellationToken);
        var incoming = normalizedInvitees.ToDictionary(i => i.Email, StringComparer.OrdinalIgnoreCase);
        var tokenByAttendee = new Dictionary<CalendarEventAttendee, string>();

        foreach (var attendee in existing.Where(a => !incoming.ContainsKey(a.Email)).ToList())
        {
            await RemoveMirrorAsync(tenantId, attendee, cancellationToken);
            _db.CalendarEventAttendees.Remove(attendee);
        }

        foreach (var invitee in normalizedInvitees)
        {
            var attendee = existing.FirstOrDefault(a => a.Email.Equals(invitee.Email, StringComparison.OrdinalIgnoreCase));
            if (attendee is null)
            {
                attendee = new CalendarEventAttendee
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EventId = calendarEvent.Id,
                    Email = invitee.Email,
                    DisplayName = invitee.DisplayName,
                    Role = invitee.Role,
                    IsExternal = invitee.IsExternal,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                if (invitee.IsResource)
                {
                    attendee.ResponseStatus = "accepted";
                    attendee.RespondedAt = DateTimeOffset.UtcNow;
                    _db.CalendarEventAttendees.Add(attendee);
                    await UpsertResourceMirrorAsync(tenantId, attendee, calendarEvent, invitee.UserId!.Value, cancellationToken);
                    continue;
                }

                var token = _sessions.GenerateToken(TimeSpan.FromDays(30));
                attendee.ResponseStatus = "needs_action";
                attendee.TokenHash = token.HashedToken;
                attendee.TokenExpiresAt = token.ExpiresAt;
                _db.CalendarEventAttendees.Add(attendee);
                tokenByAttendee[attendee] = token.RawToken;
                continue;
            }

            attendee.DisplayName = invitee.DisplayName;
            attendee.Role = invitee.Role;
            attendee.IsExternal = invitee.IsExternal;
            attendee.UpdatedAt = DateTimeOffset.UtcNow;
            if (invitee.IsResource)
            {
                // Re-booking a room for a moved meeting must not require the room to accept again.
                attendee.ResponseStatus = "accepted";
                attendee.RespondedAt ??= DateTimeOffset.UtcNow;
                await UpsertResourceMirrorAsync(tenantId, attendee, calendarEvent, invitee.UserId!.Value, cancellationToken);
                continue;
            }

            // Always regenerate the RSVP token on sync to ensure links are present in updated invitations
            var rsvpToken = _sessions.GenerateToken(TimeSpan.FromDays(30));
            attendee.TokenHash = rsvpToken.HashedToken;
            attendee.TokenExpiresAt = rsvpToken.ExpiresAt;
            tokenByAttendee[attendee] = rsvpToken.RawToken;

            if (resetResponses)
            {
                attendee.ResponseStatus = "needs_action";
                attendee.RespondedAt = null;
            }
        }

        return tokenByAttendee;
    }


    private async Task<IReadOnlyList<NormalizedInvitee>> NormalizeInviteesAsync(Guid tenantId, Guid userId, IReadOnlyList<CalendarInviteeRequest>? invitees, CancellationToken cancellationToken)
    {
        if (invitees is null || invitees.Count == 0)
        {
            return Array.Empty<NormalizedInvitee>();
        }

        if (invitees.Count > 100)
        {
            throw new ArgumentException("A meeting can have up to 100 invitees.");
        }

        var organizerEmail = await _db.Users.Where(u => u.TenantId == tenantId && u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
        var activeUsers = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive && !u.IsService)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);
        var usersByEmail = activeUsers.ToDictionary(u => u.Email, u => u.Id, StringComparer.OrdinalIgnoreCase);

        // A room is a service account and books itself. Group and alias addresses are
        // distribution lists, shared mailboxes are not identities at all: none of them can
        // answer an invitation, so accepting one silently would drop the meeting.
        var resourcesByEmail = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive && u.IsService)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);
        var resourceIds = resourcesByEmail.ToDictionary(u => u.Email, u => u.Id, StringComparer.OrdinalIgnoreCase);

        var blockedAddresses = (await _db.Mailboxes
                .Where(m => m.TenantId == tenantId && m.IsActive && m.Kind == "shared")
                .Select(m => m.Address)
                .ToListAsync(cancellationToken))
            .Concat(await _db.Groups
                .Where(g => g.TenantId == tenantId)
                .Select(g => g.Address)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<NormalizedInvitee>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var invitee in invitees)
        {
            if (!TryNormalizeEmail(invitee.Email, out var email))
            {
                throw new ArgumentException("Meeting invitee email is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(organizerEmail) && email.Equals(organizerEmail, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (blockedAddresses.Contains(email))
            {
                throw new ArgumentException($"{email} is a shared mailbox, group, or alias and cannot be invited to a meeting.");
            }

            if (!seen.Add(email))
            {
                continue;
            }

            var role = string.IsNullOrWhiteSpace(invitee.Role) ? "required" : invitee.Role.Trim().ToLowerInvariant();
            if (!ValidRoles.Contains(role))
            {
                throw new ArgumentException("Unsupported meeting invitee role.");
            }

            var displayName = string.IsNullOrWhiteSpace(invitee.DisplayName) ? null : invitee.DisplayName.Trim();
            if (resourceIds.TryGetValue(email, out var resourceId))
            {
                result.Add(new NormalizedInvitee(email, displayName, role, false, resourceId, true));
                continue;
            }

            usersByEmail.TryGetValue(email, out var targetUserId);
            var isExternal = targetUserId == Guid.Empty;
            result.Add(new NormalizedInvitee(email, displayName, role, isExternal, isExternal ? null : targetUserId, false));
        }

        return result;
    }

    private async Task SendInvitationsAsync(Guid tenantId, Guid userId, CalendarEvent calendarEvent, IReadOnlyDictionary<CalendarEventAttendee, string> tokenByAttendee, CalendarIcalMethod method, CancellationToken cancellationToken)
    {
        var recipients = await _db.CalendarEventAttendees
            .Where(a => a.TenantId == tenantId && a.EventId == calendarEvent.Id)
            .OrderBy(a => a.Email)
            .ToListAsync(cancellationToken);

        // A service account is a room: it holds an accepted response and no token, and it is
        // booked by the mirror, never by an email.
        var humanRecipients = await ResolveHumanRecipientsAsync(tenantId, recipients, cancellationToken);
        if (humanRecipients.Count == 0)
        {
            return;
        }

        if (method == CalendarIcalMethod.Request && string.IsNullOrWhiteSpace(_invitationOptions.PublicBaseUrl))
        {
            throw new InvalidOperationException("Cannot send calendar invitations because the server requires a public base URL for RSVP links. Please configure MIAUTRIX_PUBLIC_BASE_URL.");
        }

        var organizerMailbox = await ResolveOrganizerMailboxAsync(tenantId, userId, calendarEvent, cancellationToken);
        if (organizerMailbox is null)
        {
            throw new InvalidOperationException("Cannot send calendar invitations because an organizer mailbox could not be resolved.");
        }

        foreach (var attendee in humanRecipients)
        {
            tokenByAttendee.TryGetValue(attendee, out var rawToken);
            var invitation = CalendarInvitationBuilder.BuildInvitation(
                calendarEvent,
                attendee,
                rawToken,
                _invitationOptions.PublicBaseUrl,
                recipients,
                organizerMailbox.Address,
                method);

            var attachment = new Mime.MimeAttachment(
                invitation.IcalFileName,
                "text/calendar; charset=utf-8",
                invitation.IcalBytes,
                Inline: false,
                Method: method == CalendarIcalMethod.Cancel ? "CANCEL" : "REQUEST",
                Component: "VEVENT");

            await _messages.SendMessageAsync(
                tenantId,
                userId,
                organizerMailbox.Id,
                new SendMessageRequest(
                    organizerMailbox.Address,
                    new[] { attendee.Email },
                    null,
                    null,
                    invitation.Subject,
                    invitation.BodyText,
                    invitation.BodyHtml,
                    new[] { attachment }),
                cancellationToken);
        }
    }

    /// <summary>
    /// Drops rooms and any attendee address that no longer maps to an active user, so a room
    /// booking never produces a human invitation.
    /// </summary>
    private async Task<IReadOnlyList<CalendarEventAttendee>> ResolveHumanRecipientsAsync(Guid tenantId, IReadOnlyList<CalendarEventAttendee> attendees, CancellationToken cancellationToken)
    {
        if (attendees.Count == 0)
        {
            return attendees;
        }

        var serviceEmails = (await _db.Users
                .Where(u => u.TenantId == tenantId && u.IsService)
                .Select(u => u.Email)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return attendees.Where(a => !serviceEmails.Contains(a.Email)).ToList();
    }

    private async Task<Mailbox?> ResolveOrganizerMailboxAsync(Guid tenantId, Guid userId, CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(calendarEvent.Organizer))
        {
            var byOrganizer = await _db.Mailboxes.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.IsActive && m.Address.ToLower() == calendarEvent.Organizer.ToLower(), cancellationToken);
            if (byOrganizer is not null)
            {
                return byOrganizer;
            }
        }

        var userEmail = await _db.Users.Where(u => u.TenantId == tenantId && u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(userEmail)
            ? null
            : await _db.Mailboxes.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.IsActive && m.Address.ToLower() == userEmail.ToLower(), cancellationToken);
    }

    private async Task<(CalendarEventAttendee Attendee, CalendarEvent Event)?> ResolveInvitationAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (!IsTokenShapeValid(rawToken))
        {
            return null;
        }

        var hash = _sessions.HashToken(rawToken);
        var attendee = await _db.CalendarEventAttendees.FirstOrDefaultAsync(a => a.TokenHash == hash, cancellationToken);
        if (attendee is null || attendee.TokenExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        var calendarEvent = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == attendee.TenantId && e.Id == attendee.EventId, cancellationToken);
        if (calendarEvent is null || calendarEvent.Status == "cancelled" || calendarEvent.EndTime <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return (attendee, calendarEvent);
    }

    private async Task UpsertMirrorAsync(Guid tenantId, CalendarEventAttendee attendee, CalendarEvent source, string response, CancellationToken cancellationToken)
    {
        if (attendee.IsExternal)
        {
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.IsActive && u.Email.ToLower() == attendee.Email.ToLower(), cancellationToken);
        if (user is null)
        {
            return;
        }

        var mirror = attendee.MirroredEventId is Guid mirrorId
            ? await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == mirrorId, cancellationToken)
            : null;
        if (mirror is null)
        {
            mirror = new CalendarEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.CalendarEvents.Add(mirror);
            attendee.MirroredEventId = mirror.Id;
        }

        mirror.Title = source.Title;
        mirror.StartTime = source.StartTime;
        mirror.EndTime = source.EndTime;
        mirror.Location = source.Location;
        mirror.Organizer = source.Organizer;
        mirror.Status = response == "tentative" ? "tentative" : "confirmed";
        mirror.Visibility = "private";
        mirror.ShowAs = response == "tentative" ? "tentative" : "busy";
        mirror.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task RemoveMirrorAsync(Guid tenantId, CalendarEventAttendee attendee, CancellationToken cancellationToken)
    {
        if (attendee.MirroredEventId is not Guid mirrorId)
        {
            return;
        }

        var mirror = await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == mirrorId, cancellationToken);
        if (mirror is not null)
        {
            _db.CalendarEvents.Remove(mirror);
        }

        attendee.MirroredEventId = null;
    }

    // A booked room always answers "accepted", so its calibration is a mirror with the
    // room as the owning user. That is what makes it appear on the resource's calendar.
    private async Task UpsertResourceMirrorAsync(Guid tenantId, CalendarEventAttendee attendee, CalendarEvent source, Guid resourceUserId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var mirror = attendee.MirroredEventId is Guid mirrorId
            ? await _db.CalendarEvents.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == mirrorId, cancellationToken)
            : null;
        if (mirror is null)
        {
            mirror = new CalendarEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = resourceUserId,
                CreatedAt = now
            };
            _db.CalendarEvents.Add(mirror);
            attendee.MirroredEventId = mirror.Id;
        }

        mirror.Title = source.Title;
        mirror.Description = source.Description;
        mirror.StartTime = source.StartTime;
        mirror.EndTime = source.EndTime;
        mirror.Location = source.Location;
        mirror.Organizer = source.Organizer;
        mirror.Status = "confirmed";
        mirror.Visibility = "private";
        mirror.ShowAs = "busy";
        mirror.UpdatedAt = now;
    }

    private async Task NotifyOrganizerOfRescheduleAsync(Guid tenantId, CalendarEvent calendarEvent, CalendarEventAttendee attendee, string? rawToken, CancellationToken cancellationToken)
    {
        var organizerUser = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == calendarEvent.UserId, cancellationToken);
        if (organizerUser is null)
        {
            return;
        }

        var organizerMailbox = await ResolveOrganizerMailboxAsync(tenantId, calendarEvent.UserId, calendarEvent, cancellationToken);
        if (organizerMailbox is null)
        {
            return;
        }

        if (attendee.ProposedStartTime is null || attendee.ProposedEndTime is null)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(calendarEvent.Title) ? "(No title)" : calendarEvent.Title;
        var proposedStart = attendee.ProposedStartTime.Value.ToUniversalTime();
        var proposedEnd = attendee.ProposedEndTime.Value.ToUniversalTime();
        var proposedTime = $"{proposedStart:dddd, d MMMM yyyy HH:mm} - {proposedEnd:HH:mm} UTC";
        var note = string.IsNullOrWhiteSpace(attendee.ProposalNote) ? null : attendee.ProposalNote;
        var baseUrl = _invitationOptions.PublicBaseUrl.TrimEnd('/');
        var link = !string.IsNullOrWhiteSpace(rawToken) && !string.IsNullOrWhiteSpace(baseUrl)
            ? $"{baseUrl}/api/v1/public/calendar/invitations/{rawToken}/proposal"
            : null;
        var actionText = link is null
            ? "Open the calendar event in Webmail to accept or decline this proposed time."
            : $"Accept proposal: {link}?action=accept\nDecline proposal: {link}?action=decline";
        var actionHtml = link is null
            ? "<p style=\"margin:0 0 12px;color:#5a5a5a\">Open the calendar event in Webmail to accept or decline this proposed time.</p>"
            : $"<p style=\"margin:0 0 12px\"><a href=\"{WebUtility.HtmlEncode(link)}?action=accept\" style=\"color:#1f7a3f;font-weight:600\" target=\"_blank\">Accept proposal</a> &nbsp; <a href=\"{WebUtility.HtmlEncode(link)}?action=decline\" style=\"color:#a12b2b;font-weight:600\" target=\"_blank\">Decline proposal</a></p>";

        var bodyText = $"{attendee.Email} requested a different time for {title}.\n\nProposed time: {proposedTime}\nTimezone: Proposed time above is UTC. Calendar clients and the web RSVP page show this in your local PC timezone.{(note is null ? string.Empty : $"\n\nNote: {note}")}\n\n{actionText}";
        var bodyHtml =
            "<div style=\"font-family:system-ui,Segoe UI,Arial,sans-serif;font-size:14px;color:#1a1a1a;line-height:1.5\">" +
            "<p style=\"margin:0 0 12px\">A meeting attendee requested a different time.</p>" +
            $"<h2 style=\"margin:0 0 12px;font-size:18px\">{WebUtility.HtmlEncode(title)}</h2>" +
            "<table cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;margin:0 0 12px\">" +
            $"<tr><td style=\"vertical-align:top;padding-right:12px;color:#5a5a5a\">Attendee</td><td>{WebUtility.HtmlEncode(attendee.Email)}</td></tr>" +
            $"<tr><td style=\"vertical-align:top;padding-right:12px;color:#5a5a5a\">Proposed time</td><td>{WebUtility.HtmlEncode(proposedTime)}</td></tr>" +
            "<tr><td style=\"vertical-align:top;padding-right:12px;color:#5a5a5a\">Timezone</td><td>Proposed time above is UTC. Calendar clients and the web RSVP page show this in your local PC timezone.</td></tr>" +
            (note is null ? string.Empty : $"<tr><td style=\"vertical-align:top;padding-right:12px;color:#5a5a5a\">Note</td><td>{WebUtility.HtmlEncode(note).Replace("\n", "<br />", StringComparison.Ordinal)}</td></tr>") +
            "</table>" +
            actionHtml +
            $"<p style=\"margin:0;color:#5a5a5a;font-size:12px\">Replies go to {WebUtility.HtmlEncode(attendee.Email)}.</p>" +
            "</div>";

        await _messages.SendMessageAsync(
            tenantId,
            calendarEvent.UserId,
            organizerMailbox.Id,
            new SendMessageRequest(
                organizerMailbox.Address,
                new[] { organizerUser.Email },
                null,
                null,
                $"Reschedule requested: {title}",
                bodyText,
                bodyHtml),
            cancellationToken);
    }

    private static List<CalendarEvent> ExpandRecurringEvents(IEnumerable<CalendarEvent> source, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var expanded = new List<CalendarEvent>();
        foreach (var calendarEvent in source)
        {
            var frequency = calendarEvent.RecurrenceFrequency;
            if (string.IsNullOrWhiteSpace(frequency))
            {
                expanded.Add(calendarEvent);
                continue;
            }

            var interval = Math.Max(1, calendarEvent.RecurrenceInterval);
            var duration = calendarEvent.EndTime - calendarEvent.StartTime;
            var cursor = calendarEvent.StartTime;
            var until = calendarEvent.RecurrenceUntil is { } recurrenceUntil && recurrenceUntil < toUtc ? recurrenceUntil : toUtc;
            var occurrence = 0;
            while (cursor < toUtc && cursor <= until && occurrence < 1000)
            {
                var end = cursor + duration;
                if (end > fromUtc && cursor < toUtc)
                {
                    expanded.Add(new CalendarEvent
                    {
                        Id = calendarEvent.Id,
                        TenantId = calendarEvent.TenantId,
                        UserId = calendarEvent.UserId,
                        Title = calendarEvent.Title,
                        StartTime = cursor,
                        EndTime = end,
                        Location = calendarEvent.Location,
                        Description = calendarEvent.Description,
                        Organizer = calendarEvent.Organizer,
                        Status = calendarEvent.Status,
                        Visibility = calendarEvent.Visibility,
                        ShowAs = calendarEvent.ShowAs,
                        RecurrenceFrequency = calendarEvent.RecurrenceFrequency,
                        RecurrenceInterval = calendarEvent.RecurrenceInterval,
                        RecurrenceUntil = calendarEvent.RecurrenceUntil,
                        Sequence = calendarEvent.Sequence,
                        CreatedAt = calendarEvent.CreatedAt,
                        UpdatedAt = calendarEvent.UpdatedAt
                    });
                }

                cursor = frequency.ToLowerInvariant() switch
                {
                    "daily" => cursor.AddDays(interval),
                    "weekly" => cursor.AddDays(interval * 7),
                    "monthly" => cursor.AddMonths(interval),
                    _ => toUtc
                };
                occurrence++;
            }
        }

        return expanded.OrderBy(e => e.StartTime).ThenBy(e => e.Title).ToList();
    }

    private static void Validate(CalendarEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Calendar event title is required.");
        }

        if (request.EndTime <= request.StartTime)
        {
            throw new ArgumentException("Calendar event end must be after start.");
        }

        if (!ValidStatuses.Contains(request.Status))
        {
            throw new ArgumentException("Unsupported calendar event status.");
        }

        if (!ValidVisibility.Contains(request.Visibility))
        {
            throw new ArgumentException("Unsupported calendar event visibility.");
        }

        if (!ValidShowAs.Contains(request.ShowAs))
        {
            throw new ArgumentException("Unsupported calendar availability state.");
        }

        if (!string.IsNullOrWhiteSpace(request.RecurrenceFrequency) && !ValidRecurrenceFrequencies.Contains(request.RecurrenceFrequency))
        {
            throw new ArgumentException("Unsupported recurrence frequency.");
        }

        if (request.RecurrenceInterval < 1)
        {
            throw new ArgumentException("Recurrence interval must be at least 1.");
        }
    }

    private static void Apply(CalendarEvent calendarEvent, CalendarEventRequest request, DateTimeOffset now)
    {
        calendarEvent.Title = request.Title.Trim();
        calendarEvent.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        calendarEvent.StartTime = request.StartTime.ToUniversalTime();
        calendarEvent.EndTime = request.EndTime.ToUniversalTime();
        calendarEvent.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        calendarEvent.Organizer = string.IsNullOrWhiteSpace(request.Organizer) ? null : request.Organizer.Trim();
        calendarEvent.Status = request.Status.Trim().ToLowerInvariant();
        calendarEvent.Visibility = request.Visibility.Trim().ToLowerInvariant();
        calendarEvent.ShowAs = request.ShowAs.Trim().ToLowerInvariant();
        var recurrenceFrequency = string.IsNullOrWhiteSpace(request.RecurrenceFrequency) ? null : request.RecurrenceFrequency.Trim().ToLowerInvariant();
        calendarEvent.RecurrenceFrequency = recurrenceFrequency == "none" ? null : recurrenceFrequency;
        calendarEvent.RecurrenceInterval = request.RecurrenceInterval > 0 ? request.RecurrenceInterval : 1;
        calendarEvent.RecurrenceUntil = calendarEvent.RecurrenceFrequency is null ? null : request.RecurrenceUntil?.ToUniversalTime();
        calendarEvent.UpdatedAt = now;
    }

    private async Task<bool> IsOwnerOrAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        await _db.Memberships
            .Where(m => m.TenantId == tenantId && m.UserId == userId && m.RoleId.HasValue)
            .Join(
                _db.Roles.Where(r => r.TenantId == tenantId && (r.Code == "owner" || r.Code == "admin")),
                m => m.RoleId!.Value,
                r => r.Id,
                (_, _) => true)
            .AnyAsync(cancellationToken);

    private static string GetDomainPart(string address)
    {
        var at = address.LastIndexOf('@');
        return at >= 0 && at + 1 < address.Length ? address[(at + 1)..].Trim().ToLowerInvariant() : string.Empty;
    }

    private static CalendarAvailabilityDto ToAvailabilityDto(User user, Guid currentUserId, bool canSeeAll, IEnumerable<CalendarEvent> events) => new(
        user.Id,
        string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name,
        user.Email,
        events
            .Select(e => new CalendarBusyBlockDto(
                e.StartTime,
                e.EndTime,
                e.ShowAs,
                e.UserId == currentUserId || canSeeAll || e.Visibility == "public" ? e.Title : null))
            .ToList());

    private static CalendarEventDto ToDto(CalendarEvent calendarEvent, Guid currentUserId, bool canSeeAll, IEnumerable<CalendarEventAttendee>? attendees = null)
    {
        var isOwn = calendarEvent.UserId == currentUserId;
        var canSeeDetails = isOwn || canSeeAll || calendarEvent.Visibility == "public";
        return new CalendarEventDto(
            calendarEvent.Id,
            calendarEvent.UserId,
            canSeeDetails ? calendarEvent.Title : "Busy",
            calendarEvent.StartTime,
            calendarEvent.EndTime,
            canSeeDetails ? calendarEvent.Location : null,
            canSeeDetails ? calendarEvent.Description : null,
            canSeeDetails ? calendarEvent.Organizer : null,
            calendarEvent.Status,
            calendarEvent.Visibility,
            canSeeDetails ? calendarEvent.ShowAs : "busy",
            isOwn,
            isOwn,
            calendarEvent.RecurrenceFrequency,
            calendarEvent.RecurrenceInterval,
            calendarEvent.RecurrenceUntil,
            canSeeDetails ? attendees?.Select(ToAttendeeDto).ToList() : Array.Empty<CalendarAttendeeDto>());
    }

    private static CalendarAttendeeDto ToAttendeeDto(CalendarEventAttendee attendee) => new(
        attendee.Id,
        attendee.Email,
        attendee.DisplayName,
        attendee.Role,
        attendee.IsExternal,
        attendee.ResponseStatus,
        attendee.RespondedAt,
        attendee.ProposedStartTime,
        attendee.ProposedEndTime,
        attendee.ProposalNote);

    private static CalendarInvitationViewDto ToInvitationView(CalendarEvent calendarEvent, CalendarEventAttendee attendee, bool canRespond) => new(
        calendarEvent.Title,
        calendarEvent.StartTime,
        calendarEvent.EndTime,
        calendarEvent.Location,
        calendarEvent.Organizer,
        attendee.Email,
        attendee.ResponseStatus,
        canRespond);

    private static bool TryNormalizeEmail(string raw, out string email)
    {
        email = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(raw.Trim());
            email = address.Address.Trim().ToLowerInvariant();
            return email.Contains('@', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeResponse(string response) => response.ToLowerInvariant() switch
    {
        "accept" => "accepted",
        "decline" => "declined",
        var value => value
    };

    private static bool IsTokenShapeValid(string token) =>
        token.Length == 64 && token.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private sealed record NormalizedInvitee(string Email, string? DisplayName, string Role, bool IsExternal, Guid? UserId, bool IsResource);
}
