using System;
using System.Collections.Generic;

namespace Miautrix.Mail.Application.Mail;

public sealed record MailboxDto(
    Guid Id,
    Guid TenantId,
    Guid DomainId,
    string Address,
    long QuotaBytes,
    long UsedBytes,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record FolderDto(
    Guid Id,
    Guid MailboxId,
    string Name,
    string Role,
    int UnreadCount,
    int TotalCount);

public sealed record MessageSummaryDto(
    Guid Id,
    Guid MailboxId,
    Guid FolderId,
    string Sender,
    string Recipient,
    string Subject,
    DateTimeOffset Date,
    long SizeBytes,
    bool IsRead,
    bool HasAttachments,
    string? Preview = null);

public sealed record AttachmentDto(
    Guid Id,
    Guid MessageId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? DownloadUrl = null);

public sealed record MessageDetailDto(
    Guid Id,
    Guid MailboxId,
    Guid FolderId,
    string Sender,
    string Recipient,
    string Subject,
    DateTimeOffset Date,
    long SizeBytes,
    bool IsRead,
    string? BodyText,
    string? BodyHtml,
    string RawHeaders,
    IReadOnlyList<AttachmentDto> Attachments);

public sealed record SendMessageRequest(
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string>? Cc,
    IReadOnlyList<string>? Bcc,
    string Subject,
    string? BodyText,
    string? BodyHtml);

public sealed record SendMessageResult(
    bool Success,
    Guid? MessageId,
    Guid? QueueItemId,
    string Message);

public sealed record MessageListFilter(
    Guid? FolderId = null,
    string? FolderRole = null,
    string? Search = null,
    bool? IsRead = null,
    int Limit = 50,
    string? Cursor = null);

public sealed record MessageListPage(
    IReadOnlyList<MessageSummaryDto> Items,
    string? NextCursor,
    bool HasMore,
    int TotalCount);
