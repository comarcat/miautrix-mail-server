using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.IntegrationTests.Mail;

public sealed class MessageUidAllocatorTests : IClassFixture<UidPostgresFixture>
{
    private readonly UidPostgresFixture _fixture;

    public MessageUidAllocatorTests(UidPostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Allocator_returns_contiguous_blocks_and_is_atomic_and_tenant_scoped()
    {
        var connectionString = await _fixture.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var (tenant, _, _, folder) = await SeedFolderAsync(db);
        var otherTenant = new Tenant { Id = Guid.NewGuid(), Slug = "other-" + Guid.NewGuid().ToString("N") };
        db.Tenants.Add(otherTenant);
        await db.SaveChangesAsync();

        var allocator = new MessageUidAllocator(db);
        Assert.Equal(1u, await allocator.AllocateAsync(tenant.Id, folder.Id));
        Assert.Equal(2u, await allocator.AllocateAsync(tenant.Id, folder.Id, 3));
        Assert.Equal(5u, await allocator.AllocateAsync(tenant.Id, folder.Id));
        await Assert.ThrowsAsync<Miautrix.Mail.Security.ResourceNotFoundException>(
            () => allocator.AllocateAsync(otherTenant.Id, folder.Id));

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(async _ =>
            {
                await using var concurrentDb = new AppDbContext(options);
                return await new MessageUidAllocator(concurrentDb).AllocateAsync(tenant.Id, folder.Id);
            }));
        Assert.Equal(20, concurrent.Distinct().Count());
        Assert.Equal(Enumerable.Range(6, 20).Select(x => (uint)x).ToArray(), concurrent.Order().ToArray());
        await db.Entry(folder).ReloadAsync();
        Assert.Equal(26u, folder.UidNext);
    }

