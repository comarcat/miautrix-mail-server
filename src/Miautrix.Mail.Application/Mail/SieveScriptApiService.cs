using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Sieve;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Application.Mail;

public sealed record SieveScriptDto(Guid Id, Guid MailboxId, string Name, string Content, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record SieveScriptSaveRequest(string Name, string Content);
public sealed record SieveScriptSaveResult(SieveScriptDto? Script, SieveParseResult Diagnostic)
{
    public bool Success => Script is not null && Diagnostic.Success;
}

public interface ISieveScriptApiService
{
    Task<IReadOnlyList<SieveScriptDto>> ListAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken ct = default);
    Task<SieveScriptDto?> GetAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid scriptId, CancellationToken ct = default);
    Task<SieveScriptSaveResult> CreateAsync(Guid tenantId, Guid userId, Guid mailboxId, SieveScriptSaveRequest request, CancellationToken ct = default);
    Task<SieveScriptSaveResult?> UpdateAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid scriptId, SieveScriptSaveRequest request, CancellationToken ct = default);
    Task<bool> SetActiveAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid scriptId, bool active, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid scriptId, CancellationToken ct = default);
}

public sealed class SieveScriptApiService : ISieveScriptApiService
{
    private const int MaxScriptLength = 256 * 1024;
    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _authorization;
    private readonly SieveCompiler _compiler;

    public SieveScriptApiService(AppDbContext db, ITenantAuthorizationHelper authorization, SieveCompiler compiler)
    {
        _db = db;
        _authorization = authorization;
        _compiler = compiler;
    }

    public async Task<IReadOnlyList<SieveScriptDto>> ListAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken ct = default)
    {
        await AuthorizeMailboxAsync(tenantId, userId, mailboxId, false, ct);
        var scripts = await _db.SieveScripts.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.MailboxId == mailboxId)
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.Name)
            .ToListAsync(ct);
        return scripts.Select(ToDto).ToArray();
    }

    public async Task<SieveScriptDto?> GetAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid scriptId, CancellationToken ct = default)
    {
        await AuthorizeMailboxAsync(tenantId, userId, mailboxId, false, ct);
        var script = await FindScriptAsync(tenantId, mailboxId, scriptId, ct);
        return script is null ? null : ToDto(script);
    }

    public async Task<SieveScriptSaveResult> CreateAsync(Guid tenantId, Guid userId, Guid mailboxId, SieveScriptSaveRequest request, CancellationToken ct = default)
    {
        await AuthorizeMailboxAsync(tenantId, userId, mailboxId, true, ct);
        var diagnostic = Validate(request);
        if (!diagnostic.Success) return new SieveScriptSaveResult(null, diagnostic);
        var now = DateTimeOffset.UtcNow;
        var script = new SieveScript
        {
            Id = Guid.NewGuid(), TenantId = tenantId, MailboxId = mailboxId, Name = request.Name.Trim(),
            Content = request.Content, IsActive = false, CreatedAt = now, UpdatedAt = now
        };
        _db.SieveScripts.Add(script);
        await _db.SaveChangesAsync(ct);
        return new SieveScriptSaveResult(ToDto(script), diagnostic);
    }

    public async Task<SieveScriptSaveResult?> UpdateAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid scriptId, SieveScriptSaveRequest request, CancellationToken ct = default)
    {
        await AuthorizeMailboxAsync(tenantId, userId, mailboxId, true, ct);
        var script = await FindScriptAsync(tenantId, mailboxId, scriptId, ct);
        if (script is null) return null;
        var diagnostic = Validate(request);
        if (!diagnostic.Success) return new SieveScriptSaveResult(null, diagnostic);
        script.Name = request.Name.Trim(); script.Content = request.Content; script.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new SieveScriptSaveResult(ToDto(script), diagnostic);
    }

    public async Task<bool> SetActiveAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid scriptId, bool active, CancellationToken ct = default)
    {
        await AuthorizeMailboxAsync(tenantId, userId, mailboxId, true, ct);
        var script = await FindScriptAsync(tenantId, mailboxId, scriptId, ct);
        if (script is null) return false;
        if (active)
        {
            var validation = _compiler.Compile(script.Content);
            if (!validation.Success) throw new ArgumentException(validation.Diagnostic.ErrorMessage ?? "Invalid Sieve script.");
        }
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        if (active)
        {
            var others = await _db.SieveScripts.Where(s => s.TenantId == tenantId && s.MailboxId == mailboxId && s.IsActive && s.Id != scriptId).ToListAsync(ct);
            foreach (var other in others) { other.IsActive = false; other.UpdatedAt = DateTimeOffset.UtcNow; }
            if (others.Count > 0) await _db.SaveChangesAsync(ct);
        }
        script.IsActive = active;
        script.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid scriptId, CancellationToken ct = default)
    {
        await AuthorizeMailboxAsync(tenantId, userId, mailboxId, true, ct);
        var script = await FindScriptAsync(tenantId, mailboxId, scriptId, ct);
        if (script is null) return false;
        _db.SieveScripts.Remove(script);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task AuthorizeMailboxAsync(Guid tenantId, Guid userId, Guid mailboxId, bool write, CancellationToken ct)
    {
        // Query by the opaque id first so a mailbox in another tenant is passed to the
        // central helper and remains indistinguishable from a missing mailbox (404).
        var mailbox = await _db.Mailboxes.FirstOrDefaultAsync(m => m.Id == mailboxId, ct);
        _authorization.AssertMailboxAccess(tenantId, userId, mailbox, write);
    }

    private Task<SieveScript?> FindScriptAsync(Guid tenantId, Guid mailboxId, Guid scriptId, CancellationToken ct) =>
        _db.SieveScripts.FirstOrDefaultAsync(s => s.Id == scriptId && s.TenantId == tenantId && s.MailboxId == mailboxId, ct);

    private SieveParseResult Validate(SieveScriptSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return new SieveParseResult(false, "Script name is required.", 1, 1);
        if (string.IsNullOrEmpty(request.Content)) return new SieveParseResult(false, "Script content is required.", 1, 1);
        if (request.Content.Length > MaxScriptLength) return new SieveParseResult(false, $"Script exceeds {MaxScriptLength} characters.", 1, 1);
        return _compiler.Compile(request.Content).Diagnostic;
    }

    private static SieveScriptDto ToDto(SieveScript script) => new(script.Id, script.MailboxId, script.Name, script.Content, script.IsActive, script.CreatedAt, script.UpdatedAt);
}
