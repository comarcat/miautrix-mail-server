# IMAP access

The server advertises `IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE NAMESPACE MOVE IDLE`. IMAP4rev2 is not advertised. Use port 993 with implicit TLS and the server hostname covered by its certificate.

`ENABLE IMAP4rev2` switches the current session to rev2 response semantics. Implemented and tested rev2 behavior includes ESEARCH/SEARCHRES, LIST-EXTENDED with LIST STATUS and STATUS=SIZE, LITERAL- (including the exact 4096/4097-octet boundary), BINARY fetch items with exact UNKNOWN-CTE handling, omission of `\\Recent`/RECENT, and the CLOSED response code. Predefined `$` keywords (`$Forwarded`, `$MDNSent`, `$Junk`, `$NotJunk`, `$Phishing`) are searchable and storable in both rev1 and rev2 sessions. IMAP4rev2 remains unadvertised until every MUST requirement has been implemented and verified; see [IMAP Phase 2 report](IMAP-Phase2-Report.md). STARTTLS is rejected with tagged BAD on this Implicit TLS listener, as required for an already TLS-protected port. LOGIN and AUTH=PLAIN are available because the connection is protected by TLS.

Mailbox names use RFC 3501 modified UTF-7 until the client sends `ENABLE UTF8=ACCEPT`. After the tagged ENABLE succeeds and `* ENABLED UTF8=ACCEPT` is returned, UTF-8 mailbox names and quoted strings are accepted and returned in UTF-8. Raw non-ASCII mailbox names before ENABLE receive BAD; clients should use modified UTF-7 until that point.

## Login and personal mailbox

Authenticate with the user's full mailbox address and password (or supported application password). The authenticated identity remains the user's own mailbox for the entire session: `INBOX` always means that mailbox's inbox.

Example Thunderbird account settings:

- Incoming server: your IMAP hostname, port 993, SSL/TLS.
- Username: full email address.
- Authentication: Normal password (or application password where enabled).
- Folder subscription/list refresh: use the `Shared/...` folders returned by LIST.

Example Himalaya account (adapt the account name and address):

```toml
[accounts.work]
email = "user@example.test"
server = "imap.example.test"
port = 993
encryption = "tls"
```

## Shared mailboxes

Delegated folders appear below `Shared/<full-mailbox-address>/<folder>`, for example `Shared/sales@example.test/INBOX`. The complete address is used to avoid collisions between identical local parts in different domains; address matching is case-insensitive. Renaming a mailbox address changes its path. A folder rename changes only the path below that mailbox.

LIST, LSUB and STATUS expose only mailboxes authorized for the authenticated user. SELECT/EXAMINE, SEARCH, FETCH, STORE, EXPUNGE, APPEND, CREATE, DELETE, RENAME and COPY re-check access against the resolved mailbox. Unknown, cross-tenant and unauthorized names return the same `NO Mailbox does not exist` response. Permissions are refreshed for each operation on an already-open connection: if a delegate is revoked while a shared folder is selected, the next FETCH/STORE/SELECT fails with that same NO response and returns no message data; reconnect or select the authenticated user's own INBOX to continue. Read delegation permits reading; write delegation permits changes. Send As and Send on Behalf alone do not grant IMAP read or write access, and those sending features are not implemented by IMAP.

Accessible personal and shared folders are subscribed by default, including folders and delegations created later. UNSUBSCRIBE records a per-user opt-out; SUBSCRIBE removes it. LSUB returns all accessible folders except explicit opt-outs and includes `\Noselect` parents needed to navigate to a subscribed child. A revoked shared folder disappears from both LIST and LSUB; a future delegation appears automatically. For SUBSCRIBE/UNSUBSCRIBE, an unknown or unauthorized name returns the same `NO Mailbox does not exist` response as other mailbox commands, concealing whether the name exists.

When a message is marked `\\Deleted`, IMAP EXPUNGE permanently removes it from that mailbox and reports the expunged sequence number. This differs from the web message delete action: its default is still `permanent=false`, which moves a message to Trash; only an explicit permanent delete or deleting a message already in Trash removes it. The IMAP EXPUNGE handler passes the explicit permanent flag, so it does not route expunged messages through Trash.

