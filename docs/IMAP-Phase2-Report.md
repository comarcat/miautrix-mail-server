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
| AUTHENTICATE, LOGIN, UNAUTHENTICATED | 9051 §§6.2.2–6.2.3 | MUST/conditional | Partial | TLS raw tests cover invalid state, LOGIN and PLAIN `[AUTHENTICATIONFAILED]`, and PLAIN `[AUTHORIZATIONFAILED]` when authzid differs; LOGIN/AUTH PLAIN are enabled only on the implicit-TLS listener | Preserve credential-provider expiration semantics if introduced and finish RFC authentication edge-case audit |
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
| LITERAL- | 7888, 9051 §6.1.1 | MUST | Implemented | Raw rev1/rev2 tests verify the 4096-octet non-synchronizing boundary and 4097-octet synchronizing continuation | Keep boundary and framing tests in the regression suite |
| BINARY command/data items and response codes | 3516, 9051 §6.4.5 | MUST | Partial | Raw tests cover BINARY, BINARY.PEEK, BINARY.SIZE and exact `NO [UNKNOWN-CTE]` for an unknown CTE | Audit BINARY empty-section and partial-fetch boundaries |
| `$`-prefixed keywords | 9051 §§2.3.2, 6.3.1, 6.4.6 | MUST | Implemented | Raw rev1 and rev2 tests STORE and SEARCH `$Forwarded`, `$MDNSent`, `$Junk`, `$NotJunk`, `$Phishing`; rev1 keeps `\\Recent`, and selection advertises `PERMANENTFLAGS (\\*)` | Preserve arbitrary keyword storage and search; do not combine `$Junk` and `$NotJunk` in client state |
| Rev2 response-code changes | 9051 §§7–9 | MUST | Partial | `CLOSED`, `UNKNOWN-CTE`, `LIMIT`, `AUTHENTICATIONFAILED`, `AUTHORIZATIONFAILED`, `CANNOT`, `NONEXISTENT`, `ALREADYEXISTS`, `INUSE`, and `SERVERBUG` paths are implemented; TLS-only and auth-provider limits affect other codes | Finish response-code mapping tests and audit all command response placement/semantics |
| LITERAL framing and octet counts | 9051 §§4, 9 | MUST | Implemented | Raw rev1 and rev2 APPEND tests exercise synchronizing and non-synchronizing frames; rev2 exact boundary cases cover 4096/4097 octets | Preserve framing boundary coverage |
| Shared mailbox authorization under new commands | 9051, project policy | MUST | Implemented for Stage A | MOVE tests exercise write checks on source and destination; IDLE rechecks access | Retain hidden-NO behavior |

Focused audit count: **14 implemented (including Stage A), 6 partial, 1 missing/not advertised** across the 21 rows above. This is not a claim that the listed rows enumerate every paragraph-level RFC 9051 MUST; BINARY boundaries, full response-code placement, and authentication/provider edge cases remain unaudited, so the version is not advertised.

### Authentication and section 7.1 response-code audit

