using System.Text;
using System.Text.RegularExpressions;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Application.Mail.Mime;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
    private sealed record ImapSequenceMessage(Guid Id, uint Uid, string Flags, bool IsRead, bool IsRecent);
    private sealed record ImapFetchMessage(Guid Id, uint Uid, string Flags, bool IsRead, bool IsRecent, DateTimeOffset Date, long SizeBytes, string ContentHash);
    private sealed record ImapSearchMessage(Guid Id, uint Uid, string Flags, bool IsRead, bool IsRecent, string Sender, string Recipient, string Subject, DateTimeOffset Date, long SizeBytes, string RawHeaders, string? BodyText, string? BodyHtml);
    private sealed record ImapSearchRecipient(Guid MessageId, string Type, string Address);
    private readonly AppDbContext _dbContext;
    private readonly IMailStorage _storage;
    private readonly IImapAuthenticator _authenticator;
    private readonly ITenantAuthorizationHelper _authorization;
    private readonly IMailboxService? _mailboxService;
    private readonly IMessageService? _messageService;
    private readonly IImapMimeFormatter? _mimeFormatter;
    private readonly IMessageUidAllocator? _uidAllocator;
    private List<uint> _selectedUidSnapshot = new();
    private readonly Dictionary<uint, string> _selectedFlagsSnapshot = new();
    private readonly Dictionary<Guid, HashSet<uint>> _sessionRecentUidsByFolder = new();
    private Guid? _userId;
    private bool _utf8AcceptEnabled;
    private readonly HashSet<string> _enabledExtensions = new(StringComparer.OrdinalIgnoreCase);
    private string? _idleTag;

    public Guid? TenantId { get; private set; }
    public Guid? MailboxId { get; private set; }
    // MailboxId is the authenticated user's own mailbox. The selected mailbox can be a
    // delegated mailbox and is derived from the selected folder, never from the username.
    private Guid? ActiveMailboxId => SelectedFolder?.MailboxId ?? MailboxId;
    public string? Username { get; private set; }
    public Folder? SelectedFolder { get; private set; }
    public ImapState State { get; private set; } = ImapState.NotAuthenticated;
    public bool SelectedReadOnly { get; private set; }
    public bool IsIdling => _idleTag is not null;
    public string? IdleTag => _idleTag;
    public const string CapabilityLine = "* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE NAMESPACE MOVE IDLE";
    public bool AwaitingSaslContinuation { get; private set; }
    private string? _saslTag;

    public ImapSession(AppDbContext dbContext, IMailStorage storage, IImapAuthenticator authenticator,
        ITenantAuthorizationHelper authorization, IMailboxService? mailboxService = null, IMessageService? messageService = null, IImapMimeFormatter? mimeFormatter = null, IMessageUidAllocator? uidAllocator = null)
    {
        _dbContext = dbContext;
        _storage = storage;
        _authenticator = authenticator;
        _authorization = authorization;
        _mailboxService = mailboxService;
        _messageService = messageService;
        _mimeFormatter = mimeFormatter;
        _uidAllocator = uidAllocator;
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
            var write = operation is "APPEND" or "STORE" or "EXPUNGE" or "CREATE" or "DELETE" or "RENAME" or "MOVE";
            // Commands that name a mailbox resolve and authorize that mailbox in their handler.
            if (commandName is not "LIST" and not "LSUB" and not "STATUS" and not "SELECT" and not "EXAMINE" and not "APPEND" and not "CREATE" and not "DELETE" and not "RENAME" and not "COPY" &&
                !await HasMailboxAccessAsync(write, cancellationToken))
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
                return await HandleNoopAsync(tag, cancellationToken);

            case "LOGOUT":
                State = ImapState.LoggedOut;
                return new ImapCommandResult(
                    tag, "OK", "LOGOUT completed",
                    new[] { "* BYE Miautrix Mail Server logging out" });

            case "LOGIN":
                return await HandleLoginAsync(tag, args, cancellationToken);

            case "AUTHENTICATE":
                return await HandleAuthenticateAsync(tag, args, cancellationToken);

            case "ENABLE":
                return HandleEnable(tag, args);

            case "SELECT":
                return await HandleSelectAsync(tag, args.FirstOrDefault()?.Text ?? string.Empty, false, cancellationToken);

            case "EXAMINE":
                return await HandleSelectAsync(tag, args.FirstOrDefault()?.Text ?? string.Empty, true, cancellationToken);

            case "LIST":
                return await HandleListAsync(tag, args, cancellationToken);

            case "LSUB":
                return await HandleListAsync(tag, args, cancellationToken, subscribed: true);

            case "SUBSCRIBE":
                return await HandleSubscriptionAsync(tag, args, subscribe: true, cancellationToken);
            case "UNSUBSCRIBE":
                return await HandleSubscriptionAsync(tag, args, subscribe: false, cancellationToken);

            case "STATUS":
                return await HandleStatusAsync(tag, args, cancellationToken);

            case "NAMESPACE":
                return HandleNamespace(tag);

            case "CHECK":
                return State == ImapState.Selected
                    ? new ImapCommandResult(tag, "OK", "CHECK completed", Array.Empty<string>())
                    : new ImapCommandResult(tag, "NO", "No mailbox selected", Array.Empty<string>());

            case "UNSELECT":
                SelectedFolder = null; State = ImapState.Authenticated; SelectedReadOnly = false;
                _selectedUidSnapshot.Clear(); _selectedFlagsSnapshot.Clear();
                return new ImapCommandResult(tag, "OK", "UNSELECT completed", Array.Empty<string>());

            case "CLOSE":
                if (!SelectedReadOnly && SelectedFolder is not null && _messageService is not null)
                    await HandleExpungeAsync(tag, null, cancellationToken);
                SelectedFolder = null; State = ImapState.Authenticated;
                _selectedUidSnapshot.Clear(); _selectedFlagsSnapshot.Clear();
                return new ImapCommandResult(tag, "OK", "CLOSE completed", Array.Empty<string>());

            case "STORE":
                return await HandleStoreAsync(tag, args, false, cancellationToken);

            case "EXPUNGE":
                return await HandleExpungeAsync(tag, null, cancellationToken);

            case "COPY":
                return await HandleCopyAsync(tag, args, false, cancellationToken);

            case "MOVE":
                return await HandleMoveAsync(tag, args, false, cancellationToken);

            case "IDLE":
                return HandleIdle(tag, args);

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
                if (args.FirstOrDefault()?.Text.Equals("MOVE", StringComparison.OrdinalIgnoreCase) == true)
                    return await HandleMoveAsync(tag, args.Skip(1).ToArray(), true, cancellationToken);
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
        catch (FormatException)
        {
            return new ImapCommandResult(tag, "BAD", "Invalid mailbox name encoding", Array.Empty<string>());
        }
        catch (Exception ex)
        {
            // Never include command arguments, credentials or message data in diagnostics.
            System.Diagnostics.Trace.TraceError("IMAP command failed: {0}", ex.GetType().Name);
            return new ImapCommandResult(tag, "NO", "[SERVERBUG] Command failed", Array.Empty<string>());
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

    private ImapCommandResult HandleEnable(string tag, IReadOnlyList<ImapToken> args)
    {
        if (args.Count == 0 || args.Any(argument => argument.Kind is not ImapTokenKind.Atom))
            return new ImapCommandResult(tag, "BAD", "ENABLE requires one or more capability atoms", Array.Empty<string>());

        // RFC 5161 says unknown or unsupported capability names are ignored. UTF8=ACCEPT
        // is advertised here and switches mailbox-name decoding/encoding to UTF-8 (RFC 6855).
        foreach (var argument in args)
        {
            if (argument.Text.Equals("UTF8=ACCEPT", StringComparison.OrdinalIgnoreCase))
            {
                _utf8AcceptEnabled = true;
                _enabledExtensions.Add("UTF8=ACCEPT");
            }
        }

        var enabled = _enabledExtensions.Count == 0
            ? Array.Empty<string>()
            : new[] { $"* ENABLED {string.Join(' ', _enabledExtensions.Order(StringComparer.OrdinalIgnoreCase))}" };
        return new ImapCommandResult(tag, "OK", "ENABLE completed", enabled);
    }

    private static ImapCommandResult HandleNamespace(string tag)
    {
        // Personal folders are unprefixed; delegated folders use the existing Shared/ tree.
        // There is no separate "other users" namespace in this server's mailbox model.
        return new ImapCommandResult(tag, "OK", "NAMESPACE completed",
            new[] { "* NAMESPACE ((\"\" \"/\")) NIL ((\"Shared/\" \"/\"))" });
    }

    private ImapCommandResult HandleIdle(string tag, IReadOnlyList<ImapToken> args)
    {
        if (args.Count != 0) return new ImapCommandResult(tag, "BAD", "IDLE takes no arguments", Array.Empty<string>());
        if (State != ImapState.Selected || SelectedFolder is null)
            return new ImapCommandResult(tag, "NO", "No mailbox selected", Array.Empty<string>());
        _idleTag = tag;
        return new ImapCommandResult(tag, "CONTINUE", string.Empty, new[] { "+ idling" });
    }

    public async Task<ImapCommandResult> PollIdleChangesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsIdling || SelectedFolder is null || TenantId is null)
            return new ImapCommandResult("*", "CONTINUE", string.Empty, Array.Empty<string>());
        var selected = SelectedFolder;
        if (!await AuthorizeMailboxAsync(selected.MailboxId, write: false, cancellationToken))
        {
            _idleTag = null;
            SelectedFolder = null;
            SelectedReadOnly = false;
            State = ImapState.Authenticated;
            _selectedUidSnapshot.Clear();
            _selectedFlagsSnapshot.Clear();
            return new ImapCommandResult("*", "CONTINUE", string.Empty, new[] { "* BYE Mailbox access revoked" });
        }

        var current = await _dbContext.Messages.AsNoTracking()
            .Where(message => message.TenantId == TenantId && message.MailboxId == selected.MailboxId && message.FolderId == selected.Id)
            .OrderBy(message => message.Uid)
            .Select(message => new { message.Uid, message.Flags, message.IsRead, message.IsRecent })
            .ToListAsync(cancellationToken);
        var currentUids = current.Select(message => message.Uid).ToList();
        var currentUidSet = currentUids.ToHashSet();
        var responses = new List<string>();
        for (var index = _selectedUidSnapshot.Count - 1; index >= 0; index--)
        {
            if (!currentUidSet.Contains(_selectedUidSnapshot[index])) responses.Add($"* {index + 1} EXPUNGE");
        }
        if (currentUids.Count > _selectedUidSnapshot.Count) responses.Add($"* {currentUids.Count} EXISTS");
        for (var index = 0; index < current.Count; index++)
        {
            var message = current[index];
            if (_selectedFlagsSnapshot.TryGetValue(message.Uid, out var oldFlags) &&
                !string.Equals(oldFlags, $"{message.Flags}|{message.IsRead}", StringComparison.Ordinal))
            {
                var formattedFlags = FormatFlags(message.Flags, message.IsRead, message.IsRecent);
                responses.Add($"* {index + 1} FETCH (UID {message.Uid} FLAGS ({formattedFlags}))");
            }
        }
        _selectedUidSnapshot = currentUids;
        _selectedFlagsSnapshot.Clear();
        foreach (var message in current) _selectedFlagsSnapshot[message.Uid] = $"{message.Flags}|{message.IsRead}";
        return new ImapCommandResult("*", "CONTINUE", string.Empty, responses);
    }

    public ImapCommandResult EndIdle()
    {
        var tag = _idleTag ?? "*";
        _idleTag = null;
        return new ImapCommandResult(tag, "OK", "IDLE terminated", Array.Empty<string>());
    }

    public void AbortIdle()
    {
        _idleTag = null;
        SelectedFolder = null;
        SelectedReadOnly = false;
        _selectedUidSnapshot.Clear();
        _selectedFlagsSnapshot.Clear();
        State = ImapState.LoggedOut;
    }

    private string DecodeMailboxPath(string path) => _utf8AcceptEnabled ? path : ImapMailboxNameCodec.Decode(path);
    private string EncodeMailboxPath(string path) => _utf8AcceptEnabled ? path : ImapMailboxNameCodec.Encode(path);

    private async Task<ImapCommandResult> HandleNoopAsync(string tag, CancellationToken ct)
    {
        if (SelectedFolder is null || TenantId is null)
            return new ImapCommandResult(tag, "OK", "NOOP completed", Array.Empty<string>());

        if (!await AuthorizeMailboxAsync(SelectedFolder.MailboxId, write: false, ct))
            return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());

        var currentMessages = await _dbContext.Messages
            .Where(message => message.TenantId == TenantId && message.MailboxId == SelectedFolder.MailboxId && message.FolderId == SelectedFolder.Id)
            .OrderBy(message => message.Uid)
            .Select(message => new { message.Uid, message.IsRecent, message.Flags, message.IsRead })
            .ToListAsync(ct);
        if (!_sessionRecentUidsByFolder.TryGetValue(SelectedFolder.Id, out var recentUids))
            _sessionRecentUidsByFolder[SelectedFolder.Id] = recentUids = new HashSet<uint>();
        var priorRecentCount = recentUids.Count;
        if (SelectedReadOnly)
            recentUids.UnionWith(currentMessages.Where(message => message.IsRecent).Select(message => message.Uid));
        else
            recentUids.UnionWith(await ClaimRecentMessagesAsync(SelectedFolder.MailboxId, SelectedFolder.Id, ct));
        var current = currentMessages.Select(message => message.Uid).ToList();
        var currentSet = current.ToHashSet();
        var responses = new List<string>();
        for (var index = _selectedUidSnapshot.Count - 1; index >= 0; index--)
            if (!currentSet.Contains(_selectedUidSnapshot[index])) responses.Add($"* {index + 1} EXPUNGE");
        recentUids.IntersectWith(currentSet);
        if (current.Count > _selectedUidSnapshot.Count) responses.Add($"* {current.Count} EXISTS");
        if (recentUids.Count != priorRecentCount) responses.Add($"* {recentUids.Count} RECENT");
        _selectedUidSnapshot = current;
        _selectedFlagsSnapshot.Clear();
        foreach (var message in currentMessages) _selectedFlagsSnapshot[message.Uid] = $"{message.Flags}|{message.IsRead}";
        return new ImapCommandResult(tag, "OK", "NOOP completed", responses);
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
        if (!await AuthorizeMailboxAsync(folder.MailboxId, write: false, ct)) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        var messages = await _dbContext.Messages.AsNoTracking().Where(m => m.TenantId == TenantId && m.MailboxId == folder.MailboxId && m.FolderId == folder.Id).ToListAsync(ct);
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
        if (wanted.Contains("RECENT"))
        {
            var pendingRecentUids = messages.Where(message => message.IsRecent).Select(message => message.Uid).ToHashSet();
            if (SelectedFolder?.Id == folder.Id && _sessionRecentUidsByFolder.TryGetValue(folder.Id, out var sessionRecentUids))
            {
                var currentUids = messages.Select(message => message.Uid).ToHashSet();
                pendingRecentUids.UnionWith(sessionRecentUids.Where(currentUids.Contains));
            }
            fields.Add($"RECENT {pendingRecentUids.Count}");
        }
        if (wanted.Contains("UIDNEXT")) fields.Add($"UIDNEXT {folder.UidNext}");
        if (wanted.Contains("UIDVALIDITY")) fields.Add($"UIDVALIDITY {folder.UidValidity}");
        if (wanted.Contains("UNSEEN")) fields.Add($"UNSEEN {messages.Count(m => !m.IsRead && !m.Flags.Contains("\\Seen", StringComparison.OrdinalIgnoreCase))}");
        var escaped = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return new ImapCommandResult(tag, "OK", "STATUS completed", new[] { $"* STATUS \"{escaped}\" ({string.Join(' ', fields)})" });
    }

    private async Task<IReadOnlyList<uint>> ClaimRecentMessagesAsync(Guid mailboxId, Guid folderId, CancellationToken ct)
    {
        if (TenantId is not Guid tenantId) return Array.Empty<uint>();
        if (!_dbContext.Database.IsRelational())
        {
            var messages = await _dbContext.Messages
                .Where(message => message.TenantId == tenantId && message.MailboxId == mailboxId && message.FolderId == folderId && message.IsRecent)
                .ToListAsync(ct);
            foreach (var message in messages) message.IsRecent = false;
            if (messages.Count > 0) await _dbContext.SaveChangesAsync(ct);
            return messages.Select(message => message.Uid).ToArray();
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        var connection = _dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = _dbContext.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "UPDATE messages SET is_recent = FALSE WHERE tenant_id = @tenant AND mailbox_id = @mailbox AND folder_id = @folder AND is_recent = TRUE RETURNING uid";
        foreach (var (name, value) in new[] { ("@tenant", (object)tenantId), ("@mailbox", mailboxId), ("@folder", folderId) })
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        var uids = new List<uint>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) uids.Add(Convert.ToUInt32(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture));
        }
        await transaction.CommitAsync(ct);
        return uids;
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
        if (args[2].Kind != ImapTokenKind.LeftParenthesis || args[^1].Kind != ImapTokenKind.RightParenthesis)
            return new ImapCommandResult(tag, "BAD", "STORE flags must be parenthesized", Array.Empty<string>());
        var incoming = args.Skip(3).Take(args.Count - 4).Where(t => t.Kind == ImapTokenKind.Atom).Select(t => t.Text).ToArray();
        var permanentSystemFlags = new HashSet<string>(["\\Seen", "\\Answered", "\\Flagged", "\\Deleted", "\\Draft"], StringComparer.OrdinalIgnoreCase);
        if (incoming.Any(flag => flag.StartsWith('\\') && !permanentSystemFlags.Contains(flag)))
            return new ImapCommandResult(tag, "BAD", "Invalid STORE flag", Array.Empty<string>());
        var messages = await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == SelectedFolder.MailboxId && m.FolderId == SelectedFolder.Id)
            .OrderBy(m => m.Uid).Select(m => new ImapSequenceMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.IsRecent)).ToListAsync(ct);
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
            if (flags.SetEquals(ParseFlags(message.Flags)) && flags.Contains("\\Seen") == message.IsRead) continue;
            if (!await _messageService.SetImapFlagsAsync(TenantId.Value, _userId.Value, SelectedFolder.MailboxId, message.Id, flags.ToArray(), ct)) continue;
            if (!silent)
            {
                var sequence = (uint)(messages.IndexOf(message) + 1);
                var payload = uidMode ? $"UID {message.Uid} " : string.Empty;
                var isRecent = message.IsRecent || (_sessionRecentUidsByFolder.TryGetValue(SelectedFolder.Id, out var recentUids) && recentUids.Contains(message.Uid));
                responses.Add($"* {sequence} FETCH ({payload}FLAGS ({FormatFlags(string.Join(' ', flags), flags.Contains("\\Seen", StringComparer.OrdinalIgnoreCase), isRecent)}))");
            }
        }
        return new ImapCommandResult(tag, "OK", "STORE completed", responses);
    }

    private async Task<ImapCommandResult> HandleExpungeAsync(string tag, string? uidSetText, CancellationToken ct)
    {
        if (SelectedFolder is null) return new ImapCommandResult(tag, "NO", "No mailbox selected", Array.Empty<string>());
        if (SelectedReadOnly) return new ImapCommandResult(tag, "NO", "[READ-ONLY] Mailbox is read-only", Array.Empty<string>());
        if (_messageService is null || TenantId is null || MailboxId is null || _userId is null) return new ImapCommandResult(tag, "NO", "EXPUNGE unavailable", Array.Empty<string>());
        var messages = await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == SelectedFolder.MailboxId && m.FolderId == SelectedFolder.Id)
            .OrderBy(m => m.Uid).Select(m => new ImapSequenceMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.IsRecent)).ToListAsync(ct);
        SequenceSet? uidFilter = null;
        if (uidSetText is not null && !SequenceSet.TryParse(uidSetText, out uidFilter)) return new ImapCommandResult(tag, "BAD", "Invalid UID EXPUNGE sequence set", Array.Empty<string>());
        var doomed = messages.Select((m, i) => (Message: m, Sequence: (uint)(i + 1)))
            .Where(pair => ParseFlags(pair.Message.Flags).Any(f => f.Equals("\\Deleted", StringComparison.OrdinalIgnoreCase)) &&
                (uidFilter is null || uidFilter.Resolve(messages.Select(m => m.Uid).DefaultIfEmpty(0u).Max()).Contains(pair.Message.Uid)))
            .OrderByDescending(pair => pair.Sequence).ToArray();
        var responses = new List<string>();
        foreach (var pair in doomed)
        {
            if (await _messageService.DeleteMessageAsync(TenantId.Value, _userId.Value, SelectedFolder.MailboxId, pair.Message.Id, permanent: true, cancellationToken: ct))
                responses.Add($"* {pair.Sequence} EXPUNGE");
        }
        _selectedUidSnapshot = await _dbContext.Messages
            .Where(message => message.TenantId == TenantId && message.MailboxId == SelectedFolder.MailboxId && message.FolderId == SelectedFolder.Id)
            .OrderBy(message => message.Uid)
            .Select(message => message.Uid)
            .ToListAsync(ct);
        if (_sessionRecentUidsByFolder.TryGetValue(SelectedFolder.Id, out var sessionRecentUids))
            sessionRecentUids.IntersectWith(_selectedUidSnapshot);
        return new ImapCommandResult(tag, "OK", "EXPUNGE completed", responses);
    }

    private async Task<ImapCommandResult> HandleCopyAsync(string tag, IReadOnlyList<ImapToken> args, bool uidMode, CancellationToken ct)
    {
        if (SelectedFolder is null) return new ImapCommandResult(tag, "NO", "No mailbox selected", Array.Empty<string>());
        if (!await AuthorizeMailboxAsync(SelectedFolder.MailboxId, write: false, ct)) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        if (args.Count < 2 || !SequenceSet.TryParse(args[0].Text, out var set) || set is null) return new ImapCommandResult(tag, "BAD", "Invalid COPY command", Array.Empty<string>());
        if (_messageService is null || TenantId is null || MailboxId is null || _userId is null) return new ImapCommandResult(tag, "NO", "COPY unavailable", Array.Empty<string>());
        var destination = await FindFolderByPathAsync(args[1].Text, ct);
        if (destination is null)
        {
            var canReadNamespace = await CanAccessMailboxNamespaceAsync(args[1].Text, write: false, ct);
            return new ImapCommandResult(tag, "NO", canReadNamespace ? "[TRYCREATE] Mailbox does not exist" : "Mailbox does not exist", Array.Empty<string>());
        }
        if (!await AuthorizeMailboxAsync(destination.MailboxId, write: true, ct)) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        if (SelectedReadOnly && destination.Id == SelectedFolder.Id) return new ImapCommandResult(tag, "NO", "[READ-ONLY] Cannot COPY into selected mailbox", Array.Empty<string>());
        var messages = await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == SelectedFolder.MailboxId && m.FolderId == SelectedFolder.Id)
            .OrderBy(m => m.Uid).Select(m => new ImapSequenceMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.IsRecent)).ToListAsync(ct);
        var max = uidMode ? messages.Select(m => m.Uid).DefaultIfEmpty(0u).Max() : (uint)messages.Count;
        var selected = set.Resolve(max).Select(value => uidMode ? messages.FirstOrDefault(m => m.Uid == value) : value > 0 && value <= messages.Count ? messages[(int)value - 1] : null).Where(m => m is not null).Cast<ImapSequenceMessage>().ToArray();
        var newUids = new List<uint>();
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            foreach (var message in selected)
            {
                var newUid = await _messageService.CopyMessageAsync(TenantId.Value, _userId.Value, SelectedFolder.MailboxId, message.Id, destination.Id, ct);
                if (newUid is not uint uid)
                {
                    if (transaction is not null) await transaction.RollbackAsync(ct);
                    return new ImapCommandResult(tag, "NO", "COPY failed", Array.Empty<string>());
                }
                newUids.Add(uid);
            }
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw;
        }
        var sourceUids = string.Join(',', selected.Select(m => m.Uid));
        var destUids = string.Join(',', newUids);
        return new ImapCommandResult(tag, "OK", $"[COPYUID {destination.UidValidity} {sourceUids} {destUids}] COPY completed", Array.Empty<string>());
    }

    private async Task<ImapCommandResult> HandleMoveAsync(string tag, IReadOnlyList<ImapToken> args, bool uidMode, CancellationToken ct)
    {
        if (SelectedFolder is null) return new ImapCommandResult(tag, "NO", "No mailbox selected", Array.Empty<string>());
        if (SelectedReadOnly) return new ImapCommandResult(tag, "NO", "[READ-ONLY] Mailbox is read-only", Array.Empty<string>());
        if (!await AuthorizeMailboxAsync(SelectedFolder.MailboxId, write: true, ct)) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        if (args.Count < 2 || !SequenceSet.TryParse(args[0].Text, out var set) || set is null)
            return new ImapCommandResult(tag, "BAD", "Invalid MOVE command", Array.Empty<string>());
        if (_messageService is null || TenantId is null || _userId is null)
            return new ImapCommandResult(tag, "NO", "MOVE unavailable", Array.Empty<string>());

        var destination = await FindFolderByPathAsync(args[1].Text, ct);
        if (destination is null)
        {
            var canReadNamespace = await CanAccessMailboxNamespaceAsync(args[1].Text, write: false, ct);
            return new ImapCommandResult(tag, "NO", canReadNamespace ? "[TRYCREATE] Mailbox does not exist" : "Mailbox does not exist", Array.Empty<string>());
        }
        if (!await AuthorizeMailboxAsync(destination.MailboxId, write: true, ct))
            return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());

        var sourceFolder = SelectedFolder;
        var messages = await _dbContext.Messages
            .Where(m => m.TenantId == TenantId && m.MailboxId == sourceFolder.MailboxId && m.FolderId == sourceFolder.Id)
            .OrderBy(m => m.Uid)
            .Select(m => new ImapSequenceMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.IsRecent))
            .ToListAsync(ct);
        var maximum = uidMode ? messages.Select(m => m.Uid).DefaultIfEmpty(0u).Max() : (uint)messages.Count;
        var selected = set.Resolve(maximum)
            .Select(value => uidMode ? messages.FirstOrDefault(m => m.Uid == value) : value > 0 && value <= messages.Count ? messages[(int)value - 1] : null)
            .Where(message => message is not null).Cast<ImapSequenceMessage>().ToArray();
        var sequences = selected.Select(message => (Message: message, Sequence: (uint)(messages.IndexOf(message) + 1))).ToArray();
        var destinationUids = new List<uint>(selected.Length);
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            foreach (var message in selected)
            {
                var uid = await _messageService.MoveMessageToMailboxAsync(
                    TenantId.Value, _userId.Value, sourceFolder.MailboxId, message.Id, destination.MailboxId, destination.Id, ct);
                if (uid is not uint movedUid)
                {
                    if (transaction is not null) await transaction.RollbackAsync(ct);
                    return new ImapCommandResult(tag, "NO", "MOVE failed", Array.Empty<string>());
                }
                destinationUids.Add(movedUid);
            }
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw;
        }

        var sourceUids = string.Join(',', selected.Select(message => message.Uid));
        var destUids = string.Join(',', destinationUids);
        var responses = new List<string> { $"* OK [COPYUID {destination.UidValidity} {sourceUids} {destUids}] MOVE completed" };
        responses.AddRange(sequences.OrderByDescending(pair => pair.Sequence).Select(pair => $"* {pair.Sequence} EXPUNGE"));
        _selectedUidSnapshot = await _dbContext.Messages
            .Where(message => message.TenantId == TenantId && message.MailboxId == sourceFolder.MailboxId && message.FolderId == sourceFolder.Id)
            .OrderBy(message => message.Uid).Select(message => message.Uid).ToListAsync(ct);
        if (_sessionRecentUidsByFolder.TryGetValue(sourceFolder.Id, out var recentUids))
            recentUids.IntersectWith(_selectedUidSnapshot);
        return new ImapCommandResult(tag, "OK", "MOVE completed", responses);
    }

    private async Task<ImapCommandResult> HandleCreateFolderAsync(string tag, IReadOnlyList<ImapToken> args, CancellationToken ct)
    {
        var name = args.LastOrDefault(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString)?.Text;
        if (name is null || TenantId is null || MailboxId is null || _userId is null || _mailboxService is null) return new ImapCommandResult(tag, "NO", "CREATE unavailable", Array.Empty<string>());
        name = DecodeMailboxPath(name);
        Guid? namespaceMailboxId = null;
        string[] segments;
        if (name.StartsWith("Shared/", StringComparison.OrdinalIgnoreCase))
        {
            var sharedSegments = name[7..].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (sharedSegments.Length < 2) return new ImapCommandResult(tag, "NO", "Parent folder does not exist", Array.Empty<string>());
            var address = sharedSegments[0];
            var sharedMailbox = await _dbContext.Mailboxes.AsNoTracking()
                .FirstOrDefaultAsync(mailbox => mailbox.TenantId == TenantId && mailbox.Kind == "shared" && mailbox.Address.ToLower() == address.ToLower(), ct);
            if (sharedMailbox is null || !await AuthorizeMailboxAsync(sharedMailbox.Id, write: true, ct))
                return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
            namespaceMailboxId = sharedMailbox.Id;
            segments = sharedSegments[1..];
        }
        else
        {
            segments = name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        if (segments.Length == 0) return new ImapCommandResult(tag, "BAD", "Invalid folder name", Array.Empty<string>());
        var parentPath = string.Join('/', segments[..^1]);
        var parent = segments.Length > 1
            ? await FindFolderByPathAsync(namespaceMailboxId is Guid sharedId
                ? $"Shared/{await _dbContext.Mailboxes.Where(mailbox => mailbox.Id == sharedId).Select(mailbox => mailbox.Address).FirstAsync(ct)}/{parentPath}"
                : parentPath, ct)
            : null;
        if (segments.Length > 1 && parent is null) return new ImapCommandResult(tag, "NO", "[TRYCREATE] Parent folder does not exist", Array.Empty<string>());
        var createMailboxId = parent?.MailboxId ?? namespaceMailboxId ?? ActiveMailboxId;
        if (createMailboxId is null || !await AuthorizeMailboxAsync(createMailboxId.Value, write: true, ct)) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        try { await _mailboxService.CreateFolderAsync(TenantId.Value, _userId.Value, createMailboxId.Value, new CreateFolderRequest(segments[^1], parent?.Id), ct); return new ImapCommandResult(tag, "OK", "CREATE completed", Array.Empty<string>()); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)) { return new ImapCommandResult(tag, "NO", "[ALREADYEXISTS] Folder collision", Array.Empty<string>()); }
        catch (Exception ex) when (ex is ArgumentException or ResourceNotFoundException or InvalidOperationException) { return new ImapCommandResult(tag, "NO", "CREATE failed", Array.Empty<string>()); }
    }

    private async Task<ImapCommandResult> HandleDeleteFolderAsync(string tag, IReadOnlyList<ImapToken> args, CancellationToken ct)
    {
        var name = args.LastOrDefault(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString)?.Text;
        if (name is null || TenantId is null || MailboxId is null || _userId is null || _mailboxService is null) return new ImapCommandResult(tag, "NO", "DELETE unavailable", Array.Empty<string>());
        var folder = await FindFolderByPathAsync(name, ct);
        if (folder is null) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        if (!await AuthorizeMailboxAsync(folder.MailboxId, write: true, ct)) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        if (await _dbContext.Folders.AnyAsync(child => child.TenantId == TenantId && child.MailboxId == folder.MailboxId && child.ParentId == folder.Id, ct))
            return new ImapCommandResult(tag, "NO", "Mailbox has inferior hierarchical names", Array.Empty<string>());
        var deleted = await _mailboxService.DeleteFolderAsync(TenantId.Value, _userId.Value, folder.MailboxId, folder.Id, ct);
        return deleted ? new ImapCommandResult(tag, "OK", "DELETE completed", Array.Empty<string>()) : new ImapCommandResult(tag, "NO", name.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ? "[CANNOT] INBOX cannot be deleted" : "[CANNOT] Folder cannot be deleted", Array.Empty<string>());
    }

    private async Task<ImapCommandResult> HandleRenameFolderAsync(string tag, IReadOnlyList<ImapToken> args, CancellationToken ct)
    {
        var names = args.Where(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString).ToArray();
        if (names.Length < 2 || TenantId is null || MailboxId is null || _userId is null || _mailboxService is null || _messageService is null) return new ImapCommandResult(tag, "BAD", "RENAME requires source and destination", Array.Empty<string>());
        var folder = await FindFolderByPathAsync(names[0].Text, ct);
        if (folder is null) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        if (!await AuthorizeMailboxAsync(folder.MailboxId, write: true, ct)) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        try
        {
            var destinationPath = DecodeMailboxPath(names[1].Text);
            var destinationMailboxId = folder.MailboxId;
            if (destinationPath.StartsWith("Shared/", StringComparison.OrdinalIgnoreCase))
            {
                var sharedSegments = destinationPath[7..].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (sharedSegments.Length < 2) return new ImapCommandResult(tag, "NO", "[TRYCREATE] Parent folder does not exist", Array.Empty<string>());
                var sharedMailbox = await _dbContext.Mailboxes.AsNoTracking().FirstOrDefaultAsync(mailbox => mailbox.TenantId == TenantId && mailbox.Kind == "shared" && mailbox.Address.ToLower() == sharedSegments[0].ToLower(), ct);
                if (sharedMailbox is null || sharedMailbox.Id != folder.MailboxId || !await AuthorizeMailboxAsync(sharedMailbox.Id, write: true, ct))
                    return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
                destinationPath = string.Join('/', sharedSegments.Skip(1));
                destinationMailboxId = sharedMailbox.Id;
            }
            if (folder.Role.Equals("inbox", StringComparison.OrdinalIgnoreCase))
            {
                if (destinationMailboxId != MailboxId.Value) return new ImapCommandResult(tag, "NO", "[CANNOT] INBOX can only be renamed within the personal namespace", Array.Empty<string>());
                var pathSegments = destinationPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (pathSegments.Length == 0) return new ImapCommandResult(tag, "BAD", "Invalid destination folder", Array.Empty<string>());
                Guid? parentId = null;
                if (pathSegments.Length > 1)
                {
                    var parent = await FindFolderByPathAsync(string.Join('/', pathSegments[..^1]), ct);
                    if (parent is null || parent.MailboxId != folder.MailboxId) return new ImapCommandResult(tag, "NO", "[TRYCREATE] Parent folder does not exist", Array.Empty<string>());
                    parentId = parent.Id;
                }
                await using var transaction = _dbContext.Database.IsRelational() ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
                try
                {
                    var destination = await _mailboxService.CreateFolderAsync(TenantId.Value, _userId.Value, folder.MailboxId, new CreateFolderRequest(pathSegments[^1], parentId), ct);
                    var inboxMessages = await _dbContext.Messages.Where(message => message.TenantId == TenantId && message.MailboxId == folder.MailboxId && message.FolderId == folder.Id).OrderBy(message => message.Uid).Select(message => message.Id).ToListAsync(ct);
                    foreach (var messageId in inboxMessages)
                    {
                        if (await _messageService.CopyMessageAsync(TenantId.Value, _userId.Value, folder.MailboxId, messageId, destination.Id, ct) is null)
                            throw new InvalidOperationException("INBOX rename copy failed.");
                    }
                    foreach (var messageId in inboxMessages)
                    {
                        if (!await _messageService.DeleteMessageAsync(TenantId.Value, _userId.Value, folder.MailboxId, messageId, permanent: true, cancellationToken: ct))
                            throw new InvalidOperationException("INBOX rename move failed.");
                    }
                    if (transaction is not null) await transaction.CommitAsync(ct);
                }
                catch
                {
                    if (transaction is not null) await transaction.RollbackAsync(ct);
                    throw;
                }
            }
            else
            {
                await _mailboxService.RenameFolderAsync(TenantId.Value, _userId.Value, folder.MailboxId, folder.Id, destinationPath, ct);
            }
            return new ImapCommandResult(tag, "OK", "RENAME completed", Array.Empty<string>());
        }
        catch (ResourceNotFoundException) { return new ImapCommandResult(tag, "NO", "Parent folder does not exist", Array.Empty<string>()); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)) { return new ImapCommandResult(tag, "NO", "[ALREADYEXISTS] Folder collision", Array.Empty<string>()); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return new ImapCommandResult(tag, "NO", "RENAME failed", Array.Empty<string>()); }
    }

    private async Task<bool> HasMailboxAccessAsync(bool write, CancellationToken cancellationToken)
    {
        if (TenantId is not Guid tenant || ActiveMailboxId is not Guid mailbox || _userId is not Guid user) return false;
        var entity = await _dbContext.Mailboxes.AsNoTracking().FirstOrDefaultAsync(m => m.TenantId == tenant && m.Id == mailbox, cancellationToken);
        try { _authorization.AssertMailboxAccess(tenant, user, entity, write); return true; }
        catch (ResourceNotFoundException) { return false; }
    }

    private async Task<bool> AuthorizeMailboxAsync(Guid mailboxId, bool write, CancellationToken ct)
    {
        if (TenantId is not Guid tenant || _userId is not Guid user) return false;
        var mailbox = await _dbContext.Mailboxes.AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenant && m.Id == mailboxId, ct);
        try { _authorization.AssertMailboxAccess(tenant, user, mailbox, write); return true; }
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

        // RFC 3501 SELECT/EXAMINE deselects the previous mailbox before
        // attempting the new selection, even when that selection fails.
        SelectedFolder = null;
        SelectedReadOnly = false;
        _selectedUidSnapshot.Clear();
        _selectedFlagsSnapshot.Clear();
        State = ImapState.Authenticated;

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

        var canWrite = false;
        if (!await AuthorizeMailboxAsync(folder.MailboxId, write: false, cancellationToken))
            return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        if (!readOnly && TenantId is Guid tenantId && _userId is Guid userId)
        {
            canWrite = await AuthorizeMailboxAsync(folder.MailboxId, write: true, cancellationToken);
        }
        SelectedFolder = folder;
        State = ImapState.Selected;
        SelectedReadOnly = readOnly || !canWrite;

        var messageCount = await _dbContext.Messages
            .CountAsync(m => m.TenantId == TenantId && m.MailboxId == folder.MailboxId && m.FolderId == folder.Id, cancellationToken);

        var orderedMessages = await _dbContext.Messages
            .Where(m => m.TenantId == TenantId && m.MailboxId == folder.MailboxId && m.FolderId == folder.Id)
            .OrderBy(m => m.Uid).Select(m => new { m.Uid, m.IsRead, m.IsRecent, m.Flags }).ToListAsync(cancellationToken);
        if (!_sessionRecentUidsByFolder.TryGetValue(folder.Id, out var recentUids))
            _sessionRecentUidsByFolder[folder.Id] = recentUids = new HashSet<uint>();
        if (SelectedReadOnly)
            recentUids.UnionWith(orderedMessages.Where(message => message.IsRecent).Select(message => message.Uid));
        else
            recentUids.UnionWith(await ClaimRecentMessagesAsync(folder.MailboxId, folder.Id, cancellationToken));
        _selectedUidSnapshot = orderedMessages.Select(message => message.Uid).ToList();
        _selectedFlagsSnapshot.Clear();
        foreach (var message in orderedMessages) _selectedFlagsSnapshot[message.Uid] = $"{message.Flags}|{message.IsRead}";
        var firstUnseen = orderedMessages.FindIndex(m => !m.IsRead && !m.Flags.Contains("\\Seen", StringComparison.OrdinalIgnoreCase));

        var untagged = new List<string>
        {
            $"* {messageCount} EXISTS",
            $"* OK [UIDVALIDITY {folder.UidValidity}] UIDs valid",
            $"* OK [UIDNEXT {folder.UidNext}] Predicted next UID",
            "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)",
            $"* {recentUids.Count} RECENT"
        };
        if (firstUnseen >= 0) untagged.Add($"* OK [UNSEEN {firstUnseen + 1}] First unseen");
        untagged.Add(SelectedReadOnly ? "* OK [PERMANENTFLAGS ()] Flags are read-only" : "* OK [PERMANENTFLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft \\*)] Permanent flags");

        return new ImapCommandResult(tag, "OK", SelectedReadOnly ? "[READ-ONLY] " + (readOnly ? "EXAMINE" : "SELECT") + " completed" : "[READ-WRITE] SELECT completed", untagged);
    }

    private async Task<ImapCommandResult> HandleListAsync(string tag, IReadOnlyList<ImapToken> arguments, CancellationToken cancellationToken, bool subscribed = false)
    {
        if (State == ImapState.NotAuthenticated || MailboxId is null)
            return new ImapCommandResult(tag, "NO", "Not authenticated", Array.Empty<string>());
        var terms = arguments.Where(t => t.Kind is ImapTokenKind.Atom or ImapTokenKind.QuotedString or ImapTokenKind.Literal).ToArray();
        if (terms.Length < 2) return new ImapCommandResult(tag, "BAD", "LIST requires reference and pattern", Array.Empty<string>());
        var reference = DecodeMailboxPath(terms[0].Text);
        var pattern = DecodeMailboxPath(terms[1].Text);
        if (pattern.Length == 0)
            return new ImapCommandResult(tag, "OK", $"{(subscribed ? "LSUB" : "LIST")} completed", new[] { $"* {(subscribed ? "LSUB" : "LIST")} (\\Noselect) \"/\" \"\"" });
        var accessibleMailboxes = _mailboxService is null
            ? Array.Empty<MailboxDto>()
            : await _mailboxService.ListMailboxesAsync(TenantId!.Value, _userId!.Value, cancellationToken);
        var mailboxPaths = new Dictionary<Guid, string>();
        foreach (var mailbox in accessibleMailboxes)
        {
            if (!await AuthorizeMailboxAsync(mailbox.Id, write: false, cancellationToken)) continue;
            if (mailbox.Id == MailboxId.Value) mailboxPaths[mailbox.Id] = string.Empty;
            else if (mailbox.Kind.Equals("shared", StringComparison.OrdinalIgnoreCase)) mailboxPaths[mailbox.Id] = $"Shared/{mailbox.Address}";
        }
        if (_mailboxService is null && await AuthorizeMailboxAsync(MailboxId.Value, write: false, cancellationToken))
            mailboxPaths[MailboxId.Value] = string.Empty;
        var folders = await _dbContext.Folders.Where(f => f.TenantId == TenantId && mailboxPaths.Keys.Contains(f.MailboxId)).OrderBy(f => f.Name).ToListAsync(cancellationToken);
        var subscriptions = subscribed
            ? await _dbContext.ImapFolderSubscriptions.AsNoTracking()
                .Where(s => s.TenantId == TenantId && s.UserId == _userId && !s.Subscribed)
                .Select(s => s.FolderId).ToListAsync(cancellationToken)
            : new List<Guid>();
        var optedOutIds = subscriptions.ToHashSet();
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
            var local = string.Join('/', names);
            return mailboxPaths.TryGetValue(folder.MailboxId, out var prefix) && prefix.Length > 0 ? $"{prefix}/{local}" : local;
        }
        var fullPattern = string.IsNullOrEmpty(reference) ? pattern : reference.TrimEnd('/') + "/" + pattern.TrimStart('/');
        var regex = "^" + Regex.Escape(fullPattern).Replace("\\*", ".*").Replace("%", "[^/]*") + "$";
        var rows = new Dictionary<string, (Folder? Folder, bool IsSubscribed)>(StringComparer.OrdinalIgnoreCase);
        if (!subscribed)
        {
            foreach (var folder in folders) rows[FullName(folder)] = (folder, true);
            foreach (var prefix in mailboxPaths.Values.Where(value => value.StartsWith("Shared/", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                rows.TryAdd("Shared", (null, false));
                rows.TryAdd(prefix, (null, false));
            }
        }
        else
        {
            foreach (var folder in folders.Where(f => !optedOutIds.Contains(f.Id)))
            {
                var current = folder;
                var isSub = true;
                while (true)
                {
                    var name = FullName(current);
                    rows[name] = (current, isSub);
                    isSub = false;
                    if (current.ParentId is not Guid parentId) break;
                    var parent = folders.FirstOrDefault(f => f.Id == parentId);
                    if (parent is null) break;
                    current = parent;
                }

                if (mailboxPaths.TryGetValue(folder.MailboxId, out var prefix) && prefix.Length > 0)
                {
                    var slash = prefix.IndexOf('/');
                    var addressParent = slash >= 0 ? prefix : prefix;
                    rows.TryAdd("Shared", (null, false));
                    rows.TryAdd(addressParent, (null, false));
                }
            }
        }
        var responses = rows.Select(pair => (Folder: pair.Value.Folder, Name: pair.Key, IsSubscribed: pair.Value.IsSubscribed))
            .Where(pair => Regex.IsMatch(pair.Name, regex, RegexOptions.IgnoreCase))
            .Select(pair =>
            {
                var hasChildren = pair.Folder is not null && folders.Any(f => f.ParentId == pair.Folder.Id);
                var attributes = pair.Folder is null
                    ? new List<string> { "\\Noselect", "\\HasChildren" }
                    : subscribed && !pair.IsSubscribed
                        ? new List<string> { "\\Noselect", hasChildren ? "\\HasChildren" : "\\HasNoChildren" }
                        : new List<string> { hasChildren ? "\\HasChildren" : "\\HasNoChildren" };
                var special = pair.Folder?.Role.ToLowerInvariant() switch
                {
                    "sent" => "\\Sent", "drafts" => "\\Drafts", "trash" => "\\Trash", "junk" => "\\Junk", "archive" => "\\Archive", _ => null
                };
                if (special is not null && !(subscribed && !pair.IsSubscribed)) attributes.Add(special);
                var escapedName = EncodeMailboxPath(pair.Name).Replace("\\", "\\\\").Replace("\"", "\\\"");
                return $"* {(subscribed ? "LSUB" : "LIST")} ({string.Join(' ', attributes)}) \"/\" \"{escapedName}\"";
            }).ToArray();
        return new ImapCommandResult(tag, "OK", $"{(subscribed ? "LSUB" : "LIST")} completed", responses);
    }

    private async Task<ImapCommandResult> HandleSubscriptionAsync(string tag, IReadOnlyList<ImapToken> arguments, bool subscribe, CancellationToken cancellationToken)
    {
        var command = subscribe ? "SUBSCRIBE" : "UNSUBSCRIBE";
        if (TenantId is null || _userId is null || MailboxId is null || arguments.Count == 0)
            return new ImapCommandResult(tag, "BAD", $"{command} requires a mailbox", Array.Empty<string>());
        var path = DecodeMailboxPath(arguments[0].Text);
        var folder = await FindFolderByPathAsync(path, cancellationToken);
        if (folder is null || !await AuthorizeMailboxAsync(folder.MailboxId, write: false, cancellationToken))
            return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());
        var existing = await _dbContext.ImapFolderSubscriptions.FirstOrDefaultAsync(
            s => s.TenantId == TenantId && s.UserId == _userId && s.FolderId == folder.Id,
            cancellationToken);
        if (subscribe && existing is not null && !existing.Subscribed)
        {
            _dbContext.ImapFolderSubscriptions.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!subscribe && existing is null)
        {
            _dbContext.ImapFolderSubscriptions.Add(new ImapFolderSubscription
            {
                TenantId = TenantId.Value,
                UserId = _userId.Value,
                FolderId = folder.Id,
                Subscribed = false
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!subscribe && existing is { Subscribed: true })
        {
            existing.Subscribed = false;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        return new ImapCommandResult(tag, "OK", $"{command} completed", Array.Empty<string>());
    }

    private async Task<Folder?> FindFolderByPathAsync(string path, CancellationToken cancellationToken)
    {
        if (TenantId is null || MailboxId is null) return null;
        path = DecodeMailboxPath(path);
        var mailboxId = MailboxId.Value;
        var localPath = path;
        if (path.StartsWith("Shared/", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = path[7..];
            var separator = remainder.IndexOf('/');
            if (separator <= 0) return null;
            var address = remainder[..separator];
            localPath = remainder[(separator + 1)..];
            var mailbox = await _dbContext.Mailboxes.AsNoTracking().FirstOrDefaultAsync(m => m.TenantId == TenantId && m.Kind == "shared" && m.Address.ToLower() == address.ToLower(), cancellationToken);
            if (mailbox is null || !await AuthorizeMailboxAsync(mailbox.Id, write: false, cancellationToken)) return null;
            mailboxId = mailbox.Id;
        }
        var folders = await _dbContext.Folders.Where(f => f.TenantId == TenantId && f.MailboxId == mailboxId).ToListAsync(cancellationToken);
        Guid? parentId = null;
        Folder? match = null;
        foreach (var segment in localPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            match = folders.FirstOrDefault(f => f.ParentId == parentId && f.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (match is null) return null;
            parentId = match.Id;
        }
        // The session DbContext lives as long as the IMAP connection; UIDs are allocated in SQL by other
        // sessions and the worker, so refresh the tracked folder before reporting UIDNEXT/UIDVALIDITY.
        if (match is not null) await _dbContext.Entry(match).ReloadAsync(cancellationToken);
        return match;
    }

    private async Task<bool> CanAccessMailboxNamespaceAsync(string path, bool write, CancellationToken cancellationToken)
    {
        if (TenantId is not Guid tenantId || _userId is null || MailboxId is null) return false;
        path = DecodeMailboxPath(path);
        if (!path.StartsWith("Shared/", StringComparison.OrdinalIgnoreCase))
            return await AuthorizeMailboxAsync(MailboxId.Value, write, cancellationToken);
        var remainder = path[7..];
        var separator = remainder.IndexOf('/');
        if (separator <= 0) return false;
        var address = remainder[..separator];
        var mailbox = await _dbContext.Mailboxes.AsNoTracking().FirstOrDefaultAsync(
            candidate => candidate.TenantId == tenantId && candidate.Kind == "shared" && candidate.Address.ToLower() == address.ToLower(),
            cancellationToken);
        return mailbox is not null && await AuthorizeMailboxAsync(mailbox.Id, write, cancellationToken);
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
        var messageQuery = _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == SelectedFolder.MailboxId && m.FolderId == SelectedFolder.Id).OrderBy(m => m.Uid);
        List<ImapSearchMessage> messages;
        if (needsBody && needsHeaders)
            messages = await messageQuery.Select(m => new ImapSearchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.IsRecent, m.Sender, m.Recipient, m.Subject, m.Date, m.SizeBytes, m.RawHeaders, m.BodyText, m.BodyHtml)).ToListAsync(cancellationToken);
        else if (needsBody)
            messages = await messageQuery.Select(m => new ImapSearchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.IsRecent, m.Sender, m.Recipient, m.Subject, m.Date, m.SizeBytes, string.Empty, m.BodyText, m.BodyHtml)).ToListAsync(cancellationToken);
        else if (needsHeaders)
            messages = await messageQuery.Select(m => new ImapSearchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.IsRecent, m.Sender, m.Recipient, m.Subject, m.Date, m.SizeBytes, m.RawHeaders, null, null)).ToListAsync(cancellationToken);
        else
            messages = await messageQuery.Select(m => new ImapSearchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.IsRecent, m.Sender, m.Recipient, m.Subject, m.Date, m.SizeBytes, string.Empty, null, null)).ToListAsync(cancellationToken);
        if (_sessionRecentUidsByFolder.TryGetValue(SelectedFolder.Id, out var searchRecentUids))
            messages = messages.Select(message => message with { IsRecent = message.IsRecent || searchRecentUids.Contains(message.Uid) }).ToList();
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
            "NEW" => message.IsRecent && !message.IsRead && !message.Flags.Contains("\\Seen", StringComparison.OrdinalIgnoreCase),
            "OLD" => !message.IsRecent, "RECENT" => message.IsRecent,
            "FROM" => contains(message.Sender, arg), "TO" => contains(message.Recipient, arg),
            "CC" => recipients.Any(r => r.MessageId == message.Id && r.Type.Equals("cc", StringComparison.OrdinalIgnoreCase) && contains(r.Address, arg)),
            "BCC" => recipients.Any(r => r.MessageId == message.Id && r.Type.Equals("bcc", StringComparison.OrdinalIgnoreCase) && contains(r.Address, arg)),
            "SUBJECT" => contains(message.Subject, arg), "BODY" => contains(message.BodyText ?? string.Empty, arg) || contains(message.BodyHtml ?? string.Empty, arg),
            "TEXT" => contains(message.RawHeaders + message.Subject + message.BodyText + message.BodyHtml, arg),
            "HEADER" => key.Values.Count == 2 && message.RawHeaders.Split("\r\n", StringSplitOptions.None).Any(line => line.StartsWith(key.Values[0] + ":", StringComparison.OrdinalIgnoreCase) && contains(line, key.Values[1])),
            "KEYWORD" => flags.Contains(arg), "UNKEYWORD" => !flags.Contains(arg),
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
        if (folder is null)
        {
            var canWriteNamespace = await CanAccessMailboxNamespaceAsync(folderName, write: true, cancellationToken);
            return new ImapCommandResult(tag, "NO", canWriteNamespace ? "[TRYCREATE] Mailbox does not exist" : "Mailbox does not exist", Array.Empty<string>());
        }
        if (!await AuthorizeMailboxAsync(folder.MailboxId, write: true, cancellationToken)) return new ImapCommandResult(tag, "NO", "Mailbox does not exist", Array.Empty<string>());

        if (SelectedReadOnly && SelectedFolder?.Id == folder.Id)
            return new ImapCommandResult(tag, "NO", "[READ-ONLY] Selected mailbox is read-only", Array.Empty<string>());

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

        if (_uidAllocator is null) return new ImapCommandResult(tag, "NO", "UID allocation unavailable", Array.Empty<string>());
        uint assignedUid = await _uidAllocator.AllocateAsync(TenantId!.Value, folder.Id, cancellationToken: cancellationToken);
        folder.UpdatedAt = DateTimeOffset.UtcNow;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId!.Value,
            MailboxId = folder.MailboxId,
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

        var messages = await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == SelectedFolder.MailboxId && m.FolderId == SelectedFolder.Id)
            .OrderBy(m => m.Uid).Select(m => new ImapFetchMessage(m.Id, m.Uid, m.Flags, m.IsRead, m.IsRecent, m.Date, m.SizeBytes, m.ContentHash)).ToListAsync(cancellationToken);
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
                else if (itemName == "FLAGS")
                {
                    var isRecent = message.IsRecent || (_sessionRecentUidsByFolder.TryGetValue(SelectedFolder!.Id, out var recentUids) && recentUids.Contains(message.Uid));
                    AddValue("FLAGS", null, $"({FormatFlags(message.Flags, message.IsRead, isRecent)})", item);
                }
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
                if (_messageService is not null && TenantId is Guid tenantId && SelectedFolder is not null && _userId is Guid userId)
                {
                    var mailboxId = SelectedFolder.MailboxId;
                    await _messageService.SetImapFlagsAsync(tenantId, userId, mailboxId, message.Id, seenFlags, cancellationToken);
                }
                else
                {
                    await _dbContext.Messages.Where(m => m.TenantId == TenantId && m.MailboxId == SelectedFolder!.MailboxId && m.Id == message.Id)
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
    private static string FormatFlags(string flags, bool isRead, bool isRecent = false) => string.Join(' ', ParseFlags(flags)
        .Concat(isRead ? new[] { "\\Seen" } : Array.Empty<string>())
        .Concat(isRecent ? new[] { "\\Recent" } : Array.Empty<string>())
        .Distinct(StringComparer.OrdinalIgnoreCase));
}
