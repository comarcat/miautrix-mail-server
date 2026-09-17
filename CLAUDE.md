# Miautrix Mail Server

Self-hosted, multi-tenant mail platform. Modular monolith, .NET 10, PostgreSQL, React.

**Blueprint:** `blueprints/miautrix-mail-server/blueprint.md`
**Charter:** `PMI_Project_Charter_and_Plan.md`
**Architecture Diagram:** `architecture.html`

## Commands

| Command | Purpose |
|---|---|
| `dotnet build Miautrix.Mail.sln -warnaserror` | Build everything, warnings are errors |
| `dotnet test` | Run the full test suite |
| `dotnet ef database update` | Apply migrations |
| `pnpm --filter admin dev` | Web admin in dev mode |
| `pnpm --filter webmail dev` | Webmail in dev mode |
| `pnpm --filter desktop dev` | Desktop shell in dev mode |

## Architecture rules

- **Dependency direction is inward.** `Domain` depends on nothing. `Application` depends only on
  `Domain`. A test enforces this and fails the build when it is violated.
- **One service layer, many transports.** Controllers, CLI commands, and MCP tools are thin. If
  business logic appears in a controller, it is in the wrong place.
- **One authorization helper.** Every tenant-scoped read and write passes through it. No exceptions,
  no "just this once".

## Security rules — these are not preferences

- Cross-tenant resources return **404, not 403**. A 403 confirms the resource exists.
- **Never log** passwords, TOTP secrets, session cookies, refresh tokens, API secrets, private
  keys, application passwords, or full message bodies. The redaction list lives in the logger, not
  at each call site.
- Invitation tokens and API keys are stored **hashed**, shown once.
- **No SMTP authentication without encryption.** There is no flag that permits it.
- The desktop shell **never loads remote content into the webview**. Local UI, API-only server
  access.
- Never commit `.env`. Commit `.env.example` with placeholders only.

## Data rules

- Forward-only migrations in production: **EXPAND → MIGRATE → CONTRACT** across separate deploys.
- Every schema change goes through EF Core Migrations. Nothing else issues DDL.
- **Never mock the database in tests.** Testcontainers, always.
- Every environment variable is parsed and validated at boot. Missing means crash.

## Licensing rules

- Licence service unreachable → **mail keeps flowing.**
- Over allowance → **block new mailboxes only.** Never lock or destroy existing data.
- MFA, backups, and local admin access are free in every edition.

## Style

- Plural `snake_case` tables, `<singular>_id` foreign keys, `idx_`/`uq_` prefixes.
- Timestamps are `timestamptz`, stored UTC.
- Follow `.editorconfig`. Run the formatter before committing.
