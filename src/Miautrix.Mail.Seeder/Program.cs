using System.Security.Cryptography;
using System.Text;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.Seeder;

public static class Program
{
    private static readonly string[] PermissionCatalogue =
    [
        "mailbox.create",
        "mailbox.read",
        "mailbox.update",
        "mailbox.delete",
        "queue.retry",
        "queue.view",
        "queue.purge",
        "domain.add",
        "domain.delete",
        "domain.verify",
        "domain.view",
        "domain.manage",
        "user.invite",
        "user.manage",
        "user.view",
        "audit.read",
        "audit.view",
        "licence.manage",
        "rule.manage",
        "rule.view",
        "sieve.edit",
        "antispam.manage",
        "backup.create",
        "backup.restore",
        "system.view",
        "system.configure",
        "quarantine.view",
        "quarantine.manage"
    ];

    private static readonly string[] SystemRoles =
    [
        "owner",
        "admin",
        "operator",
        "member"
    ];

    /// <summary>
    /// Permissions held by each non-owner system role. The owner is seeded with the whole
    /// catalogue; every other role is listed explicitly so that adding a permission to the
    /// catalogue never hands it to every role by accident.
    /// </summary>
    /// <remarks>
    /// <c>mailbox.read</c> is load-bearing far beyond reading mail: every calendar endpoint
    /// asserts it before doing anything else, and <c>mailbox.update</c> is what authorizes
    /// sending. A role holding neither can sign in and then do nothing — its own mail still
    /// opens, because owning the mailbox satisfies <c>AssertMailboxAccess</c> on its own,
    /// while the entire calendar answers 404.
    /// </remarks>
    private static readonly Dictionary<string, string[]> RolePermissionGrants = new(StringComparer.Ordinal)
    {
        ["admin"] =
        [
            "mailbox.read",
            "mailbox.update",
            "mailbox.create",
            "mailbox.delete",
            "user.view",
            "user.invite",
            "user.manage",
            "domain.view",
            "queue.view",
            "queue.retry",
            "audit.view",
            "quarantine.view",
            "quarantine.manage",
            "rule.view",
            "rule.manage",
            "sieve.edit",
            "system.view"
        ],
        ["operator"] =
        [
            "mailbox.read",
            "mailbox.update",
            "domain.view",
            "queue.view",
            "queue.retry",
            "audit.view",
            "quarantine.view",
            "rule.view",
            "sieve.edit",
            "system.view"
        ],
        ["member"] =
        [
            "mailbox.read",
            "mailbox.update",
            "rule.view",
            "sieve.edit"
        ]
    };

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("[Miautrix.Mail.Seeder] Starting database seeder...");

        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            conn = "Host=localhost;Database=miautrix_dev;Username=postgres;Password=postgres";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;

        using var db = new AppDbContext(options);

        // --permissions-only reconciles roles and permissions for tenants that already exist
        // and touches nothing else. Use it against a live database: a full run would also
        // create the default tenant, its domain, and the bootstrap admin account with its
        // well-known initial password, none of which reconciling a missing grant should do
        // to production.
        if (args.Contains("--permissions-only", StringComparer.OrdinalIgnoreCase))
        {
            var existingTenants = await db.Tenants.ToListAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Reconciling roles and permissions for {existingTenants.Count} existing tenant(s).");
            foreach (var existingTenant in existingTenants)
            {
                await SeedPermissionsAndRolesAsync(db, existingTenant);
            }

            Console.WriteLine("[Miautrix.Mail.Seeder] Role/permission reconciliation completed successfully.");
            return 0;
        }

