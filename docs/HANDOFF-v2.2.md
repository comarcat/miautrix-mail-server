# Handoff — Miautrix Mail Server v2.2

**Status:** v2.2.0 deployed to production 2026-11-25 (release `v2.2.0-590feb4`). v2.2.1 (logging noise only) deploys without `-RunMigrations`.
Previous handoff: `docs/HANDOFF-v2.1.md`. **Next: `docs/HANDOFF-v2.3.md`.**

## What is in v2.2

| Area | Change | Report |
|---|---|---|
| **Security (critical)** | The web API now requires a valid server-side session on every controller endpoint. It used to trust client-sent `X-Tenant-Id`/`X-User-Id` headers and fall back to the default admin (ERRORS §13.19). | `docs/ApiAuth-v2.2-Report.md` |
| **Inbound SPF/DKIM/DMARC** | RFC 7208/6376/7489/8601. Every inbound message gets an `Authentication-Results` header. The default is **monitor** (never rejects); enforce is opt-in. | `docs/InboundAuth-v2.2-Report.md` |
| **Sieve on delivery** | RFC 5228 plus fileinto, envelope, imap4flags, vacation, reject/ereject, copy and mailbox. Webmail gets Settings → Filters and an "Out of office" form. There is no ManageSieve. | `docs/Sieve-v2.2-Report.md` |
| **Autodiscovery** | Thunderbird autoconfig, Outlook POX autodiscover, and an Apple `.mobileconfig` download from webmail. | `docs/Autodiscovery-MtaSts-v2.2-Report.md` |
| **MTA-STS / TLS-RPT hosting** | Per-domain policy, `none` by default, with the DNS records shown in admin. | same |
| **DANE (outbound)** | RFC 7672 for `direct` domains. **Off** by default, and it needs a local validating resolver. | `docs/Audit-Remaining-v2.2-Report.md` |
| **Audit follow-up** | All 73 findings re-triaged. RFC 2047/2231 output hardened. 3 findings remain open with reasons (CalDAV/CardDAV not claimed; cipher policy). | same |
| **IMAP4rev2** | More rev2 work and tests. **Still not advertised**; the remaining rows are listed in the report. | `docs/IMAP-Phase2-Report.md` |

## Deploy

1. **Three migrations, so `-RunMigrations` is REQUIRED.** All of them are forward-only and idempotent:
   - `V22_Autodiscovery_MtaSts`;
   - `V22_Sieve_Scripts`;
   - `V22_ApiAuth_Sessions`.
2. **No `.env` change is required.** All new settings are optional (DEPLOYMENT "v2.2 upgrade notes").
3. **Everyone is logged out once**, because old browser tokens were never stored server-side. Users just log in again.

```powershell
git checkout release/v2.2
scripts/deploy-release.ps1 -DryRun -RunMigrations
scripts/deploy-release.ps1 -RunMigrations
scripts/deploy-release.ps1 -Status
```

Rollback: `scripts/deploy-release.ps1 -Rollback`. The migrations only add columns and tables, so the old code runs on the new schema. A rollback **re-opens the API authentication hole**, though, so use it only for an outage.

**Optional, later:**
- **Cloudflare Email Worker:** deploy the updated `workers/email/src/index.ts` so it forwards Cloudflare's authentication results. Then set `MIAUTRIX_INBOUND_TRUSTED_AUTHSERV_IDS` (InboundAuth report).
- **Autodiscovery / MTA-STS:** add DNS records plus nginx `server_name` entries and certificates for `autoconfig.<domain>`, `autodiscover.<domain>` and `mta-sts.<domain>` (Autodiscovery report).
- **DANE:** install a validating resolver, then set `OUTBOUND_DANE_MODE=opportunistic` and `OUTBOUND_DNSSEC_RESOLVER`.

## Owner test checklist

1. **Login and session.**
   - Log in to webmail and admin. Read, send, download an attachment and open the admin pages.
   - Log out, then press Back: data must not load.
   - On the server: `curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:<api-port>/api/v1/auth/me` prints `401`. Use loopback only.
2. **Inbound authentication.** Send yourself a message from Gmail. In webmail "show original", or in Thunderbird, check that there is exactly one `Authentication-Results: <mail-hostname>; spf=… dkim=… dmarc=…` header. Nothing is rejected in monitor mode.
3. **Sieve.**
   - Webmail → Settings → Filters → Out of office: enable it, then send a message from an external account. You get one auto-reply, and a second message within the configured days gets none.
   - Disable it afterwards.
   - Create a filter that files messages with a given subject into a folder, and test it.
4. **Autodiscovery** (after DNS and nginx): Thunderbird "Add account" with only address and password finds the IMAP and SMTP settings. The iPhone profile downloads from webmail settings.
5. **Regression:** the v2.1 checklist steps 1–6 (SMTP multi-recipient, Send As, drafts, IMAP MOVE/IDLE).
6. **Logs:** `journalctl -u miautrix-mail-worker -u miautrix-mail --since "-30 min"` shows no `FATAL` and no unhandled exceptions.

## Verification on `release/v2.2`

The coordinator ran every suite twice on a fresh, fully migrated throwaway database:

| Suite | Result |
|---|---|
| Build | 0 warnings / 0 errors |
| Model snapshot | Clean (`has-pending-model-changes`: none) |
| Unit / security / protocol | 242 / 7 / 44 |
| Webmail / admin | 24 / 23 |
| Integration | 137/149 |

The 12 integration failures are the pre-existing fixture groups (ERRORS §13.18), down from 16. The shared dev test database lacks the v2.2 schema until these migrations are applied to it.

## Next

- Finish IMAP4rev2 and advertise it.
- Fix the 12 integration fixtures.
- After a week of monitor-mode headers, decide on `MIAUTRIX_INBOUND_AUTH_MODE=enforce`.
- DMARC aggregate reports (`rua`).
- RFC 5173 Sieve `body`.
