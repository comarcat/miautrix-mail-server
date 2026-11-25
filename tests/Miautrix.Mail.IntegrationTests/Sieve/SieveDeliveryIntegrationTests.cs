using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Application.Mail.Mime;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Sieve;
using Miautrix.Mail.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Miautrix.Mail.IntegrationTests.Sieve;

public sealed class SieveDeliveryIntegrationTests
{
    [Fact]
    public async Task Delivery_AppliesActionsGuardsRedirectsVacationAndFallsBackToKeep()
    {
        var maintenance = Environment.GetEnvironmentVariable("MIAUTRIX_TEST_SERVER_CONNECTION")
            ?? throw new InvalidOperationException("MIAUTRIX_TEST_SERVER_CONNECTION is required.");
        var server = new NpgsqlConnectionStringBuilder(maintenance);
        if (!string.Equals(server.Database, "postgres", StringComparison.OrdinalIgnoreCase) ||
            (server.Database ?? string.Empty).StartsWith("miautrix", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use the postgres maintenance DB on the test server.");
        var database = "miautrix_test_sieve_delivery_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(maintenance))
        {
            await connection.OpenAsync();
            await using var create = connection.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{database}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var dbConnection = new NpgsqlConnectionStringBuilder(maintenance) { Database = database };
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(dbConnection.ConnectionString).Options;
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();
            var tenant = new Tenant { Slug = "sieve-delivery-" + Guid.NewGuid().ToString("N") };
            await db.Tenants.AddAsync(tenant);
            await db.SaveChangesAsync();
            var domain = new Miautrix.Mail.Domain.Domain { TenantId = tenant.Id, Name = "example.test" };
            var mailbox = new Mailbox { TenantId = tenant.Id, DomainId = domain.Id, Address = "filter@example.test", Name = "Filter", Kind = "shared" };
            db.AddRange(domain, mailbox,
                new Folder { TenantId = tenant.Id, MailboxId = mailbox.Id, Name = "Inbox", Role = "inbox" },
                new Folder { TenantId = tenant.Id, MailboxId = mailbox.Id, Name = "Junk", Role = "junk" },
                new Folder { TenantId = tenant.Id, MailboxId = mailbox.Id, Name = "Archive", Role = "custom" });
            await db.SaveChangesAsync();
            var queue = new SmtpQueueManager(db, new ExponentialBackoffWithJitterRetryPolicy());
            var service = new SieveDeliveryService(db, new SieveCompiler(), new SieveInterpreter(), queue, NullLogger<SieveDeliveryService>.Instance);
            var parsed = Parsed("To: filter@example.test\r\nFrom: sender@remote.test\r\nMessage-ID: <original@remote.test>\r\n", "Weekly", "filter@example.test");

            async Task<SieveDeliveryDecision> Evaluate(string content, string raw = "From: sender@remote.test\r\n")
            {
                var script = await db.SieveScripts.FirstOrDefaultAsync(s => s.MailboxId == mailbox.Id);
                if (script is null)
                {
                    script = new SieveScript { TenantId = tenant.Id, MailboxId = mailbox.Id, Name = "active", Content = content, IsActive = true };
                    db.SieveScripts.Add(script);
                }
                else { script.Content = content; script.UpdatedAt = DateTimeOffset.UtcNow; }
                await db.SaveChangesAsync();
                return await service.EvaluateAsync(mailbox, Inbound(tenant.Id, raw), parsed, "inbox");
            }

            var fileinto = await Evaluate("require [\"fileinto\"]; fileinto \"Archive\";");
            Assert.Equal("Archive", fileinto.FolderName);
            var missing = await Evaluate("require [\"fileinto\"]; fileinto \"Missing\";");
            Assert.Null(missing.FolderName);
            var create = await Evaluate("require [\"fileinto\", \"mailbox\"]; fileinto :create \"Created\";");
            Assert.Equal("Created", create.FolderName);
            Assert.Contains(db.Folders.Local, f => f.MailboxId == mailbox.Id && f.Name == "Created");

            var flags = await Evaluate("require [\"imap4flags\"]; setflag [\"\\\\Seen\"]; addflag [\"\\\\Flagged\"]; removeflag [\"\\\\Seen\"];");
            Assert.Equal(["\\Flagged"], flags.Flags);
            var discarded = await Evaluate("discard;");
            Assert.False(discarded.StoreLocally);
            Assert.True(discarded.Discard);

            var redirect = await Evaluate("require [\"copy\"]; redirect [\"filter@example.test\", \"one@remote.test\", \"two@remote.test\", \"three@remote.test\", \"four@remote.test\", \"five@remote.test\", \"six@remote.test\"];");
            Assert.False(redirect.StoreLocally);
            Assert.Equal(5, await db.SmtpQueue.CountAsync(q => q.Direction == QueueDirection.Outbound));
            Assert.All(await db.SmtpQueue.Where(q => q.Direction == QueueDirection.Outbound).ToListAsync(), q => Assert.Contains("X-Miautrix-Sieve-Redirect-Count: ", q.RawMessage));

            await db.SmtpQueue.ExecuteDeleteAsync();
            var rejectRaw = Inbound(tenant.Id, "From: sender@remote.test\r\n");
            await db.SieveScripts.Where(s => s.MailboxId == mailbox.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.Content, "require [\"reject\"]; reject \"No thanks\";"));
            await service.EvaluateAsync(mailbox, rejectRaw, parsed, "inbox");
            var rejection = await db.SmtpQueue.SingleAsync();
            Assert.Equal(string.Empty, rejection.Sender);
            Assert.Equal("sender@remote.test", rejection.Recipient);
            Assert.Contains("multipart/report", rejection.RawMessage);
            await db.SmtpQueue.ExecuteDeleteAsync();
            await service.EvaluateAsync(mailbox, Inbound(tenant.Id, "From: <>\r\n"), parsed, "inbox");
            Assert.Empty(await db.SmtpQueue.ToListAsync());

            await db.SieveScripts.Where(s => s.MailboxId == mailbox.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.Content, "require [\"vacation\"]; vacation :days 7 :subject \"Away\" :handle \"holiday\" \"Out of office\";"));
            await service.EvaluateAsync(mailbox, rejectRaw, parsed, "inbox");
            await service.EvaluateAsync(mailbox, rejectRaw, parsed, "inbox");
            Assert.Single(await db.SmtpQueue.Where(q => q.Direction == QueueDirection.Outbound).ToListAsync());
            var vacation = await db.SmtpQueue.SingleAsync(q => q.Direction == QueueDirection.Outbound);
            Assert.Equal(string.Empty, vacation.Sender);
            Assert.Contains("Auto-Submitted: auto-replied", vacation.RawMessage);
            await db.SmtpQueue.ExecuteDeleteAsync();
            await service.EvaluateAsync(mailbox, Inbound(tenant.Id, "From: sender@remote.test\r\nList-Id: test\r\n"), parsed, "inbox");
            await service.EvaluateAsync(mailbox, Inbound(tenant.Id, "From: <>\r\n"), parsed, "inbox");
            Assert.Empty(await db.SmtpQueue.ToListAsync());

            var failedRuntime = await Evaluate("if body :contains \"secret\" { discard; }");
            Assert.True(failedRuntime.StoreLocally);
            var outboundSubmission = Inbound(tenant.Id, "From: sender@remote.test\r\n", QueueDirection.Outbound);
            var submissionDecision = await service.EvaluateAsync(mailbox, outboundSubmission, parsed, "inbox");
            Assert.True(submissionDecision.StoreLocally);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(maintenance);
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static SmtpQueueItem Inbound(Guid tenantId, string raw, QueueDirection direction = QueueDirection.Inbound) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Direction = direction, Sender = HeaderValue(raw, "From") ?? "sender@remote.test",
        Recipient = "filter@example.test", Subject = "Weekly", RawMessage = $"{raw}To: filter@example.test\r\n\r\nBody\r\n", Status = "Pending",
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
    };

    private static string? HeaderValue(string raw, string name) => raw.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault(x => x.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))?.Split(':', 2)[1].Trim();

    private static ParsedMailMessage Parsed(string headers, string subject, string to) => new(headers, subject, DateTimeOffset.UtcNow,
        "sender@remote.test", to, null, null, "<original@remote.test>", "Body", null, []);
}
