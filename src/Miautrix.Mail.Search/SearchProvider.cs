using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Search;

public sealed record SearchMessageItem(
    Guid Id,
    Guid MailboxId,
    string Sender,
    string Recipient,
    string Subject,
    DateTimeOffset Date,
    string? Snippet,
    long SizeBytes
);

public interface ISearchProvider
{
    Task IndexMessageAsync(Guid tenantId, Message message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchMessageItem>> SearchAsync(Guid tenantId, Guid mailboxId, string query, int limit = 50, CancellationToken cancellationToken = default);
    Task ReindexMailboxAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlSearchProvider : ISearchProvider
{
    private readonly AppDbContext _dbContext;

    public PostgreSqlSearchProvider(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task IndexMessageAsync(Guid tenantId, Message message, CancellationToken cancellationToken = default)
    {
        // Message is persisted in PostgreSQL with Subject, Sender, Recipient, BodyText
        // If message is new, save it or ensure properties are updated
        if (await _dbContext.Messages.AnyAsync(m => m.Id == message.Id, cancellationToken))
        {
            _dbContext.Messages.Update(message);
        }
        else
        {
            _dbContext.Messages.Add(message);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SearchMessageItem>> SearchAsync(
        Guid tenantId,
        Guid mailboxId,
        string query,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchMessageItem>();
        }

        var normalizedQuery = query.Trim().ToLowerInvariant();
        var searchPattern = $"%{normalizedQuery}%";

        // Query using PostgreSQL text search / ILike matching on Subject, BodyText, Sender, Recipient
        var messages = await _dbContext.Messages
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MailboxId == mailboxId)
            .Where(m =>
                EF.Functions.ILike(m.Subject, searchPattern) ||
                (m.BodyText != null && EF.Functions.ILike(m.BodyText, searchPattern)) ||
                EF.Functions.ILike(m.Sender, searchPattern) ||
                EF.Functions.ILike(m.Recipient, searchPattern))
            .OrderByDescending(m => m.Date)
            .Take(limit)
            .Select(m => new SearchMessageItem(
                m.Id,
                m.MailboxId,
                m.Sender,
                m.Recipient,
                m.Subject,
                m.Date,
                m.BodyText != null && m.BodyText.Length > 100 ? m.BodyText.Substring(0, 100) : m.BodyText,
                m.SizeBytes
            ))
            .ToListAsync(cancellationToken);

        return messages;
    }

    public async Task ReindexMailboxAsync(Guid tenantId, Guid mailboxId, CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.Messages
            .Where(m => m.TenantId == tenantId && m.MailboxId == mailboxId)
            .ToListAsync(cancellationToken);

        foreach (var msg in messages)
        {
            msg.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
