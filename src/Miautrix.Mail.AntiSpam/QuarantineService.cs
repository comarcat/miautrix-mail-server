using System.Text.Json;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.AntiSpam;

public interface IQuarantineService
{
    Task<InboundProcessResult> ProcessInboundMessageAsync(
        Guid tenantId,
        InboundMailContext mail,
        double threshold = 5.0,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseMessageAsync(
        Guid tenantId,
        Guid quarantineItemId,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseAndTrainAsync(
        Guid tenantId,
        Guid quarantineItemId,
        bool isSpam,
        CancellationToken cancellationToken = default);

    Task<List<QuarantineItem>> GetQuarantineListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public sealed class QuarantineService : IQuarantineService
{
    private readonly AppDbContext _dbContext;
    private readonly ISpamProvider _spamProvider;

    public QuarantineService(AppDbContext dbContext, ISpamProvider spamProvider)
    {
        _dbContext = dbContext;
        _spamProvider = spamProvider;
    }

    public async Task<InboundProcessResult> ProcessInboundMessageAsync(
        Guid tenantId,
        InboundMailContext mail,
        double threshold = 5.0,
        CancellationToken cancellationToken = default)
    {
        var verdict = await _spamProvider.EvaluateAsync(tenantId, mail, threshold, cancellationToken);
        var ruleMatches = JsonSerializer.Deserialize<List<RuleMatch>>(verdict.ReasonsJson) ?? new List<RuleMatch>();

        if (verdict.IsSpam)
        {
            var quarantineItem = new QuarantineItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Sender = mail.Sender,
                Recipient = mail.Recipient,
                Subject = mail.Subject,
                RawMessage = mail.RawMessage,
                SpamScore = verdict.Score,
                Threshold = threshold,
                ReasonsJson = verdict.ReasonsJson,
                Status = "Quarantined",
                QuarantinedAt = DateTimeOffset.UtcNow,
                IsDelivered = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Quarantine.Add(quarantineItem);
            _dbContext.SpamVerdicts.Add(verdict);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new InboundProcessResult(
                Delivered: false,
                Quarantined: true,
                QuarantineItemId: quarantineItem.Id,
                Score: verdict.Score,
                Threshold: threshold,
                MatchedRules: ruleMatches);
        }
        else
        {
            _dbContext.SpamVerdicts.Add(verdict);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new InboundProcessResult(
                Delivered: true,
                Quarantined: false,
                QuarantineItemId: null,
                Score: verdict.Score,
                Threshold: threshold,
                MatchedRules: ruleMatches);
        }
    }

    public async Task<bool> ReleaseMessageAsync(
        Guid tenantId,
        Guid quarantineItemId,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Quarantine
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quarantineItemId, cancellationToken);

        if (item == null || item.Status != "Quarantined")
        {
            return false;
        }

        item.Status = "Released";
        item.ReleasedAt = DateTimeOffset.UtcNow;
        item.IsDelivered = true;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReleaseAndTrainAsync(
        Guid tenantId,
        Guid quarantineItemId,
        bool isSpam,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Quarantine
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quarantineItemId, cancellationToken);

        if (item == null)
        {
            return false;
        }

        item.Status = isSpam ? "TrainedSpam" : "TrainedHam";
        item.TrainedAt = DateTimeOffset.UtcNow;
        item.ReleasedAt = isSpam ? null : DateTimeOffset.UtcNow;
        item.IsDelivered = !isSpam;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<QuarantineItem>> GetQuarantineListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Quarantine
            .Where(q => q.TenantId == tenantId)
            .OrderByDescending(q => q.QuarantinedAt)
            .ToListAsync(cancellationToken);
    }
}
