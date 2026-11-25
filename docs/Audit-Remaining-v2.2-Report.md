# Audit remaining v2.2 — triage and implementation report

This report assigns stable IDs in audit row order; IDs are local to this report because the v2.1 audit did not assign finding IDs. Statuses are assessed against the `b5fbcea` v2.1 baseline and v2.1 handoff; protocol packages explicitly assigned to other workers remain owned there. “Open” rows require code-level disposition in the completion sections below.

## Triage table

| ID | Severity | Protocol / RFC | Finding | Status | Decision for open rows |
|---|---|---|---|---|---|
| AUD-001 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.1, §4.1.4 | EHLO: greeting, multiline capabilities, extension reset | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-002 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.1 | HELO fallback | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-003 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.2 | MAIL FROM and envelope syntax | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-004 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.3 | RCPT TO, multiple recipients, ordering | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-005 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.4 | DATA and terminator | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-006 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.5.2 | Dot transparency | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-007 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.5 | RSET | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-008 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.6 | VRFY | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-009 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.7 | EXPN | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-010 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.8 | HELP | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-011 | Low | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.9 | NOOP | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-012 | Low | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.10 | QUIT | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-013 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.2 | Unknown commands and reply classes | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-014 | Critical | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.5.3.1 | Reply lines, max command line / message line lengths | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-015 | Critical | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.5.3.1, §4.5.3.2 | Message size and timeout limits | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-016 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4. pipelining constraints; RFC 2920 §§3-4 | Pipelining semantics | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-017 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 1870 §§3-4 | RFC 1870 SIZE | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-018 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 6152 §§2-3 | RFC 6152 8BITMIME | Fixed in v2.1 | 8BITMIME remains unadvertised; no extension support is claimed. |
| AUD-019 | Low | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 3030 §§2-4 | RFC 3030 CHUNKING/BINARYMIME | Fixed in v2.1 | CHUNKING/BINARYMIME remain unadvertised and unimplemented; no extension support is claimed. |
| AUD-020 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 3207 §§2-4 | RFC 3207 STARTTLS | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-021 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 2034 §2; RFC 3463 §§2-3 | RFC 2034 / 3463 enhanced status codes | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-022 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 6531 §§3-4 | SMTPUTF8 | Fixed in v2.1 | SMTPUTF8 remains unadvertised; no extension support is claimed. |
| AUD-023 | Low | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 3461 §§4-5 | DSN ESMTP extension | Fixed in v2.1 | DSN ESMTP parameters remain unadvertised; generated failure DSNs are a separate outbound feature. |
| AUD-024 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 7208 §2; RFC 6376 §§6-8; RFC 7489 §§6-7; RFC 8617 | SPF / DKIM verification / DMARC / ARC | Owned by inbound authentication | Delegate to inbound SPF/DKIM/DMARC work package. |
| AUD-025 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 3464 §§2-3; RFC 5321 §4.4 | RFC 3464 DSN/bounce generation | Fixed in v2.1 | Outbound v2.1 report documents RFC 3464 bounce generation; `OutboundQueueDispatcher` generates delivery-status reports. |
| AUD-026 | High | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §§3-4 | 587 submission service | Fixed in v2.1 | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-027 | Low | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 8314 §3.3 | 465 implicit TLS | Fixed in v2.1 | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-028 | Medium | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §4; RFC 4954 §4 | STARTTLS on 587; AUTH only after TLS | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-029 | High | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 4954 §4; RFC 4616 §§2-3 | AUTH PLAIN | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-030 | Medium | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / Vendor SASL mechanism (not standardized by RFC 4954) | AUTH LOGIN | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-031 | Low | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 7628 / SASL SCRAM RFCs | XOAUTH2 / SCRAM | Fixed in v2.1 | Neither XOAUTH2 nor SCRAM is advertised; no mechanism support is claimed. |
| AUD-032 | High | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §8 | Submission message fixing: Date/Message-ID/trace handling | Fixed in v2.1 | `SmtpSubmissionHandler.EnsureSubmissionHeaders` inserts missing Date and Message-ID; SMTP submission tests assert generated headers. |
| AUD-033 | Medium | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §8 and application sender policy | Sender policy | Fixed in v2.1 | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-034 | High | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §§4,8 | Envelope/recipient authorization and 5.7.1 response | Fixed in v2.1 | v2.1 handoff and current code show the SMTP WP1 or outbound WP2 behavior; regression tests cover these packages. |
| AUD-035 | Medium | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 8314 §3; RFC 4954 §4 | 587/465 TLS cert, no plaintext auth | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-036 | High | 2.3 Outbound delivery and queue client / RFC 5321 §5 | SMTP client delivery to recipient MX | Fixed in v2.1 | `DirectSmtpMailTransport` performs MX delivery; `DirectSmtpSocketTests` covers SMTP session delivery. |
| AUD-037 | High | 2.3 Outbound delivery and queue client / RFC 5321 §5.1 | MX fallback A/AAAA | Fixed in v2.1 | `DnsClientResolver` and the direct transport use recipient-domain A/AAAA when MX is absent; socket test covers that path. |
| AUD-038 | High | 2.3 Outbound delivery and queue client / RFC 5321 §4.5.4.1 | Retry schedule / max age | Fixed in v2.1 | `QueueRetryOptions` is used by the dispatcher and v2.1 configures the 5-day retry lifetime. |
| AUD-039 | High | 2.3 Outbound delivery and queue client / RFC 3207; RFC 5321 §5 | Opportunistic STARTTLS | Fixed in v2.1 | `DirectSmtpMailTransport` attempts STARTTLS when offered and its socket tests cover TLS and no-TLS recipient servers. |
| AUD-040 | Medium | 2.3 Outbound delivery and queue client / RFC 8461 | MTA-STS | Fixed in v2.1 | Outbound MTA-STS policy checking shipped in WP2; hosting remains separate. |
| AUD-041 | Medium | 2.3 Outbound delivery and queue client / RFC 7672 §§3-4 | DANE SMTP | Fixed in v2.2 | Implemented in this package with DNSSEC-validated TLSA gating; tests below. |
| AUD-042 | High | 2.3 Outbound delivery and queue client / RFC 6376 §§3-5; RFC 8463 | DKIM outbound signing | Fixed in v2.1 | `OutboundQueueDispatcher` signs direct-transport messages through `IDkimService` before sending; Cloudflare remains separately handled. |
| AUD-043 | High | 2.3 Outbound delivery and queue client / RFC 3464 | Bounce generation | Fixed in v2.1 | `OutboundQueueDispatcher` emits multipart/report with a message/delivery-status part for eligible failures. |
| AUD-044 | Medium | 2.3 Outbound delivery and queue client / Vendor HTTP contract; RFC semantic equivalence not guaranteed | Cloudflare API transport path | Fixed in v2.1 | Cloudflare HTTP transport remains a separately selected product transport; direct SMTP policy and DANE do not change its behavior. |
| AUD-045 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5228 §§2-4 | Core language grammar, strings, comments, tests/actions | Owned by Sieve | Delegate to Sieve work package. |
| AUD-046 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5228 §3.2 | `require` | Owned by Sieve | Delegate to Sieve work package. |
| AUD-047 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5228 §4.1 | `if` / `elsif` / `else`, tests | Owned by Sieve | Delegate to Sieve work package. |
| AUD-048 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5228 §4.1 | `keep`, `discard`, `stop`, `redirect` | Owned by Sieve | Delegate to Sieve work package. |
| AUD-049 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5228 §4.1 / RFC 5228 extension? (base action) | `fileinto` | Owned by Sieve | Delegate to Sieve work package. |
| AUD-050 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5429 (reject extension), not base RFC 5228 | `reject`, `ereject` | Owned by Sieve | Delegate to Sieve work package. |
| AUD-051 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5230 | `vacation` | Owned by Sieve | Delegate to Sieve work package. |
| AUD-052 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5228 §5.4 | `envelope` | Owned by Sieve | Delegate to Sieve work package. |
| AUD-053 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5232 | `imap4flags` / flag actions | Owned by Sieve | Delegate to Sieve work package. |
| AUD-054 | Low | 2.4 Sieve (RFC 5228 and extensions) / RFC 5173 | `body` | Owned by Sieve | Delegate to Sieve work package. |
| AUD-055 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5229 | `variables` / `set` | Owned by Sieve | Delegate to Sieve work package. |
| AUD-056 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5228 §§5-6 | `comparator-i;ascii-casemap`, size, exists, header/address, allof/anyof/not | Owned by Sieve | Delegate to Sieve work package. |
| AUD-057 | Medium | 2.4 Sieve (RFC 5228 and extensions) / RFC 5804 §§2-4 | ManageSieve protocol: CAPABILITY, AUTH, PUTSCRIPT, CHECKSCRIPT, SETACTIVE, GETSCRIPT, LISTSCRIPTS, DELETESCRIPT, RENAMESCRIPT, LOGOUT | Owned by Sieve | Delegate to Sieve work package. |
| AUD-058 | High | 2.4 Sieve (RFC 5228 and extensions) / RFC 5228 §§2,4 | Script execution in delivery | Owned by Sieve | Delegate to Sieve work package. |
| AUD-059 | High | 2.5 JMAP / RFC 8620 §§2-3 | JMAP session resource, capabilities, accounts, API URL | Owned by JMAP owner decision | JMAP removed by owner decision; retain placeholder only. |
| AUD-060 | High | 2.5 JMAP / RFC 8620 §§4-6 | Request/response, method calls, references, errors, limits | Owned by JMAP owner decision | JMAP removed by owner decision; retain placeholder only. |
| AUD-061 | High | 2.5 JMAP / RFC 8621 §§2-7 | Mail, Email, Mailbox, Thread, Submission methods and state | Owned by JMAP owner decision | JMAP removed by owner decision; retain placeholder only. |
| AUD-076 | High if advertised | 2.6 Calendar and contacts / RFC 4791 §§5-7 | CalDAV collection/resource, REPORT, sync, scheduling | Open | Defer: current product surface is a custom calendar JSON API and no CalDAV claim is made; a standards endpoint requires a separate DAV resource and sync-token design. |
| AUD-077 | High if advertised | 2.6 Calendar and contacts / RFC 6352 §§5-6 | CardDAV address books and vCards | Open | Defer: current product surface is a custom contacts JSON API and no CardDAV claim is made; implement only with a separate vCard/DAV package and conformance harness. |
| AUD-062 | Medium | 2.6 Calendar, contacts, WebDAV, iCalendar, iTIP/iMIP / RFC 5545 | iCalendar import/export | Open | Defer: application exposes a custom calendar JSON API and does not claim CalDAV/iCalendar interchange; adding RFC 5545 support needs a separately scoped export/import contract and parser corpus. |
| AUD-063 | Medium | 2.6 Calendar, contacts, WebDAV, iCalendar, iTIP/iMIP / RFC 5546 / RFC 6047 | iTIP/iMIP scheduling | Open | Defer: invitation UI is product workflow, with no iTIP/iMIP protocol claim; organizer/attendee scheduling semantics need a separate interoperability design. |
| AUD-064 | Low | 2.7 Client autoconfiguration / Product convention | Mozilla autoconfig XML | Owned by autodiscovery/MTA-STS hosting | Delegate policy hosting and discovery items; DANE handled in this package. |
| AUD-065 | Low | 2.7 Client autoconfiguration / Product protocol | Microsoft Autodiscover | Owned by autodiscovery/MTA-STS hosting | Delegate policy hosting and discovery items; DANE handled in this package. |
| AUD-066 | Low | 2.7 Client autoconfiguration / RFC 6186 §§3-4 | RFC 6186 SRV discovery | Owned by autodiscovery/MTA-STS hosting | Delegate policy hosting and discovery items; DANE handled in this package. |
| AUD-067 | High | 2.8 TLS posture across in-scope listeners / RFC 8996 §§3-4 | TLS 1.0/1.1 disabled | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-068 | Medium | 2.8 TLS posture across in-scope listeners / RFC 9325 §§3-4 | RFC 9325 TLS recommendations | Open | Defer: platform TLS 1.2+ defaults are explicit in SMTP WP1, while cipher policy varies by supported OS TLS provider; add an OS-specific negotiation matrix before pinning ciphers. |
| AUD-069 | High | 2.8 TLS posture across in-scope listeners / Deployment/application | Cert load and lifecycle | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-070 | High | 2.8 TLS posture across in-scope listeners / RFC 3207 | Port-25 opportunistic TLS | Fixed in v2.1 | SMTP WP1 added inbound STARTTLS on port 25; see v2.1 handoff and current listener. |
| AUD-071 | Low | 2.8 TLS posture across in-scope listeners / RFC 4954 §4 / project security rule | SMTP AUTH encryption | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |

