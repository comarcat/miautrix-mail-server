using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Infrastructure.Backup;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;

using DnsClient;
using DnsClient.Protocol;

using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.Application.Admin;

public sealed class AdminService : IAdminService
{
    private static readonly DateTimeOffset ProcessStartedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _auth;
    private readonly IBackupService _backupService;
    private readonly BackupOptions _backupOptions;
    private readonly LookupClient _dnsClient = new LookupClient();

    public AdminService(AppDbContext db, ITenantAuthorizationHelper auth, IBackupService backupService, BackupOptions backupOptions)
    {
        _db = db;
        _auth = auth;
        _backupService = backupService;
        _backupOptions = backupOptions;
    }

    // -------------------------------------------------------------
    // Domains
    // -------------------------------------------------------------
    public async Task<IReadOnlyList<DomainDto>> ListDomainsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "domain.view");

        var domains = await _db.Domains
            .Where(d => d.TenantId == tenantId)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        return domains.Select(ToDomainDto).ToList();
    }

    public async Task<DomainDto?> GetDomainAsync(Guid tenantId, Guid userId, Guid domainId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "domain.view");

        var domain = await _db.Domains
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == domainId, ct);

        if (domain is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, domain, "domain.view");
        return ToDomainDto(domain);
    }

    public async Task<DomainDto> CreateDomainAsync(Guid tenantId, Guid userId, CreateDomainRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "domain.manage");

        var normalizedDomainName = request.Name.Trim().ToLowerInvariant();

        var dkimPublicKeyBase64 = GenerateDkimPublicKeyBase64();

        var domain = new DomainEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedDomainName,
            IsVerified = false,
            IsPrimary = request.IsPrimary,
            DkimSelector = "m1",
            DkimPublicKey = dkimPublicKeyBase64,
            SpfRecord = GenerateSpfRecord(normalizedDomainName),
            DmarcRecord = GenerateDmarcRecord(normalizedDomainName),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (request.IsPrimary)
        {
            var otherDomains = await _db.Domains.Where(d => d.TenantId == tenantId && d.IsPrimary).ToListAsync(ct);
            foreach (var d in otherDomains)
            {
                d.IsPrimary = false;
            }
        }

        _db.Domains.Add(domain);
        await _db.SaveChangesAsync(ct);

        return ToDomainDto(domain);
    }
    public async Task<VerifyDomainResult> VerifyDomainAsync(Guid tenantId, Guid userId, Guid domainId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "domain.manage");

        var domain = await _db.Domains
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == domainId, ct);

        if (domain is null)
        {
            throw new ResourceNotFoundException();
        }

        _auth.AuthorizeAccess(tenantId, userId, domain, "domain.manage");

        var normalizedDomainName = domain.Name;
        var selector = string.IsNullOrWhiteSpace(domain.DkimSelector) ? "m1" : domain.DkimSelector;

        // Extract expected DKIM public key base64 even if older rows stored full DKIM txt.
        var expectedDkimValue = domain.DkimPublicKey ?? string.Empty;
        var expectedPublicKeyBase64 = expectedDkimValue;
        if (expectedDkimValue.Contains("p=", StringComparison.OrdinalIgnoreCase))
        {
            var idx = expectedDkimValue.IndexOf("p=", StringComparison.OrdinalIgnoreCase) + 2;
            var tail = expectedDkimValue[idx..];
            var semi = tail.IndexOf(';');
            expectedPublicKeyBase64 = (semi >= 0 ? tail[..semi] : tail).Trim();
        }
        expectedPublicKeyBase64 = expectedPublicKeyBase64.Trim();

        string NormalizeTxt(string s) => string.IsNullOrWhiteSpace(s)
            ? string.Empty
            : string.Concat(s.Replace("\"", string.Empty).Where(c => !char.IsWhiteSpace(c)));

        string spfExpected = domain.SpfRecord ?? string.Empty;
        string dmarcExpected = domain.DmarcRecord ?? string.Empty;

        // ---------- DKIM ----------
        var dkimQuery = $"{selector}._domainkey.{normalizedDomainName}";
        var dkimResult = await _dnsClient.QueryAsync(dkimQuery, QueryType.TXT, cancellationToken: ct);
        var dkimTxt = dkimResult.Answers
            .OfType<TxtRecord>()
            .SelectMany(r => r.Text)
            .FirstOrDefault() ?? string.Empty;

        var dkimTxtNorm = NormalizeTxt(dkimTxt);
        var expectedDkimNorm = NormalizeTxt(expectedPublicKeyBase64);

        var dkimOk = !string.IsNullOrWhiteSpace(dkimTxt) && !string.IsNullOrWhiteSpace(expectedPublicKeyBase64) &&
                     dkimTxtNorm.Contains(expectedDkimNorm, StringComparison.OrdinalIgnoreCase);

        var dkimStatus = dkimOk
            ? $"Valid DKIM TXT at {dkimQuery}"
            : (!string.IsNullOrWhiteSpace(dkimTxt)
                ? $"DKIM TXT exists at {dkimQuery}, but does not match stored public key"
                : $"Missing DKIM TXT at {dkimQuery}");

        // ---------- SPF ----------
        var spfResult = await _dnsClient.QueryAsync(normalizedDomainName, QueryType.TXT, cancellationToken: ct);
        var spfTxt = spfResult.Answers
            .OfType<TxtRecord>()
            .SelectMany(r => r.Text)
            .FirstOrDefault(txt => txt.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

        var spfOk = !string.IsNullOrWhiteSpace(spfTxt) && !string.IsNullOrWhiteSpace(spfExpected) &&
                   NormalizeTxt(spfTxt).Contains(NormalizeTxt(spfExpected), StringComparison.OrdinalIgnoreCase);

        var spfStatus = spfOk
            ? "Valid SPF TXT"
            : (!string.IsNullOrWhiteSpace(spfTxt)
                ? "SPF TXT found, but does not match expected policy"
                : "Missing SPF TXT (v=spf1)");

        // ---------- DMARC ----------
        var dmarcQuery = $"_dmarc.{normalizedDomainName}";
        var dmarcResult = await _dnsClient.QueryAsync(dmarcQuery, QueryType.TXT, cancellationToken: ct);
        var dmarcTxt = dmarcResult.Answers
            .OfType<TxtRecord>()
            .SelectMany(r => r.Text)
            .FirstOrDefault(txt => txt.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

        var dmarcOk = !string.IsNullOrWhiteSpace(dmarcTxt) && !string.IsNullOrWhiteSpace(dmarcExpected) &&
                      NormalizeTxt(dmarcTxt).Contains(NormalizeTxt(dmarcExpected), StringComparison.OrdinalIgnoreCase);

        var dmarcStatus = dmarcOk
            ? "Valid DMARC TXT"
            : (!string.IsNullOrWhiteSpace(dmarcTxt)
                ? "DMARC TXT found, but does not match expected policy"
                : "Missing DMARC TXT (v=DMARC1)");

        var allOk = dkimOk && spfOk && dmarcOk;

        domain.IsVerified = allOk;
        domain.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var message = allOk
            ? $"DNS records verified successfully for domain {normalizedDomainName}."
            : $"DNS verification failed for domain {normalizedDomainName}. Fix the missing/invalid TXT records shown in this response.";

        return new VerifyDomainResult(
            IsVerified: allOk,
            Message: message,
            DkimStatus: dkimStatus,
            SpfStatus: spfStatus,
            DmarcStatus: dmarcStatus);
    }
    public async Task<bool> DeleteDomainAsync(Guid tenantId, Guid userId, Guid domainId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "domain.manage");

        var domain = await _db.Domains
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == domainId, ct);

        if (domain is null) return false;

        _auth.AuthorizeAccess(tenantId, userId, domain, "domain.manage");

        _db.Domains.Remove(domain);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    public async Task<DomainDto?> UpdateDomainAsync(Guid tenantId, Guid userId, Guid domainId, UpdateDomainRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "domain.manage");

        var domain = await _db.Domains.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == domainId, ct);
        if (domain is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, domain, "domain.manage");

        var changedDns = false;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            domain.Name = request.Name.Trim().ToLowerInvariant();
            changedDns = true;
        }

        if (request.IsPrimary.HasValue)
        {
            if (request.IsPrimary.Value)
            {
                var others = await _db.Domains.Where(d => d.TenantId == tenantId && d.IsPrimary && d.Id != domainId).ToListAsync(ct);
                foreach (var o in others) o.IsPrimary = false;
            }
            domain.IsPrimary = request.IsPrimary.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.DkimSelector))
        {
            domain.DkimSelector = request.DkimSelector.Trim();
            changedDns = true;
        }

        if (!string.IsNullOrWhiteSpace(request.SpfRecord))
        {
            domain.SpfRecord = request.SpfRecord.Trim();
            changedDns = true;
        }

        if (!string.IsNullOrWhiteSpace(request.DmarcRecord))
        {
            domain.DmarcRecord = request.DmarcRecord.Trim();
            changedDns = true;
        }

        if (changedDns)
        {
            domain.IsVerified = false;
        }

        domain.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToDomainDto(domain);
    }
    // Users
    // -------------------------------------------------------------
    public async Task<IReadOnlyList<AdminUserDto>> ListUsersAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.view");

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var mailboxes = await _db.Mailboxes
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(ct);

        var memberships = await _db.Memberships
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(ct);

        var roles = await _db.Roles
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(ct);

        var result = new List<AdminUserDto>();

        foreach (var user in users)
        {
            var mb = mailboxes.FirstOrDefault(m => string.Equals(m.Address, user.Email, StringComparison.OrdinalIgnoreCase));
            var membership = memberships.FirstOrDefault(m => m.UserId == user.Id);
            var role = membership != null ? roles.FirstOrDefault(r => r.Id == membership.RoleId)?.Name ?? "User" : "User";

            result.Add(new AdminUserDto(
                user.Id,
                user.Email,
                user.Name,
                user.IsActive,
                role,
                mb?.QuotaBytes ?? 10737418240,
                mb?.UsedBytes ?? 0,
                user.CreatedAt));
        }

        return result;
    }

    public async Task<AdminUserDto?> GetUserAsync(Guid tenantId, Guid userId, Guid targetUserId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.view");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == targetUserId, ct);

        if (user is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, user, "user.view");

        var mb = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Address.ToLower() == user.Email.ToLower(), ct);

        var membership = await _db.Memberships
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == user.Id, ct);

        string role = "User";
        if (membership?.RoleId != null)
        {
            var roleEntity = await _db.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == membership.RoleId, ct);
            if (roleEntity != null) role = roleEntity.Name;
        }

        return new AdminUserDto(
            user.Id,
            user.Email,
            user.Name,
            user.IsActive,
            role,
            mb?.QuotaBytes ?? 10737418240,
            mb?.UsedBytes ?? 0,
            user.CreatedAt);
    }

    public async Task<AdminUserDto> CreateUserAsync(Guid tenantId, Guid userId, CreateUserRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = request.Email.Trim().ToLowerInvariant(),
            Name = request.Name.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Users.Add(user);

        // Find domain for mailbox
        var domainName = request.Email.Split('@').LastOrDefault() ?? "";
        var domain = await _db.Domains.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Name.ToLower() == domainName.ToLower(), ct);
        var domainId = domain?.Id ?? Guid.NewGuid();

        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainId = domainId,
            Address = user.Email,
            QuotaBytes = request.QuotaBytes ?? 10737418240,
            UsedBytes = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Mailboxes.Add(mailbox);

        // Assign Role
        var roleCode = (request.Role ?? "user").ToLowerInvariant();
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code.ToLower() == roleCode, ct);
        if (role != null)
        {
            _db.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                RoleId = role.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);

        return new AdminUserDto(
            user.Id,
            user.Email,
            user.Name,
            user.IsActive,
            role?.Name ?? "User",
            mailbox.QuotaBytes,
            mailbox.UsedBytes,
            user.CreatedAt);
    }

    public async Task<AdminUserDto?> UpdateUserAsync(Guid tenantId, Guid userId, Guid targetUserId, UpdateUserRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == targetUserId, ct);

        if (user is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, user, "user.manage");

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            user.Name = request.Name.Trim();
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;

        var mb = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Address.ToLower() == user.Email.ToLower(), ct);

        if (mb != null && request.QuotaBytes.HasValue)
        {
            mb.QuotaBytes = request.QuotaBytes.Value;
            mb.UpdatedAt = DateTimeOffset.UtcNow;
        }

        string roleName = "User";
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && (r.Code.ToLower() == request.Role.ToLower() || r.Name.ToLower() == request.Role.ToLower()), ct);
            if (role != null)
            {
                roleName = role.Name;
                var membership = await _db.Memberships.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == user.Id, ct);
                if (membership != null)
                {
                    membership.RoleId = role.Id;
                    membership.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    _db.Memberships.Add(new Membership
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        UserId = user.Id,
                        RoleId = role.Id,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return new AdminUserDto(
            user.Id,
            user.Email,
            user.Name,
            user.IsActive,
            roleName,
            mb?.QuotaBytes ?? 10737418240,
            mb?.UsedBytes ?? 0,
            user.CreatedAt);
    }

    public async Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, Guid targetUserId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == targetUserId, ct);

        if (user is null) return false;

        _auth.AuthorizeAccess(tenantId, userId, user, "user.manage");

        var memberships = await _db.Memberships
            .Where(m => m.TenantId == tenantId && m.UserId == targetUserId)
            .ToListAsync(ct);

        _db.Memberships.RemoveRange(memberships);
        _db.Users.Remove(user);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Quarantine
    // -------------------------------------------------------------
    public async Task<IReadOnlyList<QuarantineItemDto>> ListQuarantineAsync(Guid tenantId, Guid userId, QuarantineFilter filter, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "quarantine.view");

        var query = _db.Quarantine.Where(q => q.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(q => q.Status.ToLower() == filter.Status.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(q => q.Sender.ToLower().Contains(search) || q.Recipient.ToLower().Contains(search) || (q.Subject != null && q.Subject.ToLower().Contains(search)));
        }

        var items = await query
            .OrderByDescending(q => q.QuarantinedAt)
            .Take(Math.Min(filter.Limit, 100))
            .ToListAsync(ct);

        return items.Select(q => new QuarantineItemDto(
            q.Id,
            q.Sender,
            q.Recipient,
            q.Subject,
            q.SpamScore,
            q.Threshold,
            q.ReasonsJson,
            q.Status,
            q.QuarantinedAt,
            q.ReleasedAt)).ToList();
    }

    public async Task<QuarantineItemDto?> GetQuarantineItemAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "quarantine.view");

        var item = await _db.Quarantine
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == id, ct);

        if (item is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, item, "quarantine.view");

        return new QuarantineItemDto(
            item.Id,
            item.Sender,
            item.Recipient,
            item.Subject,
            item.SpamScore,
            item.Threshold,
            item.ReasonsJson,
            item.Status,
            item.QuarantinedAt,
            item.ReleasedAt);
    }

    public async Task<bool> ReleaseQuarantineItemAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "quarantine.manage");

        var item = await _db.Quarantine
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == id, ct);

        if (item is null) return false;

        _auth.AuthorizeAccess(tenantId, userId, item, "quarantine.manage");

        item.Status = "Released";
        item.ReleasedAt = DateTimeOffset.UtcNow;
        item.IsDelivered = true;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteQuarantineItemAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "quarantine.manage");

        var item = await _db.Quarantine
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == id, ct);

        if (item is null) return false;

        _auth.AuthorizeAccess(tenantId, userId, item, "quarantine.manage");

        _db.Quarantine.Remove(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Audit
    // -------------------------------------------------------------
    public async Task<IReadOnlyList<AuditLogDto>> ListAuditLogsAsync(Guid tenantId, Guid userId, AuditFilter filter, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "audit.view");

        var query = _db.AuditLogs.Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(a => a.Action.ToLower() == filter.Action.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(a => a.Action.ToLower().Contains(s) || a.TargetType.ToLower().Contains(s) || (a.DetailsJson != null && a.DetailsJson.ToLower().Contains(s)));
        }

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Min(filter.Limit, 100))
            .ToListAsync(ct);

        var users = await _db.Users.Where(u => u.TenantId == tenantId).ToListAsync(ct);

        return logs.Select(l => new AuditLogDto(
            l.Id,
            l.Action,
            l.ActorId,
            l.ActorId != null ? users.FirstOrDefault(u => u.Id == l.ActorId)?.Email : null,
            l.TargetType,
            l.TargetId,
            l.DetailsJson,
            l.IpAddress,
            l.CreatedAt)).ToList();
    }

    // -------------------------------------------------------------
    // Mail Flow Rules
    // -------------------------------------------------------------
    public async Task<IReadOnlyList<MailFlowRuleDto>> ListRulesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "rule.view");

        var rules = await _db.MailFlowRules
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        return rules.Select(r => new MailFlowRuleDto(
            r.Id,
            r.Name,
            r.Priority,
            r.IsEnabled,
            r.ConditionsJson,
            r.ActionsJson,
            r.CreatedAt)).ToList();
    }

    public async Task<MailFlowRuleDto?> GetRuleAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "rule.view");

        var rule = await _db.MailFlowRules
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

        if (rule is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, rule, "rule.view");

        return new MailFlowRuleDto(
            rule.Id,
            rule.Name,
            rule.Priority,
            rule.IsEnabled,
            rule.ConditionsJson,
            rule.ActionsJson,
            rule.CreatedAt);
    }

    public async Task<MailFlowRuleDto> CreateRuleAsync(Guid tenantId, Guid userId, CreateRuleRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "rule.manage");

        var rule = new MailFlowRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Priority = request.Priority,
            IsEnabled = request.IsEnabled,
            ConditionsJson = request.ConditionsJson,
            ActionsJson = request.ActionsJson,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.MailFlowRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return new MailFlowRuleDto(
            rule.Id,
            rule.Name,
            rule.Priority,
            rule.IsEnabled,
            rule.ConditionsJson,
            rule.ActionsJson,
            rule.CreatedAt);
    }

    public async Task<MailFlowRuleDto?> UpdateRuleAsync(Guid tenantId, Guid userId, Guid id, UpdateRuleRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "rule.manage");

        var rule = await _db.MailFlowRules
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

        if (rule is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, rule, "rule.manage");

        if (!string.IsNullOrWhiteSpace(request.Name)) rule.Name = request.Name.Trim();
        if (request.Priority.HasValue) rule.Priority = request.Priority.Value;
        if (request.IsEnabled.HasValue) rule.IsEnabled = request.IsEnabled.Value;
        if (!string.IsNullOrWhiteSpace(request.ConditionsJson)) rule.ConditionsJson = request.ConditionsJson;
        if (!string.IsNullOrWhiteSpace(request.ActionsJson)) rule.ActionsJson = request.ActionsJson;

        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new MailFlowRuleDto(
            rule.Id,
            rule.Name,
            rule.Priority,
            rule.IsEnabled,
            rule.ConditionsJson,
            rule.ActionsJson,
            rule.CreatedAt);
    }

    public async Task<bool> DeleteRuleAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "rule.manage");

        var rule = await _db.MailFlowRules
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

        if (rule is null) return false;

        _auth.AuthorizeAccess(tenantId, userId, rule, "rule.manage");

        _db.MailFlowRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<RuleSimulationResult> SimulateRuleAsync(Guid tenantId, Guid userId, RuleSimulationRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "rule.view");

        var actions = new List<string>();
        var logs = new List<string>
        {
            $"Evaluating message from '{request.SampleMessage.Sender}' to '{request.SampleMessage.Recipient}'",
            $"Subject: {request.SampleMessage.Subject}"
        };

        bool matched = true;
        if (request.SampleMessage.Subject.Contains("[SPAM]", StringComparison.OrdinalIgnoreCase) ||
            (request.SampleMessage.SpamScore.HasValue && request.SampleMessage.SpamScore > 5.0))
        {
            actions.Add("Quarantine Message");
            logs.Add("Triggered high spam score condition -> Action: Quarantine");
        }
        else
        {
            actions.Add("Deliver to Inbox");
            logs.Add("Standard rule path matched -> Action: Deliver");
        }

        return Task.FromResult(new RuleSimulationResult(
            Matched: matched,
            ActionsTaken: actions,
            Score: request.SampleMessage.SpamScore ?? 1.2,
            Log: logs));
    }

    // -------------------------------------------------------------
    // System & Telemetry
    // -------------------------------------------------------------
    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "system.view");

        var queued = await _db.SmtpQueue.CountAsync(q => q.TenantId == tenantId && q.Status == "Pending", ct);
        var retrying = await _db.SmtpQueue.CountAsync(q => q.TenantId == tenantId && q.Status == "Retrying", ct);
        var deadLetters = await _db.SmtpQueue.CountAsync(q => q.TenantId == tenantId && q.Status == "DeadLetter", ct);

        var since24h = DateTimeOffset.UtcNow.AddHours(-24);
        var delivered = await _db.Messages.CountAsync(m => m.TenantId == tenantId && m.CreatedAt >= since24h, ct);
        var quarantined = await _db.Quarantine.CountAsync(q => q.TenantId == tenantId && q.QuarantinedAt >= since24h, ct);
        var spamBlocked = await _db.SpamVerdicts.CountAsync(s => s.TenantId == tenantId && s.IsSpam && s.CreatedAt >= since24h, ct);

        var tenantCount = await _db.Tenants.CountAsync(ct);
        var canConnect = await _db.Database.CanConnectAsync(ct);
        var uptimeSeconds = (long)(DateTimeOffset.UtcNow - ProcessStartedAt).TotalSeconds;

        return new DashboardSummaryDto(
            ActiveQueued: queued,
            Retrying: retrying,
            DeadLetters: deadLetters,
            Delivered24h: delivered,
            Quarantined24h: quarantined,
            SpamBlocked24h: spamBlocked,
            SystemHealth: canConnect ? "Optimal" : "Degraded",
            UptimeSeconds: uptimeSeconds,
            TenantCount: tenantCount);
    }

    public async Task<SystemInfoDto> GetSystemInfoAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "system.view");

        var canConnect = await _db.Database.CanConnectAsync(ct);
        var uptimeSeconds = (long)(DateTimeOffset.UtcNow - ProcessStartedAt).TotalSeconds;
        var version = typeof(AdminService).Assembly.GetName().Version?.ToString() ?? "unknown";

        long storageUsedBytes = 0;
        long storageTotalBytes = 0;
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.RootDirectory.FullName == "/")
                ?? DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);
            if (drive is not null)
            {
                storageTotalBytes = drive.TotalSize;
                storageUsedBytes = drive.TotalSize - drive.AvailableFreeSpace;
            }
        }
        catch (IOException)
        {
            // Storage telemetry is best-effort; leave zeros if the volume can't be queried.
        }

        return new SystemInfoDto(
            Version: version,
            Runtime: $".NET {Environment.Version}",
            DatabaseStatus: canConnect ? "Connected (PostgreSQL)" : "Unreachable",
            UptimeSeconds: uptimeSeconds,
            StorageUsedBytes: storageUsedBytes,
            StorageTotalBytes: storageTotalBytes,
            ActiveWorkers: Environment.ProcessorCount,
            OsVersion: RuntimeInformation.OSDescription);
    }

    public async Task<LicensingDto> GetLicensingAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "system.view");

        var mailboxCount = await _db.Mailboxes.CountAsync(m => m.TenantId == tenantId, ct);

        return new LicensingDto(
            Edition: "Enterprise Edition (Self-Hosted)",
            ActiveMailboxes: mailboxCount,
            MaxMailboxes: 1000,
            MfaIncluded: true,
            BackupIncluded: true,
            CustomDomainsIncluded: true,
            Status: "Active",
            ValidUntil: DateTimeOffset.UtcNow.AddYears(1));
    }

    public async Task<IReadOnlyList<BackupJobDto>> GetBackupJobsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "system.view");

        var jobs = await _db.BackupJobs
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        return jobs.Select(ToBackupJobDto).ToList();
    }

    public async Task<BackupJobDto> CreateBackupAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "backup.create");

        var name = $"Manual Snapshot {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC";
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Status = "Running",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.BackupJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        Directory.CreateDirectory(_backupOptions.Directory);
        var archivePath = Path.Combine(_backupOptions.Directory, $"miautrix_backup_{tenantId:N}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.zip");

        try
        {
            await _backupService.CreateBackupAsync(_db, archivePath, ct);

            job.Status = "Completed";
            job.ArchivePath = archivePath;
            job.SizeBytes = new FileInfo(archivePath).Length;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.ErrorMessage = ex.Message;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        await _db.SaveChangesAsync(ct);
        return ToBackupJobDto(job);
    }

    private static BackupJobDto ToBackupJobDto(BackupJob j) => new(
        j.Id,
        j.Name,
        j.Status,
        j.SizeBytes,
        j.CreatedAt,
        j.CompletedAt);

    // -------------------------------------------------------------
    // DNS record generation (inlined; Application cannot reference Protocols.Smtp)
    // -------------------------------------------------------------
    private static string GenerateDkimPublicKeyBase64()
    {
        using var rsa = RSA.Create(2048);
        return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
    }

    private static string GenerateSpfRecord(string domainName) =>
        $"v=spf1 mx a:mail.{domainName} -all";

    private static string GenerateDmarcRecord(string domainName) =>
        $"v=DMARC1; p=reject; rua=mailto:dmarc-reports@{domainName}; pct=100";

    private static DomainDto ToDomainDto(DomainEntity d) => new(
        d.Id,
        d.Name,
        d.IsVerified,
        d.DkimSelector,
        d.DkimPublicKey,
        d.SpfRecord,
        d.DmarcRecord,
        d.IsPrimary,
        d.CreatedAt);
}
