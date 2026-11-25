using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Miautrix.Mail.IntegrationTests.Sieve;

public sealed class SieveMigrationTests
{
    [Fact]
    public async Task Migration_CreatesVacationDedupeTableAndActiveScriptUniqueness()
    {
        var maintenance = Environment.GetEnvironmentVariable("MIAUTRIX_TEST_SERVER_CONNECTION")
            ?? throw new InvalidOperationException("MIAUTRIX_TEST_SERVER_CONNECTION is required for throwaway migration tests.");
        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(maintenance);
        var maintenanceDatabase = maintenanceBuilder.Database ?? string.Empty;
        if (!maintenanceDatabase.Equals("postgres", StringComparison.OrdinalIgnoreCase) ||
            maintenanceDatabase.Contains("pro", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MIAUTRIX_TEST_SERVER_CONNECTION must target the non-production postgres maintenance database.");

        var database = "miautrix_test_sieve_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(maintenance))
        {
            await connection.OpenAsync();
            await using var create = connection.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{database}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var databaseBuilder = new NpgsqlConnectionStringBuilder(maintenance) { Database = database };
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(databaseBuilder.ConnectionString).Options;
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();

            var tenant = new Tenant { Slug = "sieve-migration-" + Guid.NewGuid().ToString("N") };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            var mailboxId = Guid.NewGuid();
            db.SieveScripts.Add(new SieveScript { TenantId = tenant.Id, MailboxId = mailboxId, Name = "one", Content = "keep;", IsActive = true });
            await db.SaveChangesAsync();
            db.SieveScripts.Add(new SieveScript { TenantId = tenant.Id, MailboxId = mailboxId, Name = "two", Content = "discard;", IsActive = true });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(maintenance);
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
