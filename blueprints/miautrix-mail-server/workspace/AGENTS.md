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
| `src/Miautrix.Mail.Web` | Web admin + API host. |
| `src/Miautrix.Mail.Cli` | `miautrix-mail` command line. |
| `apps/admin`, `apps/webmail`, `apps/desktop` | React frontends. |
| `tests/` | Unit, integration, protocol, security, E2E. |

## Provider seams

Swap an implementation without touching business logic:

`IIdentityProvider` · `ISpamProvider` · `IMalwareProvider` · `IMailStorage` · `IGeoIpProvider` ·
`IQueueProvider` · `IAuthenticationProvider` · `IReportingProvider` · `ISearchProvider`

## Task tracking

Tasks live in `blueprints/miautrix-mail-server/tasks.json`. Each has an id, a checkpoint tag, a
dependency list, an EARS acceptance criterion, and a verify command. Work them in dependency order.

Run `/architect-next` from the project root to pick up the next unblocked task.

## Before you claim a task is done

Run its verify command from the project root. It must exit 0. A passing build is not a passing
test, and "it looks right" is not a verify command.

## Things that will bite you

- The tenant query filter is defence in depth, **not** the enforcement point. The authorization
  helper is.
- Audit rows are written in the caller's transaction. Do not "fix" this by moving them out.
- Retry backoff needs jitter. Without it, the queue synchronises and stampedes.
- `ISearchProvider` exists because PostgreSQL FTS will not be enough forever. Do not leak FTS
  specifics through the seam.
- Message bodies are hostile input. Sanitise before render.
