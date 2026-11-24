using Miautrix.Mail.Persistence;

namespace Miautrix.Mail.Infrastructure.MailboxArchiving;

public sealed record MailboxArchiveAttachment(string FileName, string ContentType, long SizeBytes, bool Stored, string? ArchivePath);
public sealed record MailboxArchiveMessage(uint Uid, string Folder, string Subject, string Sender, string Recipient, DateTimeOffset Date, long SizeBytes, string EmlPath, IReadOnlyList<MailboxArchiveAttachment> Attachments);
public sealed record MailboxArchiveManifest(string Version, DateTimeOffset CreatedAt, Guid MailboxId, string Address, int FolderCount, int MessageCount, int AttachmentCount, IReadOnlyList<string> MissingBlobs, IReadOnlyList<MailboxArchiveMessage> Messages);

public interface IMailboxArchiveService
{
    Task<string> CreateArchiveAsync(AppDbContext db, Guid tenantId, Guid mailboxId, string mailboxAddress, CancellationToken ct = default);
    Task<int> DeleteBlobsIfUnreferencedAsync(AppDbContext db, IReadOnlyCollection<string> contentHashes, CancellationToken ct = default);
}
