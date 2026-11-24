using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class ImapFolderSubscriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS imap_folder_subscriptions (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                user_id uuid NOT NULL,
                folder_id uuid NOT NULL,
                created_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_imap_folder_subscriptions_tenant_id
                ON imap_folder_subscriptions (tenant_id);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_imap_folder_subscriptions_user_folder
                ON imap_folder_subscriptions (tenant_id, user_id, folder_id);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS imap_folder_subscriptions;");
}
