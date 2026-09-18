using Miautrix.Mail.Domain;
using Miautrix.Mail.Queue;

namespace Miautrix.Mail.Protocols.Smtp;

public interface ISmtpInboundHandler
{
    SmtpResponse HandleRcptTo(string recipientAddress, out Guid tenantId);

    Task<(SmtpResponse Response, SmtpQueueItem? QueueItem)> HandleDataAsync(
        Guid tenantId,
        string sender,
        string recipient,
        string rawMessage,
        string? subject = null,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpInboundHandler : ISmtpInboundHandler
{
    private readonly ISmtpDomainValidator _domainValidator;
    private readonly ISmtpQueueManager _queueManager;

    public SmtpInboundHandler(
        ISmtpDomainValidator domainValidator,
        ISmtpQueueManager queueManager)
    {
        _domainValidator = domainValidator;
        _queueManager = queueManager;
    }

    public SmtpResponse HandleRcptTo(string recipientAddress, out Guid tenantId)
    {
        if (!_domainValidator.IsDomainLocal(recipientAddress, out tenantId))
        {
            // Open-relay protection: reject recipient outside tenant domains
            return SmtpResponse.RelayAccessDenied;
        }

        return SmtpResponse.Ok;
    }

    public async Task<(SmtpResponse Response, SmtpQueueItem? QueueItem)> HandleDataAsync(
        Guid tenantId,
        string sender,
        string recipient,
        string rawMessage,
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        if (!_domainValidator.IsDomainLocal(recipient, out var resolvedTenantId) || resolvedTenantId != tenantId)
        {
            return (SmtpResponse.RelayAccessDenied, null);
        }

        var queueItem = await _queueManager.EnqueueAsync(
            tenantId,
            sender,
            recipient,
            rawMessage,
            subject,
            cancellationToken);

        return (SmtpResponse.Queued, queueItem);
    }
}
