# IMAP Phase 1 Validation Report

**Scope:** IMAP4rev1 (RFC 3501) for Python `imaplib`, Himalaya, Thunderbird and MailKit, plus delegated shared mailboxes. IMAP4rev2 is out of scope and is not advertised. Production validation is intentionally excluded.

## A. Architecture review

- Authentication resolves a tenant, user and primary `MailboxId` through `IImapAuthenticator`; `INBOX` therefore belongs to the authenticated user.
- Mailbox and folder data are stored in `Mailboxes` and `Folders`; messages carry tenant, mailbox, folder and per-folder UID data. `IMessageUidAllocator` allocates stable non-zero UIDs; `Folder.UidValidity` and `UidNext` supply selection state.
- Shared mailboxes are ordinary `Mailbox` rows with `Kind=shared`. `MailboxDelegate` stores `read` or `write`; Send As/Send on Behalf is a distinct concept and is not granted by IMAP Full Access.
- `Message.IsRecent` stores whether a new message is still pending recent assignment. A read-write SELECT/NOOP claims UIDs atomically; EXAMINE and read-only NOOP only observe pending flags and never consume them. A database sequence assigns non-reused UIDVALIDITY values to new folders; ordinary rename preserves folder identity, while delete/recreate receives a new value.
- The web-facing `MailboxService`/`MessageService` and `TenantAuthorizationHelper.AssertMailboxAccess` are the reuse seams. The helper records denial events and hides unavailable resources as not-found.
- Before this change, the IMAP engine kept one login mailbox ID and used it for folder lookup, listing, message queries and write checks. That prevented routing to delegated mailboxes and made selected mailbox authorization insufficiently explicit.
- IMAP is served by `ImapListenerService`; each connection has an `ImapSession` and DbContext. MIME formatting is provided by `MimeKitImapMimeFormatter`.

## B. RFC 3501 / extension compliance matrix

Status describes this repository implementation at audit time. `Partial` means a handler exists but full RFC semantics or evidence is incomplete. An advertised extension requires command-level evidence before it can be treated as complete.

Final audit counts: **30 implemented, 0 partial, 5 missing/not advertised, 1 not applicable**. Every RFC 3501 MUST command row is implemented. The five missing rows are unadvertised optional extensions or conditional STARTTLS; the not-applicable row is IMAP4rev2.

