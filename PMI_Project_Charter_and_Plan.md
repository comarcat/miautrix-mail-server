# Project Management Institute (PMI) / PMBOK Project Charter & Plan
# Miautrix Mail Server

**Document Version:** 1.0  
**Date:** 2026-10-03  
**Project Sponsor:** Miautrix  
**Project Lead / Architect:** Cristobal Arboleda  
**Status:** **Version 1.0 Complete / Approved**

**Last Updated:** 2026-10-03  

---

## 1. Project Initiation & Executive Summary

### 1.1 Project Purpose and Business Case
Miautrix Mail Server is a secure, high-performance, multi-tenant mail platform designed to give full email sovereignty back to organizations. Operating on Debian/Linux with .NET 10, PostgreSQL, and modern web/desktop administrative surfaces, the system handles SMTP/IMAPS/ManageSieve, anti-spam, mail-flow rules, and administration across isolated tenants without third-party vendor lock-in.

### 1.2 High-Level Objectives
- Deliver a production-grade mail platform capable of running end-to-end multi-tenant email operations.
- Provide a unified management suite: Web Admin GUI, Cross-Platform Desktop Admin (Windows/Linux via Electron), and CLI.
- Provide self-hosted identity, authentication (Argon2id, TOTP, WebAuthn), and granular data-driven RBAC.
- Ensure strict multi-tenant isolation where cross-tenant attempts return HTTP 404 (non-disclosure of resource existence).
- Implement modular Clean Architecture allowing swap-in of future enterprise integrations (Rspamd, S3 storage, IdP federation).

---

## 2. Project Scope Management (WBS Overview)

The project work is partitioned into 4 primary Epics covering 21 detailed work packages (tasks):

```
Miautrix Mail Server
├── Epic 1: Core Platform (T1 - T6)
│   ├── T1: Solution scaffold and CI
│   ├── T2: Domain model and EF Core schema
│   ├── T3: Seed data and indexes
│   ├── T4: Identity: credentials, MFA, sessions
│   ├── T5: Authorization, roles, and tenant isolation
│   └── T6: Audit trail
├── Epic 2: Mail Transport & Policy (T7 - T13)
│   ├── T7: SMTP listener and queue
│   ├── T8: SPF, DKIM, DMARC validation & signing
│   ├── T9: Anti-spam baseline and quarantine
│   ├── T10: IMAP, storage abstraction, attachments
│   ├── T11: ManageSieve
│   ├── T12: Full-text search indexing (PostgreSQL FTS)
│   └── T13: Mail-flow rule engine, simulator, explorer
├── Epic 3: User & Management Surfaces (T14 - T17)
│   ├── T14: REST API contract & OpenAPI spec
│   ├── T15: Web Admin GUI (React / TanStack / Tailwind)
│   ├── T16: Webmail (JMAP-first / React / DOMPurify)
│   └── T17: Operator CLI (Spectre.Console)
└── Epic 4: Operations & Lifecycle (T18 - T21)
    ├── T18: Desktop Admin App (Electron packaging)
    ├── T19: Automated Backup & Restore engine
    ├── T20: Blue/Green deployment & migration ladder
    └── T21: Security hardening & license enforcement
```

### Scope Boundaries
- **In Scope (v1):** Full core mail pipeline, multi-tenancy, local authentication/MFA, Web Admin, Webmail, Desktop App, CLI, Backup/Restore, Blue/Green update mechanism.
- **Out of Scope (Deferred):** Google Workspace / Entra ID / LDAP sync, Cloudflare mail-routing integrations, proprietary Bayes training models, POP3 protocol support, online license activation portal.

---

## 3. Project Schedule & Milestone Management

