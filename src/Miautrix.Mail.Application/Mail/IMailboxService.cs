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
}
