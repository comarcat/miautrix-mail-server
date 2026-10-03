using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCalendarSchedulingAndSubscriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "calendar_events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sequence",
                table: "calendar_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "calendar_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_calendar_subscriptions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_calendar_subscriptions_tenant_id_user_id",
                table: "calendar_subscriptions",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "idx_calendar_subscriptions_tenant_id",
                table: "calendar_subscriptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_calendar_subscriptions_tenant_id_user_id_target_user_id",
                table: "calendar_subscriptions",
                columns: new[] { "tenant_id", "user_id", "target_user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_subscriptions");

            migrationBuilder.DropColumn(
                name: "description",
                table: "calendar_events");

            migrationBuilder.DropColumn(
                name: "sequence",
                table: "calendar_events");
        }
}
