using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class CalendarEventRecurrence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "recurrence_frequency",
            table: "calendar_events",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "recurrence_interval",
            table: "calendar_events",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "recurrence_until",
            table: "calendar_events",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "recurrence_frequency",
            table: "calendar_events");

        migrationBuilder.DropColumn(
            name: "recurrence_interval",
            table: "calendar_events");

        migrationBuilder.DropColumn(
            name: "recurrence_until",
            table: "calendar_events");
    }
}
