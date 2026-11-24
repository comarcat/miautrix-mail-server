# IMAP Phase 1 Validation Report

**Scope:** IMAP4rev1 (RFC 3501) for Python `imaplib`, Himalaya, Thunderbird and MailKit, plus delegated shared mailboxes. IMAP4rev2 is out of scope and is not advertised. Production validation is intentionally excluded.

## A. Architecture review

- Authentication resolves a tenant, user and primary `MailboxId` through `IImapAuthenticator`; `INBOX` therefore belongs to the authenticated user.
- Mailbox and folder data are stored in `Mailboxes` and `Folders`; messages carry tenant, mailbox, folder and per-folder UID data. `IMessageUidAllocator` allocates stable non-zero UIDs; `Folder.UidValidity` and `UidNext` supply selection state.
- Shared mailboxes are ordinary `Mailbox` rows with `Kind=shared`. `MailboxDelegate` stores `read` or `write`; Send As/Send on Behalf is a distinct concept and is not granted by IMAP Full Access.
- The web-facing `MailboxService`/`MessageService` and `TenantAuthorizationHelper.AssertMailboxAccess` are the reuse seams. The helper records denial events and hides unavailable resources as not-found.
- Before this change, the IMAP engine kept one login mailbox ID and used it for folder lookup, listing, message queries and write checks. That prevented routing to delegated mailboxes and made selected mailbox authorization insufficiently explicit.
- IMAP is served by `ImapListenerService`; each connection has an `ImapSession` and DbContext. MIME formatting is provided by `MimeKitImapMimeFormatter`.

## B. RFC 3501 / extension compliance matrix

Status describes this repository implementation at audit time. `Partial` means a handler exists but full RFC semantics or evidence is incomplete. An advertised extension requires command-level evidence before it can be treated as complete.

Audit counts: **24 implemented, 6 partial, 5 missing/not advertised, 1 not applicable**. Every advertised capability has a raw protocol assertion; remaining partial items are RFC 3501 command semantics and are not optional capability claims.

