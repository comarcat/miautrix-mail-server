using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Miautrix.Mail.AntiMalware;
using Miautrix.Mail.AntiSpam;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Application.Mail.Mime;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.Worker;

public sealed class InboundQueueDispatcher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    private const string SpamSupectedReleasedTag = "[SPAM Supected-Released]";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InboundQueueDispatcher> _logger;
    private readonly InboundAuthenticationOptions? _inboundAuthenticationOptions;

    public InboundQueueDispatcher(IServiceScopeFactory scopeFactory, ILogger<InboundQueueDispatcher> logger, InboundAuthenticationOptions? inboundAuthenticationOptions = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _inboundAuthenticationOptions = inboundAuthenticationOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inbound Queue Dispatcher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing inbound queue.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    internal async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        List<Guid> pendingItemIds;
        using (var listScope = _scopeFactory.CreateScope())
        {
            var listDb = listScope.ServiceProvider.GetRequiredService<AppDbContext>();
            pendingItemIds = await listDb.SmtpQueue
                .Where(q => q.Direction == QueueDirection.Inbound && q.Status == "Pending" && q.NextAttemptAt <= DateTimeOffset.UtcNow)
                .OrderBy(q => q.CreatedAt)
                .Take(BatchSize)
                .Select(q => q.Id)
                .ToListAsync(ct);
        }

        if (pendingItemIds.Count == 0) return;

        // Each item gets its own scope (and therefore its own AppDbContext/ChangeTracker and
        // database transaction). One item's processing or persistence failure can then only ever
        // affect that item's own SaveChangesAsync call; it cannot roll back, detach, or otherwise
        // hide the result of any other item in the same batch -- including items processed earlier
        // or later in this same pass.
        foreach (var itemId in pendingItemIds)
        {
            using var itemScope = _scopeFactory.CreateScope();
            var db = itemScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var quarantineService = itemScope.ServiceProvider.GetRequiredService<IQuarantineService>();
            var antiMalwareScanner = itemScope.ServiceProvider.GetRequiredService<IAntiMalwareScanner>();
            var storage = itemScope.ServiceProvider.GetRequiredService<IMailStorage>();
            var mimeParser = itemScope.ServiceProvider.GetRequiredService<IMimeMessageParser>();
            var uidAllocator = itemScope.ServiceProvider.GetRequiredService<IMessageUidAllocator>();

            var item = await db.SmtpQueue.FirstOrDefaultAsync(q => q.Id == itemId, ct);
            if (item == null) continue;

            try
            {
                await ProcessItemAsync(db, quarantineService, antiMalwareScanner, storage, mimeParser, uidAllocator, item, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process inbound message {ItemId} for {Recipient}", item.Id, item.Recipient);

                // Discard every change this item's failed attempt made (new Message/Quarantine/
                // MalwareVerdict rows, folder creation, etc.) and record the failure on a clean
                // tracked copy of just this item, in its own dedicated scope/context.
                foreach (var entry in db.ChangeTracker.Entries().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                db.Attach(item);
                item.Status = "Failed";
                item.LastError = ex.Message;
                item.UpdatedAt = DateTimeOffset.UtcNow;

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to record failure status for inbound message {ItemId} for {Recipient}", item.Id, item.Recipient);
                }
            }
        }
    }

    private async Task ProcessItemAsync(AppDbContext db, IQuarantineService quarantineService, IAntiMalwareScanner antiMalwareScanner, IMailStorage storage, IMimeMessageParser mimeParser, IMessageUidAllocator uidAllocator, SmtpQueueItem item, CancellationToken ct)
    {
        var parsed = mimeParser.Parse(item.RawMessage);
        var subject = item.Subject ?? parsed.Subject ?? "No Subject";

        var recipientDomain = item.Recipient.Split('@').LastOrDefault()?.Trim().ToLowerInvariant();
        var domain = recipientDomain is null
            ? null
            : await db.Domains.FirstOrDefaultAsync(d => d.TenantId == item.TenantId && d.Name.ToLower() == recipientDomain, ct);

        if (domain?.MalwareScanningEnabled == true && parsed.Attachments.Count > 0)
        {
            foreach (var attachment in parsed.Attachments)
            {
                var policyReason = AttachmentPolicy.GetPolicyBlockReason(
                    attachment.FileName ?? "attachment",
                    attachment.Content.LongLength,
                    domain.MalwareBlockExecutables,
                    domain.MalwareBlockMacros,
                    domain.MalwareBlockEncryptedArchives,
                    domain.MalwareMaxFileSizeBytes);

                var scan = policyReason is null
                    ? await antiMalwareScanner.ScanAsync(attachment.Content, ct)
                    : new AntiMalwareScanResult(true, true, policyReason, "policy", "blocked", policyReason, 0);

                db.MalwareVerdicts.Add(new MalwareVerdict
                {
                    Id = Guid.NewGuid(),
                    TenantId = item.TenantId,
                    Sender = item.Sender,
                    Recipient = item.Recipient,
                    IsMalware = scan.IsMalware,
                    ThreatName = scan.ThreatName,
                    Engine = scan.Engine,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                if (scan.IsMalware)
                {
                    var detectedAction = domain.MalwareDetectedAction ?? "quarantine";

                    if (string.Equals(detectedAction, "discard", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "Discarding malware-detected message {QueueItemId} for {Recipient} (action={DetectedAction}).",
                            item.Id,
                            item.Recipient,
                            detectedAction);

                        // Discard means remove from the SMTP queue so it does not remain locked in
                        // Pending/Retry buckets.
                        db.SmtpQueue.Remove(item);
                        return;
                    }

                    var quarantine = new QuarantineItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = item.TenantId,
                        Sender = item.Sender,
                        Recipient = item.Recipient,
                        Subject = subject,
                        RawMessage = item.RawMessage,
                        SpamScore = 0,
                        Threshold = 0,
                        ReasonsJson = $"[{{\"RuleName\":\"{scan.ThreatName ?? scan.ErrorCode ?? "MALWARE_DETECTED"}\",\"Score\":0,\"Reason\":\"Attachment blocked by anti-malware policy\"}}]",
                        Status = "Quarantined",
                        QuarantinedAt = DateTimeOffset.UtcNow,
                        IsDelivered = false,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    db.Quarantine.Add(quarantine);
                    item.Status = "Quarantined";
                    item.LastError = scan.ThreatName ?? scan.ErrorCode ?? "Malware detected.";
                    item.UpdatedAt = DateTimeOffset.UtcNow;
                    return;
                }
            }
        }

        var mailContext = new InboundMailContext(
            Sender: item.Sender,
            Recipient: item.Recipient,
            ClientIp: null,
            Subject: subject,
            RawMessage: item.RawMessage);

        // Spam routing:
        // - If high-confidence: quarantine (existing flow)
        // - If suspicious: deliver into the mailbox Junk folder
        // - Else: deliver into Inbox
        //
        // Thresholds are domain-configurable.
        var junkThreshold = domain?.SpamHeaderScore ?? 6.0;
        var quarantineThreshold = domain?.SpamQuarantineScore ?? 10.0;

        // If the admin explicitly released a quarantined spam message, prevent it from being re-quarantined.
        // AdminService tags the subject with the exact prefix below before enqueuing.
        var quarantineEvalThreshold = item.Subject != null && item.Subject.Contains(SpamSupectedReleasedTag, StringComparison.OrdinalIgnoreCase)
            ? 1000.0
            : quarantineThreshold;

        var result = await quarantineService.ProcessInboundMessageAsync(
            item.TenantId,
            mailContext,
            threshold: quarantineEvalThreshold,
            cancellationToken: ct);

        if (!result.Delivered)
        {
            item.Status = result.Quarantined ? "Quarantined" : "Failed";
            item.UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        var dmarcQuarantine = _inboundAuthenticationOptions?.Mode == AuthenticationMode.Enforce &&
            InboundAuthenticationHeaderParser.IsOwnDmarcQuarantine(item.RawMessage, _inboundAuthenticationOptions.AuthservId);
        var deliveredRole = dmarcQuarantine ? "junk" : result.Score >= junkThreshold ? "junk" : "inbox";
        var _ = result; // keep result in scope for the following role decision

        // Continue with folder selection using deliveredRole

        // NOTE: folder creation is handled below.

        // (result variable is kept above; this is a no-op to avoid accidental scoping edits)

        // ReSharper disable once UnusedVariable



        var recipient = item.Recipient.Trim().ToLowerInvariant();
        var mailbox = await db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == item.TenantId && m.Address.ToLower() == recipient, ct);

        if (mailbox == null)
        {
            _logger.LogWarning(
                "No mailbox found for recipient {Recipient} in tenant {TenantId}",
                item.Recipient,
                item.TenantId);
            item.Status = "DeadLetter";
            item.LastError = "Recipient mailbox not found.";
            item.UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        // Reception issues (like missing mailbox or quota exceeded) must not look like outbound
        // transport failures. DeadLetter is the final state for these inbox problems.
        var rawMessageBytes = Encoding.UTF8.GetBytes(item.RawMessage ?? string.Empty);
        var estimatedMessageSizeBytes = rawMessageBytes.Length;
        if (mailbox.UsedBytes + estimatedMessageSizeBytes > mailbox.QuotaBytes)
        {
            _logger.LogWarning(
                "Mailbox quota exceeded for recipient {Recipient} in tenant {TenantId}. UsedBytes={UsedBytes}, QuotaBytes={QuotaBytes}, IncomingBytes={IncomingBytes}",
                item.Recipient,
                item.TenantId,
                mailbox.UsedBytes,
                mailbox.QuotaBytes,
                estimatedMessageSizeBytes);

            item.Status = "DeadLetter";
            item.LastError = "Mailbox quota exceeded.";
            item.UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        var desiredRole = deliveredRole;
        var dmarcFlagFallback = false;
        if (dmarcQuarantine)
        {
            var junkFolderExists = await db.Folders.AnyAsync(f => f.TenantId == item.TenantId && f.MailboxId == mailbox.Id && f.Role == "junk", ct);
            if (!junkFolderExists)
            {
                desiredRole = "inbox";
                dmarcFlagFallback = true;
            }
        }

        var folder = await db.Folders
            .FirstOrDefaultAsync(f => f.TenantId == item.TenantId && f.MailboxId == mailbox.Id && f.Role == desiredRole, ct);

        if (folder == null)
        {
            var folderName = desiredRole.ToLowerInvariant() switch
            {
                "inbox" => "Inbox",
                "junk" => "Junk",
                _ => desiredRole
            };

            folder = new Folder
            {
                Id = Guid.NewGuid(),
                TenantId = item.TenantId,
                MailboxId = mailbox.Id,
                Name = folderName,
                Role = desiredRole,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Folders.Add(folder);
        }




        using var contentStream = new MemoryStream(rawMessageBytes);
        var storageResult = await storage.StoreAsync(contentStream, ct);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = item.TenantId,
            MailboxId = mailbox.Id,
            FolderId = folder.Id,
            Uid = await uidAllocator.AllocateAsync(item.TenantId, folder.Id, cancellationToken: ct),
            Sender = item.Sender,
            Recipient = item.Recipient,
            Subject = subject,
            Date = parsed.Date ?? DateTimeOffset.UtcNow,
            ContentHash = storageResult.ContentHash,
            StoragePath = storageResult.StoragePath,
            SizeBytes = storageResult.SizeBytes,
            IsRead = false,
            Flags = dmarcFlagFallback ? "\\Flagged" : string.Empty,
            RawHeaders = parsed.RawHeaders,
            BodyText = parsed.BodyText,
            BodyHtml = parsed.BodyHtml,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Messages.Add(message);

        foreach (var parsedAttachment in parsed.Attachments)
        {
            await using var attachmentStream = new MemoryStream(parsedAttachment.Content);
            var attachmentStorage = await storage.StoreAsync(attachmentStream, ct);
            db.Attachments.Add(new Attachment
            {
                Id = Guid.NewGuid(),
                TenantId = item.TenantId,
                MessageId = message.Id,
                FileName = parsedAttachment.FileName ?? "attachment",
                ContentType = parsedAttachment.ContentType,
                SizeBytes = attachmentStorage.SizeBytes,
                ContentHash = attachmentStorage.ContentHash,
                StoragePath = attachmentStorage.StoragePath,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        mailbox.UsedBytes += storageResult.SizeBytes;

        item.Status = "Delivered";
        item.UpdatedAt = DateTimeOffset.UtcNow;
    }

}
