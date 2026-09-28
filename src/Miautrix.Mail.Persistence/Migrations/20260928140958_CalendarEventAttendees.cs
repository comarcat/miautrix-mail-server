using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class CalendarEventAttendees : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "calendar_event_attendees",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                email = table.Column<string>(type: "text", nullable: false),
                display_name = table.Column<string>(type: "text", nullable: true),
                role = table.Column<string>(type: "text", nullable: false),
                is_external = table.Column<bool>(type: "boolean", nullable: false),
                response_status = table.Column<string>(type: "text", nullable: false),
                responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                token_hash = table.Column<string>(type: "text", nullable: true),
                token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                proposed_start_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                proposed_end_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                proposal_note = table.Column<string>(type: "text", nullable: true),
                mirrored_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_calendar_event_attendees", x => x.id);
                table.ForeignKey(
                    name: "FK_calendar_event_attendees_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_calendar_event_attendees_tenant_id_email",
            table: "calendar_event_attendees",
            columns: new[] { "tenant_id", "email" });

        migrationBuilder.CreateIndex(
            name: "idx_calendar_event_attendees_tenant_id_event_id",
            table: "calendar_event_attendees",
            columns: new[] { "tenant_id", "event_id" });

        migrationBuilder.CreateIndex(
            name: "idx_calendar_event_attendees_tenant_id",
            table: "calendar_event_attendees",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "uq_calendar_event_attendees_tenant_id_event_id_email",
            table: "calendar_event_attendees",
            columns: new[] { "tenant_id", "event_id", "email" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_calendar_event_attendees_token_hash",
            table: "calendar_event_attendees",
            column: "token_hash",
            unique: true,
            filter: "token_hash IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "calendar_event_attendees");
    }
}
