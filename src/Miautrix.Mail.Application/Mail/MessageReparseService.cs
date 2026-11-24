using System.Text;
using System.Text.Json;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Application.Mail.Mime;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Application.Mail;

public sealed record MessageReparseResult(int Scanned, int Updated, int Skipped, int Failed, bool DryRun);

public sealed class MessageReparseService
{
    private const int BatchSize = 200;
    private readonly AppDbContext _db;
    private readonly IMailStorage _storage;
    private readonly IMimeMessageParser _parser;
    private readonly ITenantAuthorizationHelper _auth;

    public MessageReparseService(AppDbContext db, IMailStorage storage, IMimeMessageParser parser, ITenantAuthorizationHelper auth)
    {
        _db = db;
        _storage = storage;
        _parser = parser;
        _auth = auth;
    }

    public async Task<MessageReparseResult> ReparseAsync(Guid tenantId, Guid? userId, DateTimeOffset? since, bool dryRun, CancellationToken ct = default)
    {
        if (userId.HasValue) _auth.AssertPermission(tenantId, userId.Value, "mailbox.update");

        var scanned = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;
        var offset = 0;
        while (true)
        {
            var query = _db.Messages.AsNoTracking()
                .Where(m => m.TenantId == tenantId && (!since.HasValue || m.Date >= since.Value));
            // Stable ID paging prevents a processed row from moving within later batches.
            var batch = await query.OrderBy(m => m.Id).Skip(offset).Take(BatchSize).ToListAsync(ct);
            if (batch.Count == 0) break;
            offset += batch.Count;

            foreach (var original in batch)
            {
                scanned++;
                try
                {
                    await using var content = await _storage.OpenReadAsync(original.ContentHash, ct);
                    if (content is null) { skipped++; continue; }
                    using var buffer = new MemoryStream();
                    await content.CopyToAsync(buffer, ct);
                    var parsed = _parser.Parse(Encoding.UTF8.GetString(buffer.ToArray()));
                    if (original.BodyText == parsed.BodyText && original.BodyHtml == parsed.BodyHtml && original.Subject == parsed.Subject)
                    {
                        skipped++;
                        continue;
                    }
                    if (dryRun) { updated++; continue; }

                    var entity = await _db.Messages.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == original.Id, ct);
                    if (entity is null) { skipped++; continue; }
                    entity.BodyText = parsed.BodyText;
                    entity.BodyHtml = parsed.BodyHtml;
                    entity.Subject = parsed.Subject ?? entity.Subject;
                    updated++;
                }
                catch (Exception)
                {
                    failed++;
                }
            }
            if (!dryRun) await _db.SaveChangesAsync(ct);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ActorId = userId,
            Action = "messages.reparse", TargetType = "tenant", TargetId = tenantId,
            DetailsJson = JsonSerializer.Serialize(new { actor = userId?.ToString() ?? "cli:operator", scanned, updated, skipped, failed, dryRun }),
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return new MessageReparseResult(scanned, updated, skipped, failed, dryRun);
    }
}
