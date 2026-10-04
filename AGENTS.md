# AGENTS.md — Miautrix Mail Server

Agent-facing orientation. Humans want `CLAUDE.md`.

## Where things are

| Path | Contents |
|---|---|
| `src/Miautrix.Mail.Domain` | Entities, value objects, domain rules. Zero external dependencies. |
| `src/Miautrix.Mail.Application` | Use cases, service layer, interface declarations. |
| `src/Miautrix.Mail.Infrastructure` | Adapters implementing Application interfaces. |
| `src/Miautrix.Mail.Persistence` | `AppDbContext`, migrations, repositories. |
| `src/Miautrix.Mail.Protocols.*` | SMTP, IMAP, Sieve, JMAP listeners. |
| `src/Miautrix.Mail.Web` | Web admin + API host. Controllers under `Controllers/`. |
| `src/Miautrix.Mail.Cli` | `miautrix-mail` command line. |
| `src/Miautrix.Mail.AntiSpam` | `RuleBasedSpamProvider`, `QuarantineService`. |
| `src/Miautrix.Mail.AntiMalware` | Malware scanning provider. |
| `src/Miautrix.Mail.Worker` | Background worker, `InboundQueueDispatcher`. |
| `admin/` | React + TypeScript admin frontend. `pnpm --filter admin dev`. |
| `admin/src/components/` | One file per screen (see Admin screens below). |
| `admin/src/api/client.ts` | `AdminApiClient` — all API methods. |
| `admin/src/types.ts` | Shared TypeScript types. |
| `tests/` | Unit, integration, protocol, security, E2E. |
| `scripts/` | DB init, migrations, deployment, test injection (see Scripts below). |

## Admin screens

| Screen | File | API area |
|---|---|---|
| Dashboard | `DashboardScreen.tsx` | `/api/v1/dashboard` |
| Users & Mailboxes | `UsersScreen.tsx` | `/api/v1/users`, `/api/v1/mailboxes` |
| Shared Mailboxes | `SharedMailboxesScreen.tsx` | `/api/v1/shared-mailboxes` |
| Domains | `DomainsScreen.tsx` | `/api/v1/domains` |
| Mail Flow Rules | `MailFlowScreen.tsx` / `RuleDesignerScreen.tsx` | `/api/v1/rules` |
| Queue | `QueueScreen.tsx` | `/api/v1/queue` |
| Anti-Spam | `AntiSpamScreen.tsx` | `/api/v1/system/domains/{id}/anti-spam` |
| Anti-Malware | `AntiMalwareScreen.tsx` | — |
| Quarantine | `QuarantineScreen.tsx` | `/api/v1/quarantine` |
| Security | `SecurityScreen.tsx` | `/api/v1/system/domains/{id}/security` |
| Logs | `LogsScreen.tsx` | `/api/v1/logs` |
| Reports | `ReportsScreen.tsx` | `/api/v1/reports` |
| System | `SystemScreen.tsx` | `/api/v1/system` |
| Backup | `BackupScreen.tsx` | — |
| Licensing | `LicensingScreen.tsx` | `/api/v1/licensing` |
| Identity (DKIM/DMARC) | `IdentityScreen.tsx` | `/api/v1/identity` |

## Provider seams

Swap an implementation without touching business logic:

`IIdentityProvider` · `ISpamProvider` · `IMalwareProvider` · `IMailStorage` · `IGeoIpProvider` ·
`IQueueProvider` · `IAuthenticationProvider` · `IReportingProvider` · `ISearchProvider`

## Per-domain settings pattern

Security settings and anti-spam settings both follow the same pattern. Use `SecurityController` +
`AdminService.GetSecuritySettingsAsync/UpdateSecuritySettingsAsync` as the template when adding
new per-domain configurable settings:

1. Add columns to `Domain` entity in `Entities.cs` with C# defaults.
2. Map them in `AppDbContext` with `snake_case` column names and `HasDefaultValue`.
3. Add EF migration: `dotnet ef migrations add <Name> --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web`
4. Add DTO + request record to `AdminContracts.cs`.
5. Add `Get`/`Update` methods to `IAdminService` and implement in `AdminService`.
6. Add GET + PATCH endpoints to the appropriate controller (usually `SecurityController` for
   domain-level settings).
