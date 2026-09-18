using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    private static readonly string[] TenantScopedTables =
    [
        "domains",
        "users",
        "user_credentials",
        "user_identities",
        "identity_providers",
        "identity_provider_groups",
        "roles",
        "permissions",
        "role_permissions",
        "memberships",
        "sessions",
        "refresh_tokens",
        "application_passwords",
        "api_keys",
        "invitations",
        "mailboxes",
        "folders",
        "messages",
        "message_recipients",
        "message_flags",
        "attachments",
        "aliases",
        "groups",
        "group_members",
        "sieve_scripts",
        "mail_flow_rules",
        "smtp_queue",
        "smtp_delivery_attempts",
        "spam_verdicts",
        "malware_verdicts",
        "quarantine",
        "dkim_keys",
        "domain_dns_settings",
        "security_events",
        "audit_logs",
        "system_events",
        "backup_jobs",
        "backup_history",
        "settings",
        "jobs",
        "job_dead_letters",
        "idempotency_keys",
        "deploys",
        "licenses",
        "license_entitlements",
        "license_activations",
        "license_usage",
        "license_events"
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                slug = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenants", x => x.id);
            });

        foreach (var tableName in TenantScopedTables)
        {
            migrationBuilder.CreateTable(
                name: tableName,
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey($"PK_{tableName}", x => x.id);
                    table.ForeignKey(
                        name: $"FK_{tableName}_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: $"idx_{tableName}_tenant_id",
                table: tableName,
                column: "tenant_id");
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        for (var i = TenantScopedTables.Length - 1; i >= 0; i--)
        {
            migrationBuilder.DropTable(name: TenantScopedTables[i]);
        }

        migrationBuilder.DropTable(name: "tenants");
    }
}
