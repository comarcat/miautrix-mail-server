# Epic 4 — Operations

**Tasks:** T18–T21 · **Depends on:** Epic 3

What separates a demo from something a business runs on. Backups that restore, updates that do not
take the service down, and a licence that never holds a customer's mail hostage.

---

## T18 — Desktop admin application

Electron, Windows and Linux, bundling the same React admin UI.

**The shell bundles its UI locally and never loads remote content into the webview.** It talks to
the customer's server only over the REST API. Loading a remote page into a webview that also holds
native command access is how desktop apps become remote code execution.

Secrets live in the OS keychain — never in a config file, never in `localStorage`.

**Signing and notarization are procurement, not code.** Certificates take time to obtain, and
SmartScreen reputation builds over weeks. Start that process early; it cannot be a build gate.

**Done when** the packaged app launches with no network and renders its bundled UI.

---

## T19 — Backup and restore

`miautrix-mail backup` snapshots database and storage **consistently together** — a database
snapshot without its matching storage, or vice versa, is a corrupt backup that looks fine.

**A backup that has never been restored is not a backup.** The acceptance criterion is a restore
into a scratch database with matching row counts, not a successful archive creation.

**Done when** a restored archive produces matching row counts for every table.

---

## T20 — Blue/green update and the migrations ladder

Signed packages, staged to the inactive slot, health-gated, then a symlink flip.

WHEN the health gate fails THE SYSTEM SHALL leave the previous release serving and SHALL NOT flip.
A bad deploy must be a non-event.

Schema changes ride **EXPAND → MIGRATE → CONTRACT** across separate deploys. Never in one. One
migration system owns the schema; EF Core Migrations is it, and nothing else issues DDL.

**Done when** a failing health gate leaves the prior release serving traffic.

---

## T21 — Security hardening and licence gating

The licensing rules are constraints, not features:

- WHEN the licence service is unreachable THE SYSTEM SHALL keep mail flowing.
- WHEN a tenant exceeds its allowance THE SYSTEM SHALL block **new mailbox creation only**. It
  SHALL NOT lock, hide, or destroy existing mailboxes or messages.

Holding a customer's existing mail hostage over a billing state is not a licensing strategy; it is
a hostage situation with an invoice attached, and it is the thing customers leave over.

MFA, backups, audit visibility of one's own account, and local admin access stay free in every
edition. SSO belongs in Enterprise — that is normal and defensible.

**Done when** an over-limit tenant still receives mail for existing mailboxes while new mailbox
creation is refused, and no data is destroyed.
