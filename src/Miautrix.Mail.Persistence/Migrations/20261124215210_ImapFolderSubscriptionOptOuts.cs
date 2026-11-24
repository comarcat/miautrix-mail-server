using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class ImapFolderSubscriptionOptOuts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TABLE imap_folder_subscriptions ADD COLUMN IF NOT EXISTS subscribed boolean NOT NULL DEFAULT true;");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TABLE imap_folder_subscriptions DROP COLUMN IF EXISTS subscribed;");
}
