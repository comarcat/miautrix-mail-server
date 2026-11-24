# Miautrix Mail Server — IMAP Protocol Compatibility Bug Report

**Project:** Miautrix Mail Server  
**Component:** IMAP Server  
**Type:** Bug Report / Corrective Development Task  
**Priority:** P1 — High  
**Status:** Confirmed defects requiring investigation and correction  
**Date:** November 23, 2026

---

## 1. Executive Summary

During the integration of Miautrix Mail Server with Hermes Agent and Himalaya CLI, several IMAP protocol compatibility defects were identified.

SMTP message submission and IMAP authentication were successfully tested. However, standard IMAP operations failed, preventing external clients from listing mailboxes and retrieving messages reliably.

The server advertises IMAP4rev1 compatibility but rejects several standard commands and produces an incorrectly formatted response during message retrieval.

**The primary objective is to correct the IMAP implementation without introducing regressions in the existing SMTP, authentication, or mailbox functionality.**

## 2. Test Environment


| Component          | Configuration                             |
| ------------------ | ----------------------------------------- |
| Mail Server        | Miautrix Mail Server (custom development) |
| Server Address     | 10.11.1.51                                |
| IMAP               | TCP 993 / TLS                             |
| SMTP Submission    | TCP 587 / STARTTLS                        |
| Client OS          | Debian Linux LXC                          |
| Python IMAP Client | Python 3.13 `imaplib`                     |
| Himalaya           | v2.2.1                                    |
| Hermes Agent       | v0.23.3                                   |


The IMAP client successfully authenticated using an account credential separate from the mailbox email address.

TLS certificate validation completed successfully after configuring the appropriate certificate authority.

## 3. Confirmed Test Results


| IMAP Command | Result       | Details                                                            |
| ------------ | ------------ | ------------------------------------------------------------------ |
| CAPABILITY   | PASS         | Server advertises IMAP4REV1, AUTH=PLAIN, SASL-IR, LITERAL+, ENABLE |
| LOGIN        | PASS         | Authentication successful                                          |
| SELECT INBOX | PASS         | Returned 15 messages                                               |
| LIST         | FAIL         | BAD Unsupported command LIST                                       |
| EXAMINE      | FAIL         | BAD Unsupported command EXAMINE                                    |
| SEARCH       | FAIL         | BAD Unsupported command SEARCH                                     |
| FETCH        | FAIL         | Malformed response detected by Python imaplib                      |
| LOGOUT       | INCONCLUSIVE | Parser failure observed after malformed FETCH response             |


These results were reproduced using standard IMAP client operations. Himalaya also failed to list mailboxes and decode message retrieval responses.

## 4. Defect IMAP-001 — LIST Command Not Supported

**Observed response:**

```
IMAP LIST failed: BAD Unsupported command LIST
```

**Expected behavior:**

The server must support standard IMAP4rev1 LIST operations and return mailbox information using correctly formatted untagged and tagged responses.

Example:

```
C001 LIST "" "*"
* LIST (\HasNoChildren) "/" "INBOX"
C001 OK LIST completed
```

**Required correction:**

- Implement LIST command parsing and dispatch.
- Support mailbox reference names and mailbox patterns.
- Return mailbox names and hierarchy delimiters correctly.
- Handle nonexistent mailboxes and malformed arguments.
- Validate compatibility with standard IMAP clients.

## 5. Defect IMAP-002 — EXAMINE Command Not Supported

**Observed response:**

```
IMAP EXAMINE failed:
BAD Unsupported command EXAMINE
```

**Expected behavior:**

EXAMINE must allow a mailbox to be selected in read-only mode.

Example:

```
C002 EXAMINE "INBOX"
* 15 EXISTS
* FLAGS (\Seen \Answered \Flagged \Deleted \Draft)
C002 OK [READ-ONLY] EXAMINE completed
```

**Required correction:**

