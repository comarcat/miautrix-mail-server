using System.Text;
using System.Text.RegularExpressions;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Application.Mail.Mime;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Protocols.Imap;

public enum ImapState
{
    NotAuthenticated,
    Authenticated,
    Selected,
    LoggedOut
}

public sealed record ImapResponseSegment(byte[] Bytes)
{
    public static ImapResponseSegment Line(string line) => new(Encoding.UTF8.GetBytes(line + "\r\n"));
    public static ImapResponseSegment Data(byte[] bytes) => new(bytes);
}

public sealed record ImapCommandResult(string Tag, string Status, string Message, IReadOnlyList<ImapResponseSegment> ResponseSegments)
{
    public ImapCommandResult(string tag, string status, string message, IReadOnlyList<string> untaggedResponses)
        : this(tag, status, message, untaggedResponses.Select(ImapResponseSegment.Line).ToArray()) { }
}

public sealed partial class ImapSession
{
    private sealed record ImapSequenceMessage(Guid Id, uint Uid, string Flags, bool IsRead);
    private sealed record ImapFetchMessage(Guid Id, uint Uid, string Flags, bool IsRead, DateTimeOffset Date, long SizeBytes, string ContentHash);
    private sealed record ImapSearchMessage(Guid Id, uint Uid, string Flags, bool IsRead, string Sender, string Recipient, string Subject, DateTimeOffset Date, long SizeBytes, string RawHeaders, string? BodyText, string? BodyHtml);
    private sealed record ImapSearchRecipient(Guid MessageId, string Type, string Address);
    private readonly AppDbContext _dbContext;
    private readonly IMailStorage _storage;
    private readonly IImapAuthenticator _authenticator;
    private readonly ITenantAuthorizationHelper _authorization;
    private readonly IMailboxService? _mailboxService;
    private readonly IMessageService? _messageService;
    private readonly IImapMimeFormatter? _mimeFormatter;
    private Guid? _userId;

    public Guid? TenantId { get; private set; }
    public Guid? MailboxId { get; private set; }
    public string? Username { get; private set; }
    public Folder? SelectedFolder { get; private set; }
    public ImapState State { get; private set; } = ImapState.NotAuthenticated;
    public bool SelectedReadOnly { get; private set; }
    public const string CapabilityLine = "* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UNSELECT UIDPLUS SPECIAL-USE";
    public bool AwaitingSaslContinuation { get; private set; }
    private string? _saslTag;

    public ImapSession(AppDbContext dbContext, IMailStorage storage, IImapAuthenticator authenticator,
        ITenantAuthorizationHelper authorization, IMailboxService? mailboxService = null, IMessageService? messageService = null, IImapMimeFormatter? mimeFormatter = null)
    {
        _dbContext = dbContext;
        _storage = storage;
        _authenticator = authenticator;
        _authorization = authorization;
        _mailboxService = mailboxService;
        _messageService = messageService;
        _mimeFormatter = mimeFormatter;
    }

    public async Task<ImapCommandResult> ExecuteCommandAsync(
        string commandLine,
        Stream? literalPayload = null,
        CancellationToken cancellationToken = default)
    {
        byte[]? literal = null;
        if (literalPayload is not null)
        {
            using var buffer = new MemoryStream();
            await literalPayload.CopyToAsync(buffer, cancellationToken);
            literal = buffer.ToArray();
        }
        try
        {
            var source = commandLine;
            if (literal is not null)
            {
                if (TryFindTrailingLiteralMarker(source, out var markerStart))
                    source = source[..markerStart] + "\u00010\u0002";
            }
            var command = ImapCommandTokenizer.Parse(source, literal is null ? null : [literal]);
            return await ExecuteCommandAsync(command, cancellationToken);
        }
        catch (FormatException)
        {
            return new ImapCommandResult("*", "BAD", "Invalid command format", Array.Empty<string>());
        }
    }

