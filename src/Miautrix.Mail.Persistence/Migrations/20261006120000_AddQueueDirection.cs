using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

public partial class AddQueueDirection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "direction",
            table: "smtp_queue",
            type: "text",
            maxLength: 8,
            nullable: true);

        migrationBuilder.Sql("UPDATE smtp_queue SET direction = 'Outbound' WHERE direction IS NULL;");

        migrationBuilder.AlterColumn<string>(
            name: "direction",
            table: "smtp_queue",
            type: "text",
            maxLength: 8,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldMaxLength: 8,
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_smtp_queue_direction",
            table: "smtp_queue",
            sql: "direction IN ('Inbound', 'Outbound')");
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