| Milestone | Key Deliverables / Checkpoint | Status | Pre-requisites | Primary Verification Gate |
|---|---|---|---|---|
| **M1: Core Foundation** | Scaffold, PostgreSQL schema, Seed data, Auth/MFA, RBAC, Audit | ✅ Complete | T1–T6 | `dotnet test --filter Category=Isolation\|Audit\|Identity` |
| **M2: Mail Engine** | SMTP In/Out, SPF/DKIM/DMARC, Anti-Spam, IMAP, Sieve, FTS, Rules Engine | ✅ Complete | T7–T13 | `dotnet test --filter Category=Smtp\|Dkim\|Imap\|Rules` |
| **M3: Surfaces & Client Apps** | OpenAPI REST endpoints, Web Admin, Webmail, CLI | ✅ Complete | T14–T17 | API Integration tests & `pnpm test` |
| **M4: Operational Readiness** | Desktop Client, Backup/Restore drills, Blue/Green symlink updater, License gates, calendar invitations, Webmail stability | ✅ Complete — Version 1.0 closed 2026-10-03 | T18–T21 | `dotnet build Miautrix.Mail.sln -warnaserror`; calendar invitation unit tests; live/manual verification checklist |

### Active Execution Phase: Production Integration & Functional Delivery

**Version 1.0 closure note (2026-10-03):** Core project scope is complete. The final stabilization pass closed the painful calendar/Webmail defects that blocked signoff: public RSVP actions, reschedule proposal links, explicit UTC schedule wording, and inbox bulk-delete white-screen prevention.

