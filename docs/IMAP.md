# IMAP access

The server advertises `IMAP4rev1 AUTH=PLAIN SASL-IR LITERAL+ ENABLE UTF8=ACCEPT UNSELECT UIDPLUS SPECIAL-USE`. IMAP4rev2, NAMESPACE, MOVE and IDLE are not advertised. Use port 993 with implicit TLS and the server hostname covered by its certificate.

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

EXAMINE opens a folder read-only. STORE and EXPUNGE are rejected, APPEND to that same selected folder returns `NO [READ-ONLY]`, and FETCH BODY[] does not set `\\Seen`. CLOSE silently expunges deleted messages when selected read-write; on a read-only selection it leaves them in place. CHECK is valid only after SELECT or EXAMINE.

The explicit namespace does not provide RFC 2342 NAMESPACE discovery. Clients should use LIST and select the returned shared paths. Do not configure a shared mailbox address as the login identity to access a delegate mailbox.

## Validation

Set host, username and password only in environment variables and run:

```powershell
python scripts/imap-external-validation.py
```

To check one delegated mailbox, also set `MIAUTRIX_IMAP_SHARED_MAILBOX` to its full address. The script reports command status, counts and byte lengths only; it does not print message contents. Use `MIAUTRIX_IMAP_INSECURE=1` only for temporary IP-only diagnostics, then validate with the normal hostname and certificate.

For manual Himalaya validation, list mailboxes and select a path such as `Shared/sales@example.test/INBOX`. In Thunderbird, expand the account's folder list, locate `Shared/<address>`, open `INBOX`, fetch a message and refresh the folder list. Repeat after removing delegate access; the next command in an existing session must return NO.

For test failures requiring PostgreSQL, follow [Troubleshooting §9 and §14](TROUBLESHOOTING.md); never point test variables at production.

## Known limitations

The server does not claim IMAP4rev2 or advertise IDLE/MOVE/ACL/NAMESPACE. Some RFC 3501 edge cases, full external-client validation and portions of the shared-mailbox command matrix require further compatibility verification. See [IMAP Phase 1 report](IMAP-Phase1-Report.md) for the command-by-command audit and evidence status.