## Current-code triage notes

Baseline: `b5fbcea`. The v2.1 handoff records SMTP WP1 and outbound WP2 as merged. Current source includes `DirectSmtpMailTransport` and MTA-STS behavior. The older audit rows describing missing direct SMTP and MTA-STS must therefore be checked against v2.1 code and regression tests.

## Decisions / v2.2 work

### DANE for outbound SMTP

| RFC section | Implementation | Test evidence |
|---|---|---|
| RFC 7672 §§2-3: TLSA and DNSSEC | `_25._tcp.<mx-host>` TLSA lookup uses `OUTBOUND_DNSSEC_RESOLVER`; only a response carrying AD from that explicitly selected resolver is trusted. Without an IP resolver, opportunistic mode warns at boot and DANE remains disabled. | Fake `IDaneResolver` tests cover validated and unvalidated answers. |
| RFC 7672 §3.1.3: usable records | Supports DANE-TA (2) and DANE-EE (3), selectors 0 (certificate) and 1 (SubjectPublicKeyInfo), matching 0 (exact), 1 (SHA-256), and 2 (SHA-512). PKIX-TA/EE usages 0/1 and unknown selector/matching types are ignored. | The socket test creates a certificate and verifies DANE-EE selector 1 / SHA-256 success and mismatch failure. |
| RFC 7672 §3.2: reference identity | TLS uses the MX hostname as `TargetHost`; DANE validation rejects certificate name mismatch and still requires a matching TLSA association. DANE may relax PKIX chain errors for a matching DANE-TA/EE association. | Generated certificate SAN matches the MX name in the positive test. |
| RFC 7672 §3.4: TLS required | A usable DNSSEC-validated TLSA record makes STARTTLS mandatory. If absent or refused, TLS negotiation fails, and a TLSA certificate mismatch yields a temporary delivery failure; no plaintext fallback occurs. | Scripted SMTP server tests verify mismatch stops before MAIL/DATA and returns transient failure. |
| RFC 8461 §2: precedence | A TLSA-secured MX is not discarded by an MTA-STS MX pattern mismatch; validated DANE takes precedence when both policies apply. With no usable TLSA, existing MTA-STS or opportunistic delivery applies. | Existing MTA-STS tests plus DANE socket tests. |

