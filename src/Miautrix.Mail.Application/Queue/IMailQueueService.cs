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

    Task DeleteAsync(
        Guid tenantId,
        Guid userId,
        Guid queueItemId,
        CancellationToken cancellationToken = default);
}
