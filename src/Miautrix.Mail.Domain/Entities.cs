namespace Miautrix.Mail.Domain;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public abstract class TenantScopedEntityBase : EntityBase
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

public class Tenant : EntityBase
{
    public string Slug { get; set; } = string.Empty;
}

// Tenancy/identity
public class Domain : TenantScopedEntityBase
{
    public string Name { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool MfaEnforced { get; set; }
    public int? SessionLifetimeMinutes { get; set; }
    public int? LockoutMaxFailedAttempts { get; set; }
    public int? LockoutDurationMinutes { get; set; }
    public double SpamRejectScore { get; set; } = 14.0;
    public double SpamQuarantineScore { get; set; } = 10.0;
    public double SpamHeaderScore { get; set; } = 6.0;
    public double SpamGreylistScore { get; set; } = 4.0;
    public bool SpamGreylistingEnabled { get; set; } = true;
    public bool SpamSpfDmarcEnforcementEnabled { get; set; } = true;
    public string? DkimSelector { get; set; }
    public string? DkimPublicKey { get; set; }
    public string? SpfRecord { get; set; }
    public string? DmarcRecord { get; set; }
    public bool IsPrimary { get; set; }

    /// <summary>
    /// How this domain's SMTP traffic is relayed. <c>local</c> means the domain publishes its own
    /// MX and TXT records and we bind the mail ports; <c>cloudflare</c> means Cloudflare relays
    /// inbound and outbound SMTP through a Worker and manages the DNS records for us.
    /// Cloudflare has no IMAP/POP3 product, so mailbox access stays local in both modes.
    /// </summary>
    public string TransportMode { get; set; } = DomainTransportModes.Local;

    /// <summary>Cloudflare zone the domain belongs to. Not a secret — shown in the admin UI.</summary>
    public string? CloudflareZoneId { get; set; }

    /// <summary>Base URL of the deployed Cloudflare Worker that relays this domain's mail.</summary>
    public string? CloudflareWorkerUrl { get; set; }
}

/// <summary>
/// The values <see cref="Domain.TransportMode"/> may hold. Kept as constants rather than an enum
/// so the column stays a plain string and adding a mode needs no destructive type migration.
/// </summary>
public static class DomainTransportModes
{
    /// <summary>MX points at us; we bind 25/465/587/993 and verification checks our own DNS records.</summary>
    public const string Local = "local";

    /// <summary>Cloudflare relays SMTP via a Worker; verification checks the Worker instead of DNS.</summary>
    public const string Cloudflare = "cloudflare";

    public static bool IsKnown(string? mode) =>
        mode is Local or Cloudflare;
}
public class User : TenantScopedEntityBase
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsService { get; set; }
}
public class UserCredential : TenantScopedEntityBase
{
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "argon2id";
    public bool MustChangePassword { get; set; }
}
public class UserIdentity : TenantScopedEntityBase { }
public class IdentityProvider : TenantScopedEntityBase { }
public class IdentityProviderGroup : TenantScopedEntityBase { }
public class Role : TenantScopedEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
public class Permission : TenantScopedEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class RolePermission : TenantScopedEntityBase
{
    public Guid? RoleId { get; set; }
    public Guid? PermissionId { get; set; }
}
public class Membership : TenantScopedEntityBase
{
    public Guid? UserId { get; set; }
    public Guid? RoleId { get; set; }
}
public class Session : TenantScopedEntityBase { }
public class RefreshToken : TenantScopedEntityBase { }
public class ApplicationPassword : TenantScopedEntityBase { }
public class ApiKey : TenantScopedEntityBase { }
public class Invitation : TenantScopedEntityBase { }

