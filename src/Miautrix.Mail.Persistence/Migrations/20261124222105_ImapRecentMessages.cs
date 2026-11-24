using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class ImapRecentMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- Existing messages are not \Recent (RFC 3501 2.3.2): adding the column with
        -- DEFAULT true would make every stored message look new to clients on the
        -- first SELECT after the upgrade. Only messages delivered afterwards are recent.
        ALTER TABLE messages ADD COLUMN IF NOT EXISTS is_recent boolean NOT NULL DEFAULT false;
        ALTER TABLE messages ALTER COLUMN is_recent SET DEFAULT true;
        """);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TABLE messages DROP COLUMN IF EXISTS is_recent;");
}