| Task ID | Task Description | Scope & Acceptance | Blocked By | Status |
|---|---|---|---|---|
| **#12** | Database Migration & Seeding | EF migrations/seeding target PostgreSQL `10.11.1.52` (`miautrix-mail-pro`); production update scripts force explicit connection and confirmation. | None | ✅ Applied — `20260920223000_AddMailboxName` migrated to production 2026-09-20 |
| **#13** | Backend Authentication REST API | `/api/v1/auth/login`, `/me`, `/refresh`, `/logout`, `/change-password`; live login 500 traced to schema drift (`column m.name does not exist`), not credential validation. | #12 | ✅ Implemented / ✅ Live login verified post-migration (2026-09-20) |
| **#14** | Backend Mailbox & Message REST API | `/api/v1/mailboxes`, folders, `/messages`, `/send`; mailbox-scoped reads/writes now authorize through shared central mailbox access helper before message operations. | #12 | ✅ Implemented |
| **#15** | Backend Admin Management REST API | `/api/v1/domains`, `/users`, `/shared-mailboxes`, `/rules`, `/audit`; shared mailboxes are passwordless mailbox resources with delegate assignment endpoints. | #12 | ✅ Implemented |
| **#16** | Functional Webmail Frontend | Full-width responsive layout, Auth/Mailbox REST wiring, reply/forward actions, writable-mailbox sender fallback, rich composer toolbar, 30-second draft autosave, persisted draft attachments, inline image resize/persistence, mail signatures, recursive personal folder create/move/delete with mails-inside confirmation, Personal Contacts with recipient autosave, domain-filtered Company Directory with owner/admin editing, and address-book recipient picker for To/Cc/Bcc in new/reply/forward forms. | #13, #14 | ✅ Implemented / ✅ Contacts and recipient picker closed 2026-09-28 |
| **#17** | Functional Admin Console Frontend | Functional Admin UI including Users/Domains/Quarantine; quarantine supports domain/date filtering, 2-button row actions, and "suspected spam released" tag on delivery. | #13, #15 | ✅ Implemented |
| **#18** | Automated Deployment & Live Verification | Build, migrate, publish Linux x64 Web, Worker, and AntiSpam binaries & SPAs, deploy to Debian LXC `10.11.1.51` behind `mail.miautrix.tech`, and verify end-to-end. | #16, #17 | ✅ Web + worker deploy verified 2026-09-21; ✅ Anti-malware “malware detected action=discard” verified 2026-09-24 |
| **CF-01** | Cloudflare Workers transport per domain | Cloudflare can be selected per tenant-owned domain; external recipients delivered via tenant worker. Queue Retry and Quarantine Release send required diagnostic/tags. | #18 | ✅ **Remediated 2026-10-04 (MIA-76/MIA-77/MIA-90)** — Worker source versioned at `workers/email/`, `POST /send` 1101 eliminated with strict bearer auth, inbound `email()` wildcard matching and webhook delivery remediated, and live endpoints verified with zero 1101s. |
| **CF-02** | Worker source recovered under version control | Deployed Worker source, config and deploy procedure committed to this repo with a recorded rollback point and no secret in the diff. | CF-01 | ✅ Completed 2026-10-04 (MIA-69) — `workers/email/` holds `wrangler.jsonc`, `src/index.ts`, byte-exact `deployed-artifact.index.js` and `README.md`. Rollback version `327baddf-2d9d-494f-9630-e383c4ca4aa4` recorded. Recovery only; no behaviour change, no redeploy. |
| **CF-03** | Fix Worker `POST /send` `1101` | Move request parsing inside `try`, return `400` on a malformed body, add the bearer check the handler lacks (`401`), media-type guards (`415`), payload limit (`413`), and error mapping (`202`/`502`/`503`). | CF-02 | ✅ **Completed 2026-10-04 (MIA-76).** `POST /send` now answers `401` on a bad token (checked before body is read), `415` on non-JSON, `400` on invalid body/missing fields/bad MIME, `413` on oversize (>25MB), `202` on success, `502` on send failure, `503` when `SEND_TOKEN` or binding is missing. Zero `1101` responses. Proved by `workers/email/verify-send-contract.sh` — 29 cases, 0 failed. Live production contract verified; deployed version 8 (`51e41cc4-db42-4e85-99ac-d0cef1bbdceb`). |
| **CF-05** | Deploy the `POST /send` fix | Set the `SEND_TOKEN` Worker secret, deploy via `wrangler`, re-run the contract check, and record version id and etag. | CF-03 | ✅ **Completed 2026-10-04 (MIA-76/MIA-81).** Worker secret `SEND_TOKEN` configured, deployed via `wrangler` as version 8 (`51e41cc4-db42-4e85-99ac-d0cef1bbdceb`), deployment `dce34c04-f57a-4b01-bed2-2992a982e6c6`, etag `0fbbd1058575946ca31d60093813f7437bccd35651e7baaada6179032e9b333b`. |
| **CF-04** | Fix inbound `email()` handler defects | Allow-list wildcard `"*@miautrix.tech"` never matches (exact `indexOf` compare); forward target `"inbox@corp"` is unroutable. Remediate with wildcard allowlist matching, configuration-driven destination (`POST /api/v1/inbound/cloudflare`), secret webhook token (`INBOUND_TOKEN`), and fail-closed error handling. | CF-02 | ✅ **Completed 2026-10-04 (MIA-77).** `isSenderAllowed` implements true domain wildcard matching and exact matching case-insensitively; rejects malformed/missing senders without throwing (no `1101`). Destination is configuration-driven (`env.INBOUND_DESTINATION` in `wrangler.jsonc` `vars`); authenticated webhook delivery injects `X-Miautrix-Inbound-Token` from Worker secret; delivery failures fail closed and loud (`message.setReject()`) to generate NDR rather than silent drops. Proved by `workers/email/verify-inbound-contract.sh` (14/14 passed) and `workers/email/verify-send-contract.sh` (29/29 passed). |
| **CF-06** | Remediate external email flow and validate DNS/DKIM | End-to-end remediation and verification: recover/version Worker source, diagnose/fix Worker POST /send 1101, configure/verify inbound Email Routing, reconcile DKIM selector/signing, and assess SMTP/IMAP reachability and certificate path. | CF-01, CF-04, CF-05 | ✅ **Completed 2026-10-04 (MIA-90).** Comprehensive remediation and verification completed. Pass/fail matrix delivered across all 14 evaluation areas. Live Worker returns zero 1101s, edge shield rule verified, DKIM selector reconciled with live DNS `cf2024-1`, and residual risks documented. |

---

## 4. Quality Management & Verification Gates

