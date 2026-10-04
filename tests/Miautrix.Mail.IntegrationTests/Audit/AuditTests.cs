using System.Text.Json;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Audit;

[Trait("Category", "Audit")]
public class AuditTests
{
    private static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            conn = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";
        }
        return conn;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task When_an_audited_change_is_rolled_back_the_system_shall_not_persist_its_audit_row()
    {
        // ARRANGE
        await using var context = CreateContext();

        var tenantId = Guid.NewGuid();
        var testTenant = new Tenant
        {
            Id = tenantId,
            Slug = $"test-audit-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Tenants.Add(testTenant);
        await context.SaveChangesAsync();

        var mailboxId = Guid.NewGuid();
        var auditLogId = Guid.NewGuid();

        // ACT: Run transaction that adds mailbox + audit log, then rolls back
        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            var mailbox = new Mailbox
            {
                Id = mailboxId,
                TenantId = tenantId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.Mailboxes.Add(mailbox);

            var auditLog = new AuditLog
            {
                Id = auditLogId,
                TenantId = tenantId,
                Action = "mailbox.create",
                ActorId = Guid.NewGuid(),
                TargetType = "Mailbox",
                TargetId = mailboxId,
                DetailsJson = JsonSerializer.Serialize(new { Reason = "New user provisioning" }),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.AuditLogs.Add(auditLog);

            await context.SaveChangesAsync();

            // Explicitly rollback the transaction
            await tx.RollbackAsync();
        }
        catch
        {
            await tx.RollbackAsync();
        }

        // ASSERT: Ensure neither mailbox nor audit log row was persisted
        await using var verifyContext = CreateContext();
        var persistedMailbox = await verifyContext.Mailboxes.FirstOrDefaultAsync(m => m.Id == mailboxId);
        var persistedAudit = await verifyContext.AuditLogs.FirstOrDefaultAsync(a => a.Id == auditLogId);

        Assert.Null(persistedMailbox);
        Assert.Null(persistedAudit);

        // Clean up tenant
        var tenantToRemove = await verifyContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenantToRemove != null)
        {
            verifyContext.Tenants.Remove(tenantToRemove);
            await verifyContext.SaveChangesAsync();
        }
    }
}
