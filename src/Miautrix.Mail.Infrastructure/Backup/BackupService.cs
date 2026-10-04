using System.IO.Compression;
using System.Text.Json;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.Infrastructure.Backup;

public sealed record BackupOptions(string Directory);

public sealed record BackupManifest(
    string Version,
    DateTimeOffset CreatedAt,
    Dictionary<string, int> TableRowCounts);

public interface IBackupService
{
    Task<BackupManifest> CreateBackupAsync(AppDbContext sourceDb, string archivePath, CancellationToken cancellationToken = default);
    Task<BackupManifest> RestoreBackupAsync(string archivePath, AppDbContext targetDb, CancellationToken cancellationToken = default);
}

public sealed class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<BackupManifest> CreateBackupAsync(AppDbContext sourceDb, string archivePath, CancellationToken cancellationToken = default)
    {
        var rowCounts = new Dictionary<string, int>();

        var tenants = await sourceDb.Tenants.AsNoTracking().ToListAsync(cancellationToken);
        var domains = await sourceDb.Domains.AsNoTracking().ToListAsync(cancellationToken);
        var mailboxes = await sourceDb.Mailboxes.AsNoTracking().ToListAsync(cancellationToken);
        var folders = await sourceDb.Folders.AsNoTracking().ToListAsync(cancellationToken);
        var messages = await sourceDb.Messages.AsNoTracking().ToListAsync(cancellationToken);
        var queue = await sourceDb.SmtpQueue.AsNoTracking().ToListAsync(cancellationToken);
        var quarantine = await sourceDb.Quarantine.AsNoTracking().ToListAsync(cancellationToken);
        var auditLogs = await sourceDb.AuditLogs.AsNoTracking().ToListAsync(cancellationToken);

        rowCounts["Tenants"] = tenants.Count;
        rowCounts["Domains"] = domains.Count;
        rowCounts["Mailboxes"] = mailboxes.Count;
        rowCounts["Folders"] = folders.Count;
        rowCounts["Messages"] = messages.Count;
        rowCounts["SmtpQueue"] = queue.Count;
        rowCounts["Quarantine"] = quarantine.Count;
        rowCounts["AuditLogs"] = auditLogs.Count;

        var manifest = new BackupManifest("1.0", DateTimeOffset.UtcNow, rowCounts);

        var tempDir = Path.Combine(Path.GetTempPath(), $"miautrix_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOpts), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "tenants.json"), JsonSerializer.Serialize(tenants, JsonOpts), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "domains.json"), JsonSerializer.Serialize(domains, JsonOpts), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "mailboxes.json"), JsonSerializer.Serialize(mailboxes, JsonOpts), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "folders.json"), JsonSerializer.Serialize(folders, JsonOpts), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "messages.json"), JsonSerializer.Serialize(messages, JsonOpts), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "queue.json"), JsonSerializer.Serialize(queue, JsonOpts), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "quarantine.json"), JsonSerializer.Serialize(quarantine, JsonOpts), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "audit_logs.json"), JsonSerializer.Serialize(auditLogs, JsonOpts), cancellationToken);

            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            var dirName = Path.GetDirectoryName(archivePath);
            if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }

            ZipFile.CreateFromDirectory(tempDir, archivePath);
            return manifest;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    public async Task<BackupManifest> RestoreBackupAsync(string archivePath, AppDbContext targetDb, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException($"Backup archive not found: {archivePath}");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"miautrix_restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(archivePath, tempDir);

            var manifestJson = await File.ReadAllTextAsync(Path.Combine(tempDir, "manifest.json"), cancellationToken);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson, JsonOpts)
                ?? throw new InvalidOperationException("Invalid backup manifest");

            var tenantsJson = await File.ReadAllTextAsync(Path.Combine(tempDir, "tenants.json"), cancellationToken);
            var tenants = JsonSerializer.Deserialize<List<Tenant>>(tenantsJson, JsonOpts) ?? [];

            var domainsJson = await File.ReadAllTextAsync(Path.Combine(tempDir, "domains.json"), cancellationToken);
            var domains = JsonSerializer.Deserialize<List<DomainEntity>>(domainsJson, JsonOpts) ?? [];

            var mailboxesJson = await File.ReadAllTextAsync(Path.Combine(tempDir, "mailboxes.json"), cancellationToken);
            var mailboxes = JsonSerializer.Deserialize<List<Mailbox>>(mailboxesJson, JsonOpts) ?? [];

            var foldersJson = await File.ReadAllTextAsync(Path.Combine(tempDir, "folders.json"), cancellationToken);
            var folders = JsonSerializer.Deserialize<List<Folder>>(foldersJson, JsonOpts) ?? [];

            var messagesJson = await File.ReadAllTextAsync(Path.Combine(tempDir, "messages.json"), cancellationToken);
            var messages = JsonSerializer.Deserialize<List<Message>>(messagesJson, JsonOpts) ?? [];

            var queueJson = await File.ReadAllTextAsync(Path.Combine(tempDir, "queue.json"), cancellationToken);
            var queue = JsonSerializer.Deserialize<List<SmtpQueueItem>>(queueJson, JsonOpts) ?? [];

            var quarantineJson = await File.ReadAllTextAsync(Path.Combine(tempDir, "quarantine.json"), cancellationToken);
            var quarantine = JsonSerializer.Deserialize<List<QuarantineItem>>(quarantineJson, JsonOpts) ?? [];

            var auditJson = await File.ReadAllTextAsync(Path.Combine(tempDir, "audit_logs.json"), cancellationToken);
            var auditLogs = JsonSerializer.Deserialize<List<AuditLog>>(auditJson, JsonOpts) ?? [];

            // Populate target database in referential order
            foreach (var t in tenants)
            {
                if (!await targetDb.Tenants.AnyAsync(x => x.Id == t.Id, cancellationToken))
                    targetDb.Tenants.Add(t);
            }
            await targetDb.SaveChangesAsync(cancellationToken);

            foreach (var d in domains)
            {
                if (!await targetDb.Domains.AnyAsync(x => x.Id == d.Id, cancellationToken))
                    targetDb.Domains.Add(d);
            }
            await targetDb.SaveChangesAsync(cancellationToken);

            foreach (var m in mailboxes)
            {
                if (!await targetDb.Mailboxes.AnyAsync(x => x.Id == m.Id, cancellationToken))
                    targetDb.Mailboxes.Add(m);
            }
            await targetDb.SaveChangesAsync(cancellationToken);

            foreach (var f in folders)
            {
                if (!await targetDb.Folders.AnyAsync(x => x.Id == f.Id, cancellationToken))
                    targetDb.Folders.Add(f);
            }
            await targetDb.SaveChangesAsync(cancellationToken);

            foreach (var msg in messages)
            {
                if (!await targetDb.Messages.AnyAsync(x => x.Id == msg.Id, cancellationToken))
                    targetDb.Messages.Add(msg);
            }
            await targetDb.SaveChangesAsync(cancellationToken);

            foreach (var q in queue)
            {
                if (!await targetDb.SmtpQueue.AnyAsync(x => x.Id == q.Id, cancellationToken))
                    targetDb.SmtpQueue.Add(q);
            }
            await targetDb.SaveChangesAsync(cancellationToken);

            foreach (var q in quarantine)
            {
                if (!await targetDb.Quarantine.AnyAsync(x => x.Id == q.Id, cancellationToken))
                    targetDb.Quarantine.Add(q);
            }
            await targetDb.SaveChangesAsync(cancellationToken);

            foreach (var a in auditLogs)
            {
                if (!await targetDb.AuditLogs.AnyAsync(x => x.Id == a.Id, cancellationToken))
                    targetDb.AuditLogs.Add(a);
            }
            await targetDb.SaveChangesAsync(cancellationToken);

            return manifest;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