// Mail
public class Mailbox : TenantScopedEntityBase
{
    public Guid DomainId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "user";
    public long QuotaBytes { get; set; } = 10L * 1024 * 1024 * 1024;
    public long UsedBytes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MailboxDelegate : TenantScopedEntityBase
{
    public Guid MailboxId { get; set; }
    public Guid UserId { get; set; }
    public string AccessLevel { get; set; } = "read";
}

public class Folder : TenantScopedEntityBase
{
    public Guid MailboxId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "custom";
    public uint UidNext { get; set; } = 1;
    public uint UidValidity { get; set; } = 1;
}

public class Message : TenantScopedEntityBase
{
    public Guid MailboxId { get; set; }
    public Guid FolderId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;
    public string ContentHash { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Flags { get; set; } = string.Empty;
    public uint Uid { get; set; }
    public bool IsRead { get; set; }
    public string? BodyText { get; set; }
    public string? BodyHtml { get; set; }
    public string RawHeaders { get; set; } = string.Empty;
}

public class MessageRecipient : TenantScopedEntityBase
{
    public Guid MessageId { get; set; }
    public string Type { get; set; } = "to";
    public string Address { get; set; } = string.Empty;
}

public class MessageFlag : TenantScopedEntityBase
{
    public Guid MessageId { get; set; }
    public string Flag { get; set; } = string.Empty;
}

public class Attachment : TenantScopedEntityBase
{
    public Guid MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
}

public class Alias : TenantScopedEntityBase
{
    public string Address { get; set; } = string.Empty;
    public string TargetAddress { get; set; } = string.Empty;
}

public class Group : TenantScopedEntityBase
{
    public string Address { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class GroupMember : TenantScopedEntityBase
{
    public Guid GroupId { get; set; }
    public string MemberAddress { get; set; } = string.Empty;
}

public class SieveScript : TenantScopedEntityBase
{
    public Guid MailboxId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

// Flow/policy
public class MailFlowRule : TenantScopedEntityBase
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string ConditionsJson { get; set; } = "[]";
    public string ActionsJson { get; set; } = "[]";
}

public class SmtpQueueItem : TenantScopedEntityBase
{
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
}

public class SmtpDeliveryAttempt : TenantScopedEntityBase
{
    public Guid QueueItemId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ResponseCode { get; set; }
    public double? NextRetryDelaySeconds { get; set; }
}

public class SpamVerdict : TenantScopedEntityBase
{
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public double Score { get; set; }
    public double Threshold { get; set; }
    public bool IsSpam { get; set; }
    public string ReasonsJson { get; set; } = string.Empty;
    public bool DnsblListed { get; set; }
    public bool Greylisted { get; set; }
    public string? SpfResult { get; set; }
    public string? DkimResult { get; set; }
    public string? DmarcResult { get; set; }
}

public class MalwareVerdict : TenantScopedEntityBase
{
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public bool IsMalware { get; set; }
    public string? ThreatName { get; set; }
    public string Engine { get; set; } = "clamav";
}

public class QuarantineItem : TenantScopedEntityBase
{
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public double SpamScore { get; set; }
    public double Threshold { get; set; }
    public string ReasonsJson { get; set; } = string.Empty;
    public string Status { get; set; } = "Quarantined";
    public DateTimeOffset QuarantinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTimeOffset? TrainedAt { get; set; }
    public bool IsDelivered { get; set; }
}
public class DkimKey : TenantScopedEntityBase
{
    public string Selector { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string PublicKeyPem { get; set; } = string.Empty;
}
public class DomainDnsSetting : TenantScopedEntityBase { }

// Operations
public class SecurityEvent : TenantScopedEntityBase { }
public class AuditLog : TenantScopedEntityBase
{
    public string Action { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid? TargetId { get; set; }
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
}
public class SystemEvent : TenantScopedEntityBase { }
public class BackupJob : TenantScopedEntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? ArchivePath { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
public class BackupHistory : TenantScopedEntityBase { }
public class Setting : TenantScopedEntityBase { }
public class Job : TenantScopedEntityBase { }
public class JobDeadLetter : TenantScopedEntityBase { }
public class IdempotencyKey : TenantScopedEntityBase { }
public class Deploy : TenantScopedEntityBase { }

// Licensing
public class License : TenantScopedEntityBase { }
public class LicenseEntitlement : TenantScopedEntityBase { }
public class LicenseActivation : TenantScopedEntityBase { }
public class LicenseUsage : TenantScopedEntityBase { }
public class LicenseEvent : TenantScopedEntityBase { }
