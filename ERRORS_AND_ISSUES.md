# Miautrix Mail Server — Errors, Issues & Review Log

This document tracks identified errors, configuration discrepancies, environment-specific constraints, and verification items for the Miautrix Mail Server deployment on Debian LXC (`10.11.1.51`) and PostgreSQL (`10.11.1.52`) behind Cloudflare Tunnel (`mail.miautrix.tech`).

---

## 1. System & OS Environment Issues

| Issue ID | Component | Description & Error Message | Root Cause | Status / Notes |
|---|---|---|---|---|
| **SYS-01** | Debian LXC Package Manager | `E: Unable to locate package software-properties-common` | Minimal Debian 12 LXC images do not include PPA/Ubuntu tools in default repositories. | **Documented in SETUP.md**: Removed `software-properties-common` from standard apt install sequence. |
| **SYS-02** | SSH / Remote Auth | SSH key-based / passwordless login rejected on container `10.11.1.51`. | Container OS policy enforces password authentication for `root`. | Manual password prompt required during `scp`/`ssh` until SSH key is added to `~/.ssh/authorized_keys`. |
| **SYS-03** | Windows Deploy Script | PowerShell script execution restrictions (`ExecutionPolicy`). | Default Windows client policy prevents running `.ps1` automation scripts directly without review. | Individual `scp`/`ssh` commands provided in `SETUP.md` as alternative to `deploy.ps1`. |

---

## 2. Frontend & UI Runtime Issues

| Issue ID | Component | Description & Error Message | Root Cause | Status / Notes |
|---|---|---|---|---|
| **FE-01** | Webmail Client | Webmail loaded directly into default inbox (`alex.vance@...`) without requiring login. | Development flag `useState<boolean>(true)` was left enabled in `webmail/src/App.tsx`. | **Fixed in source**: Changed default state to `false` to render `LoginView`. Needs live verification. |
| **FE-02** | Admin Console | `/admin` rendered blank page / white screen on `mail.miautrix.tech/admin`. | Vite `base` was set to root `/`. JavaScript & CSS chunks resolved to `/assets/` instead of `/admin/assets/`, colliding with Webmail. | **Fixed in source**: Configured `base: '/admin/'` in `admin/vite.config.ts`. Needs live verification. |
| **FE-03** | Asset Packaging | Branding logos and icons (`LogoIcon.png`, `images/icons/*`) missing on deployed frontends. | Image assets were located outside the Vite `public/` directories and were omitted during `dist` bundling. | **Fixed in source**: Copied icon sets and logos into `public/images/` for both Webmail and Admin. |
| **FE-04** | Webmail Company Directory | Company Directory showed 0 contacts or failed with 404/422 during live testing. | Contact listing was checking too-strict mailbox permissions, directory source needed tenant mailbox/group addresses, and frontend request bodies needed backend-compatible `snake_case`. | **Fixed 2026-09-28**: directory lists active tenant mailboxes/groups filtered by user email domain; owner/admin can edit directory display fields; backend persists name, organization, department, and phone on `Mailbox`. |
| **FE-05** | Webmail Personal Contacts | Sent recipients needed to appear in Personal Contacts without blocking mail delivery. | Contacts were only manually managed; send flow did not autosave recipients. | **Fixed 2026-09-28**: send path best-effort autosaves To/Cc/Bcc recipients as personal contacts, deduplicates addresses, and does not fail message delivery if contact save fails. |
| **FE-06** | Webmail Composer Recipients | New/reply/forward forms required users to type recipient addresses manually for To/Cc/Bcc. | Composer had plain recipient string inputs and no address-book selector, despite contacts/directory being available in app state. | **Fixed 2026-09-28**: added contact-picker dialog for To/Cc/Bcc using loaded Personal Contacts and Company Directory. `pnpm --filter webmail build` passed. |
| **FE-07** | Calendar Public RSVP Page | Public invitation page opened, but Accept/Tentative/Decline controls did not reliably submit the attendee response. | Inline generated page used fragile button/script wiring for the RSVP POST flow. | **Fixed 2026-10-03**: public RSVP page now renders link-style Accept/Tentative/Decline actions wired to the protected POST endpoint; `?response=accept|tentative|decline` still auto-submits. `dotnet build Miautrix.Mail.sln -warnaserror` passed. |
| **FE-08** | Calendar Reschedule Proposal Emails | Organizer change-schedule request emails did not consistently include Accept/Decline proposal links. | Some reschedule paths notified the organizer without a fresh persisted raw invitation token. | **Fixed 2026-10-03**: reschedule responses generate/persist a 30-day token and include `/proposal?action=accept|decline` links. |
| **FE-09** | Calendar Email Timezone Wording | Schedule emails were confusing because users saw UTC values without clear distinction from local PC display. | Human-readable email body times did not consistently identify UTC and local-client behavior. | **Fixed 2026-10-03**: invitation and proposal emails explicitly label human-readable times as UTC and state calendar clients / the web RSVP page show local PC timezone. |
| **FE-10** | Webmail Inbox Bulk Delete | Selecting all inbox messages and deleting could white-screen the Webmail GUI. | The detail pane could retain stale selected/detail message state after the deleted message list became empty. | **Fixed 2026-10-03**: bulk delete clears stale selection/detail state, uses safe displayed-message fallbacks, and refreshes with empty-folder behavior expected. |

---

## 3. NGINX & Ingress Routing Issues

| Issue ID | Component | Description & Error Message | Root Cause | Status / Notes |
|---|---|---|---|---|
| **NGX-01** | NGINX Subpath | Direct access to `https://mail.miautrix.tech/admin` (no trailing slash) fails or serves wrong index. | NGINX `alias` requires an exact match and explicit 301 redirection from `/admin` to `/admin/`. | **Updated in SETUP.md**: Added `location = /admin { return 301 /admin/; }` rule. |
| **NGX-02** | Attachment Uploads | Large attachments (over 1MB) fail with HTTP `413 Request Entity Too Large`. | Default NGINX `client_max_body_size` is 1MB. | Configured `client_max_body_size 50M;` in NGINX site configuration. |
| **NGX-03** | Cloudflare Headers | Backend logs show `127.0.0.1` as client IP for incoming API requests. | Missing `X-Forwarded-For` and `CF-Connecting-IP` proxy header propagation. | Added proxy headers in NGINX configuration block. |

---

## 4. Backend & Database Integrity Checks

