# Handoff: Version 2.0.0 released (2026-11-24)

**Start here to catch up on v2.0.** This page is the short version. History and the step-by-step deploy log are in [`HANDOFF-v2.0.md`](HANDOFF-v2.0.md).

## 1. Where things stand

| Item | Value |
|---|---|
| Release | **v2.0.0, deployed and validated by the owner on 2026-11-24** |
| Code | `main`, which includes `release/v2.0-imap-mime`, release commit `76c8e7c` |
| Production app | `v2.0.0-76c8e7c-*` on LXC `10.11.1.51`. Get the exact ID with `scripts/deploy-release.ps1 -Status` |
| Rollback release | `v2.0.0-fb534f1-202611241923`. **Restore the pre-migration dump first** (see §5) |
| Production DB | PostgreSQL 18.6, `10.11.1.52/miautrix-mail-pro`. All migrations applied, the latest is `20261124224654_ImapFolderUidValiditySequence` |
| Test DB | `10.11.1.52/miautrix-mail-dev` on the same server. Throwaway `uidtest_*` / `schemacheck_*` databases are created and dropped by tests |
| Runtime | .NET 10 (SDK 10.0.403), EF Core 10.0.15, `dotnet-ef` 10.0.15. Self-contained `linux-x64` publish |
| Milestone | PMI **M5 closed**. Next: IMAP Phase 2 (IMAP4rev2) |

## 2. What v2.0 delivered

- **Platform:** .NET 10, a shared MimeKit parser with message backfill, sandboxed webmail rendering, and versioned deploys with backup, verification and rollback.
- **IMAP (RFC 3501, IMAP4rev1), every MUST command implemented and tested.** CAPABILITY is
  `IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE`.
  - **Shared mailboxes:** `Shared/<address>/<folder>`, for example `Shared/sales@miautrix.tech/INBOX`. `INBOX` is always the logged-in user's own inbox.
  - **Authorization:** checked on every command through `TenantAuthorizationHelper`, so revoked access applies to open sessions. A mailbox the user isn't allowed to see looks nonexistent.
  - **UIDs:** stable, non-zero and unique per folder. UIDVALIDITY comes from a sequence.
  - **Subscriptions:** every accessible folder is subscribed by default, and UNSUBSCRIBE stores an opt-out.
  - **`\Recent`:** tracked for mail delivered after the upgrade. Existing messages start not recent.
  - **EXPUNGE deletes permanently** (RFC 3501). Clients move messages to Trash themselves.
  - Details: [`IMAP.md`](IMAP.md), [`IMAP-Phase1-Report.md`](IMAP-Phase1-Report.md).
- **Sending, after a spoofing fix:** `ISenderAuthorizationService` is the single policy for SMTP submission and webmail.
  - A user can send from their own address and aliases.
  - **Send As** (`can_send_as`) allows `From:` set to the shared mailbox.
  - **Send on Behalf** (`can_send_on_behalf`) also allows it, and the server sets `Sender:` to the user.
  - Everything else gets `550 5.7.1` and an audit entry (`sender.authorization.denied`).
  - Send permissions never grant mailbox access. Mailbox access requires `access_level` `read` or `write`.
  - Details: [`SMTP-SendAs-Report.md`](SMTP-SendAs-Report.md).

## 3. Verification at release

- **Build:** `dotnet build Miautrix.Mail.sln -warnaserror` gives 0 warnings and 0 errors.
- **Tests:**

  | Suite | Passed |
  |---|---|
  | Unit | 101 |
  | Security | 7 |
  | Protocol (real TLS listener, MailKit and raw socket, dev DB) | 27 |
  | Integration (sender policy, UID allocator, migrations) | 10 |
  | Admin vitest | 18 |
  | Webmail vitest | 15 |

- **Owner validation on production:** imaplib script, Himalaya, Thunderbird (personal and `Shared/sales`), and SMTP Send As / rejection checks. All working.
- **Pre-deploy check:** all 5 delegates had `access_level = write`.

