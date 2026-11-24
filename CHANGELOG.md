# Changelog

All notable changes to this project are documented here.

## [2.0.0] - 2026-11-23

### Added
- Shared MimeKit parser contract and Infrastructure implementation; message reparse service and `messages reparse` CLI command.
- Sandboxed iframe rendering for webmail message bodies and an environment-configured IMAP external validation script.
- Buffered IMAP byte reader, token collector/tokenizer, segmented response writer, hierarchy-aware LIST/LSUB, AUTHENTICATE PLAIN, and folder rename through the Application service.
- UID/range FETCH, SEARCH parsing/evaluation, MIME envelope/body structure formatting, STORE, COPY, EXPUNGE, STATUS, and folder lifecycle command paths.
- .NET 10 SDK/framework guard and framework regression unit test.
- Versioned release tooling with verified database/config backups, rollback, cleanup retention, and deployment documentation.

### Changed
- Retargeted projects to .NET 10 / C# 14 with Microsoft 10.0.x and Npgsql EF Core 10.0.x packages.
- Kept Web and Worker self-contained `linux-x64` publishing.

### Fixed

- Allocated stable non-zero IMAP UIDs atomically per folder and added the UID backfill and uniqueness migration.
- Deploy verification no longer fails on the authenticated health endpoint (any local Kestrel status below 500 counts as alive) and prints per-check diagnostics on failure.
- Corrected single-part MIME transfer decoding, charset handling and encoded subject decoding through MimeKit.
- Corrected FETCH literal marker placement and byte framing, including the post-literal separator and partial-section source matching; rejected invalid sectionless BODY.PEEK requests. Expanded LIST, SELECT/EXAMINE, SEARCH and FETCH behavior, added mailbox-level tenant authorization checks, and map authorization denials to NO. Oversized synchronising literals receive tagged TOOBIG without continuation; oversized LITERAL+ closes with BYE. FETCH now loads raw content lazily and sequence operations project only required columns.
- Removed raw IMAP command logging and aligned greeting/CAPABILITY tokens.

### Security
- Isolated email HTML in an iframe without scripts, applied a restrictive CSP, and removed remote CSS fetches while images are blocked.
- Reparse audit records contain counts and actor mode only; message bodies are not logged or audited.
- Removed the tracked production DB password and replaced runtime storage fallbacks with fail-closed configuration outside Development; the owner must rotate the credential because it remains in repository history.
- Corrected versioned deployment rollback TFM detection, staged-release failure handling, active-state recording, legacy-file/path preflight guards, and PostgreSQL client/server major-version guidance.

> Release readiness remains incomplete: some RFC 3501 edge semantics, database-backed protocol tests (written, not executed because no Docker/test DB is available), deploy and external client validation are outstanding.

## [1.0.0] - 2026-10-03

### Added
- Initial modular .NET mail platform with tenant authorization, SMTP, basic IMAP, Sieve, search, mail-flow rules, web admin, webmail, desktop shell, CLI, backups, licensing and operations support.
- Calendar invitation, RSVP, rescheduling, and mail composition workflows.

### Changed
- Stabilized the Version 1.0 release with production deployment and operational documentation.

### Fixed
- Calendar RSVP and reschedule links, explicit UTC invitation wording, and webmail empty-inbox bulk-delete rendering.
