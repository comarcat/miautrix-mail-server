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

    public async Task<bool> DeleteFolderAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid folderId,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);

        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        var folder = await _db.Folders
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Id == folderId, cancellationToken);

        if (folder is null)
        {
            return false;
        }

        // Built-in folders are protected; custom folders can be removed in
        // personal and delegated mailboxes after the authorization check.
        if (!string.Equals(folder.Role, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // RFC 3501 DELETE must preserve inferior hierarchy names. This
        // service has no tombstone/Noselect state, so refuse a parent delete.
        if (await _db.Folders.AnyAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.ParentId == folderId, cancellationToken))
            return false;

        var folderIds = new[] { folderId };

        // Delete messages in this mailbox (this is destructive).
        var messageIds = await _db.Messages
            .Where(m => m.TenantId == tenantId && m.MailboxId == mailboxId && folderIds.Contains(m.FolderId))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (messageIds.Count > 0)
        {
            // Keep ordering consistent with HardDeleteMessageAsync.
            _db.Attachments.RemoveRange(_db.Attachments.Where(a => a.TenantId == tenantId && messageIds.Contains(a.MessageId)));
            _db.MessageRecipients.RemoveRange(_db.MessageRecipients.Where(r => r.TenantId == tenantId && messageIds.Contains(r.MessageId)));
            _db.MessageFlags.RemoveRange(_db.MessageFlags.Where(f => f.TenantId == tenantId && messageIds.Contains(f.MessageId)));
            _db.Messages.RemoveRange(_db.Messages.Where(m => m.TenantId == tenantId && messageIds.Contains(m.Id)));
        }

        _db.Folders.RemoveRange(_db.Folders.Where(f => f.TenantId == tenantId && f.MailboxId == mailboxId && folderIds.Contains(f.Id)));
        await _db.SaveChangesAsync(cancellationToken);

        return true;
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

            var unreadCount = folder.Role.Equals("drafts", StringComparison.OrdinalIgnoreCase)
                ? totalCount
                : await _db.Messages
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

        var normalizedName = request.Name.Trim();
        if (await _db.Folders.AnyAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.ParentId == parentId && f.Name.ToLower() == normalizedName.ToLower(), cancellationToken))
        {
            throw new InvalidOperationException("A folder with that name already exists.");
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailboxId,
            Name = normalizedName,
            Role = "custom",
            ParentId = parentId,
            UidValidity = await AllocateUidValidityAsync(cancellationToken),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Folders.Add(folder);
        await _db.SaveChangesAsync(cancellationToken);

        return new FolderDto(folder.Id, folder.MailboxId, folder.Name, folder.Role, folder.ParentId, 0, 0);
    }

    public async Task<FolderDto> RenameFolderAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid folderId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);
        var folder = await _db.Folders.FirstOrDefaultAsync(
            f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Id == folderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Folder not found.");
        if (!string.Equals(folder.Role, "custom", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only custom folders can be renamed.");

        var path = newName.Split('/', StringSplitOptions.TrimEntries);
        if (path.Length == 0 || path.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Folder name is required.");
        var leaf = path[^1];
        Guid? parentId = null;
        foreach (var segment in path[..^1])
        {
            var parent = await _db.Folders.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.ParentId == parentId && f.Name.ToLower() == segment.ToLower(), cancellationToken);
            if (parent is null) throw new ResourceNotFoundException("Parent folder not found.");
            parentId = parent.Id;
        }
        if (parentId == folderId) throw new ArgumentException("A folder cannot be its own parent.");
        var collision = await _db.Folders.AnyAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.ParentId == parentId && f.Id != folderId && f.Name.ToLower() == leaf.ToLower(), cancellationToken);
        if (collision) throw new InvalidOperationException("A folder with that name already exists.");

        // Apply the same authorization and parent validation/cycle checks as REST re-parenting.
        if (folder.ParentId != parentId)
            await UpdateFolderParentAsync(tenantId, userId, mailboxId, folderId, new UpdateFolderParentRequest(parentId), cancellationToken);
        folder.Name = leaf.Trim();
        folder.UpdatedAt = DateTimeOffset.UtcNow;
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
                UidValidity = await AllocateUidValidityAsync(cancellationToken),
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

    private async Task<uint> AllocateUidValidityAsync(CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
            return (uint)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);

        var value = await _db.Database.SqlQueryRaw<long>("SELECT nextval('imap_uid_validity_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);
        return checked((uint)value);
    }
}
