using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Miautrix.Mail.IntegrationTests.Api;

[Trait("Category", "Api")]
public sealed class MailApiTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;

    public MailApiTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        _factory = factory;
    }

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
    public async Task When_listing_mailboxes_returns_tenant_mailboxes()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var domainId = Guid.NewGuid();

        await SeedTenantAndMailboxAsync(tenantId, userId, mailboxId, domainId, "user@testdomain.com");

        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/mailboxes");
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Headers.Add("X-User-Id", userId.ToString());

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(body);
            var data = json.RootElement.GetProperty("data");
            Assert.True(data.GetArrayLength() > 0);
            Assert.Equal("user@testdomain.com", data[0].GetProperty("address").GetString());
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task When_get_mailbox_folders_returns_folders_and_provisions_defaults()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var domainId = Guid.NewGuid();

        await SeedTenantAndMailboxAsync(tenantId, userId, mailboxId, domainId, "inbox-test@testdomain.com");

        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/mailboxes/{mailboxId}/folders");
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Headers.Add("X-User-Id", userId.ToString());

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(body);
            var data = json.RootElement.GetProperty("data");
            Assert.True(data.GetArrayLength() >= 5); // inbox, sent, drafts, trash, junk, archive
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task When_sending_message_stores_message_and_enqueues_outbound()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var domainId = Guid.NewGuid();

        await SeedTenantAndMailboxAsync(tenantId, userId, mailboxId, domainId, "sender@testdomain.com");

        try
        {
            using var client = _factory.CreateClient();
            var sendPayload = new
            {
                from = "sender@testdomain.com",
                to = new[] { "comarcat@gmail.com" },
                subject = "Test Live Email Message",
                body_text = "Hello from Miautrix Mail Server automated test!"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/mailboxes/{mailboxId}/messages/send")
            {
                Content = new StringContent(JsonSerializer.Serialize(sendPayload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            request.Headers.Add("X-Tenant-Id", tenantId.ToString());
            request.Headers.Add("X-User-Id", userId.ToString());

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Send failed: {response.StatusCode} - {body}");
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(body);
            var data = json.RootElement.GetProperty("data");
            Assert.True(data.GetProperty("success").GetBoolean());
            Assert.True(data.TryGetProperty("message_id", out var msgId));
            Assert.NotNull(msgId.GetString());

            // Check messages listing
            using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/mailboxes/{mailboxId}/messages?folder_role=sent");
            listRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
            listRequest.Headers.Add("X-User-Id", userId.ToString());

            var listResponse = await client.SendAsync(listRequest);
            var listBody = await listResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            using var listJson = JsonDocument.Parse(listBody);
            var items = listJson.RootElement.GetProperty("data");
            Assert.True(items.GetArrayLength() > 0);
            Assert.Equal("Test Live Email Message", items[0].GetProperty("subject").GetString());
        }
        finally
        {
            await CleanupTenantAsync(tenantId);
        }
    }

    private static async Task SeedTenantAndMailboxAsync(
        Guid tenantId,
        Guid userId,
        Guid mailboxId,
        Guid domainId,
        string email)
    {
        await using var context = CreateContext();

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Slug = $"tenant-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = email,
            Name = "Test Mail User",
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
            UsedBytes = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // Add mailbox.read permission
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        context.Roles.Add(new Role
        {
            Id = roleId,
            TenantId = tenantId,
            Code = "mail_user",
            Name = "Mail User",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        context.Permissions.Add(new Permission
        {
            Id = permissionId,
            TenantId = tenantId,
            Code = "mailbox.read",
            Name = "View Mailboxes",
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

        await context.SmtpQueue.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Attachments.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.MessageRecipients.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Messages.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Folders.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
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
