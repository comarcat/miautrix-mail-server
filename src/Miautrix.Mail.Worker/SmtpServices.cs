using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Protocols.Smtp;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Worker;

/// <summary>
/// Verifies IMAP credentials against the same credential store the REST login and
/// <see cref="SmtpAuthenticator"/> use. The username is the mailbox address; the password is
/// checked against the owning user's Argon2id hash. No plaintext comparison, no bypass.
/// </summary>
public sealed class ImapAuthenticator : IImapAuthenticator
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public ImapAuthenticator(AppDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<ImapAuthenticationResult> AuthenticateAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        var denied = new ImapAuthenticationResult(false, Guid.Empty, Guid.Empty);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return denied;
        }

        var normalised = username.Trim().ToLowerInvariant();

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalised && u.IsActive, cancellationToken);
        if (user is null)
        {
            return denied;
        }

        var credential = await _db.UserCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == user.TenantId && c.UserId == user.Id, cancellationToken);
        if (credential is null || !_passwordHasher.VerifyPassword(password, credential.PasswordHash))
        {
            return denied;
        }

        // Only mailboxes inside the user's own tenant are reachable.
        var mailbox = await _db.Mailboxes.AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.TenantId == user.TenantId && m.Address.ToLower() == normalised && m.IsActive,
                cancellationToken);
        if (mailbox is null)
        {
            return denied;
        }

        return new ImapAuthenticationResult(true, user.TenantId, mailbox.Id);
    }
}

public sealed class SmtpAuthenticator : ISmtpAuthenticator
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public SmtpAuthenticator(AppDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public bool Authenticate(string username, string password, bool isTlsEncrypted, out Guid tenantId, out Guid userId)
    {
        tenantId = Guid.Empty;
        userId = Guid.Empty;
        if (!isTlsEncrypted || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return false;

        var normalised = username.Trim().ToLowerInvariant();
        var user = _db.Users.AsNoTracking().FirstOrDefault(u => u.Email.ToLower() == normalised && u.IsActive);
        if (user is null) return false;

        var credential = _db.UserCredentials.AsNoTracking()
            .FirstOrDefault(c => c.TenantId == user.TenantId && c.UserId == user.Id);
        if (credential is null || !_passwordHasher.VerifyPassword(password, credential.PasswordHash)) return false;

        tenantId = user.TenantId;
        userId = user.Id;
        return true;
    }
}
