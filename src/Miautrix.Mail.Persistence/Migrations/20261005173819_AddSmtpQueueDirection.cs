using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSmtpQueueDirection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "direction",
            table: "smtp_queue",
            type: "text",
            nullable: false,
            defaultValue: "Inbound");

        migrationBuilder.Sql("""
            UPDATE smtp_queue q
            SET direction = CASE
                WHEN EXISTS (
                    SELECT 1
                    FROM domains d
                    WHERE d.tenant_id = q.tenant_id
                      AND lower(d.name) = lower(split_part(q.recipient, '@', 2))
                ) THEN 'Inbound'
                ELSE 'Outbound'
            END;
            """);

        migrationBuilder.Sql("""
            UPDATE smtp_queue
            SET status = 'Pending',
                attempts = 0,
                last_error = NULL,
                next_attempt_at = now()
            WHERE direction = 'Inbound'
              AND status IN ('Failed', 'DeadLetter')
              AND (
                  last_error ILIKE '%Cloudflare%'
                  OR last_error ILIKE '%send_failed%'
                  OR last_error ILIKE '%502%'
              );
            """);

        migrationBuilder.CreateIndex(
            name: "IX_smtp_queue_tenant_id_direction_status_next_attempt_at",
            table: "smtp_queue",
            columns: new[] { "tenant_id", "direction", "status", "next_attempt_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_smtp_queue_tenant_id_direction_status_next_attempt_at",
            table: "smtp_queue");

        migrationBuilder.DropColumn(
            name: "direction",
            table: "smtp_queue");
    }
}