| Feature | RFC | Level | Status | Coverage/evidence | Action |
|---|---|---:|---|---|---|
| CAPABILITY | 3501 §6.1.1 | MUST | Implemented | Raw socket asserts baseline and every advertised token | Keep only implemented extensions in the line |
| NOOP | 3501 §6.1.2 | MUST | Implemented | Session command tests | Preserve state and refresh authorization |
| LOGOUT | 3501 §6.1.3 | MUST | Implemented | Session tests | Confirm BYE and tagged completion |
| STARTTLS | 3501 §6.2.1 | Conditional | Missing/not advertised | Listener TLS configuration | Not advertised; implicit TLS deployment only |
| AUTHENTICATE | 3501 §6.2.2 | Conditional | Implemented, advertised | Raw socket SASL PLAIN initial response | Continue testing client authentication paths |
| LOGIN | 3501 §6.2.3 | MUST absent AUTH | Implemented | Authenticator tests | Ensure secure transport policy |
| SELECT | 3501 §6.3.1 | MUST | Partial | Session/MailKit | Selected target and state refresh |
| EXAMINE | 3501 §6.3.2 | MUST | Implemented | Raw listener: [READ-ONLY] on STORE/EXPUNGE/APPEND into selected folder; BODY[] does not set `\\Seen` | Keep selected-folder writes blocked |
| CREATE | 3501 §6.3.3 | MUST | Partial | Session | Parent/name semantics and authorization |
| DELETE | 3501 §6.3.4 | MUST | Partial | Session | INBOX and non-empty handling |
| RENAME | 3501 §6.3.5 | MUST | Partial | Session | INBOX restrictions and namespace collisions |
| SUBSCRIBE/UNSUBSCRIBE | 3501 §6.3.6–7 | MUST | Implemented | Raw listener checks personal/shared opt-out and restore; EF stores per-user opt-out rows | Keep default subscribed; unknown/unauthorized names receive hidden NO |
| LIST | 3501 §6.3.8 | MUST | Implemented | Raw tests cover `*`/`%`, reference, delimiter query, hierarchy attributes, Shared/address parent nodes and hidden unauthorized paths | Preserve accessible parent discovery |
| LSUB | 3501 §6.3.9 | MUST | Implemented | Raw listener asserts default personal/shared folders, opt-out/restore, new folder and delegation visibility, revocation, and `\\Noselect` parents; MailKit `GetFolders` and `GetSubfolders(subscribedOnly: true)` | Preserve access filtering and user-specific opt-outs |
| STATUS | 3501 §6.3.10 | MUST | Partial | Session tests | Target routing and accurate RECENT/UID values |
| APPEND | 3501 §6.3.11 | MUST | Implemented | Raw listener asserts APPENDUID, flags/date preservation, literal+, TRYCREATE for missing target and [READ-ONLY] selected-target rejection | Preserve write authorization and octet framing |
| CHECK | 3501 §6.4.1 | MUST | Implemented | Raw listener asserts CHECK succeeds on a selected mailbox | Preserve selected-state legality |
| CLOSE | 3501 §6.4.2 | MUST | Implemented | Raw listener asserts read-write CLOSE silently expunges `\\Deleted`, while read-only CLOSE preserves it; both clear selection | Keep CLOSE free of untagged EXPUNGE |
| EXPUNGE | 3501 §6.4.3 | MUST | Implemented | Raw listener tests verify permanent deletion, sequence response, and notification to another selected session on NOOP | Preserve mailbox snapshot updates |
| SEARCH | 3501 §6.4.4 | MUST | Implemented | Raw tests cover sequence/UID sets, AND/OR/NOT, dates, HEADER, LARGER/SMALLER, KEYWORD/UNKEYWORD, UTF-8 and BADCHARSET | Keep criteria comparisons and charset responses covered |
| FETCH | 3501 §6.4.5 | MUST | Implemented | Raw tests cover BODY sections, HEADER.FIELDS(.NOT), MIME, TEXT, partial ranges, PEEK/Seen, RFC822.HEADER/TEXT and exact RFC822.SIZE/BODY[] octets | Maintain literal octet lengths and read-only seen behavior |
| STORE | 3501 §6.4.6 | MUST | Implemented | Raw listener covers FLAGS, UID STORE, all system flags, keywords, .SILENT, changed FETCH response, invalid \\Recent BAD, and PERMANENTFLAGS \\* | Keep validation consistent with advertised permanent flags |
| COPY | 3501 §6.4.7 | MUST | Implemented | Raw listener checks multi-message UID COPYUID, TRYCREATE, shared destination authorization; server wraps all copies in one DB transaction | Preserve all-or-nothing transaction |
| UID | 3501 §6.4.8 | MUST | Partial | Session | FETCH/STORE/COPY/SEARCH/EXPUNGE mapping |
| UIDPLUS | 4315 | Optional, advertised | Implemented | Raw APPENDUID, COPYUID and UID EXPUNGE | Keep response codes and UID mapping covered |
| UNSELECT | 3691 | Optional, advertised | Implemented | Raw UNSELECT leaves `\Deleted` message unexpunged | Preserve no-expunge behavior |
| SASL-IR | 4959 | Optional, advertised | Implemented | Raw AUTHENTICATE PLAIN with initial response | Keep initial-response path covered |
| LITERAL+ | 2088 | Optional, advertised | Implemented | Raw APPEND with `{n+}` and no continuation | Preserve non-synchronizing literal collection |
| ENABLE | 5161 | Optional, advertised | Implemented | ENABLE UTF8=ACCEPT returns ENABLED data | Ignore unsupported names; enable advertised extensions only |
| UTF8=ACCEPT | 6855 | Optional, advertised | Implemented | Raw LIST proves modified UTF-7 before ENABLE and UTF-8 after | Keep both mailbox-name representations covered |
| SPECIAL-USE | 6154 | Optional, advertised | Implemented | Raw LIST asserts Sent, Drafts and Trash attributes | Keep role-to-attribute mapping covered |
| IMAP4rev2 | 9051 | Not in Phase 1 | Not advertised | N/A | Do not add in this task |
| NAMESPACE | 2342 | Optional, not advertised | Missing | Not needed for explicit Shared/ prefix | Remains unsupported |
| MOVE | 6851 | Optional, not advertised | Missing | N/A | Phase 2/extension evaluation |
| IDLE | 2177 | Optional, not advertised | Missing | N/A | Phase 2/extension evaluation |
| ACL | 4314 | Optional, not advertised | Missing | N/A | No IMAP permission management |

