# Miautrix Mail Server — Implementation Blueprint

**Blueprint version:** 1.1
**Generated:** 2026-09-16
**Source specification:** `Miautrix_Mail_Server_Final_Architecture_Design.md` v1.0 (2026-09-03)
**Primary shape:** `saas-webapp` (multi-tenant) · **Secondary:** `desktop-app`
**Executable steps:** 21 across 4 epics

---

## Section 0 — How to use this blueprint

Self-contained. A Claude Code instance with zero prior context builds from it without asking a
clarifying question. Every step carries an EARS acceptance criterion, a verify command that exits
0, and a checkpoint tag.

Read Section 9 in order. Do not skip, reorder, or merge steps.

**Section 20 names the verification work that was and was not done.** Read it first.

---

## Section 1 — Product

Miautrix Mail Server is a self-hosted, multi-tenant mail platform: SMTP, IMAP, ManageSieve,
JMAP-first webmail, anti-spam, anti-malware, mail-flow rules, and full administrative tooling.
It is a **modular monolith** — one deployable process, strictly separated internal modules.

| Actor | Surface |
|---|---|
| Tenant administrator | Web admin, desktop app, CLI |
| Tenant end user | Webmail |
| System operator | Web admin, desktop app, CLI |
| Helpdesk | Web admin (scoped) |
| Auditor | Web admin (read-only) |
| Machine / integration | REST `/api/v1/*`, MCP server |

WHEN a tenant administrator completes onboarding THE SYSTEM SHALL let them add a domain, create
mailboxes, send and receive mail, and read it in webmail with no operator intervention.

WHEN the licence service is unreachable THE SYSTEM SHALL keep mail flowing and SHALL NOT lock,
hide, or destroy any existing mailbox or message.

---

## Section 2 — Scope

### 2.1 In scope for v1

Multi-tenant core · SMTP in/out with queue and retry · IMAPS · ManageSieve · JMAP webmail ·
metadata in PostgreSQL with MIME in the storage abstraction · PostgreSQL FTS behind a seam ·
local auth with TOTP and WebAuthn · roles and permissions as data · append-only audit · security
events · anti-spam baseline with quarantine · ClamAV anti-malware adapter · mail-flow rules with
simulator and explorer · web admin · webmail · Electron desktop admin (Windows + Linux) ·
`miautrix-mail` CLI · backup and restore · blue/green updates · offline-capable licensing ·
structured logs and OpenTelemetry.

### 2.2 Out of scope for v1

Google Workspace / Entra ID / LDAP-AD identity · Cloudflare or cloud mail-flow integration ·
edition gating and online activation (separate project, end of development) · trained Bayes
classifier · POP3 and cleartext IMAP · data residency, customer-managed keys, VPC packaging ·
SOC 2 / ISO / HIPAA certification.

### 2.3 Non-goals

Not a microservice architecture — do not split the modules. Not a hosted SaaS. Not an IdP for
third parties. Not an Exchange/Google protocol clone (no MAPI, no ActiveSync). Not a general
compute platform — signed, vetted plugin assemblies only. Not a marketing site or billing system.

---

## Section 3 — Architecture

Modular monolith, Clean Architecture. Dependencies point inward:

```
Presentation → Application → Domain
                   ↓
          Infrastructure → Persistence
```

`Domain` depends on nothing but the BCL. `Application` depends only on `Domain`. A build-failing
test enforces this.

**Why a monolith:** mail delivery, storage, and anti-spam share a hot path. A network boundary adds
latency to every message, multiplies deploy failure modes, and forces distributed transactions
onto operations that are naturally local.

### Provider seams

| Interface | v1 | Future |
|---|---|---|
| `IIdentityProvider` | Local store | Entra ID, Google, LDAP/AD |
| `ISpamProvider` | Built-in rules | Rspamd, SpamAssassin |
| `IMalwareProvider` | ClamAV adapter | Cloud scanning |
| `IMailStorage` | Filesystem + PG metadata | S3-compatible |
| `IGeoIpProvider` | GeoLite2 | Commercial GeoIP |
| `IQueueProvider` | PostgreSQL-backed | Redis, RabbitMQ |
| `IAuthenticationProvider` | Local + TOTP + WebAuthn | IdP federation |
| `IReportingProvider` | Built-in aggregates | External BI |
| `ISearchProvider` | PostgreSQL FTS | Open |

