using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Application.Mail.Mime;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Sieve;
using Miautrix.Mail.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Miautrix.Mail.Application.Mail;

public sealed record SieveDeliveryDecision(bool StoreLocally = true, bool Discard = false, string? FolderName = null, Guid? FolderId = null, IReadOnlyList<string>? Flags = null, bool CopyToDefault = false);

public interface ISieveDeliveryService
{
    Task<SieveDeliveryDecision> EvaluateAsync(Mailbox mailbox, SmtpQueueItem item, ParsedMailMessage parsed, string defaultFolderRole, CancellationToken cancellationToken = default);
}

/// <summary>Executes the active per-mailbox script on accepted inbound delivery only.</summary>
public sealed class SieveDeliveryService : ISieveDeliveryService
{
    private const int RedirectLimit = 5;
    private const string RedirectCountHeader = "X-Miautrix-Sieve-Redirect-Count";
    private const string RedirectedToHeader = "X-Miautrix-Sieve-Redirected-To";
    private readonly AppDbContext _db;
    private readonly SieveCompiler _compiler;
    private readonly SieveInterpreter _interpreter;
    private readonly ISmtpQueueManager _queue;
    private readonly ILogger<SieveDeliveryService> _logger;

    public SieveDeliveryService(AppDbContext db, SieveCompiler compiler, SieveInterpreter interpreter, ISmtpQueueManager queue, ILogger<SieveDeliveryService> logger)
    {
        _db = db; _compiler = compiler; _interpreter = interpreter; _queue = queue; _logger = logger;
    }

    public async Task<SieveDeliveryDecision> EvaluateAsync(Mailbox mailbox, SmtpQueueItem item, ParsedMailMessage parsed, string defaultFolderRole, CancellationToken cancellationToken = default)
    {
        if (item.Direction != QueueDirection.Inbound || IsOwnDsn(item, parsed)) return new SieveDeliveryDecision();
        var script = await _db.SieveScripts.AsNoTracking().FirstOrDefaultAsync(
            s => s.TenantId == item.TenantId && s.MailboxId == mailbox.Id && s.IsActive, cancellationToken);
        if (script is null) return new SieveDeliveryDecision();

        var compilation = _compiler.Compile(script.Content);
        if (!compilation.Success)
        {
            _logger.LogWarning("Active Sieve script {ScriptId} failed compilation at line {Line}, column {Column}; using implicit keep.",
                script.Id, compilation.Diagnostic.ErrorLine, compilation.Diagnostic.ErrorColumn);
            return new SieveDeliveryDecision();
        }

        var headers = ParseHeaders(parsed.RawHeaders);
        var mailboxes = await _db.Folders.AsNoTracking().Where(f => f.TenantId == item.TenantId && f.MailboxId == mailbox.Id)
            .Select(f => f.Name).ToListAsync(cancellationToken);
        var context = new SieveMessageContext(headers, item.Sender, item.Recipient,
            Encoding.UTF8.GetByteCount(item.RawMessage), parsed.BodyText, new HashSet<string>(mailboxes, StringComparer.OrdinalIgnoreCase));
        var execution = _interpreter.Execute(compilation.Program!, context);
        if (execution.RuntimeError)
        {
            _logger.LogWarning("Sieve script {ScriptId} exceeded its execution budget or raised a runtime error; using implicit keep. Steps={Steps} TimedOut={TimedOut}.",
                script.Id, execution.Steps, execution.TimedOut);
            return new SieveDeliveryDecision();
        }

        var effects = execution.Effects;
        if (effects.Any(e => e.Name == "discard")) return new SieveDeliveryDecision(StoreLocally: false, Discard: true);

        foreach (var reject in effects.Where(e => e.Name is "reject" or "ereject"))
            await QueueRejectAsync(mailbox, item, parsed, reject.Values.FirstOrDefault() ?? "Rejected by mailbox filter", cancellationToken);

        foreach (var redirect in effects.Where(e => e.Name == "redirect"))
            await QueueRedirectsAsync(mailbox, item, parsed, redirect, cancellationToken);

        foreach (var vacation in effects.Where(e => e.Name == "vacation"))
            await TrySendVacationAsync(mailbox, item, parsed, headers, vacation, cancellationToken);

        var folderName = defaultFolderRole.Equals("junk", StringComparison.OrdinalIgnoreCase) ? "Junk" : "Inbox";
        Guid? folderId = null;
        var useSieveFolder = false;
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var effect in effects)
        {
            if (effect.Name == "setflag") { flags.Clear(); flags.UnionWith(effect.Flags); }
            else if (effect.Name == "addflag") flags.UnionWith(effect.Flags);
            else if (effect.Name == "removeflag") flags.ExceptWith(effect.Flags);
            else if (effect.Name == "keep") flags.UnionWith(effect.Flags);
            else if (effect.Name == "fileinto")
            {
                flags.UnionWith(effect.Flags);
                var folder = effect.Values.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(folder)) continue;
                var existing = await _db.Folders.FirstOrDefaultAsync(f => f.TenantId == item.TenantId && f.MailboxId == mailbox.Id && f.Name.ToLower() == folder.ToLower(), cancellationToken);
                if (existing is null && !effect.Tags.Contains(":create")) continue;
                if (existing is null)
                {
                    var created = new Folder { Id = Guid.NewGuid(), TenantId = item.TenantId, MailboxId = mailbox.Id, Name = folder, Role = "custom", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
                    _db.Folders.Add(created);
                    folderId = created.Id;
                }
                folderName = existing?.Name ?? folder;
                folderId ??= existing?.Id;
                useSieveFolder = true;
            }
        }

