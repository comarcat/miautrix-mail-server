using System;
using System.Collections.Generic;
using Miautrix.Mail.Domain;

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
    DateTimeOffset CreatedAt,
    string TransportMode,
    string? CloudflareZoneId,
    string? CloudflareWorkerUrl);

public sealed record CreateDomainRequest(
    string Name,
    bool IsPrimary = false,
    string TransportMode = DomainTransportModes.Local,
    string? CloudflareZoneId = null,
    string? CloudflareWorkerUrl = null);

public sealed record UpdateDomainRequest(
    string? Name = null,
    bool? IsPrimary = null,
    string? DkimSelector = null,
    string? SpfRecord = null,
    string? DmarcRecord = null,
    string? TransportMode = null,
    string? CloudflareZoneId = null,
    string? CloudflareWorkerUrl = null);

/// <summary>
/// The DNS status fields are null when the domain does not use the local transport: a
/// Cloudflare-mode domain has no DKIM/SPF/DMARC verdict of its own. Empty string would render in
/// the admin UI as "checked and failed", which is a different statement.
/// </summary>
public sealed record VerifyDomainResult(
    bool IsVerified,
    string Message,
    string TransportMode,
    string? DkimStatus,
    string? SpfStatus,
    string? DmarcStatus,
    string? WorkerStatus);

// User Contracts
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string Name,
    bool IsActive,
    string Role,
    long MailboxQuotaBytes,
    long MailboxUsedBytes,
    DateTimeOffset CreatedAt,
    bool MustChangePassword,
    bool IsService,
    string MailboxKind);

public sealed record MailboxDelegateDto(
    Guid UserId,
    string Email,
    string Name,
    string AccessLevel);

public sealed record MailboxDelegateRequest(
    Guid UserId,
    string AccessLevel);

public sealed record SharedMailboxDto(
    Guid Id,
    string Email,
    string Name,
    long MailboxQuotaBytes,
    long MailboxUsedBytes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MailboxDelegateDto> Delegates);

public sealed record CreateSharedMailboxRequest(
    string Email,
    string Name,
    long? QuotaBytes = 10737418240,
    IReadOnlyList<MailboxDelegateRequest>? Delegates = null);

public sealed record CreateUserRequest(
    string Email,
    string Name,
    string? Password = null,
    string? Role = "member",
    long? QuotaBytes = 10737418240,
    bool MustChangePassword = false,
    bool IsService = false,
    string? MailboxKind = "user",
    IReadOnlyList<MailboxDelegateRequest>? Delegates = null);

public sealed record UpdateUserRequest(
    string? Name,
    bool? IsActive,
    string? Role,
    long? QuotaBytes,
    string? Email,
    bool? MustChangePassword,
    bool? IsService);

public sealed record ResetPasswordRequest(
    string? Password,
    bool MustChangePassword,
    bool GeneratePassword = false);

public sealed record ResetPasswordResult(
    string? GeneratedPassword);

// Orphan mailbox contracts
public sealed record OrphanMailboxDto(
    Guid Id,
    string Email,
    string Name,
    string Kind,
    long MailboxQuotaBytes,
    long MailboxUsedBytes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    int MessageCount,
    int AttachmentCount);

public sealed record AssignMailboxRequest(
    string Address,
    string? Name = null,
    IReadOnlyList<MailboxDelegateRequest>? Delegates = null,
    long? QuotaBytes = null);

public sealed record DeleteMailboxRequest(
    string ConfirmAddress);

public sealed record DeleteMailboxResult(
    int MessagesDeleted,
    int AttachmentsDeleted,
    int BlobsDeleted);

public sealed record MailboxArchiveDto(
    string TempFilePath,
    string FileName);

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
public sealed record TransportDashboardMetricDto(
    string TransportMode,
    int ActiveQueued,
    int Retrying,
    int DeadLetters,
    int OutboundDelivered24h,
    int OutboundDeliveredTotal,
    int SuccessfulAttempts24h,
    int FailedAttempts24h,
    int TotalAttempts24h,
    int RetryAttempts24h,
    double DeliverySuccessRate24h,
    DateTimeOffset? LastDeliveryAttemptAt,
    int? LastDeliveryResponseCode,
    string? LastDeliveryError);

public sealed record DashboardSummaryDto(
    int ActiveQueued,
    int Retrying,
    int DeadLetters,
    int Delivered24h, // mailbox delivered (last 24h)
    int Quarantined24h,
    int SpamBlocked24h,
    string SystemHealth,
    long UptimeSeconds,
    int TenantCount,
    IReadOnlyList<TransportDashboardMetricDto> TransportBreakdown);

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