---

## Section 4 — Stack

> **Every version in this section was verified against its live registry on 2026-09-16** using
direct registry API queries (`api.nuget.org/v3-flatcontainer/<id>/index.json` and
`registry.npmjs.org/<pkg>/latest`), executed via a script with no shell-quoting or interpretation
layer. `VERIFIED` means the version appears in the registry response. Nothing here is recalled
from memory.

### 4.1 Backend — NuGet, verified 2026-09-16

| Package | Version | Rationale |
|---|---|---|
| .NET SDK | 10.0.401 | Active LTS, support to 2028-11-14. Confirmed installed locally. |
| Microsoft.EntityFrameworkCore | 10.0.12 | Latest stable. |
| Npgsql | 10.0.3 | Latest stable. |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Major matches EF Core 10. |
| MailKit | 4.18.0 | SMTP/IMAP primitives. |
| MimeKit | 4.18.0 | MIME parse and DKIM sign. Locked to MailKit. |
| Quartz.Extensions.Hosting | 4.1.0 | In-process scheduling. |
| StackExchange.Redis | 3.2.1 | Optional cache only, off the delivery path. |
| Serilog | 4.4.0 | Structured logging. |
| Serilog.Sinks.Console | 6.1.1 | |
| Serilog.Sinks.File | 7.0.0 | Rolling file sink. |
| OpenTelemetry.Extensions.Hosting | 1.18.0 | |
| OpenTelemetry.Instrumentation.AspNetCore | 1.18.0 | |
| Hangfire.Core | 1.8.25 | Dashboard and recurring jobs. |
| Spectre.Console.Cli | 0.55.0 | CLI framework. See decision 6. |
| System.CommandLine | 2.0.12 | Stable alternative; not selected. |
| Otp.NET | 1.4.1 | TOTP. |
| **Fido2** | **4.0.1** | WebAuthn. **NuGet ID is `Fido2`, not `Fido2NetLib`** — that is the GitHub org name. |

> **Argon2id:** no `argon2` package exists on NuGet. Use
> `Konscious.Security.Cryptography.Argon2` — verify its version at T4 before pinning.

### 4.2 Frontend — npm, verified 2026-09-16

| Package | Version | | Package | Version |
|---|---|---|---|---|
| react | 19.3.0 | | @xyflow/react | 12.11.6 |
| react-dom | 19.3.0 | | dompurify | 3.4.15 |
| typescript | 7.0.2 | | recharts | 3.10.1 |
| vite | 8.3.0 | | i18next | 26.4.2 |
| tailwindcss | 4.3.3 | | react-i18next | 17.0.14 |
| zod | 4.6.5 | | electron | 44.4.1 |
| react-hook-form | 7.88.0 | | electron-builder | 26.15.3 |
| @hookform/resolvers | 5.9.1 | | electron-updater | 6.8.9 |
| @tanstack/react-query | 5.103.1 | | vitest | 5.0.1 |
| @tanstack/react-router | 1.170.38 | | @playwright/test | 1.63.0 |
| react-router-dom | 7.18.4 | | @testing-library/react | 16.3.3 |
| @tanstack/react-table | 9.2.4 | | msw | 2.15.0 |

### 4.3 Testing — NuGet, verified

| Package | Version |
|---|---|
| xunit.v3 | 4.0.1 |
| Testcontainers | 4.15.0 |
| FluentAssertions | 8.11.0 |

> **FluentAssertions licence:** changed at v8. Confirm the licence is acceptable before adopting;
> xunit's built-in assertions are the fallback.

### 4.4 Runtime — locally confirmed