Each work package carries strict Acceptance Criteria under the EARS standard (*WHEN `<trigger>` THE SYSTEM SHALL `<observable response>`*):

1. **Zero Compiler Warnings:** `dotnet build Miautrix.Mail.sln -warnaserror` enforced across all builds.
2. **Tenant Isolation Invariant:** Cross-tenant resource queries return HTTP 404, never 403, logged to security events.
3. **Architecture Enforceability:** Domain layer has 0 external dependencies. Controllers contain zero business logic.
4. **Data Layer Integrity:** Schema changes are forward-only (Expand → Migrate → Contract) managed strictly through EF Core migrations.
5. **Security & Privacy:** Passwords and secrets are never logged; invitation tokens and API keys are stored as cryptographically secure hashes.
6. **Shared Mailbox Identity Boundary:** Shared mailboxes are mailbox resources, not login identities. Creation does not require or process a password and does not create `User`, `UserCredential`, or `Membership` rows.
7. **Delegate Authorization:** Shared mailbox delegates must be active same-tenant, exact-domain users. `read` delegates may read mailbox/folder/message/attachment content; `write` delegates may perform approved mutations such as mark-read, move, delete, and send.
8. **Quarantine Lifecycle:** Discard updates quarantine status to `Discarded` without deleting database rows or `.eml` artifacts; **Anti-malware “malware detected action”** can alternatively **discard automatically from the SMTP queue** (no quarantine row) when enabled; Release queues the message for delivery with `[SPAM Supected-Released]` subject tagging and worker anti-spam bypass.
9. **Webmail Draft/Folder UX:** Draft attachments persist across save/reopen/send/discard lifecycle; custom personal-folder mutations require write authorization through the shared helper; deleting folders with mails requires explicit user confirmation that warns about mails/messages inside.
10. **Webmail Contacts UX:** Company Directory lists active tenant mailbox/group addresses and remains tenant/domain isolated; owner/admin users may edit allowed directory display fields; sending mail best-effort autosaves new recipients to Personal Contacts; composer recipient picker can fill To/Cc/Bcc from Personal Contacts and Company Directory without changing send/draft API payload shapes.

---

## 5. Risk Management Matrix

| Risk ID | Risk Description | Likelihood | Impact | Mitigation Strategy |
|---|---|---|---|---|
| **RSK-01** | Missing runtime dependencies (`pnpm`, PostgreSQL) during deployment testing | High | Medium | Containerize development/test environments via Docker / Testcontainers. |
| **RSK-02** | Mail deliverability issues due to DNS / DKIM / SPF misconfigurations | Medium | High | Implement built-in DNS health validator and automated DKIM record generator. |
| **RSK-03** | Frontend code drift between Web Admin and Desktop App | Medium | Medium | Shared component library and single React application codebase bundled into Electron. |
| **RSK-04** | Data loss during mailbox migrations or updates | Low | Critical | Strict forward-only migration pattern; verified automated backup/restore verification on staging before promotion. |
| **RSK-05** | License service downtime locking out customers | Low | High | License enforcement design specifies fail-open behavior: mail flows continuously regardless of licensing server availability. |
| **RSK-06** | Production component deployed from source outside version control, making it unreviewable and unrevertable | ~~High~~ Closed | Critical | **Realized and closed 2026-10-04 (MIA-69).** The Cloudflare Worker ran for three weeks from an external repo and dashboard quick-editor edits; five of its six versions were uploaded from the dashboard, so there was no reviewable diff and no rollback point in this repo. Source is now committed at `workers/email/` with the deployed version id, deployment id, etag and artefact sha256 recorded. Mitigation going forward: any component in the production mail path must be deployed from a committed definition in this repo, never from the dashboard editor. **Reinforced 2026-10-04 (MIA-76):** the first functional change to the Worker since recovery was made as a reviewable commit (`479889e`) on a branch, built with `npx wrangler deploy --dry-run`, and is deployed by one `npx wrangler deploy` from `workers/email/` — no dashboard quick-editor edit. Residual exposure: the deploy itself has not yet happened, because the available API token is read-only, so the live script is still version 6. |
| **RSK-07** | Unrestricted Cloudflare `send_email` binding | Medium | High | The deployed `EMAIL` binding has no `allowed_destination_addresses`, so it can send to any verified destination. Narrowing it is a behaviour change and was deliberately excluded from MIA-69's recovery-only scope. Raised for security review before the component is called production-ready. |
| **RSK-08** | Production deploy of the mail Worker is gated on a credential the project does not hold | Realized | High | The only Cloudflare API token available to this project is **Workers Scripts: Read** (`743069de1fde2bf2c21d02d833907cb0`). It reads script settings, versions and deployments, and is refused on every write: `wrangler secret put`, `wrangler versions upload` and `wrangler versions deploy`. Realized on MIA-76, where a verified `POST /send` fix could not be deployed. Mitigation: obtain a token scoped to **Account → Workers Scripts → Edit**, stored as a Paperclip secret and injected at deploy time. Until then every Worker change is reviewable and testable locally but not shippable, and the live script stays on version 6. |

