using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.IntegrationTests.Api;

/// <summary>
/// Orphan mailbox management: listing, assigning (convert to shared), deleting with the
/// server-side address confirmation, and ZIP export.
/// </summary>
[Trait("Category", "Api")]
public sealed class MailboxAdminApiTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private const string DomainName = "orphandomain.test";

    private static readonly string[] AdminPermissions =
    [
        "user.view", "user.manage", "mailbox.create", "mailbox.read", "mailbox.update", "mailbox.delete"
    ];

    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;

    public MailboxAdminApiTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
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

    private IMailStorage Storage => _factory.Services.GetRequiredService<IMailStorage>();

    // ------------------------------------------------------------------
    // Orphan listing
    // ------------------------------------------------------------------

    [Fact]
    public async Task When_user_kind_mailbox_has_no_user_it_is_listed_as_orphan()
    {
        var tenant = await SeedTenantAsync();
        var orphanId = Guid.NewGuid();
        var healthyId = Guid.NewGuid();

        try
        {
            await SeedMailboxAsync(tenant, orphanId, "ghost@" + DomainName, "user", "Ghost Mailbox");
            await SeedMailboxAsync(tenant, healthyId, "alive@" + DomainName, "user", "Alive Mailbox");
            await SeedUserAsync(tenant, "alive@" + DomainName, "Alive");

            var body = await GetJsonAsync(tenant, "/api/v1/mailboxes/orphans");
            var data = body.GetProperty("data");

            var addresses = data.EnumerateArray().Select(x => x.GetProperty("email").GetString()).ToList();
            Assert.Contains("ghost@" + DomainName, addresses);
            Assert.DoesNotContain("alive@" + DomainName, addresses);
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    [Fact]
    public async Task When_shared_mailbox_has_no_active_delegate_it_is_listed_as_orphan()
    {
        var tenant = await SeedTenantAsync();
        var noDelegateId = Guid.NewGuid();
        var delegatedId = Guid.NewGuid();
        var delegateUserId = Guid.NewGuid();
        var delegateMailboxId = Guid.NewGuid();

        try
        {
            await SeedMailboxAsync(tenant, noDelegateId, "orphan-shared@" + DomainName, "shared", "No Delegates");
            await SeedMailboxAsync(tenant, delegatedId, "live-shared@" + DomainName, "shared", "Has Delegate");
            await SeedUserAsync(tenant, "delegate@" + DomainName, "Delegate", delegateUserId);
            await SeedMailboxAsync(tenant, delegateMailboxId, "delegate@" + DomainName, "user", "Delegate Mailbox");
            await SeedDelegateAsync(tenant, delegatedId, delegateUserId);

            var body = await GetJsonAsync(tenant, "/api/v1/mailboxes/orphans");
            var addresses = body.GetProperty("data").EnumerateArray()
                .Select(x => x.GetProperty("email").GetString()).ToList();

            Assert.Contains("orphan-shared@" + DomainName, addresses);
            Assert.DoesNotContain("live-shared@" + DomainName, addresses);
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    [Fact]
    public async Task When_counting_orphan_contents_messages_and_attachments_are_reported()
    {
        var tenant = await SeedTenantAsync();
        var mailboxId = Guid.NewGuid();

        try
        {
            await SeedMailboxAsync(tenant, mailboxId, "counted@" + DomainName, "user", "Counted");
            var folderId = await SeedFolderAsync(tenant, mailboxId, "INBOX");
            var messageId = await SeedMessageAsync(tenant, mailboxId, folderId, "hash-count-message");
            await SeedAttachmentAsync(tenant, mailboxId, folderId, "hash-count-attachment", messageId);

            var body = await GetJsonAsync(tenant, "/api/v1/mailboxes/orphans");
            var row = body.GetProperty("data").EnumerateArray()
                .Single(x => x.GetProperty("id").GetString() == mailboxId.ToString());

            Assert.Equal(1, row.GetProperty("message_count").GetInt32());
            Assert.Equal(1, row.GetProperty("attachment_count").GetInt32());
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    // ------------------------------------------------------------------
    // Assign
    // ------------------------------------------------------------------

    [Fact]
    public async Task When_assigning_orphan_mailbox_it_becomes_shared_with_the_new_address()
    {
        var tenant = await SeedTenantAsync();
        var mailboxId = Guid.NewGuid();
        var delegateUserId = Guid.NewGuid();
        var delegateMailboxId = Guid.NewGuid();

        try
        {
            await SeedMailboxAsync(tenant, mailboxId, "legacy@" + DomainName, "user", "Legacy");
            await SeedUserAsync(tenant, "newdelegate@" + DomainName, "New Delegate", delegateUserId);
            await SeedMailboxAsync(tenant, delegateMailboxId, "newdelegate@" + DomainName, "user", "New Delegate Mailbox");

            var payload = new
            {
                address = "renamed@" + DomainName,
                name = "Renamed Mailbox",
                delegates = new[] { new { user_id = delegateUserId, access_level = "write" } },
                quota_bytes = 2147483648L,
            };

            var response = await SendAsync(tenant, HttpMethod.Post, $"/api/v1/mailboxes/{mailboxId}/assign", payload);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(body);
            var data = json.RootElement.GetProperty("data");
            Assert.Equal("renamed@" + DomainName, data.GetProperty("email").GetString());
            Assert.Equal("Renamed Mailbox", data.GetProperty("name").GetString());
            var delegates = data.GetProperty("delegates");
            Assert.Equal(1, delegates.GetArrayLength());
            Assert.Equal(delegateUserId.ToString(), delegates[0].GetProperty("user_id").GetString());
            Assert.Equal("write", delegates[0].GetProperty("access_level").GetString());

            await using var context = CreateContext();
            var stored = await context.Mailboxes.AsNoTracking().SingleAsync(m => m.Id == mailboxId);
            Assert.Equal("shared", stored.Kind);
            Assert.Equal("renamed@" + DomainName, stored.Address);
            Assert.Equal(2147483648L, stored.QuotaBytes);

            // The old address is free again for a new user.
            await SeedUserAsync(tenant, "legacy@" + DomainName, "Reused Address");
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    [Fact]
    public async Task When_assigning_without_delegates_it_is_rejected_and_nothing_changes()
    {
        var tenant = await SeedTenantAsync();
        var mailboxId = Guid.NewGuid();

        try
        {
            await SeedMailboxAsync(tenant, mailboxId, "solo@" + DomainName, "user", "Solo");

            var payload = new
            {
                address = "solo2@" + DomainName,
                name = "Solo 2",
                delegates = Array.Empty<object>(),
            };

            var response = await SendAsync(tenant, HttpMethod.Post, $"/api/v1/mailboxes/{mailboxId}/assign", payload);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            await using var context = CreateContext();
            var stored = await context.Mailboxes.AsNoTracking().SingleAsync(m => m.Id == mailboxId);
            Assert.Equal("user", stored.Kind);
            Assert.Equal("solo@" + DomainName, stored.Address);
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    // ------------------------------------------------------------------
    // Delete
    // ------------------------------------------------------------------

    [Fact]
    public async Task When_confirm_address_does_not_match_delete_is_rejected_and_nothing_is_removed()
    {
        var tenant = await SeedTenantAsync();
        var mailboxId = Guid.NewGuid();

        try
        {
            await SeedMailboxAsync(tenant, mailboxId, "keep@" + DomainName, "user", "Keep");
            var folderId = await SeedFolderAsync(tenant, mailboxId, "INBOX");
            await SeedMessageAsync(tenant, mailboxId, folderId, "hash-keep");

            var response = await SendAsync(tenant, HttpMethod.Delete, $"/api/v1/mailboxes/{mailboxId}",
                new { confirm_address = "not-the-address@" + DomainName });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            await using var context = CreateContext();
            Assert.True(await context.Mailboxes.AnyAsync(m => m.Id == mailboxId));
            Assert.True(await context.Messages.AnyAsync(m => m.MailboxId == mailboxId));
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    [Fact]
    public async Task When_delete_confirms_the_address_rows_are_removed_and_shared_blobs_survive()
    {
        var tenant = await SeedTenantAsync();
        var doomedId = Guid.NewGuid();
        var keeperId = Guid.NewGuid();

        try
        {
            var sharedHash = await StoreBlobAsync("shared body content");
            var uniqueHash = await StoreBlobAsync("unique attachment content");

            await SeedMailboxAsync(tenant, doomedId, "doomed@" + DomainName, "user", "Doomed");
            var doomedFolder = await SeedFolderAsync(tenant, doomedId, "INBOX");
            var doomedMessage = await SeedMessageAsync(tenant, doomedId, doomedFolder, sharedHash);
            await SeedAttachmentAsync(tenant, doomedId, doomedFolder, uniqueHash, doomedMessage);
            await SeedRecipientAsync(tenant, doomedMessage);
            await SeedFlagAsync(tenant, doomedMessage);
            await SeedSieveScriptAsync(tenant, doomedId);
            var otherUserId = Guid.NewGuid();
            await SeedUserAsync(tenant, "d@" + DomainName, "D", otherUserId);
            await SeedDelegateAsync(tenant, doomedId, otherUserId);

            await SeedMailboxAsync(tenant, keeperId, "keeper@" + DomainName, "user", "Keeper");
            var keeperFolder = await SeedFolderAsync(tenant, keeperId, "INBOX");
            await SeedMessageAsync(tenant, keeperId, keeperFolder, sharedHash);
            await SeedUserAsync(tenant, "keeper@" + DomainName, "Keeper");

            var response = await SendAsync(tenant, HttpMethod.Delete, $"/api/v1/mailboxes/{doomedId}",
                new { confirm_address = "doomed@" + DomainName });
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(body);
            var result = json.RootElement.GetProperty("data");
            Assert.Equal(1, result.GetProperty("messages_deleted").GetInt32());
            Assert.Equal(1, result.GetProperty("attachments_deleted").GetInt32());
            Assert.Equal(1, result.GetProperty("blobs_deleted").GetInt32());

            await using var context = CreateContext();
            Assert.False(await context.Mailboxes.AnyAsync(m => m.Id == doomedId));
            Assert.False(await context.Messages.AnyAsync(m => m.MailboxId == doomedId));
            Assert.False(await context.Folders.AnyAsync(f => f.MailboxId == doomedId));
            Assert.False(await context.Attachments.AnyAsync(a => a.MessageId == doomedMessage));
            Assert.False(await context.MessageRecipients.AnyAsync(r => r.MessageId == doomedMessage));
            Assert.False(await context.MessageFlags.AnyAsync(f => f.MessageId == doomedMessage));
            Assert.False(await context.SieveScripts.AnyAsync(s => s.MailboxId == doomedId));
            Assert.False(await context.MailboxDelegates.AnyAsync(d => d.MailboxId == doomedId));

            // Deduplicated storage: the blob another mailbox still references must survive.
            Assert.True(await Storage.ExistsAsync(sharedHash));
            Assert.False(await Storage.ExistsAsync(uniqueHash));
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    // ------------------------------------------------------------------
    // Export
    // ------------------------------------------------------------------

    [Fact]
    public async Task When_exporting_mailbox_the_zip_contains_eml_attachment_and_manifest()
    {
        var tenant = await SeedTenantAsync();
        var mailboxId = Guid.NewGuid();

        try
        {
            var address = "export@" + DomainName;
            var bodyHash = await StoreBlobAsync("raw message body");
            var attachmentHash = await StoreBlobAsync("attachment bytes");

            await SeedMailboxAsync(tenant, mailboxId, address, "user", "Export");
            var folderId = await SeedFolderAsync(tenant, mailboxId, "INBOX");
            var messageId = await SeedMessageAsync(tenant, mailboxId, folderId, bodyHash, uid: 7,
                subject: "Quarterly report",
                rawHeaders: "From: sender@example.org\r\nTo: export@" + DomainName + "\r\nSubject: Quarterly report",
                bodyText: "Please find the report attached.");
            await SeedAttachmentAsync(tenant, mailboxId, folderId, attachmentHash, messageId, fileName: "report.pdf", contentType: "application/pdf");

            using var request = BuildRequest(tenant, HttpMethod.Get, $"/api/v1/mailboxes/{mailboxId}/export");
            var response = await _factory.CreateClient().SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entries = archive.Entries.Select(e => e.FullName).ToList();

            Assert.Contains(entries, e => e.EndsWith("/manifest.json", StringComparison.Ordinal));
            Assert.Contains(entries, e => e.EndsWith(".eml", StringComparison.Ordinal) && e.Contains("1 - Quarterly report"));
            Assert.Contains(entries, e => e.Contains("attachments/1.1 report.pdf"));

            var emlEntry = archive.Entries.Single(e => e.FullName.EndsWith(".eml", StringComparison.Ordinal));
            using var reader = new StreamReader(emlEntry.Open());
            var eml = await reader.ReadToEndAsync();
            Assert.Contains("Subject: Quarterly report", eml);
            Assert.Contains("Please find the report attached.", eml);
            Assert.Contains("multipart/mixed", eml);
            Assert.Contains("Content-Disposition: attachment; filename=\"report.pdf\"", eml);
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
        }
    }

    // ------------------------------------------------------------------
    // Cross-tenant isolation: 404, never 403
    // ------------------------------------------------------------------

    [Fact]
    public async Task When_mailbox_belongs_to_another_tenant_every_route_returns_404()
    {
        var tenant = await SeedTenantAsync();
        var otherTenant = await SeedTenantAsync();
        var foreignMailboxId = Guid.NewGuid();

        try
        {
            await SeedMailboxAsync(otherTenant, foreignMailboxId, "foreign@" + DomainName, "user", "Foreign");

            var assign = await SendAsync(tenant, HttpMethod.Post, $"/api/v1/mailboxes/{foreignMailboxId}/assign",
                new { address = "taken@" + DomainName, delegates = Array.Empty<object>() });
            Assert.Equal(HttpStatusCode.NotFound, assign.StatusCode);

            var delete = await SendAsync(tenant, HttpMethod.Delete, $"/api/v1/mailboxes/{foreignMailboxId}",
                new { confirm_address = "foreign@" + DomainName });
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

            using var exportRequest = BuildRequest(tenant, HttpMethod.Get, $"/api/v1/mailboxes/{foreignMailboxId}/export");
            var export = await _factory.CreateClient().SendAsync(exportRequest);
            Assert.Equal(HttpStatusCode.NotFound, export.StatusCode);

            // The foreign mailbox is untouched.
            await using var context = CreateContext();
            Assert.True(await context.Mailboxes.AnyAsync(m => m.Id == foreignMailboxId && m.Address == "foreign@" + DomainName));
        }
        finally
        {
            await CleanupTenantAsync(tenant.TenantId);
            await CleanupTenantAsync(otherTenant.TenantId);
        }
    }

    // ------------------------------------------------------------------
    // HTTP helpers
    // ------------------------------------------------------------------

    private static HttpRequestMessage BuildRequest(TenantScope tenant, HttpMethod method, string path, object? payload = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Tenant-Id", tenant.TenantId.ToString());
        request.Headers.Add("X-User-Id", tenant.AdminUserId.ToString());

        if (payload is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(TenantScope tenant, HttpMethod method, string path, object payload)
    {
        using var request = BuildRequest(tenant, method, path, payload);
        return await _factory.CreateClient().SendAsync(request);
    }

    private async Task<JsonElement> GetJsonAsync(TenantScope tenant, string path)
    {
        using var request = BuildRequest(tenant, HttpMethod.Get, path);
        var response = await _factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<string> StoreBlobAsync(string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = await Storage.StoreAsync(stream);
        return result.ContentHash;
    }

    // ------------------------------------------------------------------
    // Seeding
    // ------------------------------------------------------------------

    private sealed record TenantScope(Guid TenantId, Guid AdminUserId, Guid DomainId);

    private static async Task<TenantScope> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();

        context.Tenants.Add(new Tenant { Id = tenantId, Slug = $"orphan-{Guid.NewGuid():N}", CreatedAt = now, UpdatedAt = now });

        context.Users.Add(new User
        {
            Id = adminUserId, TenantId = tenantId, Email = $"admin@{Guid.NewGuid():N}.{DomainName}",
            Name = "Tenant Admin", IsActive = true, CreatedAt = now, UpdatedAt = now
        });

        context.Domains.Add(new DomainEntity
        {
            Id = domainId, TenantId = tenantId, Name = DomainName, IsVerified = true, CreatedAt = now, UpdatedAt = now
        });

        context.Roles.Add(new Role { Id = roleId, TenantId = tenantId, Code = "tenant_admin", Name = "Tenant Admin", CreatedAt = now, UpdatedAt = now });

        foreach (var code in AdminPermissions)
        {
            var permissionId = Guid.NewGuid();
            context.Permissions.Add(new Permission { Id = permissionId, TenantId = tenantId, Code = code, Name = code, CreatedAt = now, UpdatedAt = now });
            context.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(), TenantId = tenantId, RoleId = roleId, PermissionId = permissionId, CreatedAt = now, UpdatedAt = now
            });
        }

        context.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = adminUserId, RoleId = roleId, CreatedAt = now, UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return new TenantScope(tenantId, adminUserId, domainId);
    }

    private static async Task<Guid> SeedUserAsync(TenantScope tenant, string email, string name, Guid? userId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var id = userId ?? Guid.NewGuid();

        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Id = id, TenantId = tenant.TenantId, Email = email, Name = name, IsActive = true, CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedMailboxAsync(TenantScope tenant, Guid mailboxId, string address, string kind, string name)
    {
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext();
        context.Mailboxes.Add(new Mailbox
        {
            Id = mailboxId, TenantId = tenant.TenantId, DomainId = tenant.DomainId,
            Address = address, Name = name, Kind = kind, QuotaBytes = 10737418240, UsedBytes = 0,
            IsActive = true, CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
        return mailboxId;
    }

    private static async Task<Guid> SeedFolderAsync(TenantScope tenant, Guid mailboxId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();

        await using var context = CreateContext();
        context.Folders.Add(new Folder
        {
            Id = id, TenantId = tenant.TenantId, MailboxId = mailboxId, Name = name, Role = "inbox",
            UidNext = 2, UidValidity = 1, CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedMessageAsync(
        TenantScope tenant,
        Guid mailboxId,
        Guid folderId,
        string contentHash,
        uint uid = 1,
        string subject = "Test message",
        string rawHeaders = "From: sender@example.org\r\nTo: someone@example.org\r\nSubject: Test message",
        string bodyText = "Test body")
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();

        await using var context = CreateContext();
        context.Messages.Add(new Message
        {
            Id = id, TenantId = tenant.TenantId, MailboxId = mailboxId, FolderId = folderId,
            Sender = "sender@example.org", Recipient = "someone@example.org", Subject = subject,
            Date = now, ContentHash = contentHash, StoragePath = "n/a", SizeBytes = 128,
            Flags = string.Empty, Uid = uid, IsRead = false, BodyText = bodyText, BodyHtml = null,
            RawHeaders = rawHeaders, CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedAttachmentAsync(
        TenantScope tenant,
        Guid mailboxId,
        Guid folderId,
        string contentHash,
        Guid? messageId = null,
        string fileName = "attachment.bin",
        string contentType = "application/octet-stream")
    {
        var target = messageId ?? await SeedMessageAsync(tenant, mailboxId, folderId, "hash-placeholder-" + Guid.NewGuid().ToString("N"));
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();

        await using var context = CreateContext();
        context.Attachments.Add(new Attachment
        {
            Id = id, TenantId = tenant.TenantId, MessageId = target, FileName = fileName,
            ContentType = contentType, SizeBytes = 64, ContentHash = contentHash, StoragePath = "n/a",
            CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static async Task SeedRecipientAsync(TenantScope tenant, Guid messageId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = CreateContext();
        context.MessageRecipients.Add(new MessageRecipient
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId, MessageId = messageId, Type = "to",
            Address = "someone@example.org", CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedFlagAsync(TenantScope tenant, Guid messageId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = CreateContext();
        context.MessageFlags.Add(new MessageFlag
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId, MessageId = messageId, Flag = "\\Seen",
            CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedSieveScriptAsync(TenantScope tenant, Guid mailboxId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = CreateContext();
        context.SieveScripts.Add(new SieveScript
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId, MailboxId = mailboxId, Name = "main",
            Content = "keep;", IsActive = true, CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedDelegateAsync(TenantScope tenant, Guid mailboxId, Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = CreateContext();
        context.MailboxDelegates.Add(new MailboxDelegate
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId, MailboxId = mailboxId, UserId = userId,
            AccessLevel = "read", CreatedAt = now, UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private static async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var context = CreateContext();

        await context.SmtpQueue.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.MailboxDelegates.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await context.SieveScripts.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();

        var messageIds = await context.Messages.Where(m => m.TenantId == tenantId).Select(m => m.Id).ToListAsync();
        if (messageIds.Count > 0)
        {
            await context.Attachments.Where(x => x.TenantId == tenantId && messageIds.Contains(x.MessageId)).ExecuteDeleteAsync();
            await context.MessageRecipients.Where(x => x.TenantId == tenantId && messageIds.Contains(x.MessageId)).ExecuteDeleteAsync();
            await context.MessageFlags.Where(x => x.TenantId == tenantId && messageIds.Contains(x.MessageId)).ExecuteDeleteAsync();
        }

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