| Component | Version | Status |
|---|---|---|
| .NET SDK | 10.0.401 | Installed |
| Node.js | v24.20.0 | Installed |
| npm | 12.0.2 | Installed |
| git | 2.55.0.windows.3 | Installed |
| **pnpm** | — | **NOT INSTALLED — required by every frontend verify command** |
| PostgreSQL | 17 | **NOT INSTALLED — required by T2 onward** |
| Docker | — | NOT INSTALLED (Testcontainers needs it, or a local PG) |

---

## Section 5 — Data model

### 5.1 Conventions

Plural `snake_case` tables · `<singular>_id` FKs · `idx_<table>_<cols>` and `uq_<table>_<cols>` ·
money as integer minor units · timestamps `timestamptz` UTC · every table carries `id`,
`created_at`, `updated_at` · soft delete via `deleted_at` **only** where recovery is a real
requirement, and excluded by a query filter rather than by memory · tenant-scoped tables carry
`tenant_id` with an index leading on it.

### 5.2 Entities

**Tenancy/identity** — `tenants`, `domains`, `users`, `user_credentials`, `user_identities`,
`identity_providers`, `identity_provider_groups`, `roles`, `permissions`, `role_permissions`,
`memberships`, `sessions`, `refresh_tokens`, `application_passwords`, `api_keys`, `invitations`

**Mail** — `mailboxes`, `folders`, `messages`, `message_recipients`, `message_flags`,
`attachments`, `aliases`, `groups`, `group_members`, `sieve_scripts`

**Flow/policy** — `mail_flow_rules`, `smtp_queue`, `smtp_delivery_attempts`, `spam_verdicts`,
`malware_verdicts`, `quarantine`, `dkim_keys`, `domain_dns_settings`

**Operations** — `security_events`, `audit_logs`, `system_events`, `backup_jobs`, `backup_history`,
`settings`, `jobs`, `job_dead_letters`, `idempotency_keys`, `deploys`

**Licensing** — `licenses`, `license_entitlements`, `license_activations`, `license_usage`,
`license_events`

### 5.3 Isolation

WHEN a user requests a resource belonging to another tenant THE SYSTEM SHALL return **404**, not
403, so the resource's existence is not disclosed.

### 5.4 Migrations

Forward-only in production. **EXPAND → MIGRATE → CONTRACT** across separate deploys. EF Core
Migrations owns the schema; nothing else issues DDL.

---

## Section 6 — Identity and authorization

**Self-hosted, because a mail server *is* an identity provider.** Delegating to Clerk, Auth0, or
Entra would make the product depend on a third party for the credentials of the accounts it exists
to serve, and would break in the air-gapped and on-premise deployments the licensing model
supports. This deliberately overrides the plugin's default auth recommendation.

Passwords hashed with Argon2id, parameters stored per-hash so they can be raised over time. TOTP
via Otp.NET, secrets encrypted at rest and never logged. WebAuthn via `Fido2`. Application
passwords stored hashed, shown once. Session cookie `HttpOnly; Secure; SameSite=Lax`. Refresh
tokens rotated on use, stored hashed.

**MFA is available in every edition.** Gating baseline security reads as extortion.

Permissions are **data**: `permissions` holds stable `object.verb` strings, `roles` bundle them per
tenant, `memberships` bind a user to a tenant with a role. A user's role is a property of the
membership, never of the user. The last owner of a tenant cannot be removed or demoted.

WHEN a user without `mailbox.create` attempts a create THE SYSTEM SHALL return 403 and write a
`security_events` row.

**Event codes:** `AUTH-4001` login success · `4010` login failure · `4011` account locked ·
`4020` MFA challenge · `4030` MFA success · `4031` MFA failure · `4040` password changed ·
`4050` session revoked · `4060` privilege escalation.

---

## Section 7 — Mail flow

**Inbound (25):** TLS → SPF → DNSBL → greylisting → per-tenant rules → anti-spam score →
anti-malware → DKIM verify → quarantine or deliver.

**Outbound (587/465):** authentication **required, never without encryption** → per-tenant rules →
DKIM sign → queue → retry with exponential backoff and jitter → dead-letter at the ceiling.

WHEN a message arrives for a recipient outside every tenant domain THE SYSTEM SHALL reject at
RCPT TO with `550 5.7.1 Relay access denied` and SHALL NOT enqueue it.