EXAMINE opens a folder read-only. STORE and EXPUNGE are rejected, APPEND to that same selected folder returns `NO [READ-ONLY]`, and FETCH BODY[] does not set `\\Seen`. CLOSE silently expunges deleted messages when selected read-write; on a read-only selection it leaves them in place. CHECK is valid only after SELECT or EXAMINE. EXAMINE and read-only NOOP observe pending `\\Recent` messages without consuming them. A read-write SELECT/NOOP claims pending recent UIDs atomically for that session; when the server cannot determine which session was first notified (including concurrent sessions), it conservatively reports the pending messages as recent as RFC 3501 recommends. STATUS reports pending messages without claiming them, so repeated STATUS calls do not change subsequent selection state.

NAMESPACE reports a personal namespace with the empty prefix and `/` delimiter, no Other Users namespace, and the shared namespace prefix `Shared/` with `/` delimiter. Clients can discover these roots and then LIST authorized paths. Do not configure a shared mailbox address as the login identity to access a delegate mailbox.

MOVE and UID MOVE move selected messages atomically to an authorized destination, assigning fresh destination UIDs. A successful command returns COPYUID and reports source EXPUNGE responses in descending sequence order. The destination requires write access; a missing destination returns TRYCREATE, and unauthorized destinations are hidden as nonexistent. Read-only selections cannot move messages.

IDLE sends the continuation `+ idling` and remains active until the client sends `DONE`. The server polls mailbox state every two seconds by default and pushes EXISTS, EXPUNGE and FETCH FLAGS updates from other IMAP sessions and database writers. Authorization is rechecked each poll; revocation ends visibility and clears selection. The server ends an IDLE after 30 minutes. Polling costs one mailbox snapshot query per selected idle session per interval; deployment operators should account for this load when sizing concurrent connections.

## Validation

Set host, username and password only in environment variables and run:

```powershell
python scripts/imap-external-validation.py
```

To check one delegated mailbox, also set `MIAUTRIX_IMAP_SHARED_MAILBOX` to its full address. The script reports command status, counts and byte lengths only; it does not print message contents. Use `MIAUTRIX_IMAP_INSECURE=1` only for temporary IP-only diagnostics, then validate with the normal hostname and certificate.

For manual Himalaya validation, list mailboxes and confirm Sent, Drafts, Archive, Junk, Trash and `Shared/<address>` appear by default; select a path such as `Shared/sales@example.test/INBOX`. Unsubscribe one folder, refresh, subscribe it again, and verify it disappears then returns. In Thunderbird, expand the account's folder list, confirm default personal folders and `Shared/<address>` are visible without manually subscribing, open shared `INBOX`, fetch a message and refresh the folder list. Repeat after removing delegate access; the next command in an existing session must return NO and the shared folders must disappear from LIST/LSUB.

Before deployment, apply all pending EF migrations, including `20261124222105_ImapRecentMessages`, `20261124223011_ImapRecentLegacyBaseline` and `20261124224654_ImapFolderUidValiditySequence` (plus the existing folder-subscription migrations). Validate that delegates have `access_level` set to exactly `read` or `write`; Send As / Send on Behalf permission alone does not expose IMAP. Tell users that IMAP EXPUNGE permanently deletes `\\Deleted` messages; the web UI's normal delete action still moves messages to Trash.

For test failures requiring PostgreSQL, follow [Troubleshooting §9 and §14](TROUBLESHOOTING.md); never point test variables at production.

## Known limitations

The server does not claim IMAP4rev2 or advertise ACL/STARTTLS; deployment uses implicit TLS. IMAP4rev2 stays unadvertised because full BINARY boundaries, response-code semantics, and the complete paragraph-level RFC MUST audit remain open; see [IMAP Phase 2 report](IMAP-Phase2-Report.md) for the gap matrix. External-client validation still must be run by the owner against the deployed host. See [IMAP Phase 1 report](IMAP-Phase1-Report.md) for the rev1 command audit and evidence status.
