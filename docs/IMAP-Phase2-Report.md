# IMAP Phase 2 Validation Report

**Scope:** Stage A extensions NAMESPACE (RFC 2342), MOVE (RFC 6851) and IDLE (RFC 2177) on the existing IMAP4rev1 service, plus an RFC 9051 audit. IMAP4rev2 is deliberately not advertised because the mandatory rev2 behavior is not yet complete. Production validation is excluded.

## Architecture

Each connection retains its existing `ImapSession`, DbContext and selected mailbox. NAMESPACE is answered by the protocol engine from the documented namespace contract: personal `""` with `/`, no Other Users namespace, and shared `Shared/` with `/`. MOVE resolves source and destination through the same mailbox namespace and authorization helper as other operations. Its application service locks the source message row and performs the mailbox/folder reassignment and UID allocation through `IMessageUidAllocator` in the caller's transaction; the protocol response contains COPYUID and descending source EXPUNGE sequence numbers.

IDLE is implemented in the listener as a per-session loop over the selected mailbox snapshot. It polls at a configurable interval (default 2 seconds), detects message additions, removals, and flag/read-state deltas, and rechecks mailbox authorization on every poll. Polling is intentionally used instead of PostgreSQL LISTEN/NOTIFY so changes from all existing writers (including webmail and inbound delivery) are observed without adding a notification contract to each writer. The cost is one mailbox snapshot query per idle connection per interval; the server timeout is 30 minutes. Revocation returns BYE and clears selected state.

## RFC 9051 compliance matrix

Columns match the Phase 1 report: Feature, RFC, Level, Status, Coverage/evidence, Action. `Missing/not advertised` means no claim of rev2 support; the corresponding rev2-specific requirement has not been demonstrated.

| Feature | RFC | Level | Status | Coverage/evidence | Action |
|---|---|---:|---|---|---|
| IMAP4rev1 behavior retained | 3501 | MUST | Implemented | Phase 1 regression matrix; Stage A regression suites green | Preserve rev1 defaults |
| CAPABILITY advertises IMAP4rev2 | 9051 §6.1.1 | MUST | Missing/not advertised | CAPABILITY remains IMAP4rev1 only | Add only after every rev2 MUST is complete |
| ENABLE IMAP4rev2 per-session mode | 9051 §6.1.1, §6.3.1 | MUST | Partial | Raw sessions verify ENABLED response and rev1/rev2 SEARCH, LIST, BINARY, LITERAL and RECENT separation; several rev2 features and complete response semantics remain unaudited | Complete remaining rev2 semantics and validate capability transition |
| AUTHENTICATE, LOGIN, UNAUTHENTICATED | 9051 §§6.2.2–6.2.3 | MUST/conditional | Partial | Credentials are accepted only on the implicit-TLS listener; generic failures do not reveal which credential was wrong; no cleartext listener/LOGINDISABLED path exists | Audit all rev2 authentication-state and response requirements |
| STARTTLS handling | 9051 §6.2.1 | Conditional | Partial | Listener is Implicit TLS; exact raw test verifies tagged BAD for STARTTLS on an already protected connection; no cleartext STARTTLS listener exists | Keep TLS-only deployment or implement a separately configured cleartext STARTTLS listener |
| SELECT, NOOP, FETCH and rev2 RECENT removal | 9051 §6.3.1 | MUST | Partial | Per-session rev2 SELECT/NOOP omit RECENT and FETCH omits `\\Recent`; STATUS RECENT is rejected; SELECT emits CLOSED when replacing a different selection; other response details remain under audit | Complete response and no-RECENT coverage |
| NAMESPACE | 2342, 9051 §6.3.10 | Extension | Implemented | Exact raw response; MailKit PersonalNamespaces/SharedNamespaces test | Preserve namespace contract |
| IDLE | 2177, 9051 §6.3.13 | Extension | Implemented | Raw tests cover cross-session APPEND/STORE/EXPUNGE, DONE, timeout and revocation | Preserve polling and authorization refresh |
| MOVE / UID MOVE | 6851 | Extension | Implemented | Raw tests cover shared/personal transfer, authorization, read-only, TRYCREATE, COPYUID and EXPUNGE; MailKit MoveToAsync | Preserve atomicity and UID allocation |
| ESEARCH | 4731, 9051 §6.4.4 | MUST | Implemented | Raw rev1 SEARCH and rev2 ESEARCH tests cover TAG/UID, empty and populated sets, and MIN/MAX/ALL/COUNT/SAVE return options | Preserve per-session response semantics |
| SEARCHRES | 5182, 9051 §6.4.4 | MUST | Implemented | Session-local saved result set; raw tests cover SEARCH/FETCH `$`, UID mode, and SAVE-only behavior | Audit other commands and keyword interactions |
| LIST-EXTENDED | 5258, 9051 §6.3.9 | MUST | Implemented | Rev2 selection options and SUBSCRIBED/CHILDREN/SPECIAL-USE return options parsed; rev1 exact rejection covered | Expand selection-option edge-case coverage |
| LIST-STATUS | 5819, 9051 §6.3.9 | MUST | Implemented | LIST STATUS emits LIST followed by STATUS for selectable folders; raw response tested | Preserve response ordering |
| STATUS=SIZE | 9051 §6.3.11 | MUST | Implemented | Rev2 STATUS returns SIZE as aggregate message octets and DELETED count; rev1 rejects rev2-only items | Verify large mailbox size bounds |
| SPECIAL-USE in LIST-EXTENDED | 6154, 9051 §6.3.9 | MUST | Implemented | RETURN (SPECIAL-USE) accepted; role attributes returned in LIST; raw test covers extended return form | Extend coverage to every special-use role |
| LITERAL- | 7888, 9051 §6.1.1 | MUST | Implemented | Rev2 session accepts synchronizing syntax as non-synchronizing up to 4096 octets; rev1 still sends a continuation; existing LITERAL+ remains | Add exact 4096/4097-octet boundary cases |
| BINARY command/data items and response codes | 3516, 9051 §6.4.5 | MUST | Partial | FETCH BINARY/BINARY.PEEK/BINARY.SIZE decode MIME sections; unknown CTE maps to UNKNOWN-CTE and formatter unit tests exercise conversion | Add exact raw UNKNOWN-CTE response and section/partial boundary cases |
| `$`-prefixed keywords | 9051 §6.3.1, §6.4.6 | MUST | Missing | STORE accepts ordinary keywords; rev2 `$` keyword handling is not validated | Implement allowed keyword semantics and tests |
| Rev2 response-code changes | 9051 §§7–9 | MUST | Partial | CLOSED on rev2 mailbox replacement and UNKNOWN-CTE for BINARY are implemented; no complete response-code audit | Compare every response against rev2 semantics |
| LITERAL framing and octet counts | 9051 §§4, 9 | MUST | Partial | Raw tests prove sync vs non-sync APPEND framing by session; LITERAL- threshold boundaries remain untested | Add rev2 literal boundary tests |
| Shared mailbox authorization under new commands | 9051, project policy | MUST | Implemented for Stage A | MOVE tests exercise write checks on source and destination; IDLE rechecks access | Retain hidden-NO behavior |

