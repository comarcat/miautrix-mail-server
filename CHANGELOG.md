# Changelog

All notable changes to this project are documented here.

## [2.1.0] - Unreleased (branch `release/v2.1`)

**Deploy:** no new migrations, so deploy without `-RunMigrations`. No `.env` changes are required. After the deploy, run `messages reparse --tenant all` (dry run first) for drafts that IMAP clients saved before v2.1. Handoff: `docs/HANDOFF-v2.1.md`.

### Added
- **IMAP:** NAMESPACE, MOVE / UID MOVE, IDLE; an IMAP4rev2 session mode after `ENABLE IMAP4rev2` (ESEARCH, SEARCHRES, LIST-EXTENDED, LIST-STATUS, STATUS=SIZE, LITERAL-, BINARY, no `\Recent`). `IMAP4rev2` is advertised only once every RFC 9051 MUST passes (`docs/IMAP-Phase2-Report.md`).
- **Outbound:** a per-domain `direct` transport mode (MX lookup with A/AAAA fallback, null MX, opportunistic STARTTLS with TLS 1.2+, MTA-STS); DKIM signing for `direct` domains (Cloudflare domains stay unsigned by us, as Cloudflare signs); exponential retry for up to 5 days (`OUTBOUND_RETRY_LIFETIME_DAYS`); RFC 3464 bounces to local senders (`docs/Outbound-v2.1-Report.md`).
- **SMTP listener:**
  - STARTTLS on port 25 and TLS 1.2+ on all SMTP ports;
  - a strict RFC 5321 state machine;
  - VRFY/EXPN `252`, HELP `214`, enhanced status codes and exact EHLO lists;
  - strict SASL PLAIN/LOGIN, and `Date`/`Message-ID` added on submission;
  - optional limits `MIAUTRIX_SMTP_MAX_RECIPIENTS`, `MIAUTRIX_SMTP_MAX_MESSAGE_SIZE`, `MIAUTRIX_SMTP_IDLE_TIMEOUT_SECONDS` and `MIAUTRIX_SMTP_DATA_TIMEOUT_SECONDS`.

  See `docs/SMTP-v2.1-Report.md`.
- **Audit:** `docs/v2.1-Protocol-Compliance-Audit.md` covers every non-IMAP protocol.

### Fixed
- **SMTP: only the last recipient received a message** with several recipients. This affected Thunderbird sends and inbound mail to several local users. Each recipient now gets its own queue item, routed to its own tenant.
- **SMTP: unbounded command and DATA input** let any client exhaust Worker memory. Line, message-size, recipient and time limits are now enforced, and a connection dropped mid-DATA queues nothing.
- **WEBMAIL-001: drafts saved by IMAP clients opened empty in webmail.** APPEND now parses through the shared MimeKit parser. Webmail saves write full RFC 5322 messages, and an unchanged autosave does nothing.
- **Deploy safety:** the new `OUTBOUND_EHLO_NAME` falls back to `MIAUTRIX_HOSTNAME`, so the existing production `.env` still boots.


**Released:** deployed to production on 2026-11-24 from commit `76c8e7c` and validated by the owner (imaplib, Himalaya, Thunderbird and SMTP Send As). Handoff: `docs/HANDOFF-v2.0.0-release.md`.

### Added
- **IMAP shared mailboxes (IMAP-009):** delegated mailboxes appear under `Shared/<address>/<folder>`; authorization re-checked on every command; revocation applies to open sessions. See `docs/IMAP.md`.
- **Full RFC 3501 command set:** every IMAP4rev1 MUST row is implemented and tested (`docs/IMAP-Phase1-Report.md`). Persistent subscriptions (default subscribed, UNSUBSCRIBE opts out), `\Recent` tracking, a UIDVALIDITY sequence, APPENDUID/COPYUID, TRYCREATE, and `UTF8=ACCEPT` after ENABLE.
- **Send As / Send on Behalf:** new delegate permissions (`can_send_as`, `can_send_on_behalf`) in the admin API and UI. One sender policy for SMTP submission and webmail. See `docs/SMTP-SendAs-Report.md`.
- Shared MimeKit parser contract and Infrastructure implementation; message reparse service and `messages reparse` CLI command.
- Sandboxed iframe rendering for webmail message bodies and an environment-configured IMAP external validation script.
- Buffered IMAP byte reader, token collector/tokenizer, segmented response writer, hierarchy-aware LIST/LSUB, AUTHENTICATE PLAIN, and folder rename through the Application service.
- UID/range FETCH, SEARCH parsing/evaluation, MIME envelope/body structure formatting, STORE, COPY, EXPUNGE, STATUS, and folder lifecycle command paths.
- .NET 10 SDK/framework guard and framework regression unit test.
- Versioned release tooling with verified database/config backups, rollback, cleanup retention, and deployment documentation.
- Troubleshooting manual (`docs/TROUBLESHOOTING.md`) with generated script appendix and zip; read-only `scripts/db-inspect.sh`; test-tenant cleanup script.
- Windows server package (Kestrel static hosting, Windows services, installer) for external testing.
- UID tests can use an external throwaway PostgreSQL (`MIAUTRIX_TEST_SERVER_CONNECTION`) instead of Docker.