7. Add `get*`/`update*` methods to `AdminApiClient` in `admin/src/api/client.ts`.
8. Add the TypeScript interface to `admin/src/types.ts`.

## Spam pipeline

```
SMTP in → InboundQueueDispatcher → RuleBasedSpamProvider.EvaluateAsync()
                                         ↓ score ≥ quarantine threshold
                                   QuarantineService → quarantine table
```

Scoring (additive): high-risk keywords +7.5, excessive punctuation +1.5, all-caps subject +2.0,
known DNSBL +5.5, SPF fail +2.5, DKIM fail +2.5, DMARC fail +3.0.

Per-domain thresholds (`reject_score`, `quarantine_score`, `header_score`, `greylist_score`) are
persisted in the `domains` table and served by `GET /api/v1/system/domains/{id}/anti-spam`. The
runtime scorer still uses a passed-in default (5.0) until explicitly wired to read domain settings
from the DB at dispatch time.

## Quarantine item lifecycle

Statuses stored **PascalCase** in the DB: `Quarantined`, `Released`, `Discarded`.

Row actions available on items with status `Quarantined`:
- **Release** — marks released, leaves row in table.
- **Deliver** — marks released + deletes row (false positive shortcut).
- **Remove** — deletes row permanently.
- **Block Domain** — creates a `MailFlowRule` to reject all future mail from the sender domain.

API endpoints:
```
GET    /api/v1/quarantine
GET    /api/v1/quarantine/{id}
POST   /api/v1/quarantine/{id}/release
POST   /api/v1/quarantine/{id}/deliver-and-delete
POST   /api/v1/quarantine/{id}/block-sender-domain
DELETE /api/v1/quarantine/{id}
```

## Task tracking

Tasks live in `blueprints/miautrix-mail-server/tasks.json`. Each has an id, a checkpoint tag, a
dependency list, an EARS acceptance criterion, and a verify command. Work them in dependency order.

Run `/architect-next` from the project root to pick up the next unblocked task.

## Before you claim a task is done

Run its verify command from the project root. It must exit 0. A passing build is not a passing
test, and "it looks right" is not a verify command.

Daily verification sequence:
```powershell
dotnet build Miautrix.Mail.sln -warnaserror
dotnet test Miautrix.Mail.sln          # integration tests require Docker
pnpm --filter admin test -- --run
pnpm --filter admin build
```

## Scripts

| Script | Purpose |
|---|---|
| `scripts/update-test-database.ps1` | Apply migrations + seed on test DB (`miautrix-mail-pro`). |
| `scripts/update-prod-database.ps1` | Apply migrations on production DB. |
| `scripts/inject-antispam-spam-test.ps1` | Send a high-score spam message via SMTP to trigger quarantine. |
| `scripts/inject-flow-mail-test.ps1` | Send a normal message to test mail-flow rules. |
| `scripts/deploy-compile-upload.ps1` | Build + push `publish/` to the server. |
| `scripts/deploy-websites.ps1` | Deploy admin/webmail frontends. |
| `scripts/init-database-scratch.ps1` | Full DB init from scratch (dev only). |

Set `$env:SMTP_SKIP_TLS_VERIFY="true"` before running injection scripts against hosts with
self-signed certificates.

## Things that will bite you

- **DB status values are PascalCase** (`"Quarantined"`, `"Released"`). Always `.toLowerCase()`
  before comparing in TypeScript.
- The tenant query filter is defence in depth, **not** the enforcement point. The authorization
  helper is.
- Audit rows are written in the caller's transaction. Do not "fix" this by moving them out.
- Retry backoff needs jitter. Without it, the queue synchronises and stampedes.
- `ISearchProvider` exists because PostgreSQL FTS will not be enough forever. Do not leak FTS
  specifics through the seam.
- Message bodies are hostile input. Sanitise before render.
- EF-generated migration files must use **file-scoped namespaces** (`namespace Foo;` not
  `namespace Foo { }`). If `dotnet ef` generates block-scoped, convert before building.
- `vi.useFakeTimers()` blocks `waitFor` polling in Vitest. Limit to
  `{ toFake: ['setTimeout', 'clearTimeout'] }` or restructure tests to avoid fake timers with
  async `waitFor`.
