using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBackupJobColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "archive_path",
            table: "backup_jobs",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "completed_at",
            table: "backup_jobs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "error_message",
            table: "backup_jobs",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "name",
            table: "backup_jobs",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<long>(
            name: "size_bytes",
            table: "backup_jobs",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "backup_jobs",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "archive_path",
            table: "backup_jobs");

        migrationBuilder.DropColumn(
            name: "completed_at",
            table: "backup_jobs");

        migrationBuilder.DropColumn(
            name: "error_message",
            table: "backup_jobs");

        migrationBuilder.DropColumn(
            name: "name",
            table: "backup_jobs");

        migrationBuilder.DropColumn(
            name: "size_bytes",
            table: "backup_jobs");

        migrationBuilder.DropColumn(
            name: "status",
            table: "backup_jobs");
    }
}
