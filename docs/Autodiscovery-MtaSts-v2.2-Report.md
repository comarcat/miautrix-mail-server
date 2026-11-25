# Client autodiscovery and MTA-STS v2.2 report

## Implementation and compliance matrix

| Requirement | Status | Implementation |
|---|---|---|
| Mozilla autoconfig | Implemented | Public routes return IMAP SSL 993, SMTP SSL 465 and STARTTLS 587 with `%EMAILADDRESS%`; unknown hosted domain returns 404. |
| Outlook POX Autodiscover | Implemented | GET/POST route accepts case-insensitive path routing, finds `EMailAddress`, emits POX Account with IMAP/SMTP entries and echoes the address. |
| Enumeration and throttling | Implemented | Results are based on domain only, independent of mailbox existence; IP fixed window 60 requests/minute, no address logging. |
| Apple profile | Partial | Auth context scoped download returns an unsigned profile for the current user's address with no password. Uses the application's existing request-context mechanism. |
| RFC 6186 / 8314 DNS guidance | Partial | Admin DNS inspector shows SRV/CNAME suggestions. Deployment owner must publish them. |
| RFC 8461 §3.2 | Partial | Host-routed policy returns exact `version`, `mode`, `mx`, `max_age` lines for testing/enforce and 404 for none/unknown. MX currently uses configured public SMTP host. |
| RFC 8460 | Partial | Admin allows configuring and displays the suggested TLS-RPT TXT value. Report ingestion is not implemented. |

## Settings and defaults

- `Domain.MtaStsMode`: `none` for existing and new domains.
- `Domain.MtaStsMaxAge`: 604800 seconds.
- `Domain.TlsReportRua`: unset; admin proposes `mailto:tls-reports@<domain>`.
- Optional `MIAUTRIX_PUBLIC_IMAP_HOST` and `MIAUTRIX_PUBLIC_SMTP_HOST` override the configured `MIAUTRIX_HOSTNAME` for client endpoints. Configure a hostname if the overrides are absent.

## Deploy notes

Apply migration `V22_Autodiscovery_MtaSts` before starting this build. Publish `_imaps._tcp.<domain> SRV 0 1 993 <mail-hostname>.`, `_submissions._tcp.<domain> SRV 0 1 465 <mail-hostname>.`, `_submission._tcp.<domain> SRV 0 1 587 <mail-hostname>.`, `autoconfig.<domain> CNAME <mail-hostname>.`, and `autodiscover.<domain> CNAME <mail-hostname>.`. For MTA-STS set `_mta-sts.<domain> TXT "v=STSv1; id=<policy-id>"`, and for reporting set `_smtp._tls.<domain> TXT "v=TLSRPTv1; rua=mailto:<report-address>"`.

The nginx HTTPS virtual host must include `server_name mta-sts.<domain> autoconfig.<domain> autodiscover.<domain>;` and present a certificate covering each name (SAN or wildcard as appropriate). Proxy these names to the Web Kestrel listener without changing the original `Host` header; MTA-STS routing reads it. If using Cloudflare, create DNS records for the names and ensure HTTPS proxying routes to the same nginx host with the Host header intact. Cloudflare Universal SSL coverage and origin certificate/SNI behavior must cover the chosen hostnames; validate this in deployment before setting policy mode to enforce. Do not proxy raw IMAP/SMTP SRV targets.

## Troubleshooting draft

- A 404 from autoconfig/autodiscover means the address domain is not present as a hosted domain; mailbox existence is intentionally not checked.
- A 404 from `mta-sts.<domain>/.well-known/mta-sts.txt` means the domain is unknown or its mode is `none`.
- Check the reverse proxy forwards the original `Host` header and TLS certificate covers `mta-sts.<domain>`.
- Check `MIAUTRIX_HOSTNAME` or the optional public host overrides resolve to the advertised IMAP/SMTP listeners.

## Test evidence

`dotnet build Miautrix.Mail.sln -warnaserror -m:1` passed with zero warnings; `pnpm --filter admin build` passed; `pnpm --filter admin test -- --run` passed (18 tests). The three .NET test projects were attempted without the required DB test-process configuration and failed during their database bootstrap; no migration was applied. Endpoint contract coverage and migration verification against an isolated throwaway database remain outstanding.

## Deferred

RFC 8460 report ingestion is out of scope. MX policy lines use the configured public SMTP hostname because the domain model has no actual inbound MX list. The TLS-RPT address is currently a policy suggestion and is not independently stored as a normalized validated email setting.

