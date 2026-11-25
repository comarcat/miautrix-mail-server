# MIAUTRIX MAIL SERVER

# IMAP-009 — Shared Mailboxes and Complete IMAP Protocol Compliance Review

**Project:** Miautrix Mail Server  
**Component:** IMAP Server, Mailbox Access, Authorization, Protocol Compliance  
**Priority:** Critical  
**Status:** Phase 1 (IMAP4rev1 and shared mailboxes) deployed and validated 2026-11-24, v2.0.0. Phase 2 (IMAP4rev2) pending  
**Scope:** Shared mailbox implementation + comprehensive IMAP audit and integration testing

---

## 1. Objective

Implement complete and secure IMAP access to authorized shared mailboxes and perform a comprehensive review of the existing IMAP implementation.

**This task must not be limited to fixing shared mailbox visibility.**

Previous development iterations exposed missing or defective IMAP functionality through external client testing, requiring multiple deployments and corrections.

The objective is to prevent additional rounds of isolated troubleshooting by performing a systematic protocol compliance review, implementing required missing functionality, and validating the complete implementation with real IMAP clients.

## 2. Existing Application Behavior

The Miautrix Mail Server web interface already displays personal folders and shared mailboxes.

The authenticated user [`cristobal@miautrix.tech`](mailto:cristobal@miautrix.tech) has access to these shared mailboxes:


| Shared mailbox                                      | Access                     |
| --------------------------------------------------- | -------------------------- |
| [alerts@miautrix.tech](mailto:alerts@miautrix.tech) | Available in web interface |
| [flows@miautrix.tech](mailto:flows@miautrix.tech)   | Available in web interface |
| [owner@miautrix.tech](mailto:owner@miautrix.tech)   | Available in web interface |
| [sales@miautrix.tech](mailto:sales@miautrix.tech)   | Full Access — confirmed    |


The web interface shows the following folders inside the sales mailbox:

- Inbox
- Sent
- Drafts
- Archive
- Junk
- Trash

The user must be able to access these same resources through a standard IMAP client, subject to the authorization rules already implemented by the application.

**Important:** Do not assume Full Access automatically includes Send As or Send on Behalf permissions. Inspect the existing authorization model and preserve those distinctions.

### Current IMAP behavior

Authentication succeeds using:

[`cristobal@miautrix.tech`](mailto:cristobal@miautrix.tech)

However, the following command:

```
LIST "" "*"
```

Returns only personal folders.

The shared mailbox namespace is not exposed through the tested IMAP paths.

Consequently, Himalaya displays the user's personal inbox instead of the required [`sales@miautrix.tech`](mailto:sales@miautrix.tech) shared inbox.

## 3. Required Architecture Review

Before implementing changes, inspect the existing application architecture.

Identify and document:

1. How personal mailboxes are stored.
2. How shared mailboxes are represented.
3. How mailbox ownership is determined.
4. How Full Access and delegated permissions are stored.
5. How the web interface retrieves shared mailbox contents.
6. Which application services or repositories the web interface uses.
7. How the existing IMAP service resolves mailbox paths.
8. How message UIDs, folder identifiers and UIDVALIDITY are generated.
9. How authorization is enforced for mailbox operations.
10. Whether shared mailbox functionality already exists in another application layer.

**Mandatory:** Reuse existing business logic, repositories and authorization services wherever practical. Do not create a separate shared mailbox permission system solely for IMAP.

## 4. Shared Mailbox IMAP Implementation

### 4.1 Namespace design

Implement a documented namespace that distinguishes personal and shared mailboxes.

Suggested structure:

```
INBOX
Sent
Drafts
Archive
Junk
Trash

Shared/alerts/INBOX
Shared/alerts/Sent

Shared/flows/INBOX
Shared/flows/Sent

Shared/owner/INBOX
Shared/owner/Sent

Shared/sales/INBOX
Shared/sales/Sent
Shared/sales/Drafts
Shared/sales/Archive
Shared/sales/Junk
Shared/sales/Trash
```