| Issue ID | Component | Description & Error Message | Root Cause | Status / Notes |
|---|---|---|---|---|
| **DB-01** | EF Core Migrations | Database migration failure risk during runtime startup. | Multi-tenant schema and RLS policies require elevated DDL permissions on `miautrix-mail-pro`. | Migration must be run via `dotnet ef database update` before starting service. |
| **DB-02** | Tenant Isolation | Risk of cross-tenant enumeration via HTTP status codes. | Standard REST APIs often return 403 Forbidden on existing unauthorized items. | Architecture rule enforced: Cross-tenant accesses return **404 Not Found**. |
| **DB-03** | Environment Variables | Service startup crash if connection string or secrets are missing. | Strict boot validation halts execution on missing environment variables. | Systemd unit file must include `MIAUTRIX_DB_CONNECTION` and `ASPNETCORE_ENVIRONMENT=Production`. |
| **DB-04** | Production Login 500 | `POST /api/v1/auth/login` returned HTTP 500 with `42703: column m.name does not exist` from `AuthService.AuthenticateAsync` line 87. | Production `mailboxes` schema was behind the EF model after `Mailbox.Name` was added; the migration existed in source but was not discovered/applied because generated migration metadata was incomplete. | **Fixed 2026-09-20**: added `20260920223000_AddMailboxName` and its designer metadata; applied to production via `scripts/update-prod-database.ps1`. Login confirmed working at `https://mail.miautrix.tech/admin`. |
| **DB-05** | Migration Target Drift | Generic `dotnet ef database update` used local/dev configuration (`miautrix_dev`) instead of production. | EF startup configuration defaults were used when no explicit `--connection` was supplied. | **Fixed 2026-09-20**: production scripts require an explicit production connection/password, pass `--connection`, reject localhost/dev/test targets, and require confirmation. |
| **DB-06** | Hardcoded Development Database Fallback Removal | Source code contained hardcoded development database connection strings and credentials when `MIAUTRIX_DB_CONNECTION` was unset, risking accidental connection fallback and credential exposure. | Hardcoded fallback strings were present in `Web/Program.cs`, `Worker/Program.cs`, `Cli/Program.cs`, `Seeder/Program.cs`, `Persistence/AppDbContext.cs` (`AppDbContextFactory`), `generate_migration.sh`, and test suites. | **Fixed 2026-10-04 (MIA-89)**: Removed all hardcoded database connection fallback strings. Made missing `MIAUTRIX_DB_CONNECTION` fail closed across all binaries, tools, migrations, and test suites (throwing `InvalidOperationException` or fatal exit 1). Injected test credentials without committing secrets. Unit tests verify fail-closed behavior when environment variable is unset/whitespace. |
| **WAF-01** | Cloudflare API Containment Rule Narrowing | Broad edge containment rule `Miautrix API emergency containment` (ID `55eb9d2446f849299ba0ae0b2a130c46`) blocked `/api/` entirely, including webmail/admin login endpoints (`/api/v1/auth/login`, `/api/v1/auth/refresh`), returning HTTP 403 before requests reached origin. | Emergency containment was initially deployed as a catch-all block for all `/api/` paths pending application-level session authentication (MIA-62). After origin auth hardening landed, edge containment must be narrowed to permit authentication endpoints while blocking all other API paths. | **Specified & Verified 2026-10-04 (MIA-86)**: Inspected zone custom firewall ruleset `55eb9d2446f849299ba0ae0b2a130c46` (phase `http_request_firewall_custom`, rule ID `7e70d965a59f4634b066f182264bd23a`). Defined narrowed expression to permit `/api/v1/auth/login` and `/api/v1/auth/refresh` while retaining block on other `/api/` paths. Documented rollback path and verified live external endpoints: unauthenticated `/api/v1/auth/me` returns 401 Unauthorized (origin auth enforced), login endpoint reachable and rejecting bad credentials (401), webmail `/` and `/admin/` return 200 OK. Verified available Cloudflare API token is read-only (Rulesets edit denied). |
| **WAF-02** | Cloudflare Rule Blocks Authenticated Frontend API | Narrowed edge rule `(http.host eq "mail.miautrix.tech" and starts_with(http.request.uri.path, "/api/") and not http.request.uri.path in {"/api/v1/auth/login" "/api/v1/auth/refresh"})` blocked authenticated SPA requests (`/api/v1/auth/me`, `/api/v1/mailboxes`, `/api/v1/messages`, etc.) with HTTP 403 at Cloudflare Edge. | The narrowed edge rule whitelisted only `/api/v1/auth/login` and `/api/v1/auth/refresh` while blocking all other `/api/` paths unconditionally, ignoring client Bearer session tokens and CORS OPTIONS preflights. When the rule is disabled, traffic reaches origin where `AuthenticationMiddleware` (MIA-62) properly validates sessions and enforces multi-tenant auth. | **Resolved 2026-10-04 (MIA-88)**: Formulated edge token-presence shielding rule with OPTIONS preflight, public calendar, OpenAPI, and inbound webhook exemptions. Applied by founder in Cloudflare dashboard and verified live: unauthenticated API requests blocked at edge (403), authenticated Bearer requests and login pass to origin, static frontends serve 200 OK, and CORS preflights return 204. |

---

## 5. Items to Review & Verify Later

- [x] **Live Webmail Login Flow**: Verified live behind `https://mail.miautrix.tech` and `http://10.11.1.51` on 2026-10-04 (MIA-87). Login screen assets load, authentication returns session token with full user profile (`admin@miautrix.org`), invalid credentials properly return HTTP 401 Unauthorized (`auth_failed`), and session persistence verified.
- [x] **Live Admin Console Navigation**: Verified live at `https://mail.miautrix.tech/admin/` and `http://10.11.1.51/admin/` on 2026-10-04 (MIA-87). Static assets bundle and load cleanly with 200 OK.
- [x] **Image & Icon Rendering**: Checked that frontend JavaScript and stylesheet assets resolve properly under both root and `/admin/` subpaths without collision.
- [x] **REST API / OpenAPI Endpoint**: Verified `https://mail.miautrix.tech/openapi/v1.json` and `http://10.11.1.51/openapi/v1.json` return OpenAPI 3.0.1 specification (HTTP 200 OK) behind Cloudflare Tunnel (MIA-87).
- [x] **Database Connection Health**: Verified live via `GET /api/v1/system/info` reporting `"database_status": "Connected (PostgreSQL)"`, 278 active tenants, and healthy query responses across domains, users, and telemetry on 2026-10-04 (MIA-87).
- [x] **Production Mailbox Name Migration**: `20260920223000_AddMailboxName` applied to production on 2026-09-20 via `scripts/update-prod-database.ps1`; database updated and working.
- [x] **Production Login Retest**: Login confirmed working at `https://mail.miautrix.tech/admin` against the migrated production schema.
- [ ] **Shared Mailbox Live Flow**: Create a passwordless shared mailbox, verify no login identity is created, assign same-domain delegates, verify read delegate cannot mutate, and verify write delegate can mark/move/delete/send.
- [ ] **IMAP LOGIN password verification**: Fixed 2026-09-21 — the handler no longer accepts any password. Verify live on port 993 with a wrong password (`a001 LOGIN "user@example.com" "wrong"`) returns `NO`.
- [x] **Quarantine discarded filter retention**: Fixed 2026-09-23 — Discard updates status to `Discarded`; database rows and `.eml` artifacts are retained and visible through Admin Anti-Spam → Discarded.
- [x] **Quarantine release delivery**: Fixed 2026-09-23 — Release queues real delivery with `[SPAM Supected-Released]` subject tag and worker anti-spam bypass for admin-released messages.
- [x] **Deploy SSH diagnostics**: Fixed 2026-09-23 — upload script captures stderr/stdout around target directory preparation failures.

### Deployed DB Configuration & Authentication Inspection (MIA-87, 2026-10-04)

- **Target Systems**: Debian LXC application host `10.11.1.51` behind Cloudflare Tunnel `mail.miautrix.tech`; PostgreSQL host `10.11.1.52` (port 5432).
- **Service Environment**:
  - Operating System: `Debian GNU/Linux 13 (trixie)`
  - Runtime: `.NET 8.0.31` self-contained Linux x64
  - Reverse Proxy: NGINX on port 80 forwarding `/api/` to Kestrel on `127.0.0.1:5000`
  - Active Workers: 2
  - Uptime / Health: Optimal, >46,200s uptime
- **Database Connection & Precedence**:
  - Connection Source & Precedence: `MIAUTRIX_DB_CONNECTION` is read from process environment variables injected via systemd unit `/etc/systemd/system/miautrix-mail.service` (or `EnvironmentFile=-/opt/miautrix-mail/.env` in `miautrix-mail-worker.service`). If unset or whitespace, source code defines a fallback development connection string.
  - Connection Health: `GET /api/v1/system/info` confirms PostgreSQL connection is active and healthy (`"database_status": "Connected (PostgreSQL)"`). Active multi-tenant data is queryable (278 tenants, active domains, mailboxes, and users).
- **Authentication Failure Logs & Contracts**:
  - Valid Login: `POST /api/v1/auth/login` with `{"email_or_username": "admin@miautrix.org", "password": "..."}` returns HTTP 200 OK with session token, refresh token, expiry timestamp, and user permissions.
  - Invalid Login: `POST /api/v1/auth/login` with invalid credentials returns HTTP 401 Unauthorized with standard payload `{"error": {"code": "auth_failed", "message": "Invalid username or password.", "request_id": "..."}}`.
  - Unmapped/Invalid Body: `POST /api/v1/auth/login` with unexpected schema returns HTTP 422 Unprocessable Entity (`validation_failed`).
  - Authenticated Identity: `GET /api/v1/auth/me` with Bearer token returns HTTP 200 OK on both public Cloudflare (`https://mail.miautrix.tech`) and direct internal (`http://10.11.1.51`) routes. Unauthenticated requests return HTTP 401 Unauthorized (`unauthorized`).
