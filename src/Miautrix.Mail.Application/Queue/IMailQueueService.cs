using Miautrix.Mail.Domain;

namespace Miautrix.Mail.Application.Queue;

public interface IMailQueueService
{
    Task<QueuePage> ListAsync(
        Guid tenantId,
        Guid userId,
        QueueFilter filter,
        CancellationToken cancellationToken = default);

    Task<SmtpQueueItem> RetryAsync(
        Guid tenantId,
        Guid userId,
        Guid queueItemId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reassign the queue item recipient mailbox and re-queue it for inbound persistence.
    /// Applicable to failed and dead-letter queue items.
    /// </summary>
    Task<SmtpQueueItem> ReassignAsync(
        Guid tenantId,
        Guid userId,
        Guid queueItemId,
        string targetMailboxAddress,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid tenantId,
        Guid userId,
        Guid queueItemId,
        CancellationToken cancellationToken = default);
}
