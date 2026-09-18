using Miautrix.Mail.Domain;
using Miautrix.Mail.Infrastructure.Backup;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.IntegrationTests.Backup;

[Trait("Category", "Backup")]
public class BackupRestoreTests
{
    private AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAndRestoreBackup_ProducesMatchingRowCounts()
    {
        var sourceDbName = $"source_db_{Guid.NewGuid():N}";
        var targetDbName = $"target_db_{Guid.NewGuid():N}";

        using var sourceDb = CreateDbContext(sourceDbName);

        // Populate source data
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Slug = "acme",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        sourceDb.Tenants.Add(tenant);

        var domainId = Guid.NewGuid();
        var domain = new DomainEntity
        {
            Id = domainId,
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        sourceDb.Domains.Add(domain);

        var mailboxId = Guid.NewGuid();
        var mailbox = new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            DomainId = domainId,
            Address = "alice@acme.corp",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        sourceDb.Mailboxes.Add(mailbox);

        var folderId = Guid.NewGuid();
        var folder = new Folder
        {
            Id = folderId,
            TenantId = tenantId,
            MailboxId = mailboxId,
            Name = "INBOX",
            Role = "inbox",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        sourceDb.Folders.Add(folder);

        var messageId = Guid.NewGuid();
        var message = new Message
        {
            Id = messageId,
            TenantId = tenantId,
            MailboxId = mailboxId,
            FolderId = folderId,
            StoragePath = "blobs/msg1.eml",
            SizeBytes = 1024,
            Subject = "Welcome Alice",
            Sender = "system@acme.corp",
            Recipient = "alice@acme.corp",
            CreatedAt = DateTimeOffset.UtcNow
        };
        sourceDb.Messages.Add(message);

        var queueItem = new SmtpQueueItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Sender = "alice@acme.corp",
            Recipient = "bob@example.com",
            Status = "Queued",
            CreatedAt = DateTimeOffset.UtcNow,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5),
            RawMessage = "From: alice\nTo: bob\n\nHi"
        };
        sourceDb.SmtpQueue.Add(queueItem);

        var quarantineItem = new QuarantineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Sender = "spammer@evil.com",
            Recipient = "alice@acme.corp",
            SpamScore = 8.5,
            RawMessage = "Spam content",
            QuarantinedAt = DateTimeOffset.UtcNow
        };
        sourceDb.Quarantine.Add(quarantineItem);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Action = "MailboxCreated",
            TargetType = "Mailbox",
            TargetId = mailboxId,
            IpAddress = "127.0.0.1",
            DetailsJson = "{}"
        };
        sourceDb.AuditLogs.Add(auditLog);

        await sourceDb.SaveChangesAsync();

        var backupService = new BackupService();
        var tempArchive = Path.Combine(Path.GetTempPath(), $"miautrix_test_backup_{Guid.NewGuid():N}.zip");

        try
        {
            // Act: Create backup
            var manifest = await backupService.CreateBackupAsync(sourceDb, tempArchive);
            Assert.NotNull(manifest);
            Assert.True(File.Exists(tempArchive));

            // Verify row counts in manifest
            Assert.Equal(1, manifest.TableRowCounts["Tenants"]);
            Assert.Equal(1, manifest.TableRowCounts["Domains"]);
            Assert.Equal(1, manifest.TableRowCounts["Mailboxes"]);
            Assert.Equal(1, manifest.TableRowCounts["Folders"]);
            Assert.Equal(1, manifest.TableRowCounts["Messages"]);
            Assert.Equal(1, manifest.TableRowCounts["SmtpQueue"]);
            Assert.Equal(1, manifest.TableRowCounts["Quarantine"]);
            Assert.Equal(1, manifest.TableRowCounts["AuditLogs"]);

            // Act: Restore to scratch database
            using var targetDb = CreateDbContext(targetDbName);
            var restoredManifest = await backupService.RestoreBackupAsync(tempArchive, targetDb);

            // Assert: verify matching row counts in target DB
            Assert.Equal(await sourceDb.Tenants.CountAsync(), await targetDb.Tenants.CountAsync());
            Assert.Equal(await sourceDb.Domains.CountAsync(), await targetDb.Domains.CountAsync());
            Assert.Equal(await sourceDb.Mailboxes.CountAsync(), await targetDb.Mailboxes.CountAsync());
            Assert.Equal(await sourceDb.Folders.CountAsync(), await targetDb.Folders.CountAsync());
            Assert.Equal(await sourceDb.Messages.CountAsync(), await targetDb.Messages.CountAsync());
            Assert.Equal(await sourceDb.SmtpQueue.CountAsync(), await targetDb.SmtpQueue.CountAsync());
            Assert.Equal(await sourceDb.Quarantine.CountAsync(), await targetDb.Quarantine.CountAsync());
            Assert.Equal(await sourceDb.AuditLogs.CountAsync(), await targetDb.AuditLogs.CountAsync());

            // Check specific entities
            var restoredMailbox = await targetDb.Mailboxes.FirstOrDefaultAsync(m => m.Id == mailboxId);
            Assert.NotNull(restoredMailbox);
            Assert.Equal("alice@acme.corp", restoredMailbox.Address);
        }
        finally
        {
            if (File.Exists(tempArchive))
            {
                File.Delete(tempArchive);
            }
        }
    }
}
