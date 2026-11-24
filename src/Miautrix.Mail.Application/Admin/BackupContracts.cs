using Miautrix.Mail.Persistence;

namespace Miautrix.Mail.Infrastructure.Backup;

public sealed record BackupOptions(string Directory);
public sealed record BackupManifest(string Version, DateTimeOffset CreatedAt, Dictionary<string, int> TableRowCounts);
public interface IBackupService
{
    Task<BackupManifest> CreateBackupAsync(AppDbContext sourceDb, string archivePath, CancellationToken cancellationToken = default);
    Task<BackupManifest> RestoreBackupAsync(string archivePath, AppDbContext targetDb, CancellationToken cancellationToken = default);
}
