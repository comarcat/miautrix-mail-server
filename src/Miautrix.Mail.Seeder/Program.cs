using System.Security.Cryptography;
using System.Text;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

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
        "user.invite",
        "user.manage",
        "audit.read",
        "licence.manage",
        "rule.manage",
        "sieve.edit",
        "antispam.manage",
        "backup.create",
        "backup.restore",
        "system.view",
        "system.configure"
    ];

    private static readonly string[] SystemRoles =
    [
        "owner",
        "admin",
        "operator",
        "member"
    ];

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

        // 2. Seed Permissions for Tenant
        var existingPermissionIds = (await db.Permissions
            .Where(p => p.TenantId == tenant.Id)
            .Select(p => p.Id)
            .ToListAsync())
            .ToHashSet();

        var newPermissions = new List<Permission>();
        var permissionMap = new Dictionary<string, Guid>();

        foreach (var permName in PermissionCatalogue)
        {
            var permId = DeterministicGuid(tenant.Id, $"permission:{permName}");
            permissionMap[permName] = permId;

            if (!existingPermissionIds.Contains(permId))
            {
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
        }

        if (newPermissions.Count > 0)
        {
            db.Permissions.AddRange(newPermissions);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded {newPermissions.Count} new permissions.");
        }
        else
        {
            Console.WriteLine("[Miautrix.Mail.Seeder] All permissions already present in catalogue.");
        }

        // 3. Seed System Roles for Tenant
        var existingRoleIds = (await db.Roles
            .Where(r => r.TenantId == tenant.Id)
            .Select(r => r.Id)
            .ToListAsync())
            .ToHashSet();

        var newRoles = new List<Role>();
        var roleMap = new Dictionary<string, Guid>();

        foreach (var roleName in SystemRoles)
        {
            var roleId = DeterministicGuid(tenant.Id, $"role:{roleName}");
            roleMap[roleName] = roleId;

            if (!existingRoleIds.Contains(roleId))
            {
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
        }

        if (newRoles.Count > 0)
        {
            db.Roles.AddRange(newRoles);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded {newRoles.Count} system roles.");
        }
        else
        {
            Console.WriteLine("[Miautrix.Mail.Seeder] All system roles already present.");
        }

        // 4. Seed Role Permissions (Owner gets all permissions)
        var ownerRoleId = roleMap["owner"];
        var existingRolePermIds = (await db.RolePermissions
            .Where(rp => rp.TenantId == tenant.Id)
            .Select(rp => rp.Id)
            .ToListAsync())
            .ToHashSet();

        var newRolePermissions = new List<RolePermission>();
        foreach (var (permName, permId) in permissionMap)
        {
            var rolePermId = DeterministicGuid(tenant.Id, $"roleperm:{ownerRoleId}:{permId}");
            if (!existingRolePermIds.Contains(rolePermId))
            {
                newRolePermissions.Add(new RolePermission
                {
                    Id = rolePermId,
                    TenantId = tenant.Id,
                    RoleId = ownerRoleId,
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
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded {newRolePermissions.Count} role permissions for Owner role.");
        }

        // 5. Seed First User and Owner Membership
        var firstUserId = DeterministicGuid(tenant.Id, "user:admin");
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Id == firstUserId && u.TenantId == tenant.Id);
        if (existingUser is null)
        {
            db.Users.Add(new User
            {
                Id = firstUserId,
                TenantId = tenant.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded default admin user ({firstUserId}).");
        }

        var membershipId = DeterministicGuid(tenant.Id, $"membership:{firstUserId}:{ownerRoleId}");
        var existingMembership = await db.Memberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.TenantId == tenant.Id);
        if (existingMembership is null)
        {
            db.Memberships.Add(new Membership
            {
                Id = membershipId,
                TenantId = tenant.Id,
                UserId = firstUserId,
                RoleId = ownerRoleId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            Console.WriteLine($"[Miautrix.Mail.Seeder] Seeded owner membership ({membershipId}).");
        }

        // Final verification counts
        var totalPermissions = await db.Permissions.CountAsync(p => p.TenantId == tenant.Id);
        var totalRoles = await db.Roles.CountAsync(r => r.TenantId == tenant.Id);
        var totalUsers = await db.Users.CountAsync(u => u.TenantId == tenant.Id);
        var totalMemberships = await db.Memberships.CountAsync(m => m.TenantId == tenant.Id);

        Console.WriteLine($"[Miautrix.Mail.Seeder] Summary: {totalPermissions} permissions, {totalRoles} roles, {totalUsers} users, {totalMemberships} memberships.");
        Console.WriteLine("[Miautrix.Mail.Seeder] Seed completed successfully.");

        return 0;
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