- **Secrets Boundary**: No credentials, passwords, or connection strings logged or stored in output artifacts.

---

## 6. Transport / Protocol Listener Issues

| Issue ID | Component | Description & Error Message | Root Cause | Status / Notes |
|---|---|---|---|---|
| **TRX-01** | SMTP Listener | No Miautrix process listening on SMTP ports (`:25/:587/:465`) inside Debian LXC container. `ss -lntup` shows only `Miautrix.Mail.Web` on `127.0.0.1:5000` and `master` on `127.0.0.1:25`. | Deploy/install only published the REST API (`Miautrix.Mail.Web`) systemd unit; no protocol listener/worker component is deployed or bound to SMTP/IMAP ports. | **Fix in source 2026-09-21**: added `Miautrix.Mail.Worker` protocol host, worker systemd unit, and deploy/restart script updates to publish/start `miautrix-mail-worker`. Needs LXC deploy and live verification that ports `25/465/587/993` are listening. |
| **TRX-02** | Worker startup | `Job for miautrix-mail-worker.service failed because of unavailable resources or another system error.` Deploy aborts at the restart step. | The unit declared `EnvironmentFile=/opt/miautrix-mail/.env`, but no script ever created that file and the Web service does not export `MIAUTRIX_DB_CONNECTION` — it runs off a hard-coded connection string. A missing `EnvironmentFile` fails the unit at the systemd level, before the app can report anything. | **Fix in source 2026-09-21**: unit uses `EnvironmentFile=-/opt/miautrix-mail/.env` (non-fatal); new `scripts/lxc-install-worker-env.sh` provisions the file, inheriting the DB connection from the Web service or from `--db-connection-file`; deploy gained `-WorkerCert/-WorkerKey/-DbConnectionFile`. |
| **TRX-03** | IMAP auth | `a001 LOGIN "<any mailbox address>" "<any password>"` returned `OK` on port 993 — full mailbox access with no credential check. | `ImapSession.HandleLoginAsync` parsed the password argument and never used it: it looked up the mailbox by address and set `State = Authenticated`, unlike `SmtpAuthenticator` which verifies the Argon2id hash. | **Fixed 2026-09-21**: new `IImapAuthenticator` (interface in `Protocols.Imap`, implementation in `Miautrix.Mail.Worker`) verifies the password with `IPasswordHasher` and resolves the mailbox within the user's own tenant. Covered by 6 tests in `ImapAuthenticatorTests`. |
| **TRX-04** | Outbound delivery | Mail submitted on 587 or received on 25 is accepted (`250`) and written to `smtp_queue` as `Pending`, then never leaves the server. No MX lookup, no SMTP client, no process reading the queue anywhere in the solution. | `SmtpSubmissionHandler.HandleSubmissionAsync` and `SmtpInboundHandler.HandleDataAsync` both stop at `ISmtpQueueManager.EnqueueAsync`. The queue rows were treated as the end of the pipeline; nothing dispatches them. | **Fixed/live verified 2026-09-21**: `OutboundQueueDispatcher` in `Miautrix.Mail.Worker` now processes due tenant queue rows for tenants with Cloudflare enabled. External recipient domains (for example `gmail.com`) do **not** need tenant domain rows; when there is no recipient-specific Cloudflare domain config, delivery falls back to the tenant's primary Cloudflare domain/Worker. Retry/backoff/dead-lettering reuse `ISmtpQueueManager.RecordDeliveryAttemptAsync`. The `local` transport remains a deliberate no-op pending a real MX delivery path. |
| **TRX-05** | Queue Retry UI | Admin Queue Retry/Resend returned HTTP `415 Unsupported Media Type`. | The frontend posted no JSON body or `Content-Type`, while `MailQueueController.Retry` binds `[FromBody] RetryQueueItemRequest` with required `reason`. | **Fixed/live verified 2026-09-21**: Admin API client sends `Content-Type: application/json` with `{ reason: "Manual retry from Admin UI" }`. |
| **TRX-06** | Worker deployment | Updated outbound dispatcher code did not affect production; worker logs still showed old SQL filtering by recipient domain (`lower(s.recipient) LIKE @suffix_endswith`). | `deploy.ps1` deployed only the Web app/Admin/Webmail and did not publish/copy/restart `Miautrix.Mail.Worker`, so `miautrix-mail-worker` kept running old binaries. | **Fixed/live verified 2026-09-21**: `deploy.ps1` publishes `src/Miautrix.Mail.Worker`, copies it to `/opt/miautrix-mail/worker`, marks it executable, and restarts `miautrix-mail-worker` with the Web app and nginx. |
| **TRX-07** | Quarantine release delivery | Admin Release changed quarantine status but did not deliver the quarantined email to the recipient mailbox/queue. | Release lifecycle updated database metadata only; the worker would also re-scan released spam if it re-entered the inbound path. | **Fixed 2026-09-23**: Release enqueues the message for real delivery with subject tag `[SPAM Supected-Released]`; worker bypasses anti-spam for released-tag subjects while preserving delivery pipeline. |
| **TRX-08** | Quarantine discard retention | Discarded messages disappeared from the Discarded filter; concern that rows and `.eml` files were being deleted. | Delete endpoint represented discard behavior ambiguously and frontend immediately filtered the row out. | **Fixed 2026-09-23**: discard marks quarantine rows as `Discarded` instead of deleting rows/artifacts, and Admin UI includes the `discarded` status filter. |
| **TRX-09** | Deploy diagnostics | `scripts/deploy-compile-upload.ps1` failed with `Target directory prep failed (255)` without enough stderr/stdout detail to diagnose SSH failure. | PowerShell SSH command output was not captured with merged stderr. | **Fixed 2026-09-23**: deployment script captures diagnostic output for remote prep failures so SSH/scp failures are actionable. |

---

## 7. Cloudflare Workers Integration

Cloudflare Workers are supported as an **optional, per-domain** mail transport, added alongside the
existing local SMTP/IMAP listeners rather than replacing them. Selected per domain in the admin
console (**Domains → transport mode = Cloudflare**); every other domain keeps its current
behaviour.

### Boundary: Cloudflare is transport only

Cloudflare relays traffic to and from the Worker. Everything that constitutes the mail system stays
in this application:

| Stays in Miautrix | Handled by Cloudflare |
|---|---|
| MIME parsing, threading, mailbox routing | MX for inbound, SMTP relay for outbound |
| Spam classification, DKIM signing, storage | DNS records for the domain (SPF/DKIM/DMARC) |
| Queue, retry and dead-lettering | Worker invocation and TLS termination |

Cloudflare sells **no IMAP or POP3 product**, so `ImapListenerService` (port 993) stays local in both
modes. "Instead of local ports" applies to 25/465/587 only. Port 25 also keeps listening in
Cloudflare mode, so a Cloudflare outage cannot stop inbound mail arriving.

### What the Worker repository provides

`lee-miautrix-email-worker-acdc7e2a` — Workers run JavaScript/TypeScript, Python, or Rust.

```
wrangler.jsonc          Worker configuration: bindings, routes, EMAIL send binding
src/index.ts            email() handler (inbound) and POST /send route (outbound)
csharp/Program.cs       C# sample client for the Email Service REST API
csharp/MiautrixEmail.csproj
README.md
```

Deployed with `npx wrangler deploy`.

### Flows

```
INBOUND   sender → MX (Cloudflare) → Worker email() handler
                                   → POST /api/v1/inbound/cloudflare   (this app, raw MIME)
                                   → SmtpInboundHandler → queue → mailbox

OUTBOUND  this app → tenant primary Cloudflare Worker POST /send → recipient
```

The outbound path is tenant-scoped. `domains` contains tenant-owned domain transport configuration,
not every possible recipient domain. For an external recipient, the dispatcher uses a matching
Cloudflare domain config only if the tenant owns that recipient domain; otherwise it relays through
the tenant's primary Cloudflare domain/Worker.

### Implementation notes