---

## 5.1 Change Log

| Date | Change | Verification |
|---|---|---|
| 2026-10-04 | **Remediated external email flow, versioned Worker source, eliminated POST /send 1101, reconciled DKIM, and assessed SMTP/IMAP reachability & certificate path (MIA-90).** Synthesized and verified end-to-end remediation following MIA-64 QA baseline. Versioned Worker source at `workers/email/` (`src/index.ts`, `wrangler.jsonc`, `deployed-artifact.index.js`, tests). Verified `POST /send` 1101 elimination and live contract (`401` unauthorized, `405` method, `400` validation, zero 1101s). Remediated inbound `email()` wildcard allowlist matching and authenticated webhook dispatch (`POST /api/v1/inbound/cloudflare` with `INBOUND_TOKEN`). Reconciled DKIM: established live Cloudflare selector `cf2024-1` as authoritative in Cloudflare transport mode; identified stale DB selector `m1`. Assessed SMTP/IMAP reachability: Cloudflare CDN proxies HTTP/HTTPS (Webmail/Admin 200 OK with public Google Trust Services TLS); native ports 25/587/993 filtered externally on Anycast IP; origin uses Cloudflare Origin CA certificate. Verified Cloudflare Edge Shield WAF rule (MIA-88) shielding unauthenticated `/api/` while passing authenticated Bearer traffic and login to origin `AuthenticationMiddleware`. Delivered comprehensive 14-point pass/fail matrix and documented residual risks. | Live probes: `https://miautrix-main-worker.comarcat.workers.dev/` (health 200, unauthed /send 401, bad method 405, zero 1101s); `workers/email/verify-inbound-contract.sh` (14/14 passed); `workers/email/verify-send-contract.sh` (29/29 passed); DNS query: MX to Cloudflare, SPF configured, DMARC reject, DKIM `cf2024-1` verified; `https://mail.miautrix.tech/` (Webmail 200, Admin 200, OpenAPI 200, unauthed /api/ 403, Bearer /api/ 401 to origin); `dotnet build Miautrix.Mail.sln -warnaserror` (0 warnings, 0 errors); `dotnet test tests/Miautrix.Mail.UnitTests/Miautrix.Mail.UnitTests.csproj` (35 passed). |
| 2026-10-04 | **Removed hardcoded DB fallback and enforced fail-closed database connectivity (MIA-89).** Removed all hardcoded development connection strings and credentials from source code (`Web/Program.cs`, `Worker/Program.cs`, `Cli/Program.cs`, `Seeder/Program.cs`, `Persistence/AppDbContext.cs`, `generate_migration.sh`) and integration/protocol test fixtures. Enforced strict fail-closed behavior across all runtime entry points, EF design-time factories, migrations, and test bootstrap when `MIAUTRIX_DB_CONNECTION` is unset or whitespace (throwing `InvalidOperationException` or exiting with fatal error). Updated test suites to use injected test credentials with zero committed secrets. Added unit tests for fail-closed behavior. Solution builds cleanly with zero warnings/errors. | `dotnet build Miautrix.Mail.sln`; `dotnet test tests/Miautrix.Mail.UnitTests/Miautrix.Mail.UnitTests.csproj` (35 passed, 0 failed); `dotnet test tests/Miautrix.Mail.SecurityTests/Miautrix.Mail.SecurityTests.csproj` (6 passed, 0 failed); `dotnet test tests/Miautrix.Mail.EndToEndTests/Miautrix.Mail.EndToEndTests.csproj` (1 passed, 0 failed); verified `IntegrationTests` and `ProtocolTests` fail closed immediately when `MIAUTRIX_DB_CONNECTION` is unset. |
| 2026-10-04 | **Diagnosed Cloudflare rule blocking authenticated API, evaluated expression semantics/frontend request paths, and verified activated edge shield rule (MIA-88).** Performed secret-free inspection of Cloudflare custom firewall ruleset (`55eb9d2446f849299ba0ae0b2a130c46`, rule `7e70d965a59f4634b066f182264bd23a`) for `miautrix.tech`. Identified root cause of authenticated frontend failure under narrowed rule `(http.host eq "mail.miautrix.tech" and starts_with(http.request.uri.path, "/api/") and not http.request.uri.path in {"/api/v1/auth/login" "/api/v1/auth/refresh"})`: the rule only allowed login/refresh and blocked all other SPA initialization endpoints (`/api/v1/auth/me`, `/api/v1/mailboxes`, `/api/v1/messages`, `/api/v1/contacts`, `/api/v1/calendar/*`, `/api/v1/admin/*`), CORS `OPTIONS` preflights, and the inbound Worker webhook (`/api/v1/inbound/cloudflare`) with HTTP 403 at Cloudflare Edge before traffic could reach origin. Formulated edge shield expression incorporating token-presence check, OPTIONS preflight bypass, and public exemptions. Applied by founder in Cloudflare dashboard and verified live: unauthenticated API requests blocked at edge (403), authenticated requests and public login reach origin, static frontends serve 200 OK, and CORS preflights return 204. No secrets revealed. | Live read-only API and edge probes: `GET https://mail.miautrix.tech/` HTTP 200; `GET https://mail.miautrix.tech/admin/` HTTP 200; unauthenticated `GET /api/v1/auth/me` HTTP 403 Forbidden (Cloudflare edge block enforced); authenticated `GET /api/v1/auth/me` (with Bearer token) reaches origin `AuthenticationMiddleware`; `POST /api/v1/auth/login` HTTP 401 on bad credentials (origin reached); preflight `OPTIONS /api/v1/mailboxes` HTTP 204 No Content; `GET /openapi/v1.json` HTTP 200 OK. |
| 2026-10-04 | **Verified deployed DB configuration, service environment, and authentication flows on 10.11.1.51 and 10.11.1.52 (MIA-87).** Performed read-only inspection of application deployment (`Debian GNU/Linux 13`, `.NET 8.0.31`, NGINX reverse proxy, Kestrel backend on `127.0.0.1:5000`, 2 active workers) and PostgreSQL connectivity on `10.11.1.52:5432`. Identified named source and precedence of `MIAUTRIX_DB_CONNECTION` (systemd unit/`.env` environment variables over fallback development string in source). Verified authentication-failure logging/contract (`401 Unauthorized` with `auth_failed` code and request ID correlation; `422 Unprocessable Entity` on schema validation failure; `401 Unauthorized` on unauthenticated `/api/v1/auth/me`). Verified public login and authenticated `/api/v1/auth/me` across both Cloudflare Tunnel (`https://mail.miautrix.tech`) and internal IP (`http://10.11.1.51`) returning valid session tokens and user identity. No secrets revealed or stored in project logs. | Live read-only API probes: `POST /api/v1/auth/login` (200 OK with valid credentials, 401 on bad credentials, 422 on bad schema); `GET /api/v1/auth/me` (200 OK authenticated, 401 unauthenticated); `GET /api/v1/system/info` (200 OK, `"database_status": "Connected (PostgreSQL)"`, 278 tenants queryable); `GET /openapi/v1.json` (200 OK); static frontends verified at `/` and `/admin/`. |
| 2026-10-04 | **Fixed Worker inbound `email()` wildcard matching, unroutable forward, and webhook contract (MIA-77).** Remediated `email()` allow-list to support true domain wildcards (`*@miautrix.tech`) and exact addresses (`admin@miautrix.org`) case-insensitively while rejecting malformed/missing senders without throwing (no `1101`). Converted forward destination to configuration-driven (`env.INBOUND_DESTINATION` / `wrangler.jsonc` `vars`). Added authenticated HTTP webhook delivery carrying secret token (`INBOUND_TOKEN`) in `X-Miautrix-Inbound-Token` header without committing secrets. Added explicit fail-closed error handling (`message.setReject()`) on missing tokens or HTTP delivery failures (such as 403 containment) to ensure sending MTA receives an NDR rather than silently discarding mail. | `workers/email/verify-inbound-contract.sh` — 14 cases, **0 failed, 0 1101s**, covering wildcard domain matches, exact matches, mixed case, malformed sender rejection without throwing, webhook token header injection, fail-closed missing token rejection, and HTTP 403 / network failure handling. Regression check `workers/email/verify-send-contract.sh` — 29 cases, **0 failed**. `npx wrangler deploy --dry-run` builds clean. Rollback targets version 8 (`51e41cc4-db42-4e85-99ac-d0cef1bbdceb`) and version 6 (`327baddf-2d9d-494f-9630-e383c4ca4aa4`) preserved server-side. |
| 2026-10-04 | **Fixed the Worker `POST /send` contract (MIA-76), commit `479889e`.** Moved every parse (`request.json()`, `atob(raw)`, `new EmailMessage()`) inside a guard and added the bearer check the handler never had, so `POST /send` now returns `401` on a missing/wrong token — checked **before** the body is read — `400` on any malformed body, `500` on an `env.EMAIL.send()` failure, and `503` when the `SEND_TOKEN` secret is unset. No request shape returns Cloudflare `1101`. The token lives only in the `SEND_TOKEN` Worker secret; `wrangler.jsonc` still carries no `account_id` and no credential, and no secret appears in the diff. Inbound `email()` untouched. No app-side change: `CloudflareApiMailTransport` already sent the exact wire format and the `Authorization: Bearer` header the Worker now reads. **Deploy blocked** — the available Cloudflare API token is Workers Scripts *Read*, so `secret put`, `versions upload` and `versions deploy` are all refused with `No access to the specified resource.`; CF-05 tracks the deploy behind a Workers Scripts *Edit* token. | `workers/email/verify-send-contract.sh` — 23 cases, **0 failed**, against `npx wrangler dev --local` (wrangler 4.147.0), covering the unchanged `GET /`/`/health` → `200` and `GET /nope` → `404`, the `401` ordering case (bad token **plus** invalid body still `401`, proving the body was never parsed), and eleven `400` shapes that previously returned `1101`. Hand-proved additionally: valid MIME with a `Message-ID` → `200 {"ok":true}` (really reaches `env.EMAIL.send()`), and a runtime with no `SEND_TOKEN` → `503` while `/health` stays `200` (fails closed). `npx wrangler deploy --dry-run` builds clean. Rollback target `327baddf-2d9d-494f-9630-e383c4ca4aa4` read back from the versions API and confirmed still **active** as deployment `db40576c-f0b2-40d9-8720-44207ad9c692`. |
| 2026-10-04 | **Recovered the Cloudflare email Worker source into version control (MIA-69).** Read the deployed script `miautrix-main-worker` from the Cloudflare Workers API and committed it to `workers/email/` as `src/index.ts` (typed, annotated), `deployed-artifact.index.js` (byte-exact rollback artefact), `wrangler.jsonc` (reconstructed from the live settings/bindings response) and `README.md` (deploy command, bindings, rollback, platform coverage). Recorded the rollback point: version `327baddf-2d9d-494f-9630-e383c4ca4aa4` (number 6), deployment `db40576c-f0b2-40d9-8720-44207ad9c692`, etag `b6582d3ac146eacda28478b87c91bbbf6b07b4fbc0c911a20c0336d4083c0165`, artefact sha256 `58e7c87472e1b24e0b89f45811c528f5b3621255b3723e2fd01378be97b50e9d`. Named the `1101` root cause: in `POST /send`, `request.json()`, `atob(body.raw)` and `new EmailMessage(...)` run outside the `try`, which wraps only `env.EMAIL.send()`, and the handler has no bearer check at all. **Recovery only — no behaviour change, no deployment, no DNS or Email Routing change, `/api/` containment untouched.** No secret in the diff; `account_id` and token are injected at deploy time. | Source read from `GET /accounts/{acct}/workers/scripts/miautrix-main-worker/content/v2` (the deployed module itself). Build equivalence: `npx wrangler deploy --dry-run --outdir <tmp>` output diffed against `deployed-artifact.index.js` — differs only by whitespace and one trailing comma after normalising comments and esbuild export wrapping; no statement differs. Live read-only probes: `GET /` -> `200 {"ok":true}`, `GET /health` -> `200 {"ok":true}`, `GET /nope` -> `404 Not found`, all matching the recovered code. Rollback target confirmed present via the versions API; not executed, as executing it would be a deployment. `POST /send` and `email()` not exercised — would send or reject real mail. |
| 2026-10-03 | **Version 1.0 completed.** Closed the calendar/Webmail stabilization work: public RSVP uses link-style accept/tentative/decline actions with a protected POST submission; reschedule requests generate organizer Accept/Decline proposal links; invitation and proposal email times explicitly identify UTC with local-PC display guidance; bulk inbox deletion clears stale detail state and safely renders an empty folder. | `dotnet build Miautrix.Mail.sln -warnaserror`; `dotnet test tests/Miautrix.Mail.UnitTests/Miautrix.Mail.UnitTests.csproj --filter CalendarInvitationBuilder` (6 passed); manual post-deploy checklist in `CALENDAR_WEBMAIL_FINAL_SOLUTION_2026-10-03.md` |
| 2026-09-28 | Closed Webmail contacts/address-book section: Company Directory lists tenant mailbox/group addresses, owner/admin directory edits persist name/organization/department/phone, sent recipients autosave to Personal Contacts, and compose/reply/forward forms include a reusable contact picker for To/Cc/Bcc. | `dotnet build Miautrix.Mail.sln -warnaserror`; `pnpm --filter webmail exec vitest run src/tests/App.test.tsx -t "navigates to Compose view"`; `pnpm --filter webmail build` |
| 2026-09-27 | Closed Webmail draft/composer/personal-folder section: persisted draft attachments, inline image sizing persistence, italic/editor cleanup, mail signatures, recursive personal-folder rendering, custom folder delete endpoint, and delete confirmation when mails/messages exist. | `dotnet build Miautrix.Mail.sln -warnaserror`; `pnpm --filter webmail build` |
| 2026-09-27 | Refreshed knowledge references using graphify/codebase-memory and updated repository graph artifacts for current Webmail/API changes. | `graphify-out/GRAPH_REPORT.md`, `graphify-out/graph.json`, `graphify-out/manifest.json` |

---

## 6. Stakeholder & Resource Plan

- **Project Sponsor:** Miautrix Executive Leadership (Scope signoff, licensing terms)
- **Technical Architect & Lead Developer:** Full-stack .NET / Systems Architecture
- **DevOps / SysAdmin:** Debian packaging, systemd daemon setups, TLS/Certbot integrations
- **Auditors & Compliance:** Security audit logs, non-disclosure verification
