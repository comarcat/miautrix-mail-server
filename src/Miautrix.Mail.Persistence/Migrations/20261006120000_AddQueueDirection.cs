using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <summary>
/// Adds smtp_queue.direction. Written to be idempotent because production databases that ran the
/// superseded 20261005173819_AddSmtpQueueDirection migration already have the column (text, NOT NULL,
/// DEFAULT 'Inbound') but no check constraint and no history row for this migration. On those databases
/// this converges the column to the fresh-install shape; on a fresh database it behaves as before.
/// See ERRORS_AND_ISSUES.md §13.10.
/// </summary>
public partial class AddQueueDirection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE smtp_queue ADD COLUMN IF NOT EXISTS direction text;");
        migrationBuilder.Sql("UPDATE smtp_queue SET direction = 'Outbound' WHERE direction IS NULL;");
        migrationBuilder.Sql("ALTER TABLE smtp_queue ALTER COLUMN direction DROP DEFAULT;");
        migrationBuilder.Sql("ALTER TABLE smtp_queue ALTER COLUMN direction SET NOT NULL;");
        migrationBuilder.Sql("ALTER TABLE smtp_queue DROP CONSTRAINT IF EXISTS ck_smtp_queue_direction;");
        migrationBuilder.Sql("ALTER TABLE smtp_queue ADD CONSTRAINT ck_smtp_queue_direction CHECK (direction IN ('Inbound', 'Outbound'));");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_smtp_queue_direction",
            table: "smtp_queue");
        migrationBuilder.DropColumn(
            name: "direction",
            table: "smtp_queue");
    }
}