    [Fact]
    public async Task Migration_renumbers_existing_uids_and_enforces_positive_unique_values()
    {
        var connectionString = await _fixture.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var (tenant, mailbox, firstFolder, secondFolder) = await SeedFolderAsync(db);
        var firstValidity = firstFolder.UidValidity;
        var secondValidity = secondFolder.UidValidity;
        var time = DateTimeOffset.UtcNow;
        db.Messages.AddRange(
            NewMessage(tenant.Id, mailbox.Id, firstFolder.Id, 51, time.AddMinutes(2)),
            NewMessage(tenant.Id, mailbox.Id, firstFolder.Id, 52, time.AddMinutes(1)),
            NewMessage(tenant.Id, mailbox.Id, secondFolder.Id, 72, time.AddMinutes(1)),
            NewMessage(tenant.Id, mailbox.Id, secondFolder.Id, 73, time.AddMinutes(2)));
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync("ALTER TABLE messages DROP CONSTRAINT ck_messages_uid_positive; DROP INDEX uq_messages_folder_id_uid; UPDATE messages SET uid = 0; UPDATE folders SET uid_next = 90;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261124173531_Imap006StableFolderUids';");
        await db.GetService<IMigrator>().MigrateAsync();

        var first = await db.Messages.AsNoTracking().Where(m => m.FolderId == firstFolder.Id).OrderBy(m => m.Date).ToListAsync();
        var second = await db.Messages.AsNoTracking().Where(m => m.FolderId == secondFolder.Id).OrderBy(m => m.Date).ToListAsync();
        Assert.Equal(new uint[] { 1, 2 }, first.Select(m => m.Uid));
        Assert.Equal(new uint[] { 1, 2 }, second.Select(m => m.Uid));
        Assert.Equal(3u, (await db.Folders.AsNoTracking().SingleAsync(f => f.Id == firstFolder.Id)).UidNext);
        Assert.Equal(3u, (await db.Folders.AsNoTracking().SingleAsync(f => f.Id == secondFolder.Id)).UidNext);
        Assert.NotEqual(firstValidity, (await db.Folders.AsNoTracking().SingleAsync(f => f.Id == firstFolder.Id)).UidValidity);
        Assert.NotEqual(secondValidity, (await db.Folders.AsNoTracking().SingleAsync(f => f.Id == secondFolder.Id)).UidValidity);

        var zeroUid = NewMessage(tenant.Id, mailbox.Id, firstFolder.Id, 0, time.AddDays(1));
        db.Messages.Add(zeroUid);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.Entry(zeroUid).State = EntityState.Detached;
        var duplicateUid = NewMessage(tenant.Id, mailbox.Id, firstFolder.Id, 1, time.AddDays(2));
        db.Messages.Add(duplicateUid);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Migrations_apply_over_production_drift_from_superseded_queue_direction_migration()
    {
        // Production ran 20261005173819_AddSmtpQueueDirection: column present (DEFAULT 'Inbound'), no
        // check constraint, and no history row for 20261006120000_AddQueueDirection (ERRORS §13.10).
        var connectionString = await _fixture.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006120000_AddQueueDirection");
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE smtp_queue DROP CONSTRAINT ck_smtp_queue_direction;
            ALTER TABLE smtp_queue ALTER COLUMN direction SET DEFAULT 'Inbound';
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006120000_AddQueueDirection';
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20261005173819_AddSmtpQueueDirection', '10.0.4');
            DROP TABLE flag_alert_configurations;
            CREATE INDEX "IX_smtp_queue_tenant_id_direction_status_next_attempt_at" ON smtp_queue (tenant_id, direction, status, next_attempt_at);
            """);

        await migrator.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var constraints = await db.Database.SqlQueryRaw<string>(
            "SELECT conname AS \"Value\" FROM pg_constraint WHERE conname IN ('ck_smtp_queue_direction', 'ck_messages_uid_positive')").ToListAsync();
        Assert.Equal(2, constraints.Count);
        var defaults = await db.Database.SqlQueryRaw<string>(
            "SELECT coalesce(column_default, '') AS \"Value\" FROM information_schema.columns WHERE table_name = 'smtp_queue' AND column_name = 'direction'").ToListAsync();
        Assert.Equal(string.Empty, Assert.Single(defaults));
        var converged = await db.Database.SqlQueryRaw<string>(
            "SELECT relname AS \"Value\" FROM pg_class WHERE relname IN ('flag_alert_configurations', 'idx_flag_alert_configurations_tenant_id', 'idx_smtp_queue_tenant_id_direction_status_next_attempt_at', 'IX_smtp_queue_tenant_id_direction_status_next_attempt_at') ORDER BY relname").ToListAsync();
        Assert.Equal(new[] { "flag_alert_configurations", "idx_flag_alert_configurations_tenant_id", "idx_smtp_queue_tenant_id_direction_status_next_attempt_at" }, converged);
    }

    private static async Task<(Tenant Tenant, Mailbox Mailbox, Folder FirstFolder, Folder SecondFolder)> SeedFolderAsync(AppDbContext db)
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Slug = "uid-" + Guid.NewGuid().ToString("N") };
        var domain = new DomainEntity { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "example.test" };
        var mailbox = new Mailbox { Id = Guid.NewGuid(), TenantId = tenant.Id, DomainId = domain.Id, Address = "uid@example.test" };
        var first = new Folder { Id = Guid.NewGuid(), TenantId = tenant.Id, MailboxId = mailbox.Id, Name = "INBOX", Role = "inbox", UidNext = 1, UidValidity = 7 };
        var second = new Folder { Id = Guid.NewGuid(), TenantId = tenant.Id, MailboxId = mailbox.Id, Name = "Archive", Role = "archive", UidNext = 1, UidValidity = 8 };
        db.AddRange(tenant, domain, mailbox, first, second);
        await db.SaveChangesAsync();
        return (tenant, mailbox, first, second);
    }

    private static Message NewMessage(Guid tenantId, Guid mailboxId, Guid folderId, uint uid, DateTimeOffset date) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId, Uid = uid,
        Sender = "sender@example.test", Recipient = "recipient@example.test", Subject = "UID migration test",
        Date = date, ContentHash = Guid.NewGuid().ToString("N"), StoragePath = "/test/message.eml", CreatedAt = date, UpdatedAt = date
    };
}

/// <summary>
/// Provides a throwaway PostgreSQL server. When MIAUTRIX_TEST_SERVER_CONNECTION is set (a role with
/// CREATEDB on a NON-production server, connected to its maintenance database), each test creates its
/// own uidtest_* database there and every one of them is dropped afterwards; otherwise Testcontainers
/// starts a disposable PostgreSQL 18 container (requires Docker).
/// </summary>
public sealed class UidPostgresFixture : IAsyncLifetime
{
    private const string ServerConnectionVariable = "MIAUTRIX_TEST_SERVER_CONNECTION";
    private readonly string? _externalServer = Environment.GetEnvironmentVariable(ServerConnectionVariable);
    private readonly List<string> _createdDatabases = [];
    private PostgreSqlContainer? _container;

    private string ServerConnection => _externalServer ?? _container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_externalServer))
        {
            var database = new NpgsqlConnectionStringBuilder(_externalServer).Database ?? string.Empty;
            if (database.Contains("pro", StringComparison.OrdinalIgnoreCase) || database.StartsWith("miautrix", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{ServerConnectionVariable} must point at the server maintenance database (e.g. 'postgres'), never an application database.");
            return;
        }

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:18-alpine")
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(ServerConnection);
        await connection.OpenAsync();
        foreach (var database in _createdDatabases)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var database = "uidtest_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(ServerConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{database}\"";
        await command.ExecuteNonQueryAsync();
        _createdDatabases.Add(database);
        var builder = new NpgsqlConnectionStringBuilder(ServerConnection) { Database = database };
        return builder.ConnectionString;
    }
}