Response framing additionally depends on RFC 3501 §§7–9: tagged status, untagged data, literals measured in octets, CRLF, IMAP string quoting and state legality. The tests must cover these at protocol level; command acknowledgement alone is not proof of conformance.

## C. Gap analysis

| Severity | Gap | Required correction |
|---|---|---|
| Critical | Mailbox resolution and all data queries assume the login mailbox | Route personal and shared folder names to a resolved mailbox; keep INBOX personal |
| Critical | Access check originally used only the login mailbox | Resolve mailbox on each command and call `AssertMailboxAccess` for read/write; refresh on every operation |
| Critical | Shared mailbox discovery and status must remain authorization-aware | Recheck every LIST/LSUB/STATUS target and preserve identical non-disclosure for unknown and unauthorized paths |
| High | Writes and cross-folder COPY can use the wrong mailbox or insufficient destination authorization | Check selected source and destination independently; preserve delegate `read` versus `write` |
| High | Remaining RFC semantics: SELECT/UID completeness, STATUS RECENT accuracy, and CREATE/DELETE/RENAME rules | Close remaining command gaps and keep behavior under protocol tests |
| High | Server exceptions can escape or be misreported as malformed commands | Log safe exception type/context; return `NO`/`[SERVERBUG]`, preserve framing/state |
| Medium | External client validation is only a manual deployed-server checklist | Extend env-only script for shared LIST/SELECT/FETCH/SEARCH and document manual clients |
| Medium | Namespace address collision/rename behavior needs stable definition | Use exact shared mailbox address as identifier; document normalized matching and rename visibility |

### Namespace and authorization contract

Shared folders use `Shared/<mailbox-address>/<folder-path>`, with `/` as hierarchy delimiter. The address component is matched case-insensitively against the mailbox's complete email address; local-part-only aliases are intentionally not used, avoiding same-local-part collisions across domains. Address changes change the visible path. `INBOX` always resolves to the authenticated user's own inbox. Every LIST/LSUB/STATUS candidate and every selected/operated mailbox is checked using the existing helper; access is re-read on each mailbox command, so revocation applies to an established session's next operation. Unauthorized and nonexistent targets both receive `NO Mailbox does not exist`.

The restored advertised capability line is:

```text
* CAPABILITY IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE
```

`IMAP4rev1` is the baseline. `AUTH=PLAIN` and `SASL-IR` are tested on the raw TLS socket; `LITERAL+` is tested using a non-synchronizing APPEND; ENABLE/UTF8=ACCEPT switches between RFC 3501 modified UTF-7 and RFC 6855 UTF-8 mailbox paths; UNSELECT is verified not to expunge; UIDPLUS response codes and UID EXPUNGE are checked; SPECIAL-USE role attributes are asserted. Client behavior changes from the previous bare-baseline line: Thunderbird/Himalaya can identify special folders, clients can use UIDPLUS responses and UTF8=ACCEPT, and SASL/literal extensions are explicitly discoverable again. These extension tests do not imply full completion of the remaining mandatory command gaps.

## D. Implementation delivered in this work

