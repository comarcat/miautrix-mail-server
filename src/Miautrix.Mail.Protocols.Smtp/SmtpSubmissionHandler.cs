using Miautrix.Mail.Domain;
using Miautrix.Mail.Queue;
using Miautrix.Mail.Application.Mail;
using System.Globalization;
using System.Text;

namespace Miautrix.Mail.Protocols.Smtp;

public interface ISmtpSubmissionHandler
{
    SmtpResponse HandleAuth(string username, string password, bool isTlsEncrypted, out Guid? tenantId, out Guid? userId);
    Task<bool> IsEnvelopeSenderAllowedAsync(Guid tenantId, Guid userId, string sender, CancellationToken ct = default);

    Task<(SmtpResponse Response, SmtpQueueItem? QueueItem)> HandleSubmissionAsync(
        bool isAuthenticated,
        bool isTlsEncrypted,
        Guid? tenantId,
        Guid? userId,
        string sender,
        string recipient,
        string rawMessage,
        string? subject = null,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpSubmissionHandler : ISmtpSubmissionHandler
{
    private readonly ISmtpAuthenticator _authenticator;
    private readonly ISmtpQueueManager _queueManager;
    private readonly ISenderAuthorizationService _senderAuthorization;

    public SmtpSubmissionHandler(
        ISmtpAuthenticator authenticator,
        ISmtpQueueManager queueManager,
        ISenderAuthorizationService senderAuthorization)
    {
        _authenticator = authenticator;
        _queueManager = queueManager;
        _senderAuthorization = senderAuthorization;
    }

    public SmtpResponse HandleAuth(string username, string password, bool isTlsEncrypted, out Guid? tenantId, out Guid? userId)
    {
        tenantId = null;
        userId = null;

        // Security rule: No SMTP authentication without encryption.
        if (!isTlsEncrypted)
        {
            return SmtpResponse.EncryptionRequired;
        }

        if (_authenticator.Authenticate(username, password, isTlsEncrypted, out var resolvedTenantId, out var resolvedUserId))
        {
            tenantId = resolvedTenantId;
            userId = resolvedUserId;
            return SmtpResponse.Ok;
        }

        return SmtpResponse.AuthenticationRequired;
    }

    public Task<bool> IsEnvelopeSenderAllowedAsync(Guid tenantId, Guid userId, string sender, CancellationToken ct = default) =>
        _senderAuthorization.IsEnvelopeSenderAllowedAsync(tenantId, userId, sender, ct);

    public static string EnsureSubmissionHeaders(string rawMessage)
    {
        var separator = rawMessage.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var newline = "\r\n";
        if (separator < 0)
        {
            separator = rawMessage.IndexOf("\n\n", StringComparison.Ordinal);
            newline = "\n";
        }
        if (separator < 0) return rawMessage;

        var headers = rawMessage[..separator];
        var hasDate = headers.Split('\n').Any(line => line.TrimEnd('\r').StartsWith("Date:", StringComparison.OrdinalIgnoreCase));
        var hasMessageId = headers.Split('\n').Any(line => line.TrimEnd('\r').StartsWith("Message-ID:", StringComparison.OrdinalIgnoreCase));
        if (hasDate && hasMessageId) return rawMessage;

        var additions = new StringBuilder(newline);
        if (!hasDate)
            additions.Append("Date: ").Append(DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture)).Append(newline);
        if (!hasMessageId)
            additions.Append("Message-ID: <").Append(Guid.NewGuid().ToString("N")).Append("@miautrix.local>").Append(newline);
        return rawMessage.Insert(separator, additions.ToString());
    }

    public async Task<(SmtpResponse Response, SmtpQueueItem? QueueItem)> HandleSubmissionAsync(
        bool isAuthenticated,
        bool isTlsEncrypted,
        Guid? tenantId,
        Guid? userId,
        string sender,
        string recipient,
        string rawMessage,
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        // WHEN an unauthenticated client attempts submission on 587 THE SYSTEM SHALL reject with 530 5.7.0.
        if (!isAuthenticated || tenantId == null)
        {
            return (SmtpResponse.AuthenticationRequired, null);
        }

        // Security rule: No submission without encryption
        if (!isTlsEncrypted)
        {
            return (SmtpResponse.EncryptionRequired, null);
        }

        if (userId is null)
            return (SmtpResponse.SenderRejected, null);

        rawMessage = EnsureSubmissionHeaders(rawMessage);
        var authorized = await _senderAuthorization.AuthorizeMessageAsync(tenantId.Value, userId.Value, sender, rawMessage, cancellationToken);
        if (!authorized.IsAuthorized)
            return (SmtpResponse.SenderRejected, null);

        var queueItem = await _queueManager.EnqueueAsync(
            tenantId.Value,
            authorized.EnvelopeSender,
            recipient,
            authorized.RawMessage,
            QueueDirection.Outbound,
            subject,
            cancellationToken);

        return (SmtpResponse.Queued, queueItem);
    }
}
