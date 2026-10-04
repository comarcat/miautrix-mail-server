using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Storage;
using Microsoft.EntityFrameworkCore;
using DomainAttachment = Miautrix.Mail.Domain.Attachment;
using DomainMessage = Miautrix.Mail.Domain.Message;

// Namespace deliberately avoids `Miautrix.Mail.Infrastructure.Mailbox`: that would shadow
// the `Miautrix.Mail.Domain.Mailbox` entity for every file under `Miautrix.Mail.Infrastructure`.
namespace Miautrix.Mail.Infrastructure.MailboxArchiving;

public sealed record MailboxArchiveAttachment(
    string FileName,
    string ContentType,
    long SizeBytes,
    bool Stored,
    string? ArchivePath);

public sealed record MailboxArchiveMessage(
    uint Uid,
    string Folder,
    string Subject,
    string Sender,
    string Recipient,
    DateTimeOffset Date,
    long SizeBytes,
    string EmlPath,
    IReadOnlyList<MailboxArchiveAttachment> Attachments);

public sealed record MailboxArchiveManifest(
    string Version,
    DateTimeOffset CreatedAt,
    Guid MailboxId,
    string Address,
    int FolderCount,
    int MessageCount,
    int AttachmentCount,
    IReadOnlyList<string> MissingBlobs,
    IReadOnlyList<MailboxArchiveMessage> Messages);

public interface IMailboxArchiveService
{
    /// <summary>Builds a ZIP of the mailbox contents into a temp file and returns its path.</summary>
    Task<string> CreateArchiveAsync(AppDbContext db, Guid tenantId, Guid mailboxId, string mailboxAddress, CancellationToken ct = default);

    /// <summary>Deletes content-addressed blobs that no remaining message or attachment references.</summary>
    Task<int> DeleteBlobsIfUnreferencedAsync(AppDbContext db, IReadOnlyCollection<string> contentHashes, CancellationToken ct = default);
}

public sealed class MailboxArchiveService : IMailboxArchiveService
{
    private const string UnfiledFolder = "_unfiled";
    private const int MaxEmbeddedAttachmentBytes = 25 * 1024 * 1024;

    private static readonly string[] ReservedFileNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IMailStorage _storage;

    public MailboxArchiveService(IMailStorage storage)
    {
        _storage = storage;
    }

