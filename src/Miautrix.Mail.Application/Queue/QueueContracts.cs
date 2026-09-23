using Miautrix.Mail.Domain;

namespace Miautrix.Mail.Application.Queue;

/// <summary>
/// Wire-level statuses exposed by the queue API. The domain stores its own status
/// strings; this enum is the API's vocabulary and is mapped to domain values at the
/// transport boundary.
/// </summary>
public enum QueueStatusFilter
{
    Queued,
    Retrying,
    DeadLetter,
    Delivered,
}

/// <summary>Cursor-paginated query parameters for the queue listing.</summary>
public sealed record QueueFilter(
    QueueStatusFilter? Status,
    string? Search,
    int Limit,
    string? Cursor,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    string? Domain);

/// <summary>One page of queue items plus the cursor to fetch the next page.</summary>
public sealed record QueuePage(
    IReadOnlyList<SmtpQueueItem> Items,
    string? NextCursor,
    bool HasMore);
