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
        var secondUserId = Guid.NewGuid();
        var secondMailboxId = Guid.NewGuid();
        var secondFolderId = Guid.NewGuid();
        var salesMailboxId = Guid.NewGuid();
        var salesInboxId = Guid.NewGuid();
        var salesTrashId = Guid.NewGuid();
        var readMailboxId = Guid.NewGuid();
        var readInboxId = Guid.NewGuid();
        var readTrashId = Guid.NewGuid();
        var sendOnlyMailboxId = Guid.NewGuid();
        var sendOnlyInboxId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var otherDomainId = Guid.NewGuid();
        var otherMailboxId = Guid.NewGuid();
        var address = $"imap_{Guid.NewGuid():N}@test.invalid";
        var secondAddress = $"delegate_{Guid.NewGuid():N}@test.invalid";
        var salesAddress = $"sales_{Guid.NewGuid():N}@test.invalid";
        var readAddress = $"read_{Guid.NewGuid():N}@test.invalid";
        var sendOnlyAddress = $"sendonly_{Guid.NewGuid():N}@test.invalid";
        var otherTenantAddress = $"other_{Guid.NewGuid():N}@cross.invalid";
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
            seed.Users.Add(new User { Id = userId, TenantId = tenantId, Email = address, Name = address, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Users.Add(new User { Id = secondUserId, TenantId = tenantId, Email = secondAddress, Name = secondAddress, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Mailboxes.Add(new Mailbox { Id = mailboxId, TenantId = tenantId, DomainId = domainId, Address = address, Name = address, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Mailboxes.Add(new Mailbox { Id = secondMailboxId, TenantId = tenantId, DomainId = domainId, Address = secondAddress, Name = secondAddress, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Mailboxes.AddRange(
                new Mailbox { Id = salesMailboxId, TenantId = tenantId, DomainId = domainId, Address = salesAddress, Name = salesAddress, Kind = "shared", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Mailbox { Id = readMailboxId, TenantId = tenantId, DomainId = domainId, Address = readAddress, Name = readAddress, Kind = "shared", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Mailbox { Id = sendOnlyMailboxId, TenantId = tenantId, DomainId = domainId, Address = sendOnlyAddress, Name = sendOnlyAddress, Kind = "shared", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.MailboxDelegates.AddRange(
                new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = salesMailboxId, UserId = userId, AccessLevel = "write" },
                new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = readMailboxId, UserId = userId, AccessLevel = "read" },
                new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = sendOnlyMailboxId, UserId = userId, AccessLevel = "none", CanSendAs = true },
                new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = salesMailboxId, UserId = secondUserId, AccessLevel = "write" });
            seed.Tenants.Add(new Tenant { Id = otherTenantId, Slug = $"imap-other-{otherTenantId:N}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Domains.Add(new DomainEntity { Id = otherDomainId, TenantId = otherTenantId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Mailboxes.Add(new Mailbox { Id = otherMailboxId, TenantId = otherTenantId, DomainId = otherDomainId, Address = otherTenantAddress, Name = otherTenantAddress, Kind = "shared", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Folders.Add(new Folder { Id = folderId, TenantId = tenantId, MailboxId = mailboxId, Name = "INBOX", Role = "inbox", UidNext = 3, UidValidity = 9, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Folders.Add(new Folder { Id = secondFolderId, TenantId = tenantId, MailboxId = secondMailboxId, Name = "INBOX", Role = "inbox", UidNext = 1, UidValidity = 19, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Folders.AddRange(
                new Folder { Id = salesInboxId, TenantId = tenantId, MailboxId = salesMailboxId, Name = "INBOX", Role = "inbox", UidNext = 2, UidValidity = 21, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Folder { Id = salesTrashId, TenantId = tenantId, MailboxId = salesMailboxId, Name = "Trash", Role = "trash", UidNext = 1, UidValidity = 22, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Folder { Id = readInboxId, TenantId = tenantId, MailboxId = readMailboxId, Name = "INBOX", Role = "inbox", UidNext = 2, UidValidity = 31, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Folder { Id = readTrashId, TenantId = tenantId, MailboxId = readMailboxId, Name = "Trash", Role = "trash", UidNext = 1, UidValidity = 32, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Folder { Id = sendOnlyInboxId, TenantId = tenantId, MailboxId = sendOnlyMailboxId, Name = "INBOX", Role = "inbox", UidNext = 1, UidValidity = 41, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Folders.Add(new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Empty", Role = "custom", UidNext = 1, UidValidity = 10, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Folders.AddRange(
                new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Sent", Role = "sent", UidNext = 1, UidValidity = 13, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Drafts", Role = "drafts", UidNext = 1, UidValidity = 14, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Trash", Role = "trash", UidNext = 1, UidValidity = 15, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Archive", Role = "archive", UidNext = 1, UidValidity = 17, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Junk", Role = "junk", UidNext = 1, UidValidity = 18, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "Café", Role = "custom", UidNext = 1, UidValidity = 16, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            var projectsId = Guid.NewGuid();
            seed.Folders.Add(new Folder { Id = projectsId, TenantId = tenantId, MailboxId = mailboxId, Name = "Projects", Role = "custom", UidNext = 1, UidValidity = 11, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Folders.Add(new Folder { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = "2026", ParentId = projectsId, Role = "custom", UidNext = 1, UidValidity = 12, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            var firstMessageDate = DateTimeOffset.UtcNow;
            const string storedHeaders = "From: sender@test.invalid\r\nTo: test@example.invalid\r\nSubject: Café\r\nX-Test: value\r\n\r\n";
            seed.Messages.Add(new Message { Id = messageId, TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId, Sender = "sender@test.invalid", Recipient = address, Subject = "Café", RawHeaders = storedHeaders, BodyText = "Body π searchable", BodyHtml = "<b>Body π searchable</b>", Date = firstMessageDate, ContentHash = stored.ContentHash, StoragePath = stored.StoragePath, SizeBytes = raw.Length, Uid = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            var secondMessageId = Guid.NewGuid();
            seed.Messages.Add(new Message { Id = secondMessageId, TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId, Sender = "sender@test.invalid", Recipient = address, Subject = "Café second", RawHeaders = storedHeaders, BodyText = "Body π searchable", BodyHtml = "<b>Body π searchable</b>", Date = firstMessageDate, ContentHash = secondStored.ContentHash, StoragePath = secondStored.StoragePath, SizeBytes = raw.Length, Uid = 2, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            seed.Messages.AddRange(
                new Message { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = salesMailboxId, FolderId = salesInboxId, Sender = "sender@test.invalid", Recipient = salesAddress, Subject = "Sales shared", RawHeaders = storedHeaders, BodyText = "sales body", Date = firstMessageDate, ContentHash = stored.ContentHash, StoragePath = stored.StoragePath, SizeBytes = raw.Length, Uid = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Message { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = readMailboxId, FolderId = readInboxId, Sender = "sender@test.invalid", Recipient = readAddress, Subject = "Read shared", RawHeaders = storedHeaders, BodyText = "read body", Date = firstMessageDate, ContentHash = stored.ContentHash, StoragePath = stored.StoragePath, SizeBytes = raw.Length, Uid = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
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
        var authenticator = new StubAuthenticator(tenantId, mailboxId, userId);
        authenticator.AddAccount(secondAddress, secondMailboxId, secondUserId);
        services.AddSingleton<IImapAuthenticator>(authenticator);
        services.AddSingleton<ISecurityEventSink, InMemorySecurityEventSink>();
        services.AddScoped<ITenantAuthorizationHelper, TenantAuthorizationHelper>();
        services.AddScoped<IPermissionRepository, EfPermissionRepository>();
        services.AddScoped<IMailboxService, MailboxService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddSingleton<Miautrix.Mail.Application.Mail.Mime.IMimeMessageParser, Miautrix.Mail.Infrastructure.Mail.MimeKitMimeMessageParser>();
        services.AddScoped<Miautrix.Mail.Application.Mail.IMessageUidAllocator, Miautrix.Mail.Application.Mail.MessageUidAllocator>();
        services.AddSingleton<IMailStorage>(storage);
        services.AddSingleton<Miautrix.Mail.Application.Mail.Mime.IImapMimeFormatter, MimeKitImapMimeFormatter>();
        using var provider = services.BuildServiceProvider();
        var service = new ImapListenerService(provider.GetRequiredService<IServiceScopeFactory>(), storage,
            provider.GetRequiredService<ILogger<ImapListenerService>>(), new TlsCertificateProvider(certificate), port: 0,
            idleTimeout: TimeSpan.FromSeconds(4), idlePollInterval: TimeSpan.FromMilliseconds(200), maxLiteralBytes: 8192);
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(45));
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
            var capabilityText = Encoding.UTF8.GetString(capabilities);
            Assert.Equal("* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE NAMESPACE MOVE IDLE\r\nA0 OK CAPABILITY completed\r\n", capabilityText);
            Assert.Equal(capabilityText, Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A0 CAPABILITY", "A0", stopping.Token)));
            foreach (var capability in new[] { "IMAP4rev1", "AUTH=PLAIN", "SASL-IR", "LITERAL+", "ENABLE", "UTF8=ACCEPT", "UNSELECT", "UIDPLUS", "SPECIAL-USE", "NAMESPACE", "MOVE", "IDLE" })
                Assert.Contains(capability, capabilityText);
            Assert.DoesNotContain("LOGINDISABLED", capabilityText);
            Assert.Equal("A0a BAD STARTTLS is not available on an Implicit TLS port\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A0a STARTTLS", "A0a", stopping.Token)));
            Assert.Equal("A0b BAD LIST is not valid in the NotAuthenticated state\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A0b LIST \"\" \"*\"", "A0b", stopping.Token)));
            Assert.Equal("A0c NO [AUTHENTICATIONFAILED] LOGIN failed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A0c LOGIN {address} bad-password", "A0c", stopping.Token)));
            var badPasswordSasl = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{address}\0bad-password"));
            Assert.Equal("A0c1 NO [AUTHENTICATIONFAILED] Authentication failed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A0c1 AUTHENTICATE PLAIN {badPasswordSasl}", "A0c1", stopping.Token)));
            var mismatchedAuthz = Convert.ToBase64String(Encoding.UTF8.GetBytes($"other@invalid\0{address}\0test-password"));
            Assert.Equal("A0d NO [AUTHORIZATIONFAILED] Cannot act as the requested authorization identity\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A0d AUTHENTICATE PLAIN {mismatchedAuthz}", "A0d", stopping.Token)));
            Assert.Equal("A0d1 BAD Invalid AUTHENTICATE base64 data\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A0d1 AUTHENTICATE PLAIN !!!", "A0d1", stopping.Token)));
            Assert.Equal("A0d2 NO [AUTHENTICATIONFAILED] Authentication failed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A0d2 AUTHENTICATE PLAIN =", "A0d2", stopping.Token)));
            await tls.WriteAsync("A0e AUTHENTICATE PLAIN\r\n"u8.ToArray(), stopping.Token);
            Assert.Equal("+", Encoding.ASCII.GetString(await reader.ReadLineAsync(stopping.Token) ?? []));
            await tls.WriteAsync("*\r\n"u8.ToArray(), stopping.Token);
            Assert.Equal("A0e BAD AUTHENTICATE cancelled\r\n", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A0e", stopping.Token)));
            await tls.WriteAsync("A0f NOOP\r\nA0g NOOP\r\n"u8.ToArray(), stopping.Token);
            Assert.Equal("A0f OK NOOP completed\r\n", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A0f", stopping.Token)));
            Assert.Equal("A0g OK NOOP completed\r\n", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A0g", stopping.Token)));

            var sasl = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{address}\0test-password"));
            var authResult = await SendAndReadAsync(tls, reader, $"A1 AUTHENTICATE PLAIN {sasl}", "A1", stopping.Token);
            Assert.Contains("A1 OK", Encoding.UTF8.GetString(authResult));
            Assert.Equal(capabilityText.Replace("A0", "A1aCAP"), Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A1aCAP CAPABILITY", "A1aCAP", stopping.Token)));
            Assert.Equal("A1a0 NO [NONEXISTENT] Mailbox does not exist\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A1a0 DELETE MissingFolder", "A1a0", stopping.Token)));
            Assert.Equal("A1a1 NO [NONEXISTENT] Mailbox does not exist\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A1a1 RENAME MissingFolder OtherFolder", "A1a1", stopping.Token)));
            var namespaceResponse = await SendAndReadAsync(tls, reader, "A1aa NAMESPACE", "A1aa", stopping.Token);
            Assert.Equal("* NAMESPACE ((\"\" \"/\")) NIL ((\"Shared/\" \"/\"))\r\nA1aa OK NAMESPACE completed\r\n", Encoding.UTF8.GetString(namespaceResponse));
            var utf8BeforeEnable = await SendAndReadAsync(tls, reader, "A1a LIST \"\" \"Café\"", "A1a", stopping.Token);
            Assert.Contains("BAD Invalid mailbox name encoding", Encoding.UTF8.GetString(utf8BeforeEnable));
            var utf7Folders = await SendAndReadAsync(tls, reader, "A1b LIST \"\" \"Caf&AOk-\"", "A1b", stopping.Token);
            Assert.Contains("Caf&AOk-", Encoding.UTF8.GetString(utf7Folders));
            var enabled = await SendAndReadAsync(tls, reader, "A1c ENABLE UTF8=ACCEPT", "A1c", stopping.Token);
            Assert.Contains("* ENABLED UTF8=ACCEPT", Encoding.UTF8.GetString(enabled));
            var utf8Folders = await SendAndReadAsync(tls, reader, "A1d LIST \"\" \"Café\"", "A1d", stopping.Token);
            Assert.Contains("Café", Encoding.UTF8.GetString(utf8Folders));
            var utf8Create = await SendAndReadAsync(tls, reader, "A1d1 CREATE \"Café Folder\"", "A1d1", stopping.Token);
            Assert.Contains("A1d1 OK", Encoding.UTF8.GetString(utf8Create));
            var utf8QuotedList = await SendAndReadAsync(tls, reader, "A1d2 LIST \"\" \"Café Folder\"", "A1d2", stopping.Token);
            Assert.Contains("\"Café Folder\"", Encoding.UTF8.GetString(utf8QuotedList));
            var specials = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A1e LIST \"\" \"*\"", "A1e", stopping.Token));
            Assert.Contains("\\Sent", specials);
            Assert.Contains("\\Drafts", specials);
            Assert.Contains("\\Trash", specials);

            await SendAndReadAsync(tls, reader, "A2 LIST \"\" \"*\"", "A2", stopping.Token);
            var listPattern = await SendAndReadAsync(tls, reader, "A2b LIST \"\" \"IN%\"", "A2b", stopping.Token);
            Assert.Contains("INBOX", Encoding.UTF8.GetString(listPattern));
            var hierarchy = await SendAndReadAsync(tls, reader, "A2bb LIST \"\" \"Projects\"", "A2bb", stopping.Token);
            Assert.Contains("\\HasChildren", Encoding.UTF8.GetString(hierarchy));
            var hierarchyChild = await SendAndReadAsync(tls, reader, "A2bc LIST \"\" \"Projects/%\"", "A2bc", stopping.Token);
            Assert.Contains("Projects/2026", Encoding.UTF8.GetString(hierarchyChild));
            var sharedParentList = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2bd LIST \"\" \"Shared\"", "A2bd", stopping.Token));
            Assert.Contains("* LIST (\\Noselect \\HasChildren) \"/\" \"Shared\"", sharedParentList);
            var sharedAddressList = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2be LIST \"\" \"Shared/%\"", "A2be", stopping.Token));
            Assert.Contains($"* LIST (\\Noselect \\HasChildren) \"/\" \"Shared/{salesAddress}\"", sharedAddressList);
            Assert.DoesNotContain("/INBOX", sharedAddressList);
            var sharedReferenceList = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2bf LIST \"Shared/\" \"%/INBOX\"", "A2bf", stopping.Token));
            Assert.Contains($"Shared/{salesAddress}/INBOX", sharedReferenceList);
            var delimiterList = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2bg LIST \"\" \"\"", "A2bg", stopping.Token));
            Assert.Contains("* LIST (\\Noselect) \"/\" \"\"", delimiterList);
            var lsub = await SendAndReadAsync(tls, reader, "A2c LSUB \"\" \"*\"", "A2c", stopping.Token);
            var defaultSubscriptions = Encoding.UTF8.GetString(lsub);
            Assert.Contains("* LSUB", defaultSubscriptions);
            Assert.Contains("INBOX", defaultSubscriptions);
            Assert.Contains("Sent", defaultSubscriptions);
            Assert.Contains("Drafts", defaultSubscriptions);
            Assert.Contains("Trash", defaultSubscriptions);
            Assert.Contains("Archive", defaultSubscriptions);
            Assert.Contains("Junk", defaultSubscriptions);
            Assert.Contains("* LSUB (\\Noselect \\HasChildren) \"/\" \"Shared\"", defaultSubscriptions);
            Assert.Contains($"Shared/{salesAddress}/INBOX", defaultSubscriptions);
            Assert.Contains($"Shared/{readAddress}/INBOX", defaultSubscriptions);
            Assert.Contains("A2d OK UNSUBSCRIBE completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2d UNSUBSCRIBE INBOX", "A2d", stopping.Token)));
            var optedOut = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2e LSUB \"\" \"INBOX\"", "A2e", stopping.Token));
            Assert.DoesNotContain("* LSUB", optedOut);
            Assert.Contains("A2f OK SUBSCRIBE completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2f SUBSCRIBE INBOX", "A2f", stopping.Token)));
            Assert.Contains("* LSUB", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2g LSUB \"\" \"INBOX\"", "A2g", stopping.Token)));
            Assert.Contains("A2h OK CREATE completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2h CREATE FreshFolder", "A2h", stopping.Token)));
            Assert.Contains("A2h1 OK [READ-WRITE] SELECT completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2h1 SELECT FreshFolder", "A2h1", stopping.Token)));
            Assert.Equal("A2h2 NO [INUSE] Mailbox is selected\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2h2 DELETE FreshFolder", "A2h2", stopping.Token)));
            Assert.Equal("A2h3 NO [INUSE] Mailbox is selected\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2h3 RENAME FreshFolder OtherFolder", "A2h3", stopping.Token)));
            Assert.Contains("A2h4 OK UNSELECT completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2h4 UNSELECT", "A2h4", stopping.Token)));
            Assert.Contains("FreshFolder", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i LSUB \"\" \"FreshFolder\"", "A2i", stopping.Token)));
            var duplicateCreate = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i1 CREATE FreshFolder", "A2i1", stopping.Token));
            Assert.Contains("NO [ALREADYEXISTS]", duplicateCreate);
            var missingParentCreate = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i2 CREATE MissingParent/Child", "A2i2", stopping.Token));
            Assert.Contains("NO [TRYCREATE]", missingParentCreate);
            Assert.Contains("A2i3 OK CREATE completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i3 CREATE MutationParent", "A2i3", stopping.Token)));
            uint mutationParentValidity;
            await using (var folderCheck = new AppDbContext(dbOptions))
                mutationParentValidity = await folderCheck.Folders.Where(folder => folder.TenantId == tenantId && folder.MailboxId == mailboxId && folder.Name == "MutationParent").Select(folder => folder.UidValidity).SingleAsync();
            Assert.Contains("A2i4 OK CREATE completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i4 CREATE MutationParent/Child", "A2i4", stopping.Token)));
            Assert.Contains("NO Mailbox has inferior hierarchical names", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i4a DELETE MutationParent", "A2i4a", stopping.Token)));
            Assert.Contains("MutationParent/Child", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i4b LIST \"\" \"MutationParent/%\"", "A2i4b", stopping.Token)));
            Assert.Contains("A2i5 OK RENAME completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i5 RENAME MutationParent/Child MutationParent/Renamed", "A2i5", stopping.Token)));
            Assert.Contains("MutationParent/Renamed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i6 LIST \"\" \"MutationParent/%\"", "A2i6", stopping.Token)));
            Assert.Contains("A2i7 OK DELETE completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i7 DELETE MutationParent/Renamed", "A2i7", stopping.Token)));
            Assert.Contains("NO [ALREADYEXISTS]", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i7a RENAME FreshFolder MutationParent", "A2i7a", stopping.Token)));
            Assert.Contains("NO", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i7b RENAME Sent RenamedSent", "A2i7b", stopping.Token)));
            Assert.Contains("A2i7c OK DELETE completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i7c DELETE MutationParent", "A2i7c", stopping.Token)));
            Assert.Contains("A2i7d OK CREATE completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i7d CREATE MutationParent", "A2i7d", stopping.Token)));
            await using (var folderCheck = new AppDbContext(dbOptions))
            {
                var replacementValidity = await folderCheck.Folders.Where(folder => folder.TenantId == tenantId && folder.MailboxId == mailboxId && folder.Name == "MutationParent").Select(folder => folder.UidValidity).SingleAsync();
                Assert.NotEqual(mutationParentValidity, replacementValidity);
            }
            Assert.Contains("NO [CANNOT] INBOX", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i8 DELETE INBOX", "A2i8", stopping.Token)));
            Assert.Contains("NO [CANNOT]", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2i9 DELETE Trash", "A2i9", stopping.Token)));
            await using (var delegateChange = new AppDbContext(dbOptions))
            {
                var grant = await delegateChange.MailboxDelegates.SingleAsync(item => item.TenantId == tenantId && item.UserId == userId && item.MailboxId == readMailboxId);
                delegateChange.MailboxDelegates.Remove(grant);
                await delegateChange.SaveChangesAsync();
            }
            var revokedLsub = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2j LSUB \"\" \"Shared/*\"", "A2j", stopping.Token));
            Assert.DoesNotContain(readAddress, revokedLsub);
            var revokedList = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2j1 LIST \"\" \"Shared/*\"", "A2j1", stopping.Token));
            Assert.DoesNotContain(readAddress, revokedList);
            await using (var delegateChange = new AppDbContext(dbOptions))
            {
                delegateChange.MailboxDelegates.Add(new MailboxDelegate { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = readMailboxId, UserId = userId, AccessLevel = "read" });
                await delegateChange.SaveChangesAsync();
            }
            var newDelegationLsub = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2k LSUB \"\" \"Shared/*\"", "A2k", stopping.Token));
            Assert.Contains($"Shared/{readAddress}/INBOX", newDelegationLsub);
            var deniedSubscribe = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A2k1 SUBSCRIBE Shared/{sendOnlyAddress}/INBOX", "A2k1", stopping.Token));
            var missingSubscribe = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2k2 SUBSCRIBE Shared/missing@test.invalid/INBOX", "A2k2", stopping.Token));
            Assert.Equal("NO Mailbox does not exist", ResponseTail(deniedSubscribe));
            Assert.Equal(ResponseTail(deniedSubscribe), ResponseTail(missingSubscribe));
            var status = await SendAndReadAsync(tls, reader, "A2l STATUS INBOX (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN)", "A2l", stopping.Token);
            Assert.Contains("MESSAGES 2", Encoding.UTF8.GetString(status));
            Assert.Contains("RECENT 2", Encoding.UTF8.GetString(status));
            var statusRepeat = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A2l1 STATUS INBOX (RECENT)", "A2l1", stopping.Token));
            Assert.Contains("RECENT 2", statusRepeat);
            var examine = await SendAndReadAsync(tls, reader, "A3 EXAMINE INBOX", "A3", stopping.Token);
            Assert.Contains("[READ-ONLY]", Encoding.UTF8.GetString(examine));
            Assert.Contains("* 2 EXISTS", Encoding.UTF8.GetString(examine));
            Assert.Contains("* 2 RECENT", Encoding.UTF8.GetString(examine));
            Assert.Contains("* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)", Encoding.UTF8.GetString(examine));
            await using (var recentCheck = new AppDbContext(dbOptions))
                Assert.Equal(2, await recentCheck.Messages.CountAsync(message => message.TenantId == tenantId && message.MailboxId == mailboxId && message.FolderId == folderId && message.IsRecent));
            Assert.Contains("[UNSEEN 1]", Encoding.UTF8.GetString(examine));
            Assert.Contains("PERMANENTFLAGS ()", Encoding.UTF8.GetString(examine));
            Assert.Contains("A3a OK CHECK completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3a CHECK", "A3a", stopping.Token)));
            Assert.Contains("Body π", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3aa FETCH 1 (BODY[])", "A3aa", stopping.Token)));
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3ab SEARCH UNSEEN", "A3ab", stopping.Token)));
            var readonlyStore = await SendAndReadAsync(tls, reader, "A3b STORE 1 +FLAGS (\\Seen)", "A3b", stopping.Token);
            Assert.Contains("[READ-ONLY]", Encoding.UTF8.GetString(readonlyStore));
            var readonlyExpunge = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3b1 EXPUNGE", "A3b1", stopping.Token));
            Assert.Contains("NO [READ-ONLY]", readonlyExpunge);
            var refusedAppend = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: readonly append\r\n\r\nbody");
            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A3b2 APPEND INBOX {{{refusedAppend.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(refusedAppend, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            Assert.Contains("NO [READ-ONLY]", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A3b2", stopping.Token)));
            var writableReselect = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3c SELECT INBOX", "A3c", stopping.Token));
            Assert.Contains("* 2 EXISTS", writableReselect);
            Assert.Contains("* 2 RECENT", writableReselect);
            Assert.Contains("[UIDVALIDITY", writableReselect);
            Assert.Contains("[UIDNEXT", writableReselect);
            Assert.Contains("[UNSEEN", writableReselect);
            Assert.Contains("[PERMANENTFLAGS", writableReselect);
            await using (var recentCheck = new AppDbContext(dbOptions))
                Assert.Equal(0, await recentCheck.Messages.CountAsync(message => message.TenantId == tenantId && message.MailboxId == mailboxId && message.FolderId == folderId && message.IsRecent));
            var peekText = await SendAndReadAsync(tls, reader, "A3d FETCH 1 (BODY.PEEK[1])", "A3d", stopping.Token);
            Assert.Contains("Body π", Encoding.UTF8.GetString(peekText));
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3e SEARCH UNSEEN", "A3e", stopping.Token)));
            var bodyText = await SendAndReadAsync(tls, reader, "A3f FETCH 1 (BODY[1])", "A3f", stopping.Token);
            Assert.Contains("Body π", Encoding.UTF8.GetString(bodyText));
            Assert.Contains("* SEARCH 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A3g SEARCH SEEN", "A3g", stopping.Token)));
            var search = await SendAndReadAsync(tls, reader, "A4 SEARCH ALL", "A4", stopping.Token);
            Assert.Equal("* SEARCH 1 2\r\nA4 OK SEARCH completed\r\n", Encoding.UTF8.GetString(search));
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(search));
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A4a SEARCH OR UNSEEN FROM sender@test.invalid", "A4a", stopping.Token)));
            Assert.Contains("* SEARCH 1 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A4b UID SEARCH 1:*", "A4b", stopping.Token)));
            var dateCriterion = DateTime.UtcNow.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
            var searchCriteria = new[] { "UNSEEN", "UNANSWERED", "UNFLAGGED", "UNDELETED", "UNDRAFT", "OLD", "FROM sender@test.invalid", $"TO {address}", "CC copy@test.invalid", "BCC blind@test.invalid", "SUBJECT Café", "BODY searchable", "TEXT Café", "HEADER X-Test value", "SINCE 01-Jan-2020", "BEFORE 01-Jan-2030", $"ON {dateCriterion}", "SENTSINCE 01-Jan-2020", "SENTBEFORE 01-Jan-2030", $"SENTON {dateCriterion}", "LARGER 1", "SMALLER 999999", "CHARSET UTF-8 SUBJECT Café", "NOT SEEN", "OR SEEN UNSEEN", "(UNSEEN SUBJECT Café)" };
            // Message 1 is \Seen after A3f and is the only one with Cc/Bcc recipients.
            var expectedOverrides = new Dictionary<string, string>
            {
                ["UNSEEN"] = "* SEARCH 2", ["NOT SEEN"] = "* SEARCH 2", ["(UNSEEN SUBJECT Café)"] = "* SEARCH 2",
                ["OLD"] = "* SEARCH", ["NEW"] = "* SEARCH 2",
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
            foreach (var (criterion, expected) in new[] { ("SEEN", "* SEARCH 1\r\n"), ("ANSWERED", "* SEARCH\r\n"), ("FLAGGED", "* SEARCH\r\n"), ("DELETED", "* SEARCH\r\n"), ("DRAFT", "* SEARCH\r\n"), ("NEW", "* SEARCH 2\r\n"), ("RECENT", "* SEARCH 1 2\r\n"), ("1:*", "* SEARCH 1 2\r\n") })
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
            var bulkCopy = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5e UID COPY 1:2 Archive", "A5e", stopping.Token));
            Assert.Contains("[COPYUID 17 1,2 1,2]", bulkCopy);
            Assert.Contains("MESSAGES 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5f STATUS Archive (MESSAGES)", "A5f", stopping.Token)));
            var archiveSelect = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5g SELECT Archive", "A5g", stopping.Token));
            Assert.Contains("PERMANENTFLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft \\*)", archiveSelect);
            Assert.Contains("NO Mailbox does not exist", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5g1 SELECT MissingFolder", "A5g1", stopping.Token)));
            Assert.Contains("BAD FETCH is not valid in the Authenticated state", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5g2 FETCH 1 (UID)", "A5g2", stopping.Token)));
            Assert.Contains("A5g3 OK [READ-WRITE] SELECT completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5g3 SELECT Archive", "A5g3", stopping.Token)));
            var storeFlags = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5h STORE 1 FLAGS (\\Seen \\Answered \\Flagged \\Deleted \\Draft custom)", "A5h", stopping.Token));
            Assert.Contains("* 1 FETCH (FLAGS", storeFlags);
            Assert.Contains("custom", storeFlags);
            var silentStore = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5i UID STORE 1 -FLAGS.SILENT (\\Deleted)", "A5i", stopping.Token));
            Assert.DoesNotContain("* 1 FETCH", silentStore);
            var storedFlags = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5j UID FETCH 1 (FLAGS)", "A5j", stopping.Token));
            Assert.Contains("\\Seen", storedFlags);
            Assert.Contains("\\Answered", storedFlags);
            Assert.Contains("custom", storedFlags);
            Assert.DoesNotContain("\\Deleted", storedFlags);
            Assert.Contains("* SEARCH 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5j1 SEARCH KEYWORD custom", "A5j1", stopping.Token)));
            Assert.Contains("* SEARCH 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5j2 SEARCH UNKEYWORD custom", "A5j2", stopping.Token)));
            Assert.Contains("BAD Invalid STORE flag", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5k STORE 1 +FLAGS (\\Recent)", "A5k", stopping.Token)));
            var inboxReselect = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5l SELECT INBOX", "A5l", stopping.Token));
            Assert.Contains("[UIDVALIDITY 9]", inboxReselect);
            Assert.Contains("[UIDNEXT 3]", inboxReselect);
            var fullBodyFetch = await SendAndReadAsync(tls, reader, "A5m FETCH 1 (RFC822.SIZE BODY.PEEK[])", "A5m", stopping.Token);
            Assert.Contains($"RFC822.SIZE {raw.Length}", Encoding.UTF8.GetString(fullBodyFetch));
            Assert.Contains($"BODY[] {{{raw.Length}}}", Encoding.UTF8.GetString(fullBodyFetch));
            Assert.True(ContainsBytes(fullBodyFetch, raw));
            var fieldsFetch = await SendAndReadAsync(tls, reader, "A5n FETCH 1 (BODY.PEEK[HEADER.FIELDS (SUBJECT X-Folded)])", "A5n", stopping.Token);
            Assert.True(ContainsBytes(fieldsFetch, Encoding.UTF8.GetBytes("Subject: Café\r\nX-Folded: one\r\n two\r\n\r\n")));
            var fieldsNotFetch = await SendAndReadAsync(tls, reader, "A5o FETCH 1 (BODY.PEEK[HEADER.FIELDS.NOT (SUBJECT)])", "A5o", stopping.Token);
            Assert.True(ContainsBytes(fieldsNotFetch, Encoding.UTF8.GetBytes("From: sender@test.invalid")));
            Assert.False(ContainsBytes(fieldsNotFetch, Encoding.UTF8.GetBytes("Subject: Café")));
            var mimeFetch = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A5p FETCH 1 (BODY.PEEK[1.MIME])", "A5p", stopping.Token));
            Assert.Contains("Content-Type: multipart/alternative", mimeFetch);
            var textFetch = await SendAndReadAsync(tls, reader, "A5q FETCH 1 (RFC822.TEXT BODY.PEEK[TEXT])", "A5q", stopping.Token);
            Assert.True(ContainsBytes(textFetch, Encoding.UTF8.GetBytes("Body π\r\n")));
            var partialFetch = await SendAndReadAsync(tls, reader, "A5r FETCH 1 (BODY.PEEK[]<0.20>)", "A5r", stopping.Token);
            Assert.Contains("BODY[]<0> {20}", Encoding.UTF8.GetString(partialFetch));
            Assert.True(ContainsBytes(partialFetch, raw[..20]));
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
            var subscribedNames = await mailKit.GetFoldersAsync(mailKit.PersonalNamespaces[0], MailKit.StatusItems.None, true, stopping.Token);
            Assert.Contains(subscribedNames, folder => folder.FullName.Equals("INBOX", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(subscribedNames, folder => folder.FullName.Equals($"Shared/{salesAddress}/INBOX", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(subscribedNames, folder => folder.FullName.Equals($"Shared/{readAddress}/INBOX", StringComparison.OrdinalIgnoreCase));
            var personalNamespaceRoot = mailKit.GetFolder(mailKit.PersonalNamespaces[0]);
            var subscribedSubfolders = personalNamespaceRoot.GetSubfolders(subscribedOnly: true);
            Assert.Contains(subscribedSubfolders, folder => folder.FullName.Equals("Projects", StringComparison.OrdinalIgnoreCase));
            var sharedRoot = mailKit.GetFolder("Shared");
            var subscribedSharedMailboxes = sharedRoot.GetSubfolders(subscribedOnly: true);
            Assert.Contains(subscribedSharedMailboxes, folder => folder.FullName.Equals($"Shared/{salesAddress}", StringComparison.OrdinalIgnoreCase));
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

            var copied = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6b UID COPY 2 Trash", "A6b", stopping.Token));
            Assert.Contains("[COPYUID 15 2 1]", copied);
            await SendAndReadAsync(tls, reader, "A6c STORE 1 +FLAGS (\\Deleted)", "A6c", stopping.Token);
            var unselect = await SendAndReadAsync(tls, reader, "A6d UNSELECT", "A6d", stopping.Token);
            Assert.Contains("UNSELECT completed", Encoding.UTF8.GetString(unselect));
            var stillDeleted = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6e SELECT INBOX", "A6e", stopping.Token));
            Assert.Contains("1 EXISTS", stillDeleted);
            Assert.Contains("\\Deleted", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6f FETCH 1 (UID FLAGS)", "A6f", stopping.Token)));
            var uidExpunge = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6g UID EXPUNGE 2", "A6g", stopping.Token));
            Assert.Contains("* 1 EXPUNGE", uidExpunge);

            var appended = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: plus literal\r\n\r\nbody");
            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A6h APPEND INBOX (\\Seen \\Flagged) \"12-Feb-2024 09:30:00 +0000\" {{{appended.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(appended, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            var literalPlus = Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A6h", stopping.Token));
            Assert.Contains("[APPENDUID 9 3]", literalPlus);
            var appendedMetadata = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6h0 FETCH 1 (UID FLAGS INTERNALDATE)", "A6h0", stopping.Token));
            Assert.Contains("\\Seen", appendedMetadata);
            Assert.Contains("\\Flagged", appendedMetadata);
            Assert.Contains("12-Feb-2024", appendedMetadata);
            var missingAppend = Encoding.UTF8.GetBytes($"A6h1 APPEND MissingFolder {{{appended.Length}+}}\r\n");
            await tls.WriteAsync(missingAppend, stopping.Token);
            await tls.WriteAsync(appended, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            Assert.Contains("NO [TRYCREATE]", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A6h1", stopping.Token)));
            Assert.Contains("NO [TRYCREATE]", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6h2 COPY 1 MissingFolder", "A6h2", stopping.Token)));
            await SendAndReadAsync(tls, reader, "A6i SELECT INBOX", "A6i", stopping.Token);
            await SendAndReadAsync(tls, reader, "A6j STORE 1 +FLAGS (\\Deleted)", "A6j", stopping.Token);
            await SendAndReadAsync(tls, reader, "A6k UNSELECT", "A6k", stopping.Token);
            await SendAndReadAsync(tls, reader, "A6l EXAMINE INBOX", "A6l", stopping.Token);
            var readOnlyClose = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6m CLOSE", "A6m", stopping.Token));
            Assert.Equal("A6m OK CLOSE completed\r\n", readOnlyClose);
            Assert.Contains("MESSAGES 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6n STATUS INBOX (MESSAGES)", "A6n", stopping.Token)));
            await SendAndReadAsync(tls, reader, "A6o SELECT INBOX", "A6o", stopping.Token);
            var close = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6p CLOSE", "A6p", stopping.Token));
            Assert.Equal("A6p OK CLOSE completed\r\n", close);
            Assert.Contains("MESSAGES 0", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A6q STATUS INBOX (MESSAGES)", "A6q", stopping.Token)));
            await SendAndReadAsync(tls, reader, "A6r SELECT INBOX", "A6r", stopping.Token);

            var sharedList = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A7 LIST \"\" \"Shared/*\"", "A7", stopping.Token));
            Assert.Contains($"Shared/{salesAddress}/INBOX", sharedList);
            Assert.Contains($"Shared/{readAddress}/INBOX", sharedList);
            Assert.DoesNotContain(sendOnlyAddress, sharedList, StringComparison.OrdinalIgnoreCase);

            var sendAsDenied = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7a SELECT Shared/{sendOnlyAddress}/INBOX", "A7a", stopping.Token));
            var nonExistentDenied = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A7b SELECT Shared/missing@test.invalid/INBOX", "A7b", stopping.Token));
            Assert.Contains("NO Mailbox does not exist", sendAsDenied);
            Assert.Contains("NO Mailbox does not exist", nonExistentDenied);
            Assert.Equal(ResponseTail(sendAsDenied), ResponseTail(nonExistentDenied));
            foreach (var (tag, path) in new[]
            {
                ("A7c", $"Shared/{sendOnlyAddress.ToUpperInvariant()}/INBOX"),
                ("A7d", $"Shared/../{address}/INBOX"),
                ("A7e", $"Shared/{otherTenantAddress}/INBOX"),
                ("A7f", $"Shared/{sendOnlyAddress}/INBOX/")
            })
            {
                var denied = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"{tag} SELECT {path}", tag, stopping.Token));
                Assert.Equal(ResponseTail(nonExistentDenied), ResponseTail(denied));
            }
            var deniedStatus = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7g STATUS Shared/{sendOnlyAddress}/INBOX (MESSAGES)", "A7g", stopping.Token));
            Assert.Contains("NO Mailbox does not exist", deniedStatus);
            var deniedList = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7h LIST \"\" \"Shared/{sendOnlyAddress}/*\"", "A7h", stopping.Token));
            Assert.DoesNotContain(sendOnlyAddress, deniedList, StringComparison.OrdinalIgnoreCase);
            var sendAsExamine = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7i EXAMINE Shared/{sendOnlyAddress}/INBOX", "A7i", stopping.Token));
            Assert.Equal(ResponseTail(sendAsExamine), ResponseTail(nonExistentDenied));
            Assert.Contains("A7i1 OK [READ-WRITE] SELECT completed", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A7i1 SELECT INBOX", "A7i1", stopping.Token)));
            var deniedCopy = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7j COPY 1 Shared/{sendOnlyAddress}/INBOX", "A7j", stopping.Token));
            var missingCopy = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A7k COPY 1 Shared/missing@test.invalid/INBOX", "A7k", stopping.Token));
            Assert.Equal("NO Mailbox does not exist", ResponseTail(deniedCopy));
            Assert.Equal("NO Mailbox does not exist", ResponseTail(missingCopy));
            foreach (var (tag, path) in new[] { ("A7l", $"Shared/{sendOnlyAddress}/NewFolder"), ("A7m", "Shared/missing@test.invalid/NewFolder") })
                Assert.Equal("NO Mailbox does not exist", ResponseTail(Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"{tag} CREATE {path}", tag, stopping.Token))));
            foreach (var (tag, path) in new[] { ("A7n", $"Shared/{sendOnlyAddress}/INBOX"), ("A7o", "Shared/missing@test.invalid/INBOX") })
            {
                var rename = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"{tag} RENAME {path} Renamed", tag, stopping.Token));
                var deleteTag = tag == "A7n" ? "A7p" : "A7q";
                var delete = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"{deleteTag} DELETE {path}", deleteTag, stopping.Token));
                Assert.Equal("NO Mailbox does not exist", ResponseTail(rename));
                Assert.Equal("NO Mailbox does not exist", ResponseTail(delete));
            }
            foreach (var (tag, destination) in new[] { ("A7r", sendOnlyAddress), ("A7s", "missing@test.invalid") })
            {
                var literal = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {destination}\r\nSubject: denied target\r\n\r\nbody");
                await tls.WriteAsync(Encoding.UTF8.GetBytes($"{tag} APPEND Shared/{destination}/INBOX {{{literal.Length}+}}\r\n"), stopping.Token);
                await tls.WriteAsync(literal, stopping.Token);
                await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                var appendResult = Encoding.UTF8.GetString(await ReadResponseAsync(reader, tag, stopping.Token));
                Assert.Equal("NO Mailbox does not exist", ResponseTail(appendResult));
                Assert.DoesNotContain("APPENDUID", appendResult);
            }
            await using (var verify = new AppDbContext(dbOptions))
                Assert.False(await verify.Messages.AnyAsync(message => message.TenantId == tenantId && message.MailboxId == sendOnlyMailboxId));

            var moveSource = Encoding.UTF8.GetBytes("From: sender@test.invalid\r\nTo: user@test.invalid\r\nSubject: MOVE source\r\n\r\nmove body");
            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A7t APPEND INBOX {{{moveSource.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(moveSource, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            var appendForMove = Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A7t", stopping.Token));
            var sourceUid = uint.Parse(System.Text.RegularExpressions.Regex.Match(appendForMove, @"APPENDUID 9 (\d+)").Groups[1].Value);
            var unauthorizedMoveTarget = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7t1 UID MOVE {sourceUid} Shared/{sendOnlyAddress}/INBOX", "A7t1", stopping.Token));
            Assert.Equal("NO Mailbox does not exist", ResponseTail(unauthorizedMoveTarget));
            Assert.Contains("NO [TRYCREATE]", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7t2 UID MOVE {sourceUid} MissingFolder", "A7t2", stopping.Token)));
            var personalToShared = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7u UID MOVE {sourceUid} Shared/{salesAddress}/INBOX", "A7u", stopping.Token));
            Assert.Contains($"* OK [COPYUID 21 {sourceUid} 2] MOVE completed", personalToShared);
            Assert.Contains("* 1 EXPUNGE", personalToShared);
            Assert.EndsWith("A7u OK MOVE completed\r\n", personalToShared);
            Assert.Contains("MESSAGES 0", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A7v STATUS INBOX (MESSAGES)", "A7v", stopping.Token)));
            Assert.Contains("MESSAGES 2", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7w STATUS Shared/{salesAddress}/INBOX (MESSAGES)", "A7w", stopping.Token)));

            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A7x APPEND INBOX {{{moveSource.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(moveSource, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            var appendForDeniedMove = Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A7x", stopping.Token));
            var deniedSourceUid = uint.Parse(System.Text.RegularExpressions.Regex.Match(appendForDeniedMove, @"APPENDUID 9 (\d+)").Groups[1].Value);
            var readOnlyMoveTarget = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7y UID MOVE {deniedSourceUid} Shared/{readAddress}/Trash", "A7y", stopping.Token));
            Assert.Equal("NO Mailbox does not exist", ResponseTail(readOnlyMoveTarget));
            await SendAndReadAsync(tls, reader, $"A7z UID STORE {deniedSourceUid} +FLAGS (\\Deleted)", "A7z", stopping.Token);
            await SendAndReadAsync(tls, reader, "A7z1 EXPUNGE", "A7z1", stopping.Token);

            await SendAndReadAsync(tls, reader, $"A7z2 SELECT Shared/{salesAddress}/INBOX", "A7z2", stopping.Token);
            var sharedToPersonal = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A7z3 UID MOVE 2 INBOX", "A7z3", stopping.Token));
            Assert.Contains("* OK [COPYUID 9 2 6] MOVE completed", sharedToPersonal);
            Assert.Contains("* 2 EXPUNGE", sharedToPersonal);
            Assert.EndsWith("A7z3 OK MOVE completed\r\n", sharedToPersonal);
            Assert.Contains("MESSAGES 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A7z4 STATUS Shared/{salesAddress}/INBOX (MESSAGES)", "A7z4", stopping.Token)));
            await SendAndReadAsync(tls, reader, "A7z5 SELECT INBOX", "A7z5", stopping.Token);
            await SendAndReadAsync(tls, reader, "A7z6 STORE 1 +FLAGS (\\Deleted)", "A7z6", stopping.Token);
            await SendAndReadAsync(tls, reader, "A7z7 EXPUNGE", "A7z7", stopping.Token);

            var readSelect = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A8 EXAMINE Shared/{readAddress}/INBOX", "A8", stopping.Token));
            Assert.Contains("[READ-ONLY]", readSelect);
            Assert.Contains("1 EXISTS", readSelect);
            var readSelectAgain = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A8aa SELECT Shared/{readAddress}/INBOX", "A8aa", stopping.Token));
            Assert.Contains("[READ-ONLY]", readSelectAgain);
            Assert.Contains("MESSAGES 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A8ab STATUS Shared/{readAddress}/INBOX (MESSAGES)", "A8ab", stopping.Token)));
            Assert.Contains("* SEARCH 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A8a SEARCH ALL", "A8a", stopping.Token)));
            Assert.Contains("UID 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A8b UID FETCH 1 (UID FLAGS RFC822.SIZE)", "A8b", stopping.Token)));
            var readStore = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A8c STORE 1 +FLAGS (\\Flagged)", "A8c", stopping.Token));
            Assert.Contains("NO", readStore);
            var readCopy = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A8d COPY 1 Shared/{readAddress}/Trash", "A8d", stopping.Token));
            Assert.Contains("NO", readCopy);
            var readExpunge = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A8e EXPUNGE", "A8e", stopping.Token));
            Assert.Contains("NO", readExpunge);
            var readCreate = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A8f CREATE Shared/{readAddress}/NewFolder", "A8f", stopping.Token));
            Assert.Contains("NO", readCreate);
            Assert.Contains("NO", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A8ff RENAME Shared/{readAddress}/INBOX Renamed", "A8ff", stopping.Token)));
            Assert.Contains("NO", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A8fff DELETE Shared/{readAddress}/INBOX", "A8fff", stopping.Token)));
            var readAppend = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {readAddress}\r\nSubject: denied append\r\n\r\nbody");
            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A8g APPEND Shared/{readAddress}/INBOX {{{readAppend.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(readAppend, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            Assert.Contains("NO", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A8g", stopping.Token)));
            await using (var verify = new AppDbContext(dbOptions))
            {
                var readMessage = await verify.Messages.SingleAsync(message => message.TenantId == tenantId && message.MailboxId == readMailboxId && message.FolderId == readInboxId);
                Assert.Empty(readMessage.Flags);
                Assert.False(await verify.Messages.AnyAsync(message => message.TenantId == tenantId && message.MailboxId == readMailboxId && message.FolderId == readTrashId));
            }

            var sharedSelect = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A9 SELECT Shared/{salesAddress}/INBOX", "A9", stopping.Token));
            Assert.Contains("[READ-WRITE]", sharedSelect);
            Assert.Contains("MESSAGES 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A9a STATUS Shared/{salesAddress}/INBOX (MESSAGES UIDNEXT)", "A9a", stopping.Token)));
            Assert.Contains("UID 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9b FETCH 1 (UID RFC822.SIZE)", "A9b", stopping.Token)));
            Assert.Contains("UID 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9c UID FETCH 1 (UID FLAGS)", "A9c", stopping.Token)));
            Assert.Contains("* SEARCH 1", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9d SEARCH SUBJECT \"Sales shared\"", "A9d", stopping.Token)));
            Assert.Contains("OK", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9e STORE 1 +FLAGS (\\Flagged)", "A9e", stopping.Token)));
            await using (var verify = new AppDbContext(dbOptions))
            {
                var sharedMessage = await verify.Messages.SingleAsync(message => message.TenantId == tenantId && message.MailboxId == salesMailboxId && message.FolderId == salesInboxId && message.Uid == 1);
                Assert.Contains("\\Flagged", sharedMessage.Flags);
                Assert.Equal(salesMailboxId, sharedMessage.MailboxId);
            }
            var sharedCopy = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A9f UID COPY 1 Shared/{salesAddress}/Trash", "A9f", stopping.Token));
            Assert.Contains("[COPYUID 22 1 1]", sharedCopy);
            await using (var verify = new AppDbContext(dbOptions))
            {
                var copiedMessage = await verify.Messages.SingleAsync(message => message.TenantId == tenantId && message.MailboxId == salesMailboxId && message.FolderId == salesTrashId && message.Uid == 1);
                Assert.Equal(salesMailboxId, copiedMessage.MailboxId);
            }

            var salesAppend = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {salesAddress}\r\nSubject: sales append\r\n\r\nbody");
            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A9g APPEND Shared/{salesAddress}/INBOX {{{salesAppend.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(salesAppend, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            Assert.Contains("[APPENDUID 21 3]", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A9g", stopping.Token)));
            await using (var verify = new AppDbContext(dbOptions))
            {
                var appendedMessage = await verify.Messages.SingleAsync(message => message.TenantId == tenantId && message.MailboxId == salesMailboxId && message.FolderId == salesInboxId && message.Uid == 3);
                Assert.Equal(salesMailboxId, appendedMessage.MailboxId);
            }
            await SendAndReadAsync(tls, reader, "A9h STORE 2 +FLAGS (\\Deleted)", "A9h", stopping.Token);
            Assert.Contains("* 2 EXPUNGE", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9i UID EXPUNGE 3", "A9i", stopping.Token)));
            await using (var verify = new AppDbContext(dbOptions))
                Assert.False(await verify.Messages.AnyAsync(message => message.TenantId == tenantId && message.MailboxId == salesMailboxId && message.FolderId == salesInboxId && message.Uid == 3));

            using (var sharedMailKit = new MailKit.Net.Imap.ImapClient { ServerCertificateValidationCallback = (_, _, _, _) => true })
            {
                await sharedMailKit.ConnectAsync("127.0.0.1", service.BoundPort, MailKit.Security.SecureSocketOptions.SslOnConnect, stopping.Token);
                await sharedMailKit.AuthenticateAsync(address, "test-password", stopping.Token);
                var sharedFolders = await sharedMailKit.GetFoldersAsync(sharedMailKit.PersonalNamespaces[0], MailKit.StatusItems.None, false, stopping.Token);
                Assert.Contains(sharedFolders, folder => folder.FullName.Equals($"Shared/{salesAddress}/INBOX", StringComparison.OrdinalIgnoreCase));
                var salesFolder = sharedMailKit.GetFolder($"Shared/{salesAddress}/INBOX");
                await salesFolder.OpenAsync(MailKit.FolderAccess.ReadOnly, stopping.Token);
                Assert.Single(await salesFolder.SearchAsync(MailKit.Search.SearchQuery.All, stopping.Token));
                await sharedMailKit.DisconnectAsync(true, stopping.Token);
            }

            using var secondClient = new TcpClient();
            await secondClient.ConnectAsync("127.0.0.1", service.BoundPort, stopping.Token);
            using var secondTls = new SslStream(secondClient.GetStream(), leaveInnerStreamOpen: false, (_, _, _, _) => true);
            await secondTls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" }, stopping.Token);
            var secondReader = new ImapConnectionReader(secondTls);
            await secondReader.ReadLineAsync(stopping.Token);
            Assert.Contains("OK", Encoding.UTF8.GetString(await SendAndReadAsync(secondTls, secondReader, $"B1 LOGIN {secondAddress} test-password", "B1", stopping.Token)));
            Assert.Contains("OK", Encoding.UTF8.GetString(await SendAndReadAsync(secondTls, secondReader, $"B2 SELECT Shared/{salesAddress}/INBOX", "B2", stopping.Token)));
            var concurrentMoveSource = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {salesAddress}\r\nSubject: concurrent move\r\n\r\nbody");
            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A9race APPEND Shared/{salesAddress}/INBOX {{{concurrentMoveSource.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(concurrentMoveSource, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            Assert.Contains("[APPENDUID 21 4]", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A9race", stopping.Token)));
            await Task.WhenAll(
                tls.WriteAsync("A9move1 UID MOVE 4 INBOX\r\n"u8.ToArray(), stopping.Token).AsTask(),
                secondTls.WriteAsync("B6 UID MOVE 4 INBOX\r\n"u8.ToArray(), stopping.Token).AsTask());
            var concurrentMoveResponses = await Task.WhenAll(
                ReadResponseAsync(reader, "A9move1", stopping.Token),
                ReadResponseAsync(secondReader, "B6", stopping.Token));
            Assert.Single(concurrentMoveResponses, response => Encoding.UTF8.GetString(response).Contains(" OK MOVE completed", StringComparison.Ordinal));
            Assert.Single(concurrentMoveResponses, response => Encoding.UTF8.GetString(response).Contains(" NO MOVE failed", StringComparison.Ordinal));
            Assert.Contains("UID 1", Encoding.UTF8.GetString(await SendAndReadAsync(secondTls, secondReader, "B3 FETCH 1 (UID FLAGS)", "B3", stopping.Token)));
            Assert.Contains("OK", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9m STORE 1 +FLAGS (\\Answered)", "A9m", stopping.Token)));
            var visibleFlag = Encoding.UTF8.GetString(await SendAndReadAsync(secondTls, secondReader, "B4 NOOP", "B4", stopping.Token));
            Assert.Contains("B4 OK", visibleFlag);
            Assert.Contains("* 1 FETCH (UID 1 FLAGS (", visibleFlag);
            Assert.Contains("\\Answered", visibleFlag);
            Assert.Contains("\\Answered", Encoding.UTF8.GetString(await SendAndReadAsync(secondTls, secondReader, "B5 FETCH 1 (UID FLAGS)", "B5", stopping.Token)));
            await secondTls.WriteAsync("B7 IDLE\r\n"u8.ToArray(), stopping.Token);
            Assert.Equal("+ idling", Encoding.ASCII.GetString(await secondReader.ReadLineAsync(stopping.Token) ?? []));
            var idleAppend = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {salesAddress}\r\nSubject: idle push\r\n\r\nbody");
            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A9idle APPEND Shared/{salesAddress}/INBOX {{{idleAppend.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(idleAppend, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            var idleAppendResult = Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A9idle", stopping.Token));
            var idleUid = uint.Parse(System.Text.RegularExpressions.Regex.Match(idleAppendResult, @"APPENDUID 21 (\d+)").Groups[1].Value);
            Assert.Contains("2 EXISTS", await ReadIdleUntilAsync(secondReader, line => line.Contains("2 EXISTS", StringComparison.Ordinal), stopping.Token));
            await SendAndReadAsync(tls, reader, "A9idle1 STORE 2 +FLAGS (\\Flagged)", "A9idle1", stopping.Token);
            Assert.Contains($"UID {idleUid}", await ReadIdleUntilAsync(secondReader, line => line.Contains("FETCH", StringComparison.Ordinal), stopping.Token));
            await SendAndReadAsync(tls, reader, "A9idle2 STORE 2 +FLAGS (\\Deleted)", "A9idle2", stopping.Token);
            await SendAndReadAsync(tls, reader, "A9idle3 EXPUNGE", "A9idle3", stopping.Token);
            Assert.Contains("* 2 EXPUNGE", await ReadIdleUntilAsync(secondReader, line => line.Contains("EXPUNGE", StringComparison.Ordinal), stopping.Token));
            await secondTls.WriteAsync("DONE\r\n"u8.ToArray(), stopping.Token);
            Assert.Equal("B7 OK IDLE terminated", Encoding.ASCII.GetString(await secondReader.ReadLineAsync(stopping.Token) ?? []));
            await SendAndReadAsync(tls, reader, "A9n STORE 1 +FLAGS (\\Deleted)", "A9n", stopping.Token);
            Assert.Contains("* 1 EXPUNGE", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9o EXPUNGE", "A9o", stopping.Token)));
            var observedExpunge = Encoding.UTF8.GetString(await SendAndReadAsync(secondTls, secondReader, "B6 NOOP", "B6", stopping.Token));
            Assert.Contains("* 1 EXPUNGE", observedExpunge);
            var commandCompletionAppend = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {salesAddress}\r\nSubject: command completion exists\r\n\r\nbody");
            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A9exists APPEND Shared/{salesAddress}/INBOX {{{commandCompletionAppend.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(commandCompletionAppend, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            Assert.Contains("[APPENDUID 21", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A9exists", stopping.Token)));
            var commandCompletionExists = Encoding.UTF8.GetString(await SendAndReadAsync(secondTls, secondReader, "B9 NOOP", "B9", stopping.Token));
            Assert.Contains("* 1 EXISTS", commandCompletionExists);

            async Task<uint> AppendInParallelAsync(SslStream connectionStream, ImapConnectionReader responseReader, string tag)
            {
                var literal = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {salesAddress}\r\nSubject: concurrent {tag}\r\n\r\nbody");
                await connectionStream.WriteAsync(Encoding.UTF8.GetBytes($"{tag} APPEND Shared/{salesAddress}/INBOX {{{literal.Length}+}}\r\n"), stopping.Token);
                await connectionStream.WriteAsync(literal, stopping.Token);
                await connectionStream.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                var response = Encoding.UTF8.GetString(await ReadResponseAsync(responseReader, tag, stopping.Token));
                var match = System.Text.RegularExpressions.Regex.Match(response, @"\[APPENDUID 21 (\d+)\]");
                Assert.True(match.Success, response);
                return uint.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            var concurrentUids = await Task.WhenAll(
                AppendInParallelAsync(tls, reader, "A9p"),
                AppendInParallelAsync(secondTls, secondReader, "B7"));
            Assert.Equal(2, concurrentUids.Distinct().Count());
            await using (var verify = new AppDbContext(dbOptions))
            {
                var concurrentMessages = await verify.Messages
                    .Where(message => message.TenantId == tenantId && message.MailboxId == salesMailboxId && message.FolderId == salesInboxId && concurrentUids.Contains(message.Uid))
                    .Select(message => new { message.Uid, message.MailboxId })
                    .ToListAsync();
                Assert.Equal(2, concurrentMessages.Count);
                Assert.Equal(2, concurrentMessages.Select(message => message.Uid).Distinct().Count());
                Assert.All(concurrentMessages, message => Assert.Equal(salesMailboxId, message.MailboxId));
            }
            await secondTls.WriteAsync("B8 IDLE\r\n"u8.ToArray(), stopping.Token);
            Assert.Equal("+ idling", Encoding.ASCII.GetString(await secondReader.ReadLineAsync(stopping.Token) ?? []));
            await using (var revokeDelegate = new AppDbContext(dbOptions))
            {
                var row = await revokeDelegate.MailboxDelegates.SingleAsync(item => item.TenantId == tenantId && item.UserId == secondUserId && item.MailboxId == salesMailboxId);
                revokeDelegate.MailboxDelegates.Remove(row);
                await revokeDelegate.SaveChangesAsync();
            }
            Assert.Contains("* BYE Mailbox access revoked", await ReadIdleUntilAsync(secondReader, line => line.StartsWith("* BYE", StringComparison.Ordinal), stopping.Token));

            Assert.Contains("OK", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A9j CREATE Shared/{salesAddress}/NewFolder", "A9j", stopping.Token)));
            await using (var verify = new AppDbContext(dbOptions))
            {
                var createdFolder = await verify.Folders.SingleAsync(folder => folder.TenantId == tenantId && folder.MailboxId == salesMailboxId && folder.Name == "NewFolder");
                Assert.Equal(salesMailboxId, createdFolder.MailboxId);
            }
            uint createdFolderUidValidity;
            await using (var verify = new AppDbContext(dbOptions))
                createdFolderUidValidity = await verify.Folders.Where(folder => folder.TenantId == tenantId && folder.MailboxId == salesMailboxId && folder.Name == "NewFolder").Select(folder => folder.UidValidity).SingleAsync();
            var protectedSharedRename = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A9j1 RENAME Shared/{salesAddress}/Sent RenamedSent", "A9j1", stopping.Token));
            Assert.Contains("NO", protectedSharedRename);
            Assert.Contains("OK", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A9k RENAME Shared/{salesAddress}/NewFolder Renamed", "A9k", stopping.Token)));
            await using (var verify = new AppDbContext(dbOptions))
            {
                var renamedFolder = await verify.Folders.SingleAsync(folder => folder.TenantId == tenantId && folder.MailboxId == salesMailboxId && folder.Name == "Renamed");
                Assert.Equal(salesMailboxId, renamedFolder.MailboxId);
                Assert.Equal(createdFolderUidValidity, renamedFolder.UidValidity);
            }
            Assert.Contains("OK", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A9l DELETE Shared/{salesAddress}/Renamed", "A9l", stopping.Token)));
            await using (var verify = new AppDbContext(dbOptions))
                Assert.False(await verify.Folders.AnyAsync(folder => folder.TenantId == tenantId && folder.MailboxId == salesMailboxId && folder.Name == "Renamed"));

            await using (var revoke = new AppDbContext(dbOptions))
            {
                var delegateRow = await revoke.MailboxDelegates.SingleAsync(item => item.TenantId == tenantId && item.UserId == userId && item.MailboxId == salesMailboxId);
                revoke.MailboxDelegates.Remove(delegateRow);
                await revoke.SaveChangesAsync();
            }
            var revokedFetch = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9q FETCH 1 (UID RFC822.SIZE)", "A9q", stopping.Token));
            Assert.Contains("NO Mailbox does not exist", revokedFetch);
            Assert.DoesNotContain("* 1 FETCH", revokedFetch);
            Assert.Contains("NO Mailbox does not exist", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9r STORE 1 +FLAGS (\\Seen)", "A9r", stopping.Token)));
            Assert.Contains("NO Mailbox does not exist", Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, $"A9s SELECT Shared/{salesAddress}/INBOX", "A9s", stopping.Token)));

            var renameInboxLiteral = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: rename inbox regression\r\n\r\nmove me");
            await tls.WriteAsync(Encoding.UTF8.GetBytes($"A9s1 APPEND INBOX {{{renameInboxLiteral.Length}+}}\r\n"), stopping.Token);
            await tls.WriteAsync(renameInboxLiteral, stopping.Token);
            await tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
            Assert.Contains("A9s1 OK", Encoding.UTF8.GetString(await ReadResponseAsync(reader, "A9s1", stopping.Token)));
            int inboxBeforeRename;
            await using (var before = new AppDbContext(dbOptions))
            {
                // Earlier steps (e.g. MOVE into INBOX) may leave messages there; RENAME INBOX must move all of them.
                inboxBeforeRename = await before.Messages.CountAsync(message => message.TenantId == tenantId && message.MailboxId == mailboxId && message.FolderId == folderId);
            }
            Assert.True(inboxBeforeRename >= 1);
            var inboxRename = Encoding.UTF8.GetString(await SendAndReadAsync(tls, reader, "A9t RENAME INBOX ImapRenamedInbox", "A9t", stopping.Token));
            Assert.Contains("A9t OK RENAME completed", inboxRename);
            await using (var verify = new AppDbContext(dbOptions))
            {
                var renamedInbox = await verify.Folders.SingleAsync(folder => folder.TenantId == tenantId && folder.MailboxId == mailboxId && folder.Name == "ImapRenamedInbox");
                var inboxCount = await verify.Messages.CountAsync(message => message.TenantId == tenantId && message.MailboxId == mailboxId && message.FolderId == folderId);
                var movedCount = await verify.Messages.CountAsync(message => message.TenantId == tenantId && message.MailboxId == mailboxId && message.FolderId == renamedInbox.Id);
                Assert.Equal(0, inboxCount);
                Assert.Equal(inboxBeforeRename, movedCount);
                Assert.NotEqual(9u, renamedInbox.UidValidity);
            }

            using (var timeoutClient = new TcpClient())
            {
                await timeoutClient.ConnectAsync("127.0.0.1", service.BoundPort, stopping.Token);
                using var timeoutTls = new SslStream(timeoutClient.GetStream(), leaveInnerStreamOpen: false, (_, _, _, _) => true);
                await timeoutTls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" }, stopping.Token);
                var timeoutReader = new ImapConnectionReader(timeoutTls);
                await timeoutReader.ReadLineAsync(stopping.Token);
                Assert.Contains("OK", Encoding.ASCII.GetString(await SendAndReadAsync(timeoutTls, timeoutReader, $"T1 LOGIN {address} test-password", "T1", stopping.Token)));
                Assert.Contains("OK", Encoding.ASCII.GetString(await SendAndReadAsync(timeoutTls, timeoutReader, "T2 SELECT ImapRenamedInbox", "T2", stopping.Token)));
                await timeoutTls.WriteAsync("T3 IDLE\r\n"u8.ToArray(), stopping.Token);
                Assert.Equal("+ idling", Encoding.ASCII.GetString(await timeoutReader.ReadLineAsync(stopping.Token) ?? []));
                Assert.Equal("* BYE IDLE timeout", await ReadIdleUntilAsync(timeoutReader, line => line.StartsWith("* BYE IDLE timeout", StringComparison.Ordinal), stopping.Token));
            }

            using (var rev1Client = new TcpClient())
            {
                await rev1Client.ConnectAsync("127.0.0.1", service.BoundPort, stopping.Token);
                using var rev1Tls = new SslStream(rev1Client.GetStream(), leaveInnerStreamOpen: false, (_, _, _, _) => true);
                await rev1Tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" }, stopping.Token);
                var rev1Reader = new ImapConnectionReader(rev1Tls);
                await rev1Reader.ReadLineAsync(stopping.Token);
                await SendAndReadAsync(rev1Tls, rev1Reader, $"R1 LOGIN {address} test-password", "R1", stopping.Token);
                await SendAndReadAsync(rev1Tls, rev1Reader, "R2 SELECT FreshFolder", "R2", stopping.Token);
                Assert.Equal("R2a BAD LIST-EXTENDED requires IMAP4rev2\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R2a LIST (SUBSCRIBED) \"\" \"*\"", "R2a", stopping.Token)));
                Assert.Equal("R2a1 BAD BINARY requires IMAP4rev2\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R2a1 FETCH 1 (BINARY[1])", "R2a1", stopping.Token)));
                Assert.Equal("* SEARCH\r\nR3 OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R3 SEARCH ALL", "R3", stopping.Token)));
                Assert.Equal("R3a BAD SEARCHRES requires IMAP4rev2\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R3a SEARCH $", "R3a", stopping.Token)));
                var rev1Literal = Encoding.UTF8.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: rev1 synchronizing literal\r\n\r\nbody");
                await rev1Tls.WriteAsync(Encoding.ASCII.GetBytes($"R3b APPEND INBOX {{{rev1Literal.Length}}}\r\n"), stopping.Token);
                Assert.Equal("+ Ready for literal data", Encoding.ASCII.GetString(await rev1Reader.ReadLineAsync(stopping.Token) ?? []));
                await rev1Tls.WriteAsync(rev1Literal, stopping.Token);
                await rev1Tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                Assert.StartsWith("R3b OK", Encoding.UTF8.GetString(await ReadResponseAsync(rev1Reader, "R3b", stopping.Token)));
                await SendAndReadAsync(rev1Tls, rev1Reader, "R3c SELECT INBOX", "R3c", stopping.Token);
                Assert.Equal("* 1 FETCH (FLAGS ($Forwarded $MDNSent $Junk $Phishing \\Recent))\r\nR3d OK STORE completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R3d STORE 1 FLAGS ($Forwarded $MDNSent $Junk $Phishing)", "R3d", stopping.Token)));
                Assert.Equal("* SEARCH 1\r\nR3e OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R3e SEARCH KEYWORD $Forwarded", "R3e", stopping.Token)));
                Assert.Equal("* SEARCH 1\r\nR3f OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R3f SEARCH KEYWORD $MDNSent", "R3f", stopping.Token)));
                Assert.Equal("* SEARCH 1\r\nR3g OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R3g SEARCH KEYWORD $Junk", "R3g", stopping.Token)));
                Assert.Equal("* SEARCH 1\r\nR3h OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R3h SEARCH KEYWORD $Phishing", "R3h", stopping.Token)));
                await SendAndReadAsync(rev1Tls, rev1Reader, "R3i STORE 1 -FLAGS ($Junk)", "R3i", stopping.Token);
                await SendAndReadAsync(rev1Tls, rev1Reader, "R3j STORE 1 +FLAGS ($NotJunk)", "R3j", stopping.Token);
                Assert.Equal("* SEARCH 1\r\nR3k OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R3k SEARCH KEYWORD $NotJunk", "R3k", stopping.Token)));
                Assert.Equal("R3l NO [TOOBIG] Literal too large\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev1Tls, rev1Reader, "R3l APPEND INBOX {8193}", "R3l", stopping.Token)));
            }
            using (var rev2Client = new TcpClient())
            {
                await rev2Client.ConnectAsync("127.0.0.1", service.BoundPort, stopping.Token);
                using var rev2Tls = new SslStream(rev2Client.GetStream(), leaveInnerStreamOpen: false, (_, _, _, _) => true);
                await rev2Tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" }, stopping.Token);
                var rev2Reader = new ImapConnectionReader(rev2Tls);
                await rev2Reader.ReadLineAsync(stopping.Token);
                await rev2Tls.WriteAsync("R4 AUTHENTICATE PLAIN\r\n"u8.ToArray(), stopping.Token);
                Assert.Equal("+", Encoding.ASCII.GetString(await rev2Reader.ReadLineAsync(stopping.Token) ?? []));
                var continuationSasl = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{address}\0test-password"));
                await rev2Tls.WriteAsync(Encoding.ASCII.GetBytes(continuationSasl + "\r\n"), stopping.Token);
                Assert.Equal("R4 OK AUTHENTICATE completed\r\n", Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R4", stopping.Token)));
                Assert.Equal("* ID NIL\r\nR4id OK ID completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R4id ID NIL", "R4id", stopping.Token)));
                Assert.Equal("* ID NIL\r\nR4id2 OK ID completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R4id2 ID (\"name\" \"client\" \"version\" NIL)", "R4id2", stopping.Token)));
                Assert.Equal("* ENABLED IMAP4REV2\r\nR5 OK ENABLE completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5 ENABLE IMAP4rev2", "R5", stopping.Token)));
                Assert.Equal(capabilityText.Replace("A0", "R5b"), Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5b CAPABILITY", "R5b", stopping.Token)));
                Assert.Equal("* ENABLED\r\nR5a OK ENABLE completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5a ENABLE X-UNSUPPORTED", "R5a", stopping.Token)));
                Assert.Equal("* ENABLED UTF8=ACCEPT IMAP4REV2\r\nR5a1 OK ENABLE completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5a1 ENABLE UTF8=ACCEPT IMAP4rev2", "R5a1", stopping.Token)));
                var normalizedCreate = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5c CREATE \"Rev2 Cafe\u0301\"", "R5c", stopping.Token));
                Assert.Contains("[OLDNAME \"Rev2 Cafe\u0301\" \"Rev2 Café\"]", normalizedCreate);
                Assert.Contains("R5c OK", normalizedCreate);
                Assert.Equal("R5d NO Invalid Net-Unicode mailbox name\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5d CREATE \"bad\uFDD0name\"", "R5d", stopping.Token)));
                Assert.Contains("R5f OK CREATE completed", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5f CREATE \"Rename Source\"", "R5f", stopping.Token)));
                Assert.Contains("R5g OK RENAME completed", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5g RENAME \"Rename Source\" \"Rename Café\"", "R5g", stopping.Token)));
                Assert.Equal("R5h NO Invalid Net-Unicode mailbox name\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5h RENAME \"Rename Café\" \"bad\uFDD0name\"", "R5h", stopping.Token)));
                var normalizedSelect = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R5e SELECT \"Rev2 Café\"", "R5e", stopping.Token));
                Assert.Contains("* LIST (\\HasNoChildren) \"/\" \"Rev2 Café\"", normalizedSelect);
                List<uint> renamedInboxUids;
                await using (var rev2Db = new AppDbContext(dbOptions))
                {
                    var renamedInboxId = await rev2Db.Folders.Where(folder => folder.TenantId == tenantId && folder.MailboxId == mailboxId && folder.Name == "ImapRenamedInbox").Select(folder => folder.Id).SingleAsync(stopping.Token);
                    renamedInboxUids = await rev2Db.Messages.Where(message => message.TenantId == tenantId && message.MailboxId == mailboxId && message.FolderId == renamedInboxId).OrderBy(message => message.Uid).Select(message => message.Uid).ToListAsync(stopping.Token);
                }
                var rev2Select = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R6 SELECT FreshFolder", "R6", stopping.Token));
                Assert.DoesNotContain(" RECENT", rev2Select);
                Assert.Contains("* LIST (\\HasNoChildren)", rev2Select);
                Assert.Contains("R6a OK NOOP completed", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R6a CHECK", "R6a", stopping.Token)));
                Assert.Equal("R6b BAD LSUB is not available in IMAP4rev2\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R6b LSUB \"\" \"*\"", "R6b", stopping.Token)));
                Assert.Contains("* OK [PERMANENTFLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft \\*)] Permanent flags", rev2Select);
                var rev2Examine = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R6e EXAMINE FreshFolder", "R6e", stopping.Token));
                Assert.Contains("* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)", rev2Examine);
                Assert.Contains("* 0 EXISTS", rev2Examine);
                Assert.Contains("* LIST (\\HasNoChildren) \"/\" \"FreshFolder\"", rev2Examine);
                Assert.Contains("* OK [PERMANENTFLAGS ()]", rev2Examine);
                Assert.Contains("[UIDNEXT ", rev2Examine);
                Assert.Contains("[UIDVALIDITY ", rev2Examine);
                Assert.Contains("R6e OK [READ-ONLY] EXAMINE completed", rev2Examine);
                Assert.Equal("* ESEARCH (TAG \"R7\")\r\nR7 OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R7 SEARCH ALL", "R7", stopping.Token)));
                Assert.Equal("* ESEARCH (TAG \"R8\") UID\r\nR8 OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R8 UID SEARCH ALL", "R8", stopping.Token)));
                Assert.Contains("BAD RECENT is not available in IMAP4rev2", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R9 STATUS FreshFolder (RECENT)", "R9", stopping.Token)));
                Assert.Contains("* OK [CLOSED] Previous mailbox closed", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R10 SELECT ImapRenamedInbox", "R10", stopping.Token)));
                var sequenceSet = renamedInboxUids.Count == 0 ? string.Empty : " ALL " + FormatTestSequenceSet(Enumerable.Range(1, renamedInboxUids.Count).Select(value => (uint)value));
                var uidSet = renamedInboxUids.Count == 0 ? string.Empty : " ALL " + FormatTestSequenceSet(renamedInboxUids);
                Assert.Equal($"* ESEARCH (TAG \"R11\"){sequenceSet}\r\nR11 OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R11 SEARCH ALL", "R11", stopping.Token)));
                Assert.Equal($"* ESEARCH (TAG \"R12\") UID{uidSet}\r\nR12 OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R12 UID SEARCH ALL", "R12", stopping.Token)));
                var expectedReturn = $"* ESEARCH (TAG \"R13\") MIN 1 MAX {renamedInboxUids.Count} ALL {FormatTestSequenceSet(Enumerable.Range(1, renamedInboxUids.Count).Select(value => (uint)value))} COUNT {renamedInboxUids.Count}\r\nR13 OK SEARCH completed\r\n";
                Assert.Equal(expectedReturn, Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R13 SEARCH RETURN (MIN MAX ALL COUNT) ALL", "R13", stopping.Token)));
                Assert.Equal("R14 OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R14 SEARCH RETURN (SAVE) ALL", "R14", stopping.Token)));
                using var expectedFetch = new MemoryStream();
                for (var index = 0; index < renamedInboxUids.Count; index++)
                {
                    var line = Encoding.UTF8.GetBytes($"* {index + 1} FETCH (UID {renamedInboxUids[index]})\r\n");
                    expectedFetch.Write(line);
                }
                expectedFetch.Write("R15 OK FETCH completed\r\n"u8);
                Assert.Equal(expectedFetch.ToArray(), await SendAndReadAsync(rev2Tls, rev2Reader, "R15 FETCH $ (UID)", "R15", stopping.Token));
                var savedSearchResponse = $"* ESEARCH (TAG \"R16\"){sequenceSet}\r\nR16 OK SEARCH completed\r\n";
                Assert.Equal(savedSearchResponse, Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R16 SEARCH $", "R16", stopping.Token)));
                Assert.Equal($"* ESEARCH (TAG \"R17\") UID{uidSet}\r\nR17 OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R17 UID SEARCH $", "R17", stopping.Token)));
                using var expectedUidFetch = new MemoryStream();
                for (var index = 0; index < renamedInboxUids.Count; index++) expectedUidFetch.Write(Encoding.UTF8.GetBytes($"* {index + 1} FETCH (UID {renamedInboxUids[index]})\r\n"));
                expectedUidFetch.Write("R18 OK FETCH completed\r\n"u8);
                Assert.Equal(expectedUidFetch.ToArray(), await SendAndReadAsync(rev2Tls, rev2Reader, "R18 UID FETCH $ (UID)", "R18", stopping.Token));
                Assert.Equal($"* ESEARCH (TAG \"R19\") MIN 1\r\nR19 OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R19 SEARCH RETURN (MIN SAVE) ALL", "R19", stopping.Token)));
                Assert.Equal($"* 1 FETCH (UID {renamedInboxUids[0]})\r\nR20 OK FETCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R20 FETCH $ (UID)", "R20", stopping.Token)));
                Assert.Equal("* STATUS \"FreshFolder\" (MESSAGES 0 DELETED 0 SIZE 0)\r\nR21 OK STATUS completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R21 STATUS FreshFolder (MESSAGES SIZE DELETED)", "R21", stopping.Token)));
                Assert.Equal("* LIST (\\HasNoChildren \\Subscribed) \"/\" \"FreshFolder\"\r\n* STATUS \"FreshFolder\" (MESSAGES 0 SIZE 0)\r\nR22 OK LIST completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22 LIST (SUBSCRIBED) \"\" \"FreshFolder\" RETURN (SUBSCRIBED CHILDREN SPECIAL-USE STATUS (MESSAGES SIZE))", "R22", stopping.Token)));
                Assert.Equal("* LIST (\\NonExistent \\Noselect \\HasNoChildren) \"/\" \"FreshFolder/NotCreated\"\r\nR22a OK LIST completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22a LIST \"\" \"FreshFolder/NotCreated\" RETURN (CHILDREN)", "R22a", stopping.Token)));
                Assert.Contains("R22b OK CREATE completed", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22b CREATE ListParent", "R22b", stopping.Token)));
                Assert.Contains("R22c OK CREATE completed", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22c CREATE ListParent/ListChild", "R22c", stopping.Token)));
                Assert.Contains("R22d OK UNSUBSCRIBE completed", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22d UNSUBSCRIBE ListParent", "R22d", stopping.Token)));
                Assert.Equal("* LIST (\\HasChildren) \"/\" \"ListParent\"\r\nR22e OK LIST completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22e LIST \"\" \"ListParent\"", "R22e", stopping.Token)));
                Assert.Equal("* LIST (\\HasChildren) \"/\" \"ListParent\"\r\n* STATUS \"ListParent\" (MESSAGES 0 DELETED 0 SIZE 0)\r\nR22f OK LIST completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22f LIST \"\" \"ListParent\" RETURN (SUBSCRIBED CHILDREN STATUS (MESSAGES DELETED SIZE))", "R22f", stopping.Token)));
                Assert.Equal("R22g OK LIST completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22g LIST (SUBSCRIBED) \"\" \"ListParent\"", "R22g", stopping.Token)));
                Assert.Equal("* LIST (\\Noselect \\HasChildren) \"/\" \"ListParent\" (\"CHILDINFO\" (\"SUBSCRIBED\"))\r\nR22h OK LIST completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22h LIST (SUBSCRIBED RECURSIVEMATCH) \"\" \"ListParent\"", "R22h", stopping.Token)));
                Assert.Equal("* LIST (\\NonExistent \\Noselect \\HasNoChildren) \"/\" \"ListParent/Missing\"\r\nR22i OK LIST completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22i LIST \"\" \"ListParent/Missing\" RETURN (CHILDREN)", "R22i", stopping.Token)));
                Assert.Equal("* LIST (\\HasNoChildren \\Subscribed) \"/\" \"ListParent/ListChild\"\r\nR22j OK LIST completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R22j LIST (SUBSCRIBED) \"\" \"ListParent/%\" RETURN (SUBSCRIBED CHILDREN)", "R22j", stopping.Token)));
                await SendAndReadAsync(rev2Tls, rev2Reader, "R23 SELECT ImapRenamedInbox", "R23", stopping.Token);
                Assert.Equal("* 1 FETCH (FLAGS ($Forwarded $MDNSent $Junk $Phishing))\r\nR23a OK STORE completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R23a STORE 1 FLAGS ($Forwarded $MDNSent $Junk $Phishing)", "R23a", stopping.Token)));
                Assert.Equal("* ESEARCH (TAG \"R23b\") ALL 1\r\nR23b OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R23b SEARCH RETURN (ALL) KEYWORD $Forwarded", "R23b", stopping.Token)));
                Assert.Equal("* ESEARCH (TAG \"R23c\") ALL 1\r\nR23c OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R23c SEARCH RETURN (ALL) KEYWORD $MDNSent", "R23c", stopping.Token)));
                Assert.Equal("* ESEARCH (TAG \"R23d\") ALL 1\r\nR23d OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R23d SEARCH RETURN (ALL) KEYWORD $Junk", "R23d", stopping.Token)));
                Assert.Equal("* ESEARCH (TAG \"R23e\") ALL 1\r\nR23e OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R23e SEARCH RETURN (ALL) KEYWORD $Phishing", "R23e", stopping.Token)));
                await SendAndReadAsync(rev2Tls, rev2Reader, "R23f STORE 1 -FLAGS ($Junk)", "R23f", stopping.Token);
                await SendAndReadAsync(rev2Tls, rev2Reader, "R23g STORE 1 +FLAGS ($NotJunk)", "R23g", stopping.Token);
                Assert.Equal("* ESEARCH (TAG \"R23h\") ALL 1\r\nR23h OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R23h SEARCH RETURN (ALL) KEYWORD $NotJunk", "R23h", stopping.Token)));
                Assert.Equal("R23i OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R23i UID SEARCH RETURN (SAVE) ALL", "R23i", stopping.Token)));
                Assert.Equal($"* ESEARCH (TAG \"R23j\") UID ALL {uidSet.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()}\r\nR23j OK SEARCH completed\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R23j UID SEARCH $", "R23j", stopping.Token)));
                var binaryFetch = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R24 FETCH 1 (BINARY[1] BINARY.SIZE[1] BINARY.PEEK[1])", "R24", stopping.Token));
                Assert.Contains("BINARY[1] {", binaryFetch);
                Assert.Contains("BINARY.SIZE[1] ", binaryFetch);
                Assert.Contains("BINARY[1] {", binaryFetch);
                var binarySize = System.Text.RegularExpressions.Regex.Match(binaryFetch, @"BINARY\.SIZE\[1\] (\d+)");
                var binaryLiteral = System.Text.RegularExpressions.Regex.Match(binaryFetch, @"BINARY\[1\] \{(\d+)\}");
                Assert.True(binarySize.Success, binaryFetch);
                Assert.True(binaryLiteral.Success, binaryFetch);
                Assert.Equal(binarySize.Groups[1].Value, binaryLiteral.Groups[1].Value);
                var binaryPartial = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R24a FETCH 1 (BINARY[1]<1.2> BINARY.SIZE[1])", "R24a", stopping.Token));
                Assert.Contains("BINARY[1]<1> {2}", binaryPartial);
                Assert.Contains($"BINARY.SIZE[1] {binarySize.Groups[1].Value}", binaryPartial);
                var binarySizePartial = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R24aa FETCH 1 (BINARY[1]<0.2> BINARY.SIZE[1])", "R24aa", stopping.Token));
                Assert.Contains("BINARY[1]<0> {2}", binarySizePartial);
                Assert.Contains($"BINARY.SIZE[1] {binarySize.Groups[1].Value}", binarySizePartial);
                var emptyBinary = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R24b FETCH 1 (BINARY[] BINARY.SIZE[])", "R24b", stopping.Token));
                var emptyBinarySize = System.Text.RegularExpressions.Regex.Match(emptyBinary, @"BINARY\.SIZE\[\] (\d+)");
                var emptyBinaryLength = System.Text.RegularExpressions.Regex.Match(emptyBinary, @"BINARY\[\] \{(\d+)\}");
                Assert.True(emptyBinarySize.Success && emptyBinaryLength.Success, emptyBinary);
                Assert.Equal(emptyBinarySize.Groups[1].Value, emptyBinaryLength.Groups[1].Value);
                Assert.Contains("R24b OK FETCH completed", emptyBinary);
                Assert.Equal("R24c BAD Invalid FETCH data items\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R24c FETCH 1 (BINARY.SIZE[1]<0.1>)", "R24c", stopping.Token)));
                var beyondBinary = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R24d FETCH 1 (BINARY[1]<99.2> BINARY.SIZE[1])", "R24d", stopping.Token));
                Assert.Contains("BINARY[1]<99> {0}", beyondBinary);
                Assert.Contains($"BINARY.SIZE[1] {binarySize.Groups[1].Value}", beyondBinary);
                var literalPrefix = Encoding.ASCII.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: rev2 literal minus\r\n\r\n");
                var literal4096 = literalPrefix.Concat(Encoding.ASCII.GetBytes(new string('x', 4096 - literalPrefix.Length))).ToArray();
                Assert.Equal(4096, literal4096.Length);
                await rev2Tls.WriteAsync(Encoding.ASCII.GetBytes($"R25 APPEND ImapRenamedInbox {{{literal4096.Length}}}\r\n"), stopping.Token);
                await rev2Tls.WriteAsync(literal4096, stopping.Token);
                await rev2Tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                Assert.StartsWith("R25 OK", Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R25", stopping.Token)));
                var literal4097 = literalPrefix.Concat(Encoding.ASCII.GetBytes(new string('y', 4097 - literalPrefix.Length))).ToArray();
                Assert.Equal(4097, literal4097.Length);
                await rev2Tls.WriteAsync(Encoding.ASCII.GetBytes($"R26 APPEND ImapRenamedInbox {{{literal4097.Length}}}\r\n"), stopping.Token);
                Assert.Equal("+ Ready for literal data", Encoding.ASCII.GetString(await rev2Reader.ReadLineAsync(stopping.Token) ?? []));
                await rev2Tls.WriteAsync(literal4097, stopping.Token);
                await rev2Tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                Assert.StartsWith("R26 OK", Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R26", stopping.Token)));
                var unknownCte = Encoding.ASCII.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: unknown CTE\r\nContent-Type: text/plain\r\nContent-Transfer-Encoding: x-unknown\r\n\r\nencoded-body");
                await rev2Tls.WriteAsync(Encoding.ASCII.GetBytes($"R27 APPEND FreshFolder {{{unknownCte.Length}+}}\r\n"), stopping.Token);
                await rev2Tls.WriteAsync(unknownCte, stopping.Token);
                await rev2Tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                Assert.StartsWith("R27 OK", Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R27", stopping.Token)));
                await SendAndReadAsync(rev2Tls, rev2Reader, "R28 SELECT FreshFolder", "R28", stopping.Token);
                Assert.Equal("R29 NO [UNKNOWN-CTE] Unknown content transfer encoding\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R29 FETCH 1 (BINARY[1])", "R29", stopping.Token)));
                Assert.Equal("R29a NO [UNKNOWN-CTE] Unknown content transfer encoding\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R29a FETCH 1 (BINARY.SIZE[1])", "R29a", stopping.Token)));
                Assert.Contains("R31 OK CREATE completed", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R31 CREATE StatusProbe", "R31", stopping.Token)));
                var deletedStatusMessage = Encoding.ASCII.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: deleted status probe\r\n\r\ndeleted\r\n");
                await rev2Tls.WriteAsync(Encoding.ASCII.GetBytes($"R32 APPEND StatusProbe (\\Deleted) {{{deletedStatusMessage.Length}+}}\r\n"), stopping.Token);
                await rev2Tls.WriteAsync(deletedStatusMessage, stopping.Token);
                await rev2Tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                var appendDeletedResponse = Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R32", stopping.Token));
                Assert.Contains("[APPENDUID ", appendDeletedResponse);
                var liveStatusMessage = Encoding.ASCII.GetBytes($"From: sender@test.invalid\r\nTo: {address}\r\nSubject: live status probe\r\n\r\nlive\r\n");
                await rev2Tls.WriteAsync(Encoding.ASCII.GetBytes($"R33 APPEND StatusProbe {{{liveStatusMessage.Length}+}}\r\n"), stopping.Token);
                await rev2Tls.WriteAsync(liveStatusMessage, stopping.Token);
                await rev2Tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                var appendLiveResponse = Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R33", stopping.Token));
                Assert.Contains("[APPENDUID ", appendLiveResponse);
                var failedAppendBody = "bad"u8.ToArray();
                await rev2Tls.WriteAsync("R34 APPEND MissingStatusProbe {3+}\r\n"u8.ToArray(), stopping.Token);
                await rev2Tls.WriteAsync(failedAppendBody, stopping.Token);
                await rev2Tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                var failedAppend = Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R34", stopping.Token));
                Assert.DoesNotContain("APPENDUID", failedAppend);
                var statusProbe = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R35 STATUS StatusProbe (MESSAGES DELETED SIZE)", "R35", stopping.Token));
                Assert.Contains($"MESSAGES 2 DELETED 1 SIZE {deletedStatusMessage.Length + liveStatusMessage.Length}", statusProbe);
                var listStatusProbe = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R36 LIST \"\" \"StatusProbe\" RETURN (STATUS (MESSAGES DELETED SIZE))", "R36", stopping.Token));
                Assert.Contains($"MESSAGES 2 DELETED 1 SIZE {deletedStatusMessage.Length + liveStatusMessage.Length}", listStatusProbe);
                Assert.Contains("R37 OK [READ-ONLY] EXAMINE completed", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R37 EXAMINE StatusProbe", "R37", stopping.Token)));
                await rev2Tls.WriteAsync("R38 APPEND StatusProbe {3+}\r\n"u8.ToArray(), stopping.Token);
                await rev2Tls.WriteAsync(failedAppendBody, stopping.Token);
                await rev2Tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                var readOnlyAppend = Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R38", stopping.Token));
                Assert.Contains("READ-ONLY", readOnlyAppend);
                Assert.DoesNotContain("APPENDUID", readOnlyAppend);
                await SendAndReadAsync(rev2Tls, rev2Reader, "R39 SELECT StatusProbe", "R39", stopping.Token);
                var failedCopy = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R40 UID COPY 2 MissingStatusProbe", "R40", stopping.Token));
                Assert.Contains("TRYCREATE", failedCopy);
                Assert.DoesNotContain("COPYUID", failedCopy);
                var rev2UidExpunge = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R41 UID EXPUNGE 1", "R41", stopping.Token));
                Assert.Contains("* 1 EXPUNGE", rev2UidExpunge);
                Assert.Contains("R41 OK EXPUNGE completed", rev2UidExpunge);
                var statusAfterUidExpunge = Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R42 STATUS StatusProbe (MESSAGES DELETED SIZE)", "R42", stopping.Token));
                Assert.Contains($"MESSAGES 1 DELETED 0 SIZE {liveStatusMessage.Length}", statusAfterUidExpunge);
                await rev2Tls.WriteAsync(Encoding.ASCII.GetBytes($"R43 APPEND StatusProbe {{{liveStatusMessage.Length}+}}\r\n"), stopping.Token);
                await rev2Tls.WriteAsync(liveStatusMessage, stopping.Token);
                await rev2Tls.WriteAsync("\r\n"u8.ToArray(), stopping.Token);
                var pipelineAppend = Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R43", stopping.Token));
                var pipelineUid = uint.Parse(System.Text.RegularExpressions.Regex.Match(pipelineAppend, @"APPENDUID \d+ (\d+)").Groups[1].Value);
                await SendAndReadAsync(rev2Tls, rev2Reader, "R44 NOOP", "R44", stopping.Token);
                await SendAndReadAsync(rev2Tls, rev2Reader, "R45 STORE 1 +FLAGS (\\Deleted)", "R45", stopping.Token);
                await rev2Tls.WriteAsync("R46 FETCH 2 (UID FLAGS)\r\nR47 STORE 2 +FLAGS (\\Flagged)\r\nR48 UID EXPUNGE 2\r\n"u8.ToArray(), stopping.Token);
                var pipelinedFetch = Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R46", stopping.Token));
                var pipelinedStore = Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R47", stopping.Token));
                var pipelinedExpunge = Encoding.UTF8.GetString(await ReadResponseAsync(rev2Reader, "R48", stopping.Token));
                Assert.Contains($"* 2 FETCH (UID {pipelineUid}", pipelinedFetch);
                Assert.Contains("* 2 FETCH (FLAGS", pipelinedStore);
                Assert.Contains("\\Flagged", pipelinedStore);
                Assert.Contains("* 1 EXPUNGE", pipelinedExpunge);
                Assert.Equal("R30 NO [LIMIT] Literal too large\r\n", Encoding.UTF8.GetString(await SendAndReadAsync(rev2Tls, rev2Reader, "R30 APPEND INBOX {8193}", "R30", stopping.Token)));
            }

            var logout = await SendAndReadAsync(tls, reader, "A6i LOGOUT", "A6i", stopping.Token);
            Assert.Contains("* BYE", Encoding.UTF8.GetString(logout));
        }
        finally
        {
            await service.StopAsync(CancellationTokenSource.CreateLinkedTokenSource(stopping.Token).Token);
            await using var cleanup = new AppDbContext(dbOptions);
            cleanup.Messages.RemoveRange(cleanup.Messages.Where(m => m.TenantId == tenantId));
            cleanup.MailboxDelegates.RemoveRange(cleanup.MailboxDelegates.Where(d => d.TenantId == tenantId));
            cleanup.Folders.RemoveRange(cleanup.Folders.Where(f => f.TenantId == tenantId));
            cleanup.Mailboxes.RemoveRange(cleanup.Mailboxes.Where(m => m.TenantId == tenantId));
            cleanup.Users.RemoveRange(cleanup.Users.Where(u => u.TenantId == tenantId));
            cleanup.Domains.RemoveRange(cleanup.Domains.Where(d => d.TenantId == tenantId));
            cleanup.Tenants.RemoveRange(cleanup.Tenants.Where(t => t.Id == tenantId));
            cleanup.Mailboxes.RemoveRange(cleanup.Mailboxes.Where(m => m.TenantId == otherTenantId));
            cleanup.Domains.RemoveRange(cleanup.Domains.Where(d => d.TenantId == otherTenantId));
            cleanup.Tenants.RemoveRange(cleanup.Tenants.Where(t => t.Id == otherTenantId));
            await cleanup.SaveChangesAsync();
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, recursive: true);
        }
    }

    private static async Task<byte[]> SendAndReadAsync(SslStream stream, ImapConnectionReader reader, string command, string tag, CancellationToken ct)
    {
        // IMAP responses must preserve UTF-8 message data on the wire.
        await stream.WriteAsync(Encoding.UTF8.GetBytes(command + "\r\n"), ct);
        return await ReadResponseAsync(reader, tag, ct);
    }

    private static async Task<byte[]> ReadResponseAsync(ImapConnectionReader reader, string tag, CancellationToken ct)
    {
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

    private static async Task<string> ReadIdleUntilAsync(ImapConnectionReader reader, Func<string, bool> predicate, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            var line = Encoding.ASCII.GetString(await reader.ReadLineAsync(timeout.Token) ?? throw new EndOfStreamException());
            if (predicate(line)) return line;
        }
    }

    private static string ResponseTail(string response)
    {
        var line = response.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[0];
        var firstSpace = line.IndexOf(' ');
        return firstSpace < 0 ? line : line[(firstSpace + 1)..];
    }

    private static bool ContainsBytes(byte[] source, byte[] expected)
    {
        for (var i = 0; i <= source.Length - expected.Length; i++) if (source.AsSpan(i, expected.Length).SequenceEqual(expected)) return true;
        return false;
    }

    private static string FormatTestSequenceSet(IEnumerable<uint> values)
    {
        var ordered = values.Distinct().Order().ToArray();
        if (ordered.Length == 0) return string.Empty;
        var ranges = new List<string>();
        var start = ordered[0];
        var previous = start;
        foreach (var value in ordered.Skip(1))
        {
            if (previous != uint.MaxValue && value == previous + 1) { previous = value; continue; }
            ranges.Add(start == previous ? start.ToString() : $"{start}:{previous}");
            start = previous = value;
        }
        ranges.Add(start == previous ? start.ToString() : $"{start}:{previous}");
        return string.Join(',', ranges);
    }

    private sealed class StubAuthenticator(Guid tenantId, Guid mailboxId, Guid userId) : IImapAuthenticator
    {
        private readonly Dictionary<string, (Guid MailboxId, Guid UserId)> _accounts = new(StringComparer.OrdinalIgnoreCase);

        public void AddAccount(string username, Guid accountMailboxId, Guid accountUserId)
            => _accounts[username] = (accountMailboxId, accountUserId);

        public Task<ImapAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            if (password == "bad-password") return Task.FromResult(new ImapAuthenticationResult(false, Guid.Empty, Guid.Empty));
            var account = _accounts.GetValueOrDefault(username, (mailboxId, userId));
            return Task.FromResult(new ImapAuthenticationResult(true, tenantId, account.Item1, account.Item2));
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
