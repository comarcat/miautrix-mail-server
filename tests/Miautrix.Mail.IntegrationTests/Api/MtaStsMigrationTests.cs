using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests.Api;

[Trait("Category", "MtaSts")]
public sealed class MtaStsMigrationTests
{
    [Fact]
    public async Task Migration_is_idempotent_and_defaults_new_domains()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")).Options);
        await db.Database.MigrateAsync();
        const string idempotentDdl = """
            ALTER TABLE domains ADD COLUMN IF NOT EXISTS mta_sts_mode text NOT NULL DEFAULT 'none';
            ALTER TABLE domains ADD COLUMN IF NOT EXISTS mta_sts_max_age integer NOT NULL DEFAULT 604800;
            ALTER TABLE domains ADD COLUMN IF NOT EXISTS tls_report_rua text NULL;
            """;
        await db.Database.ExecuteSqlRawAsync(idempotentDdl);
        await db.Database.ExecuteSqlRawAsync(idempotentDdl);
        var migrationCountBefore = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261125151035_V22_Autodiscovery_MtaSts'").SingleAsync();
        await db.Database.MigrateAsync();
        var migrationCountAfter = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261125151035_V22_Autodiscovery_MtaSts'").SingleAsync();
        Assert.Equal(1, migrationCountBefore);
        Assert.Equal(migrationCountBefore, migrationCountAfter);

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Slug = $"mta-migration-{Guid.NewGuid():N}" });
        var domain = new Miautrix.Mail.Domain.Domain { TenantId = tenantId, Name = $"mta-default-{Guid.NewGuid():N}.test" };
        db.Domains.Add(domain);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.Domains.AsNoTracking().SingleAsync(d => d.Id == domain.Id);
        Assert.Equal("none", stored.MtaStsMode);
        Assert.Equal(604800, stored.MtaStsMaxAge);
    }
}
