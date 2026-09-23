using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Application.Queue;

/// <summary>
/// Read and command operations for the SMTP queue. Every method takes the current
/// tenant and user and passes through the single authorization helper before any
/// row is touched — the tenant query filter is defence in depth, not the gate.
/// </summary>
public sealed class MailQueueService : IMailQueueService
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _authorization;

    public MailQueueService(AppDbContext db, ITenantAuthorizationHelper authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<QueuePage> ListAsync(
        Guid tenantId,
        Guid userId,
        QueueFilter filter,
        CancellationToken cancellationToken = default)
    {
        _authorization.AssertPermission(tenantId, userId, "queue.view");

        var limit = Math.Clamp(filter.Limit <= 0 ? DefaultPageSize : filter.Limit, 1, MaxPageSize);

        IQueryable<SmtpQueueItem> query = _db.SmtpQueue.AsNoTracking()
            .Where(q => q.TenantId == tenantId);

        if (filter.Status is { } status)
        {
            var domainStatuses = ToDomainStatuses(status);
            query = query.Where(q => domainStatuses.Contains(q.Status));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(q =>
                EF.Functions.ILike(q.Sender, $"%{term}%") ||
                EF.Functions.ILike(q.Recipient, $"%{term}%") ||
                (q.Subject != null && EF.Functions.ILike(q.Subject, $"%{term}%")));
        }

        if (filter.StartAt.HasValue)
        {
            query = query.Where(q => q.CreatedAt >= filter.StartAt.Value);
        }

        if (filter.EndAt.HasValue)
        {
            query = query.Where(q => q.CreatedAt <= filter.EndAt.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Domain) && !string.Equals(filter.Domain, "all", StringComparison.OrdinalIgnoreCase))
        {
            var domain = filter.Domain.Trim().TrimStart('@');
            query = query.Where(q =>
                EF.Functions.ILike(q.Sender, $"%@{domain}") ||
                EF.Functions.ILike(q.Recipient, $"%@{domain}"));
        }

        var ordered = query
            .OrderBy(q => q.CreatedAt)
            .ThenBy(q => q.Id);

        IQueryable<SmtpQueueItem> pageQuery = ordered;

        if (TryDecodeCursor(filter.Cursor, out var cursorCreatedAt, out var cursorId))
        {
            pageQuery = ordered.Where(q =>
                q.CreatedAt > cursorCreatedAt ||
                (q.CreatedAt == cursorCreatedAt && q.Id > cursorId));
        }

        var rows = await pageQuery.Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;

        var items = hasMore ? rows.Take(limit).ToList() : rows;
        string? nextCursor = hasMore && items.Count > 0
            ? EncodeCursor(items[^1].CreatedAt, items[^1].Id)
            : null;

        return new QueuePage(items, nextCursor, hasMore);
    }

    public async Task<SmtpQueueItem> RetryAsync(
        Guid tenantId,
        Guid userId,
        Guid queueItemId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.SmtpQueue.FirstOrDefaultAsync(
            q => q.Id == queueItemId && q.TenantId == tenantId, cancellationToken)
            ?? throw new ResourceNotFoundException();

        _authorization.AuthorizeAccess(tenantId, userId, item, "queue.retry");

        if (item.Status == "Delivered")
        {
            throw new InvalidOperationException("A delivered message cannot be retried.");
        }

        item.Status = "Pending";
        item.Attempts = 0;
        item.NextAttemptAt = DateTimeOffset.UtcNow;
        item.LastAttemptAt = null;
        item.LastError = null;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task DeleteAsync(
        Guid tenantId,
        Guid userId,
        Guid queueItemId,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.SmtpQueue.FirstOrDefaultAsync(
            q => q.Id == queueItemId && q.TenantId == tenantId, cancellationToken)
            ?? throw new ResourceNotFoundException();

        _authorization.AuthorizeAccess(tenantId, userId, item, "queue.purge");

        var attempts = await _db.SmtpDeliveryAttempts
            .Where(a => a.QueueItemId == queueItemId && a.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        _db.SmtpDeliveryAttempts.RemoveRange(attempts);
        _db.SmtpQueue.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SmtpQueueItem> ReassignAsync(
        Guid tenantId,
        Guid userId,
        Guid queueItemId,
        string targetMailboxAddress,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.SmtpQueue.FirstOrDefaultAsync(
            q => q.Id == queueItemId && q.TenantId == tenantId, cancellationToken)
            ?? throw new ResourceNotFoundException();

        _authorization.AuthorizeAccess(tenantId, userId, item, "queue.retry");

        if (item.Status != "Failed" && item.Status != "Retrying" && item.Status != "DeadLetter")
        {
            throw new InvalidOperationException("Only failed and dead-letter queue items can be reassigned.");
        }

        if (string.IsNullOrWhiteSpace(targetMailboxAddress))
        {
            throw new ArgumentException("target mailbox address is required.", nameof(targetMailboxAddress));
        }

        var normalizedAddress = targetMailboxAddress.Trim().ToLowerInvariant();

        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(
                m => m.TenantId == tenantId && m.Address.ToLower() == normalizedAddress,
                cancellationToken);

        if (mailbox is null)
        {
            throw new ResourceNotFoundException("Target mailbox not found.");
        }

        // Reset retry state so the inbound persistence dispatcher picks it up again.
        item.Recipient = mailbox.Address;
        item.Status = "Pending";
        item.Attempts = 0;
        item.LastAttemptAt = null;
        item.NextAttemptAt = DateTimeOffset.UtcNow;
        item.LastError = null;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        var attempts = await _db.SmtpDeliveryAttempts
            .Where(a => a.TenantId == tenantId && a.QueueItemId == queueItemId)
            .ToListAsync(cancellationToken);

        _db.SmtpDeliveryAttempts.RemoveRange(attempts);

        await _db.SaveChangesAsync(cancellationToken);
        return item;
    }

    private static string[] ToDomainStatuses(QueueStatusFilter status) => status switch
    {
        QueueStatusFilter.Queued => ["Pending"],
        QueueStatusFilter.Retrying => ["Failed", "Retrying"],
        QueueStatusFilter.DeadLetter => ["DeadLetter"],
        QueueStatusFilter.Delivered => ["Delivered"],
        _ => throw new InvalidOperationException($"Unknown queue status filter '{status}'."),
    };

    private static string EncodeCursor(DateTimeOffset createdAt, Guid id)
    {
        var raw = $"{createdAt:O}|{id:N}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset createdAt, out Guid id)
    {
        createdAt = default;
        id = default;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts = raw.Split('|', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            return DateTimeOffset.TryParse(parts[0], out createdAt) && Guid.TryParseExact(parts[1], "N", out id);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
