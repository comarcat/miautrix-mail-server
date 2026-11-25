# SMTP v2.1 Work Report

## Status

Status: audit work packages 1–4 are implemented and verified. Outbound MX delivery, SPF/DKIM/DMARC evaluation, DSNs and other audit work packages remain outside this task's scope.

## Completed

| Requirement | Result | Evidence |
|---|---|---|
| Submission multi-recipient delivery | One outbound queue item is created per accepted RCPT; each has the same raw message. | Real-socket protocol test verifies three queued recipients and identical message bytes. |
| Inbound multi-tenant routing | Each accepted recipient retains the tenant resolved by inbound RCPT validation and is queued separately under that tenant. | Real-socket protocol test verifies two recipients in different tenants receive separate items with identical message bytes. |
| Recipient count ceiling | Listener accepts up to the configured maximum (default and minimum 100) and responds `452 4.5.3` for excess recipients. | Real-socket test injects a lower fixture-only limit and verifies `452`; boot configuration rejects values below 100. |
| Command line ceiling | Lines over 512 octets including CRLF are drained and receive `500 5.5.2`. | Real-socket test sends a 513-octet command and confirms the session remains usable. |
| DATA line ceiling | Lines over 1000 octets including CRLF are drained; message receives `552 5.3.4` after the DATA terminator. | Real-socket test sends a 1001-octet line and confirms the session remains usable. |
| Total DATA size | DATA content is counted after dot unstuffing; over-limit messages are drained through the terminator, discarded, and receive `552 5.3.4`. | Real-socket test exceeds a small configured test limit and verifies no queue item and continued session. |
| RFC 1870 SIZE | Advertised SIZE matches the configured maximum; `MAIL FROM ... SIZE=n` above it receives `552 5.3.4`. | Real-socket test checks the MAIL rejection and continued session. |
| Idle and DATA timeouts | Configurable line-read deadlines close idle sessions with `421 4.4.2`; DATA timeout queues nothing and closes the session. | Real-socket tests cover both deadlines and EOF mid-DATA. |
| SMTP transaction state | MAIL/RCPT/DATA ordering is enforced; RSET and a new EHLO reset envelope state; successful DATA permits the next transaction. | Real-socket tests cover out-of-order commands, RSET, EHLO reset and repeated transactions. |
| Transparency and pipelining | DATA dot-stuffing is removed; unadvertised pipelined commands are processed and answered in order. PIPELINING is not advertised. | Real-socket tests cover dot unstuffing and ordered NOOP/HELP/QUIT batches. |
| STARTTLS and TLS floor | STARTTLS is available for inbound and submission listeners; post-upgrade state resets; TLS 1.0/1.1 are refused and TLS 1.2/1.3 are enabled. | Real-socket TLS tests cover inbound and submission STARTTLS, post-TLS EHLO, legacy TLS rejection, pre-TLS AUTH refusal, and implicit TLS submission. |
| SMTP commands | VRFY/EXPN return privacy-safe `252 2.1.5`, HELP returns `214`, and unknown commands return `500 5.5.2`. | Real-socket command assertions. |
| SASL PLAIN/LOGIN | AUTH requires TLS and EHLO on submission; base64 and response grammar are validated; `*` cancels. | Real-socket tests cover valid LOGIN, PLAIN grammar, cancel, invalid base64 and AUTH before TLS. |
| Submission headers | Missing Date and Message-ID are added once before per-recipient fanout; supplied headers remain byte-identical. | Real-socket submission checks both headers and identical recipient copies; helper test checks supplied-header preservation. |

## SMTP listener configuration

All supplied values must be positive integers; invalid values stop startup. Defaults are safe and preserve the 25 MiB advertised SIZE ceiling.

| Environment key | Default | Meaning |
|---|---:|---|
| `MIAUTRIX_SMTP_MAX_MESSAGE_SIZE` | `26214400` | Maximum canonical DATA octets and advertised SIZE. |
| `MIAUTRIX_SMTP_MAX_RECIPIENTS` | `100` | Maximum accepted recipients per transaction; configured value must be at least 100; excess RCPT gets `452 4.5.3`. |
| `MIAUTRIX_SMTP_IDLE_TIMEOUT_SECONDS` | `300` | Maximum wait for the next SMTP command line. |
| `MIAUTRIX_SMTP_DATA_TIMEOUT_SECONDS` | `300` | Maximum duration of the complete DATA block. |

