using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

public partial class MailSignatures_EF : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "mail_signatures",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                content_text = table.Column<string>(type: "text", nullable: false),
                content_html = table.Column<string>(type: "text", nullable: true),
                is_default = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mail_signatures", x => x.id);
                table.ForeignKey(
                    name: "FK_mail_signatures_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_mail_signatures_tenant_id",
            table: "mail_signatures",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "uq_mail_signatures_tenant_id_user_id_name",
            table: "mail_signatures",
            columns: new[] { "tenant_id", "user_id", "name" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "mail_signatures");
    }
}