| Code/rule | Current behavior | Evidence or applicability |
|---|---|---|
| `AUTHENTICATIONFAILED` | Used for failed LOGIN and failed SASL PLAIN authentication with generic text | Exact raw TLS assertions cover both paths without disclosing which credential failed |
| `AUTHORIZATIONFAILED` | Used when valid PLAIN authentication requests a different authorization identity | Exact raw TLS assertion; authentication and authorization identities are compared case-insensitively |
| `EXPIRED` | Not emitted | The authentication-provider contract has no expired-credential result; no password-expiration policy is currently exposed to IMAP |
| `PRIVACYREQUIRED` | Not emitted | All accepted sessions already use implicit TLS; there is no cleartext endpoint on which to reject an unprotected operation |
| `LOGINDISABLED` | Not advertised | LOGIN remains available because this listener accepts only TLS-protected connections |
| `UNAUTHENTICATED` state rules | Commands outside the unauthenticated command set return tagged BAD; LOGIN/AUTHENTICATE are allowed | Exact raw assertion verifies LIST is rejected before authentication |
| STARTTLS | Tagged BAD on the implicit-TLS endpoint | Exact raw response test; no cleartext STARTTLS listener exists |
| `CANNOT` | Used when an operation violates mailbox invariants, including protected mailboxes | Existing exact raw tests cover INBOX and special-folder deletion failures |
| `LIMIT` | Used for an oversized synchronizing literal in an enabled rev2 session; rev1 keeps `TOOBIG` | Exact raw response tests in both session modes with a bounded test listener |
| `NONEXISTENT` | Used for a missing mailbox in a visible namespace; unauthorized resources retain generic hidden `NO` | Exact raw DELETE and RENAME tests cover visible missing folders; shared-mailbox tests cover concealment |
| `ALREADYEXISTS` | Used for duplicate mailbox creation/rename | Existing exact raw mutation tests |
| `INUSE` | Used when DELETE/RENAME targets the selected mailbox | Exact raw DELETE and RENAME tests |
| `CONTACTADMIN` | Not emitted | No current IMAP condition maps an authentication or operation failure to an operator-support action |
| `SERVERBUG` | Generic engine exception fallback uses the code without sensitive exception text | Engine fallback is implemented; no fault-injection raw test currently exercises it |

Other §7.1 codes and their command-specific placement (including `UNAVAILABLE`, `NOPERM`, `OVERQUOTA`, `EXPUNGEISSUED`, `CORRUPTION`, `CLIENTBUG`, `NOTSAVED`, and response-code variants on successful commands) still require paragraph-level mapping and verification. The entries above close the requested authentication/state and named-code slice, not the full §7.1 audit.

## Gap analysis

Stage A extensions are independently advertised and have protocol tests. The rev2 switch changes ESEARCH/SEARCHRES, LIST-EXTENDED, STATUS=SIZE, LITERAL-, BINARY and RECENT behavior per session. The audited `$` keywords, UNKNOWN-CTE response, and LITERAL- boundary behaviors are implemented and tested. IMAP4rev2 remains unadvertised because the BINARY empty-section/partial-fetch boundaries, complete response-code semantics and auth-provider edge cases have not been proven, and the listener is implicit-TLS only.

## Final CAPABILITY

```text
* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE NAMESPACE MOVE IDLE
```

`IMAP4rev1` is retained as the default version. `AUTH=PLAIN`/`SASL-IR`, `LITERAL+`, `ENABLE`/`UTF8=ACCEPT`, `UNSELECT`, `UIDPLUS`, and `SPECIAL-USE` retain Phase 1 evidence. `NAMESPACE`, `MOVE`, and `IDLE` have new raw/MailKit or listener tests. `IMAP4rev2` is omitted because its MUST rows above are incomplete. ACL and STARTTLS are also omitted; the deployment uses implicit TLS.

## Test evidence

The latest implementation slice was run twice with build (`-warnaserror`): UnitTests 125/125, SecurityTests 7/7, ProtocolTests 38/38, and the documented sender authorization / UID allocator integration filter 10/10 all passed in both runs. A separate attempt to run every IntegrationTests case also ran, but failed 16/110 tests, including environment-dependent CLI storage setup and tests that require seeded folders; those failures are outside this filtered protocol verification and were not fixed here. Raw TLS coverage includes rev1/rev2 ESEARCH, SEARCHRES, LIST-EXTENDED, STATUS=SIZE, both literal boundaries, BINARY items, exact UNKNOWN-CTE and LIMIT, authentication/state codes, STARTTLS rejection on Implicit TLS, and the rev2 CLOSED response. MailKit tests cover namespaces and MOVE; MailKit 4.18 exposes no generic ENABLE API to exercise explicit IMAP4rev2 mode. External-client validation has not been run against production.

## Owner validation checklist

