using System;
using System.Collections.Generic;

namespace Miautrix.Mail.Application.Admin;

// Domain Contracts
public sealed record DomainDto(
    Guid Id,
    string Name,
    bool IsVerified,
    string? DkimSelector,
    string? DkimPublicKey,
    string? SpfRecord,
    string? DmarcRecord,
    bool IsPrimary,
    DateTimeOffset CreatedAt);

public sealed record CreateDomainRequest(
    string Name,
    bool IsPrimary = false);

public sealed record UpdateDomainRequest(
    string? Name = null,
    bool? IsPrimary = null,
    string? DkimSelector = null,
    string? SpfRecord = null,
    string? DmarcRecord = null);

public sealed record VerifyDomainResult(
    bool IsVerified,
    string Message,
    string DkimStatus,
    string SpfStatus,
    string DmarcStatus);

// User Contracts
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string Name,
    bool IsActive,
    string Role,
    long MailboxQuotaBytes,
    long MailboxUsedBytes,
    DateTimeOffset CreatedAt);

public sealed record CreateUserRequest(
    string Email,
    string Name,
    string Password,
    string? Role = "user",
    long? QuotaBytes = 10737418240);

public sealed record UpdateUserRequest(
    string? Name,
    bool? IsActive,
    string? Role,
    long? QuotaBytes);

// Quarantine Contracts
public sealed record QuarantineItemDto(
    Guid Id,
    string Sender,
    string Recipient,
    string? Subject,
    double SpamScore,
    double Threshold,
    string ReasonsJson,
    string Status,
    DateTimeOffset QuarantinedAt,
    DateTimeOffset? ReleasedAt);

public sealed record QuarantineFilter(
    string? Status = null,
    string? Search = null,
    int Limit = 50,
    string? Cursor = null);

// Audit Contracts
public sealed record AuditLogDto(
    Guid Id,
    string Action,
    Guid? ActorId,
    string? ActorEmail,
    string TargetType,
    Guid? TargetId,
    string? DetailsJson,
    string? IpAddress,
    DateTimeOffset CreatedAt);

public sealed record AuditFilter(
    string? Action = null,
    string? Search = null,
    int Limit = 50,
    string? Cursor = null);

// Mail Flow Rule Contracts
public sealed record MailFlowRuleDto(
    Guid Id,
    string Name,
    int Priority,
    bool IsEnabled,
    string ConditionsJson,
    string ActionsJson,
    DateTimeOffset CreatedAt);

public sealed record CreateRuleRequest(
    string Name,
    int Priority,
    bool IsEnabled,
    string ConditionsJson,
    string ActionsJson);

public sealed record UpdateRuleRequest(
    string? Name,
    int? Priority,
    bool? IsEnabled,
    string? ConditionsJson,
    string? ActionsJson);

public sealed record RuleSampleMessage(
    string Sender,
    string Recipient,
    string Subject,
    Dictionary<string, string>? Headers,
    bool HasAttachment,
    double? SpamScore);

public sealed record RuleSimulationRequest(
    object? Rule,
    RuleSampleMessage SampleMessage);

public sealed record RuleSimulationResult(
    bool Matched,
    List<string> ActionsTaken,
    double Score,
    List<string> Log);

// System & Telemetry Contracts
public sealed record DashboardSummaryDto(
    int ActiveQueued,
    int Retrying,
    int DeadLetters,
    int Delivered24h,
    int Quarantined24h,
    int SpamBlocked24h,
    string SystemHealth,
    long UptimeSeconds,
    int TenantCount);

public sealed record SystemInfoDto(
    string Version,
    string Runtime,
    string DatabaseStatus,
    long UptimeSeconds,
    long StorageUsedBytes,
    long StorageTotalBytes,
    int ActiveWorkers,
    string OsVersion);

public sealed record LicensingDto(
    string Edition,
    int ActiveMailboxes,
    int MaxMailboxes,
    bool MfaIncluded,
    bool BackupIncluded,
    bool CustomDomainsIncluded,
    string Status,
    DateTimeOffset ValidUntil);

public sealed record BackupJobDto(
    Guid Id,
    string Name,
    string Status,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
