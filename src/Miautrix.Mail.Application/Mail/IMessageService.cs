using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Miautrix.Mail.Application.Mail;

public interface IMessageService
{
    Task<MessageListPage> ListMessagesAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        MessageListFilter filter,
        CancellationToken cancellationToken = default);

    Task<MessageDetailDto?> GetMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        bool isRead,
        CancellationToken cancellationToken = default);

    Task<bool> MoveMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        Guid targetFolderId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        bool permanent = false,
        CancellationToken cancellationToken = default);

    Task<SendMessageResult> SendMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);
}
