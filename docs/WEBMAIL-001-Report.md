# WEBMAIL-001: IMAP APPEND drafts in webmail

## Findings and changes

APPEND stored the raw RFC 5322 bytes but only copied `From`, `To`, and `Subject` into the `Message` row. The webmail editor reads structured message fields, so the raw message could be fetched over IMAP while the editor appeared empty.

`HandleAppendAsync` now passes the stored item to `MessageService.AppendStoredMessageAsync`. That Application method parses the stored bytes through the shared MimeKit parser and writes sender, recipients (including Cc and Bcc), subject, date, bodies, raw headers, UID, flags, size, hash, and attachment rows. The original stored message bytes remain untouched, and `SizeBytes` comes from the storage result.

`UpsertDraftAsync` now writes its MIME builder output to mail storage, includes uploaded attachments in the rebuilt MIME message, and includes Bcc in saved draft MIME. A save whose editable fields and recipients match the existing row is a no-op; this prevents an untouched draft from being rewritten by autosave. A changed save retains the message ID used by webmail and assigns the row a newly allocated folder UID; the previous UID no longer resolves, `UIDNEXT` advances, and `UIDVALIDITY` is unchanged. This is the database representation of expunge plus append; the old row is updated transactionally rather than physically deleted and reinserted because the API draft ID is the message ID.

The composer hydrates either `BodyHtml` or `BodyText` and runs autosave on a serialized 30-second timeout only after user edits. The body-only and html-only Vitest cases verify the body is displayed and an untouched timeout does not call the draft-save API.

`MessageReparseService` already parses every stored message, including Drafts, and backfills `BodyText`, `BodyHtml`, and subject. The ProtocolTests regression covers an APPEND-style legacy message with empty body columns.

## Existing production draft backfill

After deployment, preview the changes first:

```powershell
dotnet run --project src/Miautrix.Mail.Cli -- messages reparse --tenant all --dry-run
```

Review the reported `updated` and `failed` counts. Then apply the backfill:

```powershell
dotnet run --project src/Miautrix.Mail.Cli -- messages reparse --tenant all
```

No migration is required. Reparse reads stored message content and does not change the raw message bytes or message UIDs.

## Verification

The current regression run passed:

| Suite | Passed |
|---|---:|
| `dotnet build Miautrix.Mail.sln -warnaserror -m:1` | 0 warnings, 0 errors |
| UnitTests | 101 |
| SecurityTests | 7 |
| ProtocolTests | 30 |
| IntegrationTests sender authorization, UID allocator, and HTTP/IMAP draft round trip | 11 |
| Webmail Vitest | 17 |

ProtocolTests includes personal/shared APPEND cases with plain, HTML, multipart attachment, Unicode subject, and folded headers, and confirms the message detail service fields. It also includes the legacy reparse backfill and shared draft read/write authorization cases. The integration test drives APPEND, GET, PUT, and GET against the real IMAP session and HTTP endpoints for both personal and shared Drafts; a second selected session observes EXPUNGE plus EXISTS on NOOP, and FETCH by the new UID returns the edited RFC 5322 content with one stored row.

## Remaining acceptance work

The shared access test runs the real `TenantAuthorizationHelper` against a database-backed mailbox; the integration round trip additionally uses a persisted write delegate and confirms no Send As permission is needed to save. The IMAP session test drives the protocol session command handler rather than opening a TLS socket; listener framing and protocol behavior remain independently covered by ProtocolTests.
