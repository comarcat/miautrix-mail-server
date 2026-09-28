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

    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _auth;
    private readonly IMessageService _messages;
    private readonly ISessionManager _sessions;
    private readonly CalendarInvitationOptions _invitationOptions;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(
        AppDbContext db,
        ITenantAuthorizationHelper auth,
        IMessageService messages,
        ISessionManager sessions,
        CalendarInvitationOptions invitationOptions,
        ILogger<CalendarService> logger)
    {
        _db = db;
        _auth = auth;
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

        var events = await _db.CalendarEvents
            .Where(e => e.TenantId == tenantId && e.StartTime < toUtc && e.EndTime > fromUtc)
            .OrderBy(e => e.StartTime)
            .ToListAsync(cancellationToken);
        var eventIds = events.Select(e => e.Id).ToList();
        var attendees = await _db.CalendarEventAttendees
            .Where(a => a.TenantId == tenantId && eventIds.Contains(a.EventId))
            .OrderBy(a => a.Email)
            .ToListAsync(cancellationToken);

        return events.Select(e => ToDto(e, userId, attendees.Where(a => a.EventId == e.Id))).ToList();
    }

    public async Task<IReadOnlyList<CalendarAvailabilityDto>> ListAvailabilityAsync(Guid tenantId, Guid userId, DateTimeOffset from, DateTimeOffset to, IReadOnlyList<Guid>? userIds, CancellationToken cancellationToken = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.read");
        if (to <= from)
        {
            throw new ArgumentException("Availability end must be after start.");
        }

        var usersQuery = _db.Users.Where(u => u.TenantId == tenantId && u.IsActive && !u.IsService);
        if (userIds is { Count: > 0 })
        {
            usersQuery = usersQuery.Where(u => userIds.Contains(u.Id));
        }

        var users = await usersQuery
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Email)
            .ToListAsync(cancellationToken);

        var visibleUserIds = users.Select(u => u.Id).ToHashSet();
        var events = await _db.CalendarEvents
            .Where(e => e.TenantId == tenantId && visibleUserIds.Contains(e.UserId) && e.StartTime < to && e.EndTime > from && e.ShowAs != "free")
            .OrderBy(e => e.StartTime)
            .ToListAsync(cancellationToken);

        return users.Select(u => new CalendarAvailabilityDto(
            u.Id,
            string.IsNullOrWhiteSpace(u.Name) ? u.Email : u.Name,
            u.Email,
            events.Where(e => e.UserId == u.Id)
                .Select(e => new CalendarBusyBlockDto(e.StartTime, e.EndTime, e.ShowAs, e.UserId == userId || e.Visibility == "public" ? e.Title : null))
                .ToList()))
            .ToList();
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
        await SendInvitationsAsync(tenantId, userId, calendarEvent, tokenByAttendee, cancellationToken);
        return ToDto(calendarEvent, userId, tokenByAttendee.Keys);
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
        var tokenByAttendee = await SyncAttendeesAsync(tenantId, userId, calendarEvent, request.Invitees, timeChanged, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await SendInvitationsAsync(tenantId, userId, calendarEvent, tokenByAttendee, cancellationToken);
        var attendees = await _db.CalendarEventAttendees.Where(a => a.TenantId == tenantId && a.EventId == eventId).ToListAsync(cancellationToken);
        return ToDto(calendarEvent, userId, attendees);
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
        if (calendarEvent is null || calendarEvent.UserId != userId)
        {
            return null;
        }

        var attendees = await _db.CalendarEventAttendees
            .Where(a => a.TenantId == tenantId && a.EventId == eventId)
            .OrderBy(a => a.Email)
            .ToListAsync(cancellationToken);
        return attendees.Select(ToAttendeeDto).ToList();
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
            if (string.IsNullOrWhiteSpace(attendee.TokenHash) || attendee.TokenExpiresAt <= now)
            {
                var token = _sessions.GenerateToken(TimeSpan.FromDays(30));
                attendee.TokenHash = token.HashedToken;
                attendee.TokenExpiresAt = token.ExpiresAt;
                attendee.UpdatedAt = now;
                tokenByAttendee[attendee] = token.RawToken;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await SendInvitationsAsync(tenantId, userId, calendarEvent, tokenByAttendee, cancellationToken);
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

        var attendee = await _db.CalendarEventAttendees.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.EventId == eventId && a.Id == attendeeId, cancellationToken);
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
        return ToDto(calendarEvent, userId, attendees);
    }

    public async Task<CalendarInvitationViewDto?> GetInvitationByTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveInvitationAsync(rawToken, cancellationToken);
        return resolved is null ? null : ToInvitationView(resolved.Value.Event, resolved.Value.Attendee, canRespond: true);
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
            await NotifyOrganizerOfRescheduleAsync(calendarEvent.TenantId, calendarEvent, attendee, cancellationToken);
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

    private async Task<Dictionary<CalendarEventAttendee, string>> AddAttendeesAsync(Guid tenantId, Guid userId, CalendarEvent calendarEvent, IReadOnlyList<CalendarInviteeRequest>? invitees, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalizedInvitees = await NormalizeInviteesAsync(tenantId, userId, invitees, cancellationToken);
        var tokenByAttendee = new Dictionary<CalendarEventAttendee, string>();
        foreach (var invitee in normalizedInvitees)
        {
            var token = _sessions.GenerateToken(TimeSpan.FromDays(30));
            var attendee = new CalendarEventAttendee
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EventId = calendarEvent.Id,
                Email = invitee.Email,
                DisplayName = invitee.DisplayName,
                Role = invitee.Role,
                IsExternal = invitee.IsExternal,
                ResponseStatus = "needs_action",
                TokenHash = token.HashedToken,
                TokenExpiresAt = token.ExpiresAt,
                CreatedAt = now,
                UpdatedAt = now
            };
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
                var token = _sessions.GenerateToken(TimeSpan.FromDays(30));
                attendee = new CalendarEventAttendee
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EventId = calendarEvent.Id,
                    Email = invitee.Email,
                    DisplayName = invitee.DisplayName,
                    Role = invitee.Role,
                    IsExternal = invitee.IsExternal,
                    ResponseStatus = "needs_action",
                    TokenHash = token.HashedToken,
                    TokenExpiresAt = token.ExpiresAt,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _db.CalendarEventAttendees.Add(attendee);
                tokenByAttendee[attendee] = token.RawToken;
                continue;
            }

            attendee.DisplayName = invitee.DisplayName;
            attendee.Role = invitee.Role;
            attendee.IsExternal = invitee.IsExternal;
            attendee.UpdatedAt = DateTimeOffset.UtcNow;
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
        var mailboxAddresses = await _db.Mailboxes.Where(m => m.TenantId == tenantId && m.IsActive).Select(m => m.Address.ToLower()).ToListAsync(cancellationToken);
        var tenantAddresses = mailboxAddresses.ToHashSet(StringComparer.OrdinalIgnoreCase);
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

            if (!seen.Add(email))
            {
                continue;
            }

            var role = string.IsNullOrWhiteSpace(invitee.Role) ? "required" : invitee.Role.Trim().ToLowerInvariant();
            if (!ValidRoles.Contains(role))
            {
                throw new ArgumentException("Unsupported meeting invitee role.");
            }

            result.Add(new NormalizedInvitee(email, string.IsNullOrWhiteSpace(invitee.DisplayName) ? null : invitee.DisplayName.Trim(), role, !tenantAddresses.Contains(email)));
        }

        return result;
    }

    private async Task SendInvitationsAsync(Guid tenantId, Guid userId, CalendarEvent calendarEvent, IReadOnlyDictionary<CalendarEventAttendee, string> tokenByAttendee, CancellationToken cancellationToken)
    {
        if (tokenByAttendee.Count == 0 || string.IsNullOrWhiteSpace(_invitationOptions.PublicBaseUrl))
        {
            return;
        }

        var organizerMailbox = await ResolveOrganizerMailboxAsync(tenantId, userId, calendarEvent, cancellationToken);
        if (organizerMailbox is null)
        {
            _logger.LogWarning("Calendar invitation email skipped because organizer mailbox could not be resolved for event {EventId}.", calendarEvent.Id);
            return;
        }

        foreach (var (attendee, rawToken) in tokenByAttendee)
        {
            var invitation = CalendarInvitationBuilder.BuildInvitation(calendarEvent, attendee, rawToken, _invitationOptions.PublicBaseUrl);
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
                    invitation.BodyHtml),
                cancellationToken);
        }
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

    private async Task NotifyOrganizerOfRescheduleAsync(Guid tenantId, CalendarEvent calendarEvent, CalendarEventAttendee attendee, CancellationToken cancellationToken)
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

        var note = string.IsNullOrWhiteSpace(attendee.ProposalNote) ? string.Empty : $"\n\nNote: {attendee.ProposalNote}";
        await _messages.SendMessageAsync(
            tenantId,
            calendarEvent.UserId,
            organizerMailbox.Id,
            new SendMessageRequest(
                organizerMailbox.Address,
                new[] { organizerUser.Email },
                null,
                null,
                $"Reschedule requested: {calendarEvent.Title}",
                $"{attendee.Email} requested a reschedule for {calendarEvent.Title}.\nProposed time: {attendee.ProposedStartTime:u} - {attendee.ProposedEndTime:u}{note}",
                null),
            cancellationToken);
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
    }

    private static void Apply(CalendarEvent calendarEvent, CalendarEventRequest request, DateTimeOffset now)
    {
        calendarEvent.Title = request.Title.Trim();
        calendarEvent.StartTime = request.StartTime.ToUniversalTime();
        calendarEvent.EndTime = request.EndTime.ToUniversalTime();
        calendarEvent.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        calendarEvent.Organizer = string.IsNullOrWhiteSpace(request.Organizer) ? null : request.Organizer.Trim();
        calendarEvent.Status = request.Status.Trim().ToLowerInvariant();
        calendarEvent.Visibility = request.Visibility.Trim().ToLowerInvariant();
        calendarEvent.ShowAs = request.ShowAs.Trim().ToLowerInvariant();
        calendarEvent.UpdatedAt = now;
    }

    private static CalendarEventDto ToDto(CalendarEvent calendarEvent, Guid currentUserId, IEnumerable<CalendarEventAttendee>? attendees = null)
    {
        var canSeeDetails = calendarEvent.UserId == currentUserId || calendarEvent.Visibility == "public";
        return new CalendarEventDto(
            calendarEvent.Id,
            calendarEvent.UserId,
            canSeeDetails ? calendarEvent.Title : "Busy",
            calendarEvent.StartTime,
            calendarEvent.EndTime,
            canSeeDetails ? calendarEvent.Location : null,
            calendarEvent.Organizer,
            calendarEvent.Status,
            calendarEvent.Visibility,
            calendarEvent.ShowAs,
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

    private sealed record NormalizedInvitee(string Email, string? DisplayName, string Role, bool IsExternal);
}
