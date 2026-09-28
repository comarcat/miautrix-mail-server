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
    DateTimeOffset CreatedAt,
    string Kind,
    string AccessLevel);

public sealed record FolderDto(
    Guid Id,
    Guid MailboxId,
    string Name,
    string Role,
    Guid? ParentId,
    int UnreadCount,
    int TotalCount);

public sealed record CreateFolderRequest(
    string Name,
    Guid? ParentId);

public sealed record UpdateFolderParentRequest(
    Guid? ParentId);

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
    string? FlagColor = null,
    string? Preview = null);

public sealed record AttachmentDto(
    Guid Id,
    Guid MessageId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? DownloadUrl = null);

// Internal DTO for streaming attachment bytes to the web API.
// Intentionally not used as an API response envelope.
public sealed record AttachmentDownloadDto(
    string FileName,
    string ContentType,
    System.IO.Stream ContentStream);

public sealed record AttachmentUploadInput(
    string FileName,
    string ContentType,
    long SizeBytes,
    System.IO.Stream ContentStream);

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
    string? FlagColor,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<string>? Cc = null,
    IReadOnlyList<string>? Bcc = null);

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

public sealed record DraftMessageRequest(
    Guid? DraftId,
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string>? Cc,
    IReadOnlyList<string>? Bcc,
    string Subject,
    string? BodyText,
    string? BodyHtml);

public sealed record DraftMessageResult(
    bool Success,
    Guid? DraftId,
    string Message);

public sealed record MailSignatureDto(
    Guid Id,
    string Name,
    string ContentText,
    string? ContentHtml,
    bool IsDefault);

public sealed record MailSignatureRequest(
    string Name,
    string ContentText,
    string? ContentHtml,
    bool IsDefault);

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
