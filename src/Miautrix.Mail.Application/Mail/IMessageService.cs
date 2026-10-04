using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Domain;

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

    Task<AttachmentDownloadDto?> DownloadAttachmentAsync(
        Guid tenantId,
        Guid userId,
        Guid messageId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    Task<AttachmentDto?> UploadDraftAttachmentAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid draftId,
        AttachmentUploadInput input,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDraftAttachmentAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid draftId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    Task<SendMessageResult> SendMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<DraftMessageResult> UpsertDraftAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        DraftMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<SendMessageResult> SendDraftAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid draftId,
        DraftMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DiscardDraftAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid draftId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MailSignatureDto>> ListSignaturesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MailSignatureDto> UpsertSignatureAsync(
        Guid tenantId,
        Guid userId,
        MailSignatureRequest request,
        Guid? signatureId = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSignatureAsync(
        Guid tenantId,
        Guid userId,
        Guid signatureId,
        CancellationToken cancellationToken = default);

    Task<bool> SetDefaultSignatureAsync(
        Guid tenantId,
        Guid userId,
        Guid signatureId,
        CancellationToken cancellationToken = default);

    Task<bool> SetFlagAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        string? flagColor,
        CancellationToken cancellationToken = default);

    Task<bool> SetFlagAlertConfigAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        string flagColor,
        string alertConfigurationJson,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlagAlertConfiguration>> GetFlagAlertConfigsAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CancellationToken cancellationToken = default);
}