    public async Task<string> CreateArchiveAsync(AppDbContext db, Guid tenantId, Guid mailboxId, string mailboxAddress, CancellationToken ct = default)
    {
        var folders = await db.Folders.AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.MailboxId == mailboxId)
            .ToListAsync(ct);
        var messages = await db.Messages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MailboxId == mailboxId)
            .OrderBy(m => m.FolderId).ThenBy(m => m.Uid)
            .ToListAsync(ct);
        var messageIds = messages.Select(m => m.Id).ToArray();
        var attachments = messageIds.Length == 0
            ? []
            : await db.Attachments.AsNoTracking()
                .Where(a => a.TenantId == tenantId && messageIds.Contains(a.MessageId))
                .ToListAsync(ct);

        var folderNames = folders.ToDictionary(f => f.Id, f => f.Name);
        var attachmentsByMessage = attachments
            .GroupBy(a => a.MessageId)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.FileName, StringComparer.OrdinalIgnoreCase).ToList());

        var rootName = SanitizeName(mailboxAddress, "mailbox");
        var tempDir = Path.Combine(Path.GetTempPath(), $"miautrix_export_{Guid.NewGuid():N}");
        var rootDir = Path.Combine(tempDir, rootName);
        Directory.CreateDirectory(rootDir);

        var missingBlobs = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var archiveMessages = new List<MailboxArchiveMessage>();
        var usedFolderDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folderDirsById = new Dictionary<Guid, string>();
        var usedEmlNamesByFolder = new Dictionary<Guid, HashSet<string>>();
        var attachmentCount = 0;
        var messageIndex = 0;

        try
        {
            foreach (var message in messages)
            {
                messageIndex++;
                var folderName = folderNames.TryGetValue(message.FolderId, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : UnfiledFolder;

                if (!folderDirsById.TryGetValue(message.FolderId, out var folderDir))
                {
                    folderDir = Path.Combine(rootDir, UniqueName(usedFolderDirs, SanitizeName(folderName, UnfiledFolder)));
                    folderDirsById[message.FolderId] = folderDir;
                    Directory.CreateDirectory(folderDir);
                }

                var messageAttachments = attachmentsByMessage.TryGetValue(message.Id, out var list) ? list : [];
                var archiveAttachments = new List<MailboxArchiveAttachment>(messageAttachments.Count);

                Dictionary<Guid, byte[]>? resolved = null;
                if (messageAttachments.Count > 0)
                {
                    resolved = [];
                    foreach (var attachment in messageAttachments)
                    {
                        var bytes = await ReadBlobAsync(attachment.ContentHash, ct);
                        if (bytes is null)
                        {
                            missingBlobs.Add(attachment.ContentHash);
                            continue;
                        }
                        if (bytes.LongLength <= MaxEmbeddedAttachmentBytes)
                        {
                            resolved[attachment.Id] = bytes;
                        }
                    }
                }

                if (!usedEmlNamesByFolder.TryGetValue(message.FolderId, out var usedEmlNames))
                {
                    usedEmlNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    usedEmlNamesByFolder[message.FolderId] = usedEmlNames;
                }

                var emlFileName = UniqueName(usedEmlNames, $"{messageIndex} - {SanitizeName(message.Subject, "no subject")}.eml");
                var emlRelativePath = $"{rootName}/{Path.GetFileName(folderDir)}/{emlFileName}";
                var emlPath = Path.Combine(folderDir, emlFileName);
                var emlBytes = BuildEml(message, messageAttachments, resolved);
                await File.WriteAllBytesAsync(emlPath, emlBytes, ct);

                if (messageAttachments.Count > 0)
                {
                    var attachmentDir = Path.Combine(folderDir, "attachments");
                    Directory.CreateDirectory(attachmentDir);
                    var usedAttachmentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var attachmentIndex = 0;

                    foreach (var attachment in messageAttachments)
                    {
                        attachmentIndex++;
                        if (resolved is null || !resolved.TryGetValue(attachment.Id, out var bytes))
                        {
                            archiveAttachments.Add(new MailboxArchiveAttachment(
                                attachment.FileName, attachment.ContentType, attachment.SizeBytes, false, null));
                            continue;
                        }

                        var fileName = UniqueName(usedAttachmentNames, $"{messageIndex}.{attachmentIndex} {SanitizeName(attachment.FileName, "attachment")}");
                        await File.WriteAllBytesAsync(Path.Combine(attachmentDir, fileName), bytes, ct);
                        attachmentCount++;
                        archiveAttachments.Add(new MailboxArchiveAttachment(
                            attachment.FileName,
                            attachment.ContentType,
                            bytes.LongLength,
                            true,
                            $"{rootName}/{Path.GetFileName(folderDir)}/attachments/{fileName}"));
                    }
                }

                archiveMessages.Add(new MailboxArchiveMessage(
                    message.Uid,
                    folderName,
                    message.Subject,
                    message.Sender,
                    message.Recipient,
                    message.Date,
                    emlBytes.LongLength,
                    emlRelativePath,
                    archiveAttachments));
            }

            var manifest = new MailboxArchiveManifest(
                "1.0",
                DateTimeOffset.UtcNow,
                mailboxId,
                mailboxAddress,
                usedFolderDirs.Count,
                archiveMessages.Count,
                attachmentCount,
                missingBlobs.ToList(),
                archiveMessages);

            await File.WriteAllTextAsync(
                Path.Combine(rootDir, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOpts),
                ct);

            var zipPath = Path.Combine(Path.GetTempPath(), $"miautrix_export_{rootName}_{Guid.NewGuid():N}.zip");
            ZipFile.CreateFromDirectory(tempDir, zipPath);
            return zipPath;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public async Task<int> DeleteBlobsIfUnreferencedAsync(AppDbContext db, IReadOnlyCollection<string> contentHashes, CancellationToken ct = default)
    {
        var hashes = contentHashes
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        if (hashes.Count == 0)
        {
            return 0;
        }

        var referencedByMessages = await db.Messages.AsNoTracking()
            .Where(m => hashes.Contains(m.ContentHash.ToLower()))
            .Select(m => m.ContentHash)
            .Distinct()
            .ToListAsync(ct);
        var referencedByAttachments = await db.Attachments.AsNoTracking()
            .Where(a => hashes.Contains(a.ContentHash.ToLower()))
            .Select(a => a.ContentHash)
            .Distinct()
            .ToListAsync(ct);

        var stillReferenced = new HashSet<string>(
            referencedByMessages.Concat(referencedByAttachments).Select(h => h.ToLowerInvariant()),
            StringComparer.Ordinal);

        var deleted = 0;
        foreach (var hash in hashes)
        {
            if (stillReferenced.Contains(hash))
            {
                continue;
            }

            await _storage.DeleteAsync(hash, ct);
            deleted++;
        }

        return deleted;
    }

    private async Task<byte[]?> ReadBlobAsync(string contentHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        await using var stream = await _storage.OpenReadAsync(contentHash, ct);
        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static byte[] BuildEml(DomainMessage message, IReadOnlyList<DomainAttachment> attachments, Dictionary<Guid, byte[]>? resolved)
    {
        var hasInlineParts = resolved is { Count: > 0 };
        var builder = new StringBuilder();

        foreach (var line in StripMimeHeaders(message.RawHeaders))
        {
            builder.Append(line).Append("\r\n");
        }

        builder.Append("MIME-Version: 1.0\r\n");

        var textBody = message.BodyText;
        var htmlBody = message.BodyHtml;
        var hasText = !string.IsNullOrEmpty(textBody);
        var hasHtml = !string.IsNullOrEmpty(htmlBody);

        if (!hasInlineParts)
        {
            if (hasHtml)
            {
                builder.Append("Content-Type: text/html; charset=utf-8\r\n");
                builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
                builder.Append(htmlBody);
            }
            else
            {
                builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
                builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
                builder.Append(textBody ?? string.Empty);
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        const string mixedBoundary = "----=_miautrix_mixed";
        const string altBoundary = "----=_miautrix_alternative";

        builder.Append("Content-Type: multipart/mixed; boundary=\"").Append(mixedBoundary).Append("\"\r\n\r\n");
        builder.Append("--").Append(mixedBoundary).Append("\r\n");

        if (hasText && hasHtml)
        {
            builder.Append("Content-Type: multipart/alternative; boundary=\"").Append(altBoundary).Append("\"\r\n\r\n");
            builder.Append("--").Append(altBoundary).Append("\r\n");
            builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
            builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
            builder.Append(textBody).Append("\r\n");
            builder.Append("--").Append(altBoundary).Append("\r\n");
            builder.Append("Content-Type: text/html; charset=utf-8\r\n");
            builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
            builder.Append(htmlBody).Append("\r\n");
            builder.Append("--").Append(altBoundary).Append("--\r\n");
        }
        else if (hasHtml)
        {
            builder.Append("Content-Type: text/html; charset=utf-8\r\n");
            builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
            builder.Append(htmlBody).Append("\r\n");
        }
        else
        {
            builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
            builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
            builder.Append((textBody ?? string.Empty)).Append("\r\n");
        }

        foreach (var attachment in attachments)
        {
            if (resolved is null || !resolved.TryGetValue(attachment.Id, out var bytes))
            {
                continue;
            }

            var fileName = SanitizeName(attachment.FileName, "attachment");
            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;

            builder.Append("\r\n--").Append(mixedBoundary).Append("\r\n");
            builder.Append("Content-Type: ").Append(contentType).Append("; name=\"").Append(fileName).Append("\"\r\n");
            builder.Append("Content-Transfer-Encoding: base64\r\n");
            builder.Append("Content-Disposition: attachment; filename=\"").Append(fileName).Append("\"\r\n\r\n");
            builder.Append(WrapBase64(bytes)).Append("\r\n");
        }

        builder.Append("--").Append(mixedBoundary).Append("--\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static IEnumerable<string> StripMimeHeaders(string? rawHeaders)
    {
        var text = (rawHeaders ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                var headerName = line[..colon].Trim().ToLowerInvariant();
                if (headerName is "content-type" or "mime-version" or "content-transfer-encoding")
                {
                    continue;
                }
            }

            yield return line;
        }
    }

    private static string WrapBase64(byte[] bytes)
    {
        var encoded = Convert.ToBase64String(bytes);
        if (encoded.Length <= 76)
        {
            return encoded;
        }

        var builder = new StringBuilder(encoded.Length + (encoded.Length / 76) * 2);
        for (var i = 0; i < encoded.Length; i += 76)
        {
            var length = Math.Min(76, encoded.Length - i);
            builder.Append(encoded, i, length).Append("\r\n");
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string UniqueName(HashSet<string> used, string name)
    {
        if (used.Add(name))
        {
            return name;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        for (var index = 2; ; index++)
        {
            var candidate = $"{stem} ({index}){extension}";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string SanitizeName(string? name, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(name) ? fallback : name;
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(raw.Length);

        foreach (var character in raw)
        {
            if (character is '/' or '\\' || Array.IndexOf(invalid, character) >= 0)
            {
                builder.Append('_');
                continue;
            }

            builder.Append(character);
        }

        var cleaned = builder.ToString().Trim().Trim('.');
        if (cleaned.Length == 0)
        {
            cleaned = fallback;
        }

        if (ReservedFileNames.Contains(cleaned.ToUpperInvariant()))
        {
            cleaned = "_" + cleaned;
        }

        return cleaned.Length > 120 ? cleaned[..120] : cleaned;
    }
}
