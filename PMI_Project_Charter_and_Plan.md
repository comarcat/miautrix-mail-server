# Project Management Institute (PMI) / PMBOK Project Charter & Plan
# Miautrix Mail Server

**Document Version:** 1.0  
**Date:** 2026-09-16  
**Project Sponsor:** Miautrix  
**Project Lead / Architect:** Cristobal Arboleda  
**Status:** Approved / Active Implementation & Production Integration

**Last Updated:** 2026-09-23  

---

## 1. Project Initiation & Executive Summary

### 1.1 Project Purpose and Business Case
Miautrix Mail Server is a secure, high-performance, multi-tenant mail platform designed to give full email sovereignty back to organizations. Operating on Debian/Linux with .NET 10, PostgreSQL, and modern web/desktop administrative surfaces, the system handles SMTP/IMAPS/ManageSieve, anti-spam, mail-flow rules, and administration across isolated tenants without third-party vendor lock-in.

### 1.2 High-Level Objectives
- Deliver a production-grade mail platform capable of running end-to-end multi-tenant email operations.
- Provide a unified management suite: Web Admin GUI, Cross-Platform Desktop Admin (Windows/Linux via Electron), and CLI.
- Provide self-hosted identity, authentication (Argon2id, TOTP, WebAuthn), and granular data-driven RBAC.
- Ensure strict multi-tenant isolation where cross-tenant attempts return HTTP 404 (non-disclosure of resource existence).
- Implement modular Clean Architecture allowing swap-in of future enterprise integrations (Rspamd, S3 storage, IdP federation).

---

## 2. Project Scope Management (WBS Overview)

The project work is partitioned into 4 primary Epics covering 21 detailed work packages (tasks):

```
Miautrix Mail Server
├── Epic 1: Core Platform (T1 - T6)
│   ├── T1: Solution scaffold and CI
│   ├── T2: Domain model and EF Core schema
│   ├── T3: Seed data and indexes
│   ├── T4: Identity: credentials, MFA, sessions
│   ├── T5: Authorization, roles, and tenant isolation
│   └── T6: Audit trail
├── Epic 2: Mail Transport & Policy (T7 - T13)
│   ├── T7: SMTP listener and queue
│   ├── T8: SPF, DKIM, DMARC validation & signing
│   ├── T9: Anti-spam baseline and quarantine
│   ├── T10: IMAP, storage abstraction, attachments
│   ├── T11: ManageSieve
│   ├── T12: Full-text search indexing (PostgreSQL FTS)
│   └── T13: Mail-flow rule engine, simulator, explorer
├── Epic 3: User & Management Surfaces (T14 - T17)
│   ├── T14: REST API contract & OpenAPI spec
│   ├── T15: Web Admin GUI (React / TanStack / Tailwind)
│   ├── T16: Webmail (JMAP-first / React / DOMPurify)
│   └── T17: Operator CLI (Spectre.Console)
└── Epic 4: Operations & Lifecycle (T18 - T21)
    ├── T18: Desktop Admin App (Electron packaging)
    ├── T19: Automated Backup & Restore engine
    ├── T20: Blue/Green deployment & migration ladder
    └── T21: Security hardening & license enforcement
```

### Scope Boundaries
- **In Scope (v1):** Full core mail pipeline, multi-tenancy, local authentication/MFA, Web Admin, Webmail, Desktop App, CLI, Backup/Restore, Blue/Green update mechanism.
- **Out of Scope (Deferred):** Google Workspace / Entra ID / LDAP sync, Cloudflare mail-routing integrations, proprietary Bayes training models, POP3 protocol support, online license activation portal.

---

## 3. Project Schedule & Milestone Management

| Milestone | Key Deliverables / Checkpoint | Status | Pre-requisites | Primary Verification Gate |
|---|---|---|---|---|
| **M1: Core Foundation** | Scaffold, PostgreSQL schema, Seed data, Auth/MFA, RBAC, Audit | ✅ Complete | T1–T6 | `dotnet test --filter Category=Isolation\|Audit\|Identity` |
| **M2: Mail Engine** | SMTP In/Out, SPF/DKIM/DMARC, Anti-Spam, IMAP, Sieve, FTS, Rules Engine | ✅ Complete | T7–T13 | `dotnet test --filter Category=Smtp\|Dkim\|Imap\|Rules` |
| **M3: Surfaces & Client Apps** | OpenAPI REST endpoints, Web Admin, Webmail, CLI | 🔄 In Progress (shared-mailbox UI/live validation) | T14–T17 | API Integration tests & `pnpm test` |
| **M4: Operational Readiness** | Desktop Client, Backup/Restore drills, Blue/Green symlink updater, License gates | 🔄 In Progress (production migration/live verification) | T18–T21 | Migration verification, service restart, live login and licensing tests |

### Active Execution Phase: Production Integration & Functional Delivery