- Implement EXAMINE using the existing mailbox selection logic where appropriate.
- Maintain read-only mailbox state.
- Reject subsequent operations that would modify mailbox state.
- Include the appropriate READ-ONLY response code.
- Ensure messages are not marked as read by read-only retrieval operations.

## 6. Defect IMAP-003 — SEARCH Command Not Supported

**Observed response:**

```
IMAP SEARCH failed:
BAD Unsupported command SEARCH
```

The error was reproduced after a successful SELECT operation.

**Expected behavior:**

A valid SEARCH request must return matching message sequence numbers.

Example:

```
C003 SEARCH ALL
* SEARCH 1 2 3 4 5
C003 OK SEARCH completed
```

**Required correction:**

Implement and validate standard IMAP4rev1 search behavior, including:

- ALL
- SEEN / UNSEEN
- FROM
- TO
- SUBJECT
- SINCE / BEFORE
- UID
- Compound search criteria

The implementation must correctly distinguish message sequence numbers from message UIDs.

Unsupported or malformed search criteria must return appropriate protocol errors rather than incorrect results.

## 7. Defect IMAP-004 — Malformed FETCH Response

**Severity:** Critical for message retrieval.

**Observed result:**

```
FETCH => unexpected response:
b'Received: from hosting.miautrix.local (38.59.164.121)'
```

A subsequent response produced:

```
unexpected response:
b'by cloudflare-email.net (cloudflare) ...'
```

The Python IMAP parser aborted after encountering unexpected raw email header content.

Himalaya also reported:

```
IMAP FETCH failed:
Decode IMAP response error
```

### Suspected Root Cause

The server may be returning message header data without correctly framing the response as an IMAP FETCH literal.

This is a hypothesis based on observed client errors. The actual server response serializer must be inspected to establish the root cause.

### Expected IMAP Response Structure

A valid response using a literal must follow the IMAP protocol framing rules:

```
* 1 FETCH (UID 123 BODY[HEADER] {<byte-count>}
<exact header bytes>
)
C004 OK FETCH completed
```

The placeholder `<byte-count>` represents the exact byte length of the returned literal.

### Required Correction

Inspect and correct:

1. FETCH command parsing and message data retrieval.
2. Literal length calculation using byte counts rather than character counts.
3. Correct placement of CRLF delimiters.
4. Correct response parentheses and tagged completion.
5. Handling of multiline email headers.
6. Header folding and MIME message content.
7. Support for BODY.PEEK without implicitly setting the Seen flag.
8. Handling of multiple FETCH data items in one request.
9. UID-based retrieval.
10. Correct protocol stream synchronization following FETCH.

The server must never expose raw message lines outside the framing expected by the IMAP protocol.

## 8. Defect IMAP-005 — Protocol Compliance Review

The server currently advertises:

```
IMAP4REV1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE
```

However, mandatory IMAP4rev1 functionality is incomplete or failing.

**Required action:**

Conduct an IMAP protocol compliance review against RFC 3501.

Verify that advertised capabilities match implemented behavior.

Particular attention should be given to command dispatch, state transitions, mailbox selection, message retrieval, UID handling, response formatting, and authentication.

Do not simply suppress unsupported command errors or modify the capability advertisement as a substitute for implementing required IMAP4rev1 functionality.

## 9. Regression and Compatibility Testing

The development agent must create automated tests covering:

- Successful and failed authentication.
- CAPABILITY response.
- LIST with mailbox patterns.
- SELECT and EXAMINE.
- SEARCH with standard criteria.
- FETCH with plain text, MIME, attachments, and folded headers.
- FETCH with Unicode and long headers.
- BODY.PEEK behavior.
- Message flags and read-only restrictions.
- UID stability and message sequence numbering.
- Empty mailboxes and nonexistent messages.
- Repeated commands within a persistent IMAP connection.
- LOGOUT after successful and unsuccessful operations.
- Concurrent access by multiple clients.

