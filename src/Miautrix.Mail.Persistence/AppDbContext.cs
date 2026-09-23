using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Miautrix.Mail.Domain;

using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<DomainEntity> Domains => Set<DomainEntity>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<IdentityProvider> IdentityProviders => Set<IdentityProvider>();
    public DbSet<IdentityProviderGroup> IdentityProviderGroups => Set<IdentityProviderGroup>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ApplicationPassword> ApplicationPasswords => Set<ApplicationPassword>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
    public DbSet<MailboxDelegate> MailboxDelegates => Set<MailboxDelegate>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageRecipient> MessageRecipients => Set<MessageRecipient>();
    public DbSet<MessageFlag> MessageFlags => Set<MessageFlag>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Alias> Aliases => Set<Alias>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<SieveScript> SieveScripts => Set<SieveScript>();

    public DbSet<MailFlowRule> MailFlowRules => Set<MailFlowRule>();
    public DbSet<SmtpQueueItem> SmtpQueue => Set<SmtpQueueItem>();
    public DbSet<SmtpDeliveryAttempt> SmtpDeliveryAttempts => Set<SmtpDeliveryAttempt>();
    public DbSet<SpamVerdict> SpamVerdicts => Set<SpamVerdict>();
    public DbSet<MalwareVerdict> MalwareVerdicts => Set<MalwareVerdict>();
    public DbSet<QuarantineItem> Quarantine => Set<QuarantineItem>();
    public DbSet<DkimKey> DkimKeys => Set<DkimKey>();
    public DbSet<DomainDnsSetting> DomainDnsSettings => Set<DomainDnsSetting>();

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemEvent> SystemEvents => Set<SystemEvent>();
    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<BackupHistory> BackupHistories => Set<BackupHistory>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobDeadLetter> JobDeadLetters => Set<JobDeadLetter>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<Deploy> Deploys => Set<Deploy>();

    public DbSet<License> Licenses => Set<License>();
    public DbSet<LicenseEntitlement> LicenseEntitlements => Set<LicenseEntitlement>();
    public DbSet<LicenseActivation> LicenseActivations => Set<LicenseActivation>();
    public DbSet<LicenseUsage> LicenseUsages => Set<LicenseUsage>();
    public DbSet<LicenseEvent> LicenseEvents => Set<LicenseEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---- Global tenants table (not tenant_id scoped) ----
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Slug)
                .HasColumnName("slug")
                .IsRequired();
        });

        // ---- Tenant-scoped tables (TPC mapping) ----
        modelBuilder.Entity<TenantScopedEntityBase>().UseTpcMappingStrategy();
        modelBuilder.Entity<TenantScopedEntityBase>().HasKey(e => e.Id);

        modelBuilder.Entity<TenantScopedEntityBase>()
            .Property(e => e.Id).HasColumnName("id");
        modelBuilder.Entity<TenantScopedEntityBase>()
            .Property(e => e.CreatedAt).HasColumnName("created_at");
        modelBuilder.Entity<TenantScopedEntityBase>()
            .Property(e => e.UpdatedAt).HasColumnName("updated_at");
        modelBuilder.Entity<TenantScopedEntityBase>()
            .Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();

        ConfigureTenantScoped<DomainEntity>(modelBuilder, "domains");
        modelBuilder.Entity<DomainEntity>(entity =>
        {
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.IsVerified).HasColumnName("is_verified");
            entity.Property(e => e.MfaEnforced).HasColumnName("mfa_enforced").HasDefaultValue(false).IsRequired();
            entity.Property(e => e.SessionLifetimeMinutes).HasColumnName("session_lifetime_minutes");
            entity.Property(e => e.LockoutMaxFailedAttempts).HasColumnName("lockout_max_failed_attempts");
            entity.Property(e => e.LockoutDurationMinutes).HasColumnName("lockout_duration_minutes");
            entity.Property(e => e.DkimSelector).HasColumnName("dkim_selector");
            entity.Property(e => e.DkimPublicKey).HasColumnName("dkim_public_key");
            entity.Property(e => e.SpfRecord).HasColumnName("spf_record");
            entity.Property(e => e.DmarcRecord).HasColumnName("dmarc_record");
            entity.Property(e => e.IsPrimary).HasColumnName("is_primary");
            // Defaulted so the migration backfills existing rows to "local" in the same statement:
            // an EXPAND, safe to apply before the new code ships.
            entity.Property(e => e.TransportMode)
                .HasColumnName("transport_mode")
                .HasDefaultValue(DomainTransportModes.Local)
                .IsRequired();
            entity.Property(e => e.CloudflareZoneId).HasColumnName("cloudflare_zone_id");
            entity.Property(e => e.CloudflareWorkerUrl).HasColumnName("cloudflare_worker_url");
        });

        ConfigureTenantScoped<User>(modelBuilder, "users");
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.Email).HasColumnName("email").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.IsService).HasColumnName("is_service");
        });

        ConfigureTenantScoped<UserCredential>(modelBuilder, "user_credentials");
        modelBuilder.Entity<UserCredential>(entity =>
        {
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(e => e.Algorithm).HasColumnName("algorithm").IsRequired();
            entity.Property(e => e.MustChangePassword).HasColumnName("must_change_password");
        });
        ConfigureTenantScoped<UserIdentity>(modelBuilder, "user_identities");
        ConfigureTenantScoped<IdentityProvider>(modelBuilder, "identity_providers");
        ConfigureTenantScoped<IdentityProviderGroup>(modelBuilder, "identity_provider_groups");
        ConfigureTenantScoped<Role>(modelBuilder, "roles");
        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        ConfigureTenantScoped<Permission>(modelBuilder, "permissions");
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Name).HasColumnName("name");
        });
        ConfigureTenantScoped<RolePermission>(modelBuilder, "role_permissions");
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
        });
        ConfigureTenantScoped<Membership>(modelBuilder, "memberships");
        modelBuilder.Entity<Membership>(entity =>
        {
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
        });
        ConfigureTenantScoped<Session>(modelBuilder, "sessions");
        ConfigureTenantScoped<RefreshToken>(modelBuilder, "refresh_tokens");
        ConfigureTenantScoped<ApplicationPassword>(modelBuilder, "application_passwords");
        ConfigureTenantScoped<ApiKey>(modelBuilder, "api_keys");
        ConfigureTenantScoped<Invitation>(modelBuilder, "invitations");

        ConfigureTenantScoped<Mailbox>(modelBuilder, "mailboxes");
        modelBuilder.Entity<Mailbox>(entity =>
        {
            entity.Property(e => e.DomainId).HasColumnName("domain_id");
            entity.Property(e => e.Address).HasColumnName("address").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Kind).HasColumnName("kind").IsRequired();
            entity.Property(e => e.QuotaBytes).HasColumnName("quota_bytes");
            entity.Property(e => e.UsedBytes).HasColumnName("used_bytes");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
        });

        ConfigureTenantScoped<MailboxDelegate>(modelBuilder, "mailbox_delegates");
        modelBuilder.Entity<MailboxDelegate>(entity =>
        {
            entity.Property(e => e.MailboxId).HasColumnName("mailbox_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AccessLevel).HasColumnName("access_level").IsRequired();

            entity.HasIndex(e => new { e.MailboxId, e.UserId })
                .IsUnique()
                .HasDatabaseName("uq_mailbox_delegates_mailbox_id_user_id");
        });

        ConfigureTenantScoped<Folder>(modelBuilder, "folders");
        modelBuilder.Entity<Folder>(entity =>
        {
            entity.Property(e => e.MailboxId).HasColumnName("mailbox_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Role).HasColumnName("role").IsRequired();
            entity.Property(e => e.UidNext).HasColumnName("uid_next");
            entity.Property(e => e.UidValidity).HasColumnName("uid_validity");
        });

        ConfigureTenantScoped<Message>(modelBuilder, "messages");
        modelBuilder.Entity<Message>(entity =>
        {
            entity.Property(e => e.MailboxId).HasColumnName("mailbox_id");
            entity.Property(e => e.FolderId).HasColumnName("folder_id");
            entity.Property(e => e.Sender).HasColumnName("sender").IsRequired();
            entity.Property(e => e.Recipient).HasColumnName("recipient").IsRequired();
            entity.Property(e => e.Subject).HasColumnName("subject").IsRequired();
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.ContentHash).HasColumnName("content_hash").IsRequired();
            entity.Property(e => e.StoragePath).HasColumnName("storage_path").IsRequired();
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
            entity.Property(e => e.Flags).HasColumnName("flags");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.BodyText).HasColumnName("body_text");
            entity.Property(e => e.BodyHtml).HasColumnName("body_html");
            entity.Property(e => e.RawHeaders).HasColumnName("raw_headers");
        });

        ConfigureTenantScoped<MessageRecipient>(modelBuilder, "message_recipients");
        modelBuilder.Entity<MessageRecipient>(entity =>
        {
            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.Address).HasColumnName("address");
        });

        ConfigureTenantScoped<MessageFlag>(modelBuilder, "message_flags");
        modelBuilder.Entity<MessageFlag>(entity =>
        {
            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.Flag).HasColumnName("flag");
        });

        ConfigureTenantScoped<Attachment>(modelBuilder, "attachments");
        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.FileName).HasColumnName("file_name").IsRequired();
            entity.Property(e => e.ContentType).HasColumnName("content_type").IsRequired();
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
            entity.Property(e => e.ContentHash).HasColumnName("content_hash").IsRequired();
            entity.Property(e => e.StoragePath).HasColumnName("storage_path").IsRequired();
        });

        ConfigureTenantScoped<Alias>(modelBuilder, "aliases");
        modelBuilder.Entity<Alias>(entity =>
        {
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.TargetAddress).HasColumnName("target_address");
        });

        ConfigureTenantScoped<Group>(modelBuilder, "groups");
        modelBuilder.Entity<Group>(entity =>
        {
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        ConfigureTenantScoped<GroupMember>(modelBuilder, "group_members");
        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.MemberAddress).HasColumnName("member_address");
        });

        ConfigureTenantScoped<SieveScript>(modelBuilder, "sieve_scripts");
        modelBuilder.Entity<SieveScript>(entity =>
        {
            entity.Property(e => e.MailboxId).HasColumnName("mailbox_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active");
        });

        ConfigureTenantScoped<MailFlowRule>(modelBuilder, "mail_flow_rules");
        modelBuilder.Entity<MailFlowRule>(entity =>
        {
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Priority).HasColumnName("priority");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled");
            entity.Property(e => e.ConditionsJson).HasColumnName("conditions_json");
            entity.Property(e => e.ActionsJson).HasColumnName("actions_json");
        });

        ConfigureTenantScoped<SmtpQueueItem>(modelBuilder, "smtp_queue");
        modelBuilder.Entity<SmtpQueueItem>(entity =>
        {
            entity.Property(e => e.Sender).HasColumnName("sender").IsRequired();
            entity.Property(e => e.Recipient).HasColumnName("recipient").IsRequired();
            entity.Property(e => e.Subject).HasColumnName("subject");
            entity.Property(e => e.RawMessage).HasColumnName("raw_message").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.Attempts).HasColumnName("attempts");
            entity.Property(e => e.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(e => e.LastAttemptAt).HasColumnName("last_attempt_at");
            entity.Property(e => e.LastError).HasColumnName("last_error");
        });

        ConfigureTenantScoped<SmtpDeliveryAttempt>(modelBuilder, "smtp_delivery_attempts");
        modelBuilder.Entity<SmtpDeliveryAttempt>(entity =>
        {
            entity.Property(e => e.QueueItemId).HasColumnName("queue_item_id");
            entity.Property(e => e.AttemptNumber).HasColumnName("attempt_number");
            entity.Property(e => e.AttemptedAt).HasColumnName("attempted_at");
            entity.Property(e => e.Success).HasColumnName("success");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.ResponseCode).HasColumnName("response_code");
            entity.Property(e => e.NextRetryDelaySeconds).HasColumnName("next_retry_delay_seconds");
        });

        ConfigureTenantScoped<SpamVerdict>(modelBuilder, "spam_verdicts");
        modelBuilder.Entity<SpamVerdict>(entity =>
        {
            entity.Property(e => e.Sender).HasColumnName("sender");
            entity.Property(e => e.Recipient).HasColumnName("recipient");
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.Threshold).HasColumnName("threshold");
            entity.Property(e => e.IsSpam).HasColumnName("is_spam");
            entity.Property(e => e.ReasonsJson).HasColumnName("reasons_json");
            entity.Property(e => e.DnsblListed).HasColumnName("dnsbl_listed");
            entity.Property(e => e.Greylisted).HasColumnName("greylisted");
            entity.Property(e => e.SpfResult).HasColumnName("spf_result");
            entity.Property(e => e.DkimResult).HasColumnName("dkim_result");
            entity.Property(e => e.DmarcResult).HasColumnName("dmarc_result");
        });

        ConfigureTenantScoped<MalwareVerdict>(modelBuilder, "malware_verdicts");
        modelBuilder.Entity<MalwareVerdict>(entity =>
        {
            entity.Property(e => e.Sender).HasColumnName("sender");
            entity.Property(e => e.Recipient).HasColumnName("recipient");
            entity.Property(e => e.IsMalware).HasColumnName("is_malware");
            entity.Property(e => e.ThreatName).HasColumnName("threat_name");
            entity.Property(e => e.Engine).HasColumnName("engine");
        });

        ConfigureTenantScoped<QuarantineItem>(modelBuilder, "quarantine");
        modelBuilder.Entity<QuarantineItem>(entity =>
        {
            entity.Property(e => e.Sender).HasColumnName("sender").IsRequired();
            entity.Property(e => e.Recipient).HasColumnName("recipient").IsRequired();
            entity.Property(e => e.Subject).HasColumnName("subject");
            entity.Property(e => e.RawMessage).HasColumnName("raw_message").IsRequired();
            entity.Property(e => e.SpamScore).HasColumnName("spam_score");
            entity.Property(e => e.Threshold).HasColumnName("threshold");
            entity.Property(e => e.ReasonsJson).HasColumnName("reasons_json");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.QuarantinedAt).HasColumnName("quarantined_at");
            entity.Property(e => e.ReleasedAt).HasColumnName("released_at");
            entity.Property(e => e.TrainedAt).HasColumnName("trained_at");
            entity.Property(e => e.IsDelivered).HasColumnName("is_delivered");
        });

        ConfigureTenantScoped<DkimKey>(modelBuilder, "dkim_keys");
        modelBuilder.Entity<DkimKey>(entity =>
        {
            entity.Property(e => e.Selector).HasColumnName("selector");
            entity.Property(e => e.DomainName).HasColumnName("domain_name");
            entity.Property(e => e.PrivateKeyPem).HasColumnName("private_key_pem");
            entity.Property(e => e.PublicKeyPem).HasColumnName("public_key_pem");
        });
        ConfigureTenantScoped<DomainDnsSetting>(modelBuilder, "domain_dns_settings");

        ConfigureTenantScoped<SecurityEvent>(modelBuilder, "security_events");
        ConfigureTenantScoped<AuditLog>(modelBuilder, "audit_logs");
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.Action).HasColumnName("action").IsRequired();
            entity.Property(e => e.ActorId).HasColumnName("actor_id");
            entity.Property(e => e.TargetType).HasColumnName("target_type");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.DetailsJson).HasColumnName("details_json");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
        });
        ConfigureTenantScoped<SystemEvent>(modelBuilder, "system_events");
        ConfigureTenantScoped<BackupJob>(modelBuilder, "backup_jobs");
        modelBuilder.Entity<BackupJob>(entity =>
        {
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.ArchivePath).HasColumnName("archive_path");
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
        });
        ConfigureTenantScoped<BackupHistory>(modelBuilder, "backup_history");
        ConfigureTenantScoped<Setting>(modelBuilder, "settings");
        ConfigureTenantScoped<Job>(modelBuilder, "jobs");
        ConfigureTenantScoped<JobDeadLetter>(modelBuilder, "job_dead_letters");
        ConfigureTenantScoped<IdempotencyKey>(modelBuilder, "idempotency_keys");
        ConfigureTenantScoped<Deploy>(modelBuilder, "deploys");

        ConfigureTenantScoped<License>(modelBuilder, "licenses");
        ConfigureTenantScoped<LicenseEntitlement>(modelBuilder, "license_entitlements");
        ConfigureTenantScoped<LicenseActivation>(modelBuilder, "license_activations");
        ConfigureTenantScoped<LicenseUsage>(modelBuilder, "license_usage");
        ConfigureTenantScoped<LicenseEvent>(modelBuilder, "license_events");
    }

    private static void ConfigureTenantScoped<TEntity>(ModelBuilder modelBuilder, string table)
        where TEntity : TenantScopedEntityBase
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.ToTable(table);
            entity.HasIndex(e => e.TenantId).HasDatabaseName($"idx_{table}_tenant_id");
        });
    }
}

internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            conn = "Host=localhost;Database=miautrix_dev;Username=postgres;Password=postgres";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn, npgsqlOptions =>
            {
            })
            .Options;

        return new AppDbContext(options);
    }
}
