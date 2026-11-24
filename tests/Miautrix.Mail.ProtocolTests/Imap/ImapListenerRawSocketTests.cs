using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Infrastructure.Mail;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Worker;
using MailKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.ProtocolTests.Imap;

[Trait("Category", "Imap")]
[Trait("RequiresDatabase", "true")]
public sealed class ImapListenerRawSocketTests
{
    [Fact]
    public async Task Report_python_sequence_over_test_listener_has_exact_literal_framing()
    {
        var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION environment variable is required.");
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if ((connection.Database ?? string.Empty).Contains("-pro", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("IMAP protocol tests refuse to connect to production.");

        var tenantId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var address = $"imap_{Guid.NewGuid():N}@test.invalid";
        var longHeader = new string('x', 8192);
        var raw = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: Café\r\nX-Folded: one\r\n two\r\nX-Long: {longHeader}\r\nContent-Type: multipart/mixed; boundary=\"mime-boundary\"\r\n\r\n--mime-boundary\r\nContent-Type: multipart/alternative; boundary=\"alternative-boundary\"\r\n\r\n--alternative-boundary\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nBody π\r\n--alternative-boundary\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<b>Body π</b>\r\n--alternative-boundary--\r\n--mime-boundary\r\nContent-Type: application/octet-stream; name=\"fixture.txt\"\r\nContent-Disposition: attachment; filename=\"fixture.txt\"\r\nContent-Transfer-Encoding: base64\r\n\r\nAQID\r\n--mime-boundary--\r\n");
        var storageDir = Path.Combine(Path.GetTempPath(), $"miautrix_imap_listener_{Guid.NewGuid():N}");
        Directory.CreateDirectory(storageDir);
        var storage = new FileSystemMailStorage(storageDir);
        await using var rawStream = new MemoryStream(raw);
        var stored = await storage.StoreAsync(rawStream);
        await using var secondRawStream = new MemoryStream(raw);
        var secondStored = await storage.StoreAsync(secondRawStream);
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using (var seed = new AppDbContext(dbOptions))
        {
            seed.Tenants.Add(new Tenant { Id = tenantId, Slug = $"imap-{tenantId:N}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Domains.Add(new DomainEntity { Id = domainId, TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Mailboxes.Add(new Mailbox { Id = mailboxId, TenantId = tenantId, DomainId = domainId, Address = address, Name = address, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Folders.Add(new Folder { Id = folderId, TenantId = tenantId, MailboxId = mailboxId, Name = "INBOX", Role = "inbox", UidNext = 2, UidValidity = 9, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Folders.Add(new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Empty", Role = "custom", UidNext = 1, UidValidity = 10, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            var projectsId = Guid.NewGuid();
            seed.Folders.Add(new Folder { Id = projectsId, TenantId = tenantId, MailboxId = mailboxId, Name = "Projects", Role = "custom", UidNext = 1, UidValidity = 11, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Folders.Add(new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "2026", ParentId = projectsId, Role = "custom", UidNext = 1, UidValidity = 12, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            var firstMessageDate = DateTimeOffset.UtcNow;
            const string storedHeaders = "From: sender@test.invalid\r\nTo: test@example.invalid\r\nSubject: Café\r\nX-Test: value\r\n\r\n";
            seed.Messages.Add(new Message { Id = messageId, TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId, Sender = "sender@test.invalid", Recipient = address, Subject = "Café", RawHeaders = storedHeaders, BodyText = "Body π searchable", BodyHtml = "<b>Body π searchable</b>", Date = firstMessageDate, ContentHash = stored.ContentHash, StoragePath = stored.StoragePath, SizeBytes = raw.Length, Uid = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            var secondMessageId = Guid.NewGuid();
            seed.Messages.Add(new Message { Id = secondMessageId, TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId, Sender = "sender@test.invalid", Recipient = address, Subject = "Café second", RawHeaders = storedHeaders, BodyText = "Body π searchable", BodyHtml = "<b>Body π searchable</b>", Date = firstMessageDate, ContentHash = secondStored.ContentHash, StoragePath = secondStored.StoragePath, SizeBytes = raw.Length, Uid = 2, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.MessageRecipients.Add(new MessageRecipient { Id = Guid.NewGuid(), TenantId = tenantId, MessageId = messageId, Type = "cc", Address = "copy@test.invalid" });
            seed.MessageRecipients.Add(new MessageRecipient { Id = Guid.NewGuid(), TenantId = tenantId, MessageId = messageId, Type = "bcc", Address = "blind@test.invalid" });
            await seed.SaveChangesAsync();
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(30));
        // Windows SChannel cannot serve TLS with an ephemeral key; round-trip through PKCS#12 (no-op on Linux).
        using var certificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<Miautrix.Mail.Worker.TlsCertificateProvider>(new TlsCertificateProvider(certificate));
        services.AddScoped<IImapAuthenticator>(_ => new StubAuthenticator(tenantId, mailboxId, userId));
        services.AddScoped<ITenantAuthorizationHelper, AllowTestMailboxAccess>();
        services.AddScoped<IPermissionRepository, EfPermissionRepository>();
        services.AddScoped<IMailboxService, MailboxService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<Miautrix.Mail.Application.Mail.IMessageUidAllocator, Miautrix.Mail.Application.Mail.MessageUidAllocator>();
        services.AddSingleton<IMailStorage>(storage);
        services.AddSingleton<Miautrix.Mail.Application.Mail.Mime.IImapMimeFormatter, MimeKitImapMimeFormatter>();
        using var provider = services.BuildServiceProvider();
        var service = new ImapListenerService(provider.GetRequiredService<IServiceScopeFactory>(), storage,
            provider.GetRequiredService<ILogger<ImapListenerService>>(), new TlsCertificateProvider(certificate), port: 0);
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await service.StartAsync(stopping.Token);
        Assert.True(SpinWait.SpinUntil(() => service.BoundPort != 0, TimeSpan.FromSeconds(5)), "IMAP test listener did not bind.");

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", service.BoundPort, stopping.Token);
            using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, (_, _, _, _) => true);
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" }, stopping.Token);
            var reader = new ImapConnectionReader(tls);
            Assert.Contains("CAPABILITY", Encoding.UTF8.GetString(await reader.ReadLineAsync(stopping.Token) ?? []));
            var capabilities = await SendAndReadAsync(tls, reader, "A0 CAPABILITY", "A0", stopping.Token);
            Assert.Contains("IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UNSELECT UIDPLUS SPECIAL-USE", Encoding.UTF8.GetString(capabilities));

            await SendAndReadAsync(tls, reader, $"A1 LOGIN \"{address}\" \"test-password\"", "A1", stopping.Token);
            await SendAndReadAsync(tls, reader, "A1b ENABLE UTF8=ACCEPT", "A1b", stopping.Token);
            await SendAndReadAsync(tls, reader, "A2 LIST \"\" \"*\"", "A2", stopping.Token);
            var listPattern = await SendAndReadAsync(tls, reader, "A2b LIST \"\" \"IN%\"", "A2b", stopping.Token);
            Assert.Contains("INBOX", Encoding.UTF8.GetString(listPattern));
            var hierarchy = await SendAndReadAsync(tls, reader, "A2bb LIST \"\" \"Projects\"", "A2bb", stopping.Token);
            Assert.Contains("\\HasChildren", Encoding.UTF8.GetString(hierarchy));
            var hierarchyChild = await SendAndReadAsync(tls, reader, "A2bc LIST \"\" \"Projects/%\"", "A2bc", stopping.Token);
            Assert.Contains("Projects/2026", Encoding.UTF8.GetString(hierarchyChild));
            var lsub = await SendAndReadAsync(tls, reader, "A2c LSUB \"\" \"*\"", "A2c", stopping.Token);
            Assert.Contains("* LSUB", Encoding.UTF8.GetString(lsub));
            var status = await SendAndReadAsync(tls, reader, "A2d STATUS INBOX (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN)", "A2d", stopping.Token);
            Assert.Contains("MESSAGES 2", Encoding.UTF8.GetString(status));
            Assert.Contains("RECENT 0", Encoding.UTF8.GetString(status));
            var examine = await SendAndReadAsync(tls, reader, "A3 EXAMINE INBOX", "A3", stopping.Token);
            Assert.Contains("[READ-ONLY]", Encoding.UTF8.GetString(examine));
            Assert.Contains("[UNSEEN 1]", Encoding.UTF8.GetString(examine));
            Assert.Contains("PERMANENTFLAGS ()", Encoding.UTF8.GetString(examine));
            await SendAndReadAsync(tls, reader, "A3a CHECK", "A3a", stopping.Token);
            Assert.Contains("Body π", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3aa FETCH 1 (BODY[1])", "A3aa", stopping.Token)));
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3ab SEARCH UNSEEN", "A3ab", stopping.Token)));
            var readonlyStore = await SendAndReadAsync(tls, reader, "A3b STORE 1 +FLAGS (\\Seen)", "A3b", stopping.Token);
            Assert.Contains("[READ-ONLY]", Encoding.UTF8.GetString(readonlyStore));
            await SendAndReadAsync(tls, reader, "A3c SELECT INBOX", "A3c", stopping.Token);
            var peekText = await SendAndReadAsync(tls, reader, "A3d FETCH 1 (BODY.PEEK[1])", "A3d", stopping.Token);
            Assert.Contains("Body π", Encoding.UTF8.GetString(peekText));
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3e SEARCH UNSEEN", "A3e", stopping.Token)));
            var bodyText = await SendAndReadAsync(tls, reader, "A3f FETCH 1 (BODY[1])", "A3f", stopping.Token);
            Assert.Contains("Body π", Encoding.UTF8.GetString(bodyText));
            Assert.Contains("* SEARCH 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3g SEARCH SEEN", "A3g", stopping.Token)));
            var search = await SendAndReadAsync(tls, reader, "A4 SEARCH ALL", "A4", stopping.Token);
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(search));
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A4a SEARCH OR UNSEEN FROM sender@test.invalid", "A4a", stopping.Token)));
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A4b UID SEARCH 1:*", "A4b", stopping.Token)));
            var dateCriterion = DateTime.UtcNow.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
            var searchCriteria = new[] { "UNSEEN", "UNANSWERED", "UNFLAGGED", "UNDELETED", "UNDRAFT", "OLD", "FROM sender@test.invalid", $"TO {address}", "CC copy@test.invalid", "BCC blind@test.invalid", "SUBJECT Café", "BODY searchable", "TEXT Café", "HEADER X-Test value", "SINCE 01-Jan-2020", "BEFORE 01-Jan-2030", $"ON {dateCriterion}", "SENTSINCE 01-Jan-2020", "SENTBEFORE 01-Jan-2030", $"SENTON {dateCriterion}", "LARGER 1", "SMALLER 999999", "CHARSET UTF-8 SUBJECT Café", "NOT SEEN", "OR SEEN UNSEEN", "(UNSEEN SUBJECT Café)" };
            // Message 1 is \Seen after A3f and is the only one with Cc/Bcc recipients.
            var expectedOverrides = new Dictionary<string, string>
            {
                ["UNSEEN"] = "* SEARCH 2", ["NOT SEEN"] = "* SEARCH 2", ["(UNSEEN SUBJECT Café)"] = "* SEARCH 2",
                ["CC copy@test.invalid"] = "* SEARCH 1", ["BCC blind@test.invalid"] = "* SEARCH 1",
            };
            var searchMismatches = new List<string>();
            for (var index = 0; index < searchCriteria.Length; index++)
            {
                var tag = $"A4x{index}";
                var expected = expectedOverrides.GetValueOrDefault(searchCriteria[index], "* SEARCH 1 2");
                var response = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"{tag} SEARCH {searchCriteria[index]}", tag, stopping.Token));
                var untagged = response.Split('\n').Select(line => line.TrimEnd('\r')).FirstOrDefault(line => line.StartsWith("* SEARCH", StringComparison.Ordinal)) ?? "<none>";
                if (untagged.TrimEnd() != expected) searchMismatches.Add($"SEARCH {searchCriteria[index]}: expected '{expected}', got '{untagged}'");
            }
            Assert.True(searchMismatches.Count == 0, string.Join(Environment.NewLine, searchMismatches));
            foreach (var (criterion, expected) in new[] { ("SEEN", "* SEARCH 1\r\n"), ("ANSWERED", "* SEARCH\r\n"), ("FLAGGED", "* SEARCH\r\n"), ("DELETED", "* SEARCH\r\n"), ("DRAFT", "* SEARCH\r\n"), ("NEW", "* SEARCH\r\n"), ("RECENT", "* SEARCH\r\n"), ("1:*", "* SEARCH 1 2\r\n") })
            {
                var tag = $"A4n{Array.IndexOf(new[] { "SEEN", "ANSWERED", "FLAGGED", "DELETED", "DRAFT", "NEW", "RECENT", "1:*" }, criterion)}";
                Assert.Contains(expected, Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"{tag} SEARCH {criterion}", tag, stopping.Token)));
            }
            Assert.Contains("[BADCHARSET", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A4c SEARCH CHARSET KOI8-R ALL", "A4c", stopping.Token)));
            Assert.Contains(" BAD ", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A4d SEARCH SOMETHING", "A4d", stopping.Token)));
            var fetch = await SendAndReadAsync(tls, reader, "A5 FETCH 1 (UID FLAGS BODY.PEEK[HEADER])", "A5", stopping.Token);
            var header = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: Café\r\nX-Folded: one\r\n two\r\nX-Long: {longHeader}\r\nContent-Type: multipart/mixed; boundary=\"mime-boundary\"\r\n\r\n");
            var fetchText = Encoding.UTF8.GetString(fetch);
            Assert.Contains($"BODY[HEADER] {{{header.Length}}}\r\n", fetchText);
            Assert.True(ContainsBytes(fetch, header));
            var rangedUidFetch = await SendAndReadAsync(tls, reader, "A5b UID FETCH 1:* (UID ENVELOPE BODYSTRUCTURE)", "A5b", stopping.Token);
            Assert.Equal(2, Encoding.UTF8.GetString(rangedUidFetch).Split(" FETCH (", StringSplitOptions.None).Length - 1);
            Assert.Contains("ENVELOPE", Encoding.UTF8.GetString(rangedUidFetch));
            Assert.Contains("BODYSTRUCTURE", Encoding.UTF8.GetString(rangedUidFetch));
            var fetchItems = await SendAndReadAsync(tls, reader, "A5c FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)", "A5c", stopping.Token);
            Assert.Equal(2, Encoding.UTF8.GetString(fetchItems).Split(" FETCH (", StringSplitOptions.None).Length - 1);
            Assert.Contains(" BAD ", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5d BOGUS", "A5d", stopping.Token)));
            var logout = await SendAndReadAsync(tls, reader, "A6 LOGOUT", "A6", stopping.Token);
            Assert.Contains("* BYE", Encoding.UTF8.GetString(logout));

            using var mailKit = new MailKit.Net.Imap.ImapClient();
            mailKit.ServerCertificateValidationCallback = (_, _, _, _) => true;
            await mailKit.ConnectAsync("127.0.0.1", service.BoundPort, MailKit.Security.SecureSocketOptions.SslOnConnect, stopping.Token);
            await mailKit.AuthenticateAsync(address, "test-password", stopping.Token);
            await mailKit.Inbox.OpenAsync(MailKit.FolderAccess.ReadOnly, stopping.Token);
            Assert.Equal(2, mailKit.Inbox.Count);
            Assert.Equal(2, (await mailKit.Inbox.SearchAsync(MailKit.Search.SearchQuery.All, stopping.Token)).Count);
            Assert.Equal(2, (await mailKit.Inbox.SearchAsync(MailKit.Search.SearchQuery.SubjectContains("Café"), stopping.Token)).Count);
            var names = await mailKit.GetFoldersAsync(mailKit.PersonalNamespaces[0], MailKit.StatusItems.None, false, stopping.Token);
            Assert.Contains(names, folder => folder.FullName.Equals("INBOX", StringComparison.OrdinalIgnoreCase));
            var summaries = await mailKit.Inbox.FetchAsync(0, -1,
                new MailKit.FetchRequest(MailKit.MessageSummaryItems.UniqueId | MailKit.MessageSummaryItems.Envelope |
                MailKit.MessageSummaryItems.BodyStructure | MailKit.MessageSummaryItems.Flags |
                MailKit.MessageSummaryItems.InternalDate | MailKit.MessageSummaryItems.Size), stopping.Token);
            Assert.Contains(summaries, summary => summary.Envelope?.Subject == "Café");
            Assert.Equal(new uint[] { 1, 2 }, summaries.Select(summary => summary.UniqueId.Id).ToArray());
            Assert.All(summaries, summary => Assert.NotNull(summary.Body));
            var downloaded = await mailKit.Inbox.GetMessageAsync(summaries[0].Index, stopping.Token);
            Assert.Contains(downloaded.Attachments, attachment => attachment.ContentDisposition?.Disposition.Equals("attachment", StringComparison.OrdinalIgnoreCase) == true);
            var empty = mailKit.GetFolder("Empty");
            await empty.OpenAsync(MailKit.FolderAccess.ReadOnly, stopping.Token);
            Assert.Empty(await empty.SearchAsync(MailKit.Search.SearchQuery.All, stopping.Token));
            await mailKit.Inbox.OpenAsync(MailKit.FolderAccess.ReadOnly, stopping.Token);
            Assert.Empty(await mailKit.Inbox.FetchAsync(98, 98,
                new MailKit.FetchRequest(MailKit.MessageSummaryItems.UniqueId | MailKit.MessageSummaryItems.Envelope), stopping.Token));
            await mailKit.Inbox.OpenAsync(MailKit.FolderAccess.ReadWrite, stopping.Token);
            await mailKit.Inbox.AddFlagsAsync(summaries[0].UniqueId, MailKit.MessageFlags.Seen, true, stopping.Token);
            Assert.Single(await mailKit.Inbox.SearchAsync(MailKit.Search.SearchQuery.Seen, stopping.Token));
            var expungedUid = summaries.Single(summary => summary.UniqueId.Id == 1).UniqueId;
            await mailKit.Inbox.AddFlagsAsync(expungedUid, MailKit.MessageFlags.Deleted, true, stopping.Token);
            await mailKit.Inbox.ExpungeAsync(stopping.Token);
            Assert.Equal(1, mailKit.Inbox.Count);
            var remaining = await mailKit.Inbox.FetchAsync(0, 0,
                new MailKit.FetchRequest(MailKit.MessageSummaryItems.UniqueId), stopping.Token);
            Assert.Equal((uint)2, Assert.Single(remaining).UniqueId.Id);
            await mailKit.DisconnectAsync(true, stopping.Token);

            using var badAuth = new MailKit.Net.Imap.ImapClient { ServerCertificateValidationCallback = (_, _, _, _) => true };
            await badAuth.ConnectAsync("127.0.0.1", service.BoundPort, MailKit.Security.SecureSocketOptions.SslOnConnect, stopping.Token);
            await Assert.ThrowsAsync<MailKit.Security.AuthenticationException>(() => badAuth.AuthenticateAsync(address, "bad-password", stopping.Token));
            await badAuth.DisconnectAsync(true, stopping.Token);

            async Task ConcurrentClientAsync()
            {
                using var client = new MailKit.Net.Imap.ImapClient { ServerCertificateValidationCallback = (_, _, _, _) => true };
                await client.ConnectAsync("127.0.0.1", service.BoundPort, MailKit.Security.SecureSocketOptions.SslOnConnect, stopping.Token);
                await client.AuthenticateAsync(address, "test-password", stopping.Token);
                await client.Inbox.OpenAsync(MailKit.FolderAccess.ReadOnly, stopping.Token);
                Assert.Equal(1, client.Inbox.Count);
                var uid = Assert.Single(await client.Inbox.FetchAsync(0, 0,
                    new MailKit.FetchRequest(MailKit.MessageSummaryItems.UniqueId), stopping.Token)).UniqueId.Id;
                Assert.Equal((uint)2, uid);
                await client.DisconnectAsync(true, stopping.Token);
            }
            await Task.WhenAll(ConcurrentClientAsync(), ConcurrentClientAsync());
        }
        finally
        {
            await service.StopAsync(CancellationTokenSource.CreateLinkedTokenSource(stopping.Token).Token);
            await using var cleanup = new AppDbContext(dbOptions);
            cleanup.Messages.RemoveRange(cleanup.Messages.Where(m => m.TenantId == tenantId));
            cleanup.Folders.RemoveRange(cleanup.Folders.Where(f => f.TenantId == tenantId));
            cleanup.Mailboxes.RemoveRange(cleanup.Mailboxes.Where(m => m.TenantId == tenantId));
            cleanup.Domains.RemoveRange(cleanup.Domains.Where(d => d.TenantId == tenantId));
            cleanup.Tenants.RemoveRange(cleanup.Tenants.Where(t => t.Id == tenantId));
            await cleanup.SaveChangesAsync();
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, recursive: true);
        }
    }

    private static async Task<byte[]> SendAndReadAsync(SslStream stream, ImapConnectionReader reader, string command, string tag, CancellationToken ct)
    {
        // UTF-8: the session ENABLEs UTF8=ACCEPT, and ASCII would turn "Café" into "Caf?".
        await stream.WriteAsync(Encoding.UTF8.GetBytes(command + "\r\n"), ct);
        using var result = new MemoryStream();
        while (true)
        {
            var line = await reader.ReadLineAsync(ct) ?? throw new EndOfStreamException();
            result.Write(line); result.Write("\r\n"u8);
            var text = Encoding.UTF8.GetString(line);
            var marker = System.Text.RegularExpressions.Regex.Match(text, @"\{(\d+)\}$");
            if (marker.Success) result.Write(await reader.ReadLiteralAsync(int.Parse(marker.Groups[1].Value), ct));
            if (text.StartsWith(tag + " ", StringComparison.Ordinal)) break;
        }
        return result.ToArray();
    }

    private static bool ContainsBytes(byte[] source, byte[] expected)
    {
        for (var i = 0; i <= source.Length - expected.Length; i++) if (source.AsSpan(i, expected.Length).SequenceEqual(expected)) return true;
        return false;
    }

    private sealed class StubAuthenticator(Guid tenantId, Guid mailboxId, Guid userId) : IImapAuthenticator
    {
        public Task<ImapAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(password == "bad-password"
                ? new ImapAuthenticationResult(false, Guid.Empty, Guid.Empty)
                : new ImapAuthenticationResult(true, tenantId, mailboxId, userId));
    }

    private sealed class AllowTestMailboxAccess : ITenantAuthorizationHelper
    {
        public TResource AuthorizeAccess<TResource>(Guid currentTenantId, Guid currentUserId, TResource? resource, string requiredPermissionCode) where TResource : TenantScopedEntityBase => resource!;
        public void AssertPermission(Guid tenantId, Guid userId, string permissionCode) { }
        public void AssertMailboxAccess(Guid tenantId, Guid userId, Mailbox? mailbox, bool requireWrite) { if (mailbox is null) throw new ResourceNotFoundException(); }
        public void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId) { }
    }
}
