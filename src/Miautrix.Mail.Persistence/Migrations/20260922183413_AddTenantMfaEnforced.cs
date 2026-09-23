using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTenantMfaEnforced : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "mfa_enforced",
            table: "tenants",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "mfa_enforced",
            table: "tenants");
    }
}