## EHLO capabilities

Only `SIZE`, `STARTTLS` and `AUTH LOGIN PLAIN` are advertised. The following lists are exact for the default size and a configured certificate; the server greeting hostname replaces `mail.example`.

| Listener state | Exact reply lines |
|---|---|
| Port 25, before STARTTLS | `250-mail.example`; `250-STARTTLS`; `250-SIZE 26214400`; `250 2.0.0 OK` |
| Port 25, after STARTTLS | `250-mail.example`; `250-SIZE 26214400`; `250 2.0.0 OK` |
| Port 587, before STARTTLS | `250-mail.example`; `250-STARTTLS`; `250-SIZE 26214400`; `250 2.0.0 OK` |
| Port 587, after STARTTLS | `250-mail.example`; `250-AUTH LOGIN PLAIN`; `250-SIZE 26214400`; `250 2.0.0 OK` |
| Port 465, implicit TLS | `250-mail.example`; `250-AUTH LOGIN PLAIN`; `250-SIZE 26214400`; `250 2.0.0 OK` |

HELO receives one `250 2.0.0 <hostname>` reply and no ESMTP extensions. PIPELINING, 8BITMIME and SMTPUTF8 remain unadvertised.

## Compliance matrix (work packages 1–4)

| Feature | RFC § | Requirement level | Status | Evidence | Test coverage | Severity | Action for v2.1 |
|---|---|---|---|---|---|---|---|
| SMTP command and DATA bounds | RFC 5321 §§4.5.3.1–4.5.3.2 | MUST | Implemented | Bounded octet line reader; fixed 25 MiB default; configurable message, recipient and deadlines. | Real socket limits, SIZE, timeout, EOF tests. | Resolved | Keep configured limits valid and advertised SIZE synchronized. |
| EHLO and extension state | RFC 5321 §§4.1.1.1, 4.1.4 | MUST | Implemented | EHLO parses a required argument, emits a multiline port/TLS capability list and resets envelope state. | Exact EHLO arrays per listener mode and TLS state; EHLO reset test. | Resolved | Do not advertise untested extensions. |
| HELO fallback | RFC 5321 §4.1.1.1 | MUST | Implemented | HELO returns one `250 2.0.0` response without extensions. | Exact HELO response test. | Resolved | None. |
| MAIL FROM sequencing and parameters | RFC 5321 §4.1.1.2; RFC 1870 | MUST | Implemented | MAIL requires greeting, rejects duplicate MAIL and enforces SIZE; submission sender policy remains applied. | State, SIZE and sender authorization tests. | Resolved | Full mailbox grammar validation beyond command/path prefix is not claimed. |
| Multi-RCPT routing | RFC 5321 §§4.1.1.3–4.1.1.4 | MUST | Implemented | Per-recipient queue item and tenant association. | Real socket submission 3 recipients and inbound two tenants. | Resolved | Preserve identical raw bytes per recipient. |
| Transaction sequencing/reset | RFC 5321 §§4.1.1, 4.1.4 | MUST | Implemented | Greeting, MAIL, RCPT and DATA states; RSET/new EHLO reset. | Out-of-order matrix, RSET, repeated transaction tests. | Resolved | Keep PIPELINING unadvertised until separately implemented. |
| DATA terminator/EOF | RFC 5321 §4.1.1.4 | MUST | Implemented | Only dot terminator queues; EOF before terminator closes without queueing. | Real socket EOF-mid-DATA and repeated-transaction tests. | Resolved | None. |
| Dot transparency / pipelined reply order | RFC 5321 §4.5.2; RFC 2920 | MUST when applicable | Implemented; PIPELINING N/A | Dot unstuffing; buffered reader processes already received lines sequentially. | Dot-stuff and ordered command batch tests. | Resolved | Do not advertise PIPELINING. |
| RCPT count ceiling | RFC 5321 §4.5.3.1.8 | MUST support at least 100 | Implemented | Default and configured minimum are 100 recipients; overflow returns `452 4.5.3`. | Real socket limit test uses a lower injected test ceiling. | Resolved | Configured production value cannot be set below 100. |
| RFC 1870 SIZE | RFC 1870 §§3–4 | MUST if advertised | Implemented | MAIL SIZE and actual canonical DATA bytes enforced; overflow drained to terminator. | MAIL SIZE, DATA overflow, session recovery tests. | Resolved | Keep EHLO limit equal to runtime configuration. |
| VRFY and EXPN | RFC 5321 §§4.1.1.6–4.1.1.7 | MUST command handling | Implemented | Both return privacy-safe `252 2.1.5`. | Exact real-socket reply assertions. | Resolved | Do not reveal mailbox or tenant existence. |
| HELP, NOOP, QUIT, unknown | RFC 5321 §§4.1.1.8–4.1.1.10, 4.2 | SHOULD/MUST | Implemented | HELP 214, NOOP 250, QUIT 221, unknown 500 with enhanced codes. | Real-socket replies and close/order tests. | Resolved | Preserve enhanced response format. |
| STARTTLS/TLS | RFC 3207; RFC 8996; RFC 9325; RFC 8314 | MUST/SHOULD by port | Implemented | STARTTLS on inbound/submission; implicit TLS on 465; TLS 1.2/1.3 only; resets state. | Real-socket tests on inbound and submission modes; TLS 1.0/1.1 rejection. | Resolved | Certificate lifecycle and deployed hostname validation remain operational follow-up. |
| Enhanced reply codes/commands | RFC 2034; RFC 3463; RFC 5321 | SHOULD / MUST commands | Implemented for listener replies and required commands | VRFY/EXPN 252, HELP 214, enhanced status responses, NOOP/QUIT/unknown. | Real-socket response assertions. | Resolved | Maintain enhanced codes when adding replies. |
| 8BITMIME / SMTPUTF8 / CHUNKING / DSN | RFC 6152; RFC 6531; RFC 3030; RFC 3461 | MUST only if advertised | N/A | No capability keywords are advertised; associated parameter/command behavior is not implemented. | Exact EHLO lists confirm absence. | Resolved | Keep unadvertised until implemented end to end. |
| Port 587 submission / port 465 implicit TLS | RFC 6409; RFC 8314 §3.3 | MUST/SHOULD | Implemented | Dedicated submission modes; 465 TLS starts before greeting. | Real TLS socket submission tests; EHLO capability assertions. | Resolved | Production certificate deployment is not covered by protocol tests. |
| Submission authentication | RFC 4954; RFC 4616; RFC 6409 | MUST if advertised | Implemented for LOGIN and PLAIN | AUTH only after TLS/EHLO; strict base64/PLAIN fields; cancel support. | Real socket valid LOGIN; malformed/cancel PLAIN/LOGIN; pre-TLS refusal. | Resolved | Additional mechanism is not advertised. |
| Submission header fixing | RFC 6409 §8 | MUST | Implemented | Date and Message-ID inserted only when absent before recipient fanout. | Real socket header and byte identity assertions. | Resolved | No trace-header addition is claimed. |
| Sender authorization | Application policy / RFC 6409 §8 | MUST | Preserved | Existing `ISenderAuthorizationService` calls and `550 5.7.1` behavior retained. | Existing real TLS sender-policy tests and integration filter. | Resolved | None within this task. |

## Verification evidence

Latest complete gate after all implemented work: `dotnet build Miautrix.Mail.sln -warnaserror -m:1` passed with 0 warnings/errors; UnitTests 101/101; SecurityTests 7/7; ProtocolTests 32/32; IntegrationTests filter `FullyQualifiedName~SenderAuthorization|FullyQualifiedName~MessageUidAllocator` 10/10. Test environment was loaded from `%USERPROFILE%\.miautrix-test.env` and guarded against `-pro` databases.

## Remaining work

Outbound MX delivery, SPF/DKIM/DMARC authentication policy, DSN generation, and optional SMTP extensions are outside packages 1–4 and remain unimplemented. Certificate deployment, live client interoperability and production environment behavior were not tested.
