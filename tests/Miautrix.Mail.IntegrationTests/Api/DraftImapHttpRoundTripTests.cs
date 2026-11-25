using System.Net;
using System.Text;
using System.Text.Json;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Miautrix.Mail.IntegrationTests.Api;

public sealed class DraftImapHttpRoundTripTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DraftImapHttpRoundTripTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory) => _factory = factory;

    [Fact]
    public async Task Shared_and_personal_drafts_roundtrip_through_imap_http_and_notify_selected_sessions()
    {
        var connection = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION is required.");
        if (connection.Contains("-pro", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to run against a production database.");

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var personalMailboxId = Guid.NewGuid();
        var sharedMailboxId = Guid.NewGuid();
        var personalDraftsId = Guid.NewGuid();
        var sharedDraftsId = Guid.NewGuid();
        var domainName = $"draft-{Guid.NewGuid():N}.test";
        var personalAddress = $"owner@{domainName}";
        var sharedAddress = $"sales@{domainName}";
        var now = DateTimeOffset.UtcNow;

        await using (var db = CreateContext(connection))
        {
            db.AddRange(
                new Tenant { Id = tenantId, Slug = "draft-roundtrip-" + Guid.NewGuid().ToString("N") },
                new Miautrix.Mail.Domain.Domain { Id = domainId, TenantId = tenantId, Name = domainName },
                new User { Id = userId, TenantId = tenantId, Email = personalAddress, Name = "Draft Owner" },
                new Mailbox { Id = personalMailboxId, TenantId = tenantId, DomainId = domainId, Address = personalAddress, Kind = "user", IsActive = true },
                new Mailbox { Id = sharedMailboxId, TenantId = tenantId, DomainId = domainId, Address = sharedAddress, Kind = "shared", IsActive = true },
                new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, MailboxId = sharedMailboxId, AccessLevel = "write" },
                new Folder { Id = personalDraftsId, TenantId = tenantId, MailboxId = personalMailboxId, Name = "Drafts", Role = "drafts", UidNext = 1, UidValidity = 401, CreatedAt = now, UpdatedAt = now },
                new Folder { Id = sharedDraftsId, TenantId = tenantId, MailboxId = sharedMailboxId, Name = "Drafts", Role = "drafts", UidNext = 1, UidValidity = 402, CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
        }

        try
        {
            using var http = _factory.CreateClient();
            await RoundTripAsync(personalMailboxId, personalAddress, "Drafts", expectSharedNamespace: false);
            await RoundTripAsync(sharedMailboxId, sharedAddress, $"Shared/{sharedAddress}/Drafts", expectSharedNamespace: true);

            async Task RoundTripAsync(Guid mailboxId, string address, string imapFolder, bool expectSharedNamespace)
            {
                await using var appendScope = _factory.Services.CreateAsyncScope();
                var appendSession = CreateImapSession(appendScope.ServiceProvider, tenantId, userId, personalMailboxId);
                Assert.Equal("OK", (await appendSession.ExecuteCommandAsync($"T1 LOGIN \"{personalAddress}\" secret")).Status);
                var literalText = $"From: {personalAddress}\r\nTo: {address}\r\nCc: colleague@example.test\r\nSubject: Initial over IMAP\r\n\r\ninitial IMAP draft body";
                var literal = Encoding.UTF8.GetBytes(literalText);
                using var raw = new MemoryStream(literal, writable: false);
                var append = await appendSession.ExecuteCommandAsync($"T2 APPEND \"{imapFolder}\" (\\Draft) {{{literal.Length}}}", raw);
                Assert.Equal("OK", append.Status);

                Guid messageId;
                uint oldUid;
                await using (var inspect = CreateContext(connection))
                {
                    var folderId = expectSharedNamespace ? sharedDraftsId : personalDraftsId;
                    var appended = await inspect.Messages.SingleAsync(m => m.TenantId == tenantId && m.MailboxId == mailboxId && m.FolderId == folderId);
                    messageId = appended.Id;
                    oldUid = appended.Uid;
                }

                // A separate IMAP session snapshots the original UID before the HTTP edit.
                await using var monitorScope = _factory.Services.CreateAsyncScope();
                var monitor = CreateImapSession(monitorScope.ServiceProvider, tenantId, userId, personalMailboxId);
                Assert.Equal("OK", (await monitor.ExecuteCommandAsync($"M1 LOGIN \"{personalAddress}\" secret")).Status);
                Assert.Equal("OK", (await monitor.ExecuteCommandAsync($"M2 SELECT \"{imapFolder}\"")).Status);

                using (var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/mailboxes/{mailboxId}/messages/{messageId}"))
                {
                    request.Headers.Add("X-Tenant-Id", tenantId.ToString());
                    request.Headers.Add("X-User-Id", userId.ToString());
                    using var response = await http.SendAsync(request);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var detail = document.RootElement.GetProperty("data");
                    Assert.Equal("Initial over IMAP", detail.GetProperty("subject").GetString());
                    Assert.Contains(address, detail.GetProperty("recipient").GetString());
                    Assert.Contains("initial IMAP draft body", detail.GetProperty("body_text").GetString());
                    Assert.Contains("colleague@example.test", detail.GetProperty("cc").ToString());
                }

                var updatePayload = JsonSerializer.Serialize(new
                {
                    from = personalAddress,
                    to = new[] { address },
                    cc = new[] { "colleague@example.test" },
                    bcc = Array.Empty<string>(),
                    subject = "Edited through webmail API",
                    body_text = "edited via HTTP and fetched by IMAP",
                    body_html = "<p>edited via HTTP and fetched by IMAP</p>",
                }, Json);
                using (var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/mailboxes/{mailboxId}/drafts/{messageId}"))
                {
                    request.Headers.Add("X-Tenant-Id", tenantId.ToString());
                    request.Headers.Add("X-User-Id", userId.ToString());
                    request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
                    request.Content = new StringContent(updatePayload, Encoding.UTF8, "application/json");
                    using var response = await http.SendAsync(request);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }

                var noop = await monitor.ExecuteCommandAsync("M3 NOOP");
                var noopText = string.Concat(noop.ResponseSegments.Select(segment => Encoding.UTF8.GetString(segment.Bytes)));
                Assert.Equal("OK", noop.Status);
                Assert.Contains("* 1 EXPUNGE", noopText);
                Assert.Contains("* 1 EXISTS", noopText);

                uint updatedUid;
                await using (var inspect = CreateContext(connection))
                {
                    var drafts = await inspect.Messages.Where(m => m.TenantId == tenantId && m.MailboxId == mailboxId).ToListAsync();
                    Assert.Single(drafts);
                    Assert.Equal(messageId, drafts[0].Id);
                    updatedUid = drafts[0].Uid;
                    Assert.NotEqual(oldUid, updatedUid);
                }

                var oldUidFetch = await monitor.ExecuteCommandAsync($"M4 UID FETCH {oldUid} (BODY.PEEK[])");
                Assert.DoesNotContain(oldUidFetch.ResponseSegments, segment => Encoding.UTF8.GetString(segment.Bytes).Contains("edited via HTTP", StringComparison.Ordinal));
                var fetch = await monitor.ExecuteCommandAsync($"M5 UID FETCH {updatedUid} (RFC822.SIZE BODY.PEEK[])");
                Assert.Equal("OK", fetch.Status);
                Assert.Contains(fetch.ResponseSegments, segment => Encoding.UTF8.GetString(segment.Bytes).Contains("Subject: Edited through webmail API", StringComparison.Ordinal));
                Assert.Contains(fetch.ResponseSegments, segment => Encoding.UTF8.GetString(segment.Bytes).Contains("edited via HTTP and fetched by IMAP", StringComparison.Ordinal));

                using (var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/mailboxes/{mailboxId}/messages/{messageId}"))
                {
                    request.Headers.Add("X-Tenant-Id", tenantId.ToString());
                    request.Headers.Add("X-User-Id", userId.ToString());
                    using var response = await http.SendAsync(request);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    Assert.Equal("Edited through webmail API", document.RootElement.GetProperty("data").GetProperty("subject").GetString());
                }
            }
        }
        finally
        {
            await CleanupTenantAsync(connection, tenantId);
        }
    }

    private static AppDbContext CreateContext(string connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    private static ImapSession CreateImapSession(IServiceProvider services, Guid tenantId, Guid userId, Guid personalMailboxId) =>
        new(services.GetRequiredService<AppDbContext>(), services.GetRequiredService<IMailStorage>(),
            new TestImapAuthenticator(tenantId, personalMailboxId, userId),
            services.GetRequiredService<ITenantAuthorizationHelper>(),
            services.GetRequiredService<IMailboxService>(), services.GetRequiredService<IMessageService>(),
            uidAllocator: services.GetRequiredService<IMessageUidAllocator>());

    private static async Task CleanupTenantAsync(string connection, Guid tenantId)
    {
        await using var db = CreateContext(connection);
        await db.AuditLogs.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await db.MessageRecipients.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Attachments.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Messages.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await db.MailboxDelegates.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Folders.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Mailboxes.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Users.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Domains.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync();
        await db.Tenants.Where(x => x.Id == tenantId).ExecuteDeleteAsync();
    }

    private sealed class TestImapAuthenticator(Guid tenantId, Guid mailboxId, Guid userId) : IImapAuthenticator
    {
        public Task<ImapAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImapAuthenticationResult(true, tenantId, mailboxId, userId));
    }
}
