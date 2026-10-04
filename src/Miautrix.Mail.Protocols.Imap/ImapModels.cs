namespace Miautrix.Mail.Protocols.Imap;

/// <summary>
/// Result of an IMAP LOGIN/AUTHENTICATE attempt.
/// </summary>
/// <param name="IsSuccess">True if the credentials were accepted and the mailbox resolved.</param>
/// <param name="TenantId">Tenant scope for the authenticated session.</param>
/// <param name="MailboxId">Mailbox the session operates on.</param>
public sealed record ImapAuthenticationResult(bool IsSuccess, Guid TenantId, Guid MailboxId);

/// <summary>
/// Verifies IMAP credentials and resolves them to a tenant + mailbox.
/// </summary>
public interface IImapAuthenticator
{
    Task<ImapAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
}
