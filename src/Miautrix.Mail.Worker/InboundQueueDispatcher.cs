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

namespace Miautrix.Mail.Worker;

public sealed class InboundQueueDispatcher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    private const string SpamSupectedReleasedTag = "[SPAM Supected-Released]";

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
        var antiMalwareScanner = scope.ServiceProvider.GetRequiredService<IAntiMalwareScanner>();
        var storage = scope.ServiceProvider.GetRequiredService<IMailStorage>();

        var pendingItems = await db.SmtpQueue
            .Where(q => q.Direction == "Inbound" && q.Status == "Pending" && q.NextAttemptAt <= DateTimeOffset.UtcNow)
            .OrderBy(q => q.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pendingItems.Count == 0) return;

        foreach (var item in pendingItems)
        {
            try
            {
                await ProcessItemAsync(db, quarantineService, antiMalwareScanner, storage, item, ct);
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

    private async Task ProcessItemAsync(AppDbContext db, IQuarantineService quarantineService, IAntiMalwareScanner antiMalwareScanner, IMailStorage storage, SmtpQueueItem item, CancellationToken ct)
    {
        var parsed = ParsedInboundMessage.Parse(item.RawMessage);
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
                    attachment.FileName,
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

        var deliveredRole = result.Score >= junkThreshold ? "junk" : "inbox";
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
            var allTenantMailboxes = await db.Mailboxes
                .Where(m => m.TenantId == item.TenantId)
                .Select(m => m.Address)
                .ToListAsync(ct);
            _logger.LogWarning(
                "No mailbox found for recipient {Recipient} in tenant {TenantId}. Searching for address: {Address}. Available mailboxes in tenant: {Addresses}",
                item.Recipient,
                item.TenantId,
                recipient,
                string.Join(", ", allTenantMailboxes));
            item.Status = "DeadLetter";
            item.LastError = $"Recipient mailbox not found: {item.Recipient}";
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
            Sender = item.Sender,
            Recipient = item.Recipient,
            Subject = subject,
            Date = parsed.Date?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
            ContentHash = storageResult.ContentHash,
            StoragePath = storageResult.StoragePath,
            SizeBytes = storageResult.SizeBytes,
            IsRead = false,
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
        public string? BodyHtml { get; private init; }
        public IReadOnlyList<ParsedAttachment> Attachments { get; private init; } = [];

        public static ParsedInboundMessage Parse(string rawMessage)
        {
            var normalized = rawMessage.Replace("\r\n", "\n").Replace('\r', '\n');
            var split = normalized.IndexOf("\n\n", StringComparison.Ordinal);
            var rawHeaders = split >= 0 ? normalized[..split] : normalized;
            var body = split >= 0 ? normalized[(split + 2)..] : string.Empty;
            var headers = ParseHeaders(rawHeaders);

            var attachments = ParseAttachments(headers, body);

            // Extract first text/plain body and first text/html body for multipart emails.
            // Fallback: ExtractTextBody for messages we can't parse as multipart.
            var bodyText = ExtractTextBody(body);
            var bodyHtml = ExtractHtmlBody(body);

            return new ParsedInboundMessage
            {
                RawHeaders = rawHeaders.Replace("\n", "\r\n"),
                Subject = headers.TryGetValue("subject", out var subject) ? subject : null,
                Date = headers.TryGetValue("date", out var date) && DateTimeOffset.TryParse(date, out var parsedDate) ? parsedDate : null,
                BodyText = bodyText,
                BodyHtml = bodyHtml,
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

        // NOTE: same parsing assumptions as ExtractHtmlBody below.
        private static string? ExtractTextBody(string body) => ExtractTextBody(body, 0);

        private static string? ExtractHtmlBody(string body, int depth)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            if (depth > 5) return null;

            var split = body.StartsWith("--", StringComparison.Ordinal)
                ? 0
                : body.IndexOf("\n--", StringComparison.Ordinal);

            if (split > 0)
            {
                // IndexOf("\n--") points at the leading newline; shift to the boundary marker.
                split += 1;
            }

            if (split < 0)
            {
                var candidate = body.Trim();
                if (candidate.StartsWith("<", StringComparison.OrdinalIgnoreCase) && candidate.Contains("</", StringComparison.OrdinalIgnoreCase))
                    return candidate;
                return null;
            }

            var firstDelim = body[split..];
            var delimLineEnd = firstDelim.IndexOf('\n');
            if (delimLineEnd < 0) return null;

            var delimLine = firstDelim[..delimLineEnd].Trim(); // "--boundary" or "--boundary--"
            if (!delimLine.StartsWith("--")) return null;

            var boundary = delimLine.Substring(2);
            if (string.IsNullOrWhiteSpace(boundary)) return null;

            var delimiter = "--" + boundary;
            foreach (var rawPart in body.Split(delimiter, StringSplitOptions.None))
            {
                var part = rawPart.Trim('\n');
                if (part.Length == 0 || part.StartsWith("--", StringComparison.Ordinal)) continue;

                var partSplit = part.IndexOf("\n\n", StringComparison.Ordinal);
                if (partSplit < 0) continue;

                var partHeaders = ParseHeaders(part[..partSplit]);
                var partBody = part[(partSplit + 2)..].Trim('\n');

                if (!partHeaders.TryGetValue("content-type", out var contentType)) continue;

                // Direct text/html.
                if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    var transferEncoding = partHeaders.TryGetValue("content-transfer-encoding", out var cte) ? cte : string.Empty;
                    var html = transferEncoding.Equals("base64", StringComparison.OrdinalIgnoreCase)
                        ? Encoding.UTF8.GetString(DecodeBase64(partBody))
                        : partBody;

                    var trimmed = html.Trim();
                    return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
                }

                // Nested multipart.
                if (contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase))
                {
                    var transferEncoding = partHeaders.TryGetValue("content-transfer-encoding", out var cte) ? cte : string.Empty;
                    var nested = transferEncoding.Equals("base64", StringComparison.OrdinalIgnoreCase)
                        ? Encoding.UTF8.GetString(DecodeBase64(partBody))
                        : partBody;

                    var extracted = ExtractHtmlBody(nested, depth + 1);
                    if (extracted is not null) return extracted;
                }
            }

            return null;
        }

        private static string? ExtractHtmlBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            return ExtractHtmlBody(body, 0);
        }

        private static string? ExtractTextBody(string body, int depth)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            if (depth > 5) return null;

            // Best-effort extraction:
            // - multipart/*: find the first direct text/plain part and decode it (recursing into nested multiparts).
            // - non-multipart: treat everything after headers as text.
            // Some emails may start multipart boundaries immediately after the initial headers,
            // so handle both "--boundary" at position 0 and the more common "\n--boundary" case.
            var split = body.StartsWith("--", StringComparison.Ordinal)
                ? 0
                : body.IndexOf("\n--", StringComparison.Ordinal);

            // Non-multipart fallback.
            if (split < 0)
            {
                var candidate = body.Trim();
                return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
            }

            // multipart: infer boundary from the first delimiter.
            var firstDelim = body[split..];
            var delimLineEnd = firstDelim.IndexOf('\n');
            if (delimLineEnd < 0) return null;

            var delimLine = firstDelim[..delimLineEnd].Trim(); // "--boundary" or "--boundary--"
            if (!delimLine.StartsWith("--")) return null;

            var boundary = delimLine.Substring(2);
            if (string.IsNullOrWhiteSpace(boundary)) return null;

            var delimiter = "--" + boundary;
            foreach (var rawPart in body.Split(delimiter, StringSplitOptions.None))
            {
                var part = rawPart.Trim('\n');
                if (part.Length == 0 || part.StartsWith("--", StringComparison.Ordinal)) continue;

                var partSplit = part.IndexOf("\n\n", StringComparison.Ordinal);
                if (partSplit < 0) continue;

                var partHeaders = ParseHeaders(part[..partSplit]);
                var partBody = part[(partSplit + 2)..].Trim('\n');

                if (!partHeaders.TryGetValue("content-type", out var contentType)) continue;

                // Direct text/plain.
                if (contentType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
                {
                    var transferEncoding = partHeaders.TryGetValue("content-transfer-encoding", out var cte) ? cte : string.Empty;

                    var text = transferEncoding.Equals("base64", StringComparison.OrdinalIgnoreCase)
                        ? Encoding.UTF8.GetString(DecodeBase64(partBody))
                        : partBody;

                    var trimmed = text.Trim();
                    return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
                }

                // Nested multipart (e.g. multipart/mixed -> multipart/alternative -> text/plain).
                if (contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase))
                {
                    var transferEncoding = partHeaders.TryGetValue("content-transfer-encoding", out var cte) ? cte : string.Empty;
                    var nested = transferEncoding.Equals("base64", StringComparison.OrdinalIgnoreCase)
                        ? Encoding.UTF8.GetString(DecodeBase64(partBody))
                        : partBody;

                    var extracted = ExtractTextBody(nested, depth + 1);
                    if (extracted is not null) return extracted;
                }
            }

            return null;
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