Tests should use actual IMAP protocol clients wherever possible, not only direct calls to internal server methods.

## 10. Required External Validation

After implementing corrections, execute the following Python tests against the running server:

```
import imaplib
import ssl

with imaplib.IMAP4_SSL(
    "mail.miautrix.tech",
    993,
    ssl_context=ssl.create_default_context()
) as imap:

    imap.login(USERNAME, PASSWORD)

    print(imap.capabilities)
    print(imap.list())
    print(imap.select("INBOX", readonly=True))
    print(imap.search(None, "ALL"))
    print(imap.fetch("1", "(UID FLAGS BODY.PEEK[HEADER])"))
    print(imap.logout())
```

The credentials must be supplied securely, never embedded directly in committed source code.

Additional external validation:

```
himalaya --account miautrix-sales mailbox list

himalaya --account miautrix-sales envelope list
```

Both operations must succeed without protocol parsing errors.

## 11. Acceptance Criteria

The corrective task can be considered complete only when:

- LIST is implemented and returns compliant responses.
- EXAMINE works and enforces read-only behavior.
- SEARCH supports the required standard criteria.
- FETCH returns correctly framed message data.
- Standard Python imaplib tests pass.
- Himalaya mailbox listing succeeds.
- Himalaya envelope listing succeeds.
- Existing IMAP authentication remains functional.
- SMTP submission and message delivery remain unaffected.
- Automated regression tests are added.
- No security regression is introduced.
- External client test evidence is documented.

## 12. Instructions to Development Agent

Review the existing Miautrix Mail Server architecture and identify the modules responsible for IMAP command parsing, mailbox operations, FETCH serialization and command response handling.

Correct the underlying protocol defects rather than implementing client-specific workarounds.

Preserve the existing authentication architecture, including support for mailbox access using authenticated identities that may differ from the mailbox email address.

Follow this workflow:

**Code Review → Root Cause Analysis → Corrective Implementation → Unit Tests → Integration Tests → External Client Validation → Regression Review**

Do not mark the task completed until the external Python and Himalaya compatibility tests pass against a running instance of the corrected server.

**Reference:** RFC 3501 — Internet Message Access Protocol, Version 4rev1.

## Resolution — Version 2.0 implementation status

This section records code changes in the current branch and remaining acceptance work. It does not claim full RFC 3501 compliance.