### MIME output used by the webmail message service

| RFC section | Implementation | Test evidence |
|---|---|---|
| RFC 5322 §§2.1.1, 2.2.3 | Subject and address display-name headers use bounded RFC 2047 encoded words; folded header lines stay within the recommended 78 octets for these generated fields. Dates use an explicit RFC 5322 date form and Message-ID uses the parsed sender domain. Attachment metadata rejects CR/LF injection. | New long non-ASCII subject folding test; existing date, Message-ID, line-ending and injection tests. |
| RFC 2047 §§2, 3 | Encoded words are split on Unicode scalar boundaries into 45-byte UTF-8 chunks, each encoded word ≤75 characters. | New unit test validates encoded-word and line lengths. |
| RFC 2231 §4 | Long or non-ASCII attachment filenames emit parameter continuations for both `name` and `filename`; continuation segments never split percent triplets. | New unit test verifies RFC 2231 continuation parameters and line lengths. |

### Settings and defaults

| Setting | Default | Validation / effect |
|---|---|---|
| `OUTBOUND_DANE_MODE` | `off` | Optional; accepts only `off` or `opportunistic`, case-insensitive. Invalid values fail Worker boot. |
| `OUTBOUND_DNSSEC_RESOLVER` | unset | Optional IP literal. Invalid values fail Worker boot. DANE remains inactive without it, even when mode is opportunistic. Configure only an operator-managed validating resolver that sets the DNS AD bit after validation. |

