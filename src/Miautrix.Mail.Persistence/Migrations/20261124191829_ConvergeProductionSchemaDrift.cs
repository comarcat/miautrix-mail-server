using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <summary>
/// Converges databases created before the v1.0 migrations were finalised (production and dev) with a
/// fresh install. Schema snapshots taken 2026-11-24 showed both were missing <c>flag_alert_configurations</c>
/// (UpdateFlagSystem was recorded as applied but the table did not exist) and carried the queue index from the
/// superseded AddSmtpQueueDirection migration, which fresh installs lacked. Every statement is idempotent,
/// so the same migration is correct on drifted and fresh databases. See ERRORS_AND_ISSUES.md §13.11.
/// </summary>
public partial class ConvergeProductionSchemaDrift : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS flag_alert_configurations (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                mailbox_id uuid NOT NULL,
                flag text NOT NULL,
                alert_configuration_json text NOT NULL,
                created_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL,
                CONSTRAINT "PK_flag_alert_configurations" PRIMARY KEY (id),
                CONSTRAINT "FK_flag_alert_configurations_tenants_tenant_id" FOREIGN KEY (tenant_id)
                    REFERENCES tenants (id) ON DELETE CASCADE
            );
            """);
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS idx_flag_alert_configurations_tenant_id ON flag_alert_configurations (tenant_id);");

        // Keep the existing queue index on drifted databases (rename to the idx_ convention), create it elsewhere.
        migrationBuilder.Sql("ALTER INDEX IF EXISTS \"IX_smtp_queue_tenant_id_direction_status_next_attempt_at\" RENAME TO idx_smtp_queue_tenant_id_direction_status_next_attempt_at;");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS idx_smtp_queue_tenant_id_direction_status_next_attempt_at ON smtp_queue (tenant_id, direction, status, next_attempt_at);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The table and index belong to earlier migrations on fresh installs; only the index is removed here.
        migrationBuilder.DropIndex(
            name: "idx_smtp_queue_tenant_id_direction_status_next_attempt_at",
            table: "smtp_queue");
    }
}
