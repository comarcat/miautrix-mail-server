# Autodiscovery and MTA-STS v2.2 report

## Implemented endpoints and standards matrix

| Surface | Requirement | Result |
|---|---|---|
| `GET /.well-known/autoconfig/mail/config-v1.1.xml?emailaddress=<address>` and `GET /mail/config-v1.1.xml?emailaddress=<address>` | Mozilla ISPDB autoconfig | Hosted domain check; IMAP 993 SSL, submission 465 SSL and 587 STARTTLS; `%EMAILADDRESS%` username and `plain` authentication. Mailbox lookup is deliberately not performed. |
| `POST` or `GET /autodiscover/autodiscover.xml` | Outlook POX Autodiscover response schema 2006a | Parses `EMailAddress`, echoes it, returns IMAP and SMTP protocol rows with SSL on, SPA off and AuthRequired on. |
| Autoconfig and Outlook endpoints | No mailbox enumeration / abuse control | Unknown domain returns 404. Any local part on a known domain gets the same server settings. Discovery endpoints alone use a fixed window of 60 requests per client IP per minute; rejected requests return 429. Behind nginx, `X-Forwarded-For` is used only when the immediate peer is loopback; untrusted peers cannot choose a bucket. Address values are not logged. |
| `GET /api/v1/webmail/mobileconfig` | Apple Mail configuration profile | Requires both request identity headers used by the existing Web request context, reads the current tenant and user together, and returns an unsigned `.mobileconfig` with no password. A tenant/user mismatch cannot resolve the user and returns 404. |
| `GET /.well-known/mta-sts.txt` on `Host: mta-sts.<domain>` | RFC 8461 §3.2 | Serves `version`, mode, ordered `mx` lines, and max-age with LF line endings and a final LF. Unknown domains and mode `none` return 404. MX uses the domain's configured `mta_sts_mx` patterns when present, otherwise the public SMTP host. |
| Admin domain editor / DNS inspector | RFC 6186 and RFC 8314 suggestions | Displays `_imaps._tcp`, `_submissions._tcp`, `_submission._tcp`, `autoconfig`, and `autodiscover` records; the editor controls MTA-STS mode, max-age, and TLS-RPT destination. `_mta-sts` TXT policy ID derives from the domain update timestamp at tick precision. |
| `_smtp._tls.<domain>` TXT suggestion | RFC 8460 | Admin shows configurable `v=TLSRPTv1; rua=...` guidance. Report ingestion is out of scope. |

## Settings and defaults

- Migration: `V22_Autodiscovery_MtaSts` idempotently adds `mta_sts_mode` (`none` default), `mta_sts_max_age` (604800 seconds), nullable `tls_report_rua`, and nullable `mta_sts_mx` to `domains`. Existing rows receive the same safe defaults.
- `mta_sts_mx` accepts comma- or newline-separated DNS MX patterns, including a leading `*.` wildcard. Configure every MX target used by the domain, including Cloudflare Email Routing targets; leaving it empty falls back to the configured public SMTP host.
- `MIAUTRIX_PUBLIC_IMAP_HOST` and `MIAUTRIX_PUBLIC_SMTP_HOST` are optional. Each defaults to `MIAUTRIX_HOSTNAME`; configure the overrides when public client endpoints differ from the host name.
- The admin UI's initial TLS-RPT suggestion is `mailto:tls-reports@<domain>`. It is a suggestion, not a report processor or validated destination.

## Owner deployment steps

1. Apply the migration before starting the new Web build. No worker, SMTP, IMAP, outbound, or inbound service changes are required.
2. Set `MIAUTRIX_HOSTNAME` to the server's public mail hostname, or set `MIAUTRIX_PUBLIC_IMAP_HOST` / `MIAUTRIX_PUBLIC_SMTP_HOST` where client-facing hosts differ.
3. Publish these DNS entries, replacing placeholders with the actual service name and domain:

   ```dns
   _imaps._tcp.<domain>.          SRV     0 1 993 <mail-hostname>.
   _submissions._tcp.<domain>.    SRV     0 1 465 <mail-hostname>.
   _submission._tcp.<domain>.     SRV     0 1 587 <mail-hostname>.
   autoconfig.<domain>.           CNAME       <mail-hostname>.
   autodiscover.<domain>.         CNAME       <mail-hostname>.
   _mta-sts.<domain>.             TXT         "v=STSv1; id=<policy-id>"
   _smtp._tls.<domain>.           TXT         "v=TLSRPTv1; rua=mailto:<report-address>"
   ```

   The admin Domains DNS inspector displays the client and policy suggestions. Copy the current `_mta-sts` policy ID after changing mode, max-age, or MX patterns. Point the MTA-STS HTTPS name at the Web service as described below.
