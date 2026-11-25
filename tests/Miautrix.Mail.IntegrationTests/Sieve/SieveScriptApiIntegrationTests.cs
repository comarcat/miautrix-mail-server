using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Sieve;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Miautrix.Mail.IntegrationTests.Sieve;

public sealed class SieveScriptApiIntegrationTests
{
    [Fact]
    public async Task ScriptCrud_EnforcesOwnerAndSharedWriteDelegateAndHidesCrossTenant()
    {
        var maintenance = Environment.GetEnvironmentVariable("MIAUTRIX_TEST_SERVER_CONNECTION")
            ?? throw new InvalidOperationException("MIAUTRIX_TEST_SERVER_CONNECTION is required.");
        var database = "miautrix_test_sieve_api_" + Guid.NewGuid().ToString("N");
        var server = new NpgsqlConnectionStringBuilder(maintenance);
        var maintenanceDatabase = server.Database ?? string.Empty;
        if (!string.Equals(maintenanceDatabase, "postgres", StringComparison.OrdinalIgnoreCase) ||
            maintenanceDatabase.Contains("pro", StringComparison.OrdinalIgnoreCase) ||
            maintenanceDatabase.StartsWith("miautrix", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A dedicated non-production postgres maintenance connection is required.");

        await using (var connection = new NpgsqlConnection(maintenance))
        {
            await connection.OpenAsync();
            await using var create = connection.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{database}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var dbConnection = new NpgsqlConnectionStringBuilder(maintenance) { Database = database };
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(dbConnection.ConnectionString).Options;
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();

            var tenant = new Tenant { Slug = "sieve-api-" + Guid.NewGuid().ToString("N") };
            var otherTenant = new Tenant { Slug = "sieve-api-other-" + Guid.NewGuid().ToString("N") };
            db.Tenants.AddRange(tenant, otherTenant);
            var owner = new User { TenantId = tenant.Id, Email = "owner@example.test", Name = "Owner" };
            var delegateUser = new User { TenantId = tenant.Id, Email = "delegate@example.test", Name = "Delegate" };
            var domain = new Miautrix.Mail.Domain.Domain { TenantId = tenant.Id, Name = "example.test" };
            var foreignDomain = new Miautrix.Mail.Domain.Domain { TenantId = otherTenant.Id, Name = "foreign.test" };
            var personal = new Mailbox { TenantId = tenant.Id, DomainId = domain.Id, Address = owner.Email, Name = "Owner" };
            var shared = new Mailbox { TenantId = tenant.Id, DomainId = domain.Id, Address = "team@example.test", Name = "Team", Kind = "shared" };
            var foreign = new Mailbox { TenantId = otherTenant.Id, DomainId = foreignDomain.Id, Address = "foreign@foreign.test", Name = "Foreign" };
            db.AddRange(owner, delegateUser, domain, foreignDomain, personal, shared, foreign,
                new MailboxDelegate { TenantId = tenant.Id, MailboxId = shared.Id, UserId = delegateUser.Id, AccessLevel = "write" });
            await db.SaveChangesAsync();

            var auth = new TenantAuthorizationHelper(new EfPermissionRepository(db), new InMemorySecurityEventSink());
            var service = new SieveScriptApiService(db, auth, new SieveCompiler());
            var created = await service.CreateAsync(tenant.Id, owner.Id, personal.Id, new SieveScriptSaveRequest("Personal", "keep;"));
            Assert.True(created.Success, created.Diagnostic.ErrorMessage);
            var first = created.Script!;
            var invalid = await service.UpdateAsync(tenant.Id, owner.Id, personal.Id, first.Id, new SieveScriptSaveRequest("Broken", "require [\"unknown\"]; keep;"));
            Assert.NotNull(invalid);
            Assert.False(invalid.Success);
            Assert.NotNull(invalid.Diagnostic.ErrorLine);
            Assert.NotNull(invalid.Diagnostic.ErrorColumn);

            var second = await service.CreateAsync(tenant.Id, owner.Id, personal.Id, new SieveScriptSaveRequest("Second", "discard;"));
            Assert.True(second.Success);
            Assert.True(await service.SetActiveAsync(tenant.Id, owner.Id, personal.Id, first.Id, true));
            Assert.True(await service.SetActiveAsync(tenant.Id, owner.Id, personal.Id, second.Script!.Id, true));
            var listed = await service.ListAsync(tenant.Id, owner.Id, personal.Id);
            Assert.Single(listed, script => script.IsActive);
            Assert.False(listed.Single(script => script.Id == first.Id).IsActive);

            var sharedScript = await service.CreateAsync(tenant.Id, delegateUser.Id, shared.Id, new SieveScriptSaveRequest("Shared", "keep;"));
            Assert.True(sharedScript.Success, sharedScript.Diagnostic.ErrorMessage);
            await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.ListAsync(tenant.Id, owner.Id, foreign.Id));
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