WHEN an unauthenticated client attempts submission on 587 THE SYSTEM SHALL reject with
`530 5.7.0 Authentication required`.

| Port | Protocol | State |
|---|---|---|
| 25 | SMTP inbound | Enabled |
| 465 | Submission, implicit TLS | Enabled |
| 587 | Submission, STARTTLS | Enabled |
| 993 | IMAPS | Enabled |
| 4190 | ManageSieve | Enabled |
| 443 | HTTPS | Enabled |
| 110 | POP3 | **Disabled by design** |
| 143 | IMAP cleartext | **Disabled by design** |

TLS 1.3 preferred, 1.2 minimum.

---

## Section 8 — Data layer and storage

PostgreSQL holds metadata, envelopes, flags, search vectors. MIME bodies and attachments live behind
`IMailStorage`, keyed by content hash — a duplicate attachment costs one copy. Large messages are
streamed with bounded buffers; nothing is buffered whole into memory. `miautrix-mail backup`
snapshots database and storage consistently together.

---

## Section 9 — Build order

> **21 steps across 4 epics. Execute in order. Each fits one sitting.** T1–T17 are detailed here;
> T18–T21 are detailed in `epics/04-operations.md`. The authoritative machine-readable list is
> `tasks.json`.

### Epic 1 — Core platform (T1–T6)

**T1 · Solution scaffold and CI** — `Miautrix.Mail.sln` with the layered projects and an
architecture test enforcing inward dependencies.
WHEN `dotnet build Miautrix.Mail.sln -warnaserror` runs on a clean checkout THE SYSTEM SHALL compile
with zero warnings and exit 0.
`verify:` `dotnet build Miautrix.Mail.sln -warnaserror` · `checkpoint:` `scaffold`

**T2 · Domain model and EF Core schema** — every entity in §5.2; `tenant_id` plus a leading index
on every tenant-scoped table.
WHEN `dotnet ef database update` runs against an empty database THE SYSTEM SHALL create every table
with its tenant index.
`verify:` `dotnet ef database update` · `checkpoint:` `schema`

**T3 · Seed data and indexes** — permission catalogue, system roles, first tenant and owner.
Idempotent.
WHEN the seed runs twice THE SYSTEM SHALL NOT duplicate the permission catalogue.
`verify:` `dotnet run --project src/Miautrix.Mail.Seeder` · `checkpoint:` `seed`

**T4 · Identity** — Argon2id, TOTP enrolment and challenge, sessions, refresh rotation,
`security_events` writes.
WHEN a TOTP-enabled user submits a correct password with no second factor THE SYSTEM SHALL refuse
and write exactly one `AUTH-4020` event.
`verify:` `dotnet test --filter Category=Identity` · `checkpoint:` `identity`

**T5 · Authorization and tenant isolation** — one helper, no bypasses.
WHEN a user in tenant A requests a mailbox in tenant B THE SYSTEM SHALL return 404.
`verify:` `dotnet test --filter Category=Isolation` · `checkpoint:` `authz`

**T6 · Audit trail** — append-only, written in the caller's transaction.
WHEN an audited change is rolled back THE SYSTEM SHALL NOT persist its audit row.
`verify:` `dotnet test --filter Category=Audit` · `checkpoint:` `audit`

### Epic 2 — Mail transport and policy (T7–T13)

**T7 · SMTP listener and queue** — inbound 25, submission 465/587, PostgreSQL-backed queue,
retry with backoff **and jitter**, alerting dead-letter store.
WHEN a message is accepted for a local recipient THE SYSTEM SHALL enqueue exactly one row, and on
failure SHALL schedule a retry with a strictly greater delay.
`verify:` `dotnet test --filter Category=Smtp` · `checkpoint:` `smtp`

**T8 · SPF, DKIM, DMARC** — per-domain keys, sign outbound, verify inbound, publish
`domain_dns_settings`.
WHEN an outbound message leaves a DKIM-configured domain THE SYSTEM SHALL attach a
`DKIM-Signature` that verifies against the published key.
`verify:` `dotnet test --filter Category=Dkim` · `checkpoint:` `dns-auth`

