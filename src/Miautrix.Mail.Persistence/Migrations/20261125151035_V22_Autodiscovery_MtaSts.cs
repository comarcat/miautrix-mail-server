using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;
    /// <inheritdoc />
    public partial class V22_Autodiscovery_MtaSts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE domains ADD COLUMN IF NOT EXISTS mta_sts_mode text NOT NULL DEFAULT 'none';
                ALTER TABLE domains ADD COLUMN IF NOT EXISTS mta_sts_max_age integer NOT NULL DEFAULT 604800;
                ALTER TABLE domains ADD COLUMN IF NOT EXISTS tls_report_rua text NULL;
                ALTER TABLE domains ADD COLUMN IF NOT EXISTS mta_sts_mx text NULL;
                """);
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

            migrationBuilder.DropColumn(
                name: "mta_sts_mx",
                table: "domains");
        }
    }
