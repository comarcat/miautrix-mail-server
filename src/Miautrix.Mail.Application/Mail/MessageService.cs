using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Application.Mail;

public sealed class MessageService : IMessageService
{
    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _auth;

    public MessageService(AppDbContext db, ITenantAuthorizationHelper auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<MessageListPage> ListMessagesAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        MessageListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);

        if (mailbox is null)
        {
            return new MessageListPage(Array.Empty<MessageSummaryDto>(), null, false, 0);
        }

        var query = _db.Messages
            .Where(m => m.TenantId == tenantId && m.MailboxId == mailboxId);

        if (filter.FolderId.HasValue)
        {
            query = query.Where(m => m.FolderId == filter.FolderId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(filter.FolderRole))
        {
            var folder = await _db.Folders
                .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Role.ToLower() == filter.FolderRole.ToLower(), cancellationToken);

            if (folder is not null)
            {
                query = query.Where(m => m.FolderId == folder.Id);
            }
        }

        if (filter.IsRead.HasValue)
        {
            query = query.Where(m => m.IsRead == filter.IsRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(m =>
                m.Subject.ToLower().Contains(term) ||
                m.Sender.ToLower().Contains(term) ||
                m.Recipient.ToLower().Contains(term) ||
                (m.BodyText != null && m.BodyText.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var limit = Math.Clamp(filter.Limit, 1, 100);
        var messages = await query
            .OrderByDescending(m => m.Date)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = messages.Count > limit;
        var items = messages.Take(limit).ToList();

        var messageIds = items.Select(m => m.Id).ToList();
        var attachments = await _db.Attachments
            .Where(a => a.TenantId == tenantId && messageIds.Contains(a.MessageId))
            .Select(a => a.MessageId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var attachmentSet = new HashSet<Guid>(attachments);

        var dtoList = items.Select(m => new MessageSummaryDto(
            m.Id,
            m.MailboxId,
            m.FolderId,
            m.Sender,
            m.Recipient,
            m.Subject,
            m.Date,
            m.SizeBytes,
            m.IsRead,
            attachmentSet.Contains(m.Id),
            GetPreview(m.BodyText))).ToList();

        return new MessageListPage(dtoList, null, hasMore, totalCount);
    }

    public async Task<MessageDetailDto?> GetMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.Id == messageId, cancellationToken);

        if (message is null)
        {
            return null;
        }

        var attachments = await _db.Attachments
            .Where(a => a.TenantId == tenantId && a.MessageId == messageId)
            .ToListAsync(cancellationToken);

        var attachmentDtos = attachments.Select(a => new AttachmentDto(
            a.Id,
            a.MessageId,
            a.FileName,
            a.ContentType,
            a.SizeBytes,
            $"/api/v1/messages/{messageId}/attachments/{a.Id}")).ToList();

        return new MessageDetailDto(
            message.Id,
            message.MailboxId,
            message.FolderId,
            message.Sender,
            message.Recipient,
            message.Subject,
            message.Date,
            message.SizeBytes,
            message.IsRead,
            message.BodyText,
            message.BodyHtml,
            message.RawHeaders,
            attachmentDtos);
    }

    public async Task<bool> MarkReadAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        bool isRead,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.Id == messageId, cancellationToken);

        if (message is null)
        {
            return false;
        }

        message.IsRead = isRead;
        message.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MoveMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        Guid targetFolderId,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.Id == messageId, cancellationToken);

        if (message is null)
        {
            return false;
        }

        var folder = await _db.Folders
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Id == targetFolderId, cancellationToken);

        if (folder is null)
        {
            return false;
        }

        message.FolderId = targetFolderId;
        message.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        bool permanent = false,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.Id == messageId, cancellationToken);

        if (message is null)
        {
            return false;
        }

        if (permanent)
        {
            _db.Attachments.RemoveRange(_db.Attachments.Where(a => a.TenantId == tenantId && a.MessageId == messageId));
            _db.MessageRecipients.RemoveRange(_db.MessageRecipients.Where(r => r.TenantId == tenantId && r.MessageId == messageId));
            _db.MessageFlags.RemoveRange(_db.MessageFlags.Where(f => f.TenantId == tenantId && f.MessageId == messageId));
            _db.Messages.Remove(message);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var trashFolder = await _db.Folders
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Role == "trash", cancellationToken);

        if (trashFolder is not null && message.FolderId != trashFolder.Id)
        {
            message.FolderId = trashFolder.Id;
            message.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        _db.Messages.Remove(message);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SendMessageResult> SendMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.From) || request.To is null || !request.To.Any())
        {
            return new SendMessageResult(false, null, null, "From and at least one To recipient are required.");
        }

        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);

        if (mailbox is null)
        {
            return new SendMessageResult(false, null, null, "Mailbox not found.");
        }

        var sentFolder = await _db.Folders
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Role == "sent", cancellationToken);

        if (sentFolder is null)
        {
            sentFolder = new Folder
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MailboxId = mailboxId,
                Name = "Sent",
                Role = "sent",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Folders.Add(sentFolder);
        }

        var bodyContent = request.BodyText ?? request.BodyHtml ?? string.Empty;
        var rawContent = $"From: {request.From}\r\nTo: {string.Join(", ", request.To)}\r\nSubject: {request.Subject}\r\nDate: {DateTimeOffset.UtcNow:R}\r\n\r\n{bodyContent}";
        var sizeBytes = Encoding.UTF8.GetByteCount(rawContent);
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawContent))).ToLowerInvariant();

        var messageId = Guid.NewGuid();
        var message = new Message
        {
            Id = messageId,
            TenantId = tenantId,
            MailboxId = mailboxId,
            FolderId = sentFolder.Id,
            Sender = request.From,
            Recipient = string.Join(", ", request.To),
            Subject = request.Subject,
            Date = DateTimeOffset.UtcNow,
            ContentHash = contentHash,
            StoragePath = $"/storage/mail/{tenantId}/{mailboxId}/{messageId}.eml",
            SizeBytes = sizeBytes,
            Flags = "\\Seen",
            IsRead = true,
            BodyText = request.BodyText,
            BodyHtml = request.BodyHtml,
            RawHeaders = $"From: {request.From}\nTo: {string.Join(", ", request.To)}\nSubject: {request.Subject}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Messages.Add(message);

        // Add recipients
        foreach (var to in request.To)
        {
            _db.MessageRecipients.Add(new MessageRecipient
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MessageId = messageId,
                Type = "to",
                Address = to,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        Guid? firstQueueItemId = null;

        // Add SmtpQueue items for outbound delivery
        foreach (var recipient in request.To)
        {
            var queueItem = new SmtpQueueItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Sender = request.From,
                Recipient = recipient,
                Subject = request.Subject,
                RawMessage = rawContent,
                Status = "Pending",
                Attempts = 0,
                NextAttemptAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.SmtpQueue.Add(queueItem);
            firstQueueItemId ??= queueItem.Id;
        }

        mailbox.UsedBytes += sizeBytes;
        await _db.SaveChangesAsync(cancellationToken);

        return new SendMessageResult(true, messageId, firstQueueItemId, "Message sent and queued for delivery.");
    }

    private static string? GetPreview(string? bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return null;
        var clean = bodyText.Replace("\r\n", " ").Replace("\n", " ").Trim();
        return clean.Length <= 120 ? clean : clean[..120] + "...";
    }
}
