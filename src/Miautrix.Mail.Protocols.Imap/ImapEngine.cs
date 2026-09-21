using System.Text;
using System.Text.RegularExpressions;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
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

public record ImapCommandResult(string Tag, string Status, string Message, IReadOnlyList<string> UntaggedResponses, byte[]? BinaryPayload = null);

public sealed partial class ImapSession
{
    private readonly AppDbContext _dbContext;
    private readonly IMailStorage _storage;
    private readonly IImapAuthenticator _authenticator;

    public Guid? TenantId { get; private set; }
    public Guid? MailboxId { get; private set; }
    public string? Username { get; private set; }
    public Folder? SelectedFolder { get; private set; }
    public ImapState State { get; private set; } = ImapState.NotAuthenticated;

    public ImapSession(AppDbContext dbContext, IMailStorage storage, IImapAuthenticator authenticator)
    {
        _dbContext = dbContext;
        _storage = storage;
        _authenticator = authenticator;
    }

    [GeneratedRegex(@"^(\S+)\s+(\S+)(?:\s+(.*))?$", RegexOptions.Compiled)]
    private static partial Regex CommandRegex();

    [GeneratedRegex(@"\((.*?)\)", RegexOptions.Compiled)]
    private static partial Regex FlagsRegex();

    [GeneratedRegex(@"\{(\d+)\}$", RegexOptions.Compiled)]
    private static partial Regex LiteralSizeRegex();

    public async Task<ImapCommandResult> ExecuteCommandAsync(
        string commandLine,
        Stream? literalPayload = null,
        CancellationToken cancellationToken = default)
    {
        var match = CommandRegex().Match(commandLine.Trim());
        if (!match.Success)
        {
            return new ImapCommandResult("*", "BAD", "Invalid command format", Array.Empty<string>());
        }

        var tag = match.Groups[1].Value;
        var commandName = match.Groups[2].Value.ToUpperInvariant();
        var arguments = match.Groups[3].Value;

        switch (commandName)
        {
            case "CAPABILITY":
                return new ImapCommandResult(
                    tag, "OK", "CAPABILITY completed",
                    new[] { "* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE" });

            case "NOOP":
                return new ImapCommandResult(tag, "OK", "NOOP completed", Array.Empty<string>());

            case "LOGOUT":
                State = ImapState.LoggedOut;
                return new ImapCommandResult(
                    tag, "OK", "LOGOUT completed",
                    new[] { "* BYE Miautrix Mail Server logging out" });

            case "LOGIN":
                return await HandleLoginAsync(tag, arguments, cancellationToken);

            case "SELECT":
                return await HandleSelectAsync(tag, arguments, cancellationToken);

            case "APPEND":
                return await HandleAppendAsync(tag, arguments, literalPayload, cancellationToken);

            case "FETCH":
            case "UID":
                return await HandleFetchOrUidAsync(tag, commandName, arguments, cancellationToken);

            default:
                return new ImapCommandResult(tag, "BAD", $"Unsupported command {commandName}", Array.Empty<string>());
        }
    }

    public async Task AuthenticateAsMailboxAsync(Guid tenantId, Guid mailboxId, string address)
    {
        TenantId = tenantId;
        MailboxId = mailboxId;
        Username = address;
        State = ImapState.Authenticated;
        await Task.CompletedTask;
    }

    private async Task<ImapCommandResult> HandleLoginAsync(string tag, string arguments, CancellationToken cancellationToken)
    {
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return new ImapCommandResult(tag, "BAD", "LOGIN requires username and password", Array.Empty<string>());
        }

        var user = parts[0].Trim('"');
        var password = parts[1].Trim('"');

        var auth = await _authenticator.AuthenticateAsync(user, password, cancellationToken);
        if (!auth.IsSuccess)
        {
            return new ImapCommandResult(tag, "NO", "LOGIN failed: invalid credentials", Array.Empty<string>());
        }

        TenantId = auth.TenantId;
        MailboxId = auth.MailboxId;
        Username = user;
        State = ImapState.Authenticated;

