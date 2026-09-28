namespace Miautrix.Mail.Application.Mail;

public interface ISieveRuleService
{
    Task<IReadOnlyList<SieveFilterRuleDto>> ListRulesAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken cancellationToken = default);

    Task<SieveFilterRuleDto> CreateRuleAsync(Guid tenantId, Guid userId, Guid mailboxId, SieveFilterRuleRequest request, CancellationToken cancellationToken = default);

    Task<SieveFilterRuleDto?> UpdateRuleAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid ruleId, SieveFilterRuleRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteRuleAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid ruleId, CancellationToken cancellationToken = default);

    Task<bool> SetRuleActiveAsync(Guid tenantId, Guid userId, Guid mailboxId, Guid ruleId, bool active, CancellationToken cancellationToken = default);
}
