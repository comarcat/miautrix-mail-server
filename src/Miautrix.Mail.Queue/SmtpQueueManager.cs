using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Queue;

public interface ISmtpQueueManager
{
    Task<SmtpQueueItem> EnqueueAsync(
        Guid tenantId,
        string sender,
        string recipient,
        string rawMessage,
        string? subject = null,
        CancellationToken cancellationToken = default);

    Task<SmtpDeliveryAttempt> RecordDeliveryAttemptAsync(
        Guid queueItemId,
        bool success,
        string? errorMessage = null,
        int? responseCode = null,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpQueueManager : ISmtpQueueManager
{
    private readonly AppDbContext _context;
    private readonly IRetryPolicy _retryPolicy;
    private readonly int _maxAttempts;

    public SmtpQueueManager(
        AppDbContext context,
        IRetryPolicy retryPolicy,
        int maxAttempts = 10)
    {
        _context = context;
        _retryPolicy = retryPolicy;
        _maxAttempts = maxAttempts;
    }

    public async Task<SmtpQueueItem> EnqueueAsync(
        Guid tenantId,
        string sender,
        string recipient,
        string rawMessage,
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        var item = new SmtpQueueItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Sender = sender,
            Recipient = recipient,
            Subject = subject,
            RawMessage = rawMessage,
            Status = "Pending",
            Attempts = 0,
            NextAttemptAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.SmtpQueue.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return item;
    }

    public async Task<SmtpDeliveryAttempt> RecordDeliveryAttemptAsync(
        Guid queueItemId,
        bool success,
        string? errorMessage = null,
        int? responseCode = null,
        CancellationToken cancellationToken = default)
    {
        var queueItem = await _context.SmtpQueue
            .FirstOrDefaultAsync(q => q.Id == queueItemId, cancellationToken);

        if (queueItem == null)
        {
            throw new KeyNotFoundException($"Queue item with ID '{queueItemId}' was not found.");
        }

        int attemptNumber = queueItem.Attempts + 1;
        var retryDelay = _retryPolicy.GetNextRetryDelay(attemptNumber);

        var attempt = new SmtpDeliveryAttempt
        {
            Id = Guid.NewGuid(),
            TenantId = queueItem.TenantId,
            QueueItemId = queueItemId,
            AttemptNumber = attemptNumber,
            AttemptedAt = DateTimeOffset.UtcNow,
            Success = success,
            ErrorMessage = errorMessage,
            ResponseCode = responseCode,
            NextRetryDelaySeconds = success ? null : retryDelay.TotalSeconds,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.SmtpDeliveryAttempts.Add(attempt);

        queueItem.Attempts = attemptNumber;
        queueItem.LastAttemptAt = DateTimeOffset.UtcNow;
        queueItem.LastError = errorMessage;
        queueItem.UpdatedAt = DateTimeOffset.UtcNow;

        if (success)
        {
            queueItem.Status = "Delivered";
        }
        else
        {
            if (attemptNumber >= _maxAttempts)
            {
                queueItem.Status = "DeadLetter";
            }
            else
            {
                queueItem.Status = "Failed";
                queueItem.NextAttemptAt = DateTimeOffset.UtcNow.Add(retryDelay);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return attempt;
    }
}