- [ ] Python `imaplib`: run `scripts/imap-external-validation.py`; confirm NAMESPACE has personal `""` `/`, shared `Shared/` `/`, and no Other Users namespace.
- [ ] Himalaya: refresh folders and confirm it discovers `Shared/` and can select an authorized shared folder.
- [ ] Thunderbird: refresh folder list and confirm shared namespace discovery and access rules.
- [ ] Exercise MOVE in Himalaya or Thunderbird from personal to shared and shared to personal; confirm both folders update.
- [ ] Start IDLE in one client, APPEND/STORE/EXPUNGE from another, and confirm changes appear within five seconds.
- [ ] Revoke shared access during IDLE and confirm updates stop and the session leaves selected state.
- [ ] Re-run the Phase 1 checklist for clients and confirm rev1 behavior remains unchanged.

## v2.2 worker checkpoint — 2026-11-25

**Disposition: partial; IMAP4rev2 remains unadvertised.** This checkpoint closes specific implementation defects, but does not satisfy the requested paragraph-by-paragraph audit of every server-applicable MUST/SHOULD/REQUIRED in RFC 9051 §§2–7 and Appendix E. Advertisement is intentionally unchanged: `* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE NAMESPACE MOVE IDLE`.

### Paragraph-level audit status for this checkpoint

This is the bounded audit completed during this worker dispatch. The full RFC paragraph enumeration remains **Missing** and cannot be represented as green based on command-level tests alone.

| RFC area / requirement | Level | Status | Code location | Test evidence |
|---|---:|---|---|---|
| §2 client command tag reuse must be accepted | MUST | Missing / not audited | `ImapEngine.cs`, command dispatcher | No focused tag-reuse test identified |
| §2 strict command syntax; server should reject syntax errors | SHOULD | Partial | `ImapCommandTokenizer.cs`, command-specific handlers | `ImapListenerRawSocketTests` syntax cases; full grammar matrix absent |
| §2 server accepts unrequested data and correctly frames continuations/literals | MUST / SHOULD | Partial | `ImapListenerService.cs`, `ImapResponseWriter.cs` | Raw literal and IDLE tests; full unsolicited response audit absent |
| §2.3 UID nonzero, ascending, stable; UIDNEXT and UIDVALIDITY semantics | MUST | Implemented (bounded behavior) | `ImapEngine.cs`; UID allocator/storage | `ImapUidStabilityTests`; raw UID/MOVE tests |
| §3 authenticated/selected state command permissions | MUST | Partial | `ImapStateMachine.cs`, `ImapEngine.cs` | Raw auth-state assertions; per-command RFC table absent |
| §4 UTF-8 and literal syntax / framing | MUST | Partial | `ImapCommandTokenizer.cs`, `ImapListenerService.cs`, `ImapEngine.cs` | Raw LITERAL- 4096/4097 boundary and rev2 ENABLE test; full malformed-octet corpus absent |
| §5 mailbox naming and Net-Unicode validity | MUST | Missing / not audited | `ImapEngine.cs` mailbox path encoding/decoding | No normalization / invalid Net-Unicode test identified |
| §6.1 CAPABILITY and ENABLE response semantics | MUST | Implemented for ENABLE response; rev2 advertisement withheld | `ImapEngine.cs: CapabilityLine`, `HandleEnable` | `ImapListenerRawSocketTests`: capability exact string, R5/R5a ENABLE |
| §6.3.1 ENABLE IMAP4rev2 session behavior; ENABLED sent even if none enabled | MUST | Implemented (bounded) | `ImapEngine.cs: HandleEnable` | `ImapListenerRawSocketTests`: R5 and R5a |
| §6.3.1 rev2 UTF-8 mailbox names | MUST | Partial; UTF-8 acceptance toggled by rev2, Net-Unicode validation unproven | `ImapEngine.cs: HandleEnable`, mailbox path codec | No rev2 Unicode mailbox round-trip test identified |
| §6.3.2–3 SELECT/EXAMINE response codes, LIST response, omit RECENT, CLOSED | MUST | Partial | `ImapEngine.cs: HandleSelectAsync` | `ImapListenerRawSocketTests`: rev2 select, no RECENT, CLOSED; LIST response code matrix absent |
| §6.3.9 LIST-EXTENDED return/selection options and attributes | MUST | Partial | `ImapEngine.cs: HandleListAsync` | Raw LIST-EXTENDED tests; complete RETURN/attribute truth-table absent |
| §6.3.11 STATUS DELETED and SIZE | MUST | Implemented (bounded) | `ImapEngine.cs: BuildStatusResponseAsync` | Existing rev2 STATUS=SIZE raw test; new external validation check for DELETED/SIZE |
| §6.4.4 SEARCH/UID SEARCH ESEARCH and SEARCHRES SAVE/$ | MUST | Implemented (bounded) | `ImapEngine.cs: HandleSearchAsync` | Raw ESEARCH, UID ESEARCH, RETURN and SEARCHRES assertions in `ImapListenerRawSocketTests` |
| §6.4.5 BINARY leaf-only section-binary; partial is over decoded octets; matching SIZE | MUST | Partial | `ImapFetchItemParser.cs`; `ImapEngine.cs` partial emission; `MimeKitImapMimeFormatter.cs` | Raw `R24a` verifies decoded partial origin/count; empty section and SIZE partial rejection `R24b/R24c`; remaining multipart/message and CRLF normalization tests absent |
| §6.4.5 unknown CTE returns NO [UNKNOWN-CTE] | MUST | Implemented for unknown transfer-encoding token | `MimeKitImapMimeFormatter.cs`; `ImapEngine.cs` | Raw `R29`; formatter unit test `Decodes_binary_sections_and_reports_unknown_transfer_encoding` |
| §6.4.9 UID EXPUNGE, APPENDUID/COPYUID semantics | MUST | Partial | `ImapEngine.cs` UID/COPY/MOVE/APPEND handlers | Raw UID EXPUNGE/COPYUID/APPENDUID assertions; complete NO/error-code placement matrix absent |
| §6 rev2 removed CHECK and LSUB; no \Recent | MUST | Implemented for CHECK/LSUB dispatch and existing response omission | `ImapEngine.cs` | New raw `R6a/R6b`; existing SELECT/FETCH/STATUS recent assertions |
| Appendix E folded/removed extension semantics, including ID and unsupported commands | MUST / conditional | Missing / not audited | Capability line and command dispatch | No complete Appendix E checklist/test mapping |
| §§7.1–7.6 response syntax, codes, placement, untagged response ordering | MUST | Partial | `ImapEngine.cs`, `ImapResponseWriter.cs` | Selected exact-code raw tests; complete code/command placement matrix remains open |