        var hasExplicitKeep = effects.Any(e => e.Name == "keep");
        var onlyRedirected = effects.Any(e => e.Name == "redirect") && !hasExplicitKeep && !useSieveFolder;
        var rejected = effects.Any(e => e.Name is "reject" or "ereject");
        var copyToDefault = useSieveFolder && effects.Any(e => e.Name == "fileinto" && e.Tags.Contains(":copy"));
        if (rejected) return new SieveDeliveryDecision(StoreLocally: false);
        return new SieveDeliveryDecision(StoreLocally: !onlyRedirected, FolderName: useSieveFolder ? folderName : null, FolderId: useSieveFolder ? folderId : null, Flags: flags.ToArray(), CopyToDefault: copyToDefault);
    }

    private async Task QueueRedirectsAsync(Mailbox mailbox, SmtpQueueItem item, ParsedMailMessage parsed, SieveEffect effect, CancellationToken ct)
    {
        var headers = ParseHeaders(parsed.RawHeaders);
        var count = headers.TryGetValue(RedirectCountHeader, out var countValues) && int.TryParse(countValues.LastOrDefault(), out var prior) ? prior : 0;
        if (count >= RedirectLimit) return;
        var visited = headers.TryGetValue(RedirectedToHeader, out var previous) ? previous.SelectMany(SplitAddresses).ToHashSet(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        visited.Add(mailbox.Address);
        foreach (var target in effect.Values.Where(IsMailboxAddress))
        {
            if (count >= RedirectLimit) break;
            if (target.Equals(mailbox.Address, StringComparison.OrdinalIgnoreCase) || visited.Contains(target)) continue;
            visited.Add(target);
            var raw = AddHeader(item.RawMessage, RedirectCountHeader, (count + 1).ToString()) ;
            raw = AddHeader(raw, RedirectedToHeader, string.Join(", ", visited));
            await _queue.EnqueueAsync(item.TenantId, item.Sender, target, raw, QueueDirection.Outbound, item.Subject, ct);
            count++;
        }
    }

    private async Task QueueRejectAsync(Mailbox mailbox, SmtpQueueItem item, ParsedMailMessage parsed, string reason, CancellationToken ct)
    {
        var sender = NormalizeAddress(item.Sender);
        if (!IsMailboxAddress(sender)) return;
        var status = "5.7.1";
        var reportingDomain = mailbox.Address[(mailbox.Address.LastIndexOf('@') + 1)..];
        var raw = DeliveryStatusNotificationBuilder.Build(sender, item.Recipient, status, "550", reportingDomain, DateTimeOffset.UtcNow);
        await _queue.EnqueueAsync(item.TenantId, string.Empty, sender, raw, QueueDirection.Outbound,
            "Delivery Status Notification (Failure)", ct);
        _logger.LogInformation("Sieve rejected inbound queue item {QueueItemId}; reason length={ReasonLength}.", item.Id, reason.Length);
    }

    private async Task TrySendVacationAsync(Mailbox mailbox, SmtpQueueItem item, ParsedMailMessage parsed,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers, SieveEffect effect, CancellationToken ct)
    {
        var sender = NormalizeAddress(item.Sender);
        if (!IsMailboxAddress(sender) || IsVacationSuppressed(sender, mailbox, parsed, headers)) return;
        var options = ReadVacationOptions(effect);
        var recipients = ExtractAddresses(parsed.To).Concat(ExtractAddresses(parsed.Cc)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recipient = NormalizeAddress(item.Recipient);
        var recipientListed = recipients.Contains(mailbox.Address) || recipients.Contains(recipient);
        var recipientConfigured = options.Addresses.Contains(mailbox.Address, StringComparer.OrdinalIgnoreCase) || options.Addresses.Contains(recipient, StringComparer.OrdinalIgnoreCase);
        if (!recipientListed && !recipientConfigured) return;
        if (await _db.Mailboxes.AsNoTracking().AnyAsync(m => m.TenantId == item.TenantId && m.Address.ToLower() == sender.ToLower(), ct) ||
            await _db.Aliases.AsNoTracking().AnyAsync(a => a.TenantId == item.TenantId && a.Address.ToLower() == sender.ToLower(), ct)) return;

        var handle = options.Handle.Length > 255 ? options.Handle[..255] : options.Handle;
        var senderHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sender.ToLowerInvariant()))).ToLowerInvariant();
        var response = await _db.SieveVacationResponses.FirstOrDefaultAsync(r => r.TenantId == item.TenantId && r.MailboxId == mailbox.Id && r.SenderHash == senderHash && r.Handle == handle, ct);
        var now = DateTimeOffset.UtcNow;
        if (response is not null && response.LastSentAt > now.AddDays(-options.Days)) return;

        var configuredFrom = ExtractAddresses(options.From).FirstOrDefault();
        var replyOptions = configuredFrom is not null && configuredFrom.Equals(mailbox.Address, StringComparison.OrdinalIgnoreCase)
            ? options
            : options with { From = mailbox.Address };
        var reply = BuildVacationReply(mailbox.Address, sender, parsed, headers, replyOptions);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _queue.EnqueueAsync(item.TenantId, string.Empty, sender, reply, QueueDirection.Outbound, options.Subject, ct);
        if (response is null)
        {
            _db.SieveVacationResponses.Add(new SieveVacationResponse { TenantId = item.TenantId, MailboxId = mailbox.Id, SenderHash = senderHash, Handle = handle, LastSentAt = now, CreatedAt = now, UpdatedAt = now });
        }
        else { response.LastSentAt = now; response.UpdatedAt = now; }
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static bool IsVacationSuppressed(string sender, Mailbox mailbox, ParsedMailMessage parsed, IReadOnlyDictionary<string, IReadOnlyList<string>> headers)
    {
        if (headers.TryGetValue("Auto-Submitted", out var auto) && auto.Any(value => !value.Trim().Equals("no", StringComparison.OrdinalIgnoreCase))) return true;
        if (headers.TryGetValue("Precedence", out var precedence) && precedence.SelectMany(v => v.Split(',')).Any(v =>
                v.Trim().Equals("bulk", StringComparison.OrdinalIgnoreCase) || v.Trim().Equals("list", StringComparison.OrdinalIgnoreCase) || v.Trim().Equals("junk", StringComparison.OrdinalIgnoreCase))) return true;
        if (headers.Keys.Any(name => name.StartsWith("List-", StringComparison.OrdinalIgnoreCase))) return true;
        if (sender.Equals(mailbox.Address, StringComparison.OrdinalIgnoreCase)) return true;
        var contentType = headers.TryGetValue("Content-Type", out var types) ? string.Join(" ", types) : string.Empty;
        return contentType.Contains("multipart/report", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(parsed.MessageId) && contentType.Contains("delivery-status", StringComparison.OrdinalIgnoreCase);
    }

    private static VacationOptions ReadVacationOptions(SieveEffect effect)
    {
        var days = 7; var subject = string.Empty; var from = string.Empty; var handle = string.Empty; IReadOnlyList<string> addresses = [];
        var groups = effect.ArgumentGroups ?? [];
        var index = 0;
        foreach (var tag in effect.Tags)
        {
            if (tag == ":mime") continue;
            if (index >= groups.Count) break;
            var group = groups[index++];
            if (tag == ":days" && int.TryParse(group.FirstOrDefault(), out var parsedDays)) days = Math.Clamp(parsedDays, 1, 30);
            else if (tag == ":subject") subject = group.FirstOrDefault() ?? string.Empty;
            else if (tag == ":from") from = group.FirstOrDefault() ?? string.Empty;
            else if (tag == ":handle") handle = group.FirstOrDefault() ?? string.Empty;
            else if (tag == ":addresses") addresses = group;
        }
        var body = groups.Skip(index).SelectMany(x => x).FirstOrDefault() ?? effect.Values.LastOrDefault() ?? string.Empty;
        return new VacationOptions(days, subject, from, handle, addresses, body, effect.Tags.Contains(":mime"));
    }

    private static string BuildVacationReply(string mailbox, string sender, ParsedMailMessage parsed,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers, VacationOptions options)
    {
        static string Safe(string value) => value.Replace("\r", " ").Replace("\n", " ");
        var originalSubject = parsed.Subject ?? string.Empty;
        var subject = options.Subject.Length > 0 ? options.Subject : originalSubject.StartsWith("Auto:", StringComparison.OrdinalIgnoreCase) ? originalSubject : "Auto: " + originalSubject;
        var messageId = parsed.MessageId;
        var references = headers.TryGetValue("References", out var refs) ? string.Join(" ", refs) : string.Empty;
        var inReplyTo = headers.TryGetValue("Message-ID", out var ids) ? ids.LastOrDefault() ?? messageId ?? string.Empty : messageId ?? string.Empty;
        var body = options.Body;
        return $"From: {Safe(options.From.Length > 0 ? options.From : mailbox)}\r\n" +
               $"To: {Safe(sender)}\r\nSubject: {Safe(subject)}\r\n" +
               "Auto-Submitted: auto-replied\r\n" +
               (inReplyTo.Length > 0 ? $"In-Reply-To: {Safe(inReplyTo)}\r\n" : string.Empty) +
               (string.Join(" ", references, inReplyTo).Trim().Length > 0 ? $"References: {Safe(string.Join(" ", references, inReplyTo).Trim())}\r\n" : string.Empty) +
               $"Message-ID: <{Guid.NewGuid():N}@{mailbox[(mailbox.LastIndexOf('@') + 1)..]}>\r\n" +
               "MIME-Version: 1.0\r\n" +
               (options.Mime ? "" : "Content-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: 8bit\r\n") +
               "\r\n" + body + "\r\n";
    }

    private static bool IsOwnDsn(SmtpQueueItem item, ParsedMailMessage parsed) =>
        NormalizeAddress(item.Sender).Length == 0 ||
        ParseHeaders(parsed.RawHeaders).TryGetValue("Auto-Submitted", out var values) && values.Any(value => value.Contains("auto-replied", StringComparison.OrdinalIgnoreCase)) ||
        ParseHeaders(parsed.RawHeaders).TryGetValue("Content-Type", out var types) && types.Any(value => value.Contains("multipart/report", StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, IReadOnlyList<string>> ParseHeaders(string rawHeaders)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in rawHeaders.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && result.Count > 0)
            {
                var key = result.Keys.Last(); result[key][^1] += " " + line.Trim(); continue;
            }
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var name = line[..colon].Trim();
            if (!result.TryGetValue(name, out var values)) result[name] = values = [];
            values.Add(line[(colon + 1)..].Trim());
        }
        return result.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string AddHeader(string message, string name, string value)
    {
        var separator = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (separator < 0) separator = message.IndexOf("\n\n", StringComparison.Ordinal);
        if (separator < 0) return $"{name}: {value}\r\n\r\n{message}";
        return message.Insert(separator, $"\r\n{name}: {value}");
    }

    private static IEnumerable<string> SplitAddresses(string raw) => ExtractAddresses(raw);
    private static IEnumerable<string> ExtractAddresses(string? raw) => raw is null ? [] : Regex.Matches(raw, @"[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase).Select(m => m.Value);
    private static string NormalizeAddress(string? value) => (value ?? string.Empty).Trim().Trim('<', '>').Trim();
    private static bool IsMailboxAddress(string value) => value.Contains('@') && !value.Contains('\r') && !value.Contains('\n');
    private sealed record VacationOptions(int Days, string Subject, string From, string Handle, IReadOnlyList<string> Addresses, string Body, bool Mime);
}