**T9 · Anti-spam baseline and quarantine** — **the largest under-scoped item in v1.** Rules,
DNSBL, greylisting, SPF/DKIM/DMARC verdicts, aggregation, quarantine with release and
release-and-train. **No trained Bayes classifier** — a half-trained filter performs worse than
none, and `ISpamProvider` keeps Rspamd droppable.
WHEN a message exceeds the spam threshold THE SYSTEM SHALL quarantine it and NOT deliver it.
`verify:` `dotnet test --filter Category=AntiSpam` · `checkpoint:` `antispam`

**T10 · IMAP, storage, attachments** — IMAPS 993, `IMailStorage` filesystem adapter, content-hash
keying, streaming both directions.
WHEN a client APPENDs and reconnects THE SYSTEM SHALL return identical bytes and flags.
`verify:` `dotnet test --filter Category=Imap` · `checkpoint:` `imap`

**T11 · ManageSieve** — 4190, validation **before** activation.
WHEN an invalid script is uploaded THE SYSTEM SHALL reject it with the offending line number and
leave the previous script active.
`verify:` `dotnet test --filter Category=Sieve` · `checkpoint:` `sieve`

**T12 · Search** — `ISearchProvider` with the PostgreSQL FTS adapter, index maintained on
delivery, reindex command for post-restore recovery.
WHEN a message is delivered THE SYSTEM SHALL make it findable by full-text query within one second.
`verify:` `dotnet test --filter Category=` · `checkpoint:` ``

**T13 · Rule engine, simulator, explorer** — conditions and actions, dry-run simulator that
mutates nothing.
WHEN a rule is simulated THE SYSTEM SHALL report every match and action without mutating any
message.
`verify:` `dotnet test --filter Category=Rules` · `checkpoint:` `rules`

### Epic 3 — Surfaces (T14–T17)

**T14 · API contract and OpenAPI** — `/api/v1/*`, one service layer, response envelopes,
checked-in OpenAPI.
WHEN a request omits a required field THE SYSTEM SHALL return 422 with field-level detail and
SHALL NOT return 200 with a failure body.
`verify:` `dotnet test --filter Category=Api` · `checkpoint:` `api`

**T15 · Web admin GUI** — the §32 menu, `@xyflow/react` rule designer driving T13's simulator,
cursor pagination, no N+1 on the dashboard.
WHEN an administrator loads the queue screen THE SYSTEM SHALL paginate with cursors and issue no
more than one request per screen.
`verify:` `pnpm --filter admin test && pnpm --filter admin build` · `checkpoint:` `web-admin`

**T16 · Webmail** — JMAP-first mail, contacts, calendar, Sieve management. **Message bodies are
hostile input.**
WHEN a body containing a `<script>` tag is rendered THE SYSTEM SHALL sanitise with DOMPurify and
SHALL NOT execute it.
`verify:` `pnpm --filter webmail test` · `checkpoint:` `webmail`

**T17 · CLI** — `miautrix-mail` on Spectre.Console.Cli, covering every administrative function.
Honours the same authorization as the GUI.
WHEN an authorized operator lists a tenant's mailboxes THE SYSTEM SHALL print them and exit 0.
`verify:` `dotnet run --project src/Miautrix.Mail.Cli -- mailbox list --help` · `checkpoint:` `cli`

### Epic 4 — Operations (T18–T21)

See `epics/04-operations.md`. Desktop app · backup/restore · blue/green with the migrations ladder ·
hardening and licence gating.

---

## Section 10 — Workspace

The `workspace/` directory is copied into the target project root. It contains `CLAUDE.md`,
`AGENTS.md`, `Directory.Build.props`, `.editorconfig`, `.claude/settings.json` with the verify-command
allowlist, `.claude/rules/{tenancy,secrets-and-logging}.md`, and `.claude/skills/verify-task/`.

**No package manifest is authored by this blueprint.** T1's `dotnet new` scaffold generates
`Miautrix.Mail.sln` and the project files; `pnpm` generates the frontend manifests at T15. Do not
author them separately — two sources for one manifest is how a build breaks.

