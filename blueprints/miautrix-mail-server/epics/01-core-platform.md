# Epic 1 — Core platform

**Tasks:** T1–T6 · **Depends on:** nothing · **Unblocks:** every other epic

This epic produces a deployable skeleton with a real database, real authentication, real
authorization, and a real audit trail. Nothing here is a stub. If this epic is done correctly,
every later feature inherits tenant isolation and auditing for free. If it is done carelessly,
those two properties are retrofitted later at several times the cost.

---

## T1 — Solution scaffold and CI

Create `Miautrix.Mail.sln` with the layered project layout:

```
/src
  Miautrix.Mail.Domain
  Miautrix.Mail.Application
  Miautrix.Mail.Infrastructure
  Miautrix.Mail.Persistence
  Miautrix.Mail.Web
  Miautrix.Mail.Admin
  Miautrix.Mail.Cli
  Miautrix.Mail.Worker
  Miautrix.Mail.Protocols.Smtp
  Miautrix.Mail.Protocols.Imap
  Miautrix.Mail.Protocols.Sieve
  Miautrix.Mail.Protocols.Jmap
  Miautrix.Mail.Identity
  Miautrix.Mail.Security
  Miautrix.Mail.AntiSpam
  Miautrix.Mail.AntiMalware
  Miautrix.Mail.MailFlow
  Miautrix.Mail.Storage
  Miautrix.Mail.Search
  Miautrix.Mail.Queue
  Miautrix.Mail.Diagnostics
  Miautrix.Mail.Reporting
  Miautrix.Mail.Licensing
  Miautrix.Mail.Plugins
/tests
  Miautrix.Mail.UnitTests
  Miautrix.Mail.IntegrationTests
  Miautrix.Mail.ProtocolTests
  Miautrix.Mail.SecurityTests
  Miautrix.Mail.EndToEndTests
```

Enforce the dependency direction with a test that fails if `Domain` references anything outside
the BCL. Add `Directory.Build.props` with `TreatWarningsAsErrors` and a pinned `LangVersion`.

**Done when** `dotnet build Miautrix.Mail.sln -warnaserror` exits 0 on a clean checkout and the
architecture test fails when a forbidden reference is added.

---

## T2 — Domain model and EF Core schema

Implement every entity in blueprint Section 5.2. Every tenant-scoped entity gets
`tenant_id NOT NULL`, a foreign key, and an index leading with `tenant_id`.

Add a global query filter that applies the tenant predicate — but do **not** rely on it alone.
The authorization helper in T5 is the real enforcement point; the filter is defence in depth.

**Done when** `dotnet ef database update` against an empty database creates every table, and a
test asserts that each tenant-scoped table has an index whose first column is `tenant_id`.

---

## T3 — Seed data and indexes

Seed the permission catalogue as data — `mailbox.create`, `mailbox.delete`, `queue.retry`,
`domain.add`, `user.invite`, `audit.read`, `licence.manage`, and the rest. Seed the system roles
and a first tenant with an owner membership.

Must be idempotent. Running it twice must not duplicate the catalogue.

**Done when** a second consecutive seed run leaves `SELECT count(*) FROM permissions` unchanged.

---

## T4 — Identity

Argon2id password hashing with per-hash parameters. TOTP enrolment with QR generation and a
confirmation step before activation. Session issuance, refresh-token rotation on use, and hashed
storage of every token.

Write a `security_events` row for each of AUTH-4001, 4010, 4011, 4020, 4030, 4031, 4040, 4050.

**Never** log a password, a TOTP secret, a session cookie, or a refresh token. The redaction list
lives in the logger, not at each call site.

**Done when** a correct password with a missing second factor is refused and writes exactly one
AUTH-4020 event.

---

## T5 — Authorization and tenant isolation

One helper — not two, not "mostly one" — through which every tenant-scoped read and write passes.
Roles bind to memberships, never to users. Permissions are checked by string, never by
`role === 'admin'`.

WHEN a resource belongs to another tenant the response is **404**, not 403. This is a security
requirement, not a style preference: a 403 confirms the resource exists.

Record failed authorization attempts, not just successes.

The last owner of a tenant cannot be removed or demoted.

**Done when** a cross-tenant read returns 404 and the test suite proves no route bypasses the helper.

---

## T6 — Audit trail

Append-only `audit_logs`. The event is written **in the same transaction as the change it
describes** — so a rollback removes the audit row too. This is what makes the trail trustworthy:
an audit log written outside the transaction can record events that never happened.

`action` is a stable `object.verb` string and is an API contract. Never rename one.

**Done when** an audited change that is rolled back leaves no audit row behind.
