using Miautrix.Mail.Domain;
using Miautrix.Mail.Queue;
using Miautrix.Mail.Application.Transport;

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
    private readonly IInboundAuthenticationService? _authentication;

    public SmtpInboundHandler(
        ISmtpDomainValidator domainValidator,
        ISmtpQueueManager queueManager,
        IInboundAuthenticationService? authentication = null)
    {
        _domainValidator = domainValidator;
        _queueManager = queueManager;
        _authentication = authentication;
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

        if (_authentication is not null)
        {
            var auth = await _authentication.EvaluateAsync(new InboundAuthenticationInput(null, null, sender, rawMessage, InboundMailPath.Smtp25), cancellationToken);
            var separator = rawMessage.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            var headerEnd = separator >= 0 ? separator : rawMessage.IndexOf("\n\n", StringComparison.Ordinal);
            var headerLength = separator >= 0 ? 4 : 2;
            var headerBlock = headerEnd >= 0 ? rawMessage[..headerEnd] : rawMessage;
            var retained = string.Join("\r\n", headerBlock.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => !line.StartsWith("Authentication-Results:", StringComparison.OrdinalIgnoreCase) ||
                    !line.Contains(Environment.GetEnvironmentVariable("MIAUTRIX_HOSTNAME") ?? string.Empty, StringComparison.OrdinalIgnoreCase)));
            rawMessage = auth.AuthenticationResults + "\r\n" + retained + "\r\n\r\n" + (headerEnd >= 0 ? rawMessage[(headerEnd + headerLength)..] : string.Empty);
        }

        var queueItem = await _queueManager.EnqueueAsync(
            tenantId,
            sender,
            recipient,
            rawMessage,
            QueueDirection.Inbound,
            subject,
            cancellationToken);

        return (SmtpResponse.Queued, queueItem);
    }
}