### Exact remaining blockers to advertisement

- Full paragraph enumeration and evidence for RFC 9051 §§2–7 and Appendix E, including all applicable MUST, MUST NOT, SHOULD, and REQUIRED statements.
- BINARY still needs server enforcement that the requested part is a leaf (not multipart, `message/rfc822`, or `message/global`), correct textual CRLF output, and tests proving BINARY.SIZE matches each corresponding decoded BINARY response across empty, out-of-range, and partial boundaries.
- Net-Unicode mailbox validation and UTF-8 create/select/rename round trips in rev2 have not been proven.
- Rev2 LIST response-code requirements, exact LIST RETURN/attribute truth table (`\\NonExistent`, `\\HasChildren`, `\\HasNoChildren`, `\\Subscribed`), and full STATUS DELETED/SIZE exact response tests remain incomplete.
- Full SELECT/EXAMINE responses (including required LIST code, READ-ONLY/READ-WRITE and no RECENT), APPENDUID/COPYUID and failure response codes, UID EXPUNGE, ID handling, SAVE/`$` behavior beyond current tests, and rev2 NOOP/CHECK edge behavior need systematic coverage.
- MailKit tests in explicit rev2 mode, a named rev1 no-ENABLE Thunderbird compatibility regression covering shared mailboxes, MOVE, IDLE and WEBMAIL-001 drafts, and exact capability checks at pre-auth/post-auth are not complete. External script now includes a rev2 pass, but it was not run against any host.
- Response-code placement and semantics have not been audited exhaustively. The existing code paths and a few exact raw assertions do not establish all RFC 9051 server requirements.

