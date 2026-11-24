using System.Net;
using System.Net.Sockets;
using System.Text;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.UnitTests.Imap;

public sealed class ImapRawSocketSequenceTests
{
    [Fact]
    public async Task Report_python_sequence_has_exact_body_peek_header_literal_framing()
    {
        var tenantId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var address = "raw@example.test";
        var raw = Encoding.UTF8.GetBytes("From: sender@example.test\r\nTo: raw@example.test\r\nSubject: Café\r\nX-Folded: one\r\n two\r\n\r\nBody π\r\n");
        var storageDir = Path.Combine(Path.GetTempPath(), $"miautrix_imap_raw_{Guid.NewGuid():N}");
        Directory.CreateDirectory(storageDir);
        var storage = new FileSystemMailStorage(storageDir);
        await using var content = new MemoryStream(raw);
        var stored = await storage.StoreAsync(content);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant { Id = tenantId, Slug = "raw-test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Domains.Add(new DomainEntity { Id = domainId, TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Mailboxes.Add(new Mailbox { Id = mailboxId, TenantId = tenantId, DomainId = domainId, Address = address, Name = address, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Folders.Add(new Folder { Id = folderId, TenantId = tenantId, MailboxId = mailboxId, Name = "INBOX", Role = "inbox", UidNext = 2, UidValidity = 7, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Messages.Add(new Message { Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, FolderId = folderId, Uid = 1, Sender = "sender@example.test", Recipient = address, Subject = "Café", Date = DateTimeOffset.UtcNow, ContentHash = stored.ContentHash, StoragePath = stored.StoragePath, SizeBytes = raw.Length, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var auth = new StubAuthenticator(tenantId, mailboxId);
        var session = new ImapSession(db, storage, auth, new AllowTestAuthorization());
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new TcpClient();
        var accepting = listener.AcceptTcpClientAsync();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await accepting;
        listener.Stop();
        var serverTask = ServeSessionAsync(server.GetStream(), session);
        var reader = new ImapConnectionReader(client.GetStream());

        await SendAndReadAsync(client.GetStream(), reader, "A1 LOGIN \"raw@example.test\" \"secret\"", "A1");
        var list = await SendAndReadAsync(client.GetStream(), reader, "A2 LIST \"\" \"*\"", "A2");
        Assert.Contains("* LIST", Encoding.UTF8.GetString(list));
        var examine = await SendAndReadAsync(client.GetStream(), reader, "A3 EXAMINE INBOX", "A3");
        Assert.Contains("[READ-ONLY]", Encoding.UTF8.GetString(examine));
        var search = await SendAndReadAsync(client.GetStream(), reader, "A4 SEARCH ALL", "A4");
        Assert.Contains("* SEARCH 1", Encoding.UTF8.GetString(search));
        var fetch = await SendAndReadAsync(client.GetStream(), reader, "A5 FETCH 1 (UID FLAGS BODY.PEEK[HEADER])", "A5");
        var fetchText = Encoding.UTF8.GetString(fetch);
        var expectedHeader = Encoding.UTF8.GetBytes("From: sender@example.test\r\nTo: raw@example.test\r\nSubject: Café\r\nX-Folded: one\r\n two\r\n\r\n");
        Assert.Contains($"BODY[HEADER] {{{expectedHeader.Length}}}\r\n", fetchText);
        Assert.True(ContainsBytes(fetch, expectedHeader));
        var itemSpacing = await SendAndReadAsync(client.GetStream(), reader, "A5a FETCH 1 (BODY.PEEK[HEADER] FLAGS UID)", "A5a");
        var expectedSpacing = Encoding.UTF8.GetBytes($"* 1 FETCH (BODY[HEADER] {{{expectedHeader.Length}}}\r\n");
        var expectedTail = Encoding.UTF8.GetBytes(" FLAGS () UID 1)\r\nA5a OK FETCH completed\r\n");
        Assert.True(itemSpacing.AsSpan(0, expectedSpacing.Length).SequenceEqual(expectedSpacing));
        Assert.True(ContainsBytes(itemSpacing, expectedHeader));
        Assert.True(itemSpacing.AsSpan(itemSpacing.Length - expectedTail.Length).SequenceEqual(expectedTail));
        var partialAndHeader = await SendAndReadAsync(client.GetStream(), reader, "A5aa FETCH 1 (BODY[]<0.10> BODY[HEADER])", "A5aa");
        Assert.True(ContainsBytes(partialAndHeader, Encoding.ASCII.GetBytes("BODY[]<0> {10}\r\n")), Encoding.UTF8.GetString(partialAndHeader));
        Assert.True(ContainsBytes(partialAndHeader, expectedHeader));
        var selectedFields = await SendAndReadAsync(client.GetStream(), reader, "A5b FETCH 1 (BODY.PEEK[HEADER.FIELDS (X-Folded)])", "A5b");
        var expectedField = Encoding.UTF8.GetBytes("X-Folded: one\r\n two\r\n\r\n");
        Assert.Contains($"BODY[HEADER.FIELDS (X-Folded)] {{{expectedField.Length}}}\r\n", Encoding.UTF8.GetString(selectedFields));
        Assert.True(ContainsBytes(selectedFields, expectedField));
        var logout = await SendAndReadAsync(client.GetStream(), reader, "A6 LOGOUT", "A6");
        Assert.Contains("* BYE", Encoding.UTF8.GetString(logout));
        await serverTask;

        var delegateSession = new ImapSession(db, storage, auth, new ReadOnlyDelegateAuthorization());
        Assert.Equal("OK", (await delegateSession.ExecuteCommandAsync("B1 LOGIN \"raw@example.test\" \"secret\"")).Status);
        var delegateSelect = await delegateSession.ExecuteCommandAsync("B2 SELECT INBOX");
        Assert.Equal("OK", delegateSelect.Status);
        Assert.Contains("[READ-ONLY]", delegateSelect.Message);
        var delegateFetch = await delegateSession.ExecuteCommandAsync("B3 FETCH 1 (BODY[])");
        Assert.Equal("OK", delegateFetch.Status);
        Assert.False((await db.Messages.SingleAsync()).IsRead);
        Directory.Delete(storageDir, recursive: true);
    }

    private static async Task ServeSessionAsync(NetworkStream stream, ImapSession session)
    {
        var reader = new ImapConnectionReader(stream);
        while (true)
        {
            var collected = await ImapCommandCollector.ReadAsync(reader, (_, _) => Task.CompletedTask, 1024 * 1024);
            var command = ImapCommandTokenizer.Parse(collected.Text, collected.Literals);
            await ImapResponseWriter.WriteAsync(stream, await session.ExecuteCommandAsync(command));
            if (session.State == ImapState.LoggedOut) break;
        }
    }

    private static async Task<byte[]> SendAndReadAsync(NetworkStream stream, ImapConnectionReader reader, string command, string tag)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(command + "\r\n"));
        using var result = new MemoryStream();
        while (true)
        {
            var line = await reader.ReadLineAsync() ?? throw new EndOfStreamException();
            result.Write(line);
            result.Write("\r\n"u8);
            var text = Encoding.UTF8.GetString(line);
            var marker = System.Text.RegularExpressions.Regex.Match(text, @"\{(\d+)\}$");
            if (marker.Success)
            {
                var data = await reader.ReadLiteralAsync(int.Parse(marker.Groups[1].Value));
                result.Write(data);
            }
            if (text.StartsWith(tag + " ", StringComparison.Ordinal)) break;
        }
        return result.ToArray();
    }

    private static bool ContainsBytes(byte[] source, byte[] expected)
    {
        for (var i = 0; i <= source.Length - expected.Length; i++)
            if (source.AsSpan(i, expected.Length).SequenceEqual(expected)) return true;
        return false;
    }

    private sealed class StubAuthenticator(Guid tenantId, Guid mailboxId) : IImapAuthenticator
    {
        public Task<ImapAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(new ImapAuthenticationResult(true, tenantId, mailboxId, Guid.NewGuid()));
    }

    private sealed class AllowTestAuthorization : ITenantAuthorizationHelper
    {
        public TResource AuthorizeAccess<TResource>(Guid currentTenantId, Guid currentUserId, TResource? resource, string requiredPermissionCode) where TResource : TenantScopedEntityBase => resource!;
        public void AssertPermission(Guid tenantId, Guid userId, string permissionCode) { }
        public void AssertMailboxAccess(Guid tenantId, Guid userId, Mailbox? mailbox, bool requireWrite) { if (mailbox is null) throw new ResourceNotFoundException(); }
        public void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId) { }
    }

    private sealed class ReadOnlyDelegateAuthorization : ITenantAuthorizationHelper
    {
        public TResource AuthorizeAccess<TResource>(Guid currentTenantId, Guid currentUserId, TResource? resource, string requiredPermissionCode) where TResource : TenantScopedEntityBase => resource!;
        public void AssertPermission(Guid tenantId, Guid userId, string permissionCode) { }
        public void AssertMailboxAccess(Guid tenantId, Guid userId, Mailbox? mailbox, bool requireWrite)
        {
            if (mailbox is null || requireWrite) throw new ResourceNotFoundException();
        }
        public void ValidateOwnerDemotion(Guid tenantId, Guid targetUserId) { }
    }
}