Focused audit count: **12 implemented (including Stage A), 7 partial, 2 missing/not advertised** across the 21 rows above. This is not a claim that the listed rows enumerate every paragraph-level RFC 9051 MUST; response-code, authentication, and keyword semantics still need a complete paragraph-level audit, so the version is not advertised.

## Gap analysis

Stage A extensions are independently advertised and have protocol tests. The rev2 switch changes ESEARCH/SEARCHRES, LIST-EXTENDED, STATUS=SIZE, LITERAL-, BINARY and RECENT behavior per session. IMAP4rev2 remains unadvertised because `$`-prefixed keyword behavior, complete response-code/authentication auditing, BINARY UNKNOWN-CTE raw coverage, and LITERAL- boundary tests are incomplete; the deployment has only an implicit-TLS listener.

## Final CAPABILITY

```text
* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE NAMESPACE MOVE IDLE
```

`IMAP4rev1` is retained as the default version. `AUTH=PLAIN`/`SASL-IR`, `LITERAL+`, `ENABLE`/`UTF8=ACCEPT`, `UNSELECT`, `UIDPLUS`, and `SPECIAL-USE` retain Phase 1 evidence. `NAMESPACE`, `MOVE`, and `IDLE` have new raw/MailKit or listener tests. `IMAP4rev2` is omitted because its MUST rows above are incomplete. ACL and STARTTLS are also omitted; the deployment uses implicit TLS.

## Test evidence

The expanded full suite was run twice after the latest implementation slice: each run passed build with 0 warnings/errors, UnitTests 122/122, SecurityTests 7/7, ProtocolTests 35/35, and the sender authorization / UID allocator integration filter 10/10. Raw TLS coverage includes rev1/rev2 ESEARCH, SEARCHRES, LIST-EXTENDED, STATUS=SIZE, LITERAL framing, BINARY items, STARTTLS rejection on Implicit TLS, and the rev2 CLOSED response. MailKit tests cover namespaces and MOVE; MailKit 4.18 exposes no generic ENABLE API to exercise explicit IMAP4rev2 mode. External clients have not been run against production.

## Owner validation checklist

- [ ] Python `imaplib`: run `scripts/imap-external-validation.py`; confirm NAMESPACE has personal `""` `/`, shared `Shared/` `/`, and no Other Users namespace.
- [ ] Himalaya: refresh folders and confirm it discovers `Shared/` and can select an authorized shared folder.
- [ ] Thunderbird: refresh folder list and confirm shared namespace discovery and access rules.
- [ ] Exercise MOVE in Himalaya or Thunderbird from personal to shared and shared to personal; confirm both folders update.
- [ ] Start IDLE in one client, APPEND/STORE/EXPUNGE from another, and confirm changes appear within five seconds.
- [ ] Revoke shared access during IDLE and confirm updates stop and the session leaves selected state.
- [ ] Re-run the Phase 1 checklist for clients and confirm rev1 behavior remains unchanged.
