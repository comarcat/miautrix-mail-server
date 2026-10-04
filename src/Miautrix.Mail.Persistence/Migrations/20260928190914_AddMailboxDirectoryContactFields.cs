using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMailboxDirectoryContactFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "department",
            table: "mailboxes",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "organization",
            table: "mailboxes",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "phone",
            table: "mailboxes",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "department",
            table: "mailboxes");

        migrationBuilder.DropColumn(
            name: "organization",
            table: "mailboxes");

        migrationBuilder.DropColumn(
            name: "phone",
            table: "mailboxes");
    }
}
