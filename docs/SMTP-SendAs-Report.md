# SMTP Send As / Send on Behalf

## Status: implementation updated; full verification pending

Implemented a shared Application sender policy and wired it into SMTP submission and webmail. The delegate model and API contract now expose independent `CanSendAs` and `CanSendOnBehalf` flags, defaulting false; Full Access (`AccessLevel=write`) grants neither sending permission. The admin picker exposes separate checkboxes. SMTP rejects invalid envelope senders at MAIL FROM and invalid From headers after DATA with `550 5.7.1`, does not disclose cross-tenant mailbox existence, and records denial actor, attempted address domain, and reason in `audit_logs` without recording message content. Send As strips any supplied Sender header; Send on Behalf overwrites it with the authenticated user's mailbox address and requires the user's own envelope sender. Missing/malformed From and null envelope senders are rejected.

The common policy is implemented in `src/Miautrix.Mail.Application/Mail/ISenderAuthorizationService.cs` and `SenderAuthorizationService.cs`. SMTP calls it in `SmtpSubmissionHandler` and `SmtpListenerService`; webmail calls the same policy from `MessageService.SendMessageInternalAsync`. Webmail retains shared and personal Sent copies, and sender grants can authorize sending from a shared mailbox independently of Full Access.

Authorization preserves the original message bytes when no Sender header change is required. Send on Behalf replaces the complete Sender field, including continuation lines, while preserving other header bytes and body. Send As removes Sender fields; simultaneous Send As and Send on Behalf grants use Send As precedence. `MarkReadAsync` requires normal mailbox write access regardless of sender grants. Rejection auditing now uses a direct database insert so it does not flush caller-tracked messages or queue rows. Webmail recomputes message size from the authorized final message before storing and queuing it.

## Permission verdicts

| Case | Code status |
|---|---|
| Own mailbox / alias | WORKS; TLS socket submission and DB policy test pass |
| Shared Send As | WORKS; queue has the authenticated envelope and no Sender header |
| Shared Send on Behalf | WORKS; queue has own envelope and Sender overwritten to delegate address |
| Full Access only / no sender grant | WORKS; DATA rejected with 550 5.7.1 and no queue item |
| Unauthorized / cross-tenant / external From | WORKS; cross-tenant and unknown envelope replies match |
| Null envelope / forged From | WORKS; MAIL FROM or DATA rejected with 550 5.7.1 and no queue item |
| SMTP AUTH without TLS | WORKS; advertised only after TLS, cleartext auth rejected |
| Admin grant/edit and tenant isolation | WORKS; API test verifies both flags persisted and foreign mailbox edit returns 404 |

## Migration and verification

Added migration `20261124203756_AddMailboxDelegateSenderPermissions` with `ADD COLUMN IF NOT EXISTS ... boolean NOT NULL DEFAULT false`, null backfill, and explicit default/not-null enforcement. Existing delegates therefore receive no sender grants. The migration's repeatable SQL was executed twice on the verified `miautrix-mail-dev` DB and twice on a fresh migrated `uidtest_*` database; the schema check confirmed both columns remain NOT NULL with false defaults. Integration tests created and dropped the throwaway databases. No production host or database was accessed.

| Suite | Result |
|---|---|
| `dotnet build Miautrix.Mail.sln -warnaserror` | Blocked by existing `tests/Miautrix.Mail.ProtocolTests/Imap/ImapListenerRawSocketTests.cs` compile error (`StubAuthenticator.AddAccount` missing); 0 warnings |
| UnitTests | Passed: 101 tests |
| SecurityTests | Passed: 7 tests |
| ProtocolTests | Blocked by compile errors in `ImapListenerRawSocketTests.cs`; no IMAP files changed for this task |
| IntegrationTests (SenderAuthorization, MessageUidAllocator, SharedMailbox filter) | Passed: 9 tests, including the new write authorization, rejection persistence, and final stored-byte/hash/size assertions |
| Admin vitest | Passed: 18 tests across 7 files |
| Webmail vitest | Passed: 15 tests across 2 files |

