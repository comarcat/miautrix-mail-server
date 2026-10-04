using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddSmtpQueueColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempts",
                table: "smtp_queue",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_attempt_at",
                table: "smtp_queue",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "smtp_queue",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "smtp_queue",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "raw_message",
                table: "smtp_queue",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipient",
                table: "smtp_queue",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "sender",
                table: "smtp_queue",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "smtp_queue",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "subject",
                table: "smtp_queue",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attempt_number",
                table: "smtp_delivery_attempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "attempted_at",
                table: "smtp_delivery_attempts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "smtp_delivery_attempts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "next_retry_delay_seconds",
                table: "smtp_delivery_attempts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "queue_item_id",
                table: "smtp_delivery_attempts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "response_code",
                table: "smtp_delivery_attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "success",
                table: "smtp_delivery_attempts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attempts",
                table: "smtp_queue");

            migrationBuilder.DropColumn(
                name: "last_attempt_at",
                table: "smtp_queue");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "smtp_queue");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "smtp_queue");

            migrationBuilder.DropColumn(
                name: "raw_message",
                table: "smtp_queue");

            migrationBuilder.DropColumn(
                name: "recipient",
                table: "smtp_queue");

            migrationBuilder.DropColumn(
                name: "sender",
                table: "smtp_queue");

            migrationBuilder.DropColumn(
                name: "status",
                table: "smtp_queue");

            migrationBuilder.DropColumn(
                name: "subject",
                table: "smtp_queue");

            migrationBuilder.DropColumn(
                name: "attempt_number",
                table: "smtp_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "attempted_at",
                table: "smtp_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "smtp_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "next_retry_delay_seconds",
                table: "smtp_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "queue_item_id",
                table: "smtp_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "response_code",
                table: "smtp_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "success",
                table: "smtp_delivery_attempts");
        }
    }

