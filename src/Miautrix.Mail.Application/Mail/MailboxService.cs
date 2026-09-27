using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Application.Mail;

public sealed class MailboxService : IMailboxService
{
    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _auth;
    private readonly IPermissionRepository _permissionRepo;

    public MailboxService(AppDbContext db, ITenantAuthorizationHelper auth, IPermissionRepository permissionRepo)
    {
        _db = db;
        _auth = auth;
        _permissionRepo = permissionRepo;
    }

    public async Task<FolderDto> UpdateFolderParentAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid folderId,
        UpdateFolderParentRequest request,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);

        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        var folder = await _db.Folders
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Id == folderId, cancellationToken);

        if (folder is null)
        {
            throw new ResourceNotFoundException("Folder not found.");
        }

        // System folders are not re-parentable.
        if (!string.Equals(folder.Role, "custom", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only personal folders can be re-parented.");
        }

        Guid? newParentId = request.ParentId;

        if (newParentId.HasValue)
        {
            // Parent must be a custom folder in the same mailbox.
            var parent = await _db.Folders
                .FirstOrDefaultAsync(
                    f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Id == newParentId.Value,
                    cancellationToken);

            if (parent is null)
            {
                throw new ResourceNotFoundException("Parent folder not found.");
            }

            if (!string.Equals(parent.Role, "custom", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Parent folder must be personal.");
            }

            // Prevent cycles (folder cannot be parent of itself, directly or indirectly).
            if (newParentId.Value == folderId)
            {
                throw new ArgumentException("A folder cannot be its own parent.");
            }

            var cursor = parent;
            while (cursor.ParentId.HasValue)
            {
                if (cursor.ParentId.Value == folderId)
                {
                    throw new ArgumentException("Folder re-parenting would create a cycle.");
                }

                cursor = await _db.Folders
                    .FirstOrDefaultAsync(
                        f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Id == cursor.ParentId.Value,
                        cancellationToken);

                if (cursor is null)
                {
                    break;
                }
            }
        }

        folder.ParentId = newParentId;
        folder.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Maintain contract shape: counts are computed by caller for now.
        return new FolderDto(folder.Id, folder.MailboxId, folder.Name, folder.Role, folder.ParentId, 0, 0);
    }

    public async Task<IReadOnlyList<MailboxDto>> ListMailboxesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var mailboxes = await _db.Mailboxes
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.Address)
            .ToListAsync(cancellationToken);

        var authorized = new List<MailboxDto>();
        foreach (var mailbox in mailboxes)
        {
            try
            {
                _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: false);
                authorized.Add(ToDto(tenantId, userId, mailbox));
            }
            catch (ResourceNotFoundException)
            {
                // List endpoints omit resources the caller cannot see.
            }
        }

        return authorized;
    }

    public async Task<MailboxDto?> GetMailboxAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);

        if (mailbox is null)
        {
            return null;
        }

        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: false);
        return ToDto(tenantId, userId, mailbox);
    }

    public async Task<MailboxDto?> GetPrimaryMailboxAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Address.ToLower() == user.Email.ToLower(), cancellationToken);

        if (mailbox is null)
        {
            return null;
        }

        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: false);
        return ToDto(tenantId, userId, mailbox);
    }

    public async Task<IReadOnlyList<FolderDto>> GetFoldersAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);

        if (mailbox is null)
        {
            return Array.Empty<FolderDto>();
        }

        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: false);

        // A mailbox always has its default folders; provision them on first read so a
        // freshly created mailbox is usable without a separate provisioning call.
        await ProvisionDefaultFoldersAsync(tenantId, mailboxId, cancellationToken);

        var folders = await _db.Folders
            .Where(f => f.TenantId == tenantId && f.MailboxId == mailboxId)
            .ToListAsync(cancellationToken);

        var result = new List<FolderDto>();
        foreach (var folder in folders)
        {
            var totalCount = await _db.Messages
                .CountAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.FolderId == folder.Id, cancellationToken);

            var unreadCount = await _db.Messages
                .CountAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.FolderId == folder.Id && !m.IsRead, cancellationToken);

            result.Add(new FolderDto(
                folder.Id,
                folder.MailboxId,
                folder.Name,
                folder.Role,
                folder.ParentId,
                unreadCount,
                totalCount));
        }

        return result;
    }

    public async Task<FolderDto> EnsureDefaultFoldersAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        await ProvisionDefaultFoldersAsync(tenantId, mailboxId, cancellationToken);

        var inbox = await _db.Folders
            .FirstAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Role == "inbox", cancellationToken);

        return new FolderDto(inbox.Id, inbox.MailboxId, inbox.Name, inbox.Role, inbox.ParentId, 0, 0);
    }

    public async Task<FolderDto> CreateFolderAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CreateFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);

        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Folder name is required.");
        }

        Guid? parentId = request.ParentId;
        if (parentId.HasValue)
        {
            var parent = await _db.Folders
                .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Id == parentId.Value, cancellationToken);

            if (parent is null)
            {
                throw new ResourceNotFoundException("Parent folder not found.");
            }
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailboxId,
            Name = request.Name.Trim(),
            Role = "custom",
            ParentId = parentId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Folders.Add(folder);
        await _db.SaveChangesAsync(cancellationToken);

        return new FolderDto(folder.Id, folder.MailboxId, folder.Name, folder.Role, folder.ParentId, 0, 0);
    }

    /// <summary>
    /// Creates any missing default folder. Idempotent; the caller is responsible for the
    /// authorization check.
    /// </summary>
    public async Task ProvisionDefaultFoldersAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken)
    {
        var existingRoles = await _db.Folders
            .Where(f => f.TenantId == tenantId && f.MailboxId == mailboxId)
            .Select(f => f.Role)
            .ToListAsync(cancellationToken);

        var defaultRoles = new[] { ("Inbox", "inbox"), ("Sent", "sent"), ("Drafts", "drafts"), ("Trash", "trash"), ("Junk", "junk"), ("Archive", "archive") };
        var added = false;

        foreach (var (name, role) in defaultRoles)
        {
            if (existingRoles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _db.Folders.Add(new Folder
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MailboxId = mailboxId,
                Name = name,
                Role = role,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            added = true;
        }

        if (added)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private MailboxDto ToDto(Guid tenantId, Guid userId, Mailbox m) => new(
        m.Id,
        m.TenantId,
        m.DomainId,
        m.Address,
        m.QuotaBytes,
        m.UsedBytes,
        m.IsActive,
        m.CreatedAt,
        m.Kind,
        _permissionRepo.GetMailboxEffectiveAccess(tenantId, userId, m));
}
