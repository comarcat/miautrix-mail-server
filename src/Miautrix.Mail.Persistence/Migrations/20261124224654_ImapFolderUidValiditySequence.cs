using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class ImapFolderUidValiditySequence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS imap_uid_validity_seq;");
        migrationBuilder.Sql("SELECT setval('imap_uid_validity_seq', GREATEST(COALESCE((SELECT MAX(uid_validity) FROM folders), 0), last_value), true) FROM imap_uid_validity_seq;");
        migrationBuilder.AlterColumn<long>(
            name: "uid_validity",
            table: "folders",
            type: "bigint",
            nullable: false,
            defaultValueSql: "nextval('imap_uid_validity_seq')",
            oldClrType: typeof(long),
            oldType: "bigint");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