| Feature | RFC | Level | Status | Coverage/evidence | Action |
|---|---|---:|---|---|---|
| CAPABILITY | 3501 §6.1.1 | MUST | Implemented | Raw socket asserts baseline and every advertised token | Keep only implemented extensions in the line |
| NOOP | 3501 §6.1.2 | MUST | Implemented | Session command tests | Preserve state and refresh authorization |
| LOGOUT | 3501 §6.1.3 | MUST | Implemented | Session tests | Confirm BYE and tagged completion |
| STARTTLS | 3501 §6.2.1 | Conditional | Missing/not advertised | Listener TLS configuration | Not advertised; implicit TLS deployment only |
| AUTHENTICATE | 3501 §6.2.2 | Conditional | Implemented, advertised | Raw socket SASL PLAIN initial response | Continue testing client authentication paths |
| LOGIN | 3501 §6.2.3 | MUST absent AUTH | Implemented | Authenticator tests | Ensure secure transport policy |
| SELECT | 3501 §6.3.1 | MUST | Implemented | Raw listener asserts FLAGS, EXISTS, RECENT, UNSEEN, PERMANENTFLAGS, UIDNEXT, UIDVALIDITY; repeated SELECT refreshes selected-mailbox state; MailKit opens selected folders | Preserve state refresh and read-only mode |
| EXAMINE | 3501 §6.3.2 | MUST | Implemented | Raw listener: [READ-ONLY] on STORE/EXPUNGE/APPEND into selected folder; BODY[] does not set `\\Seen` | Keep selected-folder writes blocked |
| CREATE | 3501 §6.3.3 | MUST | Implemented | Raw listener checks hierarchy, missing-parent TRYCREATE, collisions, personal/shared ownership and write authorization | Keep names and parents in the resolved namespace |
| DELETE | 3501 §6.3.4 | MUST | Implemented | Raw listener checks custom personal/shared deletion, refusal to remove inferior hierarchy names, INBOX and special-use protection, UIDVALIDITY change on delete/recreate, and DB ownership | Keep built-in folders protected |
| RENAME | 3501 §6.3.5 | MUST | Implemented | Raw listener checks collision, special-use protection, shared custom-folder rename, stable UIDVALIDITY, and INBOX message move with DB ownership assertions | Preserve folder UID identity for ordinary rename; INBOX move creates a new destination identity |
| SUBSCRIBE/UNSUBSCRIBE | 3501 §6.3.6–7 | MUST | Implemented | Raw listener checks personal/shared opt-out and restore; EF stores per-user opt-out rows | Keep default subscribed; unknown/unauthorized names receive hidden NO |
| LIST | 3501 §6.3.8 | MUST | Implemented | Raw tests cover `*`/`%`, reference, delimiter query, hierarchy attributes, Shared/address parent nodes and hidden unauthorized paths | Preserve accessible parent discovery |
| LSUB | 3501 §6.3.9 | MUST | Implemented | Raw listener asserts default personal/shared folders, opt-out/restore, new folder and delegation visibility, revocation, and `\\Noselect` parents; MailKit `GetFolders` and `GetSubfolders(subscribedOnly: true)` | Preserve access filtering and user-specific opt-outs |
| STATUS | 3501 §6.3.10 | MUST | Implemented | Raw listener checks shared target counters and repeated RECENT queries; STATUS is read-only and does not claim \\Recent | Keep STATUS free of mailbox-state side effects |
| APPEND | 3501 §6.3.11 | MUST | Implemented | Raw listener asserts APPENDUID, flags/date preservation, literal+, TRYCREATE for missing target and [READ-ONLY] selected-target rejection | Preserve write authorization and octet framing |
| CHECK | 3501 §6.4.1 | MUST | Implemented | Raw listener asserts CHECK succeeds on a selected mailbox | Preserve selected-state legality |
| CLOSE | 3501 §6.4.2 | MUST | Implemented | Raw listener asserts read-write CLOSE silently expunges `\\Deleted`, while read-only CLOSE preserves it; both clear selection | Keep CLOSE free of untagged EXPUNGE |
| EXPUNGE | 3501 §6.4.3 | MUST | Implemented | Raw listener tests verify permanent deletion, sequence response, and notification to another selected session on NOOP | Preserve mailbox snapshot updates |
| SEARCH | 3501 §6.4.4 | MUST | Implemented | Raw tests cover sequence/UID sets, AND/OR/NOT, dates, HEADER, LARGER/SMALLER, KEYWORD/UNKEYWORD, UTF-8 and BADCHARSET | Keep criteria comparisons and charset responses covered |
| FETCH | 3501 §6.4.5 | MUST | Implemented | Raw tests cover BODY sections, HEADER.FIELDS(.NOT), MIME, TEXT, partial ranges, PEEK/Seen, RFC822.HEADER/TEXT and exact RFC822.SIZE/BODY[] octets | Maintain literal octet lengths and read-only seen behavior |
| STORE | 3501 §6.4.6 | MUST | Implemented | Raw listener covers FLAGS, UID STORE, all system flags, keywords, .SILENT, changed FETCH response, invalid \\Recent BAD, and PERMANENTFLAGS \\* | Keep validation consistent with advertised permanent flags |
| COPY | 3501 §6.4.7 | MUST | Implemented | Raw listener checks multi-message UID COPYUID, TRYCREATE, shared destination authorization; server wraps all copies in one DB transaction | Preserve all-or-nothing transaction |
| UID | 3501 §6.4.8 | MUST | Implemented | Raw listener exercises UID FETCH/STORE/COPY/SEARCH/EXPUNGE; UID EXPUNGE emits sequence updates and COPY/APPEND return UIDPLUS mapping | Keep UID set mapping distinct from sequence sets |
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
| High | Concurrent assignment of RFC \\Recent and mailbox mutation behavior | Atomically assign recent UIDs to the first observing session; test status/select semantics and mutations over the real listener |
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

EXAMINE selects read-only: STORE and EXPUNGE return NO [READ-ONLY], APPEND into that same selected folder returns NO [READ-ONLY], body FETCH does not set `\\Seen`, and EXAMINE does not clear persisted `\\Recent`. CLOSE expunges `\\Deleted` messages without untagged EXPUNGE only for read-write selection; read-only CLOSE preserves them. CHECK is accepted only while a mailbox is selected. A read-write SELECT/NOOP claims pending recent UIDs atomically for that session; a read-only session observes pending recent messages without consuming them. This is the conservative RFC 3501 §2.3.2 behavior when the server cannot reliably determine which session was first notified (including simultaneous sessions). STATUS counts pending UIDs plus that session's claimed UIDs without changing either set, so repeated STATUS does not consume recent state.

The raw listener test exercises personal, write-delegated, read-delegated, Send As-only and cross-tenant targets; it checks shared LIST/SELECT/EXAMINE/STATUS/FETCH/UID FETCH/SEARCH/STORE/COPY/APPEND/EXPUNGE/CREATE/RENAME/DELETE outcomes and database ownership, tests forged/case-changed/traversal/trailing-slash names, revokes access mid-session, and runs two delegates concurrently through flag updates, expunge notifications and parallel APPEND UID allocation. Subscription semantics are covered over the raw listener and MailKit, including default subscription, opt-outs, new folders/delegations and revocation. Group 7 now checks non-consuming STATUS RECENT, session \\Recent assignment, and shared target counters; group 8 checks hierarchy, collisions, special-folder restrictions, delegated folder mutation, stable UIDVALIDITY on custom-folder rename, and INBOX message movement; group 9 checks all SELECT state fields, re-selection, and UID command mappings. No RFC 3501 MUST row remains partial; remaining unadvertised optional features and deployed-client checks are listed below.