### Changed
- Retargeted projects to .NET 10 / C# 14 with Microsoft 10.0.x and Npgsql EF Core 10.0.x packages (EF Core 10.0.15; `dotnet-ef` tool 10.0.15; production PostgreSQL 18.6).
- Kept Web and Worker self-contained `linux-x64` publishing.

### Fixed

- **Security: SMTP submission and webmail accepted any From address.** An authenticated user could send as another user, a shared mailbox or another tenant. Now only the user's own addresses, aliases and granted Send As / Send on Behalf mailboxes are allowed; everything else gets `550 5.7.1` and is audited.
- IMAP EXPUNGE deletes permanently (RFC 3501) instead of moving to Trash.
- Allocated stable non-zero IMAP UIDs atomically per folder and added the UID backfill and uniqueness migration.
- `AddQueueDirection` migration is idempotent so it applies over databases that ran the superseded `AddSmtpQueueDirection` migration (production drift that aborted the first `-RunMigrations` deploy).
- EF bundle build no longer needs a real database connection; the seeder no longer creates an admin with a default password (`users bootstrap-admin` instead).
- Deploy verification no longer fails on the authenticated health endpoint (any local Kestrel status below 500 counts as alive) and prints per-check diagnostics on failure.
- Corrected single-part MIME transfer decoding, charset handling and encoded subject decoding through MimeKit.
- Corrected FETCH literal marker placement and byte framing, including the post-literal separator and partial-section source matching; rejected invalid sectionless BODY.PEEK requests. Expanded LIST, SELECT/EXAMINE, SEARCH and FETCH behavior, added mailbox-level tenant authorization checks, and map authorization denials to NO. Oversized synchronising literals receive tagged TOOBIG without continuation; oversized LITERAL+ closes with BYE. FETCH now loads raw content lazily and sequence operations project only required columns.
- Removed raw IMAP command logging and aligned greeting/CAPABILITY tokens.

### Security
- Isolated email HTML in an iframe without scripts, applied a restrictive CSP, and removed remote CSS fetches while images are blocked.
- Reparse audit records contain counts and actor mode only; message bodies are not logged or audited.
- Removed the tracked production DB password and replaced runtime storage fallbacks with fail-closed configuration outside Development; the owner confirmed production credentials were changed long ago and differ from repository history (RSK-11 mitigated).
- Corrected versioned deployment rollback TFM detection, staged-release failure handling, active-state recording, legacy-file/path preflight guards, and PostgreSQL client/server major-version guidance.

> Release state (2026-11-24): v2.0 is live on production as `v2.0.0-0394daf-202611241633` (backfill applied). IMAP-006 is not yet deployed: the first `-RunMigrations` attempt aborted safely on migration drift (ERRORS §13.10, now fixed). Outstanding: DB-backed tests on a throwaway PostgreSQL, the IMAP-006 deploy, external IMAP validation, some RFC 3501 edge semantics.

## [1.0.0] - 2026-10-03

### Added
- Initial modular .NET mail platform with tenant authorization, SMTP, basic IMAP, Sieve, search, mail-flow rules, web admin, webmail, desktop shell, CLI, backups, licensing and operations support.
- Calendar invitation, RSVP, rescheduling, and mail composition workflows.

### Changed
- Stabilized the Version 1.0 release with production deployment and operational documentation.

### Fixed
- Calendar RSVP and reschedule links, explicit UTC invitation wording, and webmail empty-inbox bulk-delete rendering.
