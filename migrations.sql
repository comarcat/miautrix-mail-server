CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE tenants (
        id uuid NOT NULL,
        slug text NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_tenants" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE domains (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_domains" PRIMARY KEY (id),
        CONSTRAINT "FK_domains_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_domains_tenant_id ON domains (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_users" PRIMARY KEY (id),
        CONSTRAINT "FK_users_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_users_tenant_id ON users (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE user_credentials (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_user_credentials" PRIMARY KEY (id),
        CONSTRAINT "FK_user_credentials_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_user_credentials_tenant_id ON user_credentials (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE user_identities (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_user_identities" PRIMARY KEY (id),
        CONSTRAINT "FK_user_identities_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_user_identities_tenant_id ON user_identities (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE identity_providers (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_identity_providers" PRIMARY KEY (id),
        CONSTRAINT "FK_identity_providers_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_identity_providers_tenant_id ON identity_providers (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE identity_provider_groups (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_identity_provider_groups" PRIMARY KEY (id),
        CONSTRAINT "FK_identity_provider_groups_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_identity_provider_groups_tenant_id ON identity_provider_groups (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE roles (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_roles" PRIMARY KEY (id),
        CONSTRAINT "FK_roles_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_roles_tenant_id ON roles (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE permissions (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_permissions" PRIMARY KEY (id),
        CONSTRAINT "FK_permissions_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_permissions_tenant_id ON permissions (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE role_permissions (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_role_permissions" PRIMARY KEY (id),
        CONSTRAINT "FK_role_permissions_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_role_permissions_tenant_id ON role_permissions (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE memberships (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_memberships" PRIMARY KEY (id),
        CONSTRAINT "FK_memberships_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_memberships_tenant_id ON memberships (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE sessions (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_sessions" PRIMARY KEY (id),
        CONSTRAINT "FK_sessions_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_sessions_tenant_id ON sessions (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE refresh_tokens (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY (id),
        CONSTRAINT "FK_refresh_tokens_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_refresh_tokens_tenant_id ON refresh_tokens (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE application_passwords (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_application_passwords" PRIMARY KEY (id),
        CONSTRAINT "FK_application_passwords_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_application_passwords_tenant_id ON application_passwords (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE api_keys (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_api_keys" PRIMARY KEY (id),
        CONSTRAINT "FK_api_keys_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_api_keys_tenant_id ON api_keys (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE invitations (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_invitations" PRIMARY KEY (id),
        CONSTRAINT "FK_invitations_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_invitations_tenant_id ON invitations (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE mailboxes (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_mailboxes" PRIMARY KEY (id),
        CONSTRAINT "FK_mailboxes_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_mailboxes_tenant_id ON mailboxes (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE folders (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_folders" PRIMARY KEY (id),
        CONSTRAINT "FK_folders_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_folders_tenant_id ON folders (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE messages (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_messages" PRIMARY KEY (id),
        CONSTRAINT "FK_messages_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_messages_tenant_id ON messages (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE message_recipients (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_message_recipients" PRIMARY KEY (id),
        CONSTRAINT "FK_message_recipients_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_message_recipients_tenant_id ON message_recipients (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE message_flags (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_message_flags" PRIMARY KEY (id),
        CONSTRAINT "FK_message_flags_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_message_flags_tenant_id ON message_flags (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE attachments (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_attachments" PRIMARY KEY (id),
        CONSTRAINT "FK_attachments_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_attachments_tenant_id ON attachments (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE aliases (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_aliases" PRIMARY KEY (id),
        CONSTRAINT "FK_aliases_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_aliases_tenant_id ON aliases (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE groups (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_groups" PRIMARY KEY (id),
        CONSTRAINT "FK_groups_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_groups_tenant_id ON groups (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE group_members (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_group_members" PRIMARY KEY (id),
        CONSTRAINT "FK_group_members_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_group_members_tenant_id ON group_members (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE sieve_scripts (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_sieve_scripts" PRIMARY KEY (id),
        CONSTRAINT "FK_sieve_scripts_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_sieve_scripts_tenant_id ON sieve_scripts (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE mail_flow_rules (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_mail_flow_rules" PRIMARY KEY (id),
        CONSTRAINT "FK_mail_flow_rules_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_mail_flow_rules_tenant_id ON mail_flow_rules (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE smtp_queue (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_smtp_queue" PRIMARY KEY (id),
        CONSTRAINT "FK_smtp_queue_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_smtp_queue_tenant_id ON smtp_queue (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE smtp_delivery_attempts (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_smtp_delivery_attempts" PRIMARY KEY (id),
        CONSTRAINT "FK_smtp_delivery_attempts_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_smtp_delivery_attempts_tenant_id ON smtp_delivery_attempts (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE spam_verdicts (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_spam_verdicts" PRIMARY KEY (id),
        CONSTRAINT "FK_spam_verdicts_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_spam_verdicts_tenant_id ON spam_verdicts (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE malware_verdicts (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_malware_verdicts" PRIMARY KEY (id),
        CONSTRAINT "FK_malware_verdicts_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_malware_verdicts_tenant_id ON malware_verdicts (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE quarantine (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_quarantine" PRIMARY KEY (id),
        CONSTRAINT "FK_quarantine_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_quarantine_tenant_id ON quarantine (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE dkim_keys (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_dkim_keys" PRIMARY KEY (id),
        CONSTRAINT "FK_dkim_keys_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_dkim_keys_tenant_id ON dkim_keys (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE domain_dns_settings (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_domain_dns_settings" PRIMARY KEY (id),
        CONSTRAINT "FK_domain_dns_settings_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_domain_dns_settings_tenant_id ON domain_dns_settings (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE security_events (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_security_events" PRIMARY KEY (id),
        CONSTRAINT "FK_security_events_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_security_events_tenant_id ON security_events (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE audit_logs (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_audit_logs" PRIMARY KEY (id),
        CONSTRAINT "FK_audit_logs_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_audit_logs_tenant_id ON audit_logs (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE system_events (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_system_events" PRIMARY KEY (id),
        CONSTRAINT "FK_system_events_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_system_events_tenant_id ON system_events (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE backup_jobs (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_backup_jobs" PRIMARY KEY (id),
        CONSTRAINT "FK_backup_jobs_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_backup_jobs_tenant_id ON backup_jobs (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE backup_history (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_backup_history" PRIMARY KEY (id),
        CONSTRAINT "FK_backup_history_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_backup_history_tenant_id ON backup_history (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE settings (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_settings" PRIMARY KEY (id),
        CONSTRAINT "FK_settings_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_settings_tenant_id ON settings (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE jobs (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_jobs" PRIMARY KEY (id),
        CONSTRAINT "FK_jobs_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_jobs_tenant_id ON jobs (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE job_dead_letters (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_job_dead_letters" PRIMARY KEY (id),
        CONSTRAINT "FK_job_dead_letters_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_job_dead_letters_tenant_id ON job_dead_letters (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE idempotency_keys (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_idempotency_keys" PRIMARY KEY (id),
        CONSTRAINT "FK_idempotency_keys_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_idempotency_keys_tenant_id ON idempotency_keys (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE deploys (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_deploys" PRIMARY KEY (id),
        CONSTRAINT "FK_deploys_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_deploys_tenant_id ON deploys (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE licenses (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_licenses" PRIMARY KEY (id),
        CONSTRAINT "FK_licenses_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_licenses_tenant_id ON licenses (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE license_entitlements (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_license_entitlements" PRIMARY KEY (id),
        CONSTRAINT "FK_license_entitlements_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_license_entitlements_tenant_id ON license_entitlements (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE license_activations (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_license_activations" PRIMARY KEY (id),
        CONSTRAINT "FK_license_activations_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_license_activations_tenant_id ON license_activations (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE license_usage (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_license_usage" PRIMARY KEY (id),
        CONSTRAINT "FK_license_usage_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_license_usage_tenant_id ON license_usage (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE TABLE license_events (
        id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        tenant_id uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_license_events" PRIMARY KEY (id),
        CONSTRAINT "FK_license_events_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    CREATE INDEX idx_license_events_tenant_id ON license_events (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918021252_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260918021252_InitialCreate', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918030618_AddCatalogFields') THEN
    DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='permissions' AND column_name='code') THEN ALTER TABLE permissions ADD COLUMN code text; END IF; END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918030618_AddCatalogFields') THEN
    DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='permissions' AND column_name='name') THEN ALTER TABLE permissions ADD COLUMN name text; END IF; END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918030618_AddCatalogFields') THEN
    DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='roles' AND column_name='code') THEN ALTER TABLE roles ADD COLUMN code text; END IF; END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918030618_AddCatalogFields') THEN
    DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='roles' AND column_name='name') THEN ALTER TABLE roles ADD COLUMN name text; END IF; END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918030618_AddCatalogFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260918030618_AddCatalogFields', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034003_AddAuditLogColumns') THEN
    ALTER TABLE audit_logs ADD action text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034003_AddAuditLogColumns') THEN
    ALTER TABLE audit_logs ADD actor_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034003_AddAuditLogColumns') THEN
    ALTER TABLE audit_logs ADD details_json text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034003_AddAuditLogColumns') THEN
    ALTER TABLE audit_logs ADD ip_address text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034003_AddAuditLogColumns') THEN
    ALTER TABLE audit_logs ADD target_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034003_AddAuditLogColumns') THEN
    ALTER TABLE audit_logs ADD target_type text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034003_AddAuditLogColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260918034003_AddAuditLogColumns', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_queue ADD attempts integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_queue ADD last_attempt_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_queue ADD last_error text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_queue ADD next_attempt_at timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_queue ADD raw_message text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_queue ADD recipient text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_queue ADD sender text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_queue ADD status text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_queue ADD subject text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_delivery_attempts ADD attempt_number integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_delivery_attempts ADD attempted_at timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_delivery_attempts ADD error_message text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_delivery_attempts ADD next_retry_delay_seconds double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_delivery_attempts ADD queue_item_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_delivery_attempts ADD response_code integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    ALTER TABLE smtp_delivery_attempts ADD success boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918034216_AddSmtpQueueColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260918034216_AddSmtpQueueColumns', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD dkim_result text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD dmarc_result text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD dnsbl_listed boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD greylisted boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD is_spam boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD reasons_json text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD recipient text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD score double precision NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD sender text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD spf_result text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE spam_verdicts ADD threshold double precision NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE sieve_scripts ADD content text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE sieve_scripts ADD is_active boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE sieve_scripts ADD mailbox_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE sieve_scripts ADD name text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD is_delivered boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD quarantined_at timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD raw_message text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD reasons_json text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD recipient text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD released_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD sender text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD spam_score double precision NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD status text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD subject text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD threshold double precision NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE quarantine ADD trained_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD body_html text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD body_text text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD content_hash text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD date timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD flags text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD folder_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD is_read boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD mailbox_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD raw_headers text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD recipient text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD sender text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD size_bytes bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD storage_path text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD subject text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE messages ADD uid bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE message_recipients ADD address text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE message_recipients ADD message_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE message_recipients ADD type text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE message_flags ADD flag text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE message_flags ADD message_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE malware_verdicts ADD engine text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE malware_verdicts ADD is_malware boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE malware_verdicts ADD recipient text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE malware_verdicts ADD sender text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE malware_verdicts ADD threat_name text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mailboxes ADD address text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mailboxes ADD domain_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mailboxes ADD is_active boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mailboxes ADD quota_bytes bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mailboxes ADD used_bytes bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mail_flow_rules ADD actions_json text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mail_flow_rules ADD conditions_json text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mail_flow_rules ADD is_enabled boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mail_flow_rules ADD name text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE mail_flow_rules ADD priority integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE groups ADD address text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE groups ADD name text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE group_members ADD group_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE group_members ADD member_address text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE folders ADD mailbox_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE folders ADD name text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE folders ADD role text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE folders ADD uid_next bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE folders ADD uid_validity bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE dkim_keys ADD domain_name text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE dkim_keys ADD private_key_pem text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE dkim_keys ADD public_key_pem text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE dkim_keys ADD selector text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE attachments ADD content_hash text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE attachments ADD content_type text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE attachments ADD file_name text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE attachments ADD message_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE attachments ADD size_bytes bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE attachments ADD storage_path text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE aliases ADD address text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    ALTER TABLE aliases ADD target_address text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918035154_AddMailAndSpamColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260918035154_AddMailAndSpamColumns', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918181732_AddMembershipAndRolePermissionFks') THEN
    ALTER TABLE role_permissions ADD permission_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918181732_AddMembershipAndRolePermissionFks') THEN
    ALTER TABLE role_permissions ADD role_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918181732_AddMembershipAndRolePermissionFks') THEN
    ALTER TABLE memberships ADD role_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918181732_AddMembershipAndRolePermissionFks') THEN
    ALTER TABLE memberships ADD user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918181732_AddMembershipAndRolePermissionFks') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260918181732_AddMembershipAndRolePermissionFks', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE users ADD email text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE users ADD is_active boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE users ADD name text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE user_credentials ADD algorithm text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE user_credentials ADD must_change_password boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE user_credentials ADD password_hash text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE user_credentials ADD user_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE domains ADD dkim_public_key text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE domains ADD dkim_selector text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE domains ADD dmarc_record text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE domains ADD is_primary boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE domains ADD is_verified boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE domains ADD name text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    ALTER TABLE domains ADD spf_record text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919031054_AddUserAndDomainFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260919031054_AddUserAndDomainFields', '10.0.4');
    END IF;
END $EF$;
COMMIT;

