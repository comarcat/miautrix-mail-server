using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;
    /// <inheritdoc />
    public partial class V22_Sieve_Scripts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (
                        PARTITION BY mailbox_id
                        ORDER BY updated_at DESC, created_at DESC, id
                    ) AS active_rank
                    FROM sieve_scripts
                    WHERE is_active = TRUE
                )
                UPDATE sieve_scripts AS scripts
                SET is_active = FALSE
                FROM ranked
                WHERE scripts.id = ranked.id AND ranked.active_rank > 1;
                """);
            migrationBuilder.Sql("ALTER TABLE smtp_queue ADD COLUMN IF NOT EXISTS transport_sender_override text NULL;");
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS sieve_vacation_responses (
                    id uuid NOT NULL CONSTRAINT "PK_sieve_vacation_responses" PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    mailbox_id uuid NOT NULL,
                    sender_hash character varying(64) NOT NULL,
                    handle character varying(255) NOT NULL,
                    last_sent_at timestamp with time zone NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT "FK_sieve_vacation_responses_tenants_tenant_id"
                        FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE
                );
                """);
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS uq_sieve_scripts_active_mailbox_id ON sieve_scripts (mailbox_id) WHERE is_active = TRUE;");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS idx_sieve_vacation_responses_tenant_id ON sieve_vacation_responses (tenant_id);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS uq_sieve_vacation_responses_mailbox_sender_handle ON sieve_vacation_responses (mailbox_id, sender_hash, handle);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sieve_vacation_responses");

            migrationBuilder.DropIndex(
                name: "uq_sieve_scripts_active_mailbox_id",
                table: "sieve_scripts");
            migrationBuilder.DropColumn(name: "transport_sender_override", table: "smtp_queue");
        }
    }