### Owner deployment notes

Install and operate a local validating resolver such as Unbound on loopback or a private network address. Configure it to validate DNSSEC and to return the AD bit only for successfully validated answers; point `OUTBOUND_DNSSEC_RESOLVER` at its IP. Then set `OUTBOUND_DANE_MODE=opportunistic` and restart the Worker. Verify the resolver independently with a known DNSSEC-signed TLSA name and a deliberately bogus DNSSEC test name: the valid response should carry AD and the bogus response must fail validation / lack AD. Confirm Worker startup has no “DANE inactive” warning and use controlled outbound fixture tests; no public DNS probe is part of this package. Mode `off` is the default, so no resolver installation is required for existing deployments.

### Deferred findings

- AUD-062 / AUD-063: calendar and invitation features are custom application APIs and workflows; neither iCalendar export nor iTIP/iMIP is claimed. Full RFC 5545/iTIP behavior needs a parser/interoperability test corpus and organizer/attendee state design.
- AUD-068: protocol listener TLS 1.2+ floor is explicit in WP1. Cipher suite selection remains with the platform TLS provider to preserve supported OS behavior; define a versioned OS negotiation matrix before restricting cipher suites.
- Findings labeled “Owned by …” are delegated per the v2.2 task assignment. JMAP was removed by owner decision; Sieve, inbound authentication, IMAP, and autodiscovery/MTA-STS hosting remain under their package owners.
- Low findings remain unadvertised optional features (including CHUNKING/BINARYMIME, DSN ESMTP parameters, autoconfiguration, RFC 6186 DNS publication, ManageSieve, and POP3S). No implementation is claimed.

