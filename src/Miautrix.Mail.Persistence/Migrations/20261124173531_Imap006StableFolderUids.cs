using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;
/// <inheritdoc />
public partial class Imap006StableFolderUids : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // During deploy-release.ps1 -RunMigrations the old process can briefly remain active.
            // It may attempt UID 0 inserts until the new release switches; the CHECK rejects them.
            // Inbound mail remains queued and is retried by the worker.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE imap006_changed_folders ON COMMIT DROP AS
                SELECT DISTINCT folder_id FROM (
                    SELECT folder_id, uid, ROW_NUMBER() OVER (PARTITION BY folder_id ORDER BY date, created_at, id) AS new_uid
                    FROM messages
                ) ranked WHERE uid <> new_uid;

                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY folder_id ORDER BY date, created_at, id) AS new_uid
                    FROM messages
                )
                UPDATE messages m SET uid = r.new_uid FROM ranked r WHERE m.id = r.id;

                UPDATE folders f SET uid_next = COALESCE((SELECT MAX(m.uid) + 1 FROM messages m WHERE m.folder_id = f.id), 1);

                UPDATE folders f SET uid_validity = CASE
                    WHEN f.uid_validity = floor(extract(epoch FROM clock_timestamp()))::bigint
                    THEN f.uid_validity + 1
                    ELSE floor(extract(epoch FROM clock_timestamp()))::bigint
                END
                WHERE f.id IN (SELECT folder_id FROM imap006_changed_folders);
                """);
            migrationBuilder.CreateIndex(
                name: "uq_messages_folder_id_uid",
                table: "messages",
                columns: new[] { "folder_id", "uid" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_messages_uid_positive",
                table: "messages",
                sql: "uid > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("IMAP UIDs are forward-only; cached client UID state cannot be restored.");
        }
}
