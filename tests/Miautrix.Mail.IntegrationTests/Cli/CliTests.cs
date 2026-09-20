using System;
using System.Threading.Tasks;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Cli;

[Trait("Category", "Cli")]
public sealed class CliTests
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
    public async Task When_authorized_operator_lists_mailboxes_cli_exits_zero()
    {
        var tenantId = Guid.NewGuid();
        var authorizedUserId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var tenantSlug = $"tenant-cli-{Guid.NewGuid():N}";

        await SeedTenantAndUserAsync(tenantId, tenantSlug, authorizedUserId, mailboxId, domainId, "operator@clitest.org", "mailbox.read");

        try
        {
            var exitCode = await Miautrix.Mail.Cli.Program.Main(new[]
            {
                "mailbox", "list",
                "--tenant", tenantSlug,
                "--user", authorizedUserId.ToString()
            });

            Assert.Equal(0, exitCode);
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task When_unauthorized_user_lists_mailboxes_cli_exits_nonzero()
    {
        var tenantId = Guid.NewGuid();
        var unauthorizedUserId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var tenantSlug = $"tenant-cli-{Guid.NewGuid():N}";

        // Seed user without "mailbox.read" permission (e.g. only audit.read)
        await SeedTenantAndUserAsync(tenantId, tenantSlug, unauthorizedUserId, mailboxId, domainId, "unauth@clitest.org", "audit.read");

        try
        {
            var exitCode = await Miautrix.Mail.Cli.Program.Main(new[]
            {
                "mailbox", "list",
                "--tenant", tenantSlug,
                "--user", unauthorizedUserId.ToString()
            });

            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task When_system_info_command_executes_cli_exits_zero()
    {
        var exitCode = await Miautrix.Mail.Cli.Program.Main(new[] { "system", "info" });
        Assert.Equal(0, exitCode);
    }

    private static async Task SeedTenantAndUserAsync(
        Guid tenantId,
        string tenantSlug,
        Guid userId,
        Guid mailboxId,
        Guid domainId,
        string email,
        string permissionCode)
    {
        await using var context = CreateContext();

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Slug = tenantSlug,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = email,
            Name = "CLI Test User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        context.Domains.Add(new Domain.Domain
        {
            Id = domainId,
            TenantId = tenantId,
            Name = email.Split('@')[1],
            IsVerified = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        context.Mailboxes.Add(new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            DomainId = domainId,
            Address = email,
            QuotaBytes = 10737418240,
            UsedBytes = 1048576,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        context.Roles.Add(new Role
        {
            Id = roleId,
            TenantId = tenantId,
            Code = "cli_role",
            Name = "CLI Role",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        context.Permissions.Add(new Permission
        {
            Id = permissionId,
            TenantId = tenantId,
            Code = permissionCode,
            Name = permissionCode,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        context.RolePermissions.Add(new RolePermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        context.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var context = CreateContext();

        await context.Mailboxes.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Domains.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Memberships.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.RolePermissions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Permissions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Roles.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Users.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Tenants.Where(x => x.Id == tenantId).ExecuteDeleteAsync();
    }
}