    private static bool TryFindTrailingLiteralMarker(string source, out int start)
    {
        start = -1;
        var quoted = false;
        var escaped = false;
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (ch == '\\') escaped = true;
                else if (ch == '"') quoted = false;
                continue;
            }
            if (ch == '"') { quoted = true; continue; }
            if (ch != '{') continue;
            var end = i + 1;
            while (end < source.Length && char.IsAsciiDigit(source[end])) end++;
            if (end == i + 1) continue;
            if (end < source.Length && source[end] == '+') end++;
            if (end == source.Length - 1 && source[end] == '}') start = i;
        }
        return start >= 0;
    }

    public async Task<ImapCommandResult> ExecuteCommandAsync(ImapCommand command, CancellationToken cancellationToken = default)
    {
        var tag = command.Tag;
        var commandName = command.Name;
        var args = command.Arguments;
        var arguments = ImapCommandTokenizer.JoinValues(args);
        var literal = args.FirstOrDefault(t => t.Kind == ImapTokenKind.Literal)?.LiteralBytes;
        using var literalStream = literal is null ? null : new MemoryStream(literal, writable: false);

        if (!ImapStateMachine.IsAllowed(State, commandName))
            return new ImapCommandResult(tag, "BAD", $"{commandName} is not valid in the {State} state", Array.Empty<string>());

        if (State is ImapState.Authenticated or ImapState.Selected && commandName is not "LOGIN" and not "CAPABILITY" and not "LOGOUT")
        {
            var operation = commandName == "UID" && args.Count > 0 ? args[0].Text.ToUpperInvariant() : commandName;
            var write = operation is "APPEND" or "STORE" or "EXPUNGE" or "COPY" or "CREATE" or "DELETE" or "RENAME";
            if (!await HasMailboxAccessAsync(write, cancellationToken))
                return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        }

        try
        {
        switch (commandName)
        {
            case "CAPABILITY":
                return new ImapCommandResult(
                    tag, "OK", "CAPABILITY completed",
                    new[] { CapabilityLine });

            case "NOOP":
                return new ImapCommandResult(tag, "OK", "NOOP completed", Array.Empty<string>());

            case "LOGOUT":
                State = ImapState.LoggedOut;
                return new ImapCommandResult(
                    tag, "OK", "LOGOUT completed",
                    new[] { "* BYE Miautrix Mail Server logging out" });

            case "LOGIN":
                return await HandleLoginAsync(tag, args, cancellationToken);

            case "AUTHENTICATE":
                return await HandleAuthenticateAsync(tag, args, cancellationToken);

            case "SELECT":
                return await HandleSelectAsync(tag, args.FirstOrDefault()?.Text ?? string.Empty, false, cancellationToken);

            case "EXAMINE":
                return await HandleSelectAsync(tag, args.FirstOrDefault()?.Text ?? string.Empty, true, cancellationToken);

            case "LIST":
                return await HandleListAsync(tag, args, cancellationToken);

            case "LSUB":
                return await HandleListAsync(tag, args, cancellationToken, subscribed: true);

            case "SUBSCRIBE":
            case "UNSUBSCRIBE":
                return new ImapCommandResult(tag, "OK", $"{commandName} completed", Array.Empty<string>());

            case "STATUS":
                return await HandleStatusAsync(tag, args, cancellationToken);

            case "CHECK":
                return State == ImapState.Selected
                    ? new ImapCommandResult(tag, "OK", "CHECK completed", Array.Empty<string>())
                    : new ImapCommandResult(tag, "NO", "No mailbox selected", Array.Empty<string>());

            case "UNSELECT":
                SelectedFolder = null; State = ImapState.Authenticated; SelectedReadOnly = false;
                return new ImapCommandResult(tag, "OK", "UNSELECT completed", Array.Empty<string>());

            case "CLOSE":
                if (!SelectedReadOnly && SelectedFolder is not null && _messageService is not null)
                    await HandleExpungeAsync(tag, null, cancellationToken);
                SelectedFolder = null; State = ImapState.Authenticated;
                return new ImapCommandResult(tag, "OK", "CLOSE completed", Array.Empty<string>());

            case "ENABLE":
                return new ImapCommandResult(tag, "OK", "ENABLE completed", Array.Empty<string>());

            case "STORE":
                return await HandleStoreAsync(tag, args, false, cancellationToken);

            case "EXPUNGE":
                return await HandleExpungeAsync(tag, null, cancellationToken);

            case "COPY":
                return await HandleCopyAsync(tag, args, false, cancellationToken);

            case "CREATE":
                return await HandleCreateFolderAsync(tag, args, cancellationToken);

            case "DELETE":
                return await HandleDeleteFolderAsync(tag, args, cancellationToken);

            case "RENAME":
                return await HandleRenameFolderAsync(tag, args, cancellationToken);

            case "SEARCH":
                return await HandleSearchAsync(tag, args, false, cancellationToken);

            case "APPEND":
                return await HandleAppendAsync(tag, args, literalStream, cancellationToken);

            case "FETCH":
                return await HandleFetchOrUidAsync(tag, commandName, arguments, cancellationToken);

            case "UID":
                if (args.FirstOrDefault()?.Text.Equals("STORE", StringComparison.OrdinalIgnoreCase) == true)
                    return await HandleStoreAsync(tag, args.Skip(1).ToArray(), true, cancellationToken);
                if (args.FirstOrDefault()?.Text.Equals("COPY", StringComparison.OrdinalIgnoreCase) == true)
                    return await HandleCopyAsync(tag, args.Skip(1).ToArray(), true, cancellationToken);
                if (args.FirstOrDefault()?.Text.Equals("EXPUNGE", StringComparison.OrdinalIgnoreCase) == true)
                    return args.Count > 1 ? await HandleExpungeAsync(tag, args[1].Text, cancellationToken) : new ImapCommandResult(tag, "BAD", "UID EXPUNGE requires a sequence set", Array.Empty<string>());
                if (args.FirstOrDefault()?.Text.Equals("SEARCH", StringComparison.OrdinalIgnoreCase) == true)
                    return await HandleSearchAsync(tag, args.Skip(1).ToArray(), true, cancellationToken);
                if (args.FirstOrDefault()?.Text.Equals("FETCH", StringComparison.OrdinalIgnoreCase) == true)
                    return await HandleFetchOrUidAsync(tag, commandName, arguments, cancellationToken);
                return new ImapCommandResult(tag, "BAD", "Unsupported UID command", Array.Empty<string>());

            default:
                return new ImapCommandResult(tag, "BAD", $"Unsupported command {commandName}", Array.Empty<string>());
        }
        }
        catch (ResourceNotFoundException)
        {
            return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        }
        catch (UnauthorizedAccessException)
        {
            return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        }
    }

    private async Task<ImapCommandResult> HandleAuthenticateAsync(string tag, IReadOnlyList<ImapToken> args, CancellationToken ct)
    {
        if (State != ImapState.NotAuthenticated) return new ImapCommandResult(tag, "BAD", "Already authenticated", Array.Empty<string>());
        if (args.Count < 1 || !args[0].Text.Equals("PLAIN", StringComparison.OrdinalIgnoreCase)) return new ImapCommandResult(tag, "NO", "Unsupported authentication mechanism", Array.Empty<string>());
        if (args.Count == 1)
        {
            AwaitingSaslContinuation = true;
            _saslTag = tag;
            return new ImapCommandResult(tag, "CONTINUE", string.Empty, new[] { "+" });
        }
        return await CompleteSaslAsync(tag, args[1].Text, ct);
    }

    public async Task<ImapCommandResult> CompleteSaslAsync(string response, CancellationToken ct = default)
    {
        var tag = _saslTag ?? "*";
        AwaitingSaslContinuation = false;
        _saslTag = null;
        return await CompleteSaslAsync(tag, response, ct);
    }

    private async Task<ImapCommandResult> CompleteSaslAsync(string tag, string response, CancellationToken ct)
    {
        if (response.Trim() == "*") return new ImapCommandResult(tag, "NO", "[CANCELLED] AUTHENTICATE cancelled", Array.Empty<string>());
        try
        {
            var fields = Encoding.UTF8.GetString(Convert.FromBase64String(response.Trim())).Split('\0');
            if (fields.Length != 3) return new ImapCommandResult(tag, "NO", "AUTHENTICATE failed", Array.Empty<string>());
            var auth = await _authenticator.AuthenticateAsync(fields[1], fields[2], ct);
            if (!auth.IsSuccess) return new ImapCommandResult(tag, "NO", "AUTHENTICATE failed", Array.Empty<string>());
            TenantId = auth.TenantId; MailboxId = auth.MailboxId; _userId = auth.UserId; Username = fields[1]; State = ImapState.Authenticated;
            return new ImapCommandResult(tag, "OK", "AUTHENTICATE completed", Array.Empty<string>());
        }
        catch (FormatException) { return new ImapCommandResult(tag, "NO", "AUTHENTICATE failed", Array.Empty<string>()); }
    }

    private async Task<ImapCommandResult> HandleStatusAsync(string tag, IReadOnlyList<ImapToken> args, CancellationToken ct)
    {
        var names = args.Where(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString).ToArray();
        if (names.Length < 2 || MailboxId is null || TenantId is null) return new ImapCommandResult(tag, "BAD", "STATUS requires mailbox and item list", Array.Empty<string>());
        var name = names[0].Text;
        var folder = await FindFolderByPathAsync(name, ct);
        if (folder is null) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        var messages = await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.FolderId == folder.Id).ToListAsync(ct);
        var fields = new List<string>();
        var openItems = FindTokenIndex(args, 0, ImapTokenKind.LeftParenthesis);
        var closeItems = openItems < 0 ? -1 : FindTokenIndex(args, openItems + 1, ImapTokenKind.RightParenthesis);
        if (openItems < 0 || closeItems < 0) return new ImapCommandResult(tag, "BAD", "STATUS requires a parenthesised item list", Array.Empty<string>());
        var statusItems = args.Skip(openItems + 1).Take(closeItems - openItems - 1)
            .Where(t => t.Kind == ImapTokenKind.Atom).Select(t => t.Text.ToUpperInvariant()).ToArray();
        var allowed = new HashSet<string>(["MESSAGES", "RECENT", "UIDNEXT", "UIDVALIDITY", "UNSEEN"], StringComparer.Ordinal);
        if (statusItems.Length == 0 || statusItems.Any(item => !allowed.Contains(item))) return new ImapCommandResult(tag, "BAD", "Invalid STATUS data item", Array.Empty<string>());
        var wanted = statusItems.ToHashSet(StringComparer.Ordinal);
        if (wanted.Contains("MESSAGES")) fields.Add($"MESSAGES {messages.Count}");
        if (wanted.Contains("RECENT")) fields.Add("RECENT 0");
        if (wanted.Contains("UIDNEXT")) fields.Add($"UIDNEXT {folder.UidNext}");
        if (wanted.Contains("UIDVALIDITY")) fields.Add($"UIDVALIDITY {folder.UidValidity}");
        if (wanted.Contains("UNSEEN")) fields.Add($"UNSEEN {messages.Count(m => !m.IsRead && !m.Flags.Contains("\\Seen", StringComparison.OrdinalIgnoreCase))}");
        var escaped = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return new ImapCommandResult(tag, "OK", "STATUS completed", new[] { $"* STATUS \"{escaped}\" ({string.Join(' ', fields)})" });
    }

    private async Task<ImapCommandResult> HandleStoreAsync(string tag, IReadOnlyList<ImapToken> args, bool uidMode, CancellationToken ct)
    {
        if (SelectedFolder is null) return new ImapCommandResult(tag, "NO", "No mailbox selected", Array.Empty<string>());
        if (SelectedReadOnly) return new ImapCommandResult(tag, "NO", "[READ-ONLY] Mailbox is read-only", Array.Empty<string>());
        if (_messageService is null || TenantId is null || MailboxId is null || _userId is null || args.Count < 3)
            return new ImapCommandResult(tag, "BAD", "Invalid STORE command", Array.Empty<string>());
        if (!SequenceSet.TryParse(args[0].Text, out var set) || set is null) return new ImapCommandResult(tag, "BAD", "Invalid STORE sequence set", Array.Empty<string>());
        var mode = args[1].Text.ToUpperInvariant();
        var silent = mode.EndsWith(".SILENT", StringComparison.Ordinal);
        var operation = mode.Replace(".SILENT", string.Empty, StringComparison.Ordinal);
        if (operation is not "FLAGS" and not "+FLAGS" and not "-FLAGS") return new ImapCommandResult(tag, "BAD", "Invalid STORE data item", Array.Empty<string>());
        var incoming = args.Skip(2).Where(t => t.Kind == ImapTokenKind.Atom).Select(t => t.Text).ToArray();
        var messages = await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == MailboxId && m.FolderId == SelectedFolder.Id)
            .OrderBy(m => m.Uid).Select(m => new ImapSequenceMessage(m.Id, m.Uid, m.Flags, m.IsRead)).ToListAsync(ct);
        var max = uidMode ? messages.Select(m => m.Uid).DefaultIfEmpty(0u).Max() : (uint)messages.Count;
        var selected = set.Resolve(max).Select(value => uidMode ? messages.FirstOrDefault(m => m.Uid == value) : value > 0 && value <= messages.Count ? messages[(int)value - 1] : null).Where(m => m is not null).Cast<ImapSequenceMessage>().ToArray();
        var responses = new List<string>();
        foreach (var message in selected)
        {
            var flags = ParseFlags(message.Flags).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (message.IsRead) flags.Add("\\Seen");
            if (operation == "FLAGS") flags = incoming.ToHashSet(StringComparer.OrdinalIgnoreCase);
            else if (operation == "+FLAGS") flags.UnionWith(incoming);
            else flags.ExceptWith(incoming);
            if (!await _messageService.SetImapFlagsAsync(TenantId.Value, _userId.Value, MailboxId.Value, message.Id, flags.ToArray(), ct)) continue;
            if (!silent)
            {
                var sequence = (uint)(messages.IndexOf(message) + 1);
                var payload = uidMode ? $"UID {message.Uid} " : string.Empty;
                responses.Add($"* {sequence} FETCH ({payload}FLAGS ({string.Join(' ', flags)}))");
            }
        }
        return new ImapCommandResult(tag, "OK", "STORE completed", responses);
    }

    private async Task<ImapCommandResult> HandleExpungeAsync(string tag, string? uidSetText, CancellationToken ct)
    {
        if (SelectedFolder is null) return new ImapCommandResult(tag, "NO", "No mailbox selected", Array.Empty<string>());
        if (SelectedReadOnly) return new ImapCommandResult(tag, "NO", "[READ-ONLY] Mailbox is read-only", Array.Empty<string>());
        if (_messageService is null || TenantId is null || MailboxId is null || _userId is null) return new ImapCommandResult(tag, "NO", "EXPUNGE unavailable", Array.Empty<string>());
        var messages = await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == MailboxId && m.FolderId == SelectedFolder.Id)
            .OrderBy(m => m.Uid).Select(m => new ImapSequenceMessage(m.Id, m.Uid, m.Flags, m.IsRead)).ToListAsync(ct);
        SequenceSet? uidFilter = null;
        if (uidSetText is not null && !SequenceSet.TryParse(uidSetText, out uidFilter)) return new ImapCommandResult(tag, "BAD", "Invalid UID EXPUNGE sequence set", Array.Empty<string>());
        var doomed = messages.Select((m, i) => (Message: m, Sequence: (uint)(i + 1)))
            .Where(pair => ParseFlags(pair.Message.Flags).Any(f => f.Equals("\\Deleted", StringComparison.OrdinalIgnoreCase)) &&
                (uidFilter is null || uidFilter.Resolve(messages.Select(m => m.Uid).DefaultIfEmpty(0u).Max()).Contains(pair.Message.Uid)))
            .OrderByDescending(pair => pair.Sequence).ToArray();
        var responses = new List<string>();
        foreach (var pair in doomed)
        {
            if (await _messageService.DeleteMessageAsync(TenantId.Value, _userId.Value, MailboxId.Value, pair.Message.Id, cancellationToken: ct))
                responses.Add($"* {pair.Sequence} EXPUNGE");
        }
        return new ImapCommandResult(tag, "OK", "EXPUNGE completed", responses);
    }

    private async Task<ImapCommandResult> HandleCopyAsync(string tag, IReadOnlyList<ImapToken> args, bool uidMode, CancellationToken ct)
    {
        if (SelectedFolder is null) return new ImapCommandResult(tag, "NO", "No mailbox selected", Array.Empty<string>());
        if (args.Count < 2 || !SequenceSet.TryParse(args[0].Text, out var set) || set is null) return new ImapCommandResult(tag, "BAD", "Invalid COPY command", Array.Empty<string>());
        if (_messageService is null || TenantId is null || MailboxId is null || _userId is null) return new ImapCommandResult(tag, "NO", "COPY unavailable", Array.Empty<string>());
        var destination = await FindFolderByPathAsync(args[1].Text, ct);
        if (destination is null) return new ImapCommandResult(tag, "NO", "Destination mailbox does not exist", Array.Empty<string>());
        if (SelectedReadOnly && destination.Id == SelectedFolder.Id) return new ImapCommandResult(tag, "NO", "[READ-ONLY] Cannot COPY into selected mailbox", Array.Empty<string>());
        var messages = await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == MailboxId && m.FolderId == SelectedFolder.Id)
            .OrderBy(m => m.Uid).Select(m => new ImapSequenceMessage(m.Id, m.Uid, m.Flags, m.IsRead)).ToListAsync(ct);
        var max = uidMode ? messages.Select(m => m.Uid).DefaultIfEmpty(0u).Max() : (uint)messages.Count;
        var selected = set.Resolve(max).Select(value => uidMode ? messages.FirstOrDefault(m => m.Uid == value) : value > 0 && value <= messages.Count ? messages[(int)value - 1] : null).Where(m => m is not null).Cast<ImapSequenceMessage>().ToArray();
        var newUids = new List<uint>();
        foreach (var message in selected)
        {
            var newUid = await _messageService.CopyMessageAsync(TenantId.Value, _userId.Value, MailboxId.Value, message.Id, destination.Id, ct);
            if (newUid is uint uid) newUids.Add(uid);
        }
        var sourceUids = string.Join(',', selected.Select(m => m.Uid));
        var destUids = string.Join(',', newUids);
        return new ImapCommandResult(tag, "OK", $"[COPYUID {destination.UidValidity} {sourceUids} {destUids}] COPY completed", Array.Empty<string>());
    }

    private async Task<ImapCommandResult> HandleCreateFolderAsync(string tag, IReadOnlyList<ImapToken> args, CancellationToken ct)
    {
        var name = args.LastOrDefault(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString)?.Text;
        if (name is null || TenantId is null || MailboxId is null || _userId is null || _mailboxService is null) return new ImapCommandResult(tag, "NO", "CREATE unavailable", Array.Empty<string>());
        var segments = name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) return new ImapCommandResult(tag, "BAD", "Invalid folder name", Array.Empty<string>());
        var parent = segments.Length > 1 ? await FindFolderByPathAsync(string.Join('/', segments[..^1]), ct) : null;
        if (segments.Length > 1 && parent is null) return new ImapCommandResult(tag, "NO", "Parent folder does not exist", Array.Empty<string>());
        try { await _mailboxService.CreateFolderAsync(TenantId.Value, _userId.Value, MailboxId.Value, new CreateFolderRequest(segments[^1], parent?.Id), ct); return new ImapCommandResult(tag, "OK", "CREATE completed", Array.Empty<string>()); }
        catch (Exception ex) when (ex is ArgumentException or ResourceNotFoundException or InvalidOperationException) { return new ImapCommandResult(tag, "NO", "CREATE failed", Array.Empty<string>()); }
    }

    private async Task<ImapCommandResult> HandleDeleteFolderAsync(string tag, IReadOnlyList<ImapToken> args, CancellationToken ct)
    {
        var name = args.LastOrDefault(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString)?.Text;
        if (name is null || TenantId is null || MailboxId is null || _userId is null || _mailboxService is null) return new ImapCommandResult(tag, "NO", "DELETE unavailable", Array.Empty<string>());
        var folder = await FindFolderByPathAsync(name, ct);
        if (folder is null) return new ImapCommandResult(tag, "NO", "Folder does not exist", Array.Empty<string>());
        var deleted = await _mailboxService.DeleteFolderAsync(TenantId.Value, _userId.Value, MailboxId.Value, folder.Id, ct);
        return deleted ? new ImapCommandResult(tag, "OK", "DELETE completed", Array.Empty<string>()) : new ImapCommandResult(tag, "NO", "Cannot delete folder", Array.Empty<string>());
    }

    private async Task<ImapCommandResult> HandleRenameFolderAsync(string tag, IReadOnlyList<ImapToken> args, CancellationToken ct)
    {
        var names = args.Where(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString).ToArray();
        if (names.Length < 2 || TenantId is null || MailboxId is null || _userId is null || _mailboxService is null) return new ImapCommandResult(tag, "BAD", "RENAME requires source and destination", Array.Empty<string>());
        if (names[0].Text.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) return new ImapCommandResult(tag, "NO", "[CANNOT] INBOX cannot be renamed", Array.Empty<string>());
        var folder = await FindFolderByPathAsync(names[0].Text, ct);
        if (folder is null) return new ImapCommandResult(tag, "NO", "Folder does not exist", Array.Empty<string>());
        try { await _mailboxService.RenameFolderAsync(TenantId.Value, _userId.Value, MailboxId.Value, folder.Id, names[1].Text, ct); return new ImapCommandResult(tag, "OK", "RENAME completed", Array.Empty<string>()); }
        catch (ResourceNotFoundException) { return new ImapCommandResult(tag, "NO", "Parent folder does not exist", Array.Empty<string>()); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)) { return new ImapCommandResult(tag, "NO", "[ALREADYEXISTS] Folder collision", Array.Empty<string>()); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return new ImapCommandResult(tag, "NO", "RENAME failed", Array.Empty<string>()); }
    }

    private async Task<bool> HasMailboxAccessAsync(bool write, CancellationToken cancellationToken)
    {
        if (TenantId is not Guid tenant || MailboxId is not Guid mailbox || _userId is not Guid user) return false;
        var entity = await _dbContext.Mailboxes.AsNoTracking().FirstOrDefaultAsync(m => m.TenantId == tenant && m.Id == mailbox, cancellationToken);
        try { _authorization.AssertMailboxAccess(tenant, user, entity, write); return true; }
        catch (ResourceNotFoundException) { return false; }
    }

    private async Task<ImapCommandResult> HandleLoginAsync(string tag, IReadOnlyList<ImapToken> arguments, CancellationToken cancellationToken)
    {
        var parts = arguments.Where(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString or ImapTokenKind.Literal).ToArray();
        if (parts.Length != 2)
        {
            return new ImapCommandResult(tag, "BAD", "LOGIN requires username and password", Array.Empty<string>());
        }

        var user = parts[0].Text;
        var password = parts[1].Text;

        var auth = await _authenticator.AuthenticateAsync(user, password, cancellationToken);
        if (!auth.IsSuccess)
        {
            return new ImapCommandResult(tag, "NO", "LOGIN failed: invalid credentials", Array.Empty<string>());
        }

        TenantId = auth.TenantId;
        MailboxId = auth.MailboxId;
        _userId = auth.UserId;
        Username = user;
        State = ImapState.Authenticated;

        return new ImapCommandResult(tag, "OK", "LOGIN completed", Array.Empty<string>());
    }

    private async Task<ImapCommandResult> HandleSelectAsync(string tag, string folderName, bool readOnly, CancellationToken cancellationToken)
    {
        if (State == ImapState.NotAuthenticated || MailboxId == null)
        {
            return new ImapCommandResult(tag, "NO", "Not authenticated", Array.Empty<string>());
        }

        folderName = folderName.Trim().Trim('"');

        var folder = await FindFolderByPathAsync(folderName, cancellationToken);

        if (folder == null)
        {
            // Auto-create standard folder if INBOX
            if (string.Equals(folderName, "INBOX", StringComparison.OrdinalIgnoreCase))
            {
                if (_mailboxService is null || _userId is null) return new ImapCommandResult(tag, "NO", "INBOX is unavailable", Array.Empty<string>());
                await _mailboxService.EnsureDefaultFoldersAsync(TenantId!.Value, _userId.Value, MailboxId.Value, cancellationToken);
                folder = await FindFolderByPathAsync("INBOX", cancellationToken);
            }
            else
            {
                return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
            }
        }
        if (folder is null) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());

        SelectedFolder = folder;
        State = ImapState.Selected;
        var canWrite = false;
        if (!readOnly && TenantId is Guid tenantId && MailboxId is Guid mailboxId && _userId is Guid userId)
        {
            var mailbox = await _dbContext.Mailboxes.AsNoTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == mailboxId, cancellationToken);
            try { _authorization.AssertMailboxAccess(tenantId, userId, mailbox, requireWrite: true); canWrite = true; }
            catch (ResourceNotFoundException) { }
        }
        SelectedReadOnly = readOnly || !canWrite;

        var messageCount = await _dbContext.Messages
            .CountAsync(m => m.TenantId == TenantId && m.MailboxId == MailboxId.Value && m.FolderId == folder.Id, cancellationToken);

        var orderedMessages = await _dbContext.Messages
            .Where(m => m.TenantId == TenantId && m.MailboxId == MailboxId.Value && m.FolderId == folder.Id)
            .OrderBy(m => m.Uid).Select(m => new { m.Uid, m.IsRead, m.Flags }).ToListAsync(cancellationToken);
        var firstUnseen = orderedMessages.FindIndex(m => !m.IsRead && !m.Flags.Contains("\\Seen", StringComparison.OrdinalIgnoreCase));

        var untagged = new List<string>
        {
            $"* {messageCount} EXISTS",
            $"* OK [UIDVALIDITY {folder.UidValidity}] UIDs valid",
            $"* OK [UIDNEXT {folder.UidNext}] Predicted next UID",
            "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)",
            "* 0 RECENT"
        };
        if (firstUnseen >= 0) untagged.Add($"* OK [UNSEEN {firstUnseen + 1}] First unseen");
        untagged.Add(SelectedReadOnly ? "* OK [PERMANENTFLAGS ()] Flags are read-only" : "* OK [PERMANENTFLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)] Permanent flags");

        return new ImapCommandResult(tag, "OK", SelectedReadOnly ? "[READ-ONLY] " + (readOnly ? "EXAMINE" : "SELECT") + " completed" : "[READ-WRITE] SELECT completed", untagged);
    }

    private async Task<ImapCommandResult> HandleListAsync(string tag, IReadOnlyList<ImapToken> arguments, CancellationToken cancellationToken, bool subscribed = false)
    {
        if (State == ImapState.NotAuthenticated || MailboxId is null)
            return new ImapCommandResult(tag, "NO", "Not authenticated", Array.Empty<string>());
        var terms = arguments.Where(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString or ImapTokenKind.Literal).ToArray();
        if (terms.Length < 2) return new ImapCommandResult(tag, "BAD", "LIST requires reference and pattern", Array.Empty<string>());
        var reference = terms[0].Text;
        var pattern = terms[1].Text;
        if (pattern.Length == 0)
            return new ImapCommandResult(tag, "OK", $"{(subscribed ? "LSUB" : "LIST")} completed", new[] { $"* {(subscribed ? "LSUB" : "LIST")} (\\Noselect) \"/\" \"\"" });
        var folders = await _dbContext.Folders.Where(f => f.TenantId == TenantId && f.MailboxId == MailboxId.Value).OrderBy(f => f.Name).ToListAsync(cancellationToken);
        string FullName(Folder folder)
        {
            var names = new Stack<string>();
            var cursor = folder;
            names.Push(cursor.Name);
            while (cursor.ParentId is Guid parentId)
            {
                cursor = folders.FirstOrDefault(f => f.Id == parentId)!;
                if (cursor is null) break;
                names.Push(cursor.Name);
            }
            return string.Join('/', names);
        }
        var fullPattern = string.IsNullOrEmpty(reference) ? pattern : reference.TrimEnd('/') + "/" + pattern.TrimStart('/');
        var regex = "^" + Regex.Escape(fullPattern).Replace("\\*", ".*").Replace("%", "[^/]*") + "$";
        var responses = folders.Select(f => (Folder: f, Name: FullName(f)))
            .Where(pair => Regex.IsMatch(pair.Name, regex, RegexOptions.IgnoreCase))
            .Select(pair =>
            {
                var hasChildren = folders.Any(f => f.ParentId == pair.Folder.Id);
                var attributes = new List<string> { hasChildren ? "\\HasChildren" : "\\HasNoChildren" };
                var special = pair.Folder.Role.ToLowerInvariant() switch
                {
                    "sent" => "\\Sent", "drafts" => "\\Drafts", "trash" => "\\Trash", "junk" => "\\Junk", "archive" => "\\Archive", _ => null
                };
                if (special is not null) attributes.Add(special);
                var escapedName = pair.Name.Replace("\\", "\\\\").Replace("\"", "\\\"");
                return $"* {(subscribed ? "LSUB" : "LIST")} ({string.Join(' ', attributes)}) \"/\" \"{escapedName}\"";
            }).ToArray();
        return new ImapCommandResult(tag, "OK", $"{(subscribed ? "LSUB" : "LIST")} completed", responses);
    }

    private async Task<Folder?> FindFolderByPathAsync(string path, CancellationToken cancellationToken)
    {
        if (TenantId is null || MailboxId is null) return null;
        var folders = await _dbContext.Folders.Where(f => f.TenantId == TenantId && f.MailboxId == MailboxId).ToListAsync(cancellationToken);
        Guid? parentId = null;
        Folder? match = null;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            match = folders.FirstOrDefault(f => f.ParentId == parentId && f.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (match is null) return null;
            parentId = match.Id;
        }
        return match;
    }

    private async Task<ImapCommandResult> HandleSearchAsync(string tag, IReadOnlyList<ImapToken> arguments, bool uidMode, CancellationToken cancellationToken)
    {
        if (State != ImapState.Selected || SelectedFolder is null)
            return new ImapCommandResult(tag, "NO", "No folder selected", Array.Empty<string>());
        ImapSearchQuery query;
        try { query = ImapSearchParser.Parse(arguments); }
        catch (ImapUnsupportedCharsetException) { return new ImapCommandResult(tag, "NO", "[BADCHARSET (US-ASCII UTF-8)] Unsupported SEARCH charset", Array.Empty<string>()); }
        catch (FormatException) { return new ImapCommandResult(tag, "BAD", "Invalid SEARCH criteria", Array.Empty<string>()); }
        var keys = EnumerateSearchKeys(query.Root).ToArray();
        var needsBody = keys.Any(key => key.Name is "BODY" or "TEXT");
        var needsHeaders = keys.Any(key => key.Name is "HEADER" or "TEXT");
        var needsRecipients = keys.Any(key => key.Name is "CC" or "BCC");
        var messageQuery = _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == MailboxId && m.FolderId == SelectedFolder.Id).OrderBy(m => m.Uid);
        List<ImapSearchMessage> messages;
        if (needsBody && needsHeaders)
            messages = await messageQuery.Select(m => new ImapSearchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.Sender, m.Recipient, m.Subject, m.Date, m.SizeBytes, m.RawHeaders, m.BodyText, m.BodyHtml)).ToListAsync(cancellationToken);
        else if (needsBody)
            messages = await messageQuery.Select(m => new ImapSearchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.Sender, m.Recipient, m.Subject, m.Date, m.SizeBytes, string.Empty, m.BodyText, m.BodyHtml)).ToListAsync(cancellationToken);
        else if (needsHeaders)
            messages = await messageQuery.Select(m => new ImapSearchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.Sender, m.Recipient, m.Subject, m.Date, m.SizeBytes, m.RawHeaders, null, null)).ToListAsync(cancellationToken);
        else
            messages = await messageQuery.Select(m => new ImapSearchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.Sender, m.Recipient, m.Subject, m.Date, m.SizeBytes, string.Empty, null, null)).ToListAsync(cancellationToken);
        var recipients = needsRecipients
            ? await _dbContext.MessageRecipients.Where(r => r.TenantId == TenantId && messages.Select(m => m.Id).Contains(r.MessageId))
                .Select(r => new ImapSearchRecipient(r.MessageId, r.Type, r.Address)).ToListAsync(cancellationToken)
            : new List<ImapSearchRecipient>();
        var matches = messages.Select((m, index) => (Message: m, Sequence: (uint)(index + 1)))
            .Where(pair => EvaluateSearch(query.Root, pair.Message, pair.Sequence, messages, recipients)).ToArray();
        var found = matches.Select(pair => uidMode ? pair.Message.Uid : pair.Sequence);
        return new ImapCommandResult(tag, "OK", "SEARCH completed", new[] { "* SEARCH" + (matches.Length == 0 ? string.Empty : " " + string.Join(' ', found)) });
    }

    private static IEnumerable<ImapSearchKey> EnumerateSearchKeys(ImapSearchNode node)
    {
        if (node is ImapSearchKey key) { yield return key; yield break; }
        if (node is ImapSearchAnd and)
            foreach (var child in and.Children)
                foreach (var nested in EnumerateSearchKeys(child)) yield return nested;
        if (node is ImapSearchNot not)
            foreach (var nested in EnumerateSearchKeys(not.Child)) yield return nested;
        if (node is ImapSearchOr or)
        {
            foreach (var nested in EnumerateSearchKeys(or.Left)) yield return nested;
            foreach (var nested in EnumerateSearchKeys(or.Right)) yield return nested;
        }
    }

    private static bool EvaluateSearch(ImapSearchNode node, ImapSearchMessage message, uint sequence, IReadOnlyList<ImapSearchMessage> messages, IReadOnlyList<ImapSearchRecipient> recipients)
    {
        if (node is ImapSearchAnd and) return and.Children.All(child => EvaluateSearch(child, message, sequence, messages, recipients));
        if (node is ImapSearchNot not) return !EvaluateSearch(not.Child, message, sequence, messages, recipients);
        if (node is ImapSearchOr or) return EvaluateSearch(or.Left, message, sequence, messages, recipients) || EvaluateSearch(or.Right, message, sequence, messages, recipients);
        var key = (ImapSearchKey)node;
        var flags = ParseFlags(message.Flags).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = message.IsRead || flags.Contains("\\Seen");
        var arg = key.Values.FirstOrDefault() ?? string.Empty;
        bool contains(string value, string query) => value.Contains(query, StringComparison.OrdinalIgnoreCase);
        DateTime? date() => DateTime.TryParse(arg, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed.Date : null;
        return key.Name switch
        {
            "ALL" => true,
            "SEEN" => seen, "UNSEEN" => !seen,
            "ANSWERED" => flags.Contains("\\Answered"), "UNANSWERED" => !flags.Contains("\\Answered"),
            "FLAGGED" => flags.Contains("\\Flagged"), "UNFLAGGED" => !flags.Contains("\\Flagged"),
            "DELETED" => flags.Contains("\\Deleted"), "UNDELETED" => !flags.Contains("\\Deleted"),
            "DRAFT" => flags.Contains("\\Draft"), "UNDRAFT" => !flags.Contains("\\Draft"),
            "NEW" => false, "OLD" => true, "RECENT" => false,
            "FROM" => contains(message.Sender, arg), "TO" => contains(message.Recipient, arg),
            "CC" => recipients.Any(r => r.MessageId == message.Id && r.Type.Equals("cc", StringComparison.OrdinalIgnoreCase) && contains(r.Address, arg)),
            "BCC" => recipients.Any(r => r.MessageId == message.Id && r.Type.Equals("bcc", StringComparison.OrdinalIgnoreCase) && contains(r.Address, arg)),
            "SUBJECT" => contains(message.Subject, arg), "BODY" => contains(message.BodyText ?? string.Empty, arg) || contains(message.BodyHtml ?? string.Empty, arg),
            "TEXT" => contains(message.RawHeaders + message.Subject + message.BodyText + message.BodyHtml, arg),
            "HEADER" => key.Values.Count == 2 && message.RawHeaders.Split("\r\n", StringSplitOptions.None).Any(line => line.StartsWith(key.Values[0] + ":", StringComparison.OrdinalIgnoreCase) && contains(line, key.Values[1])),
            "LARGER" => long.TryParse(arg, out var larger) && message.SizeBytes > larger,
            "SMALLER" => long.TryParse(arg, out var smaller) && message.SizeBytes < smaller,
            "SINCE" => date() is DateTime since && message.Date.Date >= since,
            "BEFORE" => date() is DateTime before && message.Date.Date < before,
            "ON" => date() is DateTime on && message.Date.Date == on,
            "SENTSINCE" => date() is DateTime sentSince && message.Date.Date >= sentSince,
            "SENTBEFORE" => date() is DateTime sentBefore && message.Date.Date < sentBefore,
            "SENTON" => date() is DateTime sentOn && message.Date.Date == sentOn,
            "UID" => SequenceSet.TryParse(arg, out var uidSet) && uidSet!.Resolve(messages.Select(m => m.Uid).DefaultIfEmpty(0u).Max()).Contains(message.Uid),
            _ when SequenceSet.TryParse(key.Name, out var sequenceSet) => sequenceSet!.Resolve((uint)messages.Count).Contains(sequence),
            _ => false
        };
    }

    private async Task<ImapCommandResult> HandleAppendAsync(
        string tag,
        IReadOnlyList<ImapToken> arguments,
        Stream? literalPayload,
        CancellationToken cancellationToken)
    {
        if (State == ImapState.NotAuthenticated || MailboxId == null)
        {
            return new ImapCommandResult(tag, "NO", "Not authenticated", Array.Empty<string>());
        }

        // APPEND mailbox [flags] [internaldate] literal
        var folderArgument = arguments.FirstOrDefault(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString);
        if (folderArgument is null)
        {
            return new ImapCommandResult(tag, "BAD", "Invalid APPEND arguments", Array.Empty<string>());
        }

        var folderName = folderArgument.Text;
        var folder = await FindFolderByPathAsync(folderName, cancellationToken);

        if (folder == null)
        {
            if (folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase) && _mailboxService is not null && _userId is not null)
            {
                await _mailboxService.EnsureDefaultFoldersAsync(TenantId!.Value, _userId.Value, MailboxId.Value, cancellationToken);
                folder = await FindFolderByPathAsync("INBOX", cancellationToken);
            }
        }
        if (folder is null) return new ImapCommandResult(tag, "NO", "Destination mailbox does not exist", Array.Empty<string>());

        if (SelectedReadOnly && SelectedFolder?.Id == folder.Id)
            return new ImapCommandResult(tag, "NO", "Selected mailbox is read-only", Array.Empty<string>());

        var flags = string.Empty;
        var openFlags = FindTokenIndex(arguments, 0, ImapTokenKind.LeftParenthesis);
        if (openFlags >= 0)
        {
            var closeFlags = FindTokenIndex(arguments, openFlags + 1, ImapTokenKind.RightParenthesis);
            if (closeFlags < 0) return new ImapCommandResult(tag, "BAD", "Unterminated APPEND flags", Array.Empty<string>());
            flags = string.Join(' ', arguments.Skip(openFlags + 1).Take(closeFlags - openFlags - 1).Select(t => t.Text));
        }

        var internalDate = arguments.FirstOrDefault(t => t.Kind == ImapTokenKind.QuotedString && t != folderArgument)?.Text;
        var messageDate = DateTimeOffset.UtcNow;
        if (internalDate is not null && DateTimeOffset.TryParseExact(internalDate,
            new[] { "dd-MMM-yyyy HH:mm:ss zzz", "dd-MMM-yyyy HH:mm:ss zzzz" },
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedInternalDate))
            messageDate = parsedInternalDate.ToUniversalTime();

        if (literalPayload == null)
        {
            return new ImapCommandResult(tag, "BAD", "Missing message literal stream", Array.Empty<string>());
        }

        // Store payload in MailStorage (content-addressable filesystem deduplication)
        var storageResult = await _storage.StoreAsync(literalPayload, cancellationToken);

        // Read raw headers/subject/sender from storage stream for entity metadata
        string sender = "unknown@miautrix.local";
        string recipient = Username ?? "unknown@miautrix.local";
        string subject = "(No Subject)";

        await using (var readStream = await _storage.OpenReadAsync(storageResult.ContentHash, cancellationToken))
        {
            if (readStream != null)
            {
                using var reader = new StreamReader(readStream, Encoding.UTF8, leaveOpen: true);
                string? line;
                while (!string.IsNullOrWhiteSpace(line = await reader.ReadLineAsync(cancellationToken)))
                {
                    if (line.StartsWith("From:", StringComparison.OrdinalIgnoreCase))
                        sender = line[5..].Trim();
                    else if (line.StartsWith("To:", StringComparison.OrdinalIgnoreCase))
                        recipient = line[3..].Trim();
                    else if (line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
                        subject = line[8..].Trim();
                }
            }
        }

        uint assignedUid = folder.UidNext;
        folder.UidNext += 1;
        folder.UpdatedAt = DateTimeOffset.UtcNow;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId!.Value,
            MailboxId = MailboxId.Value,
            FolderId = folder.Id,
            Sender = sender,
            Recipient = recipient,
            Subject = subject,
            Date = messageDate,
            ContentHash = storageResult.ContentHash,
            StoragePath = storageResult.StoragePath,
            SizeBytes = storageResult.SizeBytes,
            Flags = flags,
            Uid = assignedUid,
            IsRead = flags.Contains("\\Seen", StringComparison.OrdinalIgnoreCase),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ImapCommandResult(
            tag, "OK", $"[APPENDUID {folder.UidValidity} {assignedUid}] APPEND completed",
            Array.Empty<string>());
    }

    private static int FindTokenIndex(IReadOnlyList<ImapToken> tokens, int start, ImapTokenKind kind)
    {
        for (var i = start; i < tokens.Count; i++) if (tokens[i].Kind == kind) return i;
        return -1;
    }

    private async Task<ImapCommandResult> HandleFetchOrUidAsync(
        string tag,
        string commandName,
        string arguments,
        CancellationToken cancellationToken)
    {
        if (State != ImapState.Selected || SelectedFolder == null)
        {
            return new ImapCommandResult(tag, "NO", "No folder selected", Array.Empty<string>());
        }

        bool isUidMode = commandName.Equals("UID", StringComparison.OrdinalIgnoreCase);
        string queryArgs = isUidMode ? arguments.Trim() : arguments;

        // e.g. "FETCH 1 (FLAGS RFC822)" or "UID FETCH 1 (FLAGS RFC822)"
        if (isUidMode && queryArgs.StartsWith("FETCH", StringComparison.OrdinalIgnoreCase))
        {
            queryArgs = queryArgs[5..].Trim();
        }

        var parts = queryArgs.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return new ImapCommandResult(tag, "BAD", "Invalid FETCH format", Array.Empty<string>());
        }
        if (!SequenceSet.TryParse(parts[0], out var set) || set is null)
            return new ImapCommandResult(tag, "BAD", "Invalid FETCH sequence set", Array.Empty<string>());
        IReadOnlyList<ImapFetchItem> items;
        try { items = ImapFetchItemParser.Parse(parts[1]); }
        catch (FormatException) { return new ImapCommandResult(tag, "BAD", "Invalid FETCH data items", Array.Empty<string>()); }

        var messages = await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == MailboxId && m.FolderId == SelectedFolder.Id)
            .OrderBy(m => m.Uid).Select(m => new ImapFetchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.Date, m.SizeBytes, m.ContentHash)).ToListAsync(cancellationToken);
        var maximum = isUidMode ? messages.Select(m => m.Uid).DefaultIfEmpty(0u).Max() : (uint)messages.Count;
        var selected = set.Resolve(maximum).Select(value => isUidMode ? messages.FirstOrDefault(m => m.Uid == value) : value > 0 && value <= messages.Count ? messages[(int)value - 1] : null).Where(m => m is not null).Cast<ImapFetchMessage>().ToArray();
        var needsRawMessage = items.Any(item => item.Name is "RFC822" or "RFC822.HEADER" or "RFC822.TEXT" or "ENVELOPE" or "BODYSTRUCTURE" || item.Name == "BODY" && item.Section is null || item.Section is not null);
        var responseSegments = new List<ImapResponseSegment>();
        foreach (var message in selected)
        {
            byte[] messageBytes = Array.Empty<byte>();
            if (needsRawMessage)
            {
                await using var stream = await _storage.OpenReadAsync(message.ContentHash, cancellationToken);
                if (stream is not null) { using var ms = new MemoryStream(); await stream.CopyToAsync(ms, cancellationToken); messageBytes = ms.ToArray(); }
            }
            var separator = FindHeaderSeparator(messageBytes);
            var header = messageBytes.AsSpan(0, separator < 0 ? messageBytes.Length : separator + 4).ToArray();
            var body = separator < 0 ? Array.Empty<byte>() : messageBytes.AsSpan(separator + 4).ToArray();
            var sequence = (uint)(messages.IndexOf(message) + 1);
            var values = new List<(string Name, byte[]? Literal, string? Text, ImapFetchItem? Source)>();
            void AddValue(string name, byte[]? literal, string? text, ImapFetchItem? source = null) => values.Add((name, literal, text, source));
            foreach (var item in items)
            {
                var itemName = item.Name.ToUpperInvariant();
                if (itemName == "UID") AddValue("UID", null, message.Uid.ToString(), item);
                else if (itemName == "FLAGS") AddValue("FLAGS", null, $"({FormatFlags(message.Flags, message.IsRead)})", item);
                else if (itemName == "INTERNALDATE") AddValue("INTERNALDATE", null, $"\"{message.Date.UtcDateTime:dd-MMM-yyyy HH:mm:ss} +0000\"", item);
                else if (itemName == "RFC822.SIZE") AddValue("RFC822.SIZE", null, message.SizeBytes.ToString(), item);
                else if (itemName == "ENVELOPE") AddValue("ENVELOPE", null, _mimeFormatter?.BuildEnvelope(messageBytes) ?? "NIL", item);
                else if (itemName == "BODYSTRUCTURE") AddValue("BODYSTRUCTURE", null, _mimeFormatter?.BuildBodyStructure(messageBytes) ?? "NIL", item);
                else if (itemName == "BODY" && item.Section is null) AddValue("BODY", null, _mimeFormatter?.BuildBodyStructure(messageBytes, extended: false) ?? "NIL", item);
                else if (itemName == "RFC822" && item.Section is null) AddValue("RFC822", messageBytes, null, item);
                else if (item.Section is not null && item.Section.Length == 0) AddValue("BODY[]", messageBytes, null, item);
                else if (itemName == "RFC822.HEADER") AddValue("RFC822.HEADER", header, null, item);
                else if ((itemName is "BODY" or "BODY.PEEK") && item.Section?.Equals("HEADER", StringComparison.OrdinalIgnoreCase) == true) AddValue("BODY[HEADER]", header, null, item);
                else if (itemName == "RFC822.TEXT" || (item.Section?.Equals("TEXT", StringComparison.OrdinalIgnoreCase) == true)) AddValue(itemName == "RFC822.TEXT" ? itemName : "BODY[TEXT]", body, null, item);
                else if (item.Section?.StartsWith("HEADER.FIELDS", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var not = item.Section.StartsWith("HEADER.FIELDS.NOT", StringComparison.OrdinalIgnoreCase);
                    var names = System.Text.RegularExpressions.Regex.Matches(item.Section, "[A-Za-z0-9-]+(?=\\s|\\))").Select(m => m.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var headerLines = Encoding.Latin1.GetString(header).Split("\r\n", StringSplitOptions.None);
                    var fields = new List<(string Name, List<string> Lines)>();
                    foreach (var line in headerLines)
                    {
                        if (line.Length == 0) break;
                        if (char.IsWhiteSpace(line[0]) && fields.Count > 0) fields[^1].Lines.Add(line);
                        else if (line.Contains(':')) fields.Add((line.Split(':', 2)[0], new List<string> { line }));
                    }
                    var filtered = fields.Where(field => names.Contains(field.Name) != not).SelectMany(field => field.Lines).ToArray();
                    var bytes = Encoding.Latin1.GetBytes(string.Join("\r\n", filtered) + "\r\n\r\n");
                    AddValue($"BODY[{item.Section}]", bytes, null, item);
                }
                else if (item.Section is not null) AddValue($"BODY[{item.Section}]", _mimeFormatter?.GetBodySection(messageBytes, item.Section) ?? Array.Empty<byte>(), null, item);
            }
            if (items.Any(i => !i.Peek && (i.Name is "RFC822" or "RFC822.TEXT" || i.Section is { Length: > 0 } section && !section.Equals("HEADER", StringComparison.OrdinalIgnoreCase) && !section.StartsWith("HEADER.FIELDS", StringComparison.OrdinalIgnoreCase) && !section.EndsWith(".MIME", StringComparison.OrdinalIgnoreCase))) && !SelectedReadOnly)
            {
                var seenFlags = ParseFlags(message.Flags).Append("\\Seen").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (_messageService is not null && TenantId is Guid tenantId && MailboxId is Guid mailboxId && _userId is Guid userId)
                    await _messageService.SetImapFlagsAsync(tenantId, userId, mailboxId, message.Id, seenFlags, cancellationToken);
                else
                {
                    await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == MailboxId && m.Id == message.Id)
                        .ExecuteUpdateAsync(update => update.SetProperty(m => m.IsRead, true).SetProperty(m => m.Flags, string.Join(' ', seenFlags)), cancellationToken);
                }
                if (!values.Any(v => v.Name == "FLAGS")) values.Add(("FLAGS", null, $"({string.Join(' ', seenFlags)})", null));
            }
            var prefix = $"* {sequence} FETCH (";
            if (isUidMode && !values.Any(v => v.Name == "UID")) values.Insert(0, ("UID", null, message.Uid.ToString(), null));
            for (var i = 0; i < values.Count; i++)
            {
                var (itemName, literalBytes, text, sourceItem) = values[i];
                var sep = i == 0 ? string.Empty : " ";
                if (literalBytes is null) prefix += sep + itemName + " " + text;
                else
                {
                if (values[i].Name.StartsWith("BODY", StringComparison.OrdinalIgnoreCase) && values[i].Name != "BODY")
                    {
                        if (sourceItem?.PartialStart is uint start && start < literalBytes.Length)
                        {
                            var count = sourceItem.PartialCount is uint requested ? (int)Math.Min(requested, literalBytes.Length - start) : literalBytes.Length - (int)start;
                            literalBytes = literalBytes.AsSpan((int)start, count).ToArray();
                        }
                        else if (sourceItem?.PartialStart is uint) literalBytes = Array.Empty<byte>();
                        if (sourceItem?.PartialStart is uint partialStart)
                            itemName += $"<{partialStart}>";
                    }
                    prefix += sep + itemName + $" {{{literalBytes.Length}}}\r\n";
                    responseSegments.Add(ImapResponseSegment.Data(Encoding.UTF8.GetBytes(prefix)));
                    responseSegments.Add(ImapResponseSegment.Data(literalBytes));
                prefix = string.Empty;
                }
            }
            responseSegments.Add(ImapResponseSegment.Line(prefix + ")"));
        }
        return new ImapCommandResult(tag, "OK", "FETCH completed", responseSegments);
    }

    private static int FindHeaderSeparator(byte[] bytes)
    {
        for (var i = 0; i + 3 < bytes.Length; i++) if (bytes[i] == 13 && bytes[i + 1] == 10 && bytes[i + 2] == 13 && bytes[i + 3] == 10) return i;
        return -1;
    }

    private static IEnumerable<string> ParseFlags(string flags) => flags.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string FormatFlags(string flags, bool isRead) => string.Join(' ', ParseFlags(flags).Concat(isRead ? new[] { "\\Seen" } : Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase));
}
