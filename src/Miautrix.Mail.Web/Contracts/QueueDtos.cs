namespace Miautrix.Mail.Web.Contracts;

/// <summary>
/// Wire representation of a queued outbound message. Field names are snake_case
/// to match the admin frontend contract.
/// </summary>
public sealed record QueueItemDto(
    string Id,
    string MessageId,
    string Sender,
    string Recipient,
    long SizeBytes,
    string Status,
    int Attempts,
    DateTimeOffset? NextRetryAt,
    DateTimeOffset CreatedAt,
    string? ErrorMessage);