        return new ImapCommandResult(tag, "OK", "LOGIN completed", Array.Empty<string>());
    }

    private async Task<ImapCommandResult> HandleSelectAsync(string tag, string folderName, CancellationToken cancellationToken)
    {
        if (State == ImapState.NotAuthenticated || MailboxId == null)
        {
            return new ImapCommandResult(tag, "NO", "Not authenticated", Array.Empty<string>());
        }

        folderName = folderName.Trim().Trim('"');

        var folder = await _dbContext.Folders
            .FirstOrDefaultAsync(f => f.MailboxId == MailboxId.Value && f.Name.ToLower() == folderName.ToLower(), cancellationToken);

        if (folder == null)
        {
            // Auto-create standard folder if INBOX
            if (string.Equals(folderName, "INBOX", StringComparison.OrdinalIgnoreCase))
            {
                folder = new Folder
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId!.Value,
                    MailboxId = MailboxId.Value,
                    Name = "INBOX",
                    Role = "inbox",
                    UidNext = 1,
                    UidValidity = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.Folders.Add(folder);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                return new ImapCommandResult(tag, "NO", $"Folder {folderName} does not exist", Array.Empty<string>());
            }
        }

        SelectedFolder = folder;
        State = ImapState.Selected;

        var messageCount = await _dbContext.Messages
            .CountAsync(m => m.FolderId == folder.Id, cancellationToken);

        var untagged = new List<string>
        {
            $"* {messageCount} EXISTS",
            $"* OK [UIDVALIDITY {folder.UidValidity}] UIDs valid",
            $"* OK [UIDNEXT {folder.UidNext}] Predicted next UID",
            "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)"
        };

        return new ImapCommandResult(tag, "OK", "[READ-WRITE] SELECT completed", untagged);
    }

    private async Task<ImapCommandResult> HandleAppendAsync(
        string tag,
        string arguments,
        Stream? literalPayload,
        CancellationToken cancellationToken)
    {
        if (State == ImapState.NotAuthenticated || MailboxId == null)
        {
            return new ImapCommandResult(tag, "NO", "Not authenticated", Array.Empty<string>());
        }

        // Example: APPEND "INBOX" (\Seen \Flagged) {123}
        var folderNameMatch = Regex.Match(arguments, @"^""?([^""\s\(\)]+)""?");
        if (!folderNameMatch.Success)
        {
            return new ImapCommandResult(tag, "BAD", "Invalid APPEND arguments", Array.Empty<string>());
        }

        var folderName = folderNameMatch.Groups[1].Value;
        var folder = await _dbContext.Folders
            .FirstOrDefaultAsync(f => f.MailboxId == MailboxId.Value && f.Name.ToLower() == folderName.ToLower(), cancellationToken);

        if (folder == null)
        {
            folder = new Folder
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId!.Value,
                MailboxId = MailboxId.Value,
                Name = folderName,
                Role = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ? "inbox" : "custom",
                UidNext = 1,
                UidValidity = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.Folders.Add(folder);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        string flags = string.Empty;
        var flagsMatch = FlagsRegex().Match(arguments);
        if (flagsMatch.Success)
        {
            flags = flagsMatch.Groups[1].Value.Trim();
        }

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
            Date = DateTimeOffset.UtcNow,
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
        if (parts.Length < 1)
        {
            return new ImapCommandResult(tag, "BAD", "Invalid FETCH format", Array.Empty<string>());
        }

        var idStr = parts[0];
        if (!uint.TryParse(idStr, out var targetId))
        {
            targetId = 1;
        }

        var message = isUidMode
            ? await _dbContext.Messages.FirstOrDefaultAsync(m => m.FolderId == SelectedFolder.Id && m.Uid == targetId, cancellationToken)
            : await _dbContext.Messages.Where(m => m.FolderId == SelectedFolder.Id).OrderBy(m => m.Uid).FirstOrDefaultAsync(cancellationToken);

        if (message == null)
        {
            return new ImapCommandResult(tag, "OK", "FETCH completed (0 messages)", Array.Empty<string>());
        }

        byte[] messageBytes = Array.Empty<byte>();
        await using (var stream = await _storage.OpenReadAsync(message.ContentHash, cancellationToken))
        {
            if (stream != null)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                messageBytes = ms.ToArray();
            }
        }

        var untagged = new List<string>
        {
            $"* 1 FETCH (UID {message.Uid} FLAGS ({message.Flags}) RFC822 {{{messageBytes.Length}}})"
        };

        return new ImapCommandResult(
            tag, "OK", "FETCH completed", untagged, BinaryPayload: messageBytes);
    }
}