| Defect | Change made | Tests and remaining validation |
|---|---|---|
| IMAP-001 LIST/LSUB | Added reference handling, `/` hierarchy paths from `ParentId`, `*` and `%` matching, child attributes, special-use roles, and non-persistent SUBSCRIBE/UNSUBSCRIBE acknowledgements. | The compile-only listener/MailKit test now exercises LIST patterns and LSUB. LSUB consistently returns the available set because subscriptions are not stored. |
| IMAP-002 SELECT/EXAMINE | Added UID-ordered sequence snapshots, real first-unseen sequence reporting, `RECENT 0`, UID metadata and read-only permanent flags. | The compile-only test exercises EXAMINE, SELECT, first unseen, read-only STORE rejection, and CHECK; no DB protocol run was performed. |
| IMAP-003 SEARCH | Added parser/evaluator for standard flag, address, text, date, size, UID and sequence criteria, boolean groups, NOT/OR, and UTF-8/US-ASCII charset handling. UID SEARCH returns UIDs. | The compile-only listener/MailKit test now includes a broad criteria matrix and error responses. Database execution and external-client validation remain outstanding. |
| IMAP-004 FETCH | Added sequence sets/ranges, requested-item parsing, multi-message segmented output, exact byte literal framing, sections, partial ranges, PEEK handling, FLAGS/UID/INTERNALDATE/SIZE, ENVELOPE and BODYSTRUCTURE formatting through MimeKit, including `message/rfc822`. | Unit and compile-only listener tests cover exact header framing, ranged UID fetch, PEEK/Seen behavior, envelope, body structure, Unicode/folded/long headers and an attachment. Exhaustive MIME part/partial combinations remain open. |
| IMAP-005 commands and state | Added state availability checks, AUTHENTICATE PLAIN (initial response and continuation), ENABLE, STATUS, STORE, COPY, EXPUNGE/UID EXPUNGE, CLOSE, UNSELECT, CREATE/DELETE/RENAME via application services, and matching advertised capabilities. INBOX rename returns `NO [CANNOT]`. | `CLOSE` expunge semantics and all mandatory RFC command response codes require review; no external client validation has run. |
| D1/D3 parser and literals | Byte-oriented buffered reader shares line/literal buffer; tokenizer handles quoted escapes, parenthesized lists and literal placeholders; collector handles multiple `{n}`/`{n+}` literals and suppresses continuations for LITERAL+. | MemoryStream tests cover buffered line/literal framing, bounds, multiple literals and tagged overlong-line errors; listener protocol suite compiles but was not run. |
| D2/D4/D5 response and logging | Response segments preserve exact binary lengths and multiple literals; LOGIN and APPEND use token parsing; error logging records only command name, tag and exception type and produces a tagged response. | Exact report sequence with BODY.PEEK[HEADER] framing is covered by local session and TLS listener tests; the TLS test is compile-only here. |
| R1/R2 FETCH syntax | Corrected the post-literal separator to exactly one SP, associate partial ranges with their source request item, and reject sectionless `BODY.PEEK`. | Unit tests assert exact `FETCH 1 (BODY.PEEK[HEADER] FLAGS UID)` bytes and ensure `BODY[]<0.10>` does not truncate `BODY[HEADER]`. |
| R3 authorization and read-only delegates | SELECT probes write authorization and selects read-only when only read access is available; authorization denials in mailbox service paths map to `NO`. | Unit fake-helper test confirms a non-PEEK fetch succeeds without changing Seen for a read-only delegate. DB delegate scenarios remain compile-only. |
| R4 oversized literals | Oversized `{n+}` receives `* BYE Literal too large` and closes; oversized synchronising `{n}` receives tagged `NO [TOOBIG]` without continuation. | MemoryStream unit tests cover both outcomes and synchronization. |
| R5 fetch/database efficiency | Raw content is opened only for items that require it; sequence operations project required metadata rather than message body fields. | Build and unit tests passed; DB-backed performance behavior is not benchmarked. |
| R6 dead authentication path | Removed unused `AuthenticateAsMailboxAsync` path that installed an empty user identity. | Solution build and unit tests passed. |
| D7 tenant authorization | IMAP session invokes `ITenantAuthorizationHelper.AssertMailboxAccess` for mailbox operations using authenticated `UserId`; `IImapAuthenticator` returns that ID. Application services authorize folder creation/deletion/rename, flag changes, copy and expunge-to-trash semantics. | Shared-mailbox authorization behavior requires DB protocol tests; do not claim deployment validation. |

Unit coverage now includes tokenizer quoting/literals, MemoryStream line/literal buffering and bounds, byte-count framing with Unicode/folded headers, sequence sets, SEARCH and FETCH parsers, state transitions, MimeKit envelope/body structure including `message/rfc822`, and folder rename. Latest local results: solution build 0 warnings/0 errors, UnitTests 70 passed, SecurityTests 6 passed, webmail tests 15 passed, and webmail build passed. A local raw-socket harness runs against the session engine and checks the report command sequence and exact FETCH header literal bytes. The TLS in-process listener/MailKit protocol suite is authored and compiles, but was not run because Docker/test DB is unavailable; the full RFC matrix and shared-mailbox database authorization validation remain open. External validation still required: run `scripts/imap-external-validation.py` with `MIAUTRIX_IMAP_HOST`, `MIAUTRIX_IMAP_USER`, and `MIAUTRIX_IMAP_PASSWORD`, then run Himalaya mailbox and envelope listing against an approved non-production server. No external validation is claimed.