---

## Section 11 — Testing

The build order **is** the test plan; each verify command is the executable form of its EARS
criterion. Thin unit base, integration-heavy bulk, under ten E2E specs.

**Use a real database, never a mock.** Testcontainers or a per-run schema. Mocked database tests
assert that your mock matches your mock. A flaky test is a broken test — quarantine it the day it
flakes.

---

## Section 12 — Observability

Structured JSON logs carrying `request_id`, `user_id`, `tenant_id`, `duration_ms`, `release`.
OpenTelemetry from day one — the one decision here that is expensive to reverse. Per-subsystem
health endpoints with an external uptime check. Four metrics: request rate, error rate,
p50/p95/p99 latency, saturation — plus queue depth, oldest-message age, dead-letter count.
Alerts fire on **symptoms**, and every alert names an action.

**Retention:** INFO 30d · NOTICE 60d · WARNING 90d · ERROR 180d · CRITICAL 365d.

**Never log** passwords, TOTP secrets, session cookies, refresh tokens, API secrets, private keys,
application passwords, or full message bodies unless explicitly enabled for controlled diagnostics.
The redaction list lives **in the logger**, not at each call site.

---

## Section 13 — Deployment

Debian/Linux under systemd, PostgreSQL co-located or managed. Blue/green with signed packages —
staged to the inactive slot, health-gated, then a symlink flip. Schema changes ride
EXPAND → MIGRATE → CONTRACT across separate deploys. Every environment variable parsed and
validated at boot; **missing means crash**. `.env` never committed; `.env.example` carries
placeholders only.

---

## Section 14 — Licensing

Metric: **active mailboxes**. Editions: Community, Professional, Enterprise, MSP.

Non-negotiable at every edition:

- Licence service unreachable → **mail keeps flowing.**
- Over allowance → **block new mailbox creation only.** Never lock, hide, or destroy existing
  mailboxes or messages.
- MFA, backups, audit visibility of one's own account, and local admin access are **free in every
  edition.**

SSO and directory federation belong in Enterprise. That is normal and defensible.

---

## Section 15 — Security

TLS 1.3 preferred / 1.2 minimum everywhere · no SMTP authentication without encryption · explicit
open-relay protection · invitation tokens and API keys hashed and shown once · cross-tenant access
returns 404 not 403 · plugin assemblies must be signed or they do not load · no secrets in the
repository · desktop app uses the OS keychain.

---

## Section 16 — Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Anti-spam is the largest under-scoped v1 item | Quality gap vs Rspamd | Ship rules+DNSBL+greylist without Bayes; `ISpamProvider` seam keeps Rspamd droppable |
| `Konscious.Security.Cryptography.Argon2` version unverified | T4 blocked | Verify at T4 before pinning |
| FluentAssertions licence change at v8 | Legal | Confirm before adopting; xunit assertions as fallback |
| PostgreSQL FTS quality at scale | Slow search | `ISearchProvider` seam; Open drops in |
| Electron bundle ~150 MB | Distribution weight | Accepted for one consistent rendering engine |
| pnpm and PostgreSQL absent on the generation machine | Smoke tests unrun | Install both, then run §20's procedure |

---

## Section 17 — Decision log

1. **No .NET runtime track exists in the plugin.** C#/.NET 10 chosen per the source document and
the user's explicit requirement. Documented deviation.
2. **TypeScript 7.0.2** is a fresh major, deviating from the plugin track's ~6.0.3.
3. **Electron over Tauri.** Tauri's WebKitGTK varies across Debian/Ubuntu/Fedora; rendering gaps
become operator support tickets. Reversal trigger: bundle size becomes a blocker.
4. **Self-hosted identity over any hosted provider.** A mail server is the IdP; third-party auth is
architecturally impossible here.
5. **Anti-spam baseline without a trained Bayes classifier in v1.** The `ISpamProvider` seam keeps
Rspamd droppable later.
6. **Spectre.Console.Cli over System.CommandLine — corrected rationale.** My earlier reason
("System.CommandLine is still beta") was **wrong**: it is stable at **2.0.12**. Spectre.Console.Cli
is retained for its rendering and interactive-prompt capabilities, which suit an operator CLI.
This is now a preference, not a forced choice; revisit if the team prefers the Microsoft-supported
option.
7. **FluentAssertions v8 licence** must be confirmed before adoption.

