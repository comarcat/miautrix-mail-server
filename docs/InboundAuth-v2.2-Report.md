# Inbound SPF, DKIM and DMARC (v2.2)

## What is implemented

Inbound authentication runs before the message is queued on SMTP port 25 and on the authenticated Cloudflare webhook. `IInboundAuthenticationService` returns SPF, each DKIM signature, DMARC policy/disposition, aligned identifiers and an RFC 8601 Authentication-Results value. The service uses an injected DNS resolver and one 10-second per-message cancellation budget. No database migration is needed; results are stored in the prepended header.

### Compliance matrix

| RFC | Sections / requirement | Status and implementation |
|---|---|---|
| RFC 7208 | §2.3 null reverse path | HELO SPF identity is evaluated when MAIL FROM is `<>`. |
| RFC 7208 | §4.6.4 DNS limits | Ten DNS-causing SPF terms and two void lookups are enforced; DNS timeout produces temperror. |
| RFC 7208 | §5, §6 mechanisms/qualifiers | `all`, `include`, `a`, `mx`, `ptr`, `ip4`, `ip6`, `exists`, qualifiers and redirect are supported; `exp` is accepted and ignored. Multiple SPF records are permerror. |
| RFC 7208 | §7 macros | Macro letters, transformers, reversal, delimiters and URL escaping are supported, including validated `%{p}`. |
| RFC 6376 | §3, §6 signature verification | MimeKit verifies all DKIM signatures against the raw message before this service adds headers. DNS keys are found through the injected resolver. |
| RFC 8301 | RSA algorithms and key size | `rsa-sha1` is never accepted; RSA key size below 1024 bits is rejected. Expired `x=` fails. |
| RFC 8463 | Ed25519-SHA256 | Verification is delegated to MimeKit. Unsupported algorithms cannot be accepted as a pass. |
| RFC 6376 | `l=` body length | MimeKit verifies the declared body length; Authentication-Results reason calls out partial body coverage risk. |
| RFC 7489 | §3, §5, §6 alignment/policy | Relaxed/strict SPF and DKIM alignment, PSL organizational-domain fallback, `p`, `sp`, `pct`, no record, and multiple From domains are handled. `rua`/`ruf` are parsed as inert tags. |
| RFC 7489 | Multiple records | Multiple DMARC records produce permerror. |
| RFC 8601 | §5 header handling | Only Authentication-Results with the configured authserv-id is removed; one local result is prepended before queue storage. |

The embedded Public Suffix List is sourced from [publicsuffix.org](https://publicsuffix.org/list/public_suffix_list.dat), snapshot version `2026-11-25_10-02-46_UTC`, and retains the upstream MPL 2.0 notice. It is embedded; there is no runtime download.

## Settings and behavior

- `MIAUTRIX_INBOUND_AUTH_MODE`: optional `off`, `monitor` (default), or `enforce`; any other value fails startup.
- `MIAUTRIX_HOSTNAME`: used as the Authentication-Results authserv-id; when absent, the process machine name is used. Whitespace is rejected.
- `monitor` records the result, logs only the path and authentication domains/results (never bodies), and never rejects or reroutes.
- `enforce` rejects only DMARC fail with effective reject policy after `pct`, at the end of SMTP DATA before queueing. DMARC quarantine routes to an existing Junk folder, otherwise Inbox with `\\Flagged`. SPF failure alone never rejects. Authentication temperror never rejects.
- The authenticated Cloudflare webhook accepts an optional `X-Miautrix-Cloudflare-Authentication-Results` result, or a Cloudflare Authentication-Results field in the raw message. The checked-in Worker contract sends the envelope headers and raw MIME; this new header is optional so legacy requests continue to work. SPF is used only when the result identifies Cloudflare; without an IP or such result SPF is none and DMARC can still pass via DKIM. In enforce mode, a DMARC reject is returned as HTTP 403 with `inbound_processing_failed`.
- Submission and internal delivery are not evaluated.

## DNS records for hosted domains

Publish records for each hosted sending domain and selector. Replace every placeholder with the actual system values; do not copy placeholders literally.

1. SPF at the domain apex: `v=spf1 ip4:<outbound-ip> mx -all` (include `ip6:<outbound-ipv6>` if applicable). Enumerate every authorized outbound sender and keep within SPF lookup limits.
2. DKIM at `<selector>._domainkey.<hosted-domain>`: `v=DKIM1; k=rsa; p=<base64-public-key>` (or the supported Ed25519 key format). Use a unique selector and rotate keys operationally.
3. DMARC at `_dmarc.<hosted-domain>`: begin with `v=DMARC1; p=none; pct=100; rua=mailto:<aggregate-report-address>`. Review aligned SPF and DKIM results before changing `p` to `quarantine` or `reject`. Set `sp` when subdomains need a distinct policy. Ensure the From domain aligns with the SPF MAIL FROM or a passing DKIM `d=` domain.

For hosted domains, inbound port 25 applies the published DMARC policy to unauthenticated mail claiming that domain. Users' authenticated submission and internal mail remain exempt.

## Troubleshooting draft

- `spf=temperror`: check DNS availability, resolver timeouts and the number of DNS lookups. Do not treat transient DNS errors as rejection.
- `spf=permerror`: check for multiple `v=spf1` TXT records, invalid mechanisms, or lookup/void-limit exhaustion.
- `dkim=fail`: confirm the selector TXT record, public key, body/header integrity, signature expiry and minimum RSA key size.
- `dmarc=fail`: check the visible From domain, SPF MAIL FROM alignment, DKIM `d=` alignment, strict versus relaxed tags, and subdomain policy.
- `dmarc=none`: publish a DMARC record at the From domain or organizational domain.
- To observe without affecting delivery, keep `MIAUTRIX_INBOUND_AUTH_MODE=monitor`; only move to enforce after reviewing reports and tests.

## Test evidence

Unit tests use an in-memory fake DNS resolver; they do not call public DNS. SPF has more than 30 table cases covering all mechanisms, qualifiers, macro forms, include/redirect, limits, duplicate records, timeout, HELO/null reverse path and IPv6. Mechanism examples were adapted from the [IETF SPF test suite](https://github.com/ietf-wg-spfbis/spf-test-suite). DKIM fixtures are signed in-test using MimeKit and generated RSA keys, including tampering, weak/obsolete signatures, expiry, missing key, and multiple signatures. DMARC tests cover strict/relaxed SPF/DKIM alignment, organizational fallback, subdomain policy, percentage sampling, missing and multiple records, and multiple From domains.

Validated during implementation: SPF evaluator tests 39/39; DKIM + DMARC unit tests 16/16, including DNS timeout; full unit suite 181/181; security tests 7/7; full protocol tests 41/41; focused SMTP protocol tests 4/4; targeted integration suite 39/39; inbound webhook and DMARC quarantine tests 3/3; solution build with warnings as errors (zero warnings/errors). Full integration suite: 99/112 passed, 13 failed. These failures are in the existing CLI/storage configuration, EF in-memory UID allocation and Sent-folder fixture groups documented under `ERRORS_AND_ISSUES.md` §13.18; the inbound dispatcher fixtures now seed Inbox and all five dispatcher tests pass. No failure occurred in the inbound authentication tests.

## Deferred

The optional webmail sender-warning indicator was skipped to keep the change focused on the inbound authentication and delivery path. No persistence schema was added because the Authentication-Results header carries the recorded results.
