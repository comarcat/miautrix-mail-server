using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDomainSecurityPolicySettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "lockout_duration_minutes",
            table: "domains",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "lockout_max_failed_attempts",
            table: "domains",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "session_lifetime_minutes",
            table: "domains",
            type: "integer",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "lockout_duration_minutes",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "lockout_max_failed_attempts",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "session_lifetime_minutes",
            table: "domains");
    }
}