---

## Section 18 — Environment variables

Every variable parsed and validated at boot.

| Variable | Required | Purpose |
|---|---|---|
| `MIAUTRIX_DB_CONNECTION` | yes | PostgreSQL connection |
| `MIAUTRIX_STORAGE_ROOT` | yes | Mail storage root |
| `MIAUTRIX_LOG_DIR` | yes | Log output |
| `MIAUTRIX_TLS_CERT_PATH` | yes | TLS certificate |
| `MIAUTRIX_TLS_KEY_PATH` | yes | TLS private key |
| `MIAUTRIX_SMTP_HOSTNAME` | yes | SMTP banner / HELO |
| `MIAUTRIX_LICENSE_SERVER` | no | Absence forces offline mode |
| `MIAUTRIX_REDIS` | no | Optional cache |
| `MIAUTRIX_OTEL_ENDPOINT` | no | OTel collector |

---

## Section 19 — Verify commands

### 19.1 Where they run

All verify commands run from the **target project root** — the directory `workspace/` was copied
into — not from the blueprint directory. `tasks.json` records this per task as `verify_from`.

### 19.2 Settings allowlist

Every verify command appears in `workspace/.claude/settings.json`'s allow list. A verify command
absent from it will prompt mid-build and stall an autonomous run.

### 19.3 Toolchain prerequisites

T1–T14 and T17 need the **.NET SDK** (present). T15, T16, and T18 need **pnpm** (absent — install
`npm i -g pnpm`). T2 onward needs **PostgreSQL** (absent — install locally or provide Docker for
Testcontainers).

### 19.6 Verify-critical configuration

`workspace/.claude/settings.json` · `workspace/Directory.Build.props` · `workspace/.editorconfig`.
These are a **set**; all three are required for the verify commands to behave identically on every
machine.

---

## Section 20 — Verification status (READ FIRST)

### What was verified

| Gate | Status | Evidence |
|---|---|---|
| Version pins | ✅ **VERIFIED** | Every NuGet and npm package in §4 queried live on 2026-09-16 via registry APIs. Three earlier pins were **wrong and are corrected**: StackExchange.Redis 2.10.2→**3.2.1**, Serilog.Sinks.File 6.0.0→**7.0.0**, and the WebAuthn package ID `Fido2NetLib`→**`Fido2` 4.0.1**. |
| Toolchain presence | ✅ **CHECKED** | `dotnet 10.0.401`, `node v24.20.0`, `npm 12.0.2`, `git 2.55.0` present. `pnpm`, Docker, PostgreSQL **absent**. |

### What was NOT verified

| Gate | Status | Reason | How to close |
|---|---|---|---|
| Validator audit | **NOT RUN** | Generation-session tooling returned mismatched results; the bundle could not be reliably read back for audit | Run `/architect-audit` in a fresh session |
| Smoke tests | **NOT RUN** | `pnpm` absent (blocks T15/T16/T18 verify) and no PostgreSQL (blocks T2 onward, including the first database step). Docker absent, so Testcontainers cannot substitute | Install pnpm + PostgreSQL, then run the §9 bootstrap twice and the T2 database step once |

**Smoke-test statement, exact wording:** this bundle was **not smoke-tested past the toolchain** —
the generation environment has `dotnet` and `node` but lacks `pnpm`, PostgreSQL, and Docker, so the
frontend verify commands and the first database step could not execute.

### A correction to the record

An earlier draft of this section stated that `dotnet` and `node` were not installed and that NuGet
returned empty response bodies. **Both claims were false** — they were drawn from truncated tool
output and stated as fact without being tested. This section reflects the measured result. If any
other claim in this blueprint disagrees with what the toolchain reports, trust the toolchain.

---

**End of blueprint.**
