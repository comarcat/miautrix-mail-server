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
| ENABLE IMAP4rev2 per-session mode | 9051 §6.1.1, §6.3.1 | MUST | Missing | No rev2 session switch or semantics matrix | Implement and test session-isolated switch |
| AUTHENTICATE, LOGIN, UNAUTHENTICATED | 9051 §§6.2.2–6.2.3 | MUST/conditional | Partial | Rev1 PLAIN auth is covered; rev2 UNAUTHENTICATED response semantics not audited to completion | Audit response codes and authentication state rules |
| STARTTLS handling | 9051 §6.2.1 | Conditional | Missing/not advertised | Deployment uses implicit TLS; STARTTLS is absent | Keep unadvertised unless implemented |
| SELECT and rev2 RECENT removal | 9051 §6.3.1 | MUST | Partial | Rev1 SELECT reports RECENT; rev2 per-session omission is not implemented | Switch response semantics after ENABLE IMAP4rev2 |
| NAMESPACE | 2342, 9051 §6.3.10 | Extension | Implemented | Exact raw response; MailKit PersonalNamespaces/SharedNamespaces test | Preserve namespace contract |
| IDLE | 2177, 9051 §6.3.13 | Extension | Implemented | Raw tests cover cross-session APPEND/STORE/EXPUNGE, DONE, timeout and revocation | Preserve polling and authorization refresh |
| MOVE / UID MOVE | 6851 | Extension | Implemented | Raw tests cover shared/personal transfer, authorization, read-only, TRYCREATE, COPYUID and EXPUNGE; MailKit MoveToAsync | Preserve atomicity and UID allocation |
| ESEARCH | 4731, 9051 §6.4.4 | MUST | Missing | SEARCH/UID SEARCH do not implement ESEARCH return data | Implement TAG, UID, MIN/MAX/ALL/COUNT/MODSEQ options and tests |
| SEARCHRES | 5182, 9051 §6.4.4 | MUST | Missing | `$` saved result references are not implemented | Implement SAVE and saved-result state per session |
| LIST-EXTENDED | 5258, 9051 §6.3.9 | MUST | Missing | LIST does not parse extended selection/return options | Implement and test selection and RETURN options |
| LIST-STATUS | 5819, 9051 §6.3.9 | MUST | Missing | LIST STATUS return data not implemented | Implement status items in LIST responses |
| STATUS=SIZE | 9051 §6.3.11 | MUST | Missing | STATUS accepts only MESSAGES, RECENT, UIDNEXT, UIDVALIDITY, UNSEEN | Add SIZE and exact aggregate size tests |
| SPECIAL-USE in LIST-EXTENDED | 6154, 9051 §6.3.9 | MUST | Missing | Basic SPECIAL-USE attributes are listed, extended LIST option absent | Implement extended LIST use selection/return |
| LITERAL- | 7888, 9051 §6.1.1 | MUST | Missing | LITERAL+ is supported; synchronizing LITERAL- negotiation/limits are absent | Implement framing and size constraints |
| BINARY command/data items and response codes | 3516, 9051 §6.4.5 | MUST | Missing | FETCH BINARY/BINARY.SIZE and required conversion response codes absent | Implement MIME part octet decoding and response codes |
| `$`-prefixed keywords | 9051 §6.3.1, §6.4.6 | MUST | Missing | STORE accepts ordinary keywords; rev2 `$` keyword handling is not validated | Implement allowed keyword semantics and tests |
| Rev2 response-code changes | 9051 §§7–9 | MUST | Partial | Rev1 response codes covered; no complete cross-command rev2 response-code audit | Compare every response against rev2 semantics |
| LITERAL framing and octet counts | 9051 §§4, 9 | MUST | Partial | Rev1 literal tests cover BODY and APPEND; rev2 LITERAL- behavior absent | Add rev2 literal boundary/continuation tests |
| Shared mailbox authorization under new commands | 9051, project policy | MUST | Implemented for Stage A | MOVE tests exercise write checks on source and destination; IDLE rechecks access | Retain hidden-NO behavior |

Focused audit count: **5 implemented (including Stage A), 4 partial, 12 missing/not advertised** across the 21 rows above. This is not a claim that the listed rows enumerate every paragraph-level RFC 9051 MUST; the missing count is a lower-bound audit of the mandatory areas reviewed here, and the version is not advertised.

## Gap analysis

The implemented extensions are independently advertised and have protocol tests. Rev2 is not safe to advertise: several mandatory commands/options and response semantics are absent, and ENABLE cannot currently switch behavior on only one session. In particular, ESEARCH, SEARCHRES, LIST-EXTENDED, LIST-STATUS, STATUS=SIZE, extended SPECIAL-USE, LITERAL-, BINARY and rev2-specific RECENT/keyword/response behavior remain gaps. The aggregate RFC 9051 MUST set is therefore not complete; no percentage or implemented count is claimed from this focused audit.

## Final CAPABILITY

```text
* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE NAMESPACE MOVE IDLE
```

`IMAP4rev1` is retained as the default version. `AUTH=PLAIN`/`SASL-IR`, `LITERAL+`, `ENABLE`/`UTF8=ACCEPT`, `UNSELECT`, `UIDPLUS`, and `SPECIAL-USE` retain Phase 1 evidence. `NAMESPACE`, `MOVE`, and `IDLE` have new raw/MailKit or listener tests. `IMAP4rev2` is omitted because its MUST rows above are incomplete. ACL and STARTTLS are also omitted; the deployment uses implicit TLS.

## Test evidence

After Stage A, `dotnet build Miautrix.Mail.sln -warnaserror -m:1` passed with 0 warnings and 0 errors; UnitTests passed 101/101, SecurityTests 7/7, ProtocolTests 29/29, and the sender authorization / UID allocator integration filter 10/10. Protocol tests run against the real TLS listener and dev test database. The external validation script now checks NAMESPACE using only environment-provided host/credentials; external clients have not been run against production.

## Owner validation checklist

- [ ] Python `imaplib`: run `scripts/imap-external-validation.py`; confirm NAMESPACE has personal `""` `/`, shared `Shared/` `/`, and no Other Users namespace.
- [ ] Himalaya: refresh folders and confirm it discovers `Shared/` and can select an authorized shared folder.
- [ ] Thunderbird: refresh folder list and confirm shared namespace discovery and access rules.
- [ ] Exercise MOVE in Himalaya or Thunderbird from personal to shared and shared to personal; confirm both folders update.
- [ ] Start IDLE in one client, APPEND/STORE/EXPUNGE from another, and confirm changes appear within five seconds.
- [ ] Revoke shared access during IDLE and confirm updates stop and the session leaves selected state.
- [ ] Re-run the Phase 1 checklist for clients and confirm rev1 behavior remains unchanged.