### Implementation changes and verification

BINARY section parsing accepts the grammar's empty `section-binary` (`BINARY[]`); BODY[] remains the separate complete-message fetch. Decoded BINARY partial responses slice at the requested octet offset and report the returned origin; BINARY.SIZE partial syntax is rejected. Raw tests compare BINARY.SIZE with the returned BINARY literal and cover empty sections, partials, past-end offsets, and invalid BINARY.SIZE partial syntax (`R24`–`R24d`, `R24aa`). IMAP4rev2 ENABLE returns ENABLED even when no supported extension was enabled, enables UTF-8 mailbox decoding for the rev2 session, treats CHECK as NOOP, and rejects LSUB. Raw tests cover these in `R5a`, `R6a`, `R6b`, and the mailbox normalization cases `R5c`–`R5e`. `scripts/imap-external-validation.py` runs a distinct ENABLE IMAP4rev2 validation pass following the legacy rev1 pass.

Since this checkpoint, the engine normalizes rev2 mailbox paths to NFC and rejects invalid Net-Unicode scalar values during CREATE. Tests cover a decomposed UTF-8 CREATE followed by selecting its normalized spelling and rejection of a Unicode noncharacter. Exact CAPABILITY stability is tested before authentication, after authentication, and after ENABLE; rev2 is still deliberately absent from CAPABILITY. The protocol suite passed 38/38 after these changes.

### Remaining audit matrix rows before advertisement

The table above is not a complete paragraph-level audit. The specific requirement families still marked Missing or not yet evidenced are:

