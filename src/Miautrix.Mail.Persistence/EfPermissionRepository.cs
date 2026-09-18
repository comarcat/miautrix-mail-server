using Miautrix.Mail.Domain;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Persistence;

/// <summary>
/// Database-backed implementation of <see cref="IPermissionRepository"/>. Roles bind
/// to memberships, permissions to roles, so a user's permission set is derived from
/// their membership's role within the tenant.
/// </summary>
public sealed class EfPermissionRepository : IPermissionRepository
{
    private readonly AppDbContext _db;

    public EfPermissionRepository(AppDbContext db) => _db = db;

    public bool HasPermission(Guid tenantId, Guid userId, string permissionCode)
    {
        var membershipRoleIds = _db.Memberships
            .Where(m => m.TenantId == tenantId && m.UserId == userId && m.RoleId != null)
            .Select(m => m.RoleId!.Value);

        return _db.RolePermissions
            .Where(rp => rp.TenantId == tenantId
                         && rp.RoleId != null
                         && rp.PermissionId != null
                         && membershipRoleIds.Contains(rp.RoleId.Value))
            .Join(
                _db.Permissions.Where(p => p.TenantId == tenantId && p.Code == permissionCode),
                rp => rp.PermissionId!.Value,
                p => p.Id,
                (rp, p) => p)
            .Any();
    }

    public int GetTenantOwnerCount(Guid tenantId)
    {
        var ownerRoleIds = _db.Roles
            .Where(r => r.TenantId == tenantId && r.Code == "owner")
            .Select(r => r.Id);

        return _db.Memberships
            .Count(m => m.TenantId == tenantId && m.RoleId != null && ownerRoleIds.Contains(m.RoleId.Value));
    }

    public bool IsUserTenantOwner(Guid tenantId, Guid userId)
    {
        var ownerRoleIds = _db.Roles
            .Where(r => r.TenantId == tenantId && r.Code == "owner")
            .Select(r => r.Id);

        return _db.Memberships.Any(m =>
            m.TenantId == tenantId && m.UserId == userId && m.RoleId != null && ownerRoleIds.Contains(m.RoleId.Value));
    }
}