## 4. How to work on it

- **Build and test:** see `CLAUDE.md`. DB-backed tests need `%USERPROFILE%\.miautrix-test.env`, which holds `MIAUTRIX_DB_CONNECTION` for the dev DB and `MIAUTRIX_TEST_SERVER_CONNECTION` for the CREATEDB maintenance connection.
  - Load it into the test process only, and never print it. The procedure is in [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §14.
  - Test guards refuse any database whose name contains `-pro`.
- **Deploy:** `scripts/deploy-release.ps1 -DryRun [-RunMigrations]`, then the real run. See [`DEPLOYMENT.md`](DEPLOYMENT.md).
  - A deploy needs `-RunMigrations` whenever new migrations exist.
  - Each deploy takes a `pg_dump` before migrating and keeps 2 releases.
- **Schema drift:**
  - `scripts/db-inspect.sh` gives a read-only migration and drift report.
  - `scripts/db-schema-snapshot.sh` produces a fingerprint to diff between production, dev and a fresh install (TROUBLESHOOTING §13).
- **Migrations:** EF Core only, forward-only, written idempotent (`IF NOT EXISTS`). Prove each on a throwaway DB and on dev.
- **Health checks** use loopback or the server IP, never the public hostname (it sits behind Cloudflare). Never hardcode hostnames; use `<imap-hostname>` / `<web-hostname>`.
- **Workers:** Orca/Codex workers have no production access and never commit. The coordinator reviews, runs every suite and commits.

## 5. Rollback

`scripts/deploy-release.ps1 -Rollback` switches the code only. The previous release (`fb534f1`) predates six migrations: delegate send flags, subscriptions, `\Recent` and the UIDVALIDITY sequence.

- **Compatibility:** the new columns have defaults and the old code ignores them, so a code-only rollback should start. It is not tested, though.
- **Re-opens spoofing:** a code-only rollback brings back the sender-spoofing hole.
- **Full rollback:** restore the pre-migration dump with `-Rollback -RestoreDatabase`. This needs owner approval, and it loses mail received since the deploy.

See TROUBLESHOOTING §5.

## 6. Open items, in priority order

1. **IMAP Phase 2: IMAP4rev2 (RFC 9051).**
   - Evaluate and implement NAMESPACE, MOVE, IDLE, ESEARCH, LIST-EXTENDED/LIST-STATUS and STATUS=SIZE, plus the other mandatory rev2 behaviors.
   - Advertise `IMAP4rev2` alongside `IMAP4rev1` only after full validation with the same clients.
   - Requirements: `docs/IMAP Report.md`. Matrix template: `IMAP-Phase1-Report.md`.
2. **Post-deploy schema fingerprint.** Capture `artifacts/schema-pro.txt` and compare it with a fresh install. The expected difference is only the legacy `AddSmtpQueueDirection` history row.
3. **Inline images:** `cid:` references are not yet mapped to the attachment endpoint.
4. **Folder rename REST endpoint:** IMAP RENAME works, but webmail has no endpoint.
5. **Ops:** switch deploys to SSH keys (`-SshKey`) and rotate the LXC root password.
6. **Windows package (V2-10):** rebuild from `main` for external testers.
7. **Older items:** CF-07 Worker Dashboard deploy, RSK-07 and RSK-08 (PMI).

## 7. Where to look

| Need | Document |
|---|---|
| Plan, milestones, change log | `PMI_Project_Charter_and_Plan.md` |
| Every defect and its root cause | `ERRORS_AND_ISSUES.md` (§13.7–§13.13 cover v2.0 IMAP and sending) |
| Release notes | `CHANGELOG.md` |
| Operations and fixes | `docs/TROUBLESHOOTING.md` and `docs/TROUBLESHOOTING-scripts.zip` |
| IMAP behaviour and client setup | `docs/IMAP.md` |
| Send As model | `docs/SMTP-SendAs-Report.md` |
