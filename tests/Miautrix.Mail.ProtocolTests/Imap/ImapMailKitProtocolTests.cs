using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Infrastructure.Mail;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.ProtocolTests.Imap;

[CollectionDefinition("IMAP MailKit listener", DisableParallelization = true)]
public sealed class ImapMailKitListenerCollection : ICollectionFixture<ImapMailKitListenerFixture>;

public sealed record SeededImapMailbox(string Address, Guid TenantId, Guid MailboxId, Guid InboxId, Guid EmptyFolderId);

public sealed class ImapMailKitListenerFixture : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly string _storageDirectory = Path.Combine(Path.GetTempPath(), $"miautrix_imap_mailkit_{Guid.NewGuid():N}");
    private readonly List<Guid> _tenantIds = [];
    private readonly ConcurrentDictionary<string, (Guid TenantId, Guid MailboxId, Guid UserId)> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private ServiceProvider? _provider;
    private ImapListenerService? _listener;
    private FileSystemMailStorage? _storage;
    private X509Certificate2? _certificate;

    public int Port => _listener?.BoundPort ?? throw new InvalidOperationException("Listener is not started.");

    public ImapMailKitListenerFixture()
    {
        _connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION environment variable is required.");
        var connection = new NpgsqlConnectionStringBuilder(_connectionString);
        if ((connection.Database ?? string.Empty).Contains("-pro", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("IMAP protocol tests refuse to connect to production.");
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_storageDirectory);
        _storage = new FileSystemMailStorage(_storageDirectory);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using (var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(30)))
            // Windows SChannel cannot serve TLS with an ephemeral key; round-trip through PKCS#12 (no-op on Linux).
            _certificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_connectionString));
        services.AddSingleton<TlsCertificateProvider>(new TlsCertificateProvider(_certificate));
        services.AddScoped<IImapAuthenticator>(_ => new FixtureAuthenticator(_accounts));
        services.AddScoped<ITenantAuthorizationHelper, AllowTestMailboxAccess>();
        services.AddScoped<IPermissionRepository, EfPermissionRepository>();
        services.AddScoped<IMailboxService, MailboxService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<Miautrix.Mail.Application.Mail.IMessageUidAllocator, Miautrix.Mail.Application.Mail.MessageUidAllocator>();
        services.AddSingleton<IMailStorage>(_storage);
        services.AddSingleton<Miautrix.Mail.Application.Mail.Mime.IImapMimeFormatter, MimeKitImapMimeFormatter>();
        _provider = services.BuildServiceProvider();
        _listener = new ImapListenerService(_provider.GetRequiredService<IServiceScopeFactory>(), _storage,
            _provider.GetRequiredService<ILogger<ImapListenerService>>(), new TlsCertificateProvider(_certificate), port: 0);
        await _listener.StartAsync(CancellationToken.None);
        if (!SpinWait.SpinUntil(() => _listener.BoundPort != 0, TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("IMAP test listener failed to bind.");
    }

    public async Task<SeededImapMailbox> CreateMailboxAsync()
    {
        var tenantId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var emptyId = Guid.NewGuid();
        var projectsId = Guid.NewGuid();
        var address = $"mailkit_{Guid.NewGuid():N}@test.invalid";
        var userId = Guid.NewGuid();
        var raw = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: Café\r\nX-Folded: one\r\n two\r\nX-Long: {new string('x', 8192)}\r\nContent-Type: multipart/mixed; boundary=\"mixed\"\r\n\r\n--mixed\r\nContent-Type: multipart/alternative; boundary=\"alternative\"\r\n\r\n--alternative\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nBody π searchable\r\n--alternative\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<b>Body π searchable</b>\r\n--alternative--\r\n--mixed\r\nContent-Type: application/octet-stream; name=\"fixture.txt\"\r\nContent-Disposition: attachment; filename=\"fixture.txt\"\r\nContent-Transfer-Encoding: base64\r\n\r\nAQID\r\n--mixed--\r\n");
        await using var content = new MemoryStream(raw);
        var stored = await _storage!.StoreAsync(content);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options;
        await using var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant { Id = tenantId, Slug = $"mailkit-{tenantId:N}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Domains.Add(new DomainEntity { Id = domainId, TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Mailboxes.Add(new Mailbox { Id = mailboxId, TenantId = tenantId, DomainId = domainId, Address = address, Name = address, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Folders.AddRange(
            new Folder { Id = inboxId, TenantId = tenantId, MailboxId = mailboxId, Name = "INBOX", Role = "inbox", UidNext = 3, UidValidity = 21, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Folder { Id = emptyId, TenantId = tenantId, MailboxId = mailboxId, Name = "Empty", Role = "custom", UidNext = 1, UidValidity = 22, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Folder { Id = projectsId, TenantId = tenantId, MailboxId = mailboxId, Name = "Projects", Role = "custom", UidNext = 1, UidValidity = 23, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "2026", ParentId = projectsId, Role = "custom", UidNext = 1, UidValidity = 24, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Archive", Role = "archive", UidNext = 1, UidValidity = 26, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Trash", Role = "trash", UidNext = 1, UidValidity = 25, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        for (uint uid = 1; uid <= 2; uid++)
        {
            var id = Guid.NewGuid();
            db.Messages.Add(new Message { Id = id, TenantId = tenantId, MailboxId = mailboxId, FolderId = inboxId, Sender = "sender@test.invalid", Recipient = address, Subject = uid == 1 ? "Café" : "Café second", RawHeaders = $"From: sender@test.invalid\r\nTo: {address}\r\nSubject: Café\r\nX-Test: value\r\n\r\n", BodyText = "Body π searchable", BodyHtml = "<b>Body π searchable</b>", Date = DateTimeOffset.UtcNow, ContentHash = stored.ContentHash, StoragePath = stored.StoragePath, SizeBytes = raw.Length, Uid = uid, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            if (uid == 1)
            {
                db.MessageRecipients.Add(new MessageRecipient { Id = Guid.NewGuid(), TenantId = tenantId, MessageId = id, Type = "cc", Address = "copy@test.invalid" });
                db.MessageRecipients.Add(new MessageRecipient { Id = Guid.NewGuid(), TenantId = tenantId, MessageId = id, Type = "bcc", Address = "blind@test.invalid" });
            }
        }
        await db.SaveChangesAsync();
        _tenantIds.Add(tenantId);
        _accounts[address] = (tenantId, mailboxId, userId);
        return new SeededImapMailbox(address, tenantId, mailboxId, inboxId, emptyId);
    }

    public async Task<ImapClient> ConnectAsync(string address, CancellationToken cancellationToken = default)
    {
        var client = new ImapClient { ServerCertificateValidationCallback = (_, _, _, _) => true };
        await client.ConnectAsync("127.0.0.1", Port, SecureSocketOptions.SslOnConnect, cancellationToken);
        await client.AuthenticateAsync(address, "test-password", cancellationToken);
        return client;
    }

    public async Task DisposeAsync()
    {
        if (_listener is not null) await _listener.StopAsync(CancellationToken.None);
        _provider?.Dispose();
        if (_tenantIds.Count > 0)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options;
            await using var db = new AppDbContext(options);
            db.MessageRecipients.RemoveRange(db.MessageRecipients.Where(r => _tenantIds.Contains(r.TenantId)));
            db.Messages.RemoveRange(db.Messages.Where(m => _tenantIds.Contains(m.TenantId)));
            db.Folders.RemoveRange(db.Folders.Where(f => _tenantIds.Contains(f.TenantId)));
            db.Mailboxes.RemoveRange(db.Mailboxes.Where(m => _tenantIds.Contains(m.TenantId)));
            db.Domains.RemoveRange(db.Domains.Where(d => _tenantIds.Contains(d.TenantId)));
            db.Tenants.RemoveRange(db.Tenants.Where(t => _tenantIds.Contains(t.Id)));
            await db.SaveChangesAsync();
        }
        if (Directory.Exists(_storageDirectory)) Directory.Delete(_storageDirectory, recursive: true);
        _certificate?.Dispose();
    }

    private sealed class FixtureAuthenticator(ConcurrentDictionary<string, (Guid TenantId, Guid MailboxId, Guid UserId)> accounts) : IImapAuthenticator
    {
        public Task<ImapAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            if (password == "test-password" && accounts.TryGetValue(username, out var account))
                return Task.FromResult(new ImapAuthenticationResult(true, account.TenantId, account.MailboxId, account.UserId));
            return Task.FromResult(new ImapAuthenticationResult(false, Guid.Empty, Guid.Empty));
        }
    }

    private sealed class AllowTestMailboxAccess : ITenantAuthorizationHelper
    {
        public TResource AuthorizeAccess<TResource>(Guid currentTenantId, Guid currentUserId, TResource? resource, string requiredPermissionCode) where TResource : TenantScopedEntityBase => resource!;
        public void AssertPermission(Guid tenantId, Guid userId, string permissionCode) { }
        public void AssertMailboxAccess(Guid tenantId, Guid userId, Mailbox? mailbox, bool requireWrite) { if (mailbox is null) throw new ResourceNotFoundException(); }
        public void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId) { }
    }
}

[Collection("IMAP MailKit listener")]
[Trait("Category", "Imap")]
[Trait("RequiresDatabase", "true")]
public sealed class ImapMailKitProtocolTests(ImapMailKitListenerFixture fixture)
{
    [Fact]
    public async Task MailKit_authentication_capability_and_logout_after_auth_failure()
    {
        var mailbox = await fixture.CreateMailboxAsync();
        using var client = await fixture.ConnectAsync(mailbox.Address);
        Assert.Contains("IMAP4rev1", client.Capabilities.ToString());
        await client.DisconnectAsync(true);
        using var bad = new ImapClient { ServerCertificateValidationCallback = (_, _, _, _) => true };
        await bad.ConnectAsync("127.0.0.1", fixture.Port, SecureSocketOptions.SslOnConnect);
        await Assert.ThrowsAsync<MailKit.Security.AuthenticationException>(() => bad.AuthenticateAsync(mailbox.Address, "incorrect"));
        await bad.DisconnectAsync(true);
    }

    [Fact]
    public async Task MailKit_discovers_personal_shared_and_empty_other_users_namespaces()
    {
        var mailbox = await fixture.CreateMailboxAsync();
        using var client = await fixture.ConnectAsync(mailbox.Address);

        Assert.Single(client.PersonalNamespaces);
        Assert.Equal(string.Empty, client.PersonalNamespaces[0].Path);
        Assert.Equal('/', client.PersonalNamespaces[0].DirectorySeparator);
        Assert.Single(client.SharedNamespaces);
        // MailKit canonicalizes namespace prefixes by trimming the trailing delimiter.
        Assert.Equal("Shared", client.SharedNamespaces[0].Path);
        Assert.Equal('/', client.SharedNamespaces[0].DirectorySeparator);
        Assert.Empty(client.OtherNamespaces);
    }

    [Fact]
    public async Task MailKit_move_to_uses_server_move_and_updates_both_folders()
    {
        var mailbox = await fixture.CreateMailboxAsync();
        using var client = await fixture.ConnectAsync(mailbox.Address);
        Assert.True(client.Capabilities.HasFlag(ImapCapabilities.Move));
        await client.Inbox.OpenAsync(FolderAccess.ReadWrite);
        var summary = Assert.Single(await client.Inbox.FetchAsync(0, 0,
            new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope)));
        var archive = client.GetFolder("Archive");

        var destinationUid = await client.Inbox.MoveToAsync(summary.UniqueId, archive);

        Assert.NotNull(destinationUid);
        Assert.Equal(1, client.Inbox.Count);
        await archive.OpenAsync(FolderAccess.ReadOnly);
        Assert.Single(await archive.FetchAsync(0, 0, new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope)));
    }

    [Fact]
    public async Task MailKit_list_hierarchy_select_examine_status_and_search_criteria()
    {
        var mailbox = await fixture.CreateMailboxAsync();
        using var client = await fixture.ConnectAsync(mailbox.Address);
        var folders = await client.GetFoldersAsync(client.PersonalNamespaces[0], MailKit.StatusItems.None, false);
        Assert.Contains(folders, folder => folder.FullName == "Projects/2026");
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly);
        Assert.Equal(2, inbox.Count);
        Assert.Equal(2, (await inbox.SearchAsync(SearchQuery.All)).Count);
        Assert.Equal(2, (await inbox.SearchAsync(SearchQuery.SubjectContains("Café"))).Count);
        var empty = client.GetFolder("Empty");
        await empty.OpenAsync(FolderAccess.ReadOnly);
        Assert.Empty(await empty.SearchAsync(SearchQuery.All));
    }

    [Fact]
    public async Task MailKit_fetch_mime_attachment_unicode_and_body_peek_read_only_enforcement()
    {
        var mailbox = await fixture.CreateMailboxAsync();
        using var client = await fixture.ConnectAsync(mailbox.Address);
        await client.Inbox.OpenAsync(FolderAccess.ReadOnly);
        var summaries = await client.Inbox.FetchAsync(0, -1,
            new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure |
                MessageSummaryItems.Flags | MessageSummaryItems.InternalDate | MessageSummaryItems.Size));
        Assert.Equal(2, summaries.Count);
        Assert.Contains(summaries, summary => summary.Envelope?.Subject == "Café");
        Assert.All(summaries, summary => Assert.NotNull(summary.Body));
        var message = await client.Inbox.GetMessageAsync(summaries[0].Index);
        Assert.Contains(message.Attachments, attachment => attachment.ContentDisposition?.Disposition.Equals("attachment", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains("Body π", message.TextBody);
        Assert.Equal(2, (await client.Inbox.SearchAsync(SearchQuery.Not(SearchQuery.Seen))).Count);
        // MailKit refuses STORE on a read-only folder client-side; the server-side [READ-ONLY] rejection is
        // asserted by the raw-socket test (A3b).
        await Assert.ThrowsAsync<MailKit.FolderNotOpenException>(() => client.Inbox.AddFlagsAsync(summaries[0].UniqueId, MessageFlags.Seen, true));
    }

    [Fact]
    public async Task MailKit_store_expunge_sequence_renumbering_and_uid_stability_across_sessions()
    {
        var mailbox = await fixture.CreateMailboxAsync();
        using (var client = await fixture.ConnectAsync(mailbox.Address))
        {
            await client.Inbox.OpenAsync(FolderAccess.ReadWrite);
            var all = await client.Inbox.FetchAsync(0, -1, new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Flags));
            var removed = all.Single(item => item.UniqueId.Id == 1).UniqueId;
            await client.Inbox.AddFlagsAsync(removed, MessageFlags.Deleted, true);
            await client.Inbox.ExpungeAsync();
            Assert.Equal(1, client.Inbox.Count);
            var remaining = Assert.Single(await client.Inbox.FetchAsync(0, 0, new FetchRequest(MessageSummaryItems.UniqueId)));
            Assert.Equal((uint)2, remaining.UniqueId.Id);
        }
        using var second = await fixture.ConnectAsync(mailbox.Address);
        await second.Inbox.OpenAsync(FolderAccess.ReadOnly);
        Assert.Equal((uint)2, Assert.Single(await second.Inbox.FetchAsync(0, 0, new FetchRequest(MessageSummaryItems.UniqueId))).UniqueId.Id);
    }

    [Fact]
    public async Task MailKit_persistent_commands_nonexistent_fetch_and_two_concurrent_clients()
    {
        var mailbox = await fixture.CreateMailboxAsync();
        using var first = await fixture.ConnectAsync(mailbox.Address);
        await first.Inbox.OpenAsync(FolderAccess.ReadOnly);
        Assert.Empty(await first.Inbox.FetchAsync(98, 98, new FetchRequest(MessageSummaryItems.UniqueId)));
        Assert.Equal(2, (await first.Inbox.SearchAsync(SearchQuery.FromContains("sender@test.invalid"))).Count);
        Assert.Equal(2, (await first.Inbox.SearchAsync(SearchQuery.Not(SearchQuery.Seen))).Count);
        Assert.Equal(2, (await first.Inbox.SearchAsync(SearchQuery.Or(SearchQuery.Seen, SearchQuery.Not(SearchQuery.Seen)))).Count);
        var all = await first.Inbox.FetchAsync(0, -1, new FetchRequest(MessageSummaryItems.UniqueId));
        Assert.Equal(new uint[] { 1, 2 }, all.Select(item => item.UniqueId.Id).ToArray());

        async Task CheckSecondClientAsync()
        {
            using var second = await fixture.ConnectAsync(mailbox.Address);
            await second.Inbox.OpenAsync(FolderAccess.ReadOnly);
            var summary = Assert.Single(await second.Inbox.FetchAsync(0, 0, new FetchRequest(MessageSummaryItems.UniqueId)));
            Assert.Equal((uint)1, summary.UniqueId.Id);
            await second.DisconnectAsync(true);
        }
        await Task.WhenAll(CheckSecondClientAsync(), CheckSecondClientAsync());
        await first.DisconnectAsync(true);
    }
}