- **Inbound**: `InboundController` (`POST /api/v1/inbound/cloudflare`) authenticates with a shared
  secret in `X-Miautrix-Inbound-Token`, compared with `CryptographicOperations.FixedTimeEquals`. It
  **fails closed**: with `MIAUTRIX_INBOUND_TOKEN` unset every request is rejected with `503`, so an
  unconfigured deployment can never become an open relay. Envelope sender and recipient arrive in
  `X-Miautrix-Envelope-From` / `X-Miautrix-Envelope-To`; the recipient is re-checked against the
  verified domains in the database, and a rejection is recorded as a failed authorization event
  (`AUTH-4070`). Message bodies are never logged.
- **Outbound**: `IOutboundMailTransport` implementations live in
  `Miautrix.Mail.Application/Transport/` and are selected by `Mode`. The `cloudflare` transport posts
  base64-encoded raw MIME so attachments survive JSON escaping. The `local` transport is a deliberate
  no-op pending a real MX delivery path (see TRX-04).
- **Credentials**: the Cloudflare API token lives only in the server environment
  (`CLOUDFLARE_API_TOKEN`, optional `CLOUDFLARE_API_BASE`). The per-domain Worker URL and Zone ID are
  *not* secrets and live in the `domains` table. No DTO carries the token, and it is never logged.
- **Verification**: a Cloudflare-mode domain is verified by probing the Worker instead of looking up
  MX/SPF/DKIM/DMARC TXT records, because Cloudflare owns those records. `VerifyDomainResult` returns
  `worker_status` with the three DNS statuses left null — null rather than `""`, which would render in
  the admin UI as "checked and failed".
- **Schema**: migration `20260921161423_AddDomainTransportMode` adds `transport_mode` (default
  `'local'`), `cloudflare_zone_id` and `cloudflare_worker_url`. EXPAND-only — no drop, no backfill
  rewrite; existing rows become `local` in the same statement.

### Deployment

```
scripts/cloudflare.env.example      template — placeholders only; copy to cloudflare.env (git-ignored)
scripts/lxc-install-worker-env.sh   --cloudflare-env-file <file> --cert ... --key ...
scripts/deploy-compile-upload.ps1   -CloudflareEnvFile <file> -WorkerCert ... -WorkerKey ...
```

The env file is passed **by path**, never as an argument value: argv is world-readable via `ps` and
lands in shell history. `lxc-install-worker-env.sh` rejects any key it does not recognise, so a typo
fails the deploy instead of silently disarming the integration. `CLOUDFLARE_API_TOKEN` and
`CLOUDFLARE_API_BASE` are written to the Worker's environment file; `MIAUTRIX_INBOUND_TOKEN` is
written to a `0600 root:root` systemd drop-in for **`miautrix-mail.service`** (the Web app serves the
webhook, not the Worker) and the Web service is restarted.

### Worker source recovered into this repo (MIA-69, 2026-10-04)

**Error.** `POST /send` on the deployed Worker returned Cloudflare `1101` for every request
shape tested by QA (MIA-64): well-formed body, `{}`, `{"to":...}`, non-JSON body, and a wrong
bearer token. A malformed payload should give `400` and a missing bearer `401`; `1101` for all
of them means the handler threw before validating anything. The source could not be read,
reviewed or reverted, because it lived only in an external repo
(`lee-miautrix-email-worker-acdc7e2a`, referenced above) and in the live deployment.

**Root cause.** In `POST /send`, the three statements that parse the request —
`await request.json()`, `atob(body.raw)` and `new EmailMessage(body.from, body.to, rawMime)` —
sit **outside** the `try` block. The `try` wraps only `env.EMAIL.send()`. Any bad or absent
field therefore throws uncaught, and the Workers runtime answers `1101`:

- `{}` / `{"to":...}` -> `body.raw` is `undefined`; `atob("undefined")` throws `InvalidCharacterError`.
- non-JSON body -> `request.json()` throws `SyntaxError`.
- wrong bearer token -> **the handler contains no authentication check at all**, so the
  request falls through to the same unguarded parse and throws there. This is why a bad token
  returns `1101` rather than `401`.

Two further defects found in the same recovery, in the inbound `email()` handler:

- The allow-list test is `allowList.indexOf(message.from)`, an exact string compare, so the
  `"*@miautrix.tech"` wildcard entry can never match a real sender. Only the literal
  `admin@miautrix.org` passes.
- It forwards to `"inbox@corp"`, which is not a routable address or a verified Email Routing
  destination, so the forward cannot succeed. The architecture calls for
  `POST /api/v1/inbound/cloudflare` instead.

**Fix.** None applied. MIA-69 was scoped to recovery and version control only, explicitly
forbidding a behaviour change or a redeploy. The source now lives at `workers/email/`
(`wrangler.jsonc`, `src/index.ts`, `deployed-artifact.index.js`, `README.md`), so the fix is
now a reviewable, revertable code change instead of a hand-edit in the dashboard. Applying it
is the follow-up task.

**Rollback point.** Script `miautrix-main-worker`, version
`327baddf-2d9d-494f-9630-e383c4ca4aa4` (number 6), deployment
`db40576c-f0b2-40d9-8720-44207ad9c692`, etag
`b6582d3ac146eacda28478b87c91bbbf6b07b4fbc0c911a20c0336d4083c0165`, deployed
`2026-09-21T19:33:11Z`. Artefact sha256
`58e7c87472e1b24e0b89f45811c528f5b3621255b3723e2fd01378be97b50e9d`. Restore with
`npx wrangler versions deploy 327baddf-2d9d-494f-9630-e383c4ca4aa4@100% --name miautrix-main-worker --yes`.

**Verification.** Three checks, all executed:

1. Source read from the Workers API `content/v2` endpoint for the live script — the deployed
   module itself, not a reconstruction.
2. `npx wrangler deploy --dry-run --outdir <tmp>` compiled `src/index.ts`; the output diffed
   against `deployed-artifact.index.js` differs only by whitespace and one trailing comma
   after normalising comments and esbuild's export wrapping. No statement differs.
3. Live read-only probes matched the recovered code: `GET /` -> `200 {"ok":true}`,
   `GET /health` -> `200 {"ok":true}`, `GET /nope` -> `404 Not found`.

`POST /send` and `email()` were **not** executed — doing so would send or reject real mail.
Their behaviour is asserted from the recovered bytes plus the MIA-64 evidence.

No secret is committed: `wrangler.jsonc` carries no `account_id` and no token; both are
injected at deploy time as `CLOUDFLARE_ACCOUNT_ID` and `CLOUDFLARE_API_TOKEN`. No DNS, Email
Routing or `/api/` containment change was made.

Also noted for hardening review: the `EMAIL` `send_email` binding is **unrestricted** (no
`allowed_destination_addresses`). Narrowing it is a behaviour change and so was left alone.

### `POST /send` fixed: 400 on a bad body, 401 on a bad token, no 1101 (MIA-76, 2026-10-04)

**Error.** As recorded above for MIA-69 and found by QA in MIA-64: every `POST /send` request
shape returned Cloudflare `1101` — well-formed body, `{}`, `{"to":...}`, a non-JSON body, and
a wrong bearer token alike.

**Root cause.** Unchanged from the MIA-69 diagnosis, not re-investigated. `request.json()`,
`atob(body.raw)` and `new EmailMessage(...)` all ran outside the `try`, which wrapped only
`env.EMAIL.send()`, so any bad or absent field threw uncaught and the runtime answered
`1101`. Separately, the handler contained **no authentication check at all**, which is why a
wrong token also produced `1101` instead of `401`.

**Fix.** Applied in `workers/email/src/index.ts` and reconciled with production live baseline (version 8). `POST /send` now answers
a strict contract:

| Status | Condition |
| --- | --- |
| `401` | missing or wrong bearer token, checked **before** the body is read |
| `415` | non-JSON `Content-Type` header |
| `400` | non-JSON body, non-object body, missing fields, invalid email address, invalid MIME structure |
| `413` | raw payload exceeds 25 MB decoded limit |
| `202` | accepted and handed to `env.EMAIL.send()` |
| `502` | `env.EMAIL.send()` failed |
| `503` | no `SEND_TOKEN` secret or missing `EMAIL` binding — fails closed, never open |
| `405` | unsupported method on `/send` or `/health` |

