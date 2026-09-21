using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Infrastructure.Backup;
using Miautrix.Mail.Infrastructure.MailboxArchiving;
using Miautrix.Mail.Identity;
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
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMailboxArchiveService _mailboxArchiveService;
    private readonly ICloudflareTransport _cloudflareTransport;
    private readonly LookupClient _dnsClient = new LookupClient();

    public AdminService(
        AppDbContext db,
        ITenantAuthorizationHelper auth,
        IBackupService backupService,
        BackupOptions backupOptions,
        IPasswordHasher passwordHasher,
        IMailboxArchiveService mailboxArchiveService,
        ICloudflareTransport cloudflareTransport)
    {
        _db = db;
        _auth = auth;
        _backupService = backupService;
        _backupOptions = backupOptions;
        _passwordHasher = passwordHasher;
        _mailboxArchiveService = mailboxArchiveService;
        _cloudflareTransport = cloudflareTransport;
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

        var transportMode = string.IsNullOrWhiteSpace(request.TransportMode)
            ? DomainTransportModes.Local
            : request.TransportMode.Trim().ToLowerInvariant();
        if (!DomainTransportModes.IsKnown(transportMode))
        {
            throw new ArgumentException($"Unknown transport mode '{request.TransportMode}'.", nameof(request));
        }

        var normalizedDomainName = request.Name.Trim().ToLowerInvariant();

        var dkimPublicKeyBase64 = GenerateDkimPublicKeyBase64();

        var domain = new DomainEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedDomainName,
            IsVerified = false,
            IsPrimary = request.IsPrimary,
            TransportMode = transportMode,
            CloudflareZoneId = request.CloudflareZoneId?.Trim(),
            CloudflareWorkerUrl = request.CloudflareWorkerUrl?.Trim(),
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

        // A Cloudflare-mode domain publishes nothing itself: Cloudflare owns the zone and answers
        // MX on our behalf, so the DKIM/SPF/DMARC lookups below would only ever report our own TXT
        // records missing. The claim worth verifying is that the Worker we hand mail to is up.
        if (string.Equals(domain.TransportMode, DomainTransportModes.Cloudflare, StringComparison.OrdinalIgnoreCase))
        {
            var probe = await _cloudflareTransport.ProbeAsync(domain, ct);

            domain.IsVerified = probe.IsReachable;
            domain.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new VerifyDomainResult(
                IsVerified: probe.IsReachable,
                Message: probe.IsReachable
                    ? $"Cloudflare Worker verified for domain {normalizedDomainName}."
                    : $"Cloudflare Worker check failed for domain {normalizedDomainName}. {probe.Status}",
                TransportMode: DomainTransportModes.Cloudflare,
                DkimStatus: null,
                SpfStatus: null,
                DmarcStatus: null,
                WorkerStatus: probe.Status);
        }

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

        try
        {
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
                TransportMode: DomainTransportModes.Local,
                DkimStatus: dkimStatus,
                SpfStatus: spfStatus,
                DmarcStatus: dmarcStatus,
                WorkerStatus: null);
        }
        catch (DnsResponseException ex)
        {
            // An unreachable resolver is not a verdict about the domain's records, and a 500 tells
            // the operator nothing they can act on. Report the reason and leave the domain's
            // existing verification state untouched: a DNS outage must not silently un-verify a
            // domain that is already delivering mail.
            return new VerifyDomainResult(
                IsVerified: domain.IsVerified,
                Message: $"DNS lookup failed while verifying {normalizedDomainName}: {ex.Message}",
                TransportMode: DomainTransportModes.Local,
                DkimStatus: null,
                SpfStatus: null,
                DmarcStatus: null,
                WorkerStatus: null);
        }
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

        // Switching transport invalidates the previous verification: the old proof was about a
        // different claim (our own TXT records vs. a reachable Worker), so it cannot carry over.
        if (!string.IsNullOrWhiteSpace(request.TransportMode))
        {
            var mode = request.TransportMode.Trim().ToLowerInvariant();
            if (!DomainTransportModes.IsKnown(mode))
            {
                throw new ArgumentException($"Unknown transport mode '{request.TransportMode}'.", nameof(request));
            }

            if (!string.Equals(domain.TransportMode, mode, StringComparison.Ordinal))
            {
                domain.TransportMode = mode;
                changedDns = true;
            }
        }

        if (request.CloudflareZoneId is not null)
        {
            domain.CloudflareZoneId = string.IsNullOrWhiteSpace(request.CloudflareZoneId)
                ? null
                : request.CloudflareZoneId.Trim();
            changedDns = true;
        }

        if (request.CloudflareWorkerUrl is not null)
        {
            domain.CloudflareWorkerUrl = string.IsNullOrWhiteSpace(request.CloudflareWorkerUrl)
                ? null
                : request.CloudflareWorkerUrl.Trim();
            changedDns = true;
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

        var credentials = await _db.UserCredentials
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(ct);

        return users
            .Select(user => ToAdminUserDto(
                user,
                mailboxes.FirstOrDefault(m => string.Equals(m.Address, user.Email, StringComparison.OrdinalIgnoreCase)),
                memberships.FirstOrDefault(m => m.UserId == user.Id),
                roles,
                credentials.FirstOrDefault(c => c.UserId == user.Id)))
            .ToList();
    }

    public async Task<AdminUserDto?> GetUserAsync(Guid tenantId, Guid userId, Guid targetUserId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.view");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == targetUserId, ct);

        if (user is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, user, "user.view");
        return await BuildAdminUserDtoAsync(tenantId, user, ct);
    }

    public async Task<AdminUserDto> CreateUserAsync(Guid tenantId, Guid userId, CreateUserRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");

        var email = NormalizeEmail(request.Email);
        var mailboxKind = NormalizeMailboxKind(request.MailboxKind);
        if (mailboxKind != "user")
        {
            throw new InvalidOperationException("Use the shared mailbox endpoint to create shared mailboxes.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Password is required for user mailboxes.");
        }

        await EnsureEmailAvailableAsync(tenantId, email, null, null, ct);

        var domain = await FindDomainForEmailAsync(tenantId, email, ct);
        if (domain is null)
        {
            throw new InvalidOperationException("A verified tenant domain is required for this email address.");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Name = request.Name.Trim(),
            IsActive = true,
            IsService = request.IsService,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        _db.UserCredentials.Add(new UserCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Algorithm = "argon2id",
            MustChangePassword = request.MustChangePassword,
            CreatedAt = now,
            UpdatedAt = now
        });

        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainId = domain.Id,
            Address = email,
            Name = request.Name.Trim(),
            Kind = "user",
            QuotaBytes = ValidateQuotaBytes(request.QuotaBytes),
            UsedBytes = 0,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Mailboxes.Add(mailbox);

        var roleCode = (request.Role ?? "member").Trim().ToLowerInvariant();
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code.ToLower() == roleCode, ct);
        if (role is null)
        {
            throw new InvalidOperationException($"Role '{roleCode}' was not found.");
        }

        _db.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            RoleId = role.Id,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return ToAdminUserDto(user, mailbox, role.Code, request.MustChangePassword);
    }

    public async Task<SharedMailboxDto> CreateSharedMailboxAsync(Guid tenantId, Guid userId, CreateSharedMailboxRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");

        var email = NormalizeEmail(request.Email);
        var name = NormalizeDisplayName(request.Name);
        var quotaBytes = ValidateQuotaBytes(request.QuotaBytes);
        await EnsureEmailAvailableAsync(tenantId, email, null, null, ct);

        var domain = await FindDomainForEmailAsync(tenantId, email, ct);
        if (domain is null)
        {
            throw new InvalidOperationException("A verified tenant domain is required for this email address.");
        }

        var now = DateTimeOffset.UtcNow;
        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainId = domain.Id,
            Address = email,
            Name = name,
            Kind = "shared",
            QuotaBytes = quotaBytes,
            UsedBytes = 0,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Mailboxes.Add(mailbox);
        await ReplaceMailboxDelegatesAsync(tenantId, mailbox, request.Delegates ?? Array.Empty<MailboxDelegateRequest>(), now, ct);
        await _db.SaveChangesAsync(ct);

        return await BuildSharedMailboxDtoAsync(tenantId, mailbox, ct);
    }

    public async Task<AdminUserDto?> UpdateUserAsync(Guid tenantId, Guid userId, Guid targetUserId, UpdateUserRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == targetUserId, ct);

        if (user is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, user, "user.manage");

        var now = DateTimeOffset.UtcNow;
        var oldEmail = user.Email;
        var mb = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Address.ToLower() == oldEmail.ToLower(), ct);

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = NormalizeEmail(request.Email);
            if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                await EnsureEmailAvailableAsync(tenantId, email, user.Id, mb?.Id, ct);
                var domain = await FindDomainForEmailAsync(tenantId, email, ct);
                if (domain is null)
                {
                    throw new InvalidOperationException("A verified tenant domain is required for this email address.");
                }

                user.Email = email;
                if (mb is not null)
                {
                    mb.Address = email;
                    mb.DomainId = domain.Id;
                    mb.UpdatedAt = now;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            user.Name = request.Name.Trim();
        }

        if (request.IsActive.HasValue)
        {
            if (!request.IsActive.Value)
            {
                _auth.ValidateOwnerDemotion(tenantId, targetUserId);
            }
            user.IsActive = request.IsActive.Value;
        }

        if (request.IsService.HasValue)
        {
            user.IsService = request.IsService.Value;
        }

        user.UpdatedAt = now;

        if (mb != null && request.QuotaBytes.HasValue)
        {
            mb.QuotaBytes = request.QuotaBytes.Value;
            mb.UpdatedAt = now;
        }

        var membership = await _db.Memberships.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == user.Id, ct);
        Role? selectedRole = null;
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleCode = request.Role.Trim().ToLowerInvariant();
            selectedRole = await _db.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code.ToLower() == roleCode, ct);
            if (selectedRole is null)
            {
                throw new InvalidOperationException($"Role '{roleCode}' was not found.");
            }

            if (!string.Equals(selectedRole.Code, "owner", StringComparison.OrdinalIgnoreCase))
            {
                _auth.ValidateOwnerDemotion(tenantId, targetUserId);
            }

            if (membership != null)
            {
                membership.RoleId = selectedRole.Id;
                membership.UpdatedAt = now;
            }
            else
            {
                membership = new Membership
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UserId = user.Id,
                    RoleId = selectedRole.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Memberships.Add(membership);
            }
        }

        if (request.MustChangePassword.HasValue)
        {
            var credential = await GetOrCreateCredentialAsync(tenantId, user.Id, null, request.MustChangePassword.Value, now, ct);
            credential.MustChangePassword = request.MustChangePassword.Value;
            credential.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return await BuildAdminUserDtoAsync(tenantId, user, ct);
    }

    public async Task<ResetPasswordResult?> ResetPasswordAsync(Guid tenantId, Guid userId, Guid targetUserId, ResetPasswordRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == targetUserId, ct);

        if (user is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, user, "user.manage");

        var generatedPassword = string.IsNullOrWhiteSpace(request.Password) || request.GeneratePassword
            ? GeneratePassword()
            : null;
        var password = generatedPassword ?? request.Password!;
        var now = DateTimeOffset.UtcNow;
        var credential = await GetOrCreateCredentialAsync(tenantId, user.Id, password, request.MustChangePassword, now, ct);
        credential.PasswordHash = _passwordHasher.HashPassword(password);
        credential.Algorithm = "argon2id";
        credential.MustChangePassword = request.MustChangePassword;
        credential.UpdatedAt = now;
        user.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return new ResetPasswordResult(generatedPassword);
    }

    public async Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, Guid targetUserId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == targetUserId, ct);

        if (user is null) return false;

        _auth.AuthorizeAccess(tenantId, userId, user, "user.manage");
        _auth.ValidateOwnerDemotion(tenantId, targetUserId);

        var memberships = await _db.Memberships
            .Where(m => m.TenantId == tenantId && m.UserId == targetUserId)
            .ToListAsync(ct);
        var credentials = await _db.UserCredentials
            .Where(c => c.TenantId == tenantId && c.UserId == targetUserId)
            .ToListAsync(ct);

        _db.Memberships.RemoveRange(memberships);
        _db.UserCredentials.RemoveRange(credentials);
        _db.Users.Remove(user);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Orphan mailboxes
    // -------------------------------------------------------------
    public async Task<IReadOnlyList<OrphanMailboxDto>> ListOrphanMailboxesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.view");

        var mailboxes = await _db.Mailboxes
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(ct);

        if (mailboxes.Count == 0)
        {
            return Array.Empty<OrphanMailboxDto>();
        }

        var mailboxIds = mailboxes.Select(m => m.Id).ToArray();
        var activeUsers = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .ToListAsync(ct);
        var activeUserIds = activeUsers.Select(u => u.Id).ToHashSet();
        var activeEmails = activeUsers
            .Select(u => u.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var delegateMailboxIds = activeUserIds.Count == 0
            ? new HashSet<Guid>()
            : await _db.MailboxDelegates
                .Where(d => d.TenantId == tenantId && activeUserIds.Contains(d.UserId))
                .Select(d => d.MailboxId)
                .Distinct()
                .ToHashSetAsync(ct);

        var messageCounts = await _db.Messages
            .Where(m => m.TenantId == tenantId && mailboxIds.Contains(m.MailboxId))
            .GroupBy(m => m.MailboxId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var attachmentCounts = await _db.Attachments
            .Where(a => a.TenantId == tenantId)
            .Join(
                _db.Messages.Where(m => m.TenantId == tenantId),
                a => a.MessageId,
                m => m.Id,
                (a, m) => new { m.MailboxId })
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .GroupBy(x => x.MailboxId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var result = new List<OrphanMailboxDto>(mailboxes.Count);
        foreach (var mailbox in mailboxes.OrderBy(m => m.Address))
        {
            var isShared = string.Equals(mailbox.Kind, "shared", StringComparison.OrdinalIgnoreCase);
            var isOrphan = isShared
                ? !delegateMailboxIds.Contains(mailbox.Id)
                : !activeEmails.Contains(mailbox.Address);

            if (!isOrphan)
            {
                continue;
            }

            result.Add(new OrphanMailboxDto(
                mailbox.Id,
                mailbox.Address,
                mailbox.Name,
                mailbox.Kind,
                mailbox.QuotaBytes,
                mailbox.UsedBytes,
                mailbox.IsActive,
                mailbox.CreatedAt,
                messageCounts.GetValueOrDefault(mailbox.Id),
                attachmentCounts.GetValueOrDefault(mailbox.Id)));
        }

        return result;
    }

    public async Task<SharedMailboxDto> AssignMailboxAsync(Guid tenantId, Guid userId, Guid mailboxId, AssignMailboxRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");

        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, ct);
        mailbox = _auth.AuthorizeAccess(tenantId, userId, mailbox, "user.manage");

        var email = NormalizeEmail(request.Address);
        var name = string.IsNullOrWhiteSpace(request.Name) ? mailbox.Name : NormalizeDisplayName(request.Name);
        var quotaBytes = ValidateQuotaBytes(request.QuotaBytes);

        if (!string.Equals(email, mailbox.Address, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureEmailAvailableAsync(tenantId, email, null, mailbox.Id, ct);
        }

        var domain = await FindDomainForEmailAsync(tenantId, email, ct);
        if (domain is null)
        {
            throw new InvalidOperationException("A verified tenant domain is required for this email address.");
        }

        var delegates = request.Delegates ?? Array.Empty<MailboxDelegateRequest>();
        if (delegates.Count == 0)
        {
            throw new InvalidOperationException("A shared mailbox requires at least one delegate.");
        }

        var now = DateTimeOffset.UtcNow;
        mailbox.Address = email;
        mailbox.DomainId = domain.Id;
        mailbox.Name = name;
        mailbox.Kind = "shared";
        mailbox.QuotaBytes = quotaBytes;
        mailbox.UpdatedAt = now;

        await ReplaceMailboxDelegatesAsync(tenantId, mailbox, delegates, now, ct);
        await _db.SaveChangesAsync(ct);

        return await BuildSharedMailboxDtoAsync(tenantId, mailbox, ct);
    }

    public async Task<DeleteMailboxResult> DeleteMailboxAsync(Guid tenantId, Guid userId, Guid mailboxId, DeleteMailboxRequest request, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "mailbox.delete");

        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, ct);
        mailbox = _auth.AuthorizeAccess(tenantId, userId, mailbox, "mailbox.delete");

        if (!string.Equals(request.ConfirmAddress?.Trim(), mailbox.Address, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Confirmation address does not match the mailbox address.");
        }

        var messages = await _db.Messages
            .Where(m => m.TenantId == tenantId && m.MailboxId == mailboxId)
            .ToListAsync(ct);
        var messageIds = messages.Select(m => m.Id).ToArray();
        var attachments = messageIds.Length == 0
            ? []
            : await _db.Attachments
                .Where(a => a.TenantId == tenantId && messageIds.Contains(a.MessageId))
                .ToListAsync(ct);

        var contentHashes = messages
            .Select(m => m.ContentHash)
            .Concat(attachments.Select(a => a.ContentHash))
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var recipients = messageIds.Length == 0
            ? []
            : await _db.MessageRecipients
                .Where(r => r.TenantId == tenantId && messageIds.Contains(r.MessageId))
                .ToListAsync(ct);
        var flags = messageIds.Length == 0
            ? []
            : await _db.MessageFlags
                .Where(f => f.TenantId == tenantId && messageIds.Contains(f.MessageId))
                .ToListAsync(ct);
        var folders = await _db.Folders
            .Where(f => f.TenantId == tenantId && f.MailboxId == mailboxId)
            .ToListAsync(ct);
        var mailboxDelegates = await _db.MailboxDelegates
            .Where(d => d.TenantId == tenantId && d.MailboxId == mailboxId)
            .ToListAsync(ct);
        var sieveScripts = await _db.SieveScripts
            .Where(s => s.TenantId == tenantId && s.MailboxId == mailboxId)
            .ToListAsync(ct);

        var messagesDeleted = messages.Count;
        var attachmentsDeleted = attachments.Count;

        _db.MessageRecipients.RemoveRange(recipients);
        _db.MessageFlags.RemoveRange(flags);
        _db.Attachments.RemoveRange(attachments);
        _db.Messages.RemoveRange(messages);
        _db.Folders.RemoveRange(folders);
        _db.MailboxDelegates.RemoveRange(mailboxDelegates);
        _db.SieveScripts.RemoveRange(sieveScripts);
        _db.Mailboxes.Remove(mailbox);

        await _db.SaveChangesAsync(ct);

        var blobsDeleted = await _mailboxArchiveService.DeleteBlobsIfUnreferencedAsync(_db, contentHashes, ct);

        return new DeleteMailboxResult(messagesDeleted, attachmentsDeleted, blobsDeleted);
    }

    public async Task<MailboxArchiveDto> ExportMailboxAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken ct = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, ct);

        if (mailbox is null)
        {
            throw new ResourceNotFoundException();
        }

        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: false);

        var tempFilePath = await _mailboxArchiveService.CreateArchiveAsync(_db, tenantId, mailboxId, mailbox.Address, ct);
        var safeAddress = mailbox.Address.Replace('@', '-').Replace('.', '-');
        var fileName = $"mailbox-export-{safeAddress}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.zip";

        return new MailboxArchiveDto(tempFilePath, fileName);
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
        // Retry UI currently maps to Failed/Retrying bucket; in the real model we use Status="Failed"
        var retrying = await _db.SmtpQueue.CountAsync(q => q.TenantId == tenantId && q.Status == "Failed", ct);
        var deadLetters = await _db.SmtpQueue.CountAsync(q => q.TenantId == tenantId && q.Status == "DeadLetter", ct);

        var since24h = DateTimeOffset.UtcNow.AddHours(-24);

        // Delivered24h (existing card): mailbox-level delivery volume.
        var delivered = await _db.Messages.CountAsync(m => m.TenantId == tenantId && m.CreatedAt >= since24h, ct);

        var quarantined = await _db.Quarantine.CountAsync(q => q.TenantId == tenantId && q.QuarantinedAt >= since24h, ct);
        var spamBlocked = await _db.SpamVerdicts.CountAsync(s => s.TenantId == tenantId && s.IsSpam && s.CreatedAt >= since24h, ct);

        // Outbound transport metrics (queue/attempt based)
        var outboundDeliveredTotal = await _db.SmtpQueue.CountAsync(q => q.TenantId == tenantId && q.Status == "Delivered", ct);

        var delivered24hOutbound = await _db.SmtpQueue
            .CountAsync(q => q.TenantId == tenantId && q.Status == "Delivered" && q.LastAttemptAt >= since24h, ct);

        var attempts24hBase = _db.SmtpDeliveryAttempts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.AttemptedAt >= since24h);

        var successfulAttempts24h = await attempts24hBase.CountAsync(a => a.Success, ct);
        var failedAttempts24h = await attempts24hBase.CountAsync(a => !a.Success, ct);
        var totalAttempts24h = successfulAttempts24h + failedAttempts24h;

        var deliverySuccessRate24h = totalAttempts24h == 0
            ? 100d
            : Math.Round(successfulAttempts24h * 100d / totalAttempts24h, 1);

        var retryAttempts24h = await attempts24hBase.CountAsync(a =>
            !a.Success && a.NextRetryDelaySeconds.HasValue && a.NextRetryDelaySeconds.Value > 0, ct);

        var lastAttempt = await _db.SmtpDeliveryAttempts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.AttemptedAt)
            .FirstOrDefaultAsync(ct);

        var lastDeliveryAttemptAt = lastAttempt?.AttemptedAt;
        var lastDeliveryResponseCode = lastAttempt?.ResponseCode;
        var lastDeliveryError = lastAttempt?.ErrorMessage;

        // Transport breakdown (All/Local/Cloudflare/Unknown)
        // Join smtp_queue -> domains via the recipient domain; if unknown, it falls back to "unknown".
        // Note: this is intentionally best-effort and does not impact combined totals.
        var transportBreakdown = await GetOutboundTransportBreakdownAsync(tenantId, since24h, ct);

        var tenantCount = await _db.Tenants.CountAsync(ct);
        var canConnect = await _db.Database.CanConnectAsync(ct);
        var uptimeSeconds = (long)(DateTimeOffset.UtcNow - ProcessStartedAt).TotalSeconds;

        // First element should represent combined totals; UI can also use breakdown for filters.
        var allMetric = transportBreakdown.FirstOrDefault(t => t.TransportMode == "all")
            ?? new TransportDashboardMetricDto(
                TransportMode: "all",
                ActiveQueued: queued,
                Retrying: retrying,
                DeadLetters: deadLetters,
                OutboundDelivered24h: (int)delivered24hOutbound,
                OutboundDeliveredTotal: (int)outboundDeliveredTotal,
                SuccessfulAttempts24h: (int)successfulAttempts24h,
                FailedAttempts24h: (int)failedAttempts24h,
                TotalAttempts24h: (int)totalAttempts24h,
                RetryAttempts24h: (int)retryAttempts24h,
                DeliverySuccessRate24h: deliverySuccessRate24h,
                LastDeliveryAttemptAt: lastDeliveryAttemptAt,
                LastDeliveryResponseCode: lastDeliveryResponseCode,
                LastDeliveryError: lastDeliveryError);

        return new DashboardSummaryDto(
            ActiveQueued: queued,
            Retrying: retrying,
            DeadLetters: deadLetters,
            Delivered24h: delivered,
            Quarantined24h: quarantined,
            SpamBlocked24h: spamBlocked,
            SystemHealth: canConnect ? "Optimal" : "Degraded",
            UptimeSeconds: uptimeSeconds,
            TenantCount: tenantCount,
            TransportBreakdown: transportBreakdown.Count == 0
                ? new List<TransportDashboardMetricDto> { allMetric }
                : new List<TransportDashboardMetricDto> { allMetric }.Concat(transportBreakdown).ToList());

    }

    private async Task<IReadOnlyList<TransportDashboardMetricDto>> GetOutboundTransportBreakdownAsync(Guid tenantId, DateTimeOffset since24h, CancellationToken ct)
    {
        // Combined “all” metric from precomputed counts is handled by caller.
        // Here we only compute local/cloudflare/unknown counters and return them.

        // Note: calculating “ActiveQueued/Retrying/DeadLetters” per transport needs smtp_queue joined to
        // domains, but the recipient domain is derived from smtp_queue.Recipient.
        // For simplicity and robustness, we compute transport breakdown only from delivery attempts
        // (success/failure, retries, response codes) and queue transitions (delivered/failed/deadletter)
        // via best-effort domain matching.

        var smtpRows = await _db.SmtpQueue
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId)
            .Select(q => new { q.Id, q.Status, q.LastAttemptAt, Recipient = q.Recipient })
            .ToListAsync(ct);

        // Load domain transport modes for recipients.
        var domains = await _db.Domains
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new { d.Name, d.TransportMode })
            .ToListAsync(ct);

        string ResolveTransport(string recipient)
        {
            var at = recipient.LastIndexOf('@');
            if (at < 0 || at == recipient.Length - 1) return "unknown";
            var rcptDomain = recipient[(at + 1)..].Trim().ToLowerInvariant();
            var match = domains.FirstOrDefault(d => d.Name.ToLower() == rcptDomain);
            return match?.TransportMode ?? "unknown";
        }

        var breakdown = new Dictionary<string, TransportDashboardMetricDto>(StringComparer.OrdinalIgnoreCase);

        // Initialize known buckets.
        foreach (var mode in new[] { "local", "cloudflare", "unknown" })
        {
            breakdown[mode] = new TransportDashboardMetricDto(
                TransportMode: mode,
                ActiveQueued: 0,
                Retrying: 0,
                DeadLetters: 0,
                OutboundDelivered24h: 0,
                OutboundDeliveredTotal: 0,
                SuccessfulAttempts24h: 0,
                FailedAttempts24h: 0,
                TotalAttempts24h: 0,
                RetryAttempts24h: 0,
                DeliverySuccessRate24h: 100d,
                LastDeliveryAttemptAt: null,
                LastDeliveryResponseCode: null,
                LastDeliveryError: null);
        }

        // Queue transition buckets.
        foreach (var row in smtpRows)
        {
            var mode = ResolveTransport(row.Recipient);
            var metric = breakdown[mode];

            if (row.Status == "Pending") metric = metric with { ActiveQueued = metric.ActiveQueued + 1 };
            if (row.Status == "Failed") metric = metric with { Retrying = metric.Retrying + 1 };
            if (row.Status == "DeadLetter") metric = metric with { DeadLetters = metric.DeadLetters + 1 };
            if (row.Status == "Delivered")
            {
                metric = metric with
                {
                    OutboundDeliveredTotal = metric.OutboundDeliveredTotal + 1,
                    OutboundDelivered24h = metric.OutboundDelivered24h + (row.LastAttemptAt >= since24h ? 1 : 0)
                };
            }

            breakdown[mode] = metric;
        }

        // Attempt buckets.
        var attempts = await _db.SmtpDeliveryAttempts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.AttemptedAt >= since24h)
            .Select(a => new
            {
                a.QueueItemId,
                a.Success,
                a.ResponseCode,
                a.ErrorMessage,
                a.AttemptedAt,
                a.NextRetryDelaySeconds
            })
            .ToListAsync(ct);

        // Map queue item id -> resolved transport from smtp_queue rows loaded above.
        var queueIdToTransport = smtpRows.ToDictionary(
            q => q.Id,
            q => ResolveTransport(q.Recipient));

        foreach (var a in attempts)
        {
            var mode = queueIdToTransport.TryGetValue(a.QueueItemId, out var m) ? m : "unknown";
            if (!breakdown.TryGetValue(mode, out var metric)) continue;

            metric = metric with
            {
                SuccessfulAttempts24h = metric.SuccessfulAttempts24h + (a.Success ? 1 : 0),
                FailedAttempts24h = metric.FailedAttempts24h + (!a.Success ? 1 : 0),
                TotalAttempts24h = metric.TotalAttempts24h + 1,
                RetryAttempts24h = metric.RetryAttempts24h + (!a.Success && a.NextRetryDelaySeconds.HasValue && a.NextRetryDelaySeconds.Value > 0 ? 1 : 0),
                LastDeliveryAttemptAt = metric.LastDeliveryAttemptAt == null || a.AttemptedAt > metric.LastDeliveryAttemptAt ? a.AttemptedAt : metric.LastDeliveryAttemptAt,
                LastDeliveryResponseCode = metric.LastDeliveryAttemptAt == null || a.AttemptedAt > metric.LastDeliveryAttemptAt ? a.ResponseCode : metric.LastDeliveryResponseCode,
                LastDeliveryError = metric.LastDeliveryAttemptAt == null || a.AttemptedAt > metric.LastDeliveryAttemptAt ? a.ErrorMessage : metric.LastDeliveryError,
            };

            breakdown[mode] = metric;
        }

        // Finalize success rates.
        var result = new List<TransportDashboardMetricDto>();
        foreach (var mode in new[] { "local", "cloudflare", "unknown" })
        {
            var metric = breakdown[mode];
            metric = metric with
            {
                DeliverySuccessRate24h = metric.TotalAttempts24h == 0
                    ? 100d
                    : Math.Round(metric.SuccessfulAttempts24h * 100d / metric.TotalAttempts24h, 1)
            };
            result.Add(metric);
        }

        // Optionally include an “all” bucket; caller may compute it.
        return result;
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

    public async Task<IReadOnlyList<SharedMailboxDto>> ListSharedMailboxesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.view");
        var mailboxes = await _db.Mailboxes
            .Where(m => m.TenantId == tenantId && m.Kind == "shared")
            .OrderBy(m => m.Address)
            .ToListAsync(ct);

        var result = new List<SharedMailboxDto>(mailboxes.Count);
        foreach (var mailbox in mailboxes)
        {
            _auth.AuthorizeAccess(tenantId, userId, mailbox, "user.view");
            result.Add(await BuildSharedMailboxDtoAsync(tenantId, mailbox, ct));
        }

        return result;
    }

    public async Task<SharedMailboxDto?> GetSharedMailboxAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.view");
        var mailbox = await _db.Mailboxes.FirstOrDefaultAsync(
            m => m.TenantId == tenantId && m.Id == mailboxId && m.Kind == "shared", ct);
        if (mailbox is null) return null;

        _auth.AuthorizeAccess(tenantId, userId, mailbox, "user.view");
        return await BuildSharedMailboxDtoAsync(tenantId, mailbox, ct);
    }

    public async Task<SharedMailboxDto> UpdateSharedMailboxDelegatesAsync(Guid tenantId, Guid userId, Guid mailboxId, IReadOnlyList<MailboxDelegateRequest> delegates, CancellationToken ct = default)
    {
        _auth.AssertPermission(tenantId, userId, "user.manage");
        var mailbox = await _db.Mailboxes.FirstOrDefaultAsync(
            m => m.TenantId == tenantId && m.Id == mailboxId, ct);
        if (mailbox is null || mailbox.Kind != "shared") throw new ResourceNotFoundException();

        _auth.AuthorizeAccess(tenantId, userId, mailbox, "user.manage");
        await ReplaceMailboxDelegatesAsync(tenantId, mailbox, delegates, DateTimeOffset.UtcNow, ct);
        await _db.SaveChangesAsync(ct);
        return await BuildSharedMailboxDtoAsync(tenantId, mailbox, ct);
    }

    private async Task<SharedMailboxDto> BuildSharedMailboxDtoAsync(Guid tenantId, Mailbox mailbox, CancellationToken ct)
    {
        var assignments = await _db.MailboxDelegates
            .Where(d => d.TenantId == tenantId && d.MailboxId == mailbox.Id)
            .ToListAsync(ct);
        var ids = assignments.Select(d => d.UserId).ToArray();
        var users = await _db.Users.Where(u => u.TenantId == tenantId && ids.Contains(u.Id)).ToListAsync(ct);
        var usersById = users.ToDictionary(user => user.Id);
        var delegates = assignments
            .OrderBy(assignment => assignment.CreatedAt)
            .Where(assignment => usersById.ContainsKey(assignment.UserId))
            .Select(assignment =>
            {
                var user = usersById[assignment.UserId];
                return new MailboxDelegateDto(user.Id, user.Email, user.Name, assignment.AccessLevel);
            })
            .ToList();

        return new SharedMailboxDto(mailbox.Id, mailbox.Address, mailbox.Name, mailbox.QuotaBytes,
            mailbox.UsedBytes, mailbox.IsActive, mailbox.CreatedAt, delegates);
    }

    private async Task ReplaceMailboxDelegatesAsync(Guid tenantId, Mailbox mailbox, IReadOnlyList<MailboxDelegateRequest> requests, DateTimeOffset now, CancellationToken ct)
    {
        var normalized = requests.Select(request =>
        {
            var level = request.AccessLevel.Trim().ToLowerInvariant();
            if (level is not ("read" or "write")) throw new InvalidOperationException("Delegate access level must be 'read' or 'write'.");
            return new MailboxDelegateRequest(request.UserId, level);
        }).ToList();
        if (normalized.Select(d => d.UserId).Distinct().Count() != normalized.Count)
            throw new InvalidOperationException("A delegate may only be assigned once.");

        var ids = normalized.Select(d => d.UserId).ToArray();
        var users = await _db.Users.Where(u => u.TenantId == tenantId && ids.Contains(u.Id)).ToListAsync(ct);
        var domainName = (await _db.Domains.FirstAsync(d => d.TenantId == tenantId && d.Id == mailbox.DomainId, ct)).Name;
        foreach (var request in normalized)
        {
            var user = users.FirstOrDefault(u => u.Id == request.UserId);
            if (user is null || !user.IsActive || !string.Equals(user.Email[(user.Email.LastIndexOf('@') + 1)..], domainName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Delegates must be active users in the shared mailbox domain.");
        }

        var existing = await _db.MailboxDelegates.Where(d => d.TenantId == tenantId && d.MailboxId == mailbox.Id).ToListAsync(ct);
        _db.MailboxDelegates.RemoveRange(existing);
        _db.MailboxDelegates.AddRange(normalized.Select(d => new MailboxDelegate
        {
            Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailbox.Id, UserId = d.UserId,
            AccessLevel = d.AccessLevel, CreatedAt = now, UpdatedAt = now
        }));
    }

    private async Task<AdminUserDto> BuildAdminUserDtoAsync(Guid tenantId, User user, CancellationToken ct)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Address.ToLower() == user.Email.ToLower(), ct);
        var membership = await _db.Memberships
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == user.Id, ct);
        var roles = await _db.Roles
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(ct);
        var credential = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.UserId == user.Id, ct);

        return ToAdminUserDto(user, mailbox, membership, roles, credential);
    }

    private static AdminUserDto ToAdminUserDto(
        User user,
        Mailbox? mailbox,
        Membership? membership,
        IReadOnlyCollection<Role> roles,
        UserCredential? credential)
    {
        var role = roles.FirstOrDefault(r => r.Id == membership?.RoleId)?.Code ?? "member";
        return ToAdminUserDto(user, mailbox, role, credential?.MustChangePassword ?? false);
    }

    private static AdminUserDto ToAdminUserDto(User user, Mailbox? mailbox, string role, bool mustChangePassword) => new(
        user.Id,
        user.Email,
        user.Name,
        user.IsActive,
        role,
        mailbox?.QuotaBytes ?? 0,
        mailbox?.UsedBytes ?? 0,
        user.CreatedAt,
        mustChangePassword,
        user.IsService,
        mailbox?.Kind ?? "user");

    private static string NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A valid email address is required.");
        }

        return normalized;
    }

    private static string NormalizeMailboxKind(string? mailboxKind)
    {
        var normalized = string.IsNullOrWhiteSpace(mailboxKind)
            ? "user"
            : mailboxKind.Trim().ToLowerInvariant();

        if (normalized is not ("user" or "shared"))
        {
            throw new InvalidOperationException("Mailbox kind must be 'user' or 'shared'.");
        }

        return normalized;
    }

    private static string NormalizeDisplayName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("A mailbox name is required.");
        }

        return normalized;
    }

    private static long ValidateQuotaBytes(long? quotaBytes)
    {
        var quota = quotaBytes ?? 10737418240L;
        if (quota <= 0)
        {
            throw new InvalidOperationException("Mailbox quota must be greater than zero.");
        }

        return quota;
    }

    private async Task EnsureEmailAvailableAsync(Guid tenantId, string email, Guid? excludedUserId, Guid? excludedMailboxId, CancellationToken ct)
    {
        var userExists = await _db.Users.AnyAsync(u =>
            u.TenantId == tenantId &&
            u.Email.ToLower() == email.ToLower() &&
            (!excludedUserId.HasValue || u.Id != excludedUserId.Value), ct);
        var mailboxExists = await _db.Mailboxes.AnyAsync(m =>
            m.TenantId == tenantId &&
            m.Address.ToLower() == email.ToLower() &&
            (!excludedMailboxId.HasValue || m.Id != excludedMailboxId.Value), ct);

        if (userExists || mailboxExists)
        {
            throw new InvalidOperationException("Email address is already in use.");
        }
    }

    private async Task<DomainEntity?> FindDomainForEmailAsync(Guid tenantId, string email, CancellationToken ct)
    {
        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1)
        {
            return null;
        }

        var domainName = email[(at + 1)..].ToLowerInvariant();
        return await _db.Domains.FirstOrDefaultAsync(d =>
            d.TenantId == tenantId &&
            d.IsVerified &&
            d.Name.ToLower() == domainName, ct);
    }

    private async Task<UserCredential> GetOrCreateCredentialAsync(
        Guid tenantId,
        Guid targetUserId,
        string? password,
        bool mustChangePassword,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var credential = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.UserId == targetUserId, ct);

        if (credential is not null)
        {
            return credential;
        }

        credential = new UserCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = targetUserId,
            PasswordHash = string.IsNullOrEmpty(password) ? _passwordHasher.HashPassword(GeneratePassword()) : _passwordHasher.HashPassword(password),
            Algorithm = "argon2id",
            MustChangePassword = mustChangePassword,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.UserCredentials.Add(credential);
        return credential;
    }

    private static string GeneratePassword()
    {
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@#$%*-_+?";
        const string all = lower + upper + digits + symbols;

        var chars = new char[20];
        chars[0] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[1] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

        for (var i = 4; i < chars.Length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle<char>(chars);
        return new string(chars);
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
        d.CreatedAt,
        d.TransportMode,
        d.CloudflareZoneId,
        d.CloudflareWorkerUrl);
}