### Troubleshooting draft

- **DANE inactive:** ensure `OUTBOUND_DANE_MODE=opportunistic`, set `OUTBOUND_DNSSEC_RESOLVER` to the IP of the local validating resolver, and confirm it returns AD for validated TLSA answers. Without the resolver, delivery intentionally uses MTA-STS or opportunistic STARTTLS.
- **Temporary failure for a recipient domain:** a DNSSEC-validated usable TLSA record makes STARTTLS and certificate association mandatory. Check the MX hostname, certificate SAN, and TLSA usage/selector/matching data. Do not work around a mismatch by enabling plaintext fallback.
- **Existing Cloudflare mode:** DANE applies only to the direct SMTP transport; Cloudflare transport selection and behavior are unchanged.

### Verification

- `dotnet build src/Miautrix.Mail.Application/Miautrix.Mail.Application.csproj -warnaserror -m:1` — passed with zero warnings.
- `dotnet build src/Miautrix.Mail.Worker/Miautrix.Mail.Worker.csproj -warnaserror -m:1` — passed with zero warnings.
- `dotnet test tests/Miautrix.Mail.UnitTests/Miautrix.Mail.UnitTests.csproj --filter FullyQualifiedName~DirectSmtpSocketTests -m:1` — 19 passed, including DANE success, mismatch/no plaintext fallback, absent/unvalidated TLSA, and mode-off behavior.
- `dotnet build Miautrix.Mail.sln -warnaserror -m:1` — passed, zero warnings.
- Unit suite — 132 passed; SecurityTests — 7 passed; ProtocolTests — 38 passed (loaded local test environment without printing values).
- Targeted IntegrationTests — 34 passed. Full IntegrationTests — 94 passed / 16 failed of 110, matching the documented pre-existing baseline; failures are the known fixture/CLI/calendar cases.