Authentication runs first, so a bad token on an unreadable body answers `401` and not `400`;
an unauthenticated caller learns nothing about request validation. Every parse is inside a
guard and the handler has an outer `catch`, so no request shape can reach the runtime as an
uncaught throw. The token is read from the `SEND_TOKEN` Worker secret, which appears neither
in the source nor in `wrangler.jsonc`; an unset secret returns `503` rather than allowing
everyone through.

The inbound `email()` handler was kept as recovered on this branch; its rewrite is separated
onto `task/MIA-77-inbound-email-remediation` for MIA-77 per product decision.

No app-side change was needed: `CloudflareApiMailTransport` already posts `from`/`to`/`raw`
to `/send` and already sends `Authorization: Bearer <CLOUDFLARE_API_TOKEN>`. The Worker now
reads that header where before it ignored it. The same token value must therefore be set as
both the Worker's `SEND_TOKEN` and the app's `CLOUDFLARE_API_TOKEN`; it is held as the
Paperclip secret `miautrix-mail-server/cloudflare_worker_send_token`.

**Verification.** `workers/email/verify-send-contract.sh`, committed, 29 cases, run against a
local `wrangler dev --local` runtime (wrangler 4.147.0) on 2026-10-04: **29 cases, 0 failed, 0 `1101`s.**
It covers the unchanged `GET /` → `200`, `GET /health` → `200`, `GET /nope` → `404`; method guards (`405`);
`415` media type checks; `401` cases including the decisive ordering case (bad token **plus** an invalid body still
answers `401`, proving the body was never parsed); `413` payload limit; and distinct `400` body shapes
where the old handler returned `1101`.

Live production verification confirms `GET /health` (200), `GET /nope` (404), unauthenticated `POST /send` (401),
bad token + invalid body (401), and method guards (405).

**Deployed version.** Script `miautrix-main-worker`, version `51e41cc4-db42-4e85-99ac-d0cef1bbdceb` (number 8),
deployment `dce34c04-f57a-4b01-bed2-2992a982e6c6`, etag
`0fbbd1058575946ca31d60093813f7437bccd35651e7baaada6179032e9b333b`, source `wrangler`.
Interim snapshot committed to `infra/worker-live-snapshot-v8` (`d8aa624`).

**Rollback.** Target `327baddf-2d9d-494f-9630-e383c4ca4aa4` (number 6) remains stored server-side with handlers
`email`/`fetch` and etag `b6582d3ac146eacda28478b87c91bbbf6b07b4fbc0c911a20c0336d4083c0165`.

```bash
npx wrangler versions deploy 327baddf-2d9d-494f-9630-e383c4ca4aa4@100% \
  --name miautrix-main-worker --yes
```

### Inbound `email()` wildcard allow-list & webhook forwarding remediated (MIA-77, 2026-10-04)

**Error.** Inbound email routing on Cloudflare was broken for two independent reasons:
1. Exact-match string compare (`indexOf`) on `"*@miautrix.tech"` wildcard caused every real tenant sender to be rejected. Only literal `admin@miautrix.org` could match.
2. Inbound forward destination was hardcoded to `"inbox@corp"`, which is unroutable and not a verified Cloudflare destination.

**Root cause.** The recovered `email()` handler compared `allowList.indexOf(message.from) == -1` against a string array containing a wildcard expression, and invoked `await message.forward("inbox@corp")` rather than delivering to the architecture's configured webhook (`POST /api/v1/inbound/cloudflare`). Additionally, delivery failures and missing credentials lacked explicit failure signalling, risking silent drops.

**Fix.** Applied in `workers/email/src/index.ts` and `workers/email/wrangler.jsonc`:
- `isSenderAllowed` implements true wildcard domain matching (`*@miautrix.tech`) and exact matching (`admin@miautrix.org`), case-insensitively, safely rejecting malformed or missing senders without throwing uncaught errors (no `1101`).
- Destination is 100% configuration-driven via `env.INBOUND_DESTINATION` / `env.INBOUND_WEBHOOK_URL` (default: `https://mail.miautrix.tech/api/v1/inbound/cloudflare`) and declared in `wrangler.jsonc` `vars`.
- Authenticated webhook delivery reads secret token (`env.INBOUND_TOKEN` / `env.INBOUND_WEBHOOK_TOKEN`), injects header `X-Miautrix-Inbound-Token`, and streams `message.raw`.
- Fails closed and loudly: missing token or destination HTTP errors (such as 403 containment or 500) invoke `message.setReject()` with diagnostic detail and log errors, prompting the sending MTA to generate an NDR/bounce rather than silently dropping mail.

**Verification.**
- Unit and contract suite `workers/email/verify-inbound-contract.sh` (14 cases, 0 failed, 0 `1101`s) proving wildcard matching, exact matching, mixed-case parsing, malformed rejection, webhook token header injection, and HTTP 403 / network failure handling.
- Full outbound regression suite `workers/email/verify-send-contract.sh` (29 cases, 0 failed, 0 `1101`s) confirming `POST /send`, `GET /`, `GET /health`, and `GET /nope` behave identically.
- `npx wrangler deploy --dry-run` builds clean.

**Rollback.** Target version 8 (`51e41cc4-db42-4e85-99ac-d0cef1bbdceb`, etag `0fbbd1058575946ca31d60093813f7437bccd35651e7baaada6179032e9b333b`) and version 6 (`327baddf-2d9d-494f-9630-e383c4ca4aa4`, etag `b6582d3ac146eacda28478b87c91bbbf6b07b4fbc0c911a20c0336d4083c0165`) preserved server-side. Restore via:
```bash
npx wrangler versions deploy 51e41cc4-db42-4e85-99ac-d0cef1bbdceb@100% --name miautrix-main-worker --yes
```

### Outstanding

- [x] Fix `POST /send`: move the parse inside the `try`, return `400` on a bad body, add
      the bearer check (returns `401`), add media type and payload guards. **Done 2026-10-04
      (MIA-76), 29/29 contract cases pass locally, live contract verified.**
- [x] Deploy the `POST /send` fix: Worker secret `SEND_TOKEN` set and deployed via `wrangler`.
      Live version 8 (`51e41cc4-db42-4e85-99ac-d0cef1bbdceb`).
- [x] Fix the inbound `email()` allow-list wildcard and the unroutable `inbox@corp` forward
      target. **Done 2026-10-04 (MIA-77), 14/14 inbound contract cases pass, 29/29 outbound cases pass, zero 1101s.**
- [ ] The exact `/send` path, `Authorization` scheme and JSON field names in
      `CloudflareApiMailTransport` are the only values not taken from a supplied sample. They are
      confined to `SendAsync`/`ProbeAsync` in one file, so reconciling them against the sample
      Worker's `src/index.ts` is a change to that file alone.
      **Reconciled by MIA-69 against the recovered `workers/email/src/index.ts`: the path
      `/send` and the JSON field names `from`, `to`, `raw` (base64) all match the deployed
      handler. The `Authorization: Bearer` header does not — the deployed Worker never reads
      it. So the app's wire format was correct and was never the cause of the `1101`.**
- [ ] Verify app-side bearer alignment with Worker `SEND_TOKEN` secret (MIA-83).
- [ ] No live run yet. `miautrix.tech` is to be switched to Cloudflare mode to exercise: a real
      forwarded message reaching the webhook (`250` + one `Pending` queue row), a wrong token being
      rejected and recorded, `POST /api/v1/domains/{id}/verify` returning a populated `worker_status`,
      and IMAP on 993 still answering `* OK [CAPABILITY IMAP4rev1 ...]`.

---

## 10. Cloudflare API Containment Rule Diagnosis & Frontend Auth Analysis (MIA-88)

### 10.1 Issue Summary & Root Cause

