using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDomainTransportMode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "cloudflare_worker_url",
            table: "domains",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "cloudflare_zone_id",
            table: "domains",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "transport_mode",
            table: "domains",
            type: "text",
            nullable: false,
            defaultValue: "local");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "cloudflare_worker_url",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "cloudflare_zone_id",
            table: "domains");

        migrationBuilder.DropColumn(
            name: "transport_mode",
            table: "domains");
    }
}
