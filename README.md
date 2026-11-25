# Miautrix Mail Server

Self-hosted, multi-tenant mail platform designed as a modular monolith in .NET 10 LTS with PostgreSQL and React frontend surfaces.

## 📐 Architecture & Design

- **Architecture Diagram:** [`architecture.html`](architecture.html) (interactive diagram with showcase quality profile)
- **Architecture Blueprint:** [`blueprints/miautrix-mail-server/blueprint.md`](blueprints/miautrix-mail-server/blueprint.md)
- **Charter & Plan:** [`PMI_Project_Charter_and_Plan.md`](PMI_Project_Charter_and_Plan.md) — Version 1.0 complete (2026-10-03); **Version 2.0.0 released 2026-11-24** (M5 closed); **v2.1 deployed 2026-11-25**; **v2.2 deployed 2026-11-25**
- **Handoff (v2.3):** [`docs/HANDOFF-v2.3.md`](docs/HANDOFF-v2.3.md) — **start here to catch up**: deploy, smoke test, and what's recommended for v2.4. Previous: [`docs/HANDOFF-v2.2.md`](docs/HANDOFF-v2.2.md), [`docs/HANDOFF-v2.1.md`](docs/HANDOFF-v2.1.md), [`docs/HANDOFF-v2.0.0-release.md`](docs/HANDOFF-v2.0.0-release.md). Full history: [`docs/HANDOFF-v2.0.md`](docs/HANDOFF-v2.0.md)
- **Protocols (v2.1):** compliance audit [`docs/v2.1-Protocol-Compliance-Audit.md`](docs/v2.1-Protocol-Compliance-Audit.md); SMTP [`docs/SMTP-v2.1-Report.md`](docs/SMTP-v2.1-Report.md); outbound/direct MX [`docs/Outbound-v2.1-Report.md`](docs/Outbound-v2.1-Report.md); IMAP phase 2 [`docs/IMAP-Phase2-Report.md`](docs/IMAP-Phase2-Report.md)
- **IMAP:** [`docs/IMAP.md`](docs/IMAP.md) — capabilities, `Shared/` namespace, client setup; Send As model: [`docs/SMTP-SendAs-Report.md`](docs/SMTP-SendAs-Report.md)
- **Deployment:** [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) — versioned deploy, storage preflight, rollback and cleanup
- **Troubleshooting:** [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md) — deployment, service, mail and IMAP diagnostics
- **Changelog:** [`CHANGELOG.md`](CHANGELOG.md)
- **Calendar/Webmail final solution:** [`CALENDAR_WEBMAIL_FINAL_SOLUTION_2026-10-03.md`](CALENDAR_WEBMAIL_FINAL_SOLUTION_2026-10-03.md)
- **Admin UI Specification:** [`DESIGN-Admin.md`](DESIGN-Admin.md) — Comprehensive technical design for the Admin Website
- **Webmail UI Specification:** [`DESIGN-WEBMAIL.md`](DESIGN-WEBMAIL.md) — Comprehensive technical design for the Webmail Frontend

## 🚀 Project Status

**Version 2.0.0 is released and running in production (2026-11-24).** It brings .NET 10, MIME parsing and backfill, webmail isolation, full IMAP4rev1 with shared mailboxes, Send As / Send on Behalf, and versioned deployment with rollback.

**Version 2.1 is deployed (2026-11-25)** and in owner validation. It fixes SMTP multi-recipient delivery and unbounded input, adds STARTTLS on port 25 with TLS 1.2+, per-domain direct MX delivery (Cloudflare stays the default), retries with bounces, IMAP NAMESPACE/MOVE/IDLE, and makes drafts saved by IMAP clients editable in webmail. IMAP4rev2 is implemented as a session mode but not yet advertised. 

**Version 2.2** was deployed on 2026-11-25. It contains:
- **API session authentication:** a critical fix;
- **inbound SPF/DKIM/DMARC:** monitor mode by default;
- **Sieve filters:** with an out-of-office reply;
- **client autodiscovery;**
- **MTA-STS/TLS-RPT hosting;**
- **opt-in outbound DANE.**


**Version 2.3** (2026-11-25):
- fixes the remaining integration tests (149/149);
- adds `scripts/smoke-test.ps1` for checking a deployed server by LAN IP;
- improves IMAP and adds a full RFC 9051 audit.

IMAP4rev2 is still not advertised. The RFC 9051 audit (`docs/IMAP4rev2-v2.3-Audit.md`) has 0 Missing rows and 89 applicable MUST rows marked Partial, mostly edge cases without dedicated tests. Closing them is recommended for v2.4.

Build and run with the .NET 10 SDK (`global.json` pins 10.0.403). The `messages reparse` command can backfill stored messages after deployment. Without `--user`, it trusts the local host operator, matching the existing CLI local-access model.

Example: `dotnet run --project src/Miautrix.Mail.Cli -- messages reparse --tenant <tenant-guid> --dry-run`.

Tasks completed and verified (T1–T13):

