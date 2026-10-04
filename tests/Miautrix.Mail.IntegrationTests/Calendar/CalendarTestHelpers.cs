using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Calendar;

/// <summary>One seeded person. <c>IsService</c> marks a room/resource account.</summary>
internal sealed record CalendarUser(Guid Id, string Email, string Name, bool IsService);

/// <summary>
/// Seeding, HTTP, MIME, and cleanup helpers shared by the calendar integration tests.
/// Everything here talks to the real PostgreSQL database — no in-memory provider.
/// </summary>
internal static class CalendarTestHelpers
{
    /// <summary>The base URL the RSVP links are built from when a test overrides the option.</summary>
    internal const string PublicBaseUrl = "https://webmail.calendar.test";

    internal static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION environment variable is required.");
        }

        return conn;
    }

    internal static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(GetConnectionString()).Options);

    internal static CalendarUser NewUser(string? email = null, string? name = null, bool isService = false)
    {
        var id = Guid.NewGuid();
        return new CalendarUser(
            id,
            email ?? $"user-{id:N}@test.local",
            name ?? $"User {id:N}",
            isService);
    }

    /// <summary>
    /// Seeds a tenant, one role, the permissions that role holds, and one membership per user.
    /// <c>mailbox.read</c> is always granted because every calendar endpoint asserts it.
    /// </summary>
    internal static async Task SeedTenantAsync(
        Guid tenantId,
        string roleCode,
        IReadOnlyList<CalendarUser> users,
        params string[] extraPermissions)
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Slug = $"tenant-{Guid.NewGuid():N}",
            CreatedAt = now,
            UpdatedAt = now
        });

        var roleId = Guid.NewGuid();
        context.Roles.Add(new Role
        {
            Id = roleId,
            TenantId = tenantId,
            Code = roleCode,
            Name = roleCode,
            CreatedAt = now,
            UpdatedAt = now
        });

        foreach (var code in new[] { "mailbox.read" }.Concat(extraPermissions).Distinct(StringComparer.Ordinal))
        {
            var permissionId = Guid.NewGuid();
            context.Permissions.Add(new Permission
            {
                Id = permissionId,
                TenantId = tenantId,
                Code = code,
                Name = code,
                CreatedAt = now,
                UpdatedAt = now
            });

            context.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleId = roleId,
                PermissionId = permissionId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        foreach (var user in users)
        {
            context.Users.Add(new User
            {
                Id = user.Id,
                TenantId = tenantId,
                Email = user.Email,
                Name = user.Name,
                IsActive = true,
                IsService = user.IsService,
                CreatedAt = now,
                UpdatedAt = now
            });

            context.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                RoleId = roleId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a mailbox. For a calendar invitation to leave the server, the organizer needs an
    /// active <c>Kind == "user"</c> mailbox whose address matches <c>Users.Email</c> — that is what
    /// <c>MessageService.SendMessageAsync</c> authorizes the send against.
    /// </summary>
    internal static async Task<Guid> SeedMailboxAsync(Guid tenantId, string address, string kind = "user")
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var mailboxId = Guid.NewGuid();

        context.Mailboxes.Add(new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            DomainId = Guid.NewGuid(),
            Address = address,
            Name = address,
            Kind = kind,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return mailboxId;
    }

    internal static async Task SeedGroupAsync(Guid tenantId, string address)
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;

        context.Groups.Add(new Group
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Address = address,
            Name = address,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
    }

    internal static async Task<Guid> SeedEventAsync(
        Guid tenantId,
        Guid userId,
        string title,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string visibility = "private",
        string showAs = "busy",
        string status = "confirmed",
        string? location = null,
        string? description = null,
        string? organizer = null)
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid();

        context.CalendarEvents.Add(new CalendarEvent
        {
            Id = eventId,
            TenantId = tenantId,
            UserId = userId,
            Title = title,
            StartTime = startTime.ToUniversalTime(),
            EndTime = endTime.ToUniversalTime(),
            Visibility = visibility,
            ShowAs = showAs,
            Status = status,
            Location = location,
            Description = description,
            Organizer = organizer,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return eventId;
    }

    internal static async Task SeedSubscriptionAsync(Guid tenantId, Guid userId, Guid targetUserId)
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;

        context.CalendarSubscriptions.Add(new CalendarSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            TargetUserId = targetUserId,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Replaces the <see cref="CalendarInvitationOptions"/> singleton so invitation emails carry a
    /// usable RSVP base URL. The option is read once at host build, so it cannot be set by an
    /// environment variable after the factory was created.
    /// </summary>
    internal static WebApplicationFactory<Miautrix.Mail.Web.Program> WithPublicBaseUrl(
        this WebApplicationFactory<Miautrix.Mail.Web.Program> factory,
        string baseUrl = PublicBaseUrl) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(new CalendarInvitationOptions { PublicBaseUrl = baseUrl })));

    /// <summary>Builds a request with the tenant/user headers and an idempotency key for mutations.</summary>
    internal static HttpRequestMessage Request(
        HttpMethod method,
        string url,
        Guid tenantId,
        Guid userId,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add("X-User-Id", userId.ToString());

        if (method != HttpMethod.Get && method != HttpMethod.Head)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return request;
    }

    /// <summary>Reads the <c>data</c> envelope every endpoint wraps its payload in.</summary>
    internal static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        Assert.True(
            document.RootElement.TryGetProperty("data", out var data),
            $"Response did not carry a data envelope: {payload}");
        return data.Clone();
    }

    internal static Dictionary<string, object?> Invitee(string email, string? displayName = null, string role = "required")
    {
        var invitee = new Dictionary<string, object?> { ["email"] = email, ["role"] = role };
        if (displayName is not null)
        {
            invitee["display_name"] = displayName;
        }

        return invitee;
    }

    internal static Dictionary<string, object?> EventRequest(
        string title,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string visibility = "private",
        string showAs = "busy",
        string status = "confirmed",
        string? location = null,
        string? description = null,
        string? organizer = null,
        IReadOnlyList<Dictionary<string, object?>>? invitees = null,
        bool? sendInvitations = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["start_time"] = startTime.ToUniversalTime(),
            ["end_time"] = endTime.ToUniversalTime(),
            ["visibility"] = visibility,
            ["show_as"] = showAs,
            ["status"] = status
        };

        if (location is not null)
        {
            body["location"] = location;
        }

        if (description is not null)
        {
            body["description"] = description;
        }

        if (organizer is not null)
        {
            body["organizer"] = organizer;
        }

        if (invitees is not null)
        {
            body["invitees"] = invitees;
        }

        if (sendInvitations is not null)
        {
            body["send_invitations"] = sendInvitations;
        }

        return body;
    }

    /// <summary>
    /// Pulls one MIME part out of a raw message by a marker in its headers and decodes the body
    /// according to its <c>Content-Transfer-Encoding</c>. Base64 and quoted-printable are both
    /// handled because text parts fall back to quoted-printable once a line exceeds 78 bytes.
    /// </summary>
    internal static string ExtractDecodedPart(string rawMessage, string headerMarker)
    {
        var normalized = rawMessage.Replace("\r\n", "\n", StringComparison.Ordinal);
        var markerIndex = normalized.IndexOf(headerMarker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"MIME header marker '{headerMarker}' was not found in the raw message.");

        var headersEnd = normalized.IndexOf("\n\n", markerIndex, StringComparison.Ordinal);
        Assert.True(headersEnd > markerIndex, $"MIME part after '{headerMarker}' has no header/body separator.");

        // Start at the whole header block (after the previous blank line or the part boundary),
        // so Content-Transfer-Encoding and Content-Disposition are both visible regardless of
        // which line the marker landed on.
        var previousSeparator = normalized.LastIndexOf("\n\n", markerIndex, StringComparison.Ordinal);
        var headerStart = previousSeparator < 0 ? 0 : previousSeparator + 2;
        var headers = normalized[headerStart..headersEnd];
        var bodyStart = headersEnd + 2;
        var bodyEnd = normalized.Length;

        var cursor = bodyStart;
        while (cursor < normalized.Length)
        {
            var lineEnd = normalized.IndexOf('\n', cursor);
            if (lineEnd < 0)
            {
                break;
            }

            if (normalized[cursor..lineEnd].StartsWith("--", StringComparison.Ordinal))
            {
                bodyEnd = cursor;
                break;
            }

            cursor = lineEnd + 1;
        }

        var body = normalized[bodyStart..bodyEnd].Trim();
        if (headers.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(new string(body.Where(c => !char.IsWhiteSpace(c)).ToArray())));
        }

        if (headers.Contains("quoted-printable", StringComparison.OrdinalIgnoreCase))
        {
            return DecodeQuotedPrintable(body);
        }

        return body;
    }

    private static string DecodeQuotedPrintable(string value)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '=' && i + 2 < value.Length)
            {
                // A soft line break: the "=" is followed by the newline pair.
                if (value[i + 1] == '\r' || value[i + 1] == '\n')
                {
                    i += value[i + 1] == '\r' && i + 2 < value.Length && value[i + 2] == '\n' ? 2 : 1;
                    continue;
                }

                if (int.TryParse(value.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var code))
                {
                    builder.Append((char)code);
                    i += 2;
                    continue;
                }
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Rejoins the RFC 5545 folded lines (a continuation line starts with a single space) so a
    /// long ATTENDEE or ORGANIZER line can be matched as one string.
    /// </summary>
    internal static string UnfoldCalendar(string ical) =>
        ical.Replace("\r\n ", string.Empty, StringComparison.Ordinal)
            .Replace("\n ", string.Empty, StringComparison.Ordinal);

    /// <summary>Removes every row the calendar tests create for a tenant. Safe to call twice.</summary>
    internal static async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var context = CreateContext();

        await context.SmtpQueue.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Contacts.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.MessageRecipients.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Attachments.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Messages.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Folders.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.MailboxDelegates.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Mailboxes.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.CalendarSubscriptions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.CalendarEventAttendees.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.CalendarEvents.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.GroupMembers.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Groups.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Memberships.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.RolePermissions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Permissions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Roles.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Users.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Tenants.Where(x => x.Id == tenantId).ExecuteDeleteAsync();
    }

    /// <summary>Asserts a status code and returns the response body for a failure message.</summary>
    internal static async Task<string> AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expected,
            $"Expected {(int)expected} {expected} but got {(int)response.StatusCode} {response.StatusCode}. Body: {payload}");
        return payload;
    }
}
