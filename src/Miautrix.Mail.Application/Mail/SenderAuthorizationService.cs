using System.Net.Mail;
using System.Text;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Application.Mail;

public sealed class SenderAuthorizationService(AppDbContext db) : ISenderAuthorizationService
{
    public Task<bool> HasSharedMailboxSendPermissionAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken ct = default) =>
        db.MailboxDelegates.AnyAsync(d => d.TenantId == tenantId && d.MailboxId == mailboxId && d.UserId == userId && (d.CanSendAs || d.CanSendOnBehalf), ct);

    public async Task<bool> IsEnvelopeSenderAllowedAsync(Guid tenantId, Guid userId, string sender, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sender) || sender == "<>" || !TrySingleAddress(sender, out var address))
        {
            await Reject(tenantId, userId, sender, "Envelope sender is invalid.", ct);
            return false;
        }
        if (await IsOwnAddressAsync(tenantId, userId, address, ct) || await HasSharedPermissionAsync(tenantId, userId, address, ct, sendAs: true, onBehalf: false)) return true;
        await Reject(tenantId, userId, address, "Envelope sender is not authorized.", ct);
        return false;
    }

    public async Task<SenderAuthorizationResult> AuthorizeMessageAsync(Guid tenantId, Guid userId, string envelopeSender, string rawMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawMessage)) return await Reject(tenantId, userId, envelopeSender, "Message is missing headers.", ct);
        var split = rawMessage.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (split < 0) split = rawMessage.IndexOf("\n\n", StringComparison.Ordinal);
        if (split < 0) return await Reject(tenantId, userId, envelopeSender, "Message is missing headers.", ct);
        var separatorLength = rawMessage.AsSpan(split).StartsWith("\r\n\r\n") ? 4 : 2;
        var headerText = rawMessage[..split];
        var body = rawMessage[(split + separatorLength)..];
        var headers = ParseHeaders(headerText);
        var fromValues = headers.Where(h => h.Name.Equals("From", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).ToList();
        if (fromValues.Count != 1 || !TryAddresses(fromValues[0], out var fromAddresses) || fromAddresses.Count == 0)
            return await Reject(tenantId, userId, "", "Missing or malformed From header.", ct);

        var own = await GetOwnAddressesAsync(tenantId, userId, ct);
        var needBehalf = false;
        foreach (var from in fromAddresses)
        {
            if (own.Contains(from)) continue;
            var permission = await GetSharedPermissionAsync(tenantId, userId, from, ct);
            if (permission == 1) continue;
            if (permission == 2) { needBehalf = true; continue; }
            return await Reject(tenantId, userId, from, "Sender address is not authorized.", ct);
        }

        if (fromAddresses.Count > 1 && !needBehalf)
            return await Reject(tenantId, userId, fromAddresses[0], "Multiple From addresses require Send on Behalf authorization.", ct);

        if (!TrySingleAddress(envelopeSender, out var envelope)) return await Reject(tenantId, userId, envelopeSender, "Envelope sender is not authorized.", ct);
        if (needBehalf)
        {
            if (!own.Contains(envelope)) return await Reject(tenantId, userId, envelope, "Send on Behalf requires the delegate's own envelope address.", ct);
            var ownMailbox = await db.Mailboxes.Where(m => m.TenantId == tenantId && m.Kind == "user" && m.Address.ToLower() == envelope.ToLower()).Select(m => m.Address).FirstOrDefaultAsync(ct);
            if (ownMailbox is null) return await Reject(tenantId, userId, envelope, "Delegate mailbox is unavailable.", ct);
            return new(true, envelope, ReplaceSenderHeader(rawMessage, split, separatorLength, ownMailbox));
        }

        var envelopeAllowed = own.Contains(envelope) || await HasSharedPermissionAsync(tenantId, userId, envelope, ct, sendAs: true, onBehalf: false);
        if (!envelopeAllowed) return await Reject(tenantId, userId, envelope, "Envelope sender is not authorized.", ct);
        if (!own.Contains(envelope) && !fromAddresses.Contains(envelope, StringComparer.OrdinalIgnoreCase))
            return await Reject(tenantId, userId, envelope, "Shared mailbox envelope sender must match the From header.", ct);
        var hasSender = headers.Any(h => h.Name.Equals("Sender", StringComparison.OrdinalIgnoreCase));
        return new(true, envelope, hasSender ? ReplaceSenderHeader(rawMessage, split, separatorLength, null) : rawMessage);
    }

    public async Task<SenderAuthorizationResult> AuthorizeWebmailAsync(Guid tenantId, Guid userId, string requestedFrom, string rawMessage, CancellationToken ct = default)
    {
        // The message's MIME From remains the requested identity; envelope defaults to that identity.
        // For Send on Behalf the policy changes the envelope to the user's mailbox.
        if (!TrySingleAddress(requestedFrom, out var requestedAddress))
            return await Reject(tenantId, userId, requestedFrom, "Requested From address is invalid.", ct);
        var own = await GetOwnAddressesAsync(tenantId, userId, ct);
        var behalf = await HasSharedPermissionAsync(tenantId, userId, requestedAddress, ct, sendAs: false, onBehalf: true);
        var envelope = behalf && !own.Contains(requestedAddress) ? own.FirstOrDefault() ?? "" : requestedAddress;
        return await AuthorizeMessageAsync(tenantId, userId, envelope, rawMessage, ct);
    }

    private async Task<HashSet<string>> GetOwnAddressesAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var addresses = await db.Mailboxes.Where(m => m.TenantId == tenantId && m.Kind == "user")
            .Join(db.Users.Where(u => u.TenantId == tenantId && u.Id == userId), m => m.Address.ToLower(), u => u.Email.ToLower(), (m, _) => m.Address).ToListAsync(ct);
        var normalized = addresses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (addresses.Count > 0)
        {
            var aliases = await db.Aliases.Where(a => a.TenantId == tenantId && addresses.Select(x => x.ToLower()).Contains(a.TargetAddress.ToLower())).Select(a => a.Address).ToListAsync(ct);
            normalized.UnionWith(aliases);
        }
        return normalized;
    }

    // 0=none, 1=Send As, 2=Send on Behalf; Send As takes precedence.
    private async Task<int> GetSharedPermissionAsync(Guid tenantId, Guid userId, string address, CancellationToken ct)
    {
        var grant = await db.Mailboxes.Where(m => m.TenantId == tenantId && m.Kind == "shared" && m.Address.ToLower() == address.ToLower())
            .Join(db.MailboxDelegates.Where(d => d.TenantId == tenantId && d.UserId == userId), m => m.Id, d => d.MailboxId, (_, d) => new { d.CanSendAs, d.CanSendOnBehalf }).FirstOrDefaultAsync(ct);
        return grant?.CanSendAs == true ? 1 : grant?.CanSendOnBehalf == true ? 2 : 0;
    }

    private async Task<bool> HasSharedPermissionAsync(Guid tenantId, Guid userId, string address, CancellationToken ct, bool sendAs, bool onBehalf)
    {
        var mode = await GetSharedPermissionAsync(tenantId, userId, address, ct);
        return (sendAs && mode == 1) || (onBehalf && mode is 1 or 2);
    }

    private async Task<bool> IsOwnAddressAsync(Guid tenantId, Guid userId, string address, CancellationToken ct) => (await GetOwnAddressesAsync(tenantId, userId, ct)).Contains(address);

    private async Task<SenderAuthorizationResult> Reject(Guid tenantId, Guid userId, string address, string reason, CancellationToken ct)
    {
        var domain = TrySingleAddress(address, out var parsed) ? parsed[(parsed.LastIndexOf('@') + 1)..] : "invalid";
        var now = DateTimeOffset.UtcNow;
        var details = System.Text.Json.JsonSerializer.Serialize(new { domain, reason });
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO audit_logs (id, tenant_id, actor_id, action, target_type, details_json, created_at, updated_at) VALUES ({Guid.NewGuid()}, {tenantId}, {userId}, {"sender.authorization.denied"}, {"sender"}, {details}, {now}, {now})", ct);
        return new(false, address, "", reason);
    }

    private static bool TrySingleAddress(string value, out string address)
    {
        address = "";
        try { var parsed = new MailAddress(value.Trim()); if (!parsed.Address.Contains('@')) return false; address = parsed.Address; return true; }
        catch { return false; }
    }

    private static bool TryAddresses(string value, out List<string> addresses)
    {
        addresses = [];
        try { var collection = new MailAddressCollection(); collection.Add(value); addresses = collection.Select(a => a.Address).ToList(); return true; }
        catch { return false; }
    }

    private sealed record Header(string Name, string Value);
    private static List<Header> ParseHeaders(string text)
    {
        var result = new List<Header>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && result.Count > 0) { var last = result[^1]; result[^1] = last with { Value = last.Value + " " + line.Trim() }; continue; }
            var colon = line.IndexOf(':'); if (colon > 0) result.Add(new(line[..colon].Trim(), line[(colon + 1)..].Trim()));
        }
        return result;
    }
    private static string ReplaceSenderHeader(string raw, int headerEnd, int separatorLength, string? sender)
    {
        var header = raw[..headerEnd];
        var lines = header.Split('\n');
        var output = new List<string>();
        var skipping = false;
        var replaced = false;
        foreach (var original in lines)
        {
            var line = original.TrimEnd('\r');
            if (line.StartsWith(' ') || line.StartsWith('\t'))
            {
                if (!skipping) output.Add(original);
                continue;
            }
            skipping = line.StartsWith("Sender:", StringComparison.OrdinalIgnoreCase);
            if (skipping) { if (!replaced && sender is not null) output.Add("Sender: " + sender + (original.EndsWith('\r') ? "\r" : "")); replaced = true; continue; }
            output.Add(original);
        }
        if (sender is not null && !replaced) output.Add("Sender: " + sender + (header.Contains("\r\n", StringComparison.Ordinal) ? "\r" : ""));
        var newHeader = string.Join("\n", output);
        return newHeader + raw.Substring(headerEnd, separatorLength) + raw[(headerEnd + separatorLength)..];
    }
}
