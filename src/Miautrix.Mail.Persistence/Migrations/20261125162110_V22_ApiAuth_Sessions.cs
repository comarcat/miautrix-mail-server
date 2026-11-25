using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Miautrix.Mail.Persistence.Migrations;

public partial class V22_ApiAuth_Sessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE sessions ADD COLUMN IF NOT EXISTS user_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE sessions ADD COLUMN IF NOT EXISTS access_token_hash text NOT NULL DEFAULT '';
        ALTER TABLE sessions ADD COLUMN IF NOT EXISTS expires_at timestamptz NOT NULL DEFAULT now();
        ALTER TABLE sessions ADD COLUMN IF NOT EXISTS last_seen_at timestamptz NULL;
        ALTER TABLE sessions ADD COLUMN IF NOT EXISTS revoked_at timestamptz NULL;
        ALTER TABLE sessions ADD COLUMN IF NOT EXISTS client_hash text NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS IX_sessions_access_token_hash ON sessions(access_token_hash);
        ALTER TABLE refresh_tokens ADD COLUMN IF NOT EXISTS session_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE refresh_tokens ADD COLUMN IF NOT EXISTS user_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE refresh_tokens ADD COLUMN IF NOT EXISTS token_hash text NOT NULL DEFAULT '';
        ALTER TABLE refresh_tokens ADD COLUMN IF NOT EXISTS expires_at timestamptz NOT NULL DEFAULT now();
        ALTER TABLE refresh_tokens ADD COLUMN IF NOT EXISTS used_at timestamptz NULL;
        ALTER TABLE refresh_tokens ADD COLUMN IF NOT EXISTS revoked_at timestamptz NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS IX_refresh_tokens_token_hash ON refresh_tokens(token_hash);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