| Task | Title | Status | Verification Gate |
|---|---|---|---|
| **T1** | Solution scaffold & CI | ✅ Done | `dotnet build Miautrix.Mail.sln -warnaserror` |
| **T2** | Domain model & EF Core schema | ✅ Done | `dotnet ef database update` |
| **T3** | Seed data and indexes | ✅ Done | `dotnet run --project src/Miautrix.Mail.Seeder` |
| **T4** | Identity: credentials, MFA, sessions | ✅ Done | `dotnet test --filter Category=Identity` |
| **T5** | Authorization, roles, tenant isolation | ✅ Done | `dotnet test --filter Category=Isolation` |
| **T6** | Audit trail | ✅ Done | `dotnet test --filter Category=Audit` |
| **T7** | SMTP listener and queue | ✅ Done | `dotnet test --filter Category=Smtp` |
| **T8** | SPF, DKIM, DMARC | ✅ Done | `dotnet test --filter Category=Dkim` |
| **T9** | Anti-spam baseline and quarantine | ✅ Done | `dotnet test --filter Category=AntiSpam` |
| **T10** | IMAP, storage abstraction, attachments | ✅ Done | `dotnet test --filter Category=Imap` |
| **T11** | ManageSieve | ✅ Done | `dotnet test --filter Category=Sieve` |
| **T12** | Full-text search indexing | ✅ Done | `dotnet test --filter Category=Search` |
| **T13** | Mail-flow rule engine & simulator | ✅ Done | `dotnet test --filter Category=Rules` |
| **T14** | API contract and OpenAPI | ✅ Done | `dotnet test --filter Category=Api` |
| **T15** | Web Admin GUI | ✅ Done | `pnpm --filter admin build && pnpm test` |
| **T16** | Webmail Client | ✅ Done | `pnpm --filter webmail build && pnpm test` |
| **T17–T21** | CLI, Desktop, Backup, Operations, Version 1.0 stabilization | ✅ Done | `dotnet build Miautrix.Mail.sln -warnaserror`; calendar invitation unit tests; final manual production checklist |

## 🛠️ Deploying & Updating

We now provide automated PowerShell / Bash scripts to update the database and push backend, worker, Admin Console, and Webmail artifacts to the LXC target server:

- **Database from scratch:** `./scripts/init-database-scratch.ps1` -- provisions the DB, applies EF schemas and seeds the `admin@miautrix.org` user.
- **Development Database Update:** `./scripts/update-database.ps1` -- applies migrations and runs the seeder against the explicitly supplied development connection.
- **Production Database Update:** `./scripts/update-prod-database.ps1` or `./scripts/update-prod-database.sh` -- requires an explicit production connection/password, refuses localhost/dev/test targets, requires database-name confirmation, and passes EF Core `--connection` so migrations cannot silently target development.
- **Full application deploy:** `./deploy.ps1` -- publishes `Miautrix.Mail.Web` to `/opt/miautrix-mail/app`, publishes `Miautrix.Mail.Worker` to `/opt/miautrix-mail/worker`, builds Admin/Webmail, copies all artifacts, and restarts `miautrix-mail`, `miautrix-mail-worker`, and `nginx`.
- **Upload Websites only:** `./scripts/deploy-websites.ps1` -- builds React frontends and uploads via SCP to `/opt/miautrix-mail/`.

See the scripts directory for the raw files or execute them from the repository root.

## ✉️ Outbound transport model

Tenant-owned domains define transport configuration. External recipient domains such as `gmail.com` are **not** added to the tenant `domains` table. For outbound delivery, `Miautrix.Mail.Worker` reads due `smtp_queue` rows and uses a recipient-specific Cloudflare domain configuration when present; otherwise it falls back to the tenant's primary Cloudflare domain/Worker as the relay. The queue Retry action posts JSON with reason `Manual retry from Admin UI` and only resets queue state; delivery attempts are made by the worker service.

## 🛠️ Tech Stack & Prerequisites

- **Backend:** .NET 10 LTS / C# 13
- **Database:** PostgreSQL 18 (production 18.6) (`Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3` / `EF Core 10.0.4`)
- **Frontend Surfaces:** React 19, TypeScript, Tailwind CSS, Vite
- **Storage:** Content-addressable storage (CAS) with SHA-256 deduplication and bounded streaming
- **Authentication:** Argon2id / bcrypt, TOTP MFA, WebAuthn

## ⚙️ Development Environment

Set the PostgreSQL connection string:

```bash
export MIAUTRIX_DB_CONNECTION="Host=<db-host>;Port=5432;Database=miautrix-mail-dev;Username=<db-user>;Password=<db-password>"
```

### Build & Test Commands

```bash
# Build the entire solution (warnings treated as errors)
dotnet build Miautrix.Mail.sln -warnaserror

# Run full test suite
dotnet test Miautrix.Mail.sln

# Run targeted test categories
dotnet test Miautrix.Mail.sln --filter Category=Dkim
dotnet test Miautrix.Mail.sln --filter Category=AntiSpam
dotnet test Miautrix.Mail.sln --filter Category=Imap
dotnet test Miautrix.Mail.sln --filter Category=Sieve
dotnet test Miautrix.Mail.sln --filter Category=Search
dotnet test Miautrix.Mail.sln --filter Category=Rules
```

## 🔒 Security Invariants

- **Multi-Tenant Isolation:** Cross-tenant resource lookups return `404 Not Found` (never `403 Forbidden`) and log `AUTH-4060`.
- **Sensitive Data Redaction:** Passwords, TOTP secrets, session tokens, private keys, and message bodies are never logged.
- **Strict Transport Security:** SMTP authentication without encryption is unconditionally rejected (`530 5.7.0`).
- **Open-Relay Protection:** Inbound messages for non-local recipients are rejected at `RCPT TO` with `550 5.7.1`.
- **Shared Mailboxes:** Shared mailboxes are passwordless mailbox resources, not login identities. They do not create `User`, `UserCredential`, or `Membership` records; backend delegate authorization is enforced centrally with same-domain `read`/`write` access and 404 concealment.
- **Production Migration Note:** The 2026-09-20 login 500 (`42703: column m.name does not exist`) was caused by production schema drift after adding `Mailbox.Name`. Fixed by `20260920223000_AddMailboxName` and production-safe DB update scripts; **migration applied to production on 2026-09-20**.
