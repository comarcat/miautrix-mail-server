using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;
    /// <inheritdoc />
    public partial class V22_Autodiscovery_MtaSts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "mta_sts_max_age",
                table: "domains",
                type: "integer",
                nullable: false,
                defaultValue: 604800);

            migrationBuilder.AddColumn<string>(
                name: "mta_sts_mode",
                table: "domains",
                type: "text",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<string>(
                name: "tls_report_rua",
                table: "domains",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mta_sts_max_age",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "mta_sts_mode",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "tls_report_rua",
                table: "domains");
        }
    }