These are proposed IMAP paths. The development agent may choose a more appropriate convention based on existing architecture and standards.

The design must document how shared mailbox addresses map to IMAP paths, including potential naming collisions and renames.

### 4.2 Required behavior

The implementation must support:

- Discovery of authorized shared mailboxes.
- Listing folders inside a shared mailbox.
- Selecting personal and shared folders.
- Reading messages from shared folders.
- Searching messages in shared folders.
- Copying and moving messages where authorized.
- Managing flags and read/unread state.
- Folder creation, renaming and deletion where authorized.
- Message append and expunge where authorized.
- Correct permission enforcement on every operation.
- Multiple users accessing the same shared mailbox.
- Consistent visibility between web and IMAP clients.

### 4.3 Authorization

Every mailbox operation must validate the authenticated user's effective permissions.

Access must not depend exclusively on whether a mailbox appears in a `LIST` response.

Explicitly prevent:

- Unauthorized SELECT requests.
- Unauthorized UID FETCH requests.
- Cross-mailbox message disclosure.
- Access through crafted mailbox paths.
- Bypassing permission checks through renamed folders.
- Unauthorized modifications or deletion.
- Permission escalation through IMAP commands.

Revoking Full Access must prevent subsequent unauthorized operations, including those attempted over an already established IMAP session according to the documented authorization-refresh behavior.

### 4.4 Shared mailbox identity

The IMAP implementation must distinguish:

**Authenticated identity:** [`cristobal@miautrix.tech`](mailto:cristobal@miautrix.tech)

**Selected mailbox owner:** [`sales@miautrix.tech`](mailto:sales@miautrix.tech)

**Selected folder:** sales mailbox Inbox

The `INBOX` alias must resolve consistently to the authenticated user's personal inbox unless the server explicitly documents another supported selection mechanism.

Simply changing an email address in a client configuration must not silently redirect access to a different mailbox.

## 5. Comprehensive IMAP Standards Audit

Perform a complete review against the applicable IMAP RFC specifications.

### Primary references

- RFC 9051 — IMAP4rev2
- RFC 3501 — IMAP4rev1
- RFC 5322 — Internet Message Format
- RFC 2045–2049 — MIME
- RFC 4314 — IMAP ACL extension, if supported
- RFC 6154 — Special-Use Mailboxes, if supported
- RFC 8314 — TLS requirements for email access

Review additional RFCs required by the capabilities currently advertised or selected for implementation.

**Protocol compatibility policy:** Determine which protocol versions the server currently claims to support. Fully implement the mandatory features of every advertised version, or correct the advertised capabilities. Do not advertise optional extensions without implementing their required behavior.

### 5.1 Connection and authentication

Audit:

- CAPABILITY
- NOOP
- LOGOUT
- LOGIN
- AUTHENTICATE, where advertised
- STARTTLS, where supported
- TLS certificate handling
- Authentication failures
- Command state transitions
- Tagged and untagged responses
- Multiple commands per connection
- Session timeouts
- Malformed command handling

### 5.2 Mailbox management

Audit all applicable mailbox operations:

- LIST
- LSUB, where applicable
- SELECT
- EXAMINE
- STATUS
- CREATE
- DELETE
- RENAME
- SUBSCRIBE
- UNSUBSCRIBE
- APPEND
- CLOSE
- CHECK
- UNSELECT, where supported
- NAMESPACE, where supported or required

Verify personal and shared mailbox behavior independently.

### 5.3 Message retrieval

Audit:

- FETCH
- UID FETCH
- RFC822.SIZE
- FLAGS
- INTERNALDATE
- ENVELOPE
- BODY
- BODYSTRUCTURE
- BODY[]
- BODY.PEEK[]
- BODY[HEADER]
- BODY[TEXT]
- BODY section selectors
- Partial message retrieval
- MIME multipart messages
- Attachments
- Message literals and binary-safe retrieval

A complete message must be returned with correct literal framing and byte lengths.

### 5.4 Message operations

Audit:

- SEARCH
- UID SEARCH
- STORE
- UID STORE
- COPY
- UID COPY
- EXPUNGE
- UID EXPUNGE, where supported
- MOVE and UID MOVE, where supported
- Message flags and keywords
- Permanent flag behavior
- Deleted-message handling

### 5.5 UID handling

Verify:

- UID uniqueness within each mailbox.
- UID persistence across sessions.
- Correct UIDVALIDITY behavior.
- Correct UIDNEXT behavior.
- Message sequence numbers versus UIDs.
- UID behavior after deletions.
- UID behavior after moving or copying.
- UID behavior in shared mailboxes.
- Concurrent access consistency.

Never return UID zero for a valid assigned message.

### 5.6 Response correctness

Review all response serialization code for:

- Correct tagged completion responses.
- Correct OK, NO and BAD usage.
- Correct IMAP string escaping.
- Correct NIL handling.
- Correct parentheses and nested structures.
- Correct literal byte lengths.
- Correct ENVELOPE formatting.
- UTF-8 and encoded headers.
- RFC 5322 parsing.
- CRLF framing.
- Proper handling of empty mailboxes.
- Proper handling of malformed messages.
- Unexpected null values and storage errors.

Avoid returning generic `BAD Internal server error` for server-side defects without logging the underlying exception. Use protocol-appropriate error responses and preserve session synchronization.

## 6. Previously Discovered Defects

Ensure these historical defects have regression tests.


| ID       | Defect                                             |
| -------- | -------------------------------------------------- |
| IMAP-001 | LIST command unsupported or incomplete             |
| IMAP-002 | EXAMINE command unsupported                        |
| IMAP-003 | SEARCH command unsupported                         |
| IMAP-004 | Incorrect FETCH response framing                   |
| IMAP-005 | Incomplete protocol compatibility                  |
| IMAP-006 | Invalid message UID values                         |
| IMAP-007 | ENVELOPE serialization fails for certain messages  |
| IMAP-008 | RFC822.SIZE differs from returned message content  |
| IMAP-009 | Shared mailboxes are not discoverable through IMAP |


Historical corrections must not regress when new functionality is introduced.

## 7. Required Integration Testing

Unit tests alone are insufficient.

### 7.1 External clients

Use real IMAP connections to validate behavior with:

1. Python `imaplib`
2. Himalaya CLI
3. Mozilla Thunderbird
4. An additional compatible IMAP client or independent protocol test suite, where practical

Tests must run against a deployed server instance, not exclusively mocked services.

### 7.2 Shared mailbox test matrix


| Test      | Personal         | Sales shared     | Other authorized shared | Unauthorized shared            |
| --------- | ---------------- | ---------------- | ----------------------- | ------------------------------ |
| LIST      | PASS             | PASS             | PASS                    | Not disclosed unless permitted |
| SELECT    | PASS             | PASS             | PASS                    | Denied                         |
| FETCH     | PASS             | PASS             | PASS                    | Denied                         |
| SEARCH    | PASS             | PASS             | PASS                    | Denied                         |
| STORE     | Permission-based | Permission-based | Permission-based        | Denied                         |
| COPY/MOVE | Permission-based | Permission-based | Permission-based        | Denied                         |
| APPEND    | Permission-based | Permission-based | Permission-based        | Denied                         |
| EXPUNGE   | Permission-based | Permission-based | Permission-based        | Denied                         |


All tests must validate actual mailbox ownership, not simply successful command responses.

### 7.3 Message compatibility cases

Test at minimum:

- Empty inbox.
- Single message.
- Large mailbox.
- Plain-text email.
- HTML email.
- Multipart MIME email.
- Email with attachments.
- Unicode subject.
- Internationalized sender display name.
- Missing optional headers.
- Folded headers.
- Malformed header values.
- Zero-byte or inaccessible stored message.
- Large message.
- Multiple concurrent FETCH operations.
- Multiple clients accessing the same shared mailbox.
- Message deletion during another client's session.
- Invalid or revoked mailbox permissions.

### 7.4 Concurrency and reliability

Validate:

- Simultaneous sessions from the same account.
- Multiple delegates accessing one shared mailbox.
- Consistent UID mapping across sessions.
- Flag updates across clients.
- Folder updates and message movement.
- Reconnection after network interruption.
- Server restart while clients are connected.
- No message corruption or loss.
- No unauthorized data exposure.

## 8. Capability and Feature Matrix

The agent must generate a complete protocol coverage report.

For every applicable IMAP command, response, feature and extension, record:


| Field             | Description                                               |
| ----------------- | --------------------------------------------------------- |
| Feature           | Command or protocol capability                            |
| Applicable RFC    | Specification reference                                   |
| Requirement level | Mandatory, conditional or optional for advertised version |
| Current status    | Implemented, partial, missing, not applicable             |
| Test coverage     | Unit, integration and external client                     |
| Result            | PASS, FAIL, BLOCKED                                       |
| Action            | Required correction or documented limitation              |


Every capability advertised in the IMAP CAPABILITY response must have corresponding passing compatibility tests.

If a feature is intentionally unsupported, document that decision and verify that capability advertising and error handling remain standards-compliant.

## 9. Deliverables

The development agent must produce:

**A. Architecture Review**

Existing mailbox architecture, delegated access design and reusable services.

**B. IMAP Compliance Matrix**

Complete comparison against the applicable protocol specifications.

**C. Gap Analysis**

Every missing, partial or defective feature, classified by severity.

**D. Implementation**

Corrected IMAP server with shared mailbox access and required protocol fixes.

**E. Automated Test Suite**

Unit tests, protocol tests and integration tests covering personal and shared mailbox operations.

**F. External Client Validation**

Evidence of successful tests using Himalaya, Python and Thunderbird, including any environmental blockers.

**G. Technical Documentation**

Document:

- Supported IMAP versions and capabilities.
- Shared mailbox namespace.
- Authentication and delegated authorization.
- Shared folder selection.
- Known limitations.
- Configuration examples.
- Troubleshooting procedures.

**H. Final Validation Report**

Include test results, remaining defects, regression status and objective evidence of completion.

## 10. Acceptance Criteria

The task cannot be marked complete until:

1. All four authorized shared mailboxes are discoverable using the documented IMAP mechanism.
2. The sales shared mailbox can be selected and read without accessing the personal mailbox.
3. Full Access permissions work consistently with existing application authorization rules.
4. Unauthorized users cannot access shared mailbox contents.
5. Applicable mandatory IMAP functionality is implemented and tested.
6. Advertised optional capabilities are functional.
7. All historical IMAP defects have passing regression tests.
8. Standard clients can list folders, list messages, retrieve complete messages and perform authorized operations.
9. UID behavior remains stable and compliant.
10. Shared mailbox operations remain safe during concurrent access.
11. No message loss, mailbox data corruption or security regression is observed.
12. Documentation and the compliance matrix are complete.

## 11. Implementation Instructions

Work in this order:

**Phase 1 — Discovery and audit**

Review source code, architecture, existing tests, RFC requirements and advertised IMAP capabilities.

**Phase 2 — Gap analysis**

Identify all incomplete and noncompliant behavior before implementing isolated fixes.

**Phase 3 — Architecture design**

Define shared namespace, permissions, mailbox routing and message ownership.

**Phase 4 — Implementation**

Implement shared mailbox access and correct protocol defects.

**Phase 5 — Automated verification**

Run comprehensive protocol, security and regression tests.

**Phase 6 — External interoperability validation**

Test with real clients and confirm actual mailbox contents.

**Phase 7 — Final report**

Provide objective evidence, compliance status and remaining limitations.

### Mandatory development rule

Do not declare a feature complete merely because a command returns `OK`.

Verify that the response is correctly formatted, represents the intended data, satisfies applicable RFC requirements and behaves correctly in an independent client.

**Do not report the IMAP implementation as fully compliant unless the claimed protocol version and advertised capabilities have been validated.**

The goal is a reliable, reusable IMAP implementation—not another series of individual fixes discovered during production testing.