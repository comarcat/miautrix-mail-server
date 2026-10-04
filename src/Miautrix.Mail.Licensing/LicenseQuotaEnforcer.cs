using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Licensing;

public interface ILicenseQuotaEnforcer
{
    Task<bool> CanCreateMailboxAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Mailbox> CreateMailboxGuardedAsync(Guid tenantId, Guid domainId, string address, long quotaBytes, CancellationToken cancellationToken = default);
    Task DeliverMessageAsync(Message message, CancellationToken cancellationToken = default);
}

public class LicenseQuotaException : InvalidOperationException
{
    public LicenseQuotaException(string message) : base(message) { }
}

public class LicenseQuotaEnforcer : ILicenseQuotaEnforcer
{
    private readonly AppDbContext _db;
    public const int DefaultMailboxAllowance = 5;

    public LicenseQuotaEnforcer(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetMailboxAllowanceAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Check license entitlements or return default allowance
        return DefaultMailboxAllowance;
    }

    public async Task<bool> CanCreateMailboxAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var currentCount = await _db.Mailboxes.CountAsync(m => m.TenantId == tenantId, cancellationToken);
        var allowance = await GetMailboxAllowanceAsync(tenantId, cancellationToken);
        return currentCount < allowance;
    }

    public async Task<Mailbox> CreateMailboxGuardedAsync(Guid tenantId, Guid domainId, string address, long quotaBytes, CancellationToken cancellationToken = default)
    {
        if (!await CanCreateMailboxAsync(tenantId, cancellationToken))
        {
            throw new LicenseQuotaException($"Tenant {tenantId} has reached or exceeded its mailbox allowance.");
        }

        var mailbox = new Mailbox
        {
            TenantId = tenantId,
            DomainId = domainId,
            Address = address,
            QuotaBytes = quotaBytes,
            IsActive = true
        };

        _db.Mailboxes.Add(mailbox);
        await _db.SaveChangesAsync(cancellationToken);
        return mailbox;
    }

    public async Task DeliverMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        // Even if tenant is over mailbox allowance, message delivery for existing mailboxes MUST proceed!
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