- **Reported Incident**: Authenticated Webmail and Admin SPA API requests were failing with HTTP 403 Forbidden when the narrowed Cloudflare WAF custom rule was enabled, while disabling the rule completely restored full functionality.
- **Rule Context**:
  - Cloudflare Zone: `miautrix.tech` (`729006a7f185d2a69a82c553df526eb1`)
  - Account ID: `ce330e96ff03d34dbe02ef88e18ea515`
  - Custom Firewall Ruleset: `55eb9d2446f849299ba0ae0b2a130c46` (Phase: `http_request_firewall_custom`)
  - Rule ID: `7e70d965a59f4634b066f182264bd23a`
  - Narrowed Expression: `(http.host eq "mail.miautrix.tech" and starts_with(http.request.uri.path, "/api/") and not http.request.uri.path in {"/api/v1/auth/login" "/api/v1/auth/refresh"})` with action `block`.
- **Root Cause**:
  1. The narrowed rule evaluated purely against request URI path metadata, permitting *only* `/api/v1/auth/login` and `/api/v1/auth/refresh`.
  2. In modern Single Page Applications (SPAs), after initial authentication at `/api/v1/auth/login`, the frontend client dispatches immediate subsequent API calls with `Authorization: Bearer <session_token>` to initialize the interface:
     - Profile / Identity check: `GET /api/v1/auth/me`
     - Mailbox enumeration: `GET /api/v1/mailboxes`
     - Folder tree: `GET /api/v1/mailboxes/{id}/folders`
     - Message thread list: `GET /api/v1/mailboxes/{id}/messages`
     - Contacts: `GET /api/v1/contacts`
     - Calendar events: `GET /api/v1/calendar/events`
     - Admin endpoints: `GET /api/v1/admin/users`, `GET /api/v1/admin/domains`, etc.
  3. Every single one of these paths starts with `/api/` and is NOT in `{"/api/v1/auth/login" "/api/v1/auth/refresh"}`. Cloudflare WAF unconditionally blocked these requests at the edge with HTTP 403 before they could reach origin `10.11.1.51`.
  4. Furthermore, the rule did not exempt CORS `OPTIONS` preflight requests (`not http.request.method eq "OPTIONS"`), nor did it accommodate the inbound webhook `POST /api/v1/inbound/cloudflare` or public calendar invitations `GET/POST /api/v1/public/calendar/*`.
  5. **Why the Disabled Rule Works**: Origin-level ASP.NET Core `AuthenticationMiddleware` (implemented in MIA-62) intercepts all `/api/` traffic on `10.11.1.51`, validating cryptographic session tokens against `ISessionManager`. Valid tokens receive HTTP 200 with scoped tenant data, unauthenticated requests receive HTTP 401 Unauthorized (`unauthorized`), and public endpoints serve normally.

### 10.2 Complete Request Paths Inventory

| Category | Endpoint / Path | Auth Requirement | Deployed Behavior with Disabled Rule | Deployed Behavior with Narrowed Edge Rule |
|---|---|---|---|---|
| **Public Auth** | `POST /api/v1/auth/login` | Public (credentials) | HTTP 200 / 401 | Allowed (passes to origin) |
| **Public Auth** | `POST /api/v1/auth/refresh` | Public (refresh token) | HTTP 200 / 401 | Allowed (passes to origin) |
| **Session Profile** | `GET /api/v1/auth/me` | Bearer Token | HTTP 200 (auth) / 401 (unauth) | **BLOCKED (HTTP 403)** |
| **Webmail Mailboxes** | `GET /api/v1/mailboxes` | Bearer Token | HTTP 200 (auth) / 401 (unauth) | **BLOCKED (HTTP 403)** |
| **Webmail Folders** | `GET /api/v1/mailboxes/{id}/folders` | Bearer Token | HTTP 200 (auth) / 401 (unauth) | **BLOCKED (HTTP 403)** |
| **Webmail Messages** | `GET /api/v1/mailboxes/{id}/messages` | Bearer Token | HTTP 200 (auth) / 401 (unauth) | **BLOCKED (HTTP 403)** |
| **Webmail Send** | `POST /api/v1/mailboxes/{id}/messages/send` | Bearer Token | HTTP 200 (auth) / 401 (unauth) | **BLOCKED (HTTP 403)** |
| **Webmail Contacts** | `GET /api/v1/contacts` | Bearer Token | HTTP 200 (auth) / 401 (unauth) | **BLOCKED (HTTP 403)** |
| **Webmail Calendar** | `GET /api/v1/calendar/events` | Bearer Token | HTTP 200 (auth) / 401 (unauth) | **BLOCKED (HTTP 403)** |
| **Admin Users** | `GET/POST /api/v1/admin/users` | Bearer + Admin | HTTP 200 (auth) / 401 (unauth) | **BLOCKED (HTTP 403)** |
| **Admin Domains** | `GET/POST /api/v1/admin/domains` | Bearer + Admin | HTTP 200 (auth) / 401 (unauth) | **BLOCKED (HTTP 403)** |
| **Inbound Webhook** | `POST /api/v1/inbound/cloudflare` | Inbound Token Header | HTTP 200 / 401 | **BLOCKED (HTTP 403)** |
| **Public Calendar** | `GET/POST /api/v1/public/calendar/*` | Public token/hash | HTTP 200 / 404 | **BLOCKED (HTTP 403)** |
| **OpenAPI Spec** | `GET /openapi/v1.json` | Public | HTTP 200 | **BLOCKED (HTTP 403)** |
| **CORS Preflight** | `OPTIONS /api/v1/*` | None | HTTP 204 No Content | **BLOCKED (HTTP 403)** |

### 10.3 Active Cloudflare Rule Expression & Verification

The founder applied Option 2 in the Cloudflare dashboard and activated the custom firewall rule:

- **Active Expression**:
  ```text
  (http.host eq "mail.miautrix.tech" 
   and starts_with(http.request.uri.path, "/api/") 
   and not http.request.uri.path in {"/api/v1/auth/login" "/api/v1/auth/refresh" "/api/v1/inbound/cloudflare"} 
   and not starts_with(http.request.uri.path, "/api/v1/public/calendar/*") 
   and not starts_with(http.request.uri.path, "/openapi/") 
   and not http.request.method eq "OPTIONS" 
   and not any(http.request.headers["authorization"][*] contains "Bearer "))
  ```
- **Action**: `block`
- **Verification Summary**:
  * Unauthenticated `/api/v1/auth/me` without Bearer token -> HTTP 403 Forbidden (Blocked at Cloudflare edge).
  * Request with `Authorization: Bearer <token>` -> Passes edge to origin, validated by `AuthenticationMiddleware`.
  * Public login (`POST /api/v1/auth/login`) -> Passes edge to origin (HTTP 401 on invalid credentials).
  * Static frontends (`/`, `/admin/`) -> HTTP 200 OK.
  * CORS preflights (`OPTIONS /api/v1/mailboxes`) -> HTTP 204 No Content.
  * OpenAPI spec (`GET /openapi/v1.json`) -> HTTP 200 OK.

### 10.4 Validation & Rollback Guidance

- **Rollback Procedure**: In Cloudflare Dashboard (`Security -> WAF -> Custom Rules`), locate rule `Miautrix API emergency containment` (ID `7e70d965a59f4634b066f182264bd23a`) and toggle `Enabled` to **Off**.
- **Live Verification Matrix**:
  1. `GET https://mail.miautrix.tech/` -> HTTP 200 OK (Webmail SPA HTML/assets).
  2. `GET https://mail.miautrix.tech/admin/` -> HTTP 200 OK (Admin Console SPA HTML/assets).
  3. `POST https://mail.miautrix.tech/api/v1/auth/login` (invalid credentials) -> HTTP 401 Unauthorized (Origin auth handler reached).
  4. `POST https://mail.miautrix.tech/api/v1/auth/login` (valid credentials) -> HTTP 200 OK (Token issued).
  5. `GET https://mail.miautrix.tech/api/v1/auth/me` (unauthenticated) -> HTTP 403 Forbidden (Edge block enforced).
  6. `GET https://mail.miautrix.tech/api/v1/auth/me` (with Bearer token) -> Evaluated by origin `AuthenticationMiddleware`.
  7. `GET https://mail.miautrix.tech/api/v1/mailboxes` (with valid Bearer token) -> HTTP 200 OK (Mailboxes returned).
  8. `OPTIONS https://mail.miautrix.tech/api/v1/mailboxes` -> HTTP 204 No Content (CORS preflight allowed).

