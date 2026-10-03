using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

    /// <inheritdoc />
    public partial class FinalizeCalendarScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
             migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    src_name text := 'idx_calendar_subscriptions_tenant_id';
                    dst_name text;
                    tbl_name text;
                BEGIN
                    SELECT tablename INTO tbl_name
                    FROM pg_indexes
                    WHERE indexname = src_name
                    AND schemaname = 'public'
                    LIMIT 1;

                    IF tbl_name IS NULL THEN
                        RETURN;
                    END IF;

                    dst_name := 'idx_' || tbl_name || '_tenant_id';

                    IF EXISTS (
                        SELECT 1
                        FROM pg_indexes
                        WHERE indexname = dst_name
                        AND schemaname = 'public'
                    ) THEN
                        RETURN;
                    END IF;

                    EXECUTE format('ALTER INDEX %I RENAME TO %I', src_name, dst_name);
                END $$;
                ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "users",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "user_identities",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "user_credentials",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "system_events",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "spam_verdicts",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "smtp_queue",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "smtp_delivery_attempts",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "sieve_scripts",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "settings",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "sessions",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "security_events",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "roles",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "role_permissions",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "refresh_tokens",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "quarantine",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "permissions",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "messages",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "message_recipients",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "message_flags",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "memberships",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "malware_verdicts",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "mailboxes",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "mailbox_delegates",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "mail_signatures",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "mail_flow_rules",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "licenses",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "license_usage",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "license_events",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "license_entitlements",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "license_activations",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "jobs",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "job_dead_letters",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "invitations",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "identity_providers",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "identity_provider_groups",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "idempotency_keys",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "groups",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "group_members",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "folders",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "flag_alert_configurations",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "domains",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "domain_dns_settings",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "dkim_keys",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "deploys",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "contacts",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "calendar_subscriptions",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "calendar_events",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "calendar_event_attendees",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "backup_jobs",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "backup_history",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "audit_logs",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "attachments",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "application_passwords",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "api_keys",
                newName: "idx_calendar_subscriptions_tenant_id");

            migrationBuilder.RenameIndex(
                name: "idx_license_events_tenant_id",
                table: "aliases",
                newName: "idx_calendar_subscriptions_tenant_id");
        }
    }
