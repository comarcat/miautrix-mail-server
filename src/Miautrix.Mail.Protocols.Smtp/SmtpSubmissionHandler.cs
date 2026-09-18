using Miautrix.Mail.Domain;
using Miautrix.Mail.Queue;

namespace Miautrix.Mail.Protocols.Smtp;

public interface ISmtpSubmissionHandler
{
    SmtpResponse HandleAuth(string username, string password, bool isTlsEncrypted, out Guid? tenantId, out Guid? userId);

    Task<(SmtpResponse Response, SmtpQueueItem? QueueItem)> HandleSubmissionAsync(
        bool isAuthenticated,
        bool isTlsEncrypted,
        Guid? tenantId,
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

    public SmtpSubmissionHandler(
        ISmtpAuthenticator authenticator,
        ISmtpQueueManager queueManager)
    {
        _authenticator = authenticator;
        _queueManager = queueManager;
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

    public async Task<(SmtpResponse Response, SmtpQueueItem? QueueItem)> HandleSubmissionAsync(
        bool isAuthenticated,
        bool isTlsEncrypted,
        Guid? tenantId,
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

        var queueItem = await _queueManager.EnqueueAsync(
            tenantId.Value,
            sender,
            recipient,
            rawMessage,
            subject,
            cancellationToken);

        return (SmtpResponse.Queued, queueItem);
    }
}