| Task ID | Task Description | Scope & Acceptance | Blocked By | Status |
|---|---|---|---|---|
| **#12** | Database Migration & Seeding | EF migrations/seeding target PostgreSQL `10.11.1.52` (`miautrix-mail-pro`); production update scripts force explicit connection and confirmation. | None | ✅ Applied — `20260920223000_AddMailboxName` migrated to production 2026-09-20 |
| **#13** | Backend Authentication REST API | `/api/v1/auth/login`, `/me`, `/refresh`, `/logout`, `/change-password`; live login 500 traced to schema drift (`column m.name does not exist`), not credential validation. | #12 | ✅ Implemented / ✅ Live login verified post-migration (2026-09-20) |
| **#14** | Backend Mailbox & Message REST API | `/api/v1/mailboxes`, folders, `/messages`, `/send`; mailbox-scoped reads/writes now authorize through shared central mailbox access helper before message operations. | #12 | ✅ Implemented |
| **#15** | Backend Admin Management REST API | `/api/v1/domains`, `/users`, `/shared-mailboxes`, `/rules`, `/audit`; shared mailboxes are passwordless mailbox resources with delegate assignment endpoints. | #12 | ✅ Implemented |
| **#16** | Functional Webmail Frontend | Full-width responsive layout, asset/icon paths, Auth/Mailbox REST wiring. | #13, #14 | ✅ Implemented / 🔄 Live retest after migration |
| **#17** | Functional Admin Console Frontend | Functional Admin UI including Users/Domains/Quarantine; quarantine supports domain/date filtering, 2-button row actions, and "suspected spam released" tag on delivery. | #13, #15 | ✅ Implemented |
| **#18** | Automated Deployment & Live Verification | Build, migrate, publish Linux x64 Web, Worker, and AntiSpam binaries & SPAs, deploy to Debian LXC `10.11.1.51` behind `mail.miautrix.tech`, and verify end-to-end. | #16, #17 | ✅ Web + worker deploy verified 2026-09-21 |
| **CF-01** | Cloudflare Workers transport per domain | Cloudflare can be selected per tenant-owned domain; external recipients delivered via tenant worker. Queue Retry and Quarantine Release send required diagnostic/tags. | #18 | ✅ Live verified 2026-09-21, ✅ Admin release workflow verified 2026-09-23 |

---

## 4. Quality Management & Verification Gates

Each work package carries strict Acceptance Criteria under the EARS standard (*WHEN `<trigger>` THE SYSTEM SHALL `<observable response>`*):

1. **Zero Compiler Warnings:** `dotnet build Miautrix.Mail.sln -warnaserror` enforced across all builds.
2. **Tenant Isolation Invariant:** Cross-tenant resource queries return HTTP 404, never 403, logged to security events.
3. **Architecture Enforceability:** Domain layer has 0 external dependencies. Controllers contain zero business logic.
4. **Data Layer Integrity:** Schema changes are forward-only (Expand → Migrate → Contract) managed strictly through EF Core migrations.
5. **Security & Privacy:** Passwords and secrets are never logged; invitation tokens and API keys are stored as cryptographically secure hashes.
6. **Shared Mailbox Identity Boundary:** Shared mailboxes are mailbox resources, not login identities. Creation does not require or process a password and does not create `User`, `UserCredential`, or `Membership` rows.
7. **Delegate Authorization:** Shared mailbox delegates must be active same-tenant, exact-domain users. `read` delegates may read mailbox/folder/message/attachment content; `write` delegates may perform approved mutations such as mark-read, move, delete, and send.
8. **Quarantine Lifecycle:** Discard updates quarantine status to `Discarded` without deleting database rows or `.eml` artifacts; Release queues the message for delivery with `[SPAM Supected-Released]` subject tagging and worker anti-spam bypass.

---

## 5. Risk Management Matrix

| Risk ID | Risk Description | Likelihood | Impact | Mitigation Strategy |
|---|---|---|---|---|
| **RSK-01** | Missing runtime dependencies (`pnpm`, PostgreSQL) during deployment testing | High | Medium | Containerize development/test environments via Docker / Testcontainers. |
| **RSK-02** | Mail deliverability issues due to DNS / DKIM / SPF misconfigurations | Medium | High | Implement built-in DNS health validator and automated DKIM record generator. |
| **RSK-03** | Frontend code drift between Web Admin and Desktop App | Medium | Medium | Shared component library and single React application codebase bundled into Electron. |
| **RSK-04** | Data loss during mailbox migrations or updates | Low | Critical | Strict forward-only migration pattern; verified automated backup/restore verification on staging before promotion. |
| **RSK-05** | License service downtime locking out customers | Low | High | License enforcement design specifies fail-open behavior: mail flows continuously regardless of licensing server availability. |

---

## 6. Stakeholder & Resource Plan

- **Project Sponsor:** Miautrix Executive Leadership (Scope signoff, licensing terms)
- **Technical Architect & Lead Developer:** Full-stack .NET / Systems Architecture
- **DevOps / SysAdmin:** Debian packaging, systemd daemon setups, TLS/Certbot integrations
- **Auditors & Compliance:** Security audit logs, non-disclosure verification