`ImapSession` now preserves the authenticated user's mailbox identity separately and derives the active mailbox from the selected folder. Shared paths resolve by full address, shared LIST output comes from the existing authorized mailbox service, and SELECT, STATUS, APPEND, COPY, folder mutations and selected-message operations authorize the resolved mailbox. Selected operations re-check read access; writes re-check write access, and COPY separately checks source read and destination write. A delegate row with `CanSendAs` but no `read`/`write` access no longer grants IMAP access. INBOX remains the authenticated user's own inbox. Runtime faults are logged by exception type only and produce `NO [SERVERBUG]` without command data. ENABLE implements UTF8=ACCEPT and returns ENABLED data; raw listener tests accept modified UTF-7 before ENABLE, reject raw UTF-8 before ENABLE, and accept UTF-8 mailbox names and quoted strings after ENABLE. EXPUNGE permanently deletes, while the web controller's default `permanent=false` continues to move non-Trash items into Trash.

SUBSCRIBE and UNSUBSCRIBE now persist explicit per-user/per-folder opt-outs. Accessible personal and shared folders are subscribed by default; newly created folders and newly delegated mailboxes therefore appear automatically. LSUB rechecks authorization, filters only the authenticated user's opt-outs and includes `\\Noselect` parent nodes. SUBSCRIBE/UNSUBSCRIBE return the same hidden NO for unknown and unauthorized names. Migration `20261124215210_ImapFolderSubscriptionOptOuts` adds the boolean with `ADD COLUMN IF NOT EXISTS` and defaults legacy rows to subscribed; the earlier table migration uses `CREATE TABLE/INDEX IF NOT EXISTS`. Both migrations were applied to the guarded `miautrix-mail-dev` database, and the UID allocator integration fixture applied the full migration chain successfully to throwaway databases (3/3 tests).

EXAMINE selects read-only: STORE and EXPUNGE return NO [READ-ONLY], APPEND into that same selected folder returns NO [READ-ONLY], and body FETCH does not set `\\Seen`. CLOSE expunges `\\Deleted` messages without untagged EXPUNGE only for read-write selection; read-only CLOSE preserves them. CHECK is accepted only while a mailbox is selected.

Known implementation boundary: the raw listener test now exercises personal, write-delegated, read-delegated, Send As-only and cross-tenant targets; it checks shared LIST/SELECT/EXAMINE/STATUS/FETCH/UID FETCH/SEARCH/STORE/COPY/APPEND/EXPUNGE/CREATE/RENAME/DELETE outcomes and database ownership, tests forged/case-changed/traversal/trailing-slash names, revokes access mid-session, and runs two delegates concurrently through flag updates, expunge notifications and parallel APPEND UID allocation. Subscription semantics are covered over the raw listener and MailKit, including default subscription, opt-outs, new folders/delegations and revocation. Groups 8–9 remain incomplete: full CREATE/DELETE/RENAME rules, complete SELECT state responses, comprehensive UID mapping and every shared negative-command combination remain to be verified; STATUS RECENT currently reports zero because the storage model has no per-session recent tracking.

## E. Automated evidence

| Verification | Result |
|---|---|
| `dotnet build Miautrix.Mail.sln -warnaserror` | PASS, 0 warnings, 0 errors |
| `dotnet test tests/Miautrix.Mail.ProtocolTests` | PASS, 27/27; includes MailKit and expanded raw listener delegated-access tests |
| `dotnet test tests/Miautrix.Mail.SecurityTests` | PASS, 7/7; includes Send As-only access denial |
| UID allocator integration filter | PASS, 3/3 |
| `dotnet test tests/Miautrix.Mail.UnitTests` | PASS, 101/101 |
| External Python syntax parse | PASS |
| Deployed-client validation (imaplib/Himalaya/Thunderbird) | BLOCKED by scope: no production or deployed instance access was used |

## F–H. External validation, documentation and final state

The external kit is `scripts/imap-external-validation.py`; set `MIAUTRIX_IMAP_SHARED_MAILBOX` to probe `Shared/<address>`. It verifies authorized shared LIST/EXAMINE/SEARCH/FETCH and RFC822.SIZE equality without printing bodies. Manual Himalaya and Thunderbird checklists and connection settings are in `docs/IMAP.md`. This report is the final evidence record; it does **not** claim full Phase 1 acceptance while the remaining matrix combinations, partial RFC command semantics and deployed-client checks remain unresolved.
