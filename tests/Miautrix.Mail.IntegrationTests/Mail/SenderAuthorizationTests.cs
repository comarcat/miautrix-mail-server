using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Miautrix.Mail.IntegrationTests.Mail;

public sealed class SenderAuthorizationTests : IClassFixture<UidPostgresFixture>
{
    private readonly UidPostgresFixture _fixture;
    public SenderAuthorizationTests(UidPostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Sender_permission_columns_migration_is_idempotent()
    {
        var cs = await _fixture.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        const string repeatable = """
            ALTER TABLE mailbox_delegates ADD COLUMN IF NOT EXISTS can_send_as boolean NOT NULL DEFAULT false;
            UPDATE mailbox_delegates SET can_send_as = false WHERE can_send_as IS NULL;
            ALTER TABLE mailbox_delegates ALTER COLUMN can_send_as SET DEFAULT false;
            ALTER TABLE mailbox_delegates ALTER COLUMN can_send_as SET NOT NULL;
            ALTER TABLE mailbox_delegates ADD COLUMN IF NOT EXISTS can_send_on_behalf boolean NOT NULL DEFAULT false;
            UPDATE mailbox_delegates SET can_send_on_behalf = false WHERE can_send_on_behalf IS NULL;
            ALTER TABLE mailbox_delegates ALTER COLUMN can_send_on_behalf SET DEFAULT false;
            ALTER TABLE mailbox_delegates ALTER COLUMN can_send_on_behalf SET NOT NULL;
            """;
        await db.Database.ExecuteSqlRawAsync(repeatable);
        await db.Database.ExecuteSqlRawAsync(repeatable);
        var schema = await db.Database.SqlQueryRaw<string>("SELECT column_name || ':' || is_nullable || ':' || column_default AS \"Value\" FROM information_schema.columns WHERE table_name = 'mailbox_delegates' AND column_name IN ('can_send_as', 'can_send_on_behalf') ORDER BY column_name").ToListAsync();
        Assert.Equal(new[] { "can_send_as:NO:false", "can_send_on_behalf:NO:false" }, schema);
    }

    [Fact]
    public async Task Sender_permission_migration_is_repeatable_on_dev_database()
    {
        var connection = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION") ?? throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION required.");
        var databaseName = new NpgsqlConnectionStringBuilder(connection).Database ?? string.Empty;
        if (!string.Equals(databaseName, "miautrix-mail-dev", StringComparison.Ordinal) &&
            !databaseName.StartsWith("miautrix_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Idempotency check is allowed only against miautrix-mail-dev or an isolated miautrix_test_ database.");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        const string repeatable = """
            ALTER TABLE mailbox_delegates ADD COLUMN IF NOT EXISTS can_send_as boolean NOT NULL DEFAULT false;
            UPDATE mailbox_delegates SET can_send_as = false WHERE can_send_as IS NULL;
            ALTER TABLE mailbox_delegates ALTER COLUMN can_send_as SET DEFAULT false;
            ALTER TABLE mailbox_delegates ALTER COLUMN can_send_as SET NOT NULL;
            ALTER TABLE mailbox_delegates ADD COLUMN IF NOT EXISTS can_send_on_behalf boolean NOT NULL DEFAULT false;
            UPDATE mailbox_delegates SET can_send_on_behalf = false WHERE can_send_on_behalf IS NULL;
            ALTER TABLE mailbox_delegates ALTER COLUMN can_send_on_behalf SET DEFAULT false;
            ALTER TABLE mailbox_delegates ALTER COLUMN can_send_on_behalf SET NOT NULL;
            """;
        await db.Database.ExecuteSqlRawAsync(repeatable);
        await db.Database.ExecuteSqlRawAsync(repeatable);
    }

    [Fact]
    public async Task Sender_policy_enforces_own_alias_send_as_behalf_and_rejects_spoofing()
    {
        var cs = await _fixture.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var tenant = new Tenant { Id = Guid.NewGuid(), Slug = "sender-" + Guid.NewGuid().ToString("N") };
        var otherTenant = new Tenant { Id = Guid.NewGuid(), Slug = "other-" + Guid.NewGuid().ToString("N") };
        var domain = new Miautrix.Mail.Domain.Domain { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "sender.test" };
        var foreignDomain = new Miautrix.Mail.Domain.Domain { Id = Guid.NewGuid(), TenantId = otherTenant.Id, Name = "other.test" };
        var userId = Guid.NewGuid();
        db.Tenants.AddRange(tenant, otherTenant);
        db.Domains.AddRange(domain, foreignDomain);
        db.Users.Add(new User { Id = userId, TenantId = tenant.Id, Email = "delegate@sender.test", Name = "Delegate" });
        var userMailbox = new Mailbox { Id = Guid.NewGuid(), TenantId = tenant.Id, DomainId = domain.Id, Address = "delegate@sender.test", Kind = "user", Name = "Delegate" };
        var shared = new Mailbox { Id = Guid.NewGuid(), TenantId = tenant.Id, DomainId = domain.Id, Address = "sales@sender.test", Kind = "shared", Name = "Sales" };
        var foreignShared = new Mailbox { Id = Guid.NewGuid(), TenantId = otherTenant.Id, DomainId = foreignDomain.Id, Address = "foreign@other.test", Kind = "shared", Name = "Foreign" };
        db.Mailboxes.AddRange(userMailbox, shared, foreignShared);
        db.Aliases.Add(new Alias { Id = Guid.NewGuid(), TenantId = tenant.Id, Address = "d@sender.test", TargetAddress = userMailbox.Address });
        var grant = new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenant.Id, MailboxId = shared.Id, UserId = userId, AccessLevel = "read", CanSendAs = true };
        db.MailboxDelegates.Add(grant);
        await db.SaveChangesAsync();
        var policy = new SenderAuthorizationService(db);
        static string Message(string from, string? sender = null) => $"From: {from}\r\n" + (sender is null ? "" : $"Sender: {sender}\r\n") + "Subject: authorization test\r\n\r\nbody";

        Assert.True((await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", Message("delegate@sender.test"))).IsAuthorized);
        Assert.True((await policy.AuthorizeMessageAsync(tenant.Id, userId, "d@sender.test", Message("d@sender.test"))).IsAuthorized);
        var sendAs = await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", Message("sales@sender.test"));
        Assert.True(sendAs.IsAuthorized);
        Assert.DoesNotContain("Sender:", sendAs.RawMessage, StringComparison.OrdinalIgnoreCase);
        const string folded = "From: delegate@sender.test\r\nReferences: <one@example.test>\r\n\t<two@example.test>\r\nSubject: x\r\n\r\nbody";
        var unchanged = await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", folded);
        Assert.Equal(folded, unchanged.RawMessage);
        var webmailSendAs = await policy.AuthorizeWebmailAsync(tenant.Id, userId, "sales@sender.test", Message("sales@sender.test"));
        Assert.True(webmailSendAs.IsAuthorized);
        Assert.DoesNotContain("Sender:", webmailSendAs.RawMessage, StringComparison.OrdinalIgnoreCase);

        grant.CanSendAs = false;
        grant.CanSendOnBehalf = true;
        await db.SaveChangesAsync();
        var behalf = await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", Message("sales@sender.test", "forged@sender.test"));
        Assert.True(behalf.IsAuthorized);
        Assert.Contains("Sender: delegate@sender.test", behalf.RawMessage, StringComparison.OrdinalIgnoreCase);
        var webmailBehalf = await policy.AuthorizeWebmailAsync(tenant.Id, userId, "sales@sender.test", Message("sales@sender.test"));
        Assert.True(webmailBehalf.IsAuthorized);
        Assert.Equal("delegate@sender.test", webmailBehalf.EnvelopeSender);
        const string injected = "From: sales@sender.test\r\nSender: forged@sender.test\r\n\t(extra)\r\nSubject: x\r\n\r\nbody";
        var replaced = await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", injected);
        Assert.Equal("From: sales@sender.test\r\nSender: delegate@sender.test\r\nSubject: x\r\n\r\nbody", replaced.RawMessage);
        grant.CanSendAs = true;
        await db.SaveChangesAsync();
        var both = await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", Message("sales@sender.test"));
        Assert.True(both.IsAuthorized);
        Assert.DoesNotContain("Sender:", both.RawMessage, StringComparison.OrdinalIgnoreCase);
        grant.CanSendAs = false;
        await db.SaveChangesAsync();

        grant.CanSendOnBehalf = false;
        grant.AccessLevel = "write";
        await db.SaveChangesAsync();
        Assert.False((await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", Message("sales@sender.test"))).IsAuthorized);
        db.MailboxDelegates.Remove(grant);
        await db.SaveChangesAsync();
        Assert.False((await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", Message("sales@sender.test"))).IsAuthorized);
        Assert.False((await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", Message("foreign@other.test"))).IsAuthorized);
        Assert.False((await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", Message("outside@external.test"))).IsAuthorized);
        Assert.False(await policy.IsEnvelopeSenderAllowedAsync(tenant.Id, userId, "<>"));
        Assert.False((await policy.AuthorizeMessageAsync(tenant.Id, userId, "delegate@sender.test", Message("another@sender.test"))).IsAuthorized);
    }
}
