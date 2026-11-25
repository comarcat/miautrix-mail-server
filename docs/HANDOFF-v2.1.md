# Handoff — Miautrix Mail Server v2.1

**Status:** merged on branch `release/v2.1`, **not yet deployed**. v2.0.0 (`76c8e7c`) is in production.
Previous handoff: `docs/HANDOFF-v2.0.0-release.md`.

## What is in v2.1

| Area | Change | Report |
|---|---|---|
| SMTP listener (WP1) | Fixes: only the last recipient got multi-recipient mail (ERRORS §13.14); unbounded input (§13.15). Adds STARTTLS on 25, TLS 1.2+, strict state machine, limits | `docs/SMTP-v2.1-Report.md` |
| Outbound (WP2) | Per-domain `direct` MX transport (Cloudflare stays the default), MTA-STS, DKIM for `direct` only, 5-day retry, RFC 3464 bounces | `docs/Outbound-v2.1-Report.md` |
| IMAP Phase 2 | Stage A: NAMESPACE, MOVE, IDLE. Stage B: rev2 session mode behind `ENABLE IMAP4rev2`; **`IMAP4rev2` not advertised** until every RFC 9051 MUST row passes | `docs/IMAP-Phase2-Report.md` |
| WEBMAIL-001 | Drafts saved over IMAP open correctly in webmail; webmail edits reach IMAP clients (§13.16) | `docs/WEBMAIL-001-Report.md` |
| JMAP | Claim removed from the architecture diagram; the empty `Protocols.Jmap` project stays as a placeholder | — |
| Audit | Full non-IMAP protocol audit, 11 work packages | `docs/v2.1-Protocol-Compliance-Audit.md` |

Coordinator review fixes before merge are in ERRORS §13.17 (DKIM scope, Worker boot crash, lost troubleshooting §15).

## Deploy

- **No migrations** since v2.0.0, so `-RunMigrations` is not needed.
- **No `.env` changes required.** All new settings are optional (TROUBLESHOOTING §16, §17). `OUTBOUND_EHLO_NAME` defaults to `MIAUTRIX_HOSTNAME`.

```powershell
git checkout release/v2.1
scripts/deploy-release.ps1 -DryRun
scripts/deploy-release.ps1
scripts/deploy-release.ps1 -Status
```

After the deploy, backfill drafts saved by IMAP clients before v2.1 (TROUBLESHOOTING §18; load `/opt/miautrix-mail/.env` as in §7):

```bash
/opt/miautrix-mail/cli/Miautrix.Mail.Cli messages reparse --tenant all --dry-run
/opt/miautrix-mail/cli/Miautrix.Mail.Cli messages reparse --tenant all
```

Rollback: `scripts/deploy-release.ps1 -Rollback` (no schema change, so it is a plain binary rollback).

## Owner test checklist

1. **SMTP multi-recipient:** from Thunderbird send one message to two local users and one external address; all three receive it.
2. **SMTP limits:** a normal 10 MB attachment sends; `openssl s_client -starttls smtp -connect 127.0.0.1:25 -tls1_1` on the server is refused, `-tls1_2` works.
3. **Send As** (regression from v2.0.0): sending as `sales@…` still works for granted delegates and gets `550 5.7.1` otherwise.
4. **Outbound:** miautrix.tech stays `cloudflare`; an external recipient shows DKIM `pass` for `d=miautrix.tech` (signed by Cloudflare only, no second signature).
5. **Drafts (WEBMAIL-001):** save a draft in Thunderbird in personal Drafts and in `Shared/sales@…/Drafts`; open it in webmail (subject, recipients, body present); edit and save; refresh Thunderbird — one copy, with the edit.
6. **IMAP:** in Thunderbird, move a message between folders (MOVE) and between a personal and shared folder; leave INBOX open and send yourself mail — it appears without a manual refresh (IDLE). `Shared/` folders still listed.
7. **Logs:** `journalctl -u miautrix-mail-worker -u miautrix-mail --since "-30 min"` shows no `FATAL` and no unhandled exceptions.

## Verification on `release/v2.1`

Build 0 warnings/0 errors; unit 125, security 7, protocol 38, integration 34 (Cloudflare, SMTP, sender authorization, UID allocator, drafts), admin 18, webmail 17. Run twice. The full integration suite is 94/110; the same 16 fixture failures exist on v2.0.0 (ERRORS §13.18).

## Next

- Finish IMAP Stage B remaining MUST rows (response codes, `$` keywords, UNKNOWN-CTE), then advertise `IMAP4rev2`.
- WP3 inbound SPF/DKIM/DMARC; WP4 Sieve on delivery (no ManageSieve).
- Deferred: autodiscovery, MTA-STS policy hosting for our own domains, DANE.
