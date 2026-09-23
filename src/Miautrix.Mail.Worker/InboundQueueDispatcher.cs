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
using Miautrix.Mail.AntiSpam;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Storage;

namespace Miautrix.Mail.Worker;

public sealed class InboundQueueDispatcher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InboundQueueDispatcher> _logger;

    public InboundQueueDispatcher(IServiceScopeFactory scopeFactory, ILogger<InboundQueueDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quarantineService = scope.ServiceProvider.GetRequiredService<IQuarantineService>();
        var storage = scope.ServiceProvider.GetRequiredService<IMailStorage>();

        var pendingItems = await db.SmtpQueue
            .Where(q => q.Status == "Pending" && q.NextAttemptAt <= DateTimeOffset.UtcNow)
            .OrderBy(q => q.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pendingItems.Count == 0) return;

        foreach (var item in pendingItems)
        {
            try
            {
                await ProcessItemAsync(db, quarantineService, storage, item, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process inbound message {ItemId} for {Recipient}", item.Id, item.Recipient);
                item.Status = "Failed";
                item.LastError = ex.Message;
                item.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ProcessItemAsync(AppDbContext db, IQuarantineService quarantineService, IMailStorage storage, SmtpQueueItem item, CancellationToken ct)
    {
        var parsed = ParsedInboundMessage.Parse(item.RawMessage);
        var subject = item.Subject ?? parsed.Subject ?? "No Subject";

        var mailContext = new InboundMailContext(
            Sender: item.Sender,
            Recipient: item.Recipient,
            ClientIp: null,
            Subject: subject,
            RawMessage: item.RawMessage);

        var result = await quarantineService.ProcessInboundMessageAsync(item.TenantId, mailContext, cancellationToken: ct);

        if (!result.Delivered)
        {
            item.Status = result.Quarantined ? "Quarantined" : "Failed";
            item.UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

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

        var folder = await db.Folders
            .FirstOrDefaultAsync(f => f.TenantId == item.TenantId && f.MailboxId == mailbox.Id && f.Role == "inbox", ct);

        if (folder == null)
        {
            folder = new Folder
            {
                Id = Guid.NewGuid(),
                TenantId = item.TenantId,
                MailboxId = mailbox.Id,
                Name = "Inbox",
                Role = "inbox",
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
            Sender = item.Sender,
            Recipient = item.Recipient,
            Subject = subject,
            Date = parsed.Date ?? DateTimeOffset.UtcNow,
            ContentHash = storageResult.ContentHash,
            StoragePath = storageResult.StoragePath,
            SizeBytes = storageResult.SizeBytes,
            IsRead = false,
            RawHeaders = parsed.RawHeaders,
            BodyText = parsed.BodyText,
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
                FileName = parsedAttachment.FileName,
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

    private sealed record ParsedAttachment(string FileName, string ContentType, byte[] Content);

    private sealed class ParsedInboundMessage
    {
        public string RawHeaders { get; private init; } = string.Empty;
        public string? Subject { get; private init; }
        public DateTimeOffset? Date { get; private init; }
        public string? BodyText { get; private init; }
        public IReadOnlyList<ParsedAttachment> Attachments { get; private init; } = [];

        public static ParsedInboundMessage Parse(string rawMessage)
        {
            var normalized = rawMessage.Replace("\r\n", "\n").Replace('\r', '\n');
            var split = normalized.IndexOf("\n\n", StringComparison.Ordinal);
            var rawHeaders = split >= 0 ? normalized[..split] : normalized;
            var body = split >= 0 ? normalized[(split + 2)..] : string.Empty;
            var headers = ParseHeaders(rawHeaders);

            var attachments = ParseAttachments(headers, body);

            return new ParsedInboundMessage
            {
                RawHeaders = rawHeaders.Replace("\n", "\r\n"),
                Subject = headers.TryGetValue("subject", out var subject) ? subject : null,
                Date = headers.TryGetValue("date", out var date) && DateTimeOffset.TryParse(date, out var parsedDate) ? parsedDate : null,
                BodyText = ExtractTextBody(body),
                Attachments = attachments
            };
        }

        private static Dictionary<string, string> ParseHeaders(string rawHeaders)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? currentName = null;

            foreach (var line in rawHeaders.Split('\n'))
            {
                if ((line.StartsWith(' ') || line.StartsWith('\t')) && currentName is not null)
                {
                    headers[currentName] += " " + line.Trim();
                    continue;
                }

                var colon = line.IndexOf(':');
                if (colon <= 0) continue;

                currentName = line[..colon].Trim().ToLowerInvariant();
                headers[currentName] = line[(colon + 1)..].Trim();
            }

            return headers;
        }

        private static List<ParsedAttachment> ParseAttachments(Dictionary<string, string> headers, string body)
        {
            var attachments = new List<ParsedAttachment>();
            if (!headers.TryGetValue("content-type", out var contentTypeHeader)) return attachments;

            var boundary = GetParameter(contentTypeHeader, "boundary");
            if (string.IsNullOrWhiteSpace(boundary)) return attachments;

            var delimiter = "--" + boundary;
            foreach (var rawPart in body.Split(delimiter, StringSplitOptions.None))
            {
                var part = rawPart.Trim('\n');
                if (part.Length == 0 || part.StartsWith("--", StringComparison.Ordinal)) continue;

                var split = part.IndexOf("\n\n", StringComparison.Ordinal);
                if (split < 0) continue;

                var partHeaders = ParseHeaders(part[..split]);
                var partBody = part[(split + 2)..].Trim('\n');

                if (!partHeaders.TryGetValue("content-disposition", out var disposition) ||
                    !disposition.Contains("attachment", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = GetParameter(disposition, "filename")
                    ?? (partHeaders.TryGetValue("content-type", out var ct) ? GetParameter(ct, "name") : null)
                    ?? "attachment";
                var contentType = partHeaders.TryGetValue("content-type", out var partContentType)
                    ? partContentType.Split(';', 2)[0].Trim()
                    : "application/octet-stream";

                var transferEncoding = partHeaders.TryGetValue("content-transfer-encoding", out var cte) ? cte : string.Empty;
                var bytes = transferEncoding.Equals("base64", StringComparison.OrdinalIgnoreCase)
                    ? DecodeBase64(partBody)
                    : Encoding.UTF8.GetBytes(partBody.Replace("\n", "\r\n"));

                attachments.Add(new ParsedAttachment(fileName, contentType, bytes));
            }

            return attachments;
        }

        private static string? ExtractTextBody(string body)
        {
            var split = body.IndexOf("\n--", StringComparison.Ordinal);
            var candidate = split >= 0 ? body[..split] : body;
            return string.IsNullOrWhiteSpace(candidate) ? null : candidate.Trim();
        }

        private static string? GetParameter(string headerValue, string name)
        {
            // Very small header parameter parser.
            // Works for: boundary="..." and boundary=...
            var escapedName = Regex.Escape(name);
            var pattern = "(?:^|;)\\s*" + escapedName + "\\s*=\\s*\\\"?([^\\\";]+)\\\"?";
            var match = Regex.Match(headerValue, pattern, RegexOptions.IgnoreCase);

            if (!match.Success) return null;
            return match.Groups[1].Value.Trim();
        }

        private static byte[] DecodeBase64(string value)
        {
            var compact = Regex.Replace(value, "\\s+", string.Empty);
            return compact.Length == 0 ? [] : Convert.FromBase64String(compact);
        }
    }
}
