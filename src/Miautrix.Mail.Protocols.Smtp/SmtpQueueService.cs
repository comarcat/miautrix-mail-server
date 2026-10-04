using Miautrix.Mail.Domain;

namespace Miautrix.Mail.Protocols.Smtp;

public interface ISmtpQueueService
{
    Task<SmtpQueueItem> EnqueueMessageAsync(Guid tenantId, string sender, string recipient, string rawMessage);
}

public sealed class SmtpQueueService : ISmtpQueueService
{
    private readonly Miautrix.Mail.Persistence.AppDbContext _context;

    public SmtpQueueService(Miautrix.Mail.Persistence.AppDbContext context)
    {
        _context = context;
    }

    public async Task<SmtpQueueItem> EnqueueMessageAsync(Guid tenantId, string sender, string recipient, string rawMessage)
    {
        var queueItem = new SmtpQueueItem
        {
            TenantId = tenantId,
            Sender = sender,
            Recipient = recipient,
            RawMessage = rawMessage,
            Status = "Pending",
            Attempts = 0,
            NextAttemptAt = DateTimeOffset.UtcNow
        };

        _context.SmtpQueue.Add(queueItem);
        await _context.SaveChangesAsync();
        return queueItem;
    }
}
