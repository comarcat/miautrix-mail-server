using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Miautrix.Mail.Application.Mail;

public interface IMailboxService
{
    Task<IReadOnlyList<MailboxDto>> ListMailboxesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MailboxDto?> GetMailboxAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CancellationToken cancellationToken = default);

    Task<MailboxDto?> GetPrimaryMailboxAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FolderDto>> GetFoldersAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CancellationToken cancellationToken = default);

    Task<FolderDto> EnsureDefaultFoldersAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CancellationToken cancellationToken = default);

    Task<FolderDto> CreateFolderAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CreateFolderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates any missing default system folders (Inbox, Sent, Drafts, Trash, Junk, Archive)
    /// for the mailbox. Idempotent. Performs no authorization check: callers are responsible
    /// for having already established access to the mailbox.
    /// </summary>
    Task ProvisionDefaultFoldersAsync(
        Guid tenantId,
        Guid mailboxId,
        CancellationToken cancellationToken = default);

    Task<FolderDto> UpdateFolderParentAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid folderId,
        UpdateFolderParentRequest request,
        CancellationToken cancellationToken = default);
}
