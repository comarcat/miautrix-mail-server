# IMAP4rev2 v2.3 worker report

**Status: partial; IMAP4rev2 remains unadvertised.** The acceptance criteria require a complete RFC 9051 audit and no missing applicable MUST rows. The existing audit in [IMAP-Phase2-Report.md](IMAP-Phase2-Report.md#remaining-audit-matrix-rows-before-advertisement) still contains partial and missing MUST rows, so advertising the extension would make an unsupported compliance claim.

## Implemented in this checkpoint

| Requirement | RFC | Status | Code | Raw protocol evidence |
|---|---|---|---|---|
| ID accepts NIL and string/nstring pair lists and returns ID NIL | RFC 2971; RFC 9051 Appendix E | Implemented for supported syntax | `ImapEngine.HandleId` | `ImapListenerRawSocketTests.Report_python_sequence_over_test_listener_has_exact_literal_framing`: `R4id`, `R4id2` |
| ID is valid in pre-authenticated, authenticated, and selected states | RFC 2971 | Implemented | `ImapStateMachine.IsAllowed` | Raw ID commands in authenticated session; all other state coverage remains outstanding |
| ENABLE remains additive across repeated calls | RFC 5161; RFC 9051 §6.3.1 | Implemented for IMAP4rev2 and UTF8=ACCEPT | `ImapEngine.HandleEnable` | Raw ENABLE sequence `R5`, `R5a`, `R5a1` |
| CREATE reports a normalized mailbox spelling with OLDNAME | RFC 9051 §6.3.4 | Implemented for UTF-8 NFC normalization | `ImapEngine.HandleCreateFolderAsync` | Raw decomposed CREATE `R5c` asserts OLDNAME old/new values |
| APPENDUID/COPYUID success and UID EXPUNGE basic path | RFC 9051 §§6.3.12, 6.4.9 | Existing tests | `ImapEngine` APPEND/COPY/EXPUNGE handlers | Existing raw assertions `A9f`, `A9g`, `A9i` |
| Pre-auth/post-auth/after-ENABLE capability remains stable; rev1 MailKit behavior | RFC 9051 §6.1.1 | Existing tests | `ImapEngine.CapabilityLine` | Raw CAPABILITY assertions; `MailKit_rev1_without_enable_keeps_shared_move_idle_and_draft_append_working` |

No new settings or migration were added. Deploy requires no schema, DNS, or nginx change. The final CAPABILITY line remains `* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE NAMESPACE MOVE IDLE`.

## Remaining blockers and audit coverage

The full RFC 9051 §§2–7 and Appendix E (E.1–E.21) paragraph-level matrix is **not complete**. The Phase 2 report is the current bounded gap matrix, not a complete audit; it records missing/partial MUST evidence for §2.3 UID/sequence invariants, §3–4 framing/state, §5.1 mailbox-name validation, §5.2 updates, §5.5 EXPUNGE pipelining, §6.1–6.2 auth/capability edge cases, §6.3.9 LIST option interactions, §6.3.11 nonzero STATUS DELETED/SIZE, §6.3.12/§6.4.9 response-code placement, §6.4.5 MIME BINARY eligibility/normalization, §6.4.4 SEARCH result combinations, §§7.1–7.2 response grammar, and Appendix E mapping. Each still needs paragraph-by-paragraph disposition, exact implementation location, and test evidence.

Additional requested work remains: complete APPENDUID/COPYUID failure placement, all LIST truth-table combinations, Net-Unicode RENAME coverage, nested multipart/message/rfc822/message/global BINARY behavior and CRLF cases, the SEARCH/UID SEARCH ESEARCH+SEARCHRES matrix, pipelined FETCH/STORE/EXPUNGE ordering, command-completion unsolicited updates, syntax SHOULD grammar tests, and final response-code placement review. MailKit exposes no public generic ENABLE API, so rev2 mode needs raw socket coverage; rev1 MailKit regression is covered. STARTTLS on a cleartext listener is not applicable: the service only offers implicit TLS on port 993 and STARTTLS returns BAD on that TLS session.

## Test evidence

- `dotnet test tests/Miautrix.Mail.ProtocolTests/Miautrix.Mail.ProtocolTests.csproj -warnaserror -m:1`: 44/44 passed with the configured non-production test database.
- A test invocation that did not correctly pass the loaded environment to the child process failed before tests ran because `MIAUTRIX_DB_CONNECTION` was absent; rerunning with process environment variables passed.
- Full solution, other suites, and full integration suite were not run for this partial checkpoint.

## Troubleshooting draft

IMAP4rev2 is not yet advertised. Existing clients continue to use IMAP4rev1; do not enable or advertise IMAP4rev2 until the remaining audit matrix and protocol tests are complete.

## Continuation checkpoint

Further implementation in this checkpoint adds command-boundary unsolicited mailbox updates and prevents those polls from displacing the command's authorization response; it also rejects extra arguments to selected no-argument commands. LIST recursive parent attributes and CHILDINFO behavior, OLDNAME as LIST extended data, and raw assertions for selection, pipelining, and updates were extended. A build of the protocol test project succeeds with zero warnings; the current raw-socket test is still failing, so this is not a green protocol checkpoint.

The advertisement gate remains blocked by concrete, applicable normative rows that are not yet completely implemented or audited: RFC 9051 §§7.1–7.2 response grammar and response-code placement; §6.3.9 complete LIST-EXTENDED selection/RETURN combinations; §6.4.4 SEARCH/UID SEARCH result combinations with ESEARCH and SEARCHRES; §§2.3 and 3–5 sequence, state, framing, mailbox-name and unsolicited-update invariants; and Appendix E.1–E.21 item-by-item compliance mapping. Their requirement-level dispositions, code locations and test names are not enumerated completely here. Do not advertise IMAP4rev2 until these rows are individually audited and every applicable MUST is implemented and verified.