        // 1. Seed or retrieve Default Tenant
        var defaultTenantSlug = "default";
        var defaultTenantId = DeterministicGuid(Guid.Empty, $"tenant:{defaultTenantSlug}");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == defaultTenantId || t.Slug == defaultTenantSlug);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = defaultTenantId,
                Slug = defaultTenantSlug,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Created default tenant: {tenant.Slug} ({tenant.Id})");
        }
        else
        {
            Console.WriteLine($"[Miautrix.Mail.Seeder] Default tenant exists: {tenant.Slug} ({tenant.Id})");
        }

        // 2. Seed Default Domain (miautrix.org)
        var defaultDomainName = "miautrix.org";
        var domainId = DeterministicGuid(tenant.Id, $"domain:{defaultDomainName}");
        var domain = await db.Domains.FirstOrDefaultAsync(d => d.TenantId == tenant.Id && d.Name == defaultDomainName);
        if (domain is null)
        {
            domain = new DomainEntity
            {
                Id = domainId,
                TenantId = tenant.Id,
                Name = defaultDomainName,
                IsVerified = true,
                DkimSelector = "default",
                SpfRecord = "v=spf1 mx ~all",
                DmarcRecord = "v=DMARC1; p=quarantine; pct=100;",
                IsPrimary = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Domains.Add(domain);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded default domain: {defaultDomainName} ({domain.Id})");
        }
        else
        {
            Console.WriteLine($"[Miautrix.Mail.Seeder] Default domain exists: {defaultDomainName} ({domain.Id})");
        }

        // 3-5. Roles and permissions for EVERY tenant, not just the default one.
        //
        // Seeding only the default tenant left every other tenant with roles holding no
        // permissions at all. Signing in still worked, and a user's own mail still opened —
        // owning the mailbox satisfies AssertMailboxAccess on its own — but every calendar
        // endpoint answers 404, because AssertPermission("mailbox.read") has no such
        // fallback. A tenant is therefore only usable once this has run against it.
        var tenants = await db.Tenants.ToListAsync();
        foreach (var seededTenant in tenants)
        {
            await SeedPermissionsAndRolesAsync(db, seededTenant);
        }

        // The bootstrap admin keeps its owner membership; its role id is derived exactly the
        // way SeedPermissionsAndRolesAsync derived it above.
        var ownerRoleId = DeterministicGuid(tenant.Id, "role:owner");

        // 6. Seed Default Admin User (admin@miautrix.org)
        var adminEmail = "admin@miautrix.org";
        var firstUserId = DeterministicGuid(tenant.Id, $"user:{adminEmail}");
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.TenantId == tenant.Id && (u.Id == firstUserId || u.Email == adminEmail));
        if (existingUser is null)
        {
            existingUser = new User
            {
                Id = firstUserId,
                TenantId = tenant.Id,
                Email = adminEmail,
                Name = "System Administrator",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(existingUser);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded default admin user {adminEmail} ({firstUserId}).");
        }
        else
        {
            existingUser.Email = adminEmail;
            existingUser.Name = "System Administrator";
            existingUser.IsActive = true;
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Default admin user exists: {adminEmail} ({existingUser.Id}).");
        }

        // 7. Seed Admin User Credential (Argon2id Hash of "CH@nGEm3!", MustChangePassword = true)
        var passwordHasher = new Argon2idPasswordHasher();
        var defaultPassword = "CH@nGEm3!";
        var defaultPasswordHash = passwordHasher.HashPassword(defaultPassword);

        var credentialId = DeterministicGuid(tenant.Id, $"credential:{existingUser.Id}");
        var existingCredential = await db.UserCredentials.FirstOrDefaultAsync(c => c.TenantId == tenant.Id && c.UserId == existingUser.Id);
        if (existingCredential is null)
        {
            db.UserCredentials.Add(new UserCredential
            {
                Id = credentialId,
                TenantId = tenant.Id,
                UserId = existingUser.Id,
                PasswordHash = defaultPasswordHash,
                Algorithm = "argon2id",
                MustChangePassword = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded default admin credential (MustChangePassword=true).");
        }
        else
        {
            Console.WriteLine($"[Miautrix.Mail.Seeder] Default admin credential already exists.");
        }

        // 8. Seed Owner Membership
        var membershipId = DeterministicGuid(tenant.Id, $"membership:{existingUser.Id}:{ownerRoleId}");
        var existingMembership = await db.Memberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.TenantId == tenant.Id);
        if (existingMembership is null)
        {
            db.Memberships.Add(new Membership
            {
                Id = membershipId,
                TenantId = tenant.Id,
                UserId = existingUser.Id,
                RoleId = ownerRoleId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded owner membership ({membershipId}).");
        }

        // 9. Seed Default Mailbox for Admin (admin@miautrix.org)
        var mailboxId = DeterministicGuid(tenant.Id, $"mailbox:{adminEmail}");
        var mailbox = await db.Mailboxes.FirstOrDefaultAsync(m => m.TenantId == tenant.Id && m.Address == adminEmail);
        if (mailbox is null)
        {
            mailbox = new Mailbox
            {
                Id = mailboxId,
                TenantId = tenant.Id,
                DomainId = domain.Id,
                Address = adminEmail,
                QuotaBytes = 10L * 1024 * 1024 * 1024,
                UsedBytes = 0,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Mailboxes.Add(mailbox);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded default mailbox: {adminEmail} ({mailbox.Id})");
        }

        // 10. Seed Default System Folders for Mailbox
        var standardFolders = new (string Name, string Role)[]
        {
            ("INBOX", "inbox"),
            ("Sent", "sent"),
            ("Drafts", "drafts"),
            ("Trash", "trash"),
            ("Junk", "junk"),
            ("Archive", "archive")
        };

        foreach (var (folderName, folderRole) in standardFolders)
        {
            var folderId = DeterministicGuid(tenant.Id, $"folder:{mailbox.Id}:{folderRole}");
            var existingFolder = await db.Folders.FirstOrDefaultAsync(f => f.TenantId == tenant.Id && f.MailboxId == mailbox.Id && f.Role == folderRole);
            if (existingFolder is null)
            {
                db.Folders.Add(new Folder
                {
                    Id = folderId,
                    TenantId = tenant.Id,
                    MailboxId = mailbox.Id,
                    Name = folderName,
                    Role = folderRole,
                    UidNext = 1,
                    UidValidity = 1,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded standard mailbox folders.");

        // Final verification counts
        var totalDomains = await db.Domains.CountAsync(d => d.TenantId == tenant.Id);
        var totalPermissions = await db.Permissions.CountAsync(p => p.TenantId == tenant.Id);
        var totalRoles = await db.Roles.CountAsync(r => r.TenantId == tenant.Id);
        var totalUsers = await db.Users.CountAsync(u => u.TenantId == tenant.Id);
        var totalMemberships = await db.Memberships.CountAsync(m => m.TenantId == tenant.Id);
        var totalMailboxes = await db.Mailboxes.CountAsync(m => m.TenantId == tenant.Id);

        Console.WriteLine($"[Miautrix.Mail.Seeder] Summary: {totalDomains} domains, {totalPermissions} permissions, {totalRoles} roles, {totalUsers} users, {totalMemberships} memberships, {totalMailboxes} mailboxes.");
        Console.WriteLine("[Miautrix.Mail.Seeder] Seed completed successfully.");

        return 0;
    }

    /// <summary>
    /// Seeds the permission catalogue, the four system roles, and the role→permission grants
    /// for a single tenant. Idempotent: every row id is derived deterministically from the
    /// tenant id, so re-running adds only what is genuinely missing.
    /// </summary>
    private static async Task SeedPermissionsAndRolesAsync(AppDbContext db, Tenant tenant)
    {
        // Match on Code, never on the deterministic id alone. A tenant seeded by an older
        // build — or by a script — can already hold roles and permissions under different
        // ids. Inventing parallel rows would leave the existing membership bound to a role
        // that still has no permissions, which is the exact defect being repaired.
        var permissionMap = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var existing in await db.Permissions
            .Where(p => p.TenantId == tenant.Id)
            .Select(p => new { p.Id, p.Code })
            .ToListAsync())
        {
            permissionMap.TryAdd(existing.Code, existing.Id);
        }

        var newPermissions = new List<Permission>();
        foreach (var permName in PermissionCatalogue)
        {
            if (permissionMap.ContainsKey(permName))
            {
                continue;
            }

            var permId = DeterministicGuid(tenant.Id, $"permission:{permName}");
            permissionMap[permName] = permId;
            newPermissions.Add(new Permission
            {
                Id = permId,
                TenantId = tenant.Id,
                Code = permName,
                Name = permName,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        if (newPermissions.Count > 0)
        {
            db.Permissions.AddRange(newPermissions);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Tenant {tenant.Slug}: seeded {newPermissions.Count} new permissions.");
        }

        var roleMap = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var existing in await db.Roles
            .Where(r => r.TenantId == tenant.Id)
            .Select(r => new { r.Id, r.Code })
            .ToListAsync())
        {
            roleMap.TryAdd(existing.Code, existing.Id);
        }

        var newRoles = new List<Role>();
        foreach (var roleName in SystemRoles)
        {
            if (roleMap.ContainsKey(roleName))
            {
                continue;
            }

            var roleId = DeterministicGuid(tenant.Id, $"role:{roleName}");
            roleMap[roleName] = roleId;
            newRoles.Add(new Role
            {
                Id = roleId,
                TenantId = tenant.Id,
                Code = roleName,
                Name = roleName,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        if (newRoles.Count > 0)
        {
            db.Roles.AddRange(newRoles);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Tenant {tenant.Slug}: seeded {newRoles.Count} system roles.");
        }

        // Role permissions: owner gets the whole catalogue, the rest their explicit grant.
        var existingRolePerms = (await db.RolePermissions
            .Where(rp => rp.TenantId == tenant.Id)
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync())
            .Select(rp => (RoleId: rp.RoleId, PermissionId: rp.PermissionId))
            .ToHashSet();

        var grants = new Dictionary<string, IEnumerable<string>>(StringComparer.Ordinal)
        {
            ["owner"] = PermissionCatalogue
        };
        foreach (var (roleCode, permissions) in RolePermissionGrants)
        {
            grants[roleCode] = permissions;
        }

        var newRolePermissions = new List<RolePermission>();
        foreach (var (roleCode, permissions) in grants)
        {
            if (!roleMap.TryGetValue(roleCode, out var roleId))
            {
                continue;
            }

            foreach (var permName in permissions)
            {
                if (!permissionMap.TryGetValue(permName, out var permId))
                {
                    // A grant naming a permission outside the catalogue is a coding error, not
                    // a data condition. Throw rather than quietly seeding a weaker role.
                    throw new InvalidOperationException(
                        $"Role '{roleCode}' grants permission '{permName}', which is not in the permission catalogue.");
                }

                if (existingRolePerms.Contains((roleId, permId)))
                {
                    continue;
                }

                newRolePermissions.Add(new RolePermission
                {
                    Id = DeterministicGuid(tenant.Id, $"roleperm:{roleId}:{permId}"),
                    TenantId = tenant.Id,
                    RoleId = roleId,
                    PermissionId = permId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (newRolePermissions.Count > 0)
        {
            db.RolePermissions.AddRange(newRolePermissions);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Tenant {tenant.Slug}: seeded {newRolePermissions.Count} role permissions.");
        }
    }

    private static Guid DeterministicGuid(Guid namespaceId, string value)
    {
        var input = $"{namespaceId}:{value}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);

        // Set version to 5 (or 4) and RFC 4122 variant
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes);
    }
}
