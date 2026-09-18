using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddAuditLogColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "audit_logs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "actor_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "details_json",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "target_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_type",
                table: "audit_logs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "action",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "actor_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "details_json",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "target_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "target_type",
                table: "audit_logs");
        }
    }

