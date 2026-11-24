namespace Miautrix.Mail.Application.Mail;

public sealed record SenderAuthorizationResult(bool IsAuthorized, string EnvelopeSender, string RawMessage, string? FailureReason = null);

public interface ISenderAuthorizationService
{
    Task<bool> IsEnvelopeSenderAllowedAsync(Guid tenantId, Guid userId, string sender, CancellationToken ct = default);
    Task<bool> HasSharedMailboxSendPermissionAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken ct = default);
    Task<SenderAuthorizationResult> AuthorizeMessageAsync(Guid tenantId, Guid userId, string envelopeSender, string rawMessage, CancellationToken ct = default);
    Task<SenderAuthorizationResult> AuthorizeWebmailAsync(Guid tenantId, Guid userId, string requestedFrom, string rawMessage, CancellationToken ct = default);
}
