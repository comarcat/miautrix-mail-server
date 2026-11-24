# Session Handoff: Version 2.0 (2026-11-23 → 2026-11-24)

**Read this first.** It covers where v2.0 stands, what was verified, and what the next session must do.

## 1. State at handoff

| Item | Value |
|---|---|
| Branch | `release/v2.0-imap-mime` (created from `main` @ `dbf8ce3`, the same commit as `origin/main`) |
| Commits | **None.** All v2.0 work is uncommitted in the working tree, waiting for owner review |
| Production | Unchanged. `10.11.1.51` still runs the self-contained **net8.0 (8.0.31)** v1.0 build |
| Database | Unchanged. No migration was added or run. `messages reparse` was **not** run |
| Version label | 2.0.0 (`CHANGELOG.md`, PMI milestone **M5**) |

Verified by the coordinator after the last worker report:

- `dotnet build Miautrix.Mail.sln -warnaserror`: 0 warnings, 0 errors
- `dotnet test tests/Miautrix.Mail.UnitTests`: 70/70
- `dotnet test tests/Miautrix.Mail.SecurityTests`: 6/6
- `pnpm --filter webmail test`: 15/15
- `pnpm --filter webmail build`: passed

**Not run:**
- the DB-backed Integration and Protocol test projects (no Docker or test database on this machine);
- deploy;
- `messages reparse`;
- the external `imaplib` and Himalaya validation.

## 2. What v2.0 contains

| Task | Summary | Key files |
|---|---|---|
| V2-01 .NET 10 | Restored `net10.0` / C# 14 after the MIA-89 downgrade (commit `27e53dc`). Microsoft packages are on 10.0.x. Regression guards: `global.json` (SDK 10.0.403), `Directory.Build.targets` (the build fails unless `net10.0`), a unit test, and the rule `.claude/rules/target-framework.md` (linked from `CLAUDE.md` and `AGENTS.md`) | `Directory.Build.props`, `global.json`, `Directory.Build.targets`, `tests/Miautrix.Mail.UnitTests/UnitTestProjectSmokeTests.cs` |
| V2-02 MIME parsing | The hand-written parser in `InboundQueueDispatcher` was the root cause of the webmail `=20` / `3D""` defect: top-level quoted-printable was never decoded, charsets were ignored, and subjects were not decoded. It is replaced by MimeKit behind `IMimeMessageParser`. The Application layer has no MimeKit reference, and a test enforces that | `src/Miautrix.Mail.Application/Mail/Mime/*`, `src/Miautrix.Mail.Infrastructure/Mail/*`, `tests/Miautrix.Mail.UnitTests/Mime/*` |
| V2-03 Backfill | `messages reparse --tenant <id\|all> [--since] [--dry-run] [--user]` rebuilds the body and subject of existing rows from the stored `.eml`. Without `--user` it runs as the local host operator, the same trust model as backup/restore. Every run writes an audit record | `MessageReparseService.cs`, `Cli/Commands/MessageReparseCommand.cs` |
| V2-04 Webmail | Message bodies render in a sandboxed iframe (no `allow-scripts`), with a CSP and DOMPurify. Remote images are blocked by default. The iframe resizes after each load | `webmail/src/components/SanitizedMessageBody.tsx` |
| V2-05 IMAP | Rewritten protocol layer: buffered byte reader, tokenizer and collector (`{n}` / `{n+}`), segmented response writer, state machine, sequence sets. Every RFC 3501 command is dispatched. Authorization goes through `ITenantAuthorizationHelper` with the user id the authenticator returns. Passwords are no longer logged. Folder rename was added to `MailboxService` | `src/Miautrix.Mail.Protocols.Imap/*`, `src/Miautrix.Mail.Worker/ImapListenerService.cs` |
| V2-06 Tests | Unit tests for the parser, framing, literals, SEARCH/FETCH and MIME. MailKit and raw-socket protocol tests are written but compile-only here | `tests/Miautrix.Mail.UnitTests/Imap/*`, `tests/Miautrix.Mail.ProtocolTests/Imap/ImapMailKitProtocolTests.cs` |

Architecture change to know about: `IMailStorage` / `StorageWriteResult`, the backup contracts and the mailbox-archive contracts moved into the Application assembly (they keep their namespaces). Application no longer references Infrastructure; this was required so that Infrastructure can implement the MIME parser.

## 3. Next session, in order