4. Add HTTPS names to the nginx Web virtual host. For each hosted domain use `server_name mta-sts.<domain> autoconfig.<domain> autodiscover.<domain>;` (or the equivalent per-domain server block), and proxy the requests to the local Web Kestrel listener while preserving the incoming `Host` header. The MTA-STS endpoint selects the hosted domain from `Host`.
5. Install a trusted TLS certificate whose SANs cover `mta-sts.<domain>`, `autoconfig.<domain>`, and `autodiscover.<domain>`. A wildcard may cover the subdomains where the CA and DNS validation permit it. Renew the certificate before expiry and verify nginx presents it for each SNI name.
6. With Cloudflare, create DNS records for these HTTPS names and ensure the proxy/origin path reaches the same nginx server with Host and SNI preserved. Confirm Cloudflare edge and origin certificates cover the names. If proxying interferes with origin SNI or Host routing, use DNS-only for those records or correct the origin configuration. Keep IMAP/SMTP SRV target records DNS-only; Cloudflare's HTTP proxy does not proxy IMAP or SMTP.
7. In Admin → Domains, set `mta_sts_mx` to every inbound MX hostname/pattern in the same order you want advertised. For Cloudflare Email Routing, include the Cloudflare MX targets (for example, matching `*.mx.cloudflare.net`) as well as any other MX target; omitting an actual MX can cause senders enforcing MTA-STS to fail delivery. The visible admin warning repeats this requirement.
8. Set the domain policy mode to `testing` first, publish the matching `_mta-sts` ID and verify `https://mta-sts.<domain>/.well-known/mta-sts.txt` over a valid certificate. Move to `enforce` only after the domain's MX hosts and certificates are ready. Choose `none` to disable hosting.

## Troubleshooting draft

- **Autoconfig or Outlook gives 404:** the submitted address's domain is not in `domains`; mailbox existence is intentionally irrelevant. Confirm the request reaches the expected Web host and URL path.
- **MTA-STS returns 404:** verify the request Host is exactly `mta-sts.<domain>` and the stored mode is `testing` or `enforce`.
- **MTA-STS is served for the wrong domain:** check nginx forwards the original Host header; do not replace it with an upstream alias.
- **Certificate error:** verify SAN coverage for the exact HTTPS discovery host and that Cloudflare/nginx serves the expected certificate by SNI.
- **Clients cannot connect after discovery:** check public host overrides, listener reachability, certificate names, and the SRV target/ports. Make sure IMAP/SMTP targets are not Cloudflare-proxied.
- **DNS ID does not match the active policy:** fetch the current ID in Admin → Domains → DNS Config after the last policy edit, then update `_mta-sts.<domain>` TXT and allow DNS caches to expire.
- **429 from discovery:** the client IP reached the 60-request/minute limit; retry after the fixed window resets.
- **MTA-STS enforcement breaks inbound delivery:** compare every advertised `mx:` pattern with all live MX answers for `<domain>`, including Cloudflare Email Routing targets; update `mta_sts_mx`, publish its new `_mta-sts` ID, and verify in `testing` before returning to `enforce`.
- **Mobile profile is unauthorized:** open the endpoint from an authenticated webmail session that supplies the current tenant and user identity headers.

## Test evidence

- `dotnet build Miautrix.Mail.sln -warnaserror -m:1`: passed, zero warnings.
- UnitTests: 125 passed; SecurityTests: 7 passed; ProtocolTests: 38 passed.
- Targeted IntegrationTests: 47 passed, including the existing required subsets plus all package contracts.
- Autodiscovery integration contracts: 5 passed, including XML structure, settings/ports, POX address echo without an idempotency header, unknown domain, local-part independence, discovery-only limiting, and trusted-proxy IP partitioning. Repeated non-discovery OpenAPI requests remain unaffected by the discovery limiter.
- MTA-STS integration contracts: 4 passed, including exact response bytes for testing/enforce, Host routing, none/unknown 404, and policy-ID rotation on policy update.
- Mobileconfig integration contracts: 3 passed, including missing identity, no password/current address, and cross-tenant rejection.
- Migration integration contract: 1 passed; migration reapplication is a no-op and new domains retain `none` / `604800`.
- Throwaway PostgreSQL database `miautrix_test_autodisc_mig_c5098` was migrated from the immediately preceding migration. A pre-existing domain row received `none` / `604800`; applying the latest migration a second time left one migration-history entry and succeeded.
- Admin suite: 20 passed, including mode selection/max-age and DNS suggestions; admin production build passed.
- Webmail suite: 17 passed, including the iPhone/Mac setup link.
- Full IntegrationTests: 107 passed and 16 failed out of 123. The 16 failures match the repository's documented pre-existing failures; no additional failures were observed.
- Admin production build: passed (Vite emitted its existing large-chunk advisory).

## Deferred and limitations

RFC 8460 report ingestion is not implemented. If `mta_sts_mx` is blank, policy hosting falls back to the configured public SMTP host; domains using external inbound MX services must set the complete pattern list before enabling `enforce`. The Web application currently represents request identity via `X-Tenant-Id` and `X-User-Id` headers; the profile endpoint requires these existing identity headers and enforces tenant/user pairing, but this code does not add a separate token authentication scheme. DNS publication, certificate coverage, and live reverse-proxy behavior remain owner-operated deployment steps.
