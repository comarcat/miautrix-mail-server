using System;
using System.Collections.Generic;
using System.Linq;
using Attachment = Miautrix.Mail.Domain.Attachment;
using System.Net.Mail;

using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Mail.Mime;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Miautrix.Mail.Application.Mail;

public sealed class MessageService : IMessageService
{
    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _auth;
    private readonly IMailboxService _mailboxes;

    private readonly IMailStorage _storage;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        AppDbContext db,
        ITenantAuthorizationHelper auth,
        IMailboxService mailboxes,
        IMailStorage storage,
        ILogger<MessageService> logger)
    {
        _db = db;
        _auth = auth;
        _mailboxes = mailboxes;
        _storage = storage;
        _logger = logger;
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
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: false);

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

        var messageFlags = await _db.MessageFlags
            .Where(f => f.TenantId == tenantId && messageIds.Contains(f.MessageId))
            .ToDictionaryAsync(f => f.MessageId, f => f.Flag, cancellationToken);

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
            messageFlags.GetValueOrDefault(m.Id),
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
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: false);

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.Id == messageId, cancellationToken);

        if (message is null)
        {
            return null;
        }

        var attachments = await _db.Attachments
            .Where(a => a.TenantId == tenantId && a.MessageId == messageId)
            .ToListAsync(cancellationToken);

        var flag = await _db.MessageFlags
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MessageId == messageId, cancellationToken);
        var flagColor = flag?.Flag;

        var attachmentDtos = attachments.Select(a => new AttachmentDto(
            a.Id,
            a.MessageId,
            a.FileName,
            a.ContentType,
            a.SizeBytes,
            $"/api/v1/messages/{messageId}/attachments/{a.Id}")).ToList();
        var recipients = await _db.MessageRecipients
            .Where(r => r.TenantId == tenantId && r.MessageId == messageId)
            .ToListAsync(cancellationToken);
        var cc = recipients.Where(r => r.Type.Equals("cc", StringComparison.OrdinalIgnoreCase)).Select(r => r.Address).ToList();
        var bcc = recipients.Where(r => r.Type.Equals("bcc", StringComparison.OrdinalIgnoreCase)).Select(r => r.Address).ToList();

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
            flagColor,
            attachmentDtos,
            cc,
            bcc);
    }

    public async Task<bool> MarkReadAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        bool isRead,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

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
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

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
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.Id == messageId, cancellationToken);

        if (message is null)
        {
            return false;
        }

        // A delete is only ever destructive when it is explicitly requested as permanent,
        // or when the message is already sitting in Trash (the usual "empty trash" case).
        if (permanent)
        {
            await HardDeleteMessageAsync(tenantId, message, cancellationToken);
            return true;
        }

        var trashFolder = await FindTrashFolderAsync(tenantId, mailboxId, cancellationToken);

        if (message.FolderId == trashFolder.Id)
        {
            // Already in Trash: deleting again removes it for good.
            await HardDeleteMessageAsync(tenantId, message, cancellationToken);
            return true;
        }

        message.FolderId = trashFolder.Id;
        message.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Resolves the mailbox Trash folder, provisioning the default folder set when it is absent.
    /// Throws rather than returning null: a missing system folder must never be the reason a
    /// message is destroyed, so the caller fails the request instead of falling through to a
    /// permanent delete.
    /// </summary>
    private async Task<Folder> FindTrashFolderAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken)
    {
        var trashFolder = await _db.Folders
            .FirstOrDefaultAsync(
                f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Role.ToLower() == "trash",
                cancellationToken);

        if (trashFolder is not null)
        {
            return trashFolder;
        }

        // Legacy or partially provisioned mailbox: create the system folders, then retry.
        await _mailboxes.ProvisionDefaultFoldersAsync(tenantId, mailboxId, cancellationToken);

        trashFolder = await _db.Folders
            .FirstOrDefaultAsync(
                f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Role.ToLower() == "trash",
                cancellationToken);

        if (trashFolder is null)
        {
            throw new InvalidOperationException(
                $"Mailbox {mailboxId} has no Trash folder; refusing to permanently delete the message.");
        }

        return trashFolder;
    }

    private async Task HardDeleteMessageAsync(Guid tenantId, Message message, CancellationToken cancellationToken)
    {
        var messageId = message.Id;
        _db.Attachments.RemoveRange(_db.Attachments.Where(a => a.TenantId == tenantId && a.MessageId == messageId));
        _db.MessageRecipients.RemoveRange(_db.MessageRecipients.Where(r => r.TenantId == tenantId && r.MessageId == messageId));
        _db.MessageFlags.RemoveRange(_db.MessageFlags.Where(f => f.TenantId == tenantId && f.MessageId == messageId));
        _db.Messages.Remove(message);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AttachmentDownloadDto?> DownloadAttachmentAsync(
        Guid tenantId,
        Guid userId,
        Guid messageId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        // Tenant membership is enforced by tenant-scoped read + authorization.
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == messageId, cancellationToken);

        if (message is null)
        {
            return null;
        }

        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == message.MailboxId, cancellationToken);

        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: false);

        var attachment = await _db.Attachments
            .FirstOrDefaultAsync(a =>
                a.TenantId == tenantId &&
                a.MessageId == messageId &&
                a.Id == attachmentId,
                cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        var stream = await _storage.OpenReadAsync(attachment.ContentHash, cancellationToken);
        if (stream is null)
        {
            return null;
        }

        return new AttachmentDownloadDto(attachment.FileName, attachment.ContentType, stream);
    }

    private async Task<Message?> GetDraftForWriteAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid draftId,
        CancellationToken cancellationToken)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);

        if (mailbox is null)
        {
            return null;
        }

        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        var draftsFolder = await GetOrCreateDraftsFolderAsync(tenantId, mailboxId, cancellationToken);

        return await _db.Messages
            .FirstOrDefaultAsync(m =>
                m.TenantId == tenantId &&
                m.MailboxId == mailboxId &&
                m.FolderId == draftsFolder.Id &&
                m.Id == draftId,
                cancellationToken);
    }

    public async Task<AttachmentDto?> UploadDraftAttachmentAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid draftId,
        AttachmentUploadInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.SizeBytes <= 0 || input.ContentStream is null)
        {
            throw new ArgumentException("Attachment content is required.");
        }

        var draft = await GetDraftForWriteAsync(tenantId, userId, mailboxId, draftId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        var fileName = System.IO.Path.GetFileName(input.FileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "attachment";
        }

        var contentType = string.IsNullOrWhiteSpace(input.ContentType)
            ? "application/octet-stream"
            : input.ContentType.Trim();

        var stored = await _storage.StoreAsync(input.ContentStream, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MessageId = draft.Id,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = stored.SizeBytes,
            ContentHash = stored.ContentHash,
            StoragePath = stored.StoragePath,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Attachments.Add(attachment);
        draft.SizeBytes += stored.SizeBytes;
        draft.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        return ToAttachmentDto(attachment);
    }

    public async Task<bool> DeleteDraftAttachmentAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid draftId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var draft = await GetDraftForWriteAsync(tenantId, userId, mailboxId, draftId, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        var attachment = await _db.Attachments.FirstOrDefaultAsync(a =>
            a.TenantId == tenantId &&
            a.MessageId == draft.Id &&
            a.Id == attachmentId,
            cancellationToken);

        if (attachment is null)
        {
            return false;
        }

        _db.Attachments.Remove(attachment);
        draft.SizeBytes = Math.Max(0, draft.SizeBytes - attachment.SizeBytes);
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<SendMessageResult> SendMessageAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default) =>
        SendMessageInternalAsync(tenantId, userId, mailboxId, request, Array.Empty<Attachment>(), cancellationToken);

    private async Task<SendMessageResult> SendMessageInternalAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        SendMessageRequest request,
        IReadOnlyList<Attachment> sourceAttachments,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.From) || request.To is null || !request.To.Any())
        {
            return new SendMessageResult(false, null, null, "From and at least one To recipient are required.");
        }

        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        var sentFolder = await GetOrCreateSentFolderAsync(tenantId, mailboxId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var generatedAttachments = request.Attachments ?? Array.Empty<MimeAttachment>();
        var storedMimeAttachments = await LoadMimeAttachmentsAsync(sourceAttachments, cancellationToken);
        var built = MimeMessageBuilder.Build(new MimeMessageRequest(
            request.From,
            request.To,
            request.Cc,
            request.Bcc,
            request.Subject,
            request.BodyText,
            request.BodyHtml,
            storedMimeAttachments.Concat(generatedAttachments).ToList(),
            now));
        var sizeBytes = built.SizeBytes;
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(built.RawMessage))).ToLowerInvariant();

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
            Date = now,
            ContentHash = contentHash,
            StoragePath = $"/storage/mail/{tenantId}/{mailboxId}/{messageId}.eml",
            SizeBytes = sizeBytes,
            Flags = "\\Seen",
            IsRead = true,
            BodyText = request.BodyText,
            BodyHtml = request.BodyHtml,
            RawHeaders = built.TopLevelHeaders,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Messages.Add(message);
        AddAttachmentCopies(tenantId, sourceAttachments, messageId);
        await AddGeneratedAttachmentCopiesAsync(tenantId, generatedAttachments, messageId, cancellationToken);

        // Add recipients, preserving To/Cc/Bcc for message details and outbound delivery.
        foreach (var (address, type) in request.To.Select(address => (address, "to"))
            .Concat((request.Cc ?? Array.Empty<string>()).Select(address => (address, "cc")))
            .Concat((request.Bcc ?? Array.Empty<string>()).Select(address => (address, "bcc"))))
        {
            _db.MessageRecipients.Add(new MessageRecipient
            {
                Id = Guid.NewGuid(), TenantId = tenantId, MessageId = messageId,
                Type = type, Address = address, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        Guid? firstQueueItemId = null;
        var deliveryRecipients = request.To
            .Concat(request.Cc ?? Array.Empty<string>())
            .Concat(request.Bcc ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var localRecipients = await _db.Mailboxes
            .Where(m => m.TenantId == tenantId && deliveryRecipients.Select(r => r.Trim().ToLower()).Contains(m.Address.ToLower()))
            .ToDictionaryAsync(m => m.Address.ToLower(), cancellationToken);

        foreach (var recipient in deliveryRecipients)
        {
            var normalizedRecipient = recipient.Trim().ToLowerInvariant();
            if (localRecipients.TryGetValue(normalizedRecipient, out var recipientMailbox))
            {
                var inboxFolder = await GetOrCreateInboxFolderAsync(tenantId, recipientMailbox.Id, cancellationToken);
                var deliveredMessageId = Guid.NewGuid();
                _db.Messages.Add(new Message
                {
                    Id = deliveredMessageId,
                    TenantId = tenantId,
                    MailboxId = recipientMailbox.Id,
                    FolderId = inboxFolder.Id,
                    Sender = request.From,
                    Recipient = recipient,
                    Subject = request.Subject,
                    Date = now,
                    ContentHash = contentHash,
                    StoragePath = $"/storage/mail/{tenantId}/{recipientMailbox.Id}/{deliveredMessageId}.eml",
                    SizeBytes = sizeBytes,
                    Flags = string.Empty,
                    IsRead = false,
                    BodyText = request.BodyText,
                    BodyHtml = request.BodyHtml,
                    RawHeaders = built.TopLevelHeaders,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                AddAttachmentCopies(tenantId, sourceAttachments, deliveredMessageId);
                await AddGeneratedAttachmentCopiesAsync(tenantId, generatedAttachments, deliveredMessageId, cancellationToken);
                recipientMailbox.UsedBytes += sizeBytes;
                continue;
            }

            var queueItem = new SmtpQueueItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Sender = request.From,
                Recipient = recipient,
                Subject = request.Subject,
                RawMessage = built.RawMessage,
                Direction = "Outbound",
                Status = "Pending",
                Attempts = 0,
                NextAttemptAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.SmtpQueue.Add(queueItem);
            firstQueueItemId ??= queueItem.Id;
        }

        mailbox!.UsedBytes += sizeBytes;

        if (mailbox.Kind.Equals("shared", StringComparison.OrdinalIgnoreCase))
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken);
            if (user is not null)
            {
                var personalMailbox = await _db.Mailboxes
                    .FirstOrDefaultAsync(m =>
                        m.TenantId == tenantId &&
                        m.Id != mailbox.Id &&
                        m.Address.ToLower() == user.Email.ToLower(),
                        cancellationToken);

                if (personalMailbox is not null)
                {
                    var personalSentFolder = await GetOrCreateSentFolderAsync(tenantId, personalMailbox.Id, cancellationToken);
                    var personalMessageId = Guid.NewGuid();
                    _db.Messages.Add(CloneSentMessage(message, personalMessageId, personalMailbox.Id, personalSentFolder.Id));
                    AddAttachmentCopies(tenantId, sourceAttachments, personalMessageId);
                    await AddGeneratedAttachmentCopiesAsync(tenantId, generatedAttachments, personalMessageId, cancellationToken);
                    personalMailbox.UsedBytes += sizeBytes;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await AutoSaveRecipientContactsAsync(tenantId, userId, request.From, deliveryRecipients, cancellationToken);

        return new SendMessageResult(true, messageId, firstQueueItemId, "Message sent and queued for delivery.");
    }

    private async Task AutoSaveRecipientContactsAsync(Guid tenantId, Guid userId, string from, IReadOnlyList<string> recipients, CancellationToken cancellationToken)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (TryNormalizeEmail(from, out var fromAddress, out _))
        {
            excluded.Add(fromAddress);
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken);
        if (user != null && TryNormalizeEmail(user.Email, out var normalizedUserEmail, out _))
        {
            excluded.Add(normalizedUserEmail);
        }

        var candidates = recipients
            .Select(r => TryNormalizeEmail(r, out var email, out var displayName) ? (Email: email, DisplayName: displayName) : default)
            .Where(r => !string.IsNullOrWhiteSpace(r.Email) && !excluded.Contains(r.Email))
            .GroupBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var candidateEmails = candidates.Select(c => c.Email).ToList();
        var existingEmails = await _db.Contacts
            .Where(c => c.TenantId == tenantId && c.UserId == userId && candidateEmails.Contains(c.Email))
            .Select(c => c.Email)
            .Union(_db.Mailboxes
                .Where(m => m.TenantId == tenantId && m.IsActive && candidateEmails.Contains(m.Address))
                .Select(m => m.Address))
            .Union(_db.Groups
                .Where(g => g.TenantId == tenantId && candidateEmails.Contains(g.Address))
                .Select(g => g.Address))
            .ToListAsync(cancellationToken);

        var existing = existingEmails.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var candidate in candidates.Where(c => !existing.Contains(c.Email)))
        {
            _db.Contacts.Add(new Contact
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                Name = string.IsNullOrWhiteSpace(candidate.DisplayName) ? candidate.Email.Split('@')[0] : candidate.DisplayName,
                Email = candidate.Email,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!_db.ChangeTracker.Entries<Contact>().Any(e => e.State == EntityState.Added))
        {
            return;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Recipient contact autosave skipped after message send due to a persistence race.");
            foreach (var entry in _db.ChangeTracker.Entries<Contact>().Where(e => e.State == EntityState.Added))
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private static bool TryNormalizeEmail(string raw, out string email, out string? displayName)
    {
        email = string.Empty;
        displayName = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(raw.Trim());
            email = address.Address.Trim().ToLowerInvariant();
            displayName = string.IsNullOrWhiteSpace(address.DisplayName) ? null : address.DisplayName.Trim();
            return email.Contains('@', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private Task<Folder> GetOrCreateSentFolderAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken) =>
        GetOrCreateSystemFolderAsync(tenantId, mailboxId, "sent", "Sent", cancellationToken);

    private Task<Folder> GetOrCreateInboxFolderAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken) =>
        GetOrCreateSystemFolderAsync(tenantId, mailboxId, "inbox", "Inbox", cancellationToken);

    private async Task<Folder> GetOrCreateSystemFolderAsync(Guid tenantId, Guid mailboxId, string role, string name, CancellationToken cancellationToken)
    {
        var folder = await _db.Folders
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Role == role, cancellationToken);

        if (folder is not null)
        {
            return folder;
        }

        folder = new Folder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailboxId,
            Name = name,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Folders.Add(folder);
        return folder;
    }

    private void AddAttachmentCopies(Guid tenantId, IReadOnlyList<Attachment> sourceAttachments, Guid messageId)
    {
        foreach (var source in sourceAttachments)
        {
            _db.Attachments.Add(new Attachment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MessageId = messageId,
                FileName = source.FileName,
                ContentType = source.ContentType,
                SizeBytes = source.SizeBytes,
                ContentHash = source.ContentHash,
                StoragePath = source.StoragePath,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private async Task AddGeneratedAttachmentCopiesAsync(Guid tenantId, IReadOnlyList<MimeAttachment> attachments, Guid messageId, CancellationToken cancellationToken)
    {
        foreach (var attachment in attachments)
        {
            await using var stream = new MemoryStream(attachment.Content);
            var stored = await _storage.StoreAsync(stream, cancellationToken);
            _db.Attachments.Add(new Attachment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MessageId = messageId,
                FileName = string.IsNullOrWhiteSpace(attachment.FileName) ? "attachment" : Path.GetFileName(attachment.FileName),
                ContentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
                SizeBytes = stored.SizeBytes,
                ContentHash = stored.ContentHash,
                StoragePath = stored.StoragePath,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private async Task<IReadOnlyList<MimeAttachment>> LoadMimeAttachmentsAsync(IReadOnlyList<Attachment> attachments, CancellationToken cancellationToken)
    {
        var result = new List<MimeAttachment>();
        foreach (var attachment in attachments)
        {
            await using var stream = await _storage.OpenReadAsync(attachment.ContentHash, cancellationToken);
            if (stream is null)
            {
                throw new InvalidOperationException($"Attachment content for {attachment.FileName} could not be found.");
            }

            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            result.Add(new MimeAttachment(attachment.FileName, attachment.ContentType, memory.ToArray()));
        }

        return result;
    }

    private async Task<IReadOnlyList<Attachment>> LoadAttachmentsForMessageAsync(
        Guid tenantId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return await _db.Attachments
            .Where(a => a.TenantId == tenantId && a.MessageId == messageId)
            .ToListAsync(cancellationToken);
    }

    private static AttachmentDto ToAttachmentDto(Attachment attachment) => new(
        attachment.Id,
        attachment.MessageId,
        attachment.FileName,
        attachment.ContentType,
        attachment.SizeBytes,
        $"/api/v1/messages/{attachment.MessageId}/attachments/{attachment.Id}");

    private static Message CloneSentMessage(Message source, Guid messageId, Guid mailboxId, Guid folderId) => new()
    {
        Id = messageId,
        TenantId = source.TenantId,
        MailboxId = mailboxId,
        FolderId = folderId,
        Sender = source.Sender,
        Recipient = source.Recipient,
        Subject = source.Subject,
        Date = source.Date,
        ContentHash = source.ContentHash,
        StoragePath = $"/storage/mail/{source.TenantId}/{mailboxId}/{messageId}.eml",
        SizeBytes = source.SizeBytes,
        Flags = source.Flags,
        IsRead = source.IsRead,
        BodyText = source.BodyText,
        BodyHtml = source.BodyHtml,
        RawHeaders = source.RawHeaders,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    public async Task<DraftMessageResult> UpsertDraftAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        DraftMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.From))
        {
            return new DraftMessageResult(false, request.DraftId, "From address is required.");
        }

        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        var draftsFolder = await GetOrCreateDraftsFolderAsync(tenantId, mailboxId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var recipients = request.To is { Count: > 0 } ? string.Join(", ", request.To) : string.Empty;
        var built = MimeMessageBuilder.Build(new MimeMessageRequest(
            request.From,
            request.To ?? Array.Empty<string>(),
            request.Cc,
            request.Bcc,
            request.Subject,
            request.BodyText,
            request.BodyHtml,
            Array.Empty<MimeAttachment>(),
            now));
        var sizeBytes = built.SizeBytes;
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(built.RawMessage))).ToLowerInvariant();

        Message? draft = null;
        if (request.DraftId.HasValue)
        {
            draft = await _db.Messages.FirstOrDefaultAsync(m =>
                m.TenantId == tenantId &&
                m.MailboxId == mailboxId &&
                m.FolderId == draftsFolder.Id &&
                m.Id == request.DraftId.Value,
                cancellationToken);
        }

        if (draft is null)
        {
            draft = new Message
            {
                Id = request.DraftId ?? Guid.NewGuid(),
                TenantId = tenantId,
                MailboxId = mailboxId,
                FolderId = draftsFolder.Id,
                CreatedAt = now
            };
            _db.Messages.Add(draft);
        }

        draft.Sender = request.From;
        draft.Recipient = recipients;
        draft.Subject = string.IsNullOrWhiteSpace(request.Subject) ? "(No Subject)" : request.Subject;
        draft.Date = now;
        draft.ContentHash = contentHash;
        draft.StoragePath = $"/storage/mail/{tenantId}/{mailboxId}/{draft.Id}.eml";
        if (draft.Id != request.DraftId) {
            draft.SizeBytes = sizeBytes;
        }
        draft.Flags = "\\Draft";
        draft.IsRead = true;
        draft.BodyText = request.BodyText;
        draft.BodyHtml = request.BodyHtml;
        draft.RawHeaders = built.TopLevelHeaders;
        draft.UpdatedAt = now;

        _db.MessageRecipients.RemoveRange(_db.MessageRecipients.Where(r => r.TenantId == tenantId && r.MessageId == draft.Id));
        foreach (var (address, type) in (request.To ?? Array.Empty<string>()).Select(address => (address, "to"))
            .Concat((request.Cc ?? Array.Empty<string>()).Select(address => (address, "cc")))
            .Concat((request.Bcc ?? Array.Empty<string>()).Select(address => (address, "bcc"))))
        {
            _db.MessageRecipients.Add(new MessageRecipient
            {
                Id = Guid.NewGuid(), TenantId = tenantId, MessageId = draft.Id,
                Type = type, Address = address, CreatedAt = now, UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new DraftMessageResult(true, draft.Id, "Draft saved.");
    }

    public async Task<SendMessageResult> SendDraftAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid draftId,
        DraftMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var saved = await UpsertDraftAsync(tenantId, userId, mailboxId, request with { DraftId = draftId }, cancellationToken);
        if (!saved.Success || saved.DraftId is null)
        {
            return new SendMessageResult(false, null, null, saved.Message);
        }

        var draftAttachments = await LoadAttachmentsForMessageAsync(tenantId, saved.DraftId.Value, cancellationToken);

        var result = await SendMessageInternalAsync(
            tenantId,
            userId,
            mailboxId,
            new SendMessageRequest(
                request.From,
                request.To,
                request.Cc,
                request.Bcc,
                request.Subject,
                request.BodyText,
                request.BodyHtml),
            draftAttachments,
            cancellationToken);

        if (result.Success)
        {
            await DiscardDraftAsync(tenantId, userId, mailboxId, saved.DraftId.Value, cancellationToken);
        }

        return result;
    }

    public async Task<bool> DiscardDraftAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        var draftsFolder = await GetOrCreateDraftsFolderAsync(tenantId, mailboxId, cancellationToken);
        var draft = await _db.Messages.FirstOrDefaultAsync(m =>
            m.TenantId == tenantId &&
            m.MailboxId == mailboxId &&
            m.FolderId == draftsFolder.Id &&
            m.Id == draftId,
            cancellationToken);

        if (draft is null) return false;
        await HardDeleteMessageAsync(tenantId, draft, cancellationToken);
        return true;
    }

    private async Task<Folder> GetOrCreateDraftsFolderAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken)
    {
        var draftsFolder = await _db.Folders
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Role == "drafts", cancellationToken);

        if (draftsFolder is not null)
        {
            return draftsFolder;
        }

        draftsFolder = new Folder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailboxId,
            Name = "Drafts",
            Role = "drafts",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Folders.Add(draftsFolder);
        return draftsFolder;
    }

    public async Task<IReadOnlyList<MailSignatureDto>> ListSignaturesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.MailSignatures
            .Where(s => s.TenantId == tenantId && s.UserId == userId)
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.Name)
            .Select(s => new MailSignatureDto(s.Id, s.Name, s.ContentText, s.ContentHtml, s.IsDefault))
            .ToListAsync(cancellationToken);
    }

    public async Task<MailSignatureDto> UpsertSignatureAsync(
        Guid tenantId,
        Guid userId,
        MailSignatureRequest request,
        Guid? signatureId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        MailSignature? signature = null;
        if (signatureId.HasValue)
        {
            signature = await _db.MailSignatures.FirstOrDefaultAsync(s =>
                s.TenantId == tenantId && s.UserId == userId && s.Id == signatureId.Value,
                cancellationToken);
        }

        if (signature is null)
        {
            signature = new MailSignature
            {
                Id = signatureId ?? Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                CreatedAt = now
            };
            _db.MailSignatures.Add(signature);
        }

        if (request.IsDefault)
        {
            await _db.MailSignatures
                .Where(s => s.TenantId == tenantId && s.UserId == userId && s.Id != signature.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.IsDefault, false)
                    .SetProperty(s => s.UpdatedAt, now),
                    cancellationToken);
        }

        signature.Name = string.IsNullOrWhiteSpace(request.Name) ? "Signature" : request.Name.Trim();
        signature.ContentText = request.ContentText ?? string.Empty;
        signature.ContentHtml = request.ContentHtml;
        signature.IsDefault = request.IsDefault;
        signature.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return new MailSignatureDto(signature.Id, signature.Name, signature.ContentText, signature.ContentHtml, signature.IsDefault);
    }

    public async Task<bool> DeleteSignatureAsync(
        Guid tenantId,
        Guid userId,
        Guid signatureId,
        CancellationToken cancellationToken = default)
    {
        var signature = await _db.MailSignatures.FirstOrDefaultAsync(s =>
            s.TenantId == tenantId && s.UserId == userId && s.Id == signatureId,
            cancellationToken);
        if (signature is null) return false;
        _db.MailSignatures.Remove(signature);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetDefaultSignatureAsync(
        Guid tenantId,
        Guid userId,
        Guid signatureId,
        CancellationToken cancellationToken = default)
    {
        var signature = await _db.MailSignatures.FirstOrDefaultAsync(s =>
            s.TenantId == tenantId && s.UserId == userId && s.Id == signatureId,
            cancellationToken);
        if (signature is null) return false;

        var now = DateTimeOffset.UtcNow;
        await _db.MailSignatures
            .Where(s => s.TenantId == tenantId && s.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.IsDefault, s => s.Id == signatureId)
                .SetProperty(s => s.UpdatedAt, now),
                cancellationToken);
        return true;
    }

    public async Task<bool> SetFlagAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid messageId,
        string? flagColor,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.Id == messageId, cancellationToken);
        if (message is null) return false;

        var existingFlag = await _db.MessageFlags
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MessageId == messageId, cancellationToken);

        if (string.IsNullOrWhiteSpace(flagColor))
        {
            if (existingFlag is not null)
            {
                _db.MessageFlags.Remove(existingFlag);
                await _db.SaveChangesAsync(cancellationToken);
            }
            return true;
        }

        if (existingFlag is not null)
        {
            existingFlag.Flag = flagColor;
            existingFlag.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.MessageFlags.Add(new MessageFlag
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MessageId = messageId,
                Flag = flagColor,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetFlagAlertConfigAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        string flagColor,
        string alertConfigurationJson,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true);

        var existingConfig = await _db.FlagAlertConfigurations
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.MailboxId == mailboxId && f.Flag == flagColor, cancellationToken);

        if (existingConfig is not null)
        {
            existingConfig.AlertConfigurationJson = alertConfigurationJson;
            existingConfig.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.FlagAlertConfigurations.Add(new FlagAlertConfiguration
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MailboxId = mailboxId,
                Flag = flagColor,
                AlertConfigurationJson = alertConfigurationJson,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<FlagAlertConfiguration>> GetFlagAlertConfigsAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        CancellationToken cancellationToken = default)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
        _auth.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: false);

        return await _db.FlagAlertConfigurations
            .Where(f => f.TenantId == tenantId && f.MailboxId == mailboxId)
            .ToListAsync(cancellationToken);
    }

    private static string? GetPreview(string? bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return null;
        var clean = bodyText.Replace("\r\n", " ").Replace("\n", " ").Trim();
        return clean.Length <= 120 ? clean : clean[..120] + "...";
    }
}
