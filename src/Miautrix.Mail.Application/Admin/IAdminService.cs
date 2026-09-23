using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Miautrix.Mail.Application.Admin;

public interface IAdminService
{
    // Domains
    Task<IReadOnlyList<DomainDto>> ListDomainsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<DomainDto?> GetDomainAsync(Guid tenantId, Guid userId, Guid domainId, CancellationToken ct = default);
    Task<DomainDto> CreateDomainAsync(Guid tenantId, Guid userId, CreateDomainRequest request, CancellationToken ct = default);
    Task<VerifyDomainResult> VerifyDomainAsync(Guid tenantId, Guid userId, Guid domainId, CancellationToken ct = default);
    Task<bool> DeleteDomainAsync(Guid tenantId, Guid userId, Guid domainId, CancellationToken ct = default);
    Task<DomainDto?> UpdateDomainAsync(Guid tenantId, Guid userId, Guid domainId, UpdateDomainRequest request, CancellationToken ct = default);

    // Users
    Task<IReadOnlyList<AdminUserDto>> ListUsersAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<AdminUserDto?> GetUserAsync(Guid tenantId, Guid userId, Guid targetUserId, CancellationToken ct = default);
    Task<AdminUserDto> CreateUserAsync(Guid tenantId, Guid userId, CreateUserRequest request, CancellationToken ct = default);
    Task<AdminUserDto?> UpdateUserAsync(Guid tenantId, Guid userId, Guid targetUserId, UpdateUserRequest request, CancellationToken ct = default);
    Task<SharedMailboxDto> CreateSharedMailboxAsync(Guid tenantId, Guid userId, CreateSharedMailboxRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SharedMailboxDto>> ListSharedMailboxesAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<SharedMailboxDto?> GetSharedMailboxAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken ct = default);
    Task<SharedMailboxDto> UpdateSharedMailboxDelegatesAsync(Guid tenantId, Guid userId, Guid mailboxId, IReadOnlyList<MailboxDelegateRequest> delegates, CancellationToken ct = default);
    Task<ResetPasswordResult?> ResetPasswordAsync(Guid tenantId, Guid userId, Guid targetUserId, ResetPasswordRequest request, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, Guid targetUserId, CancellationToken ct = default);

    // Orphan mailboxes
    Task<IReadOnlyList<OrphanMailboxDto>> ListOrphanMailboxesAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<SharedMailboxDto> AssignMailboxAsync(Guid tenantId, Guid userId, Guid mailboxId, AssignMailboxRequest request, CancellationToken ct = default);
    Task<DeleteMailboxResult> DeleteMailboxAsync(Guid tenantId, Guid userId, Guid mailboxId, DeleteMailboxRequest request, CancellationToken ct = default);
    Task<MailboxArchiveDto> ExportMailboxAsync(Guid tenantId, Guid userId, Guid mailboxId, CancellationToken ct = default);

    // Quarantine
    Task<IReadOnlyList<QuarantineItemDto>> ListQuarantineAsync(Guid tenantId, Guid userId, QuarantineFilter filter, CancellationToken ct = default);
    Task<QuarantineItemDto?> GetQuarantineItemAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default);
    Task<bool> ReleaseQuarantineItemAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default);
    Task<bool> DeliverAndDeleteQuarantineItemAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default);
    Task<MailFlowRuleDto?> BlockQuarantineSenderDomainAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default);
    Task<bool> DeleteQuarantineItemAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default);

    // Audit
    Task<IReadOnlyList<AuditLogDto>> ListAuditLogsAsync(Guid tenantId, Guid userId, AuditFilter filter, CancellationToken ct = default);

    // Mail Flow Rules
    Task<IReadOnlyList<MailFlowRuleDto>> ListRulesAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<MailFlowRuleDto?> GetRuleAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default);
    Task<MailFlowRuleDto> CreateRuleAsync(Guid tenantId, Guid userId, CreateRuleRequest request, CancellationToken ct = default);
    Task<MailFlowRuleDto?> UpdateRuleAsync(Guid tenantId, Guid userId, Guid id, UpdateRuleRequest request, CancellationToken ct = default);
    Task<bool> DeleteRuleAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct = default);
    Task<RuleSimulationResult> SimulateRuleAsync(Guid tenantId, Guid userId, RuleSimulationRequest request, CancellationToken ct = default);

    // System
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<SystemInfoDto> GetSystemInfoAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<LicensingDto> GetLicensingAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<BackupJobDto>> GetBackupJobsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<BackupJobDto> CreateBackupAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantDto>> ListTenantsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<SecuritySettingsDto> GetSecuritySettingsAsync(Guid domainId, Guid userId, CancellationToken ct = default);
    Task<SecuritySettingsDto> UpdateSecuritySettingsAsync(Guid domainId, Guid userId, UpdateSecuritySettingsRequest request, CancellationToken ct = default);
    Task<AntiSpamSettingsDto> GetAntiSpamSettingsAsync(Guid domainId, Guid userId, CancellationToken ct = default);
    Task<AntiSpamSettingsDto> UpdateAntiSpamSettingsAsync(Guid domainId, Guid userId, UpdateAntiSpamSettingsRequest request, CancellationToken ct = default);
}