## E. Automated evidence

| Verification | Result |
|---|---|
| `dotnet build Miautrix.Mail.sln -warnaserror` | PASS, 0 warnings, 0 errors |
| `dotnet test tests/Miautrix.Mail.ProtocolTests` | PASS, 27/27; includes MailKit and expanded raw listener delegated-access tests |
| `dotnet test tests/Miautrix.Mail.SecurityTests` | PASS, 7/7; includes Send As-only access denial |
| UID allocator integration filter | PASS, 3/3 |
| `dotnet test tests/Miautrix.Mail.UnitTests` | PASS, 101/101 |
| External Python syntax parse | PASS |
| Deployed-client validation (imaplib/Himalaya/Thunderbird) | PASS: owner validated on production 2026-11-24 (v2.0.0, `76c8e7c`), including `Shared/sales` and Send As |

## F–H. External validation, documentation and final state

### Objective evidence

- Raw TLS protocol tests exercise the actual `ImapListenerService`; they check exact tagged and untagged replies, literal octet lengths, personal/shared routes, permission denial, revocation, concurrent delegates, flag/expunge synchronization, UID uniqueness, subscriptions, folder mutations and UID command mappings.
- MailKit tests cover authentication, selected-folder access, MIME/FETCH behavior, folder enumeration, and `GetSubfolders(subscribedOnly: true)` for the default subscribed folder set.
- `Message.IsRecent` uses an EF mapped boolean. `ImapRecentMessages` adds it idempotently and defaults old rows to recent; `ImapRecentLegacyBaseline` converges rows created under the earlier applied dev migration. `ImapFolderUidValiditySequence` seeds a monotonic sequence above existing folder values and makes it the database default. The dev database is guarded by the connection-name check and was already at the latest migration after update; the UID allocator integration fixture applies the complete chain to fresh throwaway databases.
- All RFC 3501 MUST rows are `Implemented`; no command row is `Partial`. STARTTLS, NAMESPACE, MOVE, IDLE and ACL are not advertised (conditional STARTTLS / optional extensions), and IMAP4rev2 is outside Phase 1.

### Owner's external-client checklist

Run the Python probe with environment variables only: `MIAUTRIX_IMAP_HOST`, `MIAUTRIX_IMAP_USER`, `MIAUTRIX_IMAP_PASSWORD`, and optionally `MIAUTRIX_IMAP_SHARED_MAILBOX=sales@example.test`; run `python scripts/imap-external-validation.py`. Confirm default personal folders are returned by LSUB, shared LIST/LSUB exposes `Shared/sales@example.test`, EXAMINE/SEARCH/FETCH succeed, and RFC822.SIZE matches fetched BODY.PEEK[] bytes. The script prints counts and lengths, never credentials or message content.

For Himalaya, configure implicit TLS on port 993 with the user's own full email address, list folders, check Sent/Drafts/Archive/Junk/Trash and `Shared/sales@example.test` are visible by default, open the shared INBOX, then unsubscribe/resubscribe one folder and verify it disappears/returns. For Thunderbird, subscribe/refresh the account folder list, confirm those personal and shared folders appear without manual initial subscription, open and fetch from `Shared/sales@example.test/INBOX`, opt one folder out and back in, and verify a revoked delegate loses the shared namespace on the next refresh and the next command in an open session returns the same NO as a nonexistent mailbox.

### Pre-deploy notes and limitations

Apply all pending migrations before starting the new worker: `20261124214216_ImapFolderSubscriptions`, `20261124215210_ImapFolderSubscriptionOptOuts`, `20261124222105_ImapRecentMessages`, `20261124223011_ImapRecentLegacyBaseline`, and `20261124224654_ImapFolderUidValiditySequence` (along with any older pending migrations). Delegate `access_level` must be exactly `read` or `write`; Send As / Send on Behalf grants no IMAP access. IMAP EXPUNGE permanently deletes `\\Deleted` messages; ordinary web UI deletion still moves messages to Trash. No production deployment or live Himalaya/Thunderbird validation was performed; the owner checklist above is still required against the deployed host.

`docs/IMAP.md` contains client setup, shared namespace, subscription defaults, recent behavior, revocation, EXPUNGE semantics and troubleshooting. The server uses implicit TLS; STARTTLS and optional NAMESPACE/MOVE/IDLE/ACL remain unadvertised, and IMAP4rev2 evaluation belongs to Phase 2.