| RFC paragraph / rule | Level | Status | Code location | Test evidence / missing test |
|---|---:|---|---|---|
| §2.2.1 tag reuse accepted | MUST | Missing | `ImapEngine.cs` command dispatch | No focused repeated-tag raw test |
| §2.2.2 strict syntax rejection | SHOULD | Missing | `ImapCommandTokenizer.cs`, command handlers | Existing malformed-command cases are not a grammar-wide audit |
| §§2.3.1.1–2 UID and sequence-number invariants across expunge/recreate | MUST | Partial | `ImapEngine.cs`, UID allocator/storage | `ImapUidStabilityTests`; no exhaustive sequence/UID transition matrix |
| §§2.3.2–2.3.4 flags, internal date, and RFC822.SIZE semantics | SHOULD/MUST | Partial | STORE/APPEND/COPY/MOVE and MIME formatter | Existing keyword and APPEND tests; no full dates/size invariant matrix |
| §§3–4 state, framing, UTF-8 quoted strings, binary strings and invalid continuations | MUST | Partial | `ImapStateMachine.cs`, listener, tokenizer | Raw state and literal boundaries exist; invalid base64/literal and pipelining matrix is absent |
| §5.1 Net-Unicode prohibition on invalid mailbox names | MUST | Partial | `ImapEngine.cs: DecodeMailboxPath`, `HandleCreateFolderAsync` | Raw `R5c`–`R5e`; rename, all mailbox-argument commands, and complete Unicode category corpus absent |
| §5.2 unsolicited size/flag updates | MUST/SHOULD | Partial | `ImapListenerService.cs` IDLE polling | IDLE tests cover observed state changes; command-time unsolicited update requirements not exhaustively tested |
| §5.5 pipelining and EXPUNGE sequence safety | MUST | Missing | `ImapListenerService.cs`, `ImapEngine.cs` | No pipelining/sequence-number race suite |
| §§6.1.1–6.2.3 CAPABILITY/authentication corner cases | MUST/conditional | Partial | `ImapEngine.cs`, `ImapListenerService.cs` | Exact CAPABILITY/auth cases exist; AUTH PLAIN cancellation, malformed base64, initial response, TLS upgrade are not fully evidenced; no cleartext STARTTLS listener |
| §6.3.1 ENABLE additive semantics and valid state | MUST | Partial | `ImapEngine.cs: HandleEnable` | Raw ENABLED/CAPABILITY stability cases; no repeated ENABLE additive test or state matrix |
| §§6.3.2–6.3.3 SELECT/EXAMINE required responses and read-only codes | MUST | Partial | `ImapEngine.cs: HandleSelectAsync` | Raw SELECT/LIST/CLOSED and no-RECENT cases; EXAMINE and full required-response assertions are absent |
| §6.3.4 CREATE normalized-name OLDNAME behavior | MUST/SHOULD | Partial | `ImapEngine.cs: HandleCreateFolderAsync` | Normalization round-trip exists; OLDNAME response data is not emitted/tested |
| §6.3.9 LIST return/selection semantics and attributes | MUST | Partial | `ImapEngine.cs: HandleListAsync` | Current raw `R22` covers SUBSCRIBED/CHILDREN/SPECIAL-USE/STATUS; no complete `\\NonExistent`, `\\HasChildren`, `\\HasNoChildren`, `\\Subscribed`, RETURN/options truth table |
| §6.3.11 STATUS DELETED/SIZE | MUST | Partial | `ImapEngine.cs: HandleStatusAsync` | Raw `R21` covers zero counts; nonzero DELETED and aggregate SIZE are not asserted |
| §6.3.12 APPENDUID and §6.4.9 COPYUID/UID EXPUNGE/error codes | MUST | Partial | APPEND/COPY/MOVE/EXPUNGE handlers in `ImapEngine.cs` | Some success/error cases exist; no complete response-code placement and UID EXPUNGE test matrix |
| §6.4.5 BINARY empty section, leaf eligibility, partial bounds, UNKNOWN-CTE | MUST | Partial | `IImapMimeFormatter`, `MimeKitImapMimeFormatter`, `ImapEngine.cs` | Raw `R24`–`R24d`, `R29`/`R29a`; nested multipart/message/global and textual CRLF normalization conformance remain unverified |
| §6.4.4 ESEARCH / SEARCHRES SAVE and `$` | MUST | Partial | `ImapEngine.cs: HandleSearchAsync` | Raw `R11`–`R20`; no complete SEARCH key matching/UID result matrix |
| §6.4.6 STORE/flags, §6.4.8 FETCH and §6.4.9 commands | MUST/SHOULD | Partial | corresponding `ImapEngine.cs` handlers | Basic rev2 raw coverage exists; full command grammar and response semantics absent |
| §§7.1–7.2 response grammar, codes and capability requirements | MUST/REQUIRED | Partial | `ImapEngine.cs`, `ImapResponseWriter.cs` | Selected exact-code assertions only; response-code placement audit incomplete, so IMAP4rev2 is correctly not advertised |
| Appendix E rev2 changes | mixed | Partial | command-specific engine and MIME paths | Items E.1–E.21 are summarized by the feature rows above; no complete item-to-test traceability yet |

The requested paragraph-by-paragraph inventory is therefore still incomplete and several MUST/REQUIRED rows remain Missing or Partial. In particular, rev2 is not advertised: protocol tests and a command-feature checklist do not establish full RFC compliance. The remaining table is a truthful gap list, not a completed compliance matrix.

Verification for this worker continuation: solution build succeeded with 0 warnings and 0 errors; UnitTests passed 126/126, SecurityTests passed 7/7, and ProtocolTests passed 38/38. The targeted IntegrationTests filter ran 34 tests and failed 10 because Testcontainers reported Docker unavailable; 24 passed. The full IntegrationTests run reported 84 passed and 26 failed (110 total), including the same documented baseline failures plus Docker unavailable and seeded-folder/environment failures. Consequently the integration acceptance gate is not green in this environment. No production host was contacted.

Build: `dotnet build tests/Miautrix.Mail.ProtocolTests/Miautrix.Mail.ProtocolTests.csproj -warnaserror -m:1` succeeded with 0 warnings and 0 errors. Protocol tests: 38/38 passed with the configured non-production test DB. An initial run without passing the test environment into the test process failed because `MIAUTRIX_DB_CONNECTION` was absent; it was rerun correctly. The full solution and all baseline suites were not run in this partial checkpoint.
