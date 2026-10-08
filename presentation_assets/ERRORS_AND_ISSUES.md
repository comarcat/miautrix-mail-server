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
| **DB-06** | Outbound queue misrouted as inbound | Webmail-sent mail to external domains (e.g. Gmail) sat stuck / dead-lettered with `Recipient mailbox not found`,  | `SmtpQueueItem.Direction` defaults to `Inbound` (`Entities.cs`), and the enqueue paths in `MessageService.SendMessageInternalAsync` and `SmtpQueueService.EnqueueMessageAsync` created the row without setting `Direction`. The outbound dispatcher requires `Direction == "Outbound"`, so the row fell to the inbound dispatcher, which dead-lettered external recipients with no local mailbox. | **Fixed 2026-10-05**: both enqueue sites now set `Direction = "Outbound"`. Build clean (`-warnaserror`). Existing already-queued external rows must be flipped with `UPDATE smtp_queue SET direction='Outbound', status='Pending', attempts=0, next_attempt_at=now() WHERE recipient LIKE '%@gmail.com' OR recipient LIKE '%@googlemail.com';` before redeploy. |

---

## 5. Items to Review & Verify Later

- [x] **Queue Ownership Migration:** `20261005173819_AddSmtpQueueDirection` applied; migration classifies inbound/outbound per-recipient, resets stuck inbound rows, and adds required coverage index for dispatcher filtering.
- [x] **Inbound Dispatcher Filtering:** `InboundQueueDispatcher` updated to filter only `Inbound` rows.
- [x] **Outbound Dispatcher Filtering:** `OutboundQueueDispatcher` updated to filter only `Outbound` rows.
- [x] **Ownership Regression Tests:** `QueueOwnershipTests` confirm inbound rows ignore outbound-dispatch pass, and outbound rows ignore inbound-mailbox lookup.
- [x] **Release Evidence (E5-T2):** Chosen option was explicit queue ownership via required `smtp_queue.direction`, not MIME rewriting. Migration `20261005173819_AddSmtpQueueDirection` adds/backfills `direction` and index `idx_smtp_queue_tenant_direction_status_next_attempt`; production repair reset affected external-recipient rows from inbound/dead-letter state to `Outbound`/`Pending` (exact manual backfill count not captured in repo). Live validation on 2026-10-05 confirmed outbound Cloudflare routing after direction repair and detailed Worker rejection logging; local build passed with `dotnet build Miautrix.Mail.sln -warnaserror` (0 warnings, 0 errors). Targeted `OutboundQueueDispatcherTests` were attempted but blocked by PostgreSQL auth failure for `mmdb-user`, not code compilation.


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

### Outstanding

- [ ] The exact `/send` path, `Authorization` scheme and JSON field names in
      `CloudflareApiMailTransport` are the only values not taken from a supplied sample. They are
      confined to `SendAsync`/`ProbeAsync` in one file, so reconciling them against the sample
      Worker's `src/index.ts` is a change to that file alone.
- [ ] No live run yet. `miautrix.tech` is to be switched to Cloudflare mode to exercise: a real
      forwarded message reaching the webhook (`250` + one `Pending` queue row), a wrong token being
      rejected and recorded, `POST /api/v1/domains/{id}/verify` returning a populated `worker_status`,
      and IMAP on 993 still answering `* OK [CAPABILITY IMAP4rev1 ...]`.
