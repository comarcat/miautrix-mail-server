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

            migrationBuilder.CreateTable(
                name: "sieve_vacation_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mailbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    handle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sieve_vacation_responses", x => x.id);
                    table.ForeignKey(
                        name: "FK_sieve_vacation_responses_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_sieve_scripts_active_mailbox_id",
                table: "sieve_scripts",
                column: "mailbox_id",
                unique: true,
                filter: "is_active = TRUE");

            migrationBuilder.CreateIndex(
                name: "idx_sieve_vacation_responses_tenant_id",
                table: "sieve_vacation_responses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_sieve_vacation_responses_mailbox_sender_handle",
                table: "sieve_vacation_responses",
                columns: new[] { "mailbox_id", "sender_hash", "handle" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sieve_vacation_responses");

            migrationBuilder.DropIndex(
                name: "uq_sieve_scripts_active_mailbox_id",
                table: "sieve_scripts");
        }
    }