0. ✅ **Credentials in repository history are not valid in production** (owner confirmed 2026-11-24; RSK-11 mitigated). Still recommended: switch deploys to SSH keys (`-SshKey`), and rotate the LXC root password, which was typed into a terminal paste during the 2026-11-24 deploy.
1. **Run the read-only server inspection** (storage pinning, `pg_dump` presence, disk, nginx paths) before the first deploy. `deploy-release.ps1 -DryRun` runs the same preflight.
2. **Review and commit** the branch. Start with `git diff --stat`. Suggested split: (a) .NET 10 and guards, (b) MIME, reparse and webmail, (c) IMAP, (d) docs. Then open a PR to `main`.
3. **Run the DB-backed tests against a NON-production database.** Never use `10.11.1.52/miautrix-mail-pro`. Install Docker or provision a throwaway PostgreSQL, set `MIAUTRIX_DB_CONNECTION`, then run `dotnet test`. Fix any failures in `ImapMailKitProtocolTests`, `ImapListenerRawSocketTests` and the integration tests.
4. ✅ **Deployed 2026-11-24** as `v2.0.0-0394daf-202611241633` (verified). v1.0 is kept as `legacy-net8.0-20261124T155032Z`; roll back with `scripts/deploy-release.ps1 -Rollback`. Future deploys: `scripts/deploy-release.ps1 -DryRun`, then `scripts/deploy-release.ps1` (see [DEPLOYMENT.md](DEPLOYMENT.md)). The app server has `postgresql-client-18` because production PostgreSQL is 18.6.
5. ✅ **Backfill applied 2026-11-24:** 76 messages corrected, 0 failed. Run the CLI on the server after loading `/opt/miautrix-mail/.env` (see ERRORS §13.6 and DEPLOYMENT.md).
6. **External IMAP validation:** run `python scripts/imap-external-validation.py` (it logs in and prints PASS/FAIL for IMAP-001..004). **First fix IMAP-006** (UID 0 on every delivered message, ERRORS §13.7, PMI V2-08). That is the next development task.
6a. ✅ **`docs/TROUBLESHOOTING.md` written** (V2-09). After changing any script in its appendix, run `pwsh scripts/build-troubleshooting-bundle.ps1` (use `-Check` to detect drift).
6c. **IMAP-006 (V2-08) is implemented but not deployed.** Run the DB-backed tests on a throwaway PostgreSQL, then deploy with `scripts/deploy-release.ps1 -RunMigrations`. UIDs are renumbered and UIDVALIDITY changes, so clients resync once; the migration is forward-only (restore the pre-deploy dump to go back). Then re-run `scripts/imap-external-validation.py`, which now checks for UID 0 and duplicate UIDs.
6d. **Windows package (V2-10):** build with `pwsh scripts/build-windows-package.ps1` (needs a clean tree). Give the tester the zip, its `.sha256` file and `docs/WINDOWS-INSTALL.md`.
6b. ✅ **Test-tenant cleanup applied 2026-11-24:** 297 test tenants removed; only `default` remains (`scripts/cleanup-test-tenants.sh --list` shows 0 candidates).
7. **Close M5** in the PMI only after steps 3–6 pass.

## 4. Known gaps and follow-ups

- **Inline images:** `cid:` references in HTML bodies are not yet mapped to the attachment endpoint, so inline images still show as blocked or broken.
- **Folder rename:** there is no REST endpoint for it; the Application method exists.
- **IMAP edge cases:**
  - subscriptions are not persisted (LSUB returns every folder);
  - `RECENT` is always 0;
  - renaming INBOX returns `NO [CANNOT]`;
  - exhaustive MIME and partial-FETCH combinations are untested.
  - Details: `ERRORS_AND_ISSUES.md` §13.3.
- **SEARCH** evaluates in memory over the folder, which is acceptable for now. Consider pushing filters into the database for large folders.
- **Risks:**
  - RSK-09: the DB tests have not been executed;
  - RSK-10: the net8 → net10 runtime switch on deploy;
  - RSK-11: a production database credential is present in repository history; owner rotation is required.
- **Older items** still open from before v2.0: CF-07 Worker Dashboard deploy, RSK-07 and RSK-08.

## 5. Environment notes

- The repository lives under OneDrive:
  - **Cloud-only files:** files can go cloud-only, which makes `git status` fail with `mmap failed` and makes reads return "Permission denied". If that happens, resume OneDrive sync and wait for "Up to date".
  - **Broken `node_modules`:** OneDrive can break `node_modules`. Fix it with `pnpm install --frozen-lockfile --force`.
  - The owner chose to keep the repository under OneDrive.
- SSH to `10.11.1.51` with `~/.ssh/miautrix_lxc` is refused (root, deploy and miauadmin). Ask the owner to run server commands, or for the correct key.
- **Orchestration:** this session used an Orca coordinator (Claude) and a Codex worker (`gpt-6-luna`, medium effort). The run id is `run_d8dc063d1049`.