The integration sender-policy test checks the Application policy on throwaway PostgreSQL databases; the protocol listener test uses the dev DB for grants and a real implicit-TLS TCP socket. The API test covers create/edit and cross-tenant 404 behavior.

## Owner steps

After deploying the migration and UI, open each shared mailbox's delegate editor and explicitly grant **Send As** or **Send on Behalf** to existing delegates such as the `sales` delegates. Existing delegate rows default to neither permission; Read/Write or Full Access alone will not enable sending. Choose Send As when the message should appear from the shared address without a Sender header, or Send on Behalf when the server should identify the delegate in Sender.

## Manual SMTP check

Use a non-production test tenant and controlled recipient, replacing placeholders:

```sh
swaks --server <SMTP_HOST> --port 587 --tls \
  --auth LOGIN --auth-user 'cristobal@<domain>' --auth-password '<APP_PASSWORD>' \
  --from 'cristobal@<domain>' --to '<CONTROLLED_RECIPIENT>' \
  --header 'From: sales@<domain>' --header 'Subject: delegated send check' \
  --body 'SMTP delegated sender authorization check'
```

With Send As, expect acceptance and no Sender header. With Send on Behalf only, expect acceptance and `Sender: cristobal@<domain>`. Full Access only, no grant, another tenant's address, external address, `MAIL FROM:<>`, and a forged From should each get `550 5.7.1` at MAIL FROM or after DATA. Without STARTTLS, `AUTH LOGIN` on port 587 should receive `538 5.7.11`. Never use production credentials or enable logging that captures passwords or message bodies.

## Edited files

- `src/Miautrix.Mail.Application/Mail/ISenderAuthorizationService.cs` (new)
- `src/Miautrix.Mail.Application/Mail/SenderAuthorizationService.cs` (new)
- `src/Miautrix.Mail.Application/Mail/MessageService.cs`
- `src/Miautrix.Mail.Application/Admin/AdminContracts.cs`
- `src/Miautrix.Mail.Application/Admin/AdminService.cs`
- `src/Miautrix.Mail.Domain/Entities.cs`
- `src/Miautrix.Mail.Persistence/AppDbContext.cs`
- `src/Miautrix.Mail.Persistence/Migrations/20261124203756_AddMailboxDelegateSenderPermissions.cs` (new)
- `src/Miautrix.Mail.Persistence/Migrations/20261124203756_AddMailboxDelegateSenderPermissions.Designer.cs` (new)
- `src/Miautrix.Mail.Persistence/Migrations/AppDbContextModelSnapshot.cs`
- `src/Miautrix.Mail.Protocols.Smtp/SmtpModels.cs`
- `src/Miautrix.Mail.Protocols.Smtp/SmtpSubmissionHandler.cs`
- `src/Miautrix.Mail.Worker/SmtpListenerService.cs`
- `src/Miautrix.Mail.Worker/Program.cs`
- `src/Miautrix.Mail.Web/Program.cs`
- `admin/src/types.ts`
- `admin/src/components/MailboxDelegatePicker.tsx`
- `admin/src/components/MailboxDelegatePicker.test.tsx` (new)
- `admin/src/components/UsersScreen.test.tsx`
- `tests/Miautrix.Mail.ProtocolTests/Smtp/SmtpProtocolTests.cs`
- `tests/Miautrix.Mail.ProtocolTests/Smtp/SmtpSenderAuthorizationListenerTests.cs` (new)
- `tests/Miautrix.Mail.UnitTests/Imap/ImapUidStabilityTests.cs` (user + policy fixture only; UID assertions unchanged)
- `tests/Miautrix.Mail.IntegrationTests/Mail/SenderAuthorizationTests.cs` (new)
- `tests/Miautrix.Mail.IntegrationTests/Mail/SenderAuthorizationWiringTests.cs` (new; mailbox write authorization, rejected send persistence, and final EML size/hash/storage)
- `tests/Miautrix.Mail.IntegrationTests/Api/MailboxAdminApiTests.cs`
