# Audit remaining v2.2 — triage and implementation report

This report assigns stable IDs in audit row order; IDs are local to this report because the v2.1 audit did not assign finding IDs. Statuses are assessed against the `b5fbcea` v2.1 baseline and v2.1 handoff; protocol packages explicitly assigned to other workers remain owned there. “Open” rows require code-level disposition in the completion sections below.

## Triage table

| ID | Severity | Protocol / RFC | Finding | Status | Decision for open rows |
|---|---|---|---|---|---|
| AUD-001 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.1, §4.1.4 | EHLO: greeting, multiline capabilities, extension reset | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-002 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.1 | HELO fallback | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-003 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.2 | MAIL FROM and envelope syntax | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-004 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.3 | RCPT TO, multiple recipients, ordering | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-005 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.4 | DATA and terminator | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-006 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.5.2 | Dot transparency | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-007 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.5 | RSET | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-008 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.6 | VRFY | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-009 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.7 | EXPN | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-010 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.8 | HELP | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-011 | Low | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.9 | NOOP | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-012 | Low | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.1.1.10 | QUIT | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-013 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.2 | Unknown commands and reply classes | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-014 | Critical | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.5.3.1 | Reply lines, max command line / message line lengths | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-015 | Critical | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4.5.3.1, §4.5.3.2 | Message size and timeout limits | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-016 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / 5321 §4. pipelining constraints; RFC 2920 §§3-4 | Pipelining semantics | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-017 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 1870 §§3-4 | RFC 1870 SIZE | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-018 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 6152 §§2-3 | RFC 6152 8BITMIME | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-019 | Low | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 3030 §§2-4 | RFC 3030 CHUNKING/BINARYMIME | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-020 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 3207 §§2-4 | RFC 3207 STARTTLS | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-021 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 2034 §2; RFC 3463 §§2-3 | RFC 2034 / 3463 enhanced status codes | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-022 | Medium | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 6531 §§3-4 | SMTPUTF8 | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-023 | Low | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 3461 §§4-5 | DSN ESMTP extension | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-024 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 7208 §2; RFC 6376 §§6-8; RFC 7489 §§6-7; RFC 8617 | SPF / DKIM verification / DMARC / ARC | Owned by inbound authentication | Delegate to inbound SPF/DKIM/DMARC work package. |
| AUD-025 | High | 2.1 SMTP inbound MTA (RFC 5321, port 25) / RFC 3464 §§2-3; RFC 5321 §4.4 | RFC 3464 DSN/bounce generation | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-026 | High | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §§3-4 | 587 submission service | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-027 | Low | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 8314 §3.3 | 465 implicit TLS | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-028 | Medium | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §4; RFC 4954 §4 | STARTTLS on 587; AUTH only after TLS | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-029 | High | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 4954 §4; RFC 4616 §§2-3 | AUTH PLAIN | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-030 | Medium | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / Vendor SASL mechanism (not standardized by RFC 4954) | AUTH LOGIN | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-031 | Low | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 7628 / SASL SCRAM RFCs | XOAUTH2 / SCRAM | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-032 | High | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §8 | Submission message fixing: Date/Message-ID/trace handling | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-033 | Medium | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §8 and application sender policy | Sender policy | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-034 | High | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 6409 §§4,8 | Envelope/recipient authorization and 5.7.1 response | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-035 | Medium | 2.2 SMTP submission (RFC 6409, RFC 8314, RFC 4954, RFC 4616) / RFC 8314 §3; RFC 4954 §4 | 587/465 TLS cert, no plaintext auth | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-036 | High | 2.3 Outbound delivery and queue client / RFC 5321 §5 | SMTP client delivery to recipient MX | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-037 | High | 2.3 Outbound delivery and queue client / RFC 5321 §5.1 | MX fallback A/AAAA | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-038 | High | 2.3 Outbound delivery and queue client / RFC 5321 §4.5.4.1 | Retry schedule / max age | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-039 | High | 2.3 Outbound delivery and queue client / RFC 3207; RFC 5321 §5 | Opportunistic STARTTLS | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-040 | Medium | 2.3 Outbound delivery and queue client / RFC 8461 | MTA-STS | Owned by autodiscovery/MTA-STS hosting | Delegate policy hosting and discovery items; DANE handled in this package. |
| AUD-041 | Medium | 2.3 Outbound delivery and queue client / RFC 7672 §§3-4 | DANE SMTP | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-042 | High | 2.3 Outbound delivery and queue client / RFC 6376 §§3-5; RFC 8463 | DKIM outbound signing | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-043 | High | 2.3 Outbound delivery and queue client / RFC 3464 | Bounce generation | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-044 | Medium | 2.3 Outbound delivery and queue client / Vendor HTTP contract; RFC semantic equivalence not guaranteed | Cloudflare API transport path | Open | Review current code and fix if still applicable; otherwise record why deferred. |
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
| AUD-062 | Medium | 2.6 Calendar, contacts, WebDAV, iCalendar, iTIP/iMIP / RFC 5545 | iCalendar import/export | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-063 | Medium | 2.6 Calendar, contacts, WebDAV, iCalendar, iTIP/iMIP / RFC 5546 / RFC 6047 | iTIP/iMIP scheduling | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-064 | Low | 2.7 Client autoconfiguration / Product convention | Mozilla autoconfig XML | Owned by autodiscovery/MTA-STS hosting | Delegate policy hosting and discovery items; DANE handled in this package. |
| AUD-065 | Low | 2.7 Client autoconfiguration / Product protocol | Microsoft Autodiscover | Owned by autodiscovery/MTA-STS hosting | Delegate policy hosting and discovery items; DANE handled in this package. |
| AUD-066 | Low | 2.7 Client autoconfiguration / RFC 6186 §§3-4 | RFC 6186 SRV discovery | Owned by autodiscovery/MTA-STS hosting | Delegate policy hosting and discovery items; DANE handled in this package. |
| AUD-067 | High | 2.8 TLS posture across in-scope listeners / RFC 8996 §§3-4 | TLS 1.0/1.1 disabled | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-068 | Medium | 2.8 TLS posture across in-scope listeners / RFC 9325 §§3-4 | RFC 9325 TLS recommendations | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-069 | High | 2.8 TLS posture across in-scope listeners / Deployment/application | Cert load and lifecycle | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |
| AUD-070 | High | 2.8 TLS posture across in-scope listeners / RFC 3207 | Port-25 opportunistic TLS | Open | Review current code and fix if still applicable; otherwise record why deferred. |
| AUD-071 | Low | 2.8 TLS posture across in-scope listeners / RFC 4954 §4 / project security rule | SMTP AUTH encryption | Fixed in v2.1 | Verify against v2.1 implementation and regression tests; do not reimplement. |

## Current-code triage notes

Baseline: `b5fbcea`. The v2.1 handoff records SMTP WP1 and outbound WP2 as merged. Current source includes `DirectSmtpMailTransport` and MTA-STS behavior. The older audit rows describing missing direct SMTP and MTA-STS must therefore be checked against v2.1 code and regression tests.

## Decisions / v2.2 work