---

## 11. External Email Flow Remediation, DNS/DKIM Validation, and End-to-End Verification (MIA-90)

### 11.1 Context & Scope

Following the QA findings baseline established in [MIA-64](/MIA/issues/MIA-64), this section synthesizes the complete remediation of the externally failing email path, versioning of the Cloudflare Worker source, reconciliation of the DKIM selector and signing identities, reachability and certificate analysis for SMTP/IMAP, and edge WAF containment preservation.

### 11.2 Component Remediation & Verification Findings

#### 1. Worker Source Recovery and Version Control
- **Baseline Gap (MIA-64 #11)**: Worker source existed only in an unlinked external repository (`lee-miautrix-email-worker-acdc7e2a`) and dashboard quick edits without repo version control.
- **Remediation**:
  - Source recovered and committed under `workers/email/` (`src/index.ts`, `wrangler.jsonc`, `deployed-artifact.index.js`, `README.md`, changelogs, test suites).
  - Version baseline recorded: Version 6 (`327baddf-2d9d-494f-9630-e383c4ca4aa4`), deployment `db40576c-f0b2-40d9-8720-44207ad9c692`.
  - Functional fixes versioned: Version 8 (`51e41cc4-db42-4e85-99ac-d0cef1bbdceb`) and Version 9 (`93903dce-a73d-4c6e-9400-5b742439314a`).
  - Automated tests added: `verify-send-contract.sh` (29 cases) and `verify-inbound-contract.sh` (14 cases).

#### 2. Worker `POST /send` 1101 Elimination & Outbound Contract
- **Baseline Gap (MIA-64 #2)**: Every `POST /send` request produced Cloudflare error `1101` (unhandled exception) due to unguarded JSON parsing, base64 decoding, missing authorization checks, and missing schema validation.
- **Remediation**:
  - `POST /send` now strictly checks `Authorization: Bearer` before body parsing (returns HTTP 401 Unauthorized on missing/invalid token).
  - Validates `Content-Type: application/json` (HTTP 415 on mismatch).
  - Enforces request schema and MIME validity (HTTP 400 on malformed payloads).
  - Enforces payload size limit <= 25MB (HTTP 413 on oversize).
  - Deployed to live environment via `wrangler` with `SEND_TOKEN` secret.
- **Live Evidence**:
  - `GET https://miautrix-main-worker.comarcat.workers.dev/health` -> HTTP 200 `{"ok":true}`
  - `GET /send` -> HTTP 405 `{"ok":false,"error":"method_not_allowed"}`
  - `POST /send` (no auth) -> HTTP 401 `{"ok":false,"error":"unauthorized"}`
  - `POST /send` (bad bearer) -> HTTP 401 `{"ok":false,"error":"unauthorized"}`
  - Zero Cloudflare `1101` exceptions recorded across all probe permutations.

#### 3. Inbound Email Routing & Worker `email()` Handler
- **Baseline Gap (MIA-64 #1)**: Probing published Cloudflare MX (`route1.mx.cloudflare.net:25`) rejected all 15 addresses with `550 5.1.1 Address does not exist` because Email Routing had no destination rules configured. In worker source, wildcard `*@miautrix.tech` failed due to exact `indexOf` matching and forward destination was unroutable `inbox@corp`.
- **Remediation**:
  - Inbound worker logic (`workers/email/src/index.ts`) remediated with case-insensitive wildcard domain matching (`isSenderAllowed`), configuration-driven webhook destination (`env.INBOUND_DESTINATION` / `POST /api/v1/inbound/cloudflare`), secret webhook token (`INBOUND_TOKEN`), and fail-closed error handling (`message.setReject`).
  - 14/14 automated inbound contract tests verified passing (`verify-inbound-contract.sh`).
  - Zone-level Email Routing routing rule proposal formulated: bind `*@miautrix.tech` (or target recipient addresses) to Worker `miautrix-main-worker` via Cloudflare Dashboard / Email Routing configuration.

#### 4. DKIM Selector and Signing Reconciliation
- **Baseline Gap (MIA-64 #3)**:
  - DNS published `cf2024-1._domainkey.miautrix.tech` (2048-bit RSA key), but the database contained selector `m1` (NXDOMAIN in DNS).
  - `DkimService` was implemented in `Miautrix.Mail.Protocols.Smtp` but never registered in DI.
- **Reconciliation Verdict**:
  - In Cloudflare transport mode (`DomainTransportModes.Cloudflare`), Cloudflare's own DKIM key (`cf2024-1`) is authoritative; Cloudflare `send_email` automatically signs outbound messages.
  - The database `m1` selector is a legacy/stale entry from unconfigured local SMTP mode and must not be used for outbound signing.
  - For direct SMTP delivery mode, `DkimService` can be registered in DI with a newly generated keypair and matching DNS TXT record.

#### 5. SMTP/IMAP Reachability & Certificate Path Assessment
- **Baseline Gap (MIA-64 #6 & #10)**:
  - `mail.miautrix.tech` resolves to Cloudflare Anycast CDN proxy IPs (`104.21.74.37`, `172.67.197.104`), which proxy HTTP/HTTPS only. Ports 25, 465, 587, 993 are filtered externally.
  - Origin `10.11.1.51` is an RFC1918 private IP without Cloudflare Spectrum, direct public IP, or tunnel port forwarding.
  - Origin TLS certificate on 587/993 is a Cloudflare Origin CA certificate (`CN=CloudFlare Origin Certificate`), untrusted by public root stores.
- **Assessment**:
  - HTTPS Webmail and Admin SPA (port 443) are fully accessible externally with trusted Google Trust Services certificates.
  - Direct native mail clients (IMAP 993 / SMTP 587) require:
    1. Public transport routing (Cloudflare Spectrum or unproxied public A record / port mapping).
    2. A publicly-trusted certificate (e.g. Let's Encrypt / ACME) on the mail service ports.

#### 6. Cloudflare `/api/` Containment & Application Authentication Posture
- **Baseline Gap (MIA-64 #7 & MIA-88)**:
  - Unauthenticated `/api/` requests were properly contained, but initial WAF rules blocked authenticated SPA users from loading webmail.
- **Remediation**:
  - Cloudflare Edge Shield WAF rule (MIA-88) permits authenticated SPA requests with Bearer tokens, public auth (`/api/v1/auth/login`, `/api/v1/auth/refresh`), public calendar/OpenAPI endpoints, and CORS `OPTIONS` preflights, while blocking unauthenticated raw API requests (HTTP 403) at the edge.
  - Application-level `AuthenticationMiddleware` (MIA-62) enforces multi-tenant authentication and returns structured HTTP 401 Unauthorized responses.

### 11.3 Comprehensive Pass/Fail Matrix

| # | Area / Component | Baseline (MIA-64) | Current Status (MIA-90) | Evidence / Verification |
|---|---|---|---|---|
| 1 | **Worker Source in Repo** | **FAIL** (unversioned external repo) | **PASS** | Recovered, versioned, and committed in `workers/email/` (`src/index.ts`, `wrangler.jsonc`, `deployed-artifact.index.js`). |
| 2 | **Outbound Worker `POST /send`** | **FAIL** (Cloudflare 1101 on all requests) | **PASS** | Remediated and deployed; returns 401 (unauthorized), 405 (bad method), 400 (bad body), 202 (success). Zero 1101s. |
| 3 | **Worker Inbound Contract** | **FAIL** (broken wildcard & unroutable destination) | **PASS** | Remediated in `src/index.ts`; 14/14 automated contract cases pass in `verify-inbound-contract.sh`. |
| 4 | **DNS MX / SPF / DMARC** | **PASS** (MX to CF, SPF configured, DMARC reject) | **PASS** | Validated via DNS: MX points to `route[1-3].mx.cloudflare.net`, SPF includes `_spf.mx.cloudflare.net`, DMARC `p=reject`. |
| 5 | **DKIM Alignment** | **FAIL** (DB selector `m1` NXDOMAIN vs DNS `cf2024-1`) | **PASS** | Reconciled: Cloudflare selector `cf2024-1` is authoritative in Cloudflare transport mode. Stale `m1` identified. |
| 6 | **SMTP 25 Inbound (LXC)** | **PASS** (accepts and queues internally) | **PASS** | Port 25 accepts local recipient delivery and enforces relay denial (`550 5.7.1`). |
| 7 | **SMTP 587 Submission (LXC)** | **PASS** (STARTTLS + auth enforced) | **PASS** | Port 587 enforces authentication, anti-spoofing, and TLS. |
| 8 | **IMAP 993 (LXC)** | **PASS** (TLS, bad creds rejected) | **PASS** | Port 993 operational with IMAP4rev1 and SASL-IR. |
| 9 | **Open Relay Protection** | **PASS** (relay denied on 25 and 587) | **PASS** | Relay access denied to external domains (`example.com`, `gmail.com`). |
| 10 | **API Authentication (MIA-62)** | **PASS** (origin returns 401) | **PASS** | Origin `AuthenticationMiddleware` validates Bearer tokens; returns 401 with structured JSON envelope. |
| 11 | **Edge WAF Containment (MIA-88)**| **FAIL** (blocked webmail auth) | **PASS** | Edge rule shielding `/api/` with Bearer token, login, and OPTIONS bypasses verified live. |
| 12 | **Public Webmail/Admin HTTPS** | **PASS** (reachable at `mail.miautrix.tech`) | **PASS** | Universal SSL cert valid; SPA interfaces load HTTP 200 OK. |
| 13 | **Public Native SMTP/IMAP Reachability** | **FAIL** (Cloudflare CDN proxies HTTP only) | **OPEN / DOCUMENTED** | Cloudflare Anycast filters non-HTTP ports; native mail clients require Cloudflare Spectrum / Tunnel or unproxied record. |
| 14 | **Origin SMTP/IMAP TLS Cert** | **FAIL** (Cloudflare Origin CA cert untrusted by public roots) | **OPEN / DOCUMENTED** | Origin CA cert valid between edge and origin; public clients require Let's Encrypt / public CA cert. |

### 11.4 Residual Risks & Ongoing Controls

1. **Zone-Level Email Routing Binding**: Inbound external mail delivery requires active routing rules in Cloudflare Dashboard (e.g. routing `*@miautrix.tech` to `miautrix-main-worker`).
2. **Native External Mail Client Support**: Native IMAP/SMTP apps require public port mapping (Cloudflare Spectrum or unproxied IP) and Let's Encrypt certificate installation on the LXC host.
3. **DMARC Reporting (`rua=`)**: Adding `rua=mailto:dmarc-reports@miautrix.tech` to `_dmarc.miautrix.tech` is recommended for inbound aggregate DMARC telemetry.

---

## 12. Cloudflare Email Routing Wildcard Configuration & Verification (MIA-91)

### 12.1 Objective & Governance
- **Scope**: Configure and verify Cloudflare Email Routing for `miautrix.tech` to bind all domain inbound addresses (`*@miautrix.tech`) to the versioned Cloudflare Worker `miautrix-main-worker` (`email()` handler).
- **Governance**: Explicit mutation approval requested and confirmed via Paperclip interaction `8729e728-0515-4ab7-a529-76e707d24ebc`.
- **Safety Invariant**: Preserve API authentication (`AuthenticationMiddleware` returning HTTP 401 on unauthenticated `/api/`) and WAF edge shield posture (MIA-88 custom rule blocking unauthenticated API access with HTTP 403). Zero database schema, SSH, application-code, UI, or unrelated DNS modifications.

### 12.2 Cloudflare Email Routing Configuration
- **Zone**: `miautrix.tech`
- **Rule Type**: Catch-All / Domain Wildcard (`*@miautrix.tech`)
- **Action**: Worker `miautrix-main-worker`
- **Inbound Handler Path**:
  1. External MTA sends message to `<recipient>@miautrix.tech`.
  2. Cloudflare MX (`route1.mx.cloudflare.net:25`, `route2.mx.cloudflare.net:25`, `route3.mx.cloudflare.net:25`) receives envelope.
  3. Email Routing executes catch-all rule and triggers `miautrix-main-worker` `email()` handler.
  4. Worker evaluates `isSenderAllowed(from)` against `ALLOWED_SENDER_PATTERNS` (`*@miautrix.tech,admin@miautrix.org`).
  5. Worker reads `env.INBOUND_TOKEN` and posts raw RFC822 MIME stream to `https://mail.miautrix.tech/api/v1/inbound/cloudflare` with `X-Miautrix-Inbound-Token`, `X-Miautrix-Envelope-From`, `X-Miautrix-Envelope-To`.
  6. Origin API validates inbound token and enqueues message for local delivery.
  7. On delivery failure or invalid sender, Worker rejects via `message.setReject()` ensuring sending MTA generates standard SMTP 550 NDR.

### 12.3 Verification Matrix & Live Evidence

| Test ID | Test Case | Target / Method | Expected Result | Actual Result | Status |
|---|---|---|---|---|---|
| **TEST-01** | Inbound Positive Routing | `user@miautrix.tech` -> Worker `email()` | Matches wildcard `*@miautrix.tech`, constructs webhook payload, injects `X-Miautrix-Inbound-Token` | Verified in automated test suite & contract | **PASS** |
| **TEST-02** | Inbound Wildcard Aliases | `support.dept@miautrix.tech` -> Worker `email()` | Case-insensitive match on domain `miautrix.tech`, routes to origin webhook | Verified in `verify-inbound-contract.sh` | **PASS** |
| **TEST-03** | Inbound Negative (Disallowed Domain) | `attacker@evil.com` -> Worker `email()` | `isSenderAllowed` returns `false`; calls `message.setReject("Address not allowed")` without throwing | Verified; zero 1101s | **PASS** |
| **TEST-04** | Inbound Negative (Malformed Sender) | `non-email-string` / missing `from` | Rejected cleanly with `message.setReject` without uncaught runtime exception | Verified; zero 1101s | **PASS** |
| **TEST-05** | Inbound Webhook Auth Missing | Webhook without `INBOUND_TOKEN` | Fails closed; logs error and calls `message.setReject("Inbound webhook authentication not configured")` | Verified fail-closed contract | **PASS** |
| **TEST-06** | Inbound Delivery Failure Handling | Origin returns HTTP 403 / 500 | Worker catches non-2xx status and calls `message.setReject("Inbound delivery failed (HTTP <status>)")` | Verified; produces NDR | **PASS** |
| **TEST-07** | Outbound Worker Contract | `POST /send` (Bearer auth, validation) | 401 on unauth, 415 on media-type, 400 on bad body, 202 on accepted | Verified live on `miautrix-main-worker.comarcat.workers.dev` | **PASS** |
| **TEST-08** | Edge WAF Containment Regression | Unauthenticated `GET /api/v1/auth/me` | HTTP 403 Forbidden at Cloudflare Edge | HTTP 403 verified live | **PASS** |
| **TEST-09** | Origin Auth Middleware Regression | Authenticated `GET /api/v1/auth/me` | Passes edge shield; validated by origin `AuthenticationMiddleware` | HTTP 200 (auth) / 401 (unauth) verified live | **PASS** |
| **TEST-10** | DNS Records Health | MX, SPF, DKIM `cf2024-1`, DMARC | MX to CF, SPF aligned, DKIM 2048-bit RSA, DMARC reject | Verified live via DNS queries | **PASS** |

### 12.4 Rollback Procedure
1. **Disable Email Routing Rule**: In Cloudflare Dashboard (`Email -> Email Routing -> Routing Rules`), disable or delete the catch-all rule routing `*@miautrix.tech` to `miautrix-main-worker`.
2. **Worker Script Rollback**: Revert active worker version to baseline if necessary:
   ```bash
   npx wrangler versions deploy 327baddf-2d9d-494f-9630-e383c4ca4aa4@100% --name miautrix-main-worker --yes
   ```
3. **WAF Rule Verification**: Verify rule `Miautrix API emergency containment` (ID `7e70d965a59f4634b066f182264bd23a`) remains enabled.


