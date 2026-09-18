using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMembershipAndRolePermissionFks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "permission_id",
            table: "role_permissions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "role_id",
            table: "role_permissions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "role_id",
            table: "memberships",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "user_id",
            table: "memberships",
            type: "uuid",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "permission_id",
            table: "role_permissions");

        migrationBuilder.DropColumn(
            name: "role_id",
            table: "role_permissions");

        migrationBuilder.DropColumn(
            name: "role_id",
            table: "memberships");

        migrationBuilder.DropColumn(
            name: "user_id",
            table: "memberships");
    }
}
