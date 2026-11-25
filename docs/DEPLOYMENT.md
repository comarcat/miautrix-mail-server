# Versioned production deployment

Troubleshooting guide: [docs/TROUBLESHOOTING.md](TROUBLESHOOTING.md).

This document describes the owner-operated deployment workflow. The deployment agent must not connect to production unless the owner explicitly runs the scripts from their machine. The workflow is designed for `/opt/miautrix-mail`; it leaves systemd and nginx stable paths in place by switching those paths to symlinks into immutable releases.

## Layout

Each release is stored under `/opt/miautrix-mail/releases/<release-id>/` with `app/`, `worker/`, `cli/`, `admin/`, `webmail/`, and `RELEASE.json`. Stable paths `/opt/miautrix-mail/{app,worker,cli,admin,webmail}` point to the active release, so existing units and nginx paths can remain unchanged. `releases/.current` and `releases/.previous` record release IDs. Database dumps, config snapshots, and manifests live under `/opt/miautrix-mail/backups/`.

Mail blobs must live outside every release and deploy-managed directory. Both Web and Worker must resolve `MIAUTRIX_STORAGE_DIR` to the same existing absolute path. Production Web, Worker, and CLI now fail at boot if this setting is absent; only Development retains the `AppContext.BaseDirectory/mail_data` fallback.

## First v2.0 deployment checklist

1. Review and commit the release branch; the deploy script requires a clean tree unless `-AllowDirty` is deliberately used for a reviewed snapshot.
2. On the server, run `scripts/lxc-prepare-storage.sh --pin` if blobs are still under an application directory. Configure `MIAUTRIX_STORAGE_DIR` in `/opt/miautrix-mail/.env` and/or systemd overrides so Web and Worker both resolve to that same absolute directory. Do this before deploying. The preflight aborts if it cannot confirm the setting or discovers files in `app/mail_data` or `worker/mail_data`; it never relocates data.
3. Confirm `/opt/miautrix-mail/.env` has `MIAUTRIX_STORAGE_DIR` and `MIAUTRIX_DB_CONNECTION`, readable only by root/the service account as appropriate. Keep credentials out of command lines and logs.
4. If `pg_dump`/`pg_restore` is missing, install the PostgreSQL client matching the database server major version (the preflight reports `apt install postgresql-client-<major>`); do not let the script install packages automatically.
   The `pg_dump` client major must be greater than or equal to the PostgreSQL server major on `10.11.1.52`; PostgreSQL rejects older dump clients and the backup step fails loudly before switching. Check the server major from the server with credentials supplied through a protected `PGPASSFILE` (never use `PGPASSWORD` or put a password in argv): `PGPASSFILE=/root/.pgpass psql -h 10.11.1.52 -U <database-user> -d <database-name> -c 'SHOW server_version'`, then compare it with `pg_dump --version`.
5. Run `scripts/deploy-release.ps1 -DryRun` first. It runs local gates, publishes Web, Worker, CLI and frontends, uploads the bundle, and runs only remote preflight and status checks. Review its output and the computed release ID.
6. Run `scripts/deploy-release.ps1` for the approved deploy. It takes and verifies a `pg_dump -Fc --no-owner` backup and a protected config snapshot before optional migrations, staging, or switching. Use `-RunMigrations` only when the release requires approved schema changes; Since v2.0.0 (`76c8e7c`), releases include migrations: deploy with `-RunMigrations` whenever `db-inspect.sh` shows the repository has migrations the database lacks. Before a rollback across migrations, read TROUBLESHOOTING §5 (migration-aware rollback).
7. After health checks pass, run `/opt/miautrix-mail/cli/Miautrix.Mail.Cli messages reparse --tenant all --dry-run` and review the counts before deciding whether to run the backfill without `--dry-run`.

`-SkipTests` and `-AllowDirty` are explicit owner choices. DB-backed tests run only when `MIAUTRIX_DB_CONNECTION` is set locally and the value does not identify production; the deploy script refuses the production host/database before invoking those tests.

## Switch and verification

The server-side `scripts/lxc-release.sh` performs preflight, backup, stage, switch, verify, rollback, cleanup, and status operations. Preflight checks server-local config/secret/data files against the upload and rejects path-valued environment settings that point into deploy-managed directories. Switching stops Worker then Web, moves legacy directories into a versioned `legacy-<tfm>-<timestamp>` release on the first conversion, atomically replaces each stable path with a temporary symlink, records the active ID before starting services, and tests nginx configuration before reloading nginx. Verification checks both services, ports 25/465/587/993, the configured Kestrel health URL, the runtimeconfig TFM of the active release (including legacy rollback targets), and the IMAPS greeting.

On a switch or verification failure the client attempts automatic rollback and exits non-zero; a staging failure only removes its partial release and never rolls back a healthy current release. The pre-switch custom-format dump remains in `/opt/miautrix-mail/backups/`; the server script prints its path and checksum. Do not delete the dump while investigating.

## Rollback

- `scripts/deploy-release.ps1 -Rollback` switches to `.previous`, restarts the services, and verifies the result. This does not restore the database.
- Restore the database only when the owner explicitly approves it: run `scripts/deploy-release.ps1 -Rollback -RestoreDatabase`. The remote operation requires typing `RESTORE DATABASE` before executing `pg_restore --clean --if-exists --no-owner` against the dump. Database restore is destructive and is never automatic.
- A manual restore can use the exact `pg_restore` command printed by `lxc-release.sh rollback`; the server `.env` supplies credentials via a temporary mode-0600 `PGPASSFILE`, not command arguments. Confirm the correct dump and target database with the owner first.

## Retention and cleanup

Cleanup runs only after a successful verification marker. `cleanup --keep 2` retains exactly the current and previous release and the two newest manifest-associated database/config backup sets. It checks resolved release paths before deletion and never removes paths outside `releases/` and `backups/`. `cleanup --keep 2 --dry-run` reports candidates without deleting them. Use `-SkipCleanup` to retain older releases for investigation.

## Security notes

The old root `deploy.ps1` is retired. `scripts/deploy-compile-upload.ps1` is deprecated and refuses its destructive wipe unless `-IUnderstandNoBackup` is supplied; use it only for a separately reviewed recovery case. A production database credential was present in tracked files and remains in repository history; the owner must rotate it. History rewriting is optional and requires owner review. No deploy or database command has been run by the authoring session.

## IMAP UID migration (IMAP-006)

When deploying the release that introduces stable per-folder IMAP UIDs, run `scripts/deploy-release.ps1 -RunMigrations` only after reviewing the UID backfill migration and backup. The migration renumbers existing messages by folder and changes UIDVALIDITY for folders whose UIDs changed. During the brief interval between migration completion and the release switch, the old process may still attempt to insert UID 0; the new database check constraint rejects that write. Inbound mail remains in `smtp_queue` and is retried by the worker after the new release starts.
