# Handoff — Miautrix Mail Server v2.3

**Status:** released on `release/v2.3` and `main`. v2.2.1 is in production. Previous handoff: `docs/HANDOFF-v2.2.md`.

## What is in v2.3

| Area | Change | Report |
|---|---|---|
| **Test suite** | The 12 long-standing integration failures are fixed (ERRORS §13.18 closed). None were product bugs: two test classes used EF in-memory instead of PostgreSQL, CLI tests lacked an isolated storage directory, and one calendar check didn't unfold MIME headers. Parallel tests no longer change process-wide environment variables. Integration **149/149**, 6+ consecutive runs on fresh databases. | `docs/IntegrationFixtures-v2.3-Report.md` |
| **Smoke test** | New `scripts/smoke-test.ps1` checks a deployed server **directly by LAN IP**: ports, TLS and certificate expiry, SMTP AUTH-before-TLS policy, IMAP capabilities, the API session requirement, webmail/admin/HSTS, autoconfig, MTA-STS, and optional logged-in checks plus a test send. Read-only by default, secrets masked, 19 Pester tests against local fakes. | `docs/SmokeTest-v2.3-Report.md` |
| **IMAP** | `ID` command, additive `ENABLE`, `OLDNAME` on normalized CREATE, BINARY limited to eligible MIME leaves, updates sent at command boundaries without echoing the session's own STORE, stricter argument handling. **IMAP4rev2 is still not advertised** (see below). | `docs/IMAP4rev2-v2.3-Report.md` |
| **RFC 9051 audit** | Paragraph-level inventory of 229 server requirements: 80 Implemented, 120 Partial, 29 N/A, **0 Missing**. | `docs/IMAP4rev2-v2.3-Audit.md` |
| **CLI** | Internal only: the CLI now disposes its service providers on exit and has a storage-directory parameter for tests. Production behaviour is unchanged. | — |

## Deploy

- **No migrations and no `.env` changes.** Deploy without `-RunMigrations`:

```powershell
git checkout release/v2.3; git pull
scripts/deploy-release.ps1 -DryRun
scripts/deploy-release.ps1
scripts/deploy-release.ps1 -Status
```

- Rollback: `scripts/deploy-release.ps1 -Rollback` (to v2.2.1; safe, since there is no schema change).

## Owner test checklist

1. From a workstation on the server's network (PowerShell 7):
   ```powershell
   ./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname>
   ./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname> -Domain <mail-domain> -Credential (Get-Credential)
   ```
   - Expect exit code 0.
   - MTA-STS shows INFO until DNS, nginx and the policy mode are set (item 3).
   - `-Credential` logs in and out once through the API.
2. Thunderbird, Outlook and iPhone: read, move a message, IDLE, save a draft. IMAP behaviour changed only in edge cases.
3. **Autodiscovery and MTA-STS (shipped in v2.2, not yet switched on in your infrastructure):**
   - DNS records for `autoconfig`, `autodiscover` and `mta-sts`, plus `_mta-sts` / `_smtp._tls` TXT records and the SRV records shown in admin → Domains;
   - nginx `server_name` entries and certificates for those hosts;
   - the three nginx `proxy_set_header` lines in `docs/DEPLOYMENT.md`.

   Then rerun the smoke test with `-Domain`.

## Recommended for v2.4

1. **IMAP4rev2 advertisement.**
   - The audit has 89 applicable MUST-level rows marked Partial. They are mostly unproven edge cases, not known defects. All core paths work and are tested, and the owner accepted shipping v2.3 on that basis.
   - Close the rows group by group with protocol tests, using `docs/IMAP4rev2-v2.3-Audit.md` as the checklist:

     | Area | Rows |
     |---|---|
     | State × command legality (§3) | 23 |
     | SEARCH/ESEARCH/SAVE result matrix (§6.4.4) | 12 |
     | SASL/TLS edge cases (§6.1–6.2) | 11 |
     | LIST selection/RETURN table (§6.3.9) | 8 |
     | Response-code placement (§7.1–7.2) | 6 |
     | UID invariants (§2.3) | 5 |
     | Data formats and literals (§4) | 5 |
     | Net-Unicode names (§5.1) | 4 |
     | STATUS DELETED/SIZE, APPENDUID errors, nested BINARY, pipelining and concurrent updates | ~15 |

   - Then flip `CapabilityLine`.
2. Decide on `MIAUTRIX_INBOUND_AUTH_MODE=enforce` after reviewing a week of `Authentication-Results` headers.
3. DMARC aggregate reports (`rua`), and Sieve `body` (RFC 5173).
4. Optional: the Cloudflare Email Worker update, so Cloudflare's authentication results are trusted (InboundAuth report).

## Verification on `release/v2.3`

| Suite | Result |
|---|---|
| Build | 0 warnings |
| Unit / security / protocol | 243 / 7 / 44 |
| Integration | 149/149 on fresh migrated DBs (3 runs after the final merge, 6 before it) |
| Webmail / admin | 24 / 23 |
| Smoke-test Pester | 19/19, twice |
