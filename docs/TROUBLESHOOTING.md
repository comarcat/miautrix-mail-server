# Troubleshooting — Miautrix Mail Server v2.1

This manual is for the owner-operator running PowerShell on Windows and administering the Debian LXC app server as root over SSH. The app server runs self-contained .NET; PostgreSQL 18.6 is on a separate server. Replace placeholders such as `<tenant-guid>` and `<db-name>` before use; never paste secrets into a command.

## How to use this manual

Find the closest symptom in the table, start with the read-only Diagnose commands, and make a change only after confirming the target. Commands marked **changes data** can alter production. Windows examples run from the repository root unless stated; server examples run in a root bash shell. `ssh` commands use single-quoted remote commands so PowerShell does not expand their contents. Hostname placeholders are `<imap-hostname>` for mail clients and `<web-hostname>` for the web UI; the current production values are configured by the operator and may change.

| Symptom | Section |
|---|---|
| Deploy refuses to run, tests fail, SSH asks for a password, or script has CRLF errors | [Deploy pipeline](#1-deploy-pipeline) |
| Preflight says tools/storage/files/paths are invalid | [Server preflight](#2-server-preflight) |
| Backup fails or `pg_dump` is too old | [Backups](#3-backups) |
| Verify fails, despite healthy units or listeners | [Verification and automatic rollback](#4-verification-and-automatic-rollback) |
| Need to return to previous release or restore DB | [Rollback and restore](#5-rollback-and-restore) |
| Service or nginx is down; need logs | [Services and logs](#6-services-and-logs) |
| CLI says DB or storage environment is missing | [CLI and `.env`](#7-running-the-cli-on-the-server) |
| HTML mail shows `=20` / `3D""` or images are absent | [Webmail rendering](#8-webmail-rendering) |
| External IMAP checks fail or clients see UID issues | [IMAP](#9-imap) |
| Test tenants remain in the database | [Test-tenant cleanup](#10-test-tenant-cleanup) |
| Credentials may be exposed or need rotation | [Security hygiene](#11-security-hygiene) |
| Support asks for logs and system state | [Collect diagnostics](#12-collect-diagnostics-for-support) |
| `-RunMigrations` fails (`42701 already exists`, `42P07`, pending migration that is "already there") | [Migrations and schema drift](#13-database-migrations-and-schema-drift) |
| Need to run the database tests without Docker | [Database tests on a throwaway server](#14-database-tests-on-a-throwaway-postgresql-server) |

## 1. Deploy pipeline

**Symptom.** `Working tree must be clean; commit or pass -AllowDirty for a reviewed local snapshot.` Or frontend tests fail on a newer Node version with localStorage-related errors. SSH prompts for a password or the server reports `$'\r': command not found`.

**Diagnose.** From the repository root:

```powershell
git status --short -- . ':(exclude)graphify-out'
node --version
pnpm --version
Get-Content scripts/lxc-release.sh -TotalCount 3
```

The deploy cleanliness check excludes `graphify-out`; ignored `publish/` and `__pycache__/` files do not make the working tree dirty. On Node 26, older admin Vitest suites failed because Node's global `localStorage` shadowed jsdom's; the test setup was corrected. Use the repository's current test setup and confirm Node/pnpm before treating this as a product regression.

**Fix.** Review and commit intended changes, or use `-AllowDirty` only for a reviewed local snapshot. If the key does not exist, create it first with `ssh-keygen -t ed25519`. Install the public key for root, then pass the private-key file with the verified `-SshKey` parameter; with a key, the script uses `BatchMode=yes` and `-i`:

```powershell
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh root@10.11.1.51 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys"
./scripts/deploy-release.ps1 -DryRun -SshKey "$env:USERPROFILE\.ssh\id_ed25519"
```

The key installation command changes the server's SSH access configuration. If shell scripts have CRLF line endings, normalize them to LF in the repository and upload the corrected file; do not edit on the server by pasting a CRLF script. **Changes data/configuration:** a real deploy is `./scripts/deploy-release.ps1`; `-DryRun` runs local gates/publish/upload and remote preflight/status but does not switch releases. `-Rollback`, `-Status`, `-SkipTests`, `-RunMigrations`, `-SkipCleanup`, `-AllowDirty`, `-SshKey`, `-TargetIp`, and `-TargetUser` are the script's declared parameters.

**Verify.** Re-run `./scripts/deploy-release.ps1 -DryRun -SshKey "$env:USERPROFILE\.ssh\id_ed25519"` and check the computed release ID and preflight output. Only run the real deploy after owner review.

**Rollback/escalate.** A dry run does not change the active release. For a failed real switch, see [Verification and automatic rollback](#4-verification-and-automatic-rollback) and [Rollback and restore](#5-rollback-and-restore). Do not use the deprecated `deploy-compile-upload.ps1` for normal deployments.

## 2. Server preflight

**Symptom.** Preflight reports `Missing required tools: ...`, `MIAUTRIX_STORAGE_DIR must be explicit...`, mail files under `app/mail_data` or `worker/mail_data`, or a path-valued environment setting inside a deploy-managed directory.

**Diagnose.** On the app server:

```bash
for t in tar systemctl ss pg_dump pg_restore curl openssl timeout nginx; do command -v "$t" || true; done
systemctl show -p Environment -p EnvironmentFiles miautrix-mail miautrix-mail-worker
find /opt/miautrix-mail/app/mail_data /opt/miautrix-mail/worker/mail_data -type f -print 2>/dev/null
find /opt/miautrix-mail/app /opt/miautrix-mail/worker -type f \( -name '*.env' -o -name '*.pem' -o -name '*.key' -o -name '*.pfx' -o -name '*.crt' \) -print
```

Required tools are `tar`, `systemctl`, `ss`, `pg_dump`, `pg_restore`, `curl`, `openssl`, `timeout`, and `nginx`. Preflight refuses server-local config/secrets/data that would be absent from an upload, and absolute `*_PATH`, `*_DIR`, `*_FILE`, TLS, or storage paths resolving under `/opt/miautrix-mail/{app,worker,cli,admin,webmail,releases}`.

**Fix.** **Changes data:** copy the storage helper from the Windows repository to the app server, inspect its report-only output, then run the pin operation to relocate blobs and pin the value:

```powershell
scp scripts\lxc-prepare-storage.sh root@10.11.1.51:/tmp/lxc-prepare-storage.sh
ssh root@10.11.1.51 'bash /tmp/lxc-prepare-storage.sh'
ssh root@10.11.1.51 'bash /tmp/lxc-prepare-storage.sh --pin'
```

Set `MIAUTRIX_STORAGE_DIR` to the same existing absolute path for Web and Worker, outside release-managed paths. Keep appsettings overrides, certificates, keys, data-protection keys, logs, and `.env` in server configuration locations outside the upload-managed app/worker directories. Install missing packages only after reviewing the preflight's package suggestion.

**Verify.** Confirm both service environments resolve to the same absolute existing directory and no files remain under app/worker `mail_data`; run `./scripts/deploy-release.ps1 -DryRun` from Windows to repeat preflight.

**Rollback/escalate.** Do not delete or move remaining data to make preflight pass. Stop and arrange a storage recovery plan if `--pin` reports unexpected layout or insufficient space. Keep the original data until the relocated store is verified.

## 3. Backups

**Symptom.** Backup reports a `pg_dump` server/client version mismatch, cannot connect, or cannot find credentials.

**Diagnose.** Production PostgreSQL is 18.6; the `pg_dump` client major must be at least 18. On the app server, use a protected `PGPASSFILE` and check versions without printing credentials:

```bash
pg_dump --version
PGPASSFILE=/root/.pgpass psql -h 10.11.1.52 -U <database-user> -d <database-name> -c 'SHOW server_version'
ls -lh /opt/miautrix-mail/backups
```

**Fix.** Install `postgresql-client-18` from PGDG when the app server's client is missing or older. The release script reads the connection from `/opt/miautrix-mail/.env`, constructs a temporary mode-0600 `PGPASSFILE`, and does not put the password in argv. Do not use `PGPASSWORD`. Release dumps are custom-format `pg_dump -Fc --no-owner`, with manifests and protected config snapshots in `/opt/miautrix-mail/backups/`.

**Verify.** Confirm `pg_dump --version` reports major 18 or later and a new dump plus manifest exists after an approved release operation. Retention runs only after successful verification; cleanup retains current/previous releases and the two newest manifest-associated DB/config backup sets.

**Rollback/escalate.** Preserve the pre-switch dump while investigating. If credentials or target DB are uncertain, stop and confirm with the owner; do not retry against a guessed database.

## 4. Verification and automatic rollback

**Symptom.** Deploy exits non-zero with failed verify checks, then attempts automatic rollback. A past false failure showed 401 for the health endpoint or `runtime=unknown` even though the service was running.

**Diagnose.** On the app server:

```bash
systemctl status miautrix-mail miautrix-mail-worker --no-pager
ss -ltnp | grep -E ':(25|465|587|993)\b'
curl -sS -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5000/api/v1/system/info
find -L /opt/miautrix-mail/app/ -maxdepth 1 -name '*.runtimeconfig.json' -type f -print
printf 'a1 CAPABILITY\r\na2 LOGOUT\r\n' | timeout 5 openssl s_client -quiet -connect 127.0.0.1:993 -servername localhost
```

The deploy verifier emits a per-condition diagnostic for both units, ports 25/465/587/993, local Kestrel HTTP status, active release runtimeconfig TFM, and IMAPS greeting. It probes `127.0.0.1`, never the public hostname: Cloudflare/public routing is not the local service check. A 401 from `/api/v1/system/info` is expected because it requires authenticated admin access; any status below 500 establishes local HTTP liveness. The runtimeconfig lookup follows symlinked release paths; earlier `find` behavior incorrectly missed runtimeconfig files under symlinked directories and is fixed.

**Fix.** Use the first failed check's diagnostic. Inspect unit logs and confirm the active release's runtimeconfig TFM matches its metadata (v2.0 net10.0; legacy rollback may be net8.0). Correct local service/config issues before another deploy; do not weaken checks or change the probe to a public Cloudflare hostname.

**Verify.** Re-run `./scripts/deploy-release.ps1 -Status` from PowerShell, or after a reviewed deploy confirm all units, ports, local HTTP liveness, runtimeconfig, and greeting checks pass.

**Rollback/escalate.** On switch/verification failure the script attempts rollback and exits non-zero. Staging failure only removes the partial release and does not roll back a healthy release. Preserve the backup and diagnostics; if automatic rollback itself reports failure, inspect actual `.current`, symlinks, and both units before another change.

## 5. Rollback and restore

**Symptom.** A release must be reverted, or the application version rollback requires restoring an older database state.

**Diagnose.** From Windows:

```powershell
./scripts/deploy-release.ps1 -Status
```

On the server, inspect release IDs and dumps:

```bash
cat /opt/miautrix-mail/releases/.current /opt/miautrix-mail/releases/.previous
ls -lh /opt/miautrix-mail/backups
```

**Fix.** **Changes service state:** switch to `.previous` without DB restore:

```powershell
./scripts/deploy-release.ps1 -Rollback
```

Database restore is destructive. Only with explicit owner approval, use:

```powershell
./scripts/deploy-release.ps1 -Rollback -RestoreDatabase
```

The script requires typing `RESTORE DATABASE`. A manual restore should use the exact `pg_restore` command printed by `lxc-release.sh rollback`; verify the chosen file and target database first. General shape (replace the dump path and database only after confirmation):

```bash
PGPASSFILE=/root/.pgpass pg_restore --clean --if-exists --no-owner -h 10.11.1.52 -U <database-user> -d <database-name> /opt/miautrix-mail/backups/<verified-dump>.dump
```

The manual form requires a valid protected PGPASSFILE. Never restore merely because the application rollback occurred; code rollback does not restore the database.

**Migration-aware rollback.** Before any rollback, check which migrations the target release predates (`db-inspect.sh` lists the applied history).
- **Example:** rolling v2.0.0 (`76c8e7c`) back to `fb534f1` crosses six migrations: delegate send flags, IMAP subscriptions, `\Recent` and the UIDVALIDITY sequence.
- **Code-only rollback:** the new columns have defaults, so the old code should run against them. It is untested, though, and it **re-opens sender spoofing** (ERRORS §13.13).
- **Release before `fb534f1`:** that release writes UID 0. It needs the pre-migration dump restored (owner approval; mail received since the deploy is lost).

**Verify.** Run `./scripts/deploy-release.ps1 -Status`, check both services and local IMAP/Kestrel health, then confirm the expected release ID. After an approved restore, verify application data against the selected recovery point.

**Rollback/escalate.** There is no safe automatic undo for a destructive `pg_restore`. Stop and escalate if the dump, database, or migration state is uncertain. Cleanup syntax is `cleanup --keep 2 --dry-run` to preview and `cleanup --keep 2` to apply; the latter deletes eligible old release/backup sets and is **changes data**. Use `-SkipCleanup` on deploy to preserve older material.

## 6. Services and logs

**Symptom.** Web/Worker is stopped, SMTP/IMAPS ports fail, or nginx config fails.

**Diagnose.** On the app server:

```bash
systemctl status miautrix-mail miautrix-mail-worker nginx --no-pager
journalctl -u miautrix-mail -n 100 --no-pager
journalctl -u miautrix-mail-worker -n 100 --no-pager
journalctl -u nginx -n 100 --no-pager
nginx -t
ls -l /opt/miautrix-mail/.env
```

The systemd unit names are `miautrix-mail` and `miautrix-mail-worker`; environment configuration is in `/opt/miautrix-mail/.env` and/or unit overrides. The `.env` must be readable by the services that need it and restricted to root/service account; do not print it into support logs.

**Fix.** Correct the specific config error with secrets protected. Validate nginx before reloading. **Changes service state:**

```bash
nginx -t && systemctl reload nginx
systemctl restart miautrix-mail miautrix-mail-worker
```

**Verify.** Check both unit states, recent logs, nginx config, and listener ports 25/465/587/993.

**Rollback/escalate.** If the new config prevents service startup, restore the last known-good protected config and restart. Avoid repeated restarts without reading the relevant journal entries.

## 7. Running the CLI on the server

**Symptom.** CLI exits with `[CLI] FATAL: MIAUTRIX_DB_CONNECTION environment variable is required.` or reports missing storage configuration.

**Diagnose.** The CLI does not load `.env` by itself. Confirm the file exists and is restricted without displaying its contents:

```bash
stat -c '%a %U:%G %n' /opt/miautrix-mail/.env
```

**Fix.** The CLI needs all variables in `.env`, not only the database and storage values. Do not `source` `.env`: connection strings contain semicolons. The proven loader below exports every non-comment line, stripping surrounding double quotes:

```bash
while IFS= read -r l; do [[ -z $l || $l == \#* ]] && continue; k=${l%%=*}; v=${l#*=}; v=${v#\"}; v=${v%\"}; export "$k=$v"; done < /opt/miautrix-mail/.env
```

From PowerShell, the same loader can be followed by the CLI command in one single-quoted SSH command:

```powershell
ssh root@10.11.1.51 'while IFS= read -r l; do [[ -z $l || $l == \#* ]] && continue; k=${l%%=*}; v=${l#*=}; v=${v#\"}; v=${v%\"}; export "$k=$v"; done < /opt/miautrix-mail/.env; /opt/miautrix-mail/cli/Miautrix.Mail.Cli messages reparse --tenant all --dry-run'
```

Run CLI commands only after checking that the environment points to the intended production DB/storage. `messages reparse` requires `--tenant` and supports `--since`, `--dry-run`, and `--user`; without `--user` the local host operator is trusted. Do not combine `--tenant all` with `--user`.

**Verify.** Run the command's `--dry-run` first and inspect the printed `scanned`, `updated`, `skipped`, `failed`, and `dry-run` counts.

**Rollback/escalate.** Do not run mutating commands if the active DB or storage path is unclear. A parse/rewrite is audited; consult the backup/recovery procedure before proceeding if data is unexpected.

## 8. Webmail rendering

**Symptom.** Message body contains literal `=20` or `3D""` artifacts, or remote images are blank.

**Diagnose.** The encoded artifacts indicate the message row was stored by the old MIME parser. V2.0 parses new inbound mail through MimeKit, but existing stored rows are not rewritten automatically. Remote images are blocked by default in the sandboxed message view.

**Fix.** Preview the repair first. **Changes data** when run without `--dry-run`:

```bash
/opt/miautrix-mail/cli/Miautrix.Mail.Cli messages reparse --tenant all --dry-run
/opt/miautrix-mail/cli/Miautrix.Mail.Cli messages reparse --tenant <tenant-guid> --dry-run
/opt/miautrix-mail/cli/Miautrix.Mail.Cli messages reparse --tenant <tenant-guid>
```

The CLI does not load `.env` automatically: run the [section 7 loader](#7-running-the-cli-on-the-server) first, or use its PowerShell SSH form (b) to execute a reparse command. Review counts, then run for one tenant before considering all tenants. For a fresh HTML message, send a new test message after deployment and verify normal text/HTML decoding in Webmail. Use **Load Remote Images** only when you trust the sender and want externally hosted images loaded; remote CSS fetches are removed and `cid:` inline image mapping remains a known gap.

**Verify.** Reopen the repaired message and confirm readable text, decoded subject, and expected formatting. Confirm dry-run reports changes before applying.

**Rollback/escalate.** Keep the original `.eml` and a valid backup. If reparse reports failures, stop and retain the counts; do not repeatedly rewrite without reviewing logs.

## 9. IMAP

**Symptom.** External validation prints `FAIL`, clients cannot connect, or a client repeats messages/loses flags after resync.

**Diagnose.** The validation script reads `MIAUTRIX_IMAP_HOST`, `MIAUTRIX_IMAP_USER`, and `MIAUTRIX_IMAP_PASSWORD`; optional `MIAUTRIX_IMAP_INSECURE=1` skips certificate validation for IP-only testing. From PowerShell, set secrets with a hidden prompt, not literal assignment:

```powershell
$env:MIAUTRIX_IMAP_HOST = '<imap-hostname>'
$env:MIAUTRIX_IMAP_USER = Read-Host 'IMAP user'
$secure = Read-Host -Prompt 'IMAP password' -MaskInput
$env:MIAUTRIX_IMAP_PASSWORD = [System.Net.NetworkCredential]::new('', $secure).Password
python scripts/imap-external-validation.py
Remove-Item Env:MIAUTRIX_IMAP_PASSWORD
```

Use `<imap-hostname>` (the hostname your mail clients use) and a publicly trusted certificate for normal testing. For testing by IP only, set `$env:MIAUTRIX_IMAP_INSECURE = '1'`; this disables certificate validation and must not be used as the normal hostname test. Read each `PASS`/`FAIL` line for IMAP-001..004 and its detail. In Thunderbird, verify the configured hostname, port 993, SSL/TLS, login, folder list, open/read a message, and refresh. Himalaya checks from the IMAP report are:

```bash
himalaya --account miautrix-sales mailbox list
himalaya --account miautrix-sales envelope list
```

**Fix.** Resolve DNS/routing, port 993, certificate name/trust, credentials, and server logs independently. For IP-only diagnostics use the insecure flag above, then return to hostname/certificate validation. UID is distinct from sequence number; UIDVALIDITY is the mailbox epoch clients use to know whether cached UIDs remain meaningful.

**Verify.** Run `python scripts/imap-external-validation.py` and check all reported tests, then test Thunderbird and both Himalaya commands. UID FETCH/SEARCH and EXPUNGE should be checked with an approved test mailbox.

**Shared mailboxes.** Delegated mailboxes appear at `Shared/<address>/<folder>`, for example `Shared/sales@<domain>/INBOX`. `INBOX` is always the logged-in user's own inbox. The full design and client setup are in `docs/IMAP.md`.

| Symptom | Check |
|---|---|
| A shared mailbox is missing from LIST | The user needs a delegate row with `access_level` `read` or `write`. Send As / Send on Behalf alone grant **no** mailbox access. Unauthorized mailboxes look nonexistent (`NO Mailbox does not exist`), by design. |
| A folder is missing in Thunderbird but present in LIST | Thunderbird shows subscribed folders. Every accessible folder is subscribed by default; an explicit UNSUBSCRIBE stores an opt-out. Re-subscribe in the client (Folder → Subscribe), or check `imap_folder_subscriptions` for `subscribed = false`. |
| Access was revoked but the client still shows the folder | The next command on it fails, because authorization is checked per command. The client's cached copy remains until it resyncs. |
| A message deleted in a client is gone, not in Trash | Expected. EXPUNGE deletes permanently (RFC 3501). Clients move a message to Trash before expunging; check the client's delete setting. |
| Every message shows as new | It shouldn't happen: messages stored before v2.0.0 start not `\Recent`. If you see it, check the `ImapRecentMessages` migration. |

Read-only checks (replace the placeholders, run on the server through `db-inspect`'s connection method):

```sql
SELECT m.address, d.access_level, d.can_send_as, d.can_send_on_behalf
FROM mailbox_delegates d JOIN mailboxes m ON m.id = d.mailbox_id
JOIN users u ON u.id = d.user_id WHERE u.email = '<user-address>';
```

**Rollback/escalate.** IMAP-006 (UID 0) is fixed and validated in v2.0.0 (2026-11-24): `uid_zero` is 0, and clients resynced once when UIDVALIDITY changed. If UID 0 persists or UID changes without UIDVALIDITY changing, stop and escalate with sanitized protocol evidence; do not delete/recreate mailboxes to mask the issue.

## 10. Test-tenant cleanup

**Symptom.** Automated test fixture tenants appear in the production tenant list.

**Diagnose.** From the Windows repository root, copy the script to the app server and run list mode. It identifies fixture-pattern tenants and always protects the production tenant; list mode is a review/rehearsal and always rolls back:

```powershell
scp scripts\cleanup-test-tenants.sh root@10.11.1.51:/tmp/cleanup-test-tenants.sh
ssh root@10.11.1.51 'bash /tmp/cleanup-test-tenants.sh --list'
```

Review the candidate count and names carefully. The script requires root, `psql`, `pg_dump`, and `pg_restore`; it takes a dump before apply.

**Fix.** **Changes data:** apply only when the reviewed count still matches:

```powershell
ssh root@10.11.1.51 'bash /tmp/cleanup-test-tenants.sh --apply --expect <count>'
```

**Verify.** Re-run `ssh root@10.11.1.51 'bash /tmp/cleanup-test-tenants.sh --list'`; success is zero candidates. The handoff records 297 removed and `--list` showing zero on 2026-11-24.

**Rollback/escalate.** Apply is transactional and backs up first, but deletion is still destructive. Stop if count or fixture names differ from the reviewed list; restore only from the confirmed pre-cleanup dump with owner approval.

## 11. Security hygiene

**Symptom.** A password may have been exposed, or a credential needs routine rotation.

**Diagnose.** Treat passwords pasted into terminals, chats, logs, screenshots, or tracked history as exposed. Never paste passwords into terminals or chats; use hidden prompts or protected files. Never print `.env`, connection strings, private keys, or PGPASSFILE contents.

**Fix.** Rotate the PostgreSQL credential at the database, update `MIAUTRIX_DB_CONNECTION` in `/opt/miautrix-mail/.env` (and any Web unit drop-in), then restart both units. Rotate the LXC root password through the server's approved account procedure and use SSH keys for deployment (`-SshKey`). Set `.env` and `.ssh` permissions narrowly; the release backup tooling protects config snapshots.

**Verify.** Confirm both services restart cleanly and inspect unit status/logs without displaying credential values. Confirm key-based SSH succeeds before closing the old access path.

**Rollback/escalate.** If either service cannot authenticate after DB credential rotation, do not revert to a known-exposed password; verify the database and environment update through a protected channel and escalate.

## 12. Collect diagnostics for support

**Symptom.** Support needs enough evidence to diagnose without receiving credentials or message content.

**Diagnose.** Run this read-only block on the app server; it reports unit state, listeners, local HTTP status, recent journal lines, release IDs, runtimeconfig filenames, nginx test, and backup filenames. It does not read `.env` contents:

```bash
printf '%s\n' '== units =='
systemctl --no-pager --full status miautrix-mail miautrix-mail-worker nginx | sed -n '1,45p'
printf '%s\n' '== listeners =='
ss -ltnp | grep -E ':(25|465|587|993)\b' || true
printf '%s\n' '== local Kestrel status =='
curl -sS -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5000/api/v1/system/info || true
printf '%s\n' '== recent logs =='
journalctl -u miautrix-mail -u miautrix-mail-worker -u nginx -n 80 --no-pager
printf '%s\n' '== release IDs =='
for f in /opt/miautrix-mail/releases/.current /opt/miautrix-mail/releases/.previous; do test ! -f "$f" || { printf '%s: ' "$f"; cat "$f"; }; done
printf '%s\n' '== runtimeconfig files =='
find -L /opt/miautrix-mail/app/ /opt/miautrix-mail/worker/ /opt/miautrix-mail/cli/ -maxdepth 1 -name '*.runtimeconfig.json' -type f -printf '%p\n' 2>/dev/null
printf '%s\n' '== nginx config test =='
nginx -t 2>&1
printf '%s\n' '== backup filenames =='
find /opt/miautrix-mail/backups -maxdepth 1 -type f -printf '%f %s bytes\n' 2>/dev/null | sort
```

**Fix.** Before sharing, redact public IPs/hostnames if desired, usernames, email addresses, tenant IDs, message IDs, database names, filesystem paths that reveal private layout, and any tokens or message content that may appear in logs. Share the exact failing check and timestamp. Never send `.env`, key material, raw message bodies, or unredacted authentication/protocol transcripts.

**Verify.** Review the captured output locally for secrets and personal data before sending. The commands above do not intentionally print environment values or message bodies.

**Rollback/escalate.** These commands are read-only. If a log line includes a credential or token, do not forward it; report that sensitive output was present and provide a redacted excerpt instead.
## 13. Database migrations and schema drift

**Symptom.** `scripts\deploy-release.ps1 -RunMigrations` stops at `== running the published migration bundle ==` with `Remote migration failed.` and a PostgreSQL error such as `42701: column "direction" of relation "smtp_queue" already exists` (or `42P07 relation ... already exists`). Incident 2026-11-24: production had run the superseded migration `20261005173819_AddSmtpQueueDirection`, which was later replaced in the repository by `20261006120000_AddQueueDirection`, so the column existed but the newer migration was not recorded as applied.

**What it means for production.** Each EF migration runs in its own transaction. The failed migration rolled back, later migrations did not run, and the deploy aborted **before** the release switch: the previous release keeps serving and the pre-migration dump from this run is in `/opt/miautrix-mail/backups/`. Confirm with `scripts\deploy-release.ps1 -Status`.

**Diagnose.** Compare the database with the repository using the read-only inspection script (its session is forced `default_transaction_read_only=on`; it reads the connection from `/opt/miautrix-mail/.env` and prints no credentials):

```powershell
scp scripts\db-inspect.sh root@10.11.1.51:/tmp/db-inspect.sh
ssh root@10.11.1.51 'bash /tmp/db-inspect.sh'
```

Then list what the repository expects (from the Windows repo root; no database connection is made):

```powershell
$env:MIAUTRIX_DB_CONNECTION = 'Host=unused.invalid;Database=unused;Username=unused'
dotnet ef migrations list --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web --no-connect
dotnet ef migrations script <last-applied-id> --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web -o artifacts/pending.sql
Remove-Item Env:MIAUTRIX_DB_CONNECTION
```

Drift exists when the database history contains an ID the repository no longer has, or when an object a pending migration creates is already present. Review `artifacts/pending.sql` to see exactly what will run.

To compare whole schemas, take a read-only structural fingerprint of each database. It lists migrations, columns, constraints, indexes, sequences, views, functions and triggers, with no row data and no credentials, and psql never prompts:

```powershell
scp scripts\db-schema-snapshot.sh scripts\db-schema-snapshot.sql root@10.11.1.51:/tmp/
ssh root@10.11.1.51 'bash /tmp/db-schema-snapshot.sh > /tmp/schema-pro.txt; echo "exit=$?"; wc -l /tmp/schema-pro.txt'
scp root@10.11.1.51:/tmp/schema-pro.txt artifacts\schema-pro.txt
```

Use `--env-file <path>` for another database (for example dev). Build the fresh-install reference by migrating an empty throwaway database (§14). Compare the files with `LC_ALL=C sort` and `diff` after stripping CR line endings. On 2026-11-24 this found `flag_alert_configurations` missing on production and dev, and a legacy queue index (ERRORS §13.11).

**Fix.** Converge through a migration, never with hand DDL (CLAUDE.md: "Nothing else issues DDL") and never by inserting history rows by hand. Make the affected migration idempotent (`ADD COLUMN IF NOT EXISTS`, `DROP CONSTRAINT IF EXISTS` then `ADD CONSTRAINT`, normalise defaults/nullability) so it produces the fresh-install shape on both kinds of database, add an integration test that reproduces the drifted state (see `Migrations_apply_over_production_drift_from_superseded_queue_direction_migration`), run the database tests (§14), commit, then re-run `scripts\deploy-release.ps1 -DryRun -RunMigrations` and the real deploy. For the 2026-11-24 incident this is `20261006120000_AddQueueDirection`: it now keeps the existing column, drops the old `DEFAULT 'Inbound'`, and adds `ck_smtp_queue_direction`; production data (`Inbound`/`Outbound` only) satisfies the constraint.

**Verify.** Re-run `db-inspect.sh`: the newest history rows include the repository's latest migration IDs, the constraint is listed, `column_default` is `<none>`, and after IMAP-006 `uid_zero` is 0 with `ck_messages_uid_positive` and `uq_messages_folder_id_uid` present. Then run `scripts\deploy-release.ps1 -Status`.

**Rollback/escalate.** If a migration fails part-way through a multi-migration run, earlier migrations in that run stay committed; the services still run the previous release, which must tolerate them (EXPAND before CONTRACT). If data must be returned to the pre-deploy state, restore the dump taken by that run (§5) with owner approval. Never edit `__EFMigrationsHistory` by hand to make an error go away.

## 14. Database tests on a throwaway PostgreSQL server

**Symptom.** Integration or protocol tests fail with `MIAUTRIX_DB_CONNECTION environment variable is required`, or the UID tests fail because Docker/Testcontainers is unavailable (the project uses LXC, not Docker).

**Diagnose.** Two variables drive the database tests; both must point at a **non-production** server:

| Variable | Used by | Points at |
|---|---|---|
| `MIAUTRIX_TEST_SERVER_CONNECTION` | `MessageUidAllocatorTests` (UID allocator, IMAP-006 migration, migration-drift test) | the maintenance database (`postgres`) as a role with `CREATEDB`; each test creates and drops its own `uidtest_*` database. Without it, Testcontainers starts `postgres:18-alpine` (needs Docker). The fixture refuses database names that look like an application database. |
| `MIAUTRIX_DB_CONNECTION` | IMAP protocol tests, API/integration tests | an empty, dedicated test database (the tests migrate it themselves) |

**Fix.** On the PostgreSQL server create a test-only role and database (never reuse the production role):

```bash
sudo -u postgres psql -c "CREATE ROLE miautrix_test LOGIN CREATEDB;"
sudo -u postgres psql -c "\password miautrix_test"
sudo -u postgres createdb -O miautrix_test <test-db-name>
```

Allow the workstation in `pg_hba.conf` for that role only and reload PostgreSQL. On Windows keep the connection strings in a file outside the repository (for example `%USERPROFILE%\.miautrix-test.env`, one `NAME=value` per line) and load it into the current session only:

```powershell
Get-Content "$env:USERPROFILE\.miautrix-test.env" | ForEach-Object { $k, $v = $_ -split '=', 2; Set-Item "Env:$k" $v }
dotnet test tests/Miautrix.Mail.IntegrationTests --filter "FullyQualifiedName~MessageUidAllocatorTests"
dotnet test tests/Miautrix.Mail.ProtocolTests
dotnet test tests/Miautrix.Mail.IntegrationTests
Remove-Item Env:MIAUTRIX_TEST_SERVER_CONNECTION, Env:MIAUTRIX_DB_CONNECTION
```

**Verify.** All three runs report `Failed: 0`. On the server, `sudo -u postgres psql -Atc "SELECT datname FROM pg_database WHERE datname LIKE 'uidtest_%'"` returns nothing after the run.

**Rollback/escalate.** If a run is interrupted, drop leftover `uidtest_*` databases as the test role. Never point either variable at the production database or the production role.

## 15. Sending as a shared mailbox (Send As / Send on Behalf)

**Symptom.** Sending fails with `550 5.7.1`, or webmail says "Sender address is not authorized"; or the recipient sees "on behalf of" when they shouldn't, or the reverse.

**Diagnose.** The single policy is `ISenderAuthorizationService`, used by both SMTP submission and webmail:

| From address | Allowed when |
|---|---|
| The user's own address or one of their aliases | Always |
| A shared mailbox | The delegate has `can_send_as`. The message goes out as-is, with no `Sender:` header |
| A shared mailbox | The delegate has only `can_send_on_behalf`. The server sets `Sender:` to the user, and the envelope `MAIL FROM` must be the user's own address |
| Anything else (other users, other tenants, external domains, `<>`, a forged header `From`) | Never: `550 5.7.1` |

Full Access (`access_level = write`) does **not** imply Send As. Denials are audited without bodies or secrets:

```sql
SELECT created_at, actor_id, details_json FROM audit_logs
WHERE action = 'sender.authorization.denied' ORDER BY created_at DESC LIMIT 20;
```

Use the delegate query in §9 to see the user's flags.

**Fix.** Grant **Send As** or **Send on Behalf** on the shared mailbox's delegate in admin. Send As wins if both are set. For SMTP, make sure the client authenticates as the user and uses the shared address only in `From:`.

**Verify.** Run the manual SMTP checks in `docs/SMTP-SendAs-Report.md`: a granted send is accepted, and an ungranted or foreign address gets `550 5.7.1`. In the received message, check `From:` and `Sender:`.

**Rollback/escalate.** If a legitimate sender is rejected after the grant, collect the audit `details_json` (domain and reason only) and escalate. Never work around it by granting a broader role. SMTP AUTH is never offered without TLS (`538 5.7.11`), by design.

## 16. SMTP listener limits, TLS and replies (v2.1)

**Symptom.** A client or remote server gets a reply it didn't before v2.1, for example `452 4.5.3`, `552 5.3.4`, `500 5.5.2` or `503 5.5.1`; a TLS handshake fails; or a message to several recipients reached only some of them.

**Diagnose.** These replies are the RFC 5321 limits v2.1 enforces. Before v2.1 there were no limits, and only the **last** recipient of a message received it.

| Reply | Meaning | Default and setting |
|---|---|---|
| `452 4.5.3` | Too many recipients in one message | 100, `MIAUTRIX_SMTP_MAX_RECIPIENTS` |
| `552 5.3.4` | Message, or a `MAIL FROM ... SIZE=`, larger than the limit (SIZE in EHLO) | 26214400 bytes, `MIAUTRIX_SMTP_MAX_MESSAGE_SIZE` |
| `500 5.5.2` | A command line over 512 octets, or a DATA line over 1000 octets | Fixed (RFC 5321 §4.5.3.1) |
| `503 5.5.1` | A command out of order, for example RCPT before MAIL, or DATA without RCPT | — |
| `421` / timeout | The client went idle, or DATA stalled | 300 s each, `MIAUTRIX_SMTP_IDLE_TIMEOUT_SECONDS`, `MIAUTRIX_SMTP_DATA_TIMEOUT_SECONDS` |

All settings are optional; an invalid value stops the Worker at boot with a `FATAL` message. A connection that drops in the middle of DATA queues nothing, by design.

TLS 1.2 or newer is required on 25, 465 and 587. Port 25 now offers STARTTLS (opportunistic), and after STARTTLS the client must send EHLO again. VRFY and EXPN answer `252` without confirming addresses; HELP answers `214`.

**Fix.** Raise a limit only on purpose and only in `/opt/miautrix-mail/.env`, then restart the Worker. A sender on TLS 1.0 or 1.1 must upgrade: RFC 8996 deprecates those versions. Check the exact EHLO list per port in `docs/SMTP-v2.1-Report.md`.

**Verify.** `openssl s_client -starttls smtp -connect 127.0.0.1:25 -tls1_2` succeeds on the server, and `-tls1_1` is refused. Send one test message to two local recipients and confirm both receive it.

**Rollback/escalate.** If legitimate mail is rejected by a limit, collect the reply line and the sender domain (no message bodies) and escalate. Never remove a limit entirely.

## 17. Outbound delivery: Cloudflare or direct MX, retries and bounces (v2.1)

**Symptom.** Mail from a domain isn't delivered, is delayed, or the sender receives a "Delivery Status Notification" bounce.

**Diagnose.** Each sending domain has a **transport mode** in admin → Domains:

| Mode | Delivery | DKIM |
|---|---|---|
| `cloudflare` (the default; used by miautrix.tech) | Cloudflare Worker API | Cloudflare signs; the server leaves the message untouched |
| `direct` | The server connects to the recipient's MX hosts: preference order, falling back to A/AAAA, opportunistic STARTTLS with TLS 1.2+, MTA-STS enforced when the recipient publishes `enforce` | The server signs with the domain's selector and key |

Temporary failures (4xx, network, Cloudflare 408/429/5xx) retry with exponential backoff for up to 5 days (`OUTBOUND_RETRY_LIFETIME_DAYS`, 1–30). Permanent failures (5xx, null MX) fail at once. Either way, the local sender gets an RFC 3464 bounce. The server never bounces a bounce, and never bounces to external senders.

```sql
SELECT status, attempts, next_attempt_at, last_error FROM smtp_queue
WHERE direction = 'Outbound' AND status <> 'Delivered' ORDER BY created_at DESC LIMIT 20;
```

**Fix.**
- **Before switching a domain to `direct`:** confirm the server can reach port 25 outbound (many providers block it), that PTR/rDNS matches `OUTBOUND_EHLO_NAME` (default `MIAUTRIX_HOSTNAME`), and that SPF includes the server's IP and the DKIM TXT record `<selector>._domainkey.<domain>` is published. Details are in `docs/Outbound-v2.1-Report.md`.
- **To go back,** set the domain to `cloudflare` again.

**Verify.** Send a test message to an external mailbox and check its `Authentication-Results`: SPF and DKIM should pass and be aligned.

**Rollback/escalate.** DANE (RFC 7672) isn't implemented. If a recipient requires it, use `cloudflare` mode for that domain.

## 18. Drafts created by IMAP clients (v2.1, WEBMAIL-001)

**Symptom.** A draft saved by Thunderbird or Himalaya opens empty in webmail, or a draft edited in webmail looks old in the IMAP client.

**Diagnose.** Before v2.1, IMAP APPEND stored the raw message but didn't fill in the body fields the webmail editor reads.
- **Since v2.1:** APPEND parses with the shared MimeKit parser. A webmail save writes a full RFC 5322 message; an unchanged autosave does nothing.
- **When a draft changes:** it keeps its webmail ID and gets a **new IMAP UID**. IMAP clients see the old UID expunged and a new message added.

**Fix.** For drafts saved by IMAP clients **before** v2.1, run once on the server after the deploy, loading `/opt/miautrix-mail/.env` as in §7:

```bash
/opt/miautrix-mail/cli/Miautrix.Mail.Cli messages reparse --tenant all --dry-run   # review the updated/failed counts
/opt/miautrix-mail/cli/Miautrix.Mail.Cli messages reparse --tenant all
```

Reparse never changes the stored bytes or UIDs.

**Verify.** Open the draft in webmail and check its subject, recipients and body. Edit it and save. In the IMAP client, refresh: the draft shows the edit, and there is only one copy.

**Rollback/escalate.** If a draft still opens empty after reparse, escalate with its folder and UID (not its content).
<!-- APPENDIX:BEGIN (generated by scripts/build-troubleshooting-bundle.ps1; do not edit below) -->
## Appendix A — Script reference

### `scripts/deploy-release.ps1`

SHA-256: `06bc20112385bc5e06b07386e141cfeb0281048c6712c52939955ccabdd110e9`

``powershell
[CmdletBinding()]
param(
    [string]$TargetIp = '10.11.1.51',
    [string]$TargetUser = 'root',
    [string]$SshKey,
    [switch]$SkipTests,
    [switch]$RunMigrations,
    [switch]$DryRun,
    [switch]$Rollback,
    [switch]$RestoreDatabase,
    [switch]$Status,
    [switch]$SkipCleanup,
    [switch]$AllowDirty,
    [switch]$LocalTest
)
$ErrorActionPreference = 'Stop'
$Repo = Split-Path -Parent $PSScriptRoot
function Invoke-Checked([string]$Label, [scriptblock]$Action) {
    Write-Host "== $Label =="
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Label failed with exit code $LASTEXITCODE" }
}
function Get-ReleaseId {
    $version = Select-String -Path (Join-Path $Repo 'CHANGELOG.md') -Pattern '^## \[([^\]]+)\]' | Select-Object -First 1
    if (-not $version) { throw 'Could not read top CHANGELOG version.' }
    $sha = (& git -C $Repo rev-parse --short HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $sha) { throw 'HEAD is unavailable.' }
    return "v$($version.Matches[0].Groups[1].Value)-$sha-$([DateTime]::UtcNow.ToString('yyyyMMddHHmm'))"
}
function Test-PublishLayout([string]$Path) {
    foreach ($file in @('app/Miautrix.Mail.Web','worker/Miautrix.Mail.Worker','cli/Miautrix.Mail.Cli','admin/index.html','webmail/index.html')) {
        if (-not (Test-Path (Join-Path $Path $file))) { throw "Publish layout missing $file" }
    }
    foreach ($component in @('app','worker','cli')) {
        $runtime = Get-ChildItem (Join-Path $Path $component) -Filter '*.runtimeconfig.json' | Select-Object -First 1
        if (-not $runtime -or (Get-Content -Raw $runtime.FullName) -notmatch '"tfm"\s*:\s*"net10\.0"') { throw "$component runtimeconfig must target net10.0" }
    }
}
if ($LocalTest) {
    $id = Get-ReleaseId
    if ($id -notmatch '^v2\.0\.0-[0-9a-f]+-\d{12}$') { throw "Release id format unexpected: $id" }
    $temp = Join-Path ([IO.Path]::GetTempPath()) "miautrix-release-layout-$([guid]::NewGuid().ToString('N'))"
    try {
        foreach ($d in @('app','worker','cli','admin','webmail')) { New-Item -ItemType Directory -Path (Join-Path $temp $d) -Force | Out-Null }
        foreach ($f in @('app/Miautrix.Mail.Web','worker/Miautrix.Mail.Worker','cli/Miautrix.Mail.Cli')) { New-Item -ItemType File -Path (Join-Path $temp $f) -Force | Out-Null }
        foreach ($d in @('app','worker','cli')) { Set-Content (Join-Path $temp "$d/test.runtimeconfig.json") '{"runtimeOptions":{"tfm":"net10.0"}}' }
        Set-Content (Join-Path $temp 'admin/index.html') 'admin'; Set-Content (Join-Path $temp 'webmail/index.html') 'webmail'
        Test-PublishLayout $temp
        Write-Host "Local release plan test passed: $id; publish layout is complete. No network was used."
    } finally { if (Test-Path $temp) { Remove-Item -LiteralPath $temp -Recurse -Force } }
    exit 0
}
$ReleaseId = Get-ReleaseId
if ($ReleaseId -notmatch '^[A-Za-z0-9._-]+$') { throw 'Computed release ID contains unsupported characters.' }
if ($Rollback -or $Status) {
    if ($RestoreDatabase -and -not $Rollback) { throw '-RestoreDatabase requires -Rollback.' }
    $Subcommand = if ($Rollback) { 'rollback' } else { 'status' }
    if ($RestoreDatabase) { $Subcommand += ' --restore-db' }
    $script = Join-Path $PSScriptRoot 'lxc-release.sh'
    $opts = @('-o','StrictHostKeyChecking=accept-new')
    if ($SshKey) { $opts += @('-o','BatchMode=yes','-i',$SshKey) }
    $remoteScript = '/tmp/miautrix-lxc-release-admin.sh'
    & scp @($opts + @($script, "$TargetUser@$TargetIp`:$remoteScript"))
    if ($LASTEXITCODE -ne 0) { throw 'Could not upload release manager.' }
    & ssh @($opts + @("$TargetUser@$TargetIp", "sudo bash '$remoteScript' $Subcommand"))
    $code = $LASTEXITCODE
    & ssh @($opts + @("$TargetUser@$TargetIp", "rm -f '$remoteScript'")) 2>$null
    if ($code -ne 0) { throw "Remote $Subcommand failed." }
    exit 0
}
if (-not $AllowDirty) {
    $dirty = (& git -C $Repo status --porcelain -- . ':(exclude)graphify-out')
    if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
    if ($dirty) { throw 'Working tree must be clean; commit or pass -AllowDirty for a reviewed local snapshot.' }
}
$head = (& git -C $Repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $head) { throw 'HEAD is unknown.' }
Invoke-Checked 'Build' { dotnet build (Join-Path $Repo 'Miautrix.Mail.sln') -warnaserror }
if (-not $SkipTests) {
    Invoke-Checked 'Unit tests' { dotnet test (Join-Path $Repo 'tests/Miautrix.Mail.UnitTests') }
    Invoke-Checked 'Security tests' { dotnet test (Join-Path $Repo 'tests/Miautrix.Mail.SecurityTests') }
    $db = $env:MIAUTRIX_DB_CONNECTION
    if ($db) {
        if ($db -match '(?i)(10\.11\.1\.52|miautrix-mail-pro)') { throw 'Refusing DB-backed tests because MIAUTRIX_DB_CONNECTION targets production.' }
        Invoke-Checked 'Integration tests' { dotnet test (Join-Path $Repo 'tests/Miautrix.Mail.IntegrationTests') }
        Invoke-Checked 'Protocol tests' { dotnet test (Join-Path $Repo 'tests/Miautrix.Mail.ProtocolTests') }
    }
    Invoke-Checked 'Webmail tests' { pnpm --filter webmail test }
    Invoke-Checked 'Admin tests' { pnpm --filter admin test -- --run }
}
$PublishRoot = Join-Path $Repo "publish/$ReleaseId"
if (Test-Path $PublishRoot) { throw "Publish output already exists: $PublishRoot" }
New-Item -ItemType Directory -Path $PublishRoot -Force | Out-Null
try {
    foreach ($pair in @(@('Web','src/Miautrix.Mail.Web/Miautrix.Mail.Web.csproj','app'),@('Worker','src/Miautrix.Mail.Worker/Miautrix.Mail.Worker.csproj','worker'),@('CLI','src/Miautrix.Mail.Cli/Miautrix.Mail.Cli.csproj','cli'))) {
        $dest = Join-Path $PublishRoot $pair[2]
        Invoke-Checked "Publish $($pair[0])" { dotnet publish (Join-Path $Repo $pair[1]) -c Release -r linux-x64 --self-contained true -o $dest }
    }
    Invoke-Checked 'Build admin' { pnpm --filter admin build }
    Invoke-Checked 'Build webmail' { pnpm --filter webmail build }
    Copy-Item (Join-Path $Repo 'admin/dist') (Join-Path $PublishRoot 'admin') -Recurse
    Copy-Item (Join-Path $Repo 'webmail/dist') (Join-Path $PublishRoot 'webmail') -Recurse
    Test-PublishLayout $PublishRoot
    $metadata = @{ id=$ReleaseId; gitSha=$head; builtAtUtc=[DateTime]::UtcNow.ToString('o'); tfm='net10.0' } | ConvertTo-Json
    Set-Content -Path (Join-Path $PublishRoot 'RELEASE.json') -Value $metadata -Encoding utf8
    $archive = Join-Path $Repo "publish/$ReleaseId.tar.gz"
    tar -czf $archive -C (Split-Path $PublishRoot) $ReleaseId
    if ($LASTEXITCODE -ne 0) { throw 'Artifact tar failed.' }
    $hash = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
    $remoteTmp = "/tmp/miautrix-release-$ReleaseId"
    $scpOpts = @('-o','StrictHostKeyChecking=accept-new'); if ($SshKey) { $scpOpts += @('-o','BatchMode=yes','-i',$SshKey) }
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "mkdir -p '$remoteTmp'"))
    if ($LASTEXITCODE -ne 0) { throw 'Remote temporary directory creation failed.' }
    & scp @($scpOpts + @((Join-Path $PSScriptRoot 'lxc-release.sh'), "$TargetUser@$TargetIp`:$remoteTmp/lxc-release.sh"))
    if ($LASTEXITCODE -ne 0) { throw 'Upload of release manager failed.' }
    & scp @($scpOpts + @($archive, "$TargetUser@$TargetIp`:$remoteTmp/$ReleaseId.tar.gz"))
    if ($LASTEXITCODE -ne 0) { throw 'Artifact upload failed.' }
    $remoteBase = "set -e; cd '$remoteTmp'; printf '%s  %s.tar.gz\n' '$hash' '$ReleaseId' | sha256sum -c -; mkdir -p upload; tar -xzf '$ReleaseId.tar.gz' -C upload"
    $uploadPath = "$remoteTmp/upload/$ReleaseId"
    $remote = "$remoteBase; bash lxc-release.sh preflight --release-id '$ReleaseId' --upload '$uploadPath'"
    if ($DryRun) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "$remote; bash lxc-release.sh status"))
        if ($LASTEXITCODE -ne 0) { throw 'Dry-run preflight/status failed.' }
        Write-Host "Dry run complete for $ReleaseId. No backup, switch, or migration was run."
        exit 0
    }
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "$remote; bash lxc-release.sh backup --release-id '$ReleaseId'"))
    if ($LASTEXITCODE -ne 0) { throw 'Remote preflight or backup failed.' }
    if ($RunMigrations) {
        # The design-time factory only needs a non-empty value; no connection is opened while bundling.
        $previousDbConnection = $env:MIAUTRIX_DB_CONNECTION
        $env:MIAUTRIX_DB_CONNECTION = 'Host=bundle-build.invalid;Database=unused;Username=unused'
        try {
            Invoke-Checked 'Build migration bundle' { dotnet ef migrations bundle --project (Join-Path $Repo 'src/Miautrix.Mail.Persistence') --startup-project (Join-Path $Repo 'src/Miautrix.Mail.Web') -r linux-x64 --self-contained --output (Join-Path $PublishRoot 'migration-bundle') }
        } finally {
            $env:MIAUTRIX_DB_CONNECTION = $previousDbConnection
        }
        & scp @($scpOpts + @((Join-Path $PublishRoot 'migration-bundle'), "$TargetUser@$TargetIp`:$remoteTmp/migration-bundle"))
        if ($LASTEXITCODE -ne 0) { throw 'Migration bundle upload failed.' }
        $migration = "cd '$remoteTmp'; bash lxc-release.sh migrate --bundle '$remoteTmp/migration-bundle'"
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", $migration))
        if ($LASTEXITCODE -ne 0) { throw 'Remote migration failed.' }
    }
    $stage = "cd '$remoteTmp'; bash lxc-release.sh stage --release-id '$ReleaseId' --upload '$uploadPath'"
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", $stage))
    if ($LASTEXITCODE -ne 0) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "rm -rf '/opt/miautrix-mail/releases/$ReleaseId'")) 2>$null
        throw "Remote stage failed; partial release removed if present, no rollback was attempted. Database dump remains in /opt/miautrix-mail/backups."
    }
    $switchAttempted = $true
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "cd '$remoteTmp'; bash lxc-release.sh switch --release-id '$ReleaseId'"))
    if ($LASTEXITCODE -ne 0) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh rollback"))
        throw "Remote switch failed; rollback attempted. Database dump remains in /opt/miautrix-mail/backups; inspect server status."
    }
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh verify"))
    if ($LASTEXITCODE -ne 0) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh rollback"))
        throw "Remote verify failed; rollback attempted. Database dump remains in /opt/miautrix-mail/backups; inspect server status."
    }
    if (-not $SkipCleanup) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh cleanup --keep 2"))
        if ($LASTEXITCODE -ne 0) { throw 'Deploy verified but cleanup failed; release retained.' }
    }
    & ssh @($scpOpts + @("$TargetUser@$TargetIp", "bash /tmp/miautrix-release-$ReleaseId/lxc-release.sh status"))
    if ($LASTEXITCODE -ne 0) { throw 'Final remote status failed.' }
} finally {
    if ($remoteTmp) {
        & ssh @($scpOpts + @("$TargetUser@$TargetIp", "rm -rf '$remoteTmp'")) 2>$null
    }
}
````

### `scripts/lxc-release.sh`

SHA-256: `abc15a00c4404eda392ab7e798fdd447196d66b473881f28114e828059c42fa3`

``bash
#!/usr/bin/env bash
set -euo pipefail

# Server-side versioned release manager. MIAUTRIX_ROOT is reserved for isolated local tests.
ROOT=$(realpath -m "${MIAUTRIX_ROOT:-/opt/miautrix-mail}")
TEST_MODE=${MIAUTRIX_TEST_MODE:-0}
RELEASES="$ROOT/releases"
BACKUPS="$ROOT/backups"
APP="$ROOT/app"
WORKER="$ROOT/worker"
CLI="$ROOT/cli"
ADMIN="$ROOT/admin"
WEBMAIL="$ROOT/webmail"

die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
summary() { printf '== %s ==\n' "$*"; }
guard_managed_roots() {
  local path resolved
  for path in "$RELEASES" "$BACKUPS"; do
    [[ ! -L "$path" ]] || die "refusing managed directory symlink: $path"
    if [[ -e "$path" ]]; then resolved=$(realpath "$path"); [[ "$resolved" == "$path" ]] || die "managed directory resolves outside its configured path: $path"; fi
  done
}
need_root() { [[ "$TEST_MODE" == 1 || $(id -u) == 0 ]] || die 'must run as root'; guard_managed_roots; }
safe_id() { [[ "$1" =~ ^[A-Za-z0-9._-]+$ ]] || die 'invalid release id'; }
current_id() { if [[ -f "$RELEASES/.current" ]]; then local id; id=$(cat "$RELEASES/.current"); safe_id "$id"; printf '%s' "$id"; else printf 'legacy'; fi; }
previous_id() { if [[ -f "$RELEASES/.previous" ]]; then local id; id=$(cat "$RELEASES/.previous"); [[ -z "$id" ]] && return 0; safe_id "$id"; printf '%s' "$id"; fi; }
tfm_of() { local f; f=$(find "${1%/}/" -maxdepth 1 -name '*.runtimeconfig.json' -type f -print -quit 2>/dev/null || true); [[ -n "$f" ]] || return 1; sed -nE 's/.*"tfm"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$f" | head -n1; }
active_environment() { systemctl show -p Environment -p EnvironmentFiles "$1" 2>/dev/null || true; }
env_value() { local data=$1 key=$2; printf '%s\n' "$data" | grep -oE "${key}=[^[:space:]]+" | tail -n1 | cut -d= -f2- | tr -d '"' || true; }
resolve_storage() {
  local web worker envfile webdir workerdir
  web=$(active_environment miautrix-mail)
  worker=$(active_environment miautrix-mail-worker)
  webdir=$(env_value "$web" MIAUTRIX_STORAGE_DIR)
  workerdir=$(env_value "$worker" MIAUTRIX_STORAGE_DIR)
  envfile="$ROOT/.env"
  if [[ -f "$envfile" ]]; then
    local fromfile
    fromfile=$(sed -nE 's/^MIAUTRIX_STORAGE_DIR=(.*)$/\1/p' "$envfile" | tail -n1)
    fromfile=${fromfile#\"}; fromfile=${fromfile%\"}; fromfile=${fromfile#\'}; fromfile=${fromfile%\'}
    [[ -n "$workerdir" ]] || workerdir=$fromfile
  fi
  [[ -n "$webdir" && -n "$workerdir" ]] || die 'MIAUTRIX_STORAGE_DIR must be explicit in the Web unit and worker environment/.env'
  [[ "$webdir" == /* && "$workerdir" == /* ]] || die 'MIAUTRIX_STORAGE_DIR must be an absolute path for both services'
  webdir=$(realpath -m "$webdir"); workerdir=$(realpath -m "$workerdir")
  [[ "$webdir" == "$workerdir" ]] || die 'Web and Worker MIAUTRIX_STORAGE_DIR values differ'
  [[ -d "$webdir" ]] || die "storage path does not exist: $webdir"
  for d in "$APP/mail_data" "$WORKER/mail_data"; do
    if [[ -d "$d" ]] && find "$d" -type f -print -quit | grep -q .; then
      die "mail files exist at $d; remediate with scripts/lxc-prepare-storage.sh --pin, then set MIAUTRIX_STORAGE_DIR in $envfile; data was not moved"
    fi
  done
  case "$webdir/" in "$APP/"*|"$WORKER/"*|"$CLI/"*|"$ADMIN/"*|"$WEBMAIL/"*|"$RELEASES/"*) die "storage path is inside a deploy-managed directory: $webdir";; esac
  printf '%s' "$webdir"
}
connection_value() {
  # Split Npgsql key/value pairs on unquoted semicolons; support single/double quoted values.
  local input=$1 wanted=$2 key value rest quote ch
  while [[ -n "$input" ]]; do
    key=${input%%=*}; [[ "$input" == *=* ]] || break; rest=${input#*=}; value=; quote=
    while [[ -n "$rest" ]]; do
      ch=${rest:0:1}; rest=${rest:1}
      if [[ -n "$quote" ]]; then
        if [[ "$ch" == "$quote" ]]; then quote=; else value+=$ch; fi
      elif [[ "$ch" == "'" || "$ch" == '"' ]]; then quote=$ch
      elif [[ "$ch" == ';' ]]; then break
      else value+=$ch
      fi
    done
    key=$(printf '%s' "$key" | tr '[:upper:]' '[:lower:]' | tr -d ' ')
    case "$key" in
      host|server) [[ "$wanted" == host ]] && { printf '%s' "$value"; return 0; };;
      port) [[ "$wanted" == port ]] && { printf '%s' "$value"; return 0; };;
      database) [[ "$wanted" == database ]] && { printf '%s' "$value"; return 0; };;
      username|userid|user_id) [[ "$wanted" == username ]] && { printf '%s' "$value"; return 0; };;
      password) [[ "$wanted" == password ]] && { printf '%s' "$value"; return 0; };;
    esac
    input=$rest
  done
  return 1
}
pgpass_escape() { local value=$1; value=${value//\\/\\\\}; value=${value//:/\\:}; printf '%s' "$value"; }
read_db_connection() {
  [[ -f "$ROOT/.env" ]] || die "missing $ROOT/.env"
  local line
  line=$(sed -nE 's/^MIAUTRIX_DB_CONNECTION=(.*)$/\1/p' "$ROOT/.env" | tail -n1)
  line=${line#\"}; line=${line%\"}; line=${line#\'}; line=${line%\'}
  [[ -n "$line" ]] || die 'MIAUTRIX_DB_CONNECTION is missing from .env'
  DB_CONNECTION=$line
  DB_HOST=$(connection_value "$line" host) || die 'DB Host missing from connection string'
  DB_PORT=$(connection_value "$line" port || printf '5432')
  DB_NAME=$(connection_value "$line" database) || die 'DB Database missing from connection string'
  DB_USER=$(connection_value "$line" username) || die 'DB Username missing from connection string'
  DB_PASSWORD=$(connection_value "$line" password) || die 'DB Password missing from connection string'
  [[ -n "$DB_HOST" && -n "$DB_NAME" && -n "$DB_USER" && -n "$DB_PASSWORD" ]] || die 'DB connection details are incomplete'
}
manifest_timestamp() { date -u +%Y%m%dT%H%M%SZ; }
backup_base() { printf '%s_%s' "$(manifest_timestamp)" "${1:-$(current_id)}"; }
record_current() { mkdir -p "$RELEASES"; printf '%s\n' "$1" > "$RELEASES/.current.tmp"; mv -f "$RELEASES/.current.tmp" "$RELEASES/.current"; }
point_link() {
  local name=$1 id=$2 temp="$ROOT/.${1}.link.$$"
  if [[ "$TEST_MODE" == 1 && ( "$OSTYPE" == msys* || "$OSTYPE" == cygwin* ) ]]; then
    printf 'MIAUTRIX_LINK:%s\n' "$RELEASES/$id/$name" > "$temp"
    mv -f "$temp" "$ROOT/$name"
    return
  fi
  ln -s "$RELEASES/$id/$name" "$temp"
  mv -Tf "$temp" "$ROOT/$name"
}
runtimeconfig_check() {
  local directory=$1 expected=${2:-net10.0} tfm marker
  if [[ "$TEST_MODE" == 1 && -f "$directory" ]]; then marker=$(cat "$directory"); [[ "$marker" == MIAUTRIX_LINK:* ]] && directory=${marker#MIAUTRIX_LINK:}; fi
  tfm=$(tfm_of "$directory") || return 1
  [[ "$tfm" == "$expected" ]]
}

release_tfm() {
  local id=$1 metadata="$RELEASES/$1/RELEASE.json" tfm
  if [[ "$id" == legacy-* ]]; then tfm=$(tfm_of "$RELEASES/$id/app") || return 1
  elif [[ -f "$metadata" ]]; then tfm=$(sed -nE 's/.*"tfm"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$metadata" | head -n1)
  else tfm=$(tfm_of "$RELEASES/$id/app") || return 1
  fi
  [[ -n "$tfm" ]] || return 1
  printf '%s' "$tfm"
}

cmd_preflight() {
  need_root; local release=$1 upload=${2:-} path total current_size=0 item tfm
  safe_id "$release"
  local missing=()
  for t in tar systemctl ss pg_dump pg_restore curl openssl timeout nginx; do command -v "$t" >/dev/null 2>&1 || missing+=("$t"); done
  ((${#missing[@]} == 0)) || {
    printf 'Missing required tools: %s\n' "${missing[*]}" >&2
    local -a packages=()
    for t in "${missing[@]}"; do case "$t" in
      tar) packages+=(tar);;
      systemctl) packages+=(systemd);;
      ss) packages+=(iproute2);;
      pg_dump|pg_restore)
        if command -v psql >/dev/null 2>&1; then major=$(psql --version | awk '{print $3}' | cut -d. -f1); elif command -v pg_config >/dev/null 2>&1; then major=$(pg_config --version | awk '{print $2}' | cut -d. -f1); else major='<server-major>'; fi
        packages+=("postgresql-client-$major");;
      curl) packages+=(curl);;
      openssl) packages+=(openssl);;
      timeout) packages+=(coreutils);;
      nginx) packages+=(nginx);;
    esac; done
    printf 'Install missing tools with: apt install %s\n' "$(printf '%s\n' "${packages[@]}" | sort -u | tr '\n' ' ')" >&2
    return 1
  }
  path=$(resolve_storage)
  if [[ -n "$upload" && ! -f "$RELEASES/.current" ]]; then
    local component rel candidate name sensitive absent=()
    for component in app worker; do
      [[ -d "$ROOT/$component" ]] || continue
      while IFS= read -r candidate; do
        rel=${candidate#"$ROOT/$component"/}
        name=${candidate##*/}; sensitive=false
        case "$name" in appsettings.*.json|*.pem|*.key|*.pfx|*.crt|*.env|logs) sensitive=true;; esac
        case "$candidate" in */logs/*|*/dataprotection*|*/DataProtection*|*/data-protection*|*/dataProtection*|*/dataprotection*/*|*/DataProtection*/*|*/data-protection*/*|*/dataProtection*/*) sensitive=true;; esac
        [[ "$sensitive" == true ]] && [[ ! -e "$upload/$component/$rel" ]] && absent+=("$component/$rel")
      done < <(find "$ROOT/$component/" -mindepth 1 \( -type f -o -type d \) -print)
    done
    if ((${#absent[@]})); then printf 'Server-local files absent from uploaded payload:\n' >&2; printf '  %s\n' "${absent[@]}" >&2; die 'move server-local configuration/data outside deploy directories (for example /opt/miautrix-mail/config or /etc/miautrix) and reference it via environment variables'; fi
  fi
  local env_data key value resolved service
  for service in miautrix-mail miautrix-mail-worker; do
    env_data=$(active_environment "$service")
    while IFS= read -r token; do
      key=${token%%=*}; value=${token#*=}; value=${value#\"}; value=${value%\"}
      [[ "$key" == MIAUTRIX_STORAGE_DIR || "$key" == MIAUTRIX_TLS_CERT_PATH || "$key" == MIAUTRIX_TLS_KEY_PATH || "$key" == *_PATH || "$key" == *_DIR || "$key" == *_FILE ]] || continue
      [[ "$value" == /* ]] || continue
      resolved=$(realpath -m "$value")
      case "$resolved/" in "$APP/"*|"$WORKER/"*|"$CLI/"*|"$ADMIN/"*|"$WEBMAIL/"*|"$RELEASES/"*) die "path-valued environment $key resolves inside a deploy-managed directory: $resolved";; esac
    done < <(printf '%s\n' "$env_data" | grep -oE '[A-Za-z_][A-Za-z0-9_]*=[^[:space:]]+' || true)
  done
  if [[ -f "$ROOT/.env" ]]; then
    while IFS='=' read -r key value; do
      [[ "$key" == MIAUTRIX_STORAGE_DIR || "$key" == MIAUTRIX_TLS_CERT_PATH || "$key" == MIAUTRIX_TLS_KEY_PATH || "$key" == *_PATH || "$key" == *_DIR || "$key" == *_FILE ]] || continue
      value=${value#\"}; value=${value%\"}; value=${value#\'}; value=${value%\'}; [[ "$value" == /* ]] || continue
      resolved=$(realpath -m "$value")
      case "$resolved/" in "$APP/"*|"$WORKER/"*|"$CLI/"*|"$ADMIN/"*|"$WEBMAIL/"*|"$RELEASES/"*) die "path-valued environment $key resolves inside a deploy-managed directory: $resolved";; esac
    done < "$ROOT/.env"
  fi
  [[ -d "$APP" && -d "$WORKER" ]] || die 'legacy app and worker directories are required for size/runtime checks'
  for d in "$APP" "$WORKER" "$CLI" "$ADMIN" "$WEBMAIL" "$RELEASES"; do [[ -e "$d" ]] && current_size=$((current_size + $(du -sb "$d" | awk '{print $1}'))); done
  total=$(df -PB1 "$ROOT" | awk 'NR==2 {print $4}')
  (( total >= current_size * 2 + 1073741824 )) || die 'free disk must be at least 2x current install size plus 1 GiB'
  tfm=$(tfm_of "$APP") || tfm=unknown
  summary "preflight $release passed; storage=$path; layout=$(for d in app worker cli admin webmail; do [[ -L "$ROOT/$d" ]] && printf '%s=symlink ' "$d" || printf '%s=directory ' "$d"; done); active=$(current_id); runtime=$tfm"
  for service in miautrix-mail miautrix-mail-worker; do systemctl is-active "$service" || true; done
  if [[ -d "$RELEASES" ]]; then find "$RELEASES" -mindepth 1 -maxdepth 1 -type d -exec du -sh {} + 2>/dev/null || true; fi
}

cmd_backup() {
  need_root; local release=$1 id base dump config tmp passfile sha size
  safe_id "$release"; id=$(current_id); base=$(backup_base "$id"); mkdir -p "$BACKUPS"; chmod 700 "$BACKUPS"
  read_db_connection
  tmp=$(mktemp); passfile=$(mktemp); trap 'rm -f "${tmp:-}" "${passfile:-}"' EXIT
  chmod 600 "$passfile"; printf '%s:%s:%s:%s:%s\n' "$(pgpass_escape "$DB_HOST")" "$DB_PORT" "$(pgpass_escape "$DB_NAME")" "$(pgpass_escape "$DB_USER")" "$(pgpass_escape "$DB_PASSWORD")" > "$passfile"
  unset DB_PASSWORD
  PGPASSFILE="$passfile" pg_dump -Fc --no-owner --host="$DB_HOST" --port="$DB_PORT" --username="$DB_USER" --dbname="$DB_NAME" --file="$tmp"
  pg_restore --list "$tmp" >/dev/null
  dump="$BACKUPS/$base.dump"
  if [[ "$TEST_MODE" == 1 ]]; then install -m 600 "$tmp" "$dump"; else install -o root -g root -m 600 "$tmp" "$dump"; fi
  config="$BACKUPS/$base.config.tar.gz"
  local -a config_files=("$ROOT/.env")
  for service in miautrix-mail miautrix-mail-worker; do
    while IFS='=' read -r key value; do
      case "$key" in
        FragmentPath) [[ -f "$value" ]] && config_files+=("$value");;
        DropInPaths) for f in $value; do [[ -f "$f" ]] && config_files+=("$f"); done;;
      esac
    done < <(systemctl show -p FragmentPath -p DropInPaths "$service")
  done
  while IFS= read -r f; do config_files+=("$f"); done < <(find /etc/systemd/system -maxdepth 3 -type f \( -name 'miautrix-mail.service' -o -path '*/miautrix-mail.service.d/*' -o -name 'miautrix-mail-worker.service' -o -path '*/miautrix-mail-worker.service.d/*' \) -print 2>/dev/null)
  while IFS= read -r f; do config_files+=("$f"); done < <(grep -rl '/opt/miautrix-mail' /etc/nginx/sites-available /etc/nginx/sites-enabled 2>/dev/null || true)
  tar -czf "$config" --absolute-names "${config_files[@]}"; [[ "$TEST_MODE" == 1 ]] || chown root:root "$config"; chmod 600 "$config"
  size=$(stat -c %s "$dump"); sha=$(sha256sum "$dump" | awk '{print $1}')
  printf 'release_id=%s\nprevious_release=%s\ndump=%s\nsize=%s\nsha256=%s\nlegacy_conversion_pending=%s\n' "$release" "$id" "$dump" "$size" "$sha" "$([[ -f "$RELEASES/.current" ]] && echo no || echo yes)" > "$BACKUPS/$base.manifest"
  [[ "$TEST_MODE" == 1 ]] || chown root:root "$BACKUPS/$base.manifest"; chmod 600 "$BACKUPS/$base.manifest"
  summary "database dump verified: $dump ($size bytes, sha256 $sha); config snapshot: $config"
}

cmd_stage() {
  need_root; local release=$1 upload=$2 target="$RELEASES/$1"; safe_id "$release"
  [[ -d "$upload" ]] || die 'upload directory does not exist'
  [[ -f "$upload/app/Miautrix.Mail.Web" && -f "$upload/worker/Miautrix.Mail.Worker" && -f "$upload/cli/Miautrix.Mail.Cli" ]] || die 'payload lacks Web, Worker, or CLI executable'
  [[ -f "$upload/admin/index.html" && -f "$upload/webmail/index.html" ]] || die 'payload lacks admin or webmail index.html'
  runtimeconfig_check "$upload/app" || die 'Web runtimeconfig must target net10.0'
  runtimeconfig_check "$upload/worker" || die 'Worker runtimeconfig must target net10.0'
  runtimeconfig_check "$upload/cli" || die 'CLI runtimeconfig must target net10.0'
  [[ ! -e "$target" ]] || die "release already exists: $target"
  local metadata_id metadata_sha metadata_built
  metadata_id=$(sed -nE 's/.*"id"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$upload/RELEASE.json" 2>/dev/null | head -n1 || true)
  metadata_sha=$(sed -nE 's/.*"gitSha"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$upload/RELEASE.json" 2>/dev/null | head -n1 || true)
  metadata_built=$(sed -nE 's/.*"builtAtUtc"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$upload/RELEASE.json" 2>/dev/null | head -n1 || true)
  [[ "$metadata_id" == "$release" && -n "$metadata_sha" && -n "$metadata_built" ]] || die 'payload RELEASE.json is missing or mismatched'
  mkdir -p "$RELEASES"; mv "$upload" "$target"; [[ "$TEST_MODE" == 1 ]] || chown -R www-data:www-data "$target"
  chmod +x "$target/app/Miautrix.Mail.Web" "$target/worker/Miautrix.Mail.Worker" "$target/cli/Miautrix.Mail.Cli"
  printf '{"id":"%s","gitSha":"%s","builtAtUtc":"%s","tfm":"net10.0"}\n' "$release" "$metadata_sha" "$metadata_built" > "$target/RELEASE.json"
  [[ "$TEST_MODE" == 1 ]] || chown www-data:www-data "$target/RELEASE.json"; summary "staged $release at $target"
}

cmd_migrate() {
  need_root; [[ ${1:-} == --bundle && -n ${2:-} ]] || die 'migrate requires --bundle FILE'
  local bundle; bundle=$(realpath "$2")
  [[ -f "$bundle" ]] || die 'migration bundle does not exist'
  chmod +x "$bundle"
  [[ -x "$bundle" ]] || die 'migration bundle is not executable'
  read_db_connection
  export MIAUTRIX_DB_CONNECTION="$DB_CONNECTION"
  unset DB_PASSWORD
  summary 'running the published migration bundle with the server environment connection'
  exec "$bundle"
}

cmd_switch() {
  need_root; local release=$1 old=$2; safe_id "$release"; [[ -d "$RELEASES/$release" ]] || die 'release is not staged'
  trap 'rc=$?; trap - ERR; printf "ERROR: switch failed (current=%s previous=%s); inspect systemd and symlink state before retrying\n" "$(current_id)" "$(previous_id)" >&2; exit "$rc"' ERR
  systemctl stop miautrix-mail-worker; systemctl stop miautrix-mail
  if [[ ! -f "$RELEASES/.current" ]]; then
    local legacy="legacy-$(tfm_of "$APP" 2>/dev/null || echo unknown)-$(manifest_timestamp)"
    mkdir -p "$RELEASES/$legacy"
    for d in app worker admin webmail; do [[ -e "$ROOT/$d" && ! -L "$ROOT/$d" ]] && mv "$ROOT/$d" "$RELEASES/$legacy/$d"; done
    [[ -e "$ROOT/cli" && ! -L "$ROOT/cli" ]] && mv "$ROOT/cli" "$RELEASES/$legacy/cli"
    mkdir -p "$RELEASES/$legacy/cli"
    printf '%s\n' "$legacy" > "$RELEASES/.previous"
    while IFS= read -r manifest; do printf 'legacy_release=%s\n' "$legacy" >> "$manifest"; done < <(grep -rl "^release_id=$release$" "$BACKUPS" --include='*.manifest' 2>/dev/null || true)
  else
    old=$(current_id); printf '%s\n' "$old" > "$RELEASES/.previous"
  fi
  for d in app worker cli admin webmail; do point_link "$d" "$release"; done
  record_current "$release"
  systemctl daemon-reload; systemctl start miautrix-mail; systemctl start miautrix-mail-worker
  if command -v nginx >/dev/null 2>&1; then nginx -t; systemctl reload nginx; fi
  rm -f "$RELEASES/.verified"; trap - ERR
  summary "switched active release to $release; previous=${old:-$(previous_id)}"
}

# Kestrel is serving if it answers any HTTP status below 500. The health URL
# requires authentication, so 401/403 are expected and still prove liveness.
http_alive() { local code; code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 5 "$1" 2>/dev/null || true); [[ "$code" =~ ^[1-4][0-9][0-9]$ ]]; }

cmd_verify() {
  need_root; local release expected_tfm current=0 appinfo greeting urls health attempts=60
  [[ "$TEST_MODE" == 1 ]] && attempts=${MIAUTRIX_VERIFY_ATTEMPTS:-2}
  release=$(current_id)
  expected_tfm=$(release_tfm "$release") || die "cannot determine active release TFM for $release"
  appinfo=$(systemctl show -p Environment -p ExecStart miautrix-mail)
  urls=$(printf '%s' "$appinfo" | grep -oE 'ASPNETCORE_URLS=[^[:space:]"]+' | tail -n1 | cut -d= -f2- || true)
  if [[ -z "$urls" ]]; then urls=$(printf '%s' "$appinfo" | grep -oE -- '--urls[= ][^[:space:]]+' | tail -n1 | sed -E 's/^--urls[= ]+//' || true); fi
  if [[ -z "$urls" && -f "$ROOT/.env" ]]; then urls=$(sed -nE 's/^ASPNETCORE_URLS=(.*)$/\1/p' "$ROOT/.env" | tail -n1 | tr -d '"'); fi
  [[ -n "$urls" ]] || die 'cannot determine Kestrel URL from systemd unit/environment'
  health="${urls%%,*}"; health="${health%/}/api/v1/system/info"
  [[ "$health" == http://* || "$health" == https://* ]] || die 'configured Kestrel URL must be http(s)'
  for ((i=0;i<attempts;i++)); do
    if systemctl is-active --quiet miautrix-mail && systemctl is-active --quiet miautrix-mail-worker && \
       runtimeconfig_check "$APP" "$expected_tfm" && \
       { ss -ltn 2>/dev/null | awk '{print $4}' | grep -Eq ':25$' && ss -ltn 2>/dev/null | awk '{print $4}' | grep -Eq ':465$' && ss -ltn 2>/dev/null | awk '{print $4}' | grep -Eq ':587$' && ss -ltn 2>/dev/null | awk '{print $4}' | grep -Eq ':993$'; } && \
       http_alive "$health" && \
       greeting=$(timeout 5 openssl s_client -connect 127.0.0.1:993 -quiet < /dev/null 2>/dev/null | head -n1 || true) && [[ "$greeting" == '* OK [CAPABILITY IMAP4rev1'* ]]; then current=1; break; fi
    sleep 1
  done
  if (( current != 1 )); then
    local p listening
    listening=$(ss -ltn 2>/dev/null | awk '{print $4}' || true)
    printf 'verify diagnostics for %s:\n' "$release" >&2
    printf '  unit miautrix-mail: %s\n' "$(systemctl is-active miautrix-mail 2>/dev/null || true)" >&2
    printf '  unit miautrix-mail-worker: %s\n' "$(systemctl is-active miautrix-mail-worker 2>/dev/null || true)" >&2
    printf '  runtimeconfig %s: %s\n' "$expected_tfm" "$(runtimeconfig_check "$APP" "$expected_tfm" && echo ok || echo FAIL)" >&2
    for p in 25 465 587 993; do printf '  port %s: %s\n' "$p" "$(grep -Eq ":$p\$" <<<"$listening" && echo listening || echo NOT-LISTENING)" >&2; done
    printf '  http %s: status %s\n' "$health" "$(curl -sS -o /dev/null -w '%{http_code}' --max-time 5 "$health" 2>/dev/null || echo none)" >&2
    printf '  imaps greeting: %s\n' "$(timeout 5 openssl s_client -connect 127.0.0.1:993 -quiet < /dev/null 2>/dev/null | head -n1 | tr -d '\r' || true)" >&2
    die "services, listener ports, health endpoint, $expected_tfm runtimeconfig, or IMAPS greeting failed within 60 seconds"
  fi
  printf '%s\n' "$release" > "$RELEASES/.verified"; summary "verified release $release (units, ports, health, $expected_tfm, IMAPS greeting)"
}

cmd_rollback() {
  need_root; local restore_db=${1:-false} previous dump='' answer old
  previous=$(previous_id); [[ -n "$previous" && -d "$RELEASES/$previous" ]] || die 'no previous release available'
  old=$(current_id); if [[ -d "$RELEASES/$old" ]]; then printf '%s\n' "$old" > "$RELEASES/.previous"; elif [[ -f "$RELEASES/.previous" ]] && [[ ! -d "$RELEASES/$(cat "$RELEASES/.previous")" ]]; then rm -f "$RELEASES/.previous"; fi
  for d in app worker cli admin webmail; do point_link "$d" "$previous"; done
  record_current "$previous"
  systemctl daemon-reload; systemctl restart miautrix-mail; systemctl restart miautrix-mail-worker
  summary "rolled back to $previous; database was not restored"
  read_db_connection; unset DB_PASSWORD
  dump=$(find "$BACKUPS" -maxdepth 1 -name '*.dump' -type f -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -n1 | cut -d' ' -f2- || true)
  [[ -n "$dump" ]] && printf 'To restore this deploy backup manually: pg_restore --clean --if-exists --no-owner --dbname="%s" "%s"\n' "$DB_NAME" "$dump"
  if [[ "$restore_db" == true ]]; then
    [[ -n "$dump" ]] || die 'no deploy database dump found'
    printf 'Type RESTORE DATABASE to confirm destructive restore: '; read -r answer
    [[ "$answer" == 'RESTORE DATABASE' ]] || die 'confirmation did not match'
    read_db_connection
    local passfile; passfile=$(mktemp); chmod 600 "$passfile"; trap 'rm -f "${passfile:-}"' EXIT
    printf '%s:%s:%s:%s:%s\n' "$(pgpass_escape "$DB_HOST")" "$DB_PORT" "$(pgpass_escape "$DB_NAME")" "$(pgpass_escape "$DB_USER")" "$(pgpass_escape "$DB_PASSWORD")" > "$passfile"
    unset DB_PASSWORD
    PGPASSFILE="$passfile" pg_restore --clean --if-exists --no-owner --host="$DB_HOST" --port="$DB_PORT" --username="$DB_USER" --dbname="$DB_NAME" "$dump"
    rm -f "$passfile"
  fi
  cmd_verify
}

cmd_cleanup() {
  need_root; local keep=2 dry=false; while (($#)); do case "$1" in --keep) keep=$2; shift 2;; --dry-run) dry=true; shift;; *) die "unknown cleanup option $1";; esac; done
  [[ "$keep" =~ ^[0-9]+$ ]] && (( keep >= 2 )) || die '--keep must be an integer of at least 2'
  [[ -f "$RELEASES/.verified" && "$(cat "$RELEASES/.verified")" == "$(current_id)" ]] || die 'cleanup refused: active release has not verified'
  local current previous resolved bytes_before bytes_after; current=$(current_id); previous=$(previous_id); bytes_before=$(du -sb "$RELEASES" "$BACKUPS" 2>/dev/null | awk '{s+=$1} END{print s+0}')
  local -a dirs=(); while IFS= read -r d; do dirs+=("$d"); done < <(find "$RELEASES" -mindepth 1 -maxdepth 1 -type d -print | sort)
  for d in "${dirs[@]}"; do
    [[ "$d" == "$RELEASES/$current" || "$d" == "$RELEASES/$previous" ]] && continue
    resolved=$(realpath "$d"); [[ "$resolved" == "$RELEASES/"* ]] || die "refusing to remove path outside releases/: $resolved"
    summary "remove release $resolved"
    [[ "$dry" == true ]] || rm -rf -- "$resolved"
  done
  # Keep dump/config/manifest sets for the two newest timestamped deploys.
  local -a retained_bases=() manifests=(); while IFS= read -r f; do retained_bases+=("${f%.manifest}"); done < <(find "$BACKUPS" -maxdepth 1 -name '*.manifest' -type f -printf '%T@ %p\n' | sort -nr | head -n "$keep" | cut -d' ' -f2-)
  while IFS= read -r f; do manifests+=("$f"); done < <(find "$BACKUPS" -maxdepth 1 -name '*.manifest' -type f -printf '%T@ %p\n' | sort -nr | tail -n +$((keep + 1)) | cut -d' ' -f2-)
  for f in "${manifests[@]}"; do base=${f%.manifest}; for x in "$f" "$base.dump" "$base.config.tar.gz"; do [[ -f "$x" ]] || continue; summary "remove backup $x"; [[ "$dry" == true ]] || rm -f -- "$x"; done; done
  for f in "$BACKUPS"/20*_*.dump "$BACKUPS"/20*_*.config.tar.gz; do [[ -f "$f" ]] || continue; base=${f%.dump}; base=${base%.config.tar.gz}; retained=false; for kept in "${retained_bases[@]}"; do [[ "$base" == "$kept" ]] && retained=true; done; if [[ "$retained" == false ]]; then summary "remove unretained backup $f"; [[ "$dry" == true ]] || rm -f -- "$f"; fi; done
  bytes_after=$(du -sb "$RELEASES" "$BACKUPS" 2>/dev/null | awk '{s+=$1} END{print s+0}'); summary "cleanup complete; freed $((bytes_before - bytes_after)) bytes"
}

cmd_status() {
  guard_managed_roots
  summary "current=$(current_id) previous=$(previous_id)"
  [[ -d "$RELEASES" ]] && find "$RELEASES" -mindepth 1 -maxdepth 1 -type d -exec du -sh {} + 2>/dev/null || true
  [[ -d "$BACKUPS" ]] && find "$BACKUPS" -maxdepth 1 -type f -exec du -sh {} + 2>/dev/null || true
}

usage() { printf 'Usage: %s {preflight|backup|stage|migrate|switch|verify|rollback|cleanup|status} [options]\n' "$0"; }
command=${1:-}; shift || true
case "$command" in
  preflight) [[ ${1:-} == --release-id && -n ${2:-} ]] || die 'preflight requires --release-id'; release=$2; shift 2; upload=; while (($#)); do [[ $1 == --upload && -n ${2:-} ]] || die 'preflight accepts --upload DIR'; upload=$2; shift 2; done; cmd_preflight "$release" "$upload";;
  backup) [[ ${1:-} == --release-id ]] || die 'backup requires --release-id'; cmd_backup "${2:-}";;
  stage) [[ ${1:-} == --release-id && ${3:-} == --upload ]] || die 'stage requires --release-id ID --upload DIR'; cmd_stage "$2" "$4";;
  migrate) cmd_migrate "$@";;
  switch) [[ ${1:-} == --release-id ]] || die 'switch requires --release-id'; cmd_switch "$2" "$(current_id)";;
  verify) cmd_verify;;
  rollback) if [[ ${1:-} == --restore-db ]]; then cmd_rollback true; elif [[ -z ${1:-} ]]; then cmd_rollback false; else die 'rollback accepts only --restore-db'; fi;;
  cleanup) cmd_cleanup "$@";;
  status) cmd_status;;
  *) usage; exit 2;;
esac
````

### `scripts/lxc-prepare-storage.sh`

SHA-256: `5ffd7bd70734509d08aeefe739b23a02919b8b08bd666a677faf8a4bd8ef5739`

``bash
#!/usr/bin/env bash
# Prepare the blob store used by the mailbox export / delete / assign endpoints.
#
# The API resolves its content-addressed store from MIAUTRIX_STORAGE_DIR and falls back to
# <AppContext.BaseDirectory>/mail_data. On the container that fallback is
# /opt/miautrix-mail/app/mail_data — *inside* the deploy target, which is fine as long as it
# is never wiped, but it is safer to pin it to the persistent /opt/miautrix-mail/data path.
#
#   ./lxc-prepare-storage.sh          # report only; change nothing
#   ./lxc-prepare-storage.sh --pin    # move blobs to the persistent path and pin the env var
#
# Idempotent. Never deletes a blob.

set -euo pipefail

APP_DIR=/opt/miautrix-mail/app
LEGACY_DIR="$APP_DIR/mail_data"
PERSISTENT_DIR=/opt/miautrix-mail/data/mail
DROP_IN_DIR=/etc/systemd/system/miautrix-mail.service.d
DROP_IN="$DROP_IN_DIR/storage.conf"
SERVICE=miautrix-mail

PIN=0
[[ "${1:-}" == "--pin" ]] && PIN=1

count_blobs() {
    local dir="$1"
    [[ -d "$dir" ]] || { echo 0; return; }
    find "$dir" -type f 2>/dev/null | wc -l | tr -d ' '
}

current_env_dir() {
    local unit
    unit=$(systemctl show -p Environment --value "$SERVICE" 2>/dev/null || true)
    [[ -n "$unit" ]] || { echo ""; return; }
    printf '%s\n' "$unit" \
        | tr ' ' '\n' \
        | sed -n 's/^MIAUTRIX_STORAGE_DIR=//p' \
        | tr -d '"' | head -n1 || echo ""
}

effective_dir() {
    local configured
    configured=$(current_env_dir)
    if [[ -n "$configured" ]]; then echo "$configured"; else echo "$LEGACY_DIR"; fi
}

echo "==> Blob store status"
printf '    legacy   (%s): %s file(s)\n' "$LEGACY_DIR" "$(count_blobs "$LEGACY_DIR")"
printf '    pinned   (%s): %s file(s)\n' "$PERSISTENT_DIR" "$(count_blobs "$PERSISTENT_DIR")"
printf '    effective: %s\n' "$(effective_dir)"

if [[ "$PIN" -eq 0 ]]; then
    echo "    (report only — re-run with --pin to move blobs to the persistent path)"
    exit 0
fi

# 1) Create the persistent path. Ownership must match the service user.
install -d -o www-data -g www-data -m 0750 "$PERSISTENT_DIR"

# 2) Copy any existing blobs across exactly once. Only copy into an empty target so a
#    re-run can never overwrite newer content with older content.
persistent_count=$(count_blobs "$PERSISTENT_DIR")
legacy_count=$(count_blobs "$LEGACY_DIR")

if [[ "$persistent_count" -eq 0 && "$legacy_count" -gt 0 ]]; then
    echo "==> Copying $legacy_count blob(s): $LEGACY_DIR -> $PERSISTENT_DIR"
    cp -a "$LEGACY_DIR/." "$PERSISTENT_DIR/"
    chown -R www-data:www-data "$PERSISTENT_DIR"
elif [[ "$persistent_count" -gt 0 && "$legacy_count" -gt 0 ]]; then
    echo "==> Both directories hold blobs; leaving them untouched." >&2
    echo "    Verify which one is authoritative before pinning." >&2
    exit 1
fi

# 3) Pin the env var via a drop-in so the app and the IMAP/SMTP path agree.
install -d "$DROP_IN_DIR"
cat > "$DROP_IN" <<EOF
[Service]
Environment=MIAUTRIX_STORAGE_DIR=$PERSISTENT_DIR
EOF

systemctl daemon-reload
echo "==> Pinned MIAUTRIX_STORAGE_DIR=$PERSISTENT_DIR in $DROP_IN"
echo "==> The legacy directory is left in place as a backup; remove it once exports look correct."
````

### `scripts/cleanup-test-tenants.sh`

SHA-256: `f227aeb8d2d98838d8a329044748e13ad45ea81b68607458f4160fd4dcb31116`

``bash
#!/usr/bin/env bash
# Remove test-fixture tenants that automated tests left in a database.
#
# Usage (on the app server, as root):
#   bash cleanup-test-tenants.sh --list                      # review + rehearsal, always ROLLBACK
#   bash cleanup-test-tenants.sh --apply --expect <count>    # pg_dump first, then delete and COMMIT
#
# A tenant is a candidate only when its slug matches a test-fixture pattern
# (prefix followed by a GUID, or the fixed fixture slugs 'raw-test' / 'acme')
# and it is not listed in --keep (the production tenant is always kept).
# Deletion is generic: every table with a tenant_id column, plus child tables
# without tenant_id that reference those rows through a single-column FK, are
# cleared in FK-safe order inside ONE transaction. Any failure rolls back all of it.
set -Eeuo pipefail

ROOT=${MIAUTRIX_ROOT:-/opt/miautrix-mail}
BACKUPS="$ROOT/backups"
KEEP='c1b615a2-b437-b25d-8aed-61bb68749c8b'
MODE=list
EXPECT=

die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
usage() { sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }

while (($#)); do
  case "$1" in
    --list) MODE=list; shift;;
    --apply) MODE=apply; shift;;
    --expect) [[ -n ${2:-} ]] || die '--expect needs a number'; EXPECT=$2; shift 2;;
    --keep) [[ -n ${2:-} ]] || die '--keep needs a tenant id'; KEEP="$KEEP,$2"; shift 2;;
    -h|--help) usage;;
    *) die "unknown option $1";;
  esac
done
[[ "$MODE" == list || "$EXPECT" =~ ^[0-9]+$ ]] || die '--apply requires --expect <count> (the candidate count shown by --list)'
for t in psql pg_dump pg_restore; do command -v "$t" >/dev/null || die "$t is required (apt install postgresql-client-18)"; done
[[ $EUID -eq 0 ]] || die 'run as root'

connection_value() {
  local input=$1 wanted=$2 key value rest quote ch
  while [[ -n "$input" ]]; do
    key=${input%%=*}; [[ "$input" == *=* ]] || break; rest=${input#*=}; value=; quote=
    while [[ -n "$rest" ]]; do
      ch=${rest:0:1}; rest=${rest:1}
      if [[ -n "$quote" ]]; then if [[ "$ch" == "$quote" ]]; then quote=; else value+=$ch; fi
      elif [[ "$ch" == "'" || "$ch" == '"' ]]; then quote=$ch
      elif [[ "$ch" == ';' ]]; then break
      else value+=$ch; fi
    done
    key=$(printf '%s' "$key" | tr '[:upper:]' '[:lower:]' | tr -d ' ')
    case "$key" in
      host|server) [[ "$wanted" == host ]] && { printf '%s' "$value"; return 0; };;
      port) [[ "$wanted" == port ]] && { printf '%s' "$value"; return 0; };;
      database) [[ "$wanted" == database ]] && { printf '%s' "$value"; return 0; };;
      username|userid|user_id) [[ "$wanted" == username ]] && { printf '%s' "$value"; return 0; };;
      password) [[ "$wanted" == password ]] && { printf '%s' "$value"; return 0; };;
    esac
    input=$rest
  done
  return 1
}
pgpass_escape() { local v=$1; v=${v//\\/\\\\}; v=${v//:/\\:}; printf '%s' "$v"; }

[[ -f "$ROOT/.env" ]] || die "missing $ROOT/.env"
line=$(sed -nE 's/^MIAUTRIX_DB_CONNECTION=(.*)$/\1/p' "$ROOT/.env" | tail -n1)
line=${line#\"}; line=${line%\"}; line=${line#\'}; line=${line%\'}
[[ -n "$line" ]] || die 'MIAUTRIX_DB_CONNECTION is missing from .env'
DB_HOST=$(connection_value "$line" host) || die 'Host missing'
DB_PORT=$(connection_value "$line" port || printf '5432')
DB_NAME=$(connection_value "$line" database) || die 'Database missing'
DB_USER=$(connection_value "$line" username) || die 'Username missing'
DB_PASSWORD=$(connection_value "$line" password) || die 'Password missing'
unset line

passfile=$(mktemp); trap 'rm -f "$passfile"' EXIT; chmod 600 "$passfile"
printf '%s:%s:%s:%s:%s\n' "$(pgpass_escape "$DB_HOST")" "$DB_PORT" "$(pgpass_escape "$DB_NAME")" "$(pgpass_escape "$DB_USER")" "$(pgpass_escape "$DB_PASSWORD")" > "$passfile"
unset DB_PASSWORD
export PGPASSFILE=$passfile PGHOST=$DB_HOST PGPORT=$DB_PORT PGDATABASE=$DB_NAME PGUSER=$DB_USER

if [[ "$MODE" == apply ]]; then
  mkdir -p "$BACKUPS"; chmod 700 "$BACKUPS"
  dump="$BACKUPS/$(date -u +%Y%m%dT%H%M%SZ)_before-tenant-cleanup.dump"
  printf '== taking database dump %s ==\n' "$dump"
  pg_dump -Fc --no-owner --file="$dump"
  pg_restore --list "$dump" >/dev/null
  chmod 600 "$dump"
  printf '== dump verified (%s bytes) ==\n' "$(stat -c %s "$dump")"
fi

psql -X -q -v ON_ERROR_STOP=1 -v keep="$KEEP" -v expect="${EXPECT:-0}" -v apply="$([[ $MODE == apply ]] && echo true || echo false)" <<'SQL'
\pset footer off
BEGIN;
CREATE TEMP TABLE doomed (id uuid PRIMARY KEY) ON COMMIT DROP;
INSERT INTO doomed
SELECT t.id FROM tenants t
WHERE t.id <> ALL (string_to_array(:'keep', ',')::uuid[])
  AND (t.slug ~ '-[0-9a-f]{32}$'
       OR t.slug ~ '-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR t.slug IN ('raw-test', 'acme'));

\echo
\echo '== tenants KEPT (not touched) =='
SELECT t.id, t.slug, t.created_at::date AS created,
       (SELECT count(*) FROM mailboxes x WHERE x.tenant_id = t.id) AS mailboxes,
       (SELECT count(*) FROM messages x WHERE x.tenant_id = t.id) AS messages,
       (SELECT string_agg(d.name, ', ') FROM domains d WHERE d.tenant_id = t.id) AS domains
FROM tenants t WHERE t.id NOT IN (SELECT id FROM doomed) ORDER BY t.created_at;

\echo '== candidate test tenants by slug prefix =='
SELECT regexp_replace(t.slug, '-[0-9a-f-]{32,36}$', '-<guid>') AS slug_pattern, count(*) AS tenants,
       min(t.created_at)::date AS first, max(t.created_at)::date AS last,
       sum((SELECT count(*) FROM messages x WHERE x.tenant_id = t.id)) AS messages,
       sum((SELECT count(*) FROM mailboxes x WHERE x.tenant_id = t.id)) AS mailboxes
FROM tenants t JOIN doomed USING (id) GROUP BY 1 ORDER BY 2 DESC;

SELECT count(*) AS candidate_count FROM doomed \gset
\echo '== candidate tenants:' :candidate_count '=='

DO $$
DECLARE r record; pass int; n bigint; blocked int; progress boolean; total bigint := 0;
BEGIN
  FOR pass IN 1..25 LOOP
    progress := false; blocked := 0;
    -- Child tables without tenant_id that reference tenant-scoped rows (single-column FKs).
    FOR r IN
      SELECT c.conrelid::regclass AS child, a.attname AS col, c.confrelid::regclass AS parent, pa.attname AS pcol
      FROM pg_constraint c
      JOIN pg_attribute a  ON a.attrelid = c.conrelid  AND a.attnum = c.conkey[1]
      JOIN pg_attribute pa ON pa.attrelid = c.confrelid AND pa.attnum = c.confkey[1]
      WHERE c.contype = 'f' AND array_length(c.conkey, 1) = 1
        AND c.connamespace = 'public'::regnamespace
        AND NOT EXISTS (SELECT 1 FROM pg_attribute x WHERE x.attrelid = c.conrelid AND x.attname = 'tenant_id' AND NOT x.attisdropped)
        AND (c.confrelid = 'tenants'::regclass
             OR EXISTS (SELECT 1 FROM pg_attribute x WHERE x.attrelid = c.confrelid AND x.attname = 'tenant_id' AND NOT x.attisdropped))
    LOOP
      BEGIN
        IF r.parent = 'tenants'::regclass THEN
          EXECUTE format('DELETE FROM %s WHERE %I IN (SELECT id FROM doomed)', r.child, r.col);
        ELSE
          EXECUTE format('DELETE FROM %s WHERE %I IN (SELECT %I FROM %s WHERE tenant_id IN (SELECT id FROM doomed))', r.child, r.col, r.pcol, r.parent);
        END IF;
        GET DIAGNOSTICS n = ROW_COUNT;
        IF n > 0 THEN progress := true; total := total + n; RAISE NOTICE '%: % rows', r.child, n; END IF;
      EXCEPTION WHEN foreign_key_violation THEN blocked := blocked + 1;
      END;
    END LOOP;
    -- Tenant-scoped tables.
    FOR r IN
      SELECT c.oid::regclass AS tbl FROM pg_class c
      JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
      WHERE c.relkind IN ('r', 'p') AND c.relnamespace = 'public'::regnamespace
    LOOP
      BEGIN
        EXECUTE format('DELETE FROM %s WHERE tenant_id IN (SELECT id FROM doomed)', r.tbl);
        GET DIAGNOSTICS n = ROW_COUNT;
        IF n > 0 THEN progress := true; total := total + n; RAISE NOTICE '%: % rows', r.tbl, n; END IF;
      EXCEPTION WHEN foreign_key_violation THEN blocked := blocked + 1;
      END;
    END LOOP;
    EXIT WHEN blocked = 0;
    IF NOT progress THEN RAISE EXCEPTION 'foreign keys still block % table(s); nothing was deleted', blocked; END IF;
  END LOOP;
  DELETE FROM tenants WHERE id IN (SELECT id FROM doomed);
  GET DIAGNOSTICS n = ROW_COUNT;
  RAISE NOTICE 'tenants: % rows (plus % dependent rows)', n, total;
END $$;

\if :apply
  SELECT (:candidate_count = :expect) AS count_ok \gset
  \if :count_ok
    COMMIT;
    \echo '== COMMITTED: test tenants deleted =='
  \else
    ROLLBACK;
    \echo '== ROLLED BACK: candidate count does not match --expect; nothing deleted =='
    DO $$ BEGIN RAISE EXCEPTION 'candidate count mismatch'; END $$;
  \endif
\else
  ROLLBACK;
  \echo '== REHEARSAL ONLY: rolled back, nothing deleted. Apply with: --apply --expect' :candidate_count '=='
\endif
SQL
````

### `scripts/db-inspect.sh`

SHA-256: `05c2fe07736deee80ff08a04d0e35fa6034e987ff914cb9678bfbc15868be5f7`

``bash
#!/usr/bin/env bash
# Read-only schema/migration inspection of the database configured in /opt/miautrix-mail/.env.
# The session is forced read-only (default_transaction_read_only=on); nothing can be changed.
# Usage (on the app server, as root): bash db-inspect.sh
set -Eeuo pipefail
ROOT=${MIAUTRIX_ROOT:-/opt/miautrix-mail}
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
command -v psql >/dev/null || die 'psql is required (apt install postgresql-client-18)'
connection_value() {
  local input=$1 wanted=$2 key value rest quote ch
  while [[ -n "$input" ]]; do
    key=${input%%=*}; [[ "$input" == *=* ]] || break; rest=${input#*=}; value=; quote=
    while [[ -n "$rest" ]]; do
      ch=${rest:0:1}; rest=${rest:1}
      if [[ -n "$quote" ]]; then if [[ "$ch" == "$quote" ]]; then quote=; else value+=$ch; fi
      elif [[ "$ch" == "'" || "$ch" == '"' ]]; then quote=$ch
      elif [[ "$ch" == ';' ]]; then break
      else value+=$ch; fi
    done
    key=$(printf '%s' "$key" | tr '[:upper:]' '[:lower:]' | tr -d ' ')
    case "$key" in
      host|server) [[ "$wanted" == host ]] && { printf '%s' "$value"; return 0; };;
      port) [[ "$wanted" == port ]] && { printf '%s' "$value"; return 0; };;
      database) [[ "$wanted" == database ]] && { printf '%s' "$value"; return 0; };;
      username|userid|user_id) [[ "$wanted" == username ]] && { printf '%s' "$value"; return 0; };;
      password) [[ "$wanted" == password ]] && { printf '%s' "$value"; return 0; };;
    esac
    input=$rest
  done
  return 1
}
pgpass_escape() { local v=$1; v=${v//\\/\\\\}; v=${v//:/\\:}; printf '%s' "$v"; }
[[ -f "$ROOT/.env" ]] || die "missing $ROOT/.env"
line=$(sed -nE 's/^MIAUTRIX_DB_CONNECTION=(.*)$/\1/p' "$ROOT/.env" | tail -n1)
line=${line#\"}; line=${line%\"}; line=${line#\'}; line=${line%\'}
[[ -n "$line" ]] || die 'MIAUTRIX_DB_CONNECTION is missing from .env'
DB_HOST=$(connection_value "$line" host) || die 'Host missing'
DB_PORT=$(connection_value "$line" port || printf '5432')
DB_NAME=$(connection_value "$line" database) || die 'Database missing'
DB_USER=$(connection_value "$line" username) || die 'Username missing'
DB_PASSWORD=$(connection_value "$line" password) || die 'Password missing'
unset line
passfile=$(mktemp); trap 'rm -f "$passfile"' EXIT; chmod 600 "$passfile"
printf '%s:%s:%s:%s:%s\n' "$(pgpass_escape "$DB_HOST")" "$DB_PORT" "$(pgpass_escape "$DB_NAME")" "$(pgpass_escape "$DB_USER")" "$(pgpass_escape "$DB_PASSWORD")" > "$passfile"
unset DB_PASSWORD
export PGPASSFILE=$passfile PGHOST=$DB_HOST PGPORT=$DB_PORT PGDATABASE=$DB_NAME PGUSER=$DB_USER PGOPTIONS='-c default_transaction_read_only=on'

psql -X -q -v ON_ERROR_STOP=1 <<'SQL'
\pset footer off
\echo '== server / database =='
SELECT current_database() AS database, split_part(version(), ' ', 2) AS server_version, current_setting('default_transaction_read_only') AS read_only;
\echo '== last 8 applied EF migrations =='
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 8;
\echo '== smtp_queue.direction column (AddQueueDirection) =='
SELECT column_name, data_type, is_nullable, coalesce(column_default, '<none>') AS column_default FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'smtp_queue' AND column_name = 'direction';
\echo '== ck_smtp_queue_direction constraint =='
SELECT conname, pg_get_constraintdef(oid) AS definition FROM pg_constraint WHERE conname = 'ck_smtp_queue_direction';
\echo '== smtp_queue.direction values =='
SELECT coalesce(direction, '<NULL>') AS direction, count(*) FROM smtp_queue GROUP BY 1 ORDER BY 1;
\echo '== IMAP-006 objects (expected absent before the UID migration) =='
SELECT conname FROM pg_constraint WHERE conname = 'ck_messages_uid_positive'
UNION ALL SELECT indexname FROM pg_indexes WHERE indexname = 'uq_messages_folder_id_uid';
\echo '== message UID summary =='
SELECT count(*) AS messages, count(*) FILTER (WHERE uid = 0) AS uid_zero, count(DISTINCT folder_id) AS folders FROM messages;
SQL
````

### `scripts/db-schema-snapshot.sh`

SHA-256: `bc070aa097ea157944fef8d2411073a0e6215a1a146a9d37640f73e814312346`

``bash
#!/usr/bin/env bash
# Read-only schema fingerprint of the database in /opt/miautrix-mail/.env (or --env-file <file>).
# Prints one sorted line per migration/column/constraint/index; no row data, no credentials.
# Never prompts for a password (psql -w): a parsing problem fails instead of echoing input.
# Usage (app server, root): bash db-schema-snapshot.sh [--env-file <path>] > /tmp/schema-<name>.txt
# Needs db-schema-snapshot.sql next to it. Compare two outputs with: diff -u a.txt b.txt
set -Eeuo pipefail
ENV_FILE=${MIAUTRIX_ROOT:-/opt/miautrix-mail}/.env
[[ ${1:-} == --env-file ]] && ENV_FILE=${2:?--env-file needs a path}
SQL="$(dirname "$0")/db-schema-snapshot.sql"
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
command -v psql >/dev/null || die 'psql is required (apt install postgresql-client-18)'
[[ -f "$SQL" ]] || die "missing $SQL"
[[ -f "$ENV_FILE" ]] || die "missing $ENV_FILE"
connection_value() {
  local input=$1 wanted=$2 key value rest quote ch
  while [[ -n "$input" ]]; do
    key=${input%%=*}; [[ "$input" == *=* ]] || break; rest=${input#*=}; value=; quote=
    while [[ -n "$rest" ]]; do
      ch=${rest:0:1}; rest=${rest:1}
      if [[ -n "$quote" ]]; then if [[ "$ch" == "$quote" ]]; then quote=; else value+=$ch; fi
      elif [[ "$ch" == "'" || "$ch" == '"' ]]; then quote=$ch
      elif [[ "$ch" == ';' ]]; then break
      else value+=$ch; fi
    done
    key=$(printf '%s' "$key" | tr '[:upper:]' '[:lower:]' | tr -d ' ')
    case "$key" in
      host|server) [[ "$wanted" == host ]] && { printf '%s' "$value"; return 0; };;
      port) [[ "$wanted" == port ]] && { printf '%s' "$value"; return 0; };;
      database) [[ "$wanted" == database ]] && { printf '%s' "$value"; return 0; };;
      username|userid|user_id) [[ "$wanted" == username ]] && { printf '%s' "$value"; return 0; };;
      password) [[ "$wanted" == password ]] && { printf '%s' "$value"; return 0; };;
    esac
    input=$rest
  done
  return 1
}
pgpass_escape() { local v=$1; v=${v//\\/\\\\}; v=${v//:/\\:}; printf '%s' "$v"; }
line=$(sed -nE 's/^MIAUTRIX_DB_CONNECTION=(.*)$/\1/p' "$ENV_FILE" | tail -n1 | tr -d '\r')
line=${line#\"}; line=${line%\"}; line=${line#\'}; line=${line%\'}
[[ -n "$line" ]] || die 'MIAUTRIX_DB_CONNECTION is missing'
DB_HOST=$(connection_value "$line" host) || die 'Host missing'
DB_PORT=$(connection_value "$line" port || printf '5432')
DB_NAME=$(connection_value "$line" database) || die 'Database missing'
DB_USER=$(connection_value "$line" username) || die 'Username missing'
DB_PASSWORD=$(connection_value "$line" password) || die 'Password missing'
unset line
passfile=$(mktemp); trap 'rm -f "$passfile"' EXIT; chmod 600 "$passfile"
printf '%s:%s:%s:%s:%s\n' "$(pgpass_escape "$DB_HOST")" "$DB_PORT" "$(pgpass_escape "$DB_NAME")" "$(pgpass_escape "$DB_USER")" "$(pgpass_escape "$DB_PASSWORD")" > "$passfile"
unset DB_PASSWORD
export PGPASSFILE=$passfile PGHOST=$DB_HOST PGPORT=$DB_PORT PGDATABASE=$DB_NAME PGUSER=$DB_USER PGOPTIONS='-c default_transaction_read_only=on'
psql -X -w -At -v ON_ERROR_STOP=1 -f "$SQL" </dev/null
````

### `scripts/db-schema-snapshot.sql`

SHA-256: `247a13c13fdce710fa211cdc818678df8fb555a96a268048e59ade56c1d88b29`

``sql
-- Normalised, sorted schema fingerprint of the public schema (read-only). One line per object, so two
-- databases can be compared with a plain diff. Contains no row data, only structure and EF history IDs.
SELECT line FROM (
  SELECT 'migration ' || "MigrationId" AS line FROM "__EFMigrationsHistory"
  UNION ALL
  SELECT format('column %s.%s %s null=%s default=%s', table_name, column_name,
                CASE WHEN data_type = 'character varying' THEN 'varchar(' || coalesce(character_maximum_length::text, '') || ')'
                     WHEN data_type = 'USER-DEFINED' THEN udt_name ELSE data_type END,
                is_nullable, coalesce(column_default, '-'))
  FROM information_schema.columns WHERE table_schema = 'public'
  UNION ALL
  SELECT format('constraint %s %s %s', conrelid::regclass, conname, pg_get_constraintdef(oid))
  FROM pg_constraint WHERE connamespace = 'public'::regnamespace
  UNION ALL
  SELECT format('index %s %s', tablename, regexp_replace(indexdef, '^CREATE (UNIQUE )?INDEX \S+ ON \S+ ', 'CREATE \1INDEX ON '))
         || ' name=' || indexname
  FROM pg_indexes WHERE schemaname = 'public'
  UNION ALL
  SELECT format('sequence %s', sequencename) FROM pg_sequences WHERE schemaname = 'public'
  UNION ALL
  SELECT format('view %s', viewname) FROM pg_views WHERE schemaname = 'public'
  UNION ALL
  SELECT format('function %s(%s)', p.proname, pg_get_function_identity_arguments(p.oid))
  FROM pg_proc p WHERE p.pronamespace = 'public'::regnamespace
  UNION ALL
  SELECT format('trigger %s %s', tgrelid::regclass, tgname) FROM pg_trigger WHERE NOT tgisinternal
    AND tgrelid IN (SELECT oid FROM pg_class WHERE relnamespace = 'public'::regnamespace)
) s ORDER BY line;
````

### `scripts/imap-external-validation.py`

SHA-256: `e385e6a36ba8ebfe3ce5985397dfd75fb9f507a1ca8c2534d4ef091b73063955`

``python
#!/usr/bin/env python3
"""External IMAP compatibility probe, including an optional delegated mailbox.

Host and credentials come from the environment, never from argv:
  MIAUTRIX_IMAP_HOST      hostname that mail clients use (certificate must match)
  MIAUTRIX_IMAP_USER      mailbox address
  MIAUTRIX_IMAP_PASSWORD  mailbox password or application password
  MIAUTRIX_IMAP_INSECURE  optional; "1" skips certificate validation (testing by IP only)
  MIAUTRIX_IMAP_SHARED_MAILBOX optional; full address to probe under Shared/<address>

Read-only: it uses EXAMINE and BODY.PEEK, so no flags change. It prints counts,
sizes and status only, never message contents.
"""
import imaplib
import os
import ssl
import sys

host = os.environ.get("MIAUTRIX_IMAP_HOST")
username = os.environ.get("MIAUTRIX_IMAP_USER")
password = os.environ.get("MIAUTRIX_IMAP_PASSWORD")
if not host or not username or not password:
    sys.exit("Set MIAUTRIX_IMAP_HOST, MIAUTRIX_IMAP_USER, and MIAUTRIX_IMAP_PASSWORD.")

context = ssl.create_default_context()
if os.environ.get("MIAUTRIX_IMAP_INSECURE") == "1":
    context.check_hostname = False
    context.verify_mode = ssl.CERT_NONE

results = []


def check(name, ok, detail=""):
    results.append(ok)
    print(f"{'PASS' if ok else 'FAIL'}  {name}{('  - ' + detail) if detail else ''}")


with imaplib.IMAP4_SSL(host, 993, ssl_context=context) as client:
    caps = " ".join(client.capabilities)
    check("CAPABILITY", "IMAP4REV1" in caps.upper(), caps)

    status, _ = client.login(username, password)
    check("LOGIN", status == "OK")

    status, namespaces = client.namespace()
    personal, other_users, shared = namespaces if status == "OK" else (None, None, None)
    decode = lambda value: value.decode(errors="replace") if isinstance(value, bytes) else value
    personal_ok = bool(personal and any(decode(prefix) == "" and decode(delimiter) == "/" for prefix, delimiter in personal))
    shared_ok = bool(shared and any(decode(prefix) == "Shared/" and decode(delimiter) == "/" for prefix, delimiter in shared))
    check("IMAP-010 NAMESPACE personal and shared", status == "OK" and personal_ok and shared_ok,
          f"personal={personal!r}; shared={shared!r}; other-users={other_users!r}")

    status, folders = client.list()
    names = [f.decode(errors="replace") for f in (folders or []) if f]
    has_inbox = any('"INBOX"' in n.upper() or n.upper().endswith(" INBOX") for n in names)
    check("IMAP-001 LIST returns folders", status == "OK" and has_inbox, f"{len(names)} folders")
    status, folders = client.lsub()
    subscribed_names = [f.decode(errors="replace") for f in (folders or []) if f]
    subscribed_upper = "\n".join(subscribed_names).upper()
    special_subscribed = all(name in subscribed_upper for name in ("SENT", "DRAFT", "ARCHIVE", "JUNK", "TRASH"))
    check("IMAP-009 default folders are subscribed", status == "OK" and special_subscribed,
          f"{len(subscribed_names)} subscribed folders")

    status, data = client.select("INBOX", readonly=True)
    exists = int(data[0]) if status == "OK" and data and data[0] else 0
    check("IMAP-002 EXAMINE INBOX", status == "OK", f"{exists} messages")

    status, data = client.search(None, "ALL")
    ids = data[0].split() if status == "OK" and data and data[0] else []
    check("IMAP-003 SEARCH ALL", status == "OK" and len(ids) == exists, f"{len(ids)} ids")

    status, data = client.uid("SEARCH", None, "ALL")
    check("IMAP-003 UID SEARCH ALL", status == "OK", f"{len(data[0].split()) if data and data[0] else 0} uids")

    if exists:
        status, data = client.fetch("1", "(UID FLAGS RFC822.SIZE BODY.PEEK[HEADER])")
        ok = status == "OK" and data and isinstance(data[0], tuple) and len(data[0][1]) > 0
        check("IMAP-004 FETCH headers literal", ok, f"{len(data[0][1]) if ok else 0} header bytes")

        status, data = client.fetch("1", "(RFC822.SIZE BODY.PEEK[])")
        ok = status == "OK" and data and isinstance(data[0], tuple)
        declared = None
        if ok:
            meta = data[0][0].decode(errors="replace")
            if "RFC822.SIZE" in meta:
                declared = int(meta.split("RFC822.SIZE")[1].split()[0].strip(")"))
        body_len = len(data[0][1]) if ok else 0
        check("IMAP-008 RFC822.SIZE equals BODY.PEEK[] octets", ok and declared is not None and declared == body_len,
              f"{body_len} bytes, RFC822.SIZE {declared}")

        last = ids[-1].decode() if ids else str(exists)
        status, data = client.fetch(f"1:{last}", "(UID FLAGS)")
        check("IMAP-004 FETCH sequence range", status == "OK" and len([d for d in data if d]) >= 1,
              f"{len([d for d in data if d])} responses for 1:{last}")

        uids = []
        for item in data or []:
            text = item.decode(errors="replace") if isinstance(item, bytes) else ""
            if "UID " in text:
                uids.append(int(text.split("UID ")[1].split()[0].strip(")")))
        zero = sum(1 for u in uids if u == 0)
        check("IMAP-006 UIDs non-zero and unique", bool(uids) and zero == 0 and len(set(uids)) == len(uids),
              f"{len(uids)} uids, {zero} zero, {len(uids) - len(set(uids))} duplicates")
    else:
        print("SKIP  FETCH checks: INBOX is empty")

    status, _ = client.close()
    check("CLOSE", status == "OK")

    shared_address = os.environ.get("MIAUTRIX_IMAP_SHARED_MAILBOX")
    if shared_address:
        shared_prefix = f"Shared/{shared_address}"
        status, folders = client.list('""', f'"{shared_prefix}/*"')
        shared_names = [f.decode(errors="replace") for f in (folders or []) if f]
        check("IMAP-009 authorized shared LIST", status == "OK" and bool(shared_names),
              f"{len(shared_names)} folders for configured shared mailbox")
        status, folders = client.lsub('""', f'"{shared_prefix}/*"')
        subscribed_shared = [f.decode(errors="replace") for f in (folders or []) if f]
        check("IMAP-009 shared folders are subscribed by default", status == "OK" and bool(subscribed_shared),
              f"{len(subscribed_shared)} subscribed shared folders")
        shared_inbox = f"{shared_prefix}/INBOX"
        status, data = client.select(shared_inbox, readonly=True)
        shared_count = int(data[0]) if status == "OK" and data and data[0] else 0
        check("IMAP-009 shared EXAMINE", status == "OK", f"{shared_count} messages")
        if status == "OK":
            status, data = client.search(None, "ALL")
            shared_ids = data[0].split() if status == "OK" and data and data[0] else []
            check("IMAP-009 shared SEARCH", status == "OK", f"{len(shared_ids)} ids")
            if shared_ids:
                status, data = client.fetch("1", "(UID FLAGS RFC822.SIZE BODY.PEEK[])")
                ok = status == "OK" and data and isinstance(data[0], tuple)
                literal_len = len(data[0][1]) if ok else 0
                declared = None
                if ok:
                    meta = data[0][0].decode(errors="replace")
                    if "RFC822.SIZE" in meta:
                        declared = int(meta.split("RFC822.SIZE", 1)[1].split()[0].strip(")"))
                check("IMAP-009 shared FETCH literal and size", ok and declared == literal_len,
                      f"{literal_len} bytes, RFC822.SIZE {declared}")
            client.close()
    else:
        print("SKIP  IMAP-009 shared checks: MIAUTRIX_IMAP_SHARED_MAILBOX is unset")

    client.logout()

print(f"\n{sum(results)}/{len(results)} checks passed")
sys.exit(0 if all(results) else 1)
````
