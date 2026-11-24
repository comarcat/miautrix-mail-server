using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMailboxDelegateSenderPermissions : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE mailbox_delegates ADD COLUMN IF NOT EXISTS can_send_as boolean NOT NULL DEFAULT false;
                UPDATE mailbox_delegates SET can_send_as = false WHERE can_send_as IS NULL;
                ALTER TABLE mailbox_delegates ALTER COLUMN can_send_as SET DEFAULT false;
                ALTER TABLE mailbox_delegates ALTER COLUMN can_send_as SET NOT NULL;
                ALTER TABLE mailbox_delegates ADD COLUMN IF NOT EXISTS can_send_on_behalf boolean NOT NULL DEFAULT false;
                UPDATE mailbox_delegates SET can_send_on_behalf = false WHERE can_send_on_behalf IS NULL;
                ALTER TABLE mailbox_delegates ALTER COLUMN can_send_on_behalf SET DEFAULT false;
                ALTER TABLE mailbox_delegates ALTER COLUMN can_send_on_behalf SET NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "can_send_as",
                table: "mailbox_delegates");

            migrationBuilder.DropColumn(
                name: "can_send_on_behalf",
                table: "mailbox_delegates");
        }
}
