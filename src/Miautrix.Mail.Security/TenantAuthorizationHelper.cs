using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;

namespace Miautrix.Mail.Security;

public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message = "Resource not found.") : base(message) { }
}

public class LastOwnerDemotionException : InvalidOperationException
{
    public LastOwnerDemotionException(string message = "Cannot demote or remove the last owner of a tenant.") : base(message) { }
}

public interface IPermissionRepository
{
    bool HasPermission(Guid tenantId, Guid userId, string permissionCode);
    int GetTenantOwnerCount(Guid tenantId);
    bool IsUserTenantOwner(Guid tenantId, Guid userId);
    bool IsMailboxOwner(Guid tenantId, Guid userId, Guid mailboxId);
    string? GetMailboxDelegateAccess(Guid tenantId, Guid userId, Guid mailboxId);
}

public interface ITenantAuthorizationHelper
{
    TResource AuthorizeAccess<TResource>(
        Guid currentTenantId,
        Guid currentUserId,
        TResource? resource,
        string requiredPermissionCode) where TResource : TenantScopedEntityBase;

    void AssertPermission(Guid tenantId, Guid userId, string permissionCode);

    void AssertMailboxAccess(
        Guid tenantId,
        Guid userId,
        Mailbox? mailbox,
        bool requireWrite);

    void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId);
}

public sealed class TenantAuthorizationHelper : ITenantAuthorizationHelper
{
    private readonly IPermissionRepository _permissionRepo;
    private readonly ISecurityEventSink _eventSink;

    public TenantAuthorizationHelper(
        IPermissionRepository permissionRepo,
        ISecurityEventSink eventSink)
    {
        _permissionRepo = permissionRepo;
        _eventSink = eventSink;
    }

    public TResource AuthorizeAccess<TResource>(
        Guid currentTenantId,
        Guid currentUserId,
        TResource? resource,
        string requiredPermissionCode) where TResource : TenantScopedEntityBase
    {
        if (resource == null || resource.TenantId != currentTenantId)
        {
            if (resource != null && resource.TenantId != currentTenantId)
            {
                _eventSink.RecordEvent(
                    currentTenantId,
                    currentUserId,
                    SecurityEventCodes.AuthPrivilegeEscalation,
                    $"Cross-tenant access attempt on resource {resource.Id} of tenant {resource.TenantId}");
            }

            throw new ResourceNotFoundException();
        }

        bool hasPermission = _permissionRepo.HasPermission(currentTenantId, currentUserId, requiredPermissionCode);
        if (!hasPermission)
        {
            _eventSink.RecordEvent(
                currentTenantId,
                currentUserId,
                SecurityEventCodes.AuthLoginFailed,
                $"Permission '{requiredPermissionCode}' denied for user {currentUserId} in tenant {currentTenantId}");

            throw new ResourceNotFoundException();
        }

        return resource;
    }

    public void AssertPermission(Guid tenantId, Guid userId, string permissionCode)
    {
        bool hasPermission = _permissionRepo.HasPermission(tenantId, userId, permissionCode);
        if (!hasPermission)
        {
            _eventSink.RecordEvent(
                tenantId,
                userId,
                SecurityEventCodes.AuthLoginFailed,
                $"Permission '{permissionCode}' denied for user {userId} in tenant {tenantId}");

            throw new ResourceNotFoundException();
        }
    }

    public void AssertMailboxAccess(Guid tenantId, Guid userId, Mailbox? mailbox, bool requireWrite)
    {
        if (mailbox is null || mailbox.TenantId != tenantId)
        {
            AuthorizeAccess(tenantId, userId, mailbox, requireWrite ? "mailbox.update" : "mailbox.read");
            return;
        }

        var permission = requireWrite ? "mailbox.update" : "mailbox.read";
        if (_permissionRepo.HasPermission(tenantId, userId, permission))
        {
            return;
        }

        if (_permissionRepo.IsMailboxOwner(tenantId, userId, mailbox.Id))
        {
            return;
        }

        var access = mailbox.Kind.Equals("shared", StringComparison.OrdinalIgnoreCase)
            ? _permissionRepo.GetMailboxDelegateAccess(tenantId, userId, mailbox.Id)
            : null;
        if (access is not null && (!requireWrite || access.Equals("write", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _eventSink.RecordEvent(
            tenantId,
            userId,
            SecurityEventCodes.AuthLoginFailed,
            $"Mailbox access denied for user {userId} on mailbox {mailbox.Id} in tenant {tenantId}");
        throw new ResourceNotFoundException();
    }

    public void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId)
    {
        bool isOwner = _permissionRepo.IsUserTenantOwner(tenantId, targetUserId);
        if (isOwner)
        {
            int ownerCount = _permissionRepo.GetTenantOwnerCount(tenantId);
            if (ownerCount <= 1)
            {
                _eventSink.RecordEvent(
                    tenantId,
                    targetUserId,
                    SecurityEventCodes.AuthPrivilegeEscalation,
                    $"Attempted removal/demotion of last remaining owner for tenant {tenantId}");

                throw new LastOwnerDemotionException();
            }
        }
    }
}
