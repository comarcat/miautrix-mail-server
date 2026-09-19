using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUserAndDomainFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "email",
            table: "users",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "is_active",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "name",
            table: "users",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "algorithm",
            table: "user_credentials",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "must_change_password",
            table: "user_credentials",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "password_hash",
            table: "user_credentials",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<Guid>(
            name: "user_id",
            table: "user_credentials",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<string>(
            name: "dkim_public_key",
            table: "domains",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "dkim_selector",
            table: "domains",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "dmarc_record",
            table: "domains",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "is_primary",
            table: "domains",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "is_verified",
            table: "domains",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "name",
            table: "domains",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "spf_record",
            table: "domains",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "email",
            table: "users");

        migrationBuilder.DropColumn(
            name: "is_active",
            table: "users");

        migrationBuilder.DropColumn(
            name: "name",
            table: "users");

        migrationBuilder.DropColumn(
            name: "algorithm",
            table: "user_credentials");

        migrationBuilder.DropColumn(
            name: "must_change_password",
            table: "user_credentials");

        migrationBuilder.DropColumn(
            name: "password_hash",
            table: "user_credentials");

        migrationBuilder.DropColumn(
            name: "user_id",
            table: "user_credentials");

        migrationBuilder.DropColumn(
            name: "dkim_public_key",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "dkim_selector",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "dmarc_record",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "is_primary",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "is_verified",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "name",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "spf_record",
            table: "domains");
    }
}
