using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDomainAntiSpamSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "spam_greylist_score",
            table: "domains",
            type: "double precision",
            nullable: false,
            defaultValue: 4.0);

        migrationBuilder.AddColumn<bool>(
            name: "spam_greylisting_enabled",
            table: "domains",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<double>(
            name: "spam_header_score",
            table: "domains",
            type: "double precision",
            nullable: false,
            defaultValue: 6.0);

        migrationBuilder.AddColumn<double>(
            name: "spam_quarantine_score",
            table: "domains",
            type: "double precision",
            nullable: false,
            defaultValue: 10.0);

        migrationBuilder.AddColumn<double>(
            name: "spam_reject_score",
            table: "domains",
            type: "double precision",
            nullable: false,
            defaultValue: 14.0);

        migrationBuilder.AddColumn<bool>(
            name: "spam_spf_dmarc_enforcement_enabled",
            table: "domains",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "spam_greylist_score",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "spam_greylisting_enabled",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "spam_header_score",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "spam_quarantine_score",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "spam_reject_score",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "spam_spf_dmarc_enforcement_enabled",
            table: "domains");
    }
}
