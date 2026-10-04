using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Protocols.Smtp;

/// <summary>
/// Resolves the recipient's domain against the verified domains in the database.
/// <para>
/// This is the open-relay guard. <see cref="SmtpInboundHandler"/> runs it before a message is
/// queued, and the Cloudflare inbound webhook resolves its tenant through it as well, so both
/// intake paths reject the same addresses. It lives here rather than in one host so the two hosts
/// cannot drift apart.
/// </para>
/// </summary>
public sealed class SmtpDomainValidator : ISmtpDomainValidator
{
    private readonly AppDbContext _db;

    public SmtpDomainValidator(AppDbContext db) => _db = db;

    public bool IsDomainLocal(string emailAddress, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(emailAddress)) return false;
        var at = emailAddress.LastIndexOf('@');
        if (at < 0 || at == emailAddress.Length - 1) return false;
        var domain = emailAddress[(at + 1)..].Trim().ToLowerInvariant();
        var match = _db.Domains.AsNoTracking().FirstOrDefault(d => d.Name.ToLower() == domain && d.IsVerified);
        if (match is null) return false;
        tenantId = match.TenantId;
        return true;
    }
}
