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
        QueueDirection direction,
        string? subject = null,
        CancellationToken cancellationToken = default);

    Task<SmtpDeliveryAttempt> RecordDeliveryAttemptAsync(
        Guid queueItemId,
        bool success,
        string? errorMessage = null,
        int? responseCode = null,
        CancellationToken cancellationToken = default,
        bool permanentFailure = false);
}

public sealed class SmtpQueueManager : ISmtpQueueManager
{
    private readonly AppDbContext _context;
    private readonly IRetryPolicy _retryPolicy;
    private readonly int _maxAttempts;
    private readonly QueueRetryOptions _retryOptions;
    private readonly TimeProvider _clock;

    public SmtpQueueManager(
        AppDbContext context,
        IRetryPolicy retryPolicy,
        int maxAttempts = int.MaxValue,
        QueueRetryOptions? retryOptions = null,
        TimeProvider? clock = null)
    {
        _context = context;
        _retryPolicy = retryPolicy;
        _maxAttempts = maxAttempts;
        _retryOptions = retryOptions ?? new QueueRetryOptions();
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<SmtpQueueItem> EnqueueAsync(
        Guid tenantId,
        string sender,
        string recipient,
        string rawMessage,
        QueueDirection direction,
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        var item = new SmtpQueueItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Direction = direction,
            Sender = sender,
            Recipient = recipient,
            Subject = subject,
            RawMessage = rawMessage,
            Status = "Pending",
            Attempts = 0,
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now
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
        CancellationToken cancellationToken = default,
        bool permanentFailure = false)
    {
        var queueItem = await _context.SmtpQueue
            .FirstOrDefaultAsync(q => q.Id == queueItemId, cancellationToken);

        if (queueItem == null)
        {
            throw new KeyNotFoundException($"Queue item with ID '{queueItemId}' was not found.");
        }

        int attemptNumber = queueItem.Attempts + 1;
        var now = _clock.GetUtcNow();
        var retryDelay = _retryPolicy.GetNextRetryDelay(attemptNumber);

        var attempt = new SmtpDeliveryAttempt
        {
            Id = Guid.NewGuid(),
            TenantId = queueItem.TenantId,
            QueueItemId = queueItemId,
            AttemptNumber = attemptNumber,
            AttemptedAt = now,
            Success = success,
            ErrorMessage = errorMessage,
            ResponseCode = responseCode,
            NextRetryDelaySeconds = success ? null : retryDelay.TotalSeconds,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.SmtpDeliveryAttempts.Add(attempt);

        queueItem.Attempts = attemptNumber;
        queueItem.LastAttemptAt = now;
        queueItem.LastError = errorMessage;
        queueItem.UpdatedAt = now;

        if (success)
        {
            queueItem.Status = "Delivered";
        }
        else
        {
            if (permanentFailure || attemptNumber >= _maxAttempts || _retryOptions.IsExpired(queueItem.CreatedAt, now))
            {
                queueItem.Status = "DeadLetter";
            }
            else
            {
                queueItem.Status = "Failed";
                queueItem.NextAttemptAt = Min(now.Add(retryDelay), queueItem.CreatedAt + _retryOptions.Lifetime);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return attempt;
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
}
