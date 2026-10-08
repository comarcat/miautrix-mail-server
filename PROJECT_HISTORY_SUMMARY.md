# Miautrix Mail Server — Project History Summary

**Scope:** Summary from the first recorded project memory through the latest documented change.  
**Last updated:** 2026-10-07  
**Project:** Miautrix Mail Server — self-hosted, multi-tenant mail platform built with .NET 10, PostgreSQL, EF Core, React, Webmail/Admin UI, worker services, and Cloudflare transport integration.

---

## 1. Initial Project Direction

Miautrix Mail Server began as a production-grade, self-hosted email platform intended to give organizations full email sovereignty without depending on third-party SaaS mail providers.

The architecture target was a modular monolith using Clean Architecture:

- **Domain** contains core entities and rules with no external dependencies.
- **Application** owns use cases and service contracts.
- **Infrastructure/Persistence** implements EF Core, PostgreSQL, workers, protocol services, and integrations.
- **Presentation/Transport** includes REST controllers, SMTP/IMAP/Sieve listeners, Webmail, Admin UI, CLI, and desktop client surfaces.

The core platform rules established early were:

- Tenant isolation is mandatory.
- Cross-tenant resources return **404**, never 403.
- Business logic belongs in services, not controllers.
- Database schema changes go through EF Core migrations only.
- No SMTP authentication without encryption.
- Secrets and message bodies must not be logged.
- Invitation tokens and API keys are hashed and shown once.

---

## 2. Core Foundation and Database Work

The first major implementation checkpoint was the core data model and EF Core schema.

Key work completed:

- Full tenant-scoped schema was created.
- Tenant tables received `tenant_id` columns.
- Tenant indexes followed the `idx_<table>_tenant_id` convention.
- Foreign keys from tenant-scoped tables to `tenants.id` were configured.
- `AppDbContext.ConfigureTenantScoped` standardized tenant index naming.
- Initial migration `20260918021252_InitialCreate` was authored and verified through EF migration script output.

A local verification blocker existed at the beginning: `dotnet ef database update` could not run without a local PostgreSQL instance. The schema itself was verified through generated SQL and build output.

---

## 3. Core Platform Milestones

The project advanced through the main PMI work packages:

### T1–T6 — Core Foundation

Completed:

- Solution scaffold
- Domain model
- EF Core schema
- Seeder
- Argon2id credentials
- TOTP/WebAuthn MFA foundations
- Session management
- Tenant isolation
- Authorization
- Audit trail

### T7–T13 — Mail Engine

Completed:

- SMTP submission and inbound pipeline
- SPF, DKIM, and DMARC validation/signing
- Anti-spam baseline
- Anti-malware integration
- IMAP storage access
- ManageSieve support
- PostgreSQL full-text search
- Mail-flow rules engine

### T14–T17 — APIs and User Surfaces

Completed:

- Authentication REST API
- Mailbox/message REST API
- Admin management REST API
- Webmail frontend
- Admin console frontend
- CLI/operator tooling foundations

### T18–T21 — Operations and Lifecycle

Completed:

- Worker deployment
- Backup/restore foundations
- Blue/Green deployment support
- License enforcement behavior
- Operational scripts
- Production deployment support

---

## 4. Production Database and Login Stabilization

A major production issue appeared during live login verification.

### Problem

`POST /api/v1/auth/login` returned HTTP 500 with PostgreSQL error:

```text
42703: column m.name does not exist
```

The backend expected `mailboxes.name`, but the production schema was behind the EF Core model.

### Root Cause

A migration existed in source but was not discovered by EF because the generated migration designer metadata was incomplete/missing.

### Fix

- Added migration `20260920223000_AddMailboxName` with designer metadata.
- Added safer production database update scripts.
- Scripts require explicit production connection details.
- Scripts reject localhost/dev/test targets.
- Scripts require confirmation of the database name.
- Migration was applied to production.
- Login at `https://mail.miautrix.tech/admin` was confirmed working.

---

## 5. Admin Console Evolution

The Admin Console became a functional management surface styled to the PulsePoint/Miautrix visual direction.

Implemented areas included:

- Users
- Domains
- Identity/security settings
- Anti-spam
- Anti-malware
- Quarantine
- Logs
- Reports
- Backup
- System
- Licensing
- Mail Flow
- Queue management

Important domain/admin work:

- Domains CRUD completed.
- DKIM/SPF/DMARC display and verification added.
- DNS TXT verification wired through backend services.
- Top-bar domain picker replaced the hardcoded tenant selector.
- Domain filtering was applied to domain-scoped screens such as Users, Queue, and Quarantine.
- Admin queue retry was fixed to send JSON with a retry reason, resolving HTTP 415 errors.

---

## 6. Shared Mailbox Delegation

Shared mailbox support was implemented as a mailbox-resource model, not as login identities.

Rules established:

- Shared mailboxes do **not** create `User`, `UserCredential`, or `Membership` rows.
- Shared mailboxes require no password.
- Delegates must be active same-tenant, same-domain users.
- Delegate access levels are `read` and `write`.
- Read-only delegates can view mailbox content but cannot mutate it.
- Write delegates can mark read, move, delete, and send.
- Backend enforcement happens through the central tenant authorization helper.
- UI enforcement disables write actions for read-only delegates.

This preserved the central project invariant: no tenant-scoped read/write bypasses authorization.

---

## 7. Webmail Drafts, Composer, Folders, and Contacts

The Webmail surface grew into a functional Outlook-like client.

### Drafts and Composer

Completed on 2026-09-27:

- Draft attachments persist as database attachment rows.
- Draft attachments survive reopen/send lifecycle.
- Attachments can be removed.
- Draft discard cleans up persisted draft attachments.
- Rich editor polish was added.
- Italic formatting visibility was fixed.
- Inline image resize/persistence was stabilized.
- Stray editor artifact text was removed.
- Mail signatures were persisted and selectable.

### Personal Folders

Completed:

- Create personal folders.
- Move nested folders.
- Render recursive personal folder trees.
- Delete custom folder subtrees.
- Require explicit confirmation when folders contain mails/messages.
- Folder mutations continue through central mailbox authorization.

Verification:

- `dotnet build Miautrix.Mail.sln -warnaserror` passed.
- `pnpm --filter webmail build` passed.

### Contacts and Company Directory

Completed on 2026-09-28:

- Company Directory lists tenant mailbox/group addresses.
- Owner/admin users can edit directory display fields.
- Personal Contacts work.
- Sent recipients are autosaved to Personal Contacts.
- Composer includes contact picker for To/Cc/Bcc.
- New/reply/forward flows reuse the picker without changing send/draft payload shapes.

---

## 8. Anti-Malware Discard Policy

A domain-scoped anti-malware policy was implemented to control what happens when malware is detected.

Before the change, malware detections could leave messages locked in queue/quarantine behavior.

The new behavior:

- Domain setting controls malware-detection action.
- `quarantine` remains the default behavior.
- `discard` removes malware-detected messages from the SMTP queue.
- Discarded malware does not create a quarantine row.
- Admin UI exposes **Action on Malware Detection**.

This was verified with build/test coverage and documented as a production capability.

---

## 9. Cloudflare Workers Transport

Cloudflare Workers were integrated as an optional mail transport, not as a replacement for the mail system.

Boundary:

- Miautrix keeps MIME parsing, queueing, retry/dead-lettering, mailbox routing, spam/malware policy, DKIM signing, storage, and audit behavior.
- Cloudflare handles transport relay, DNS posture, Worker invocation, and TLS edge integration.

Key features:

- Per-domain Cloudflare transport mode.
- External recipient domains do not need tenant domain rows.
- If a recipient-specific Cloudflare domain config does not exist, outbound delivery falls back to the tenant primary Cloudflare domain/Worker.
- Worker response logging was improved to show detailed rejection reasons.
- Deploy scripts were updated to publish/copy/restart the worker service.

This fixed the earlier state where queue rows were accepted but not dispatched externally.

---

## 10. Calendar Permission and Multi-Tenant Seeder Fix

A major calendar issue appeared where all calendar endpoints returned 404.

### Problem

`POST /api/v1/calendar/events` and other calendar endpoints returned `not_found` / `Resource not found`.

### Root Cause

The failure was permission-related, not routing-related.

`CalendarService` required `mailbox.read`, but the Seeder had only populated role permissions for the `default` tenant. Other tenants had roles without `RolePermissions`, so no user in those tenants could satisfy calendar permissions.

### Fix

- Seeder was changed to reconcile permissions and roles for every tenant.
- It matches existing rows by permission/role code instead of deterministic IDs.
- A `--permissions-only` mode was added for safe production repair.
- This avoided creating bootstrap default tenants/admin accounts in production.
- The repair was verified as idempotent across existing tenants.

---

## 11. Calendar and Webmail v1.0 Stabilization

The final v1.0 blocker phase focused on painful Calendar/Webmail defects.

Completed on 2026-10-03:

- Public RSVP Accept/Tentative/Decline controls were made reliable.
- RSVP page uses link-style actions wired to protected POST submission.
- Query-string auto-submit still works for RSVP responses.
- Reschedule proposal emails generate organizer Accept/Decline links.
- Invitation/proposal email times explicitly identify UTC.
- Emails explain that calendar clients and RSVP page render local PC timezone.
- Webmail select-all inbox delete no longer white-screens.
- Bulk delete clears stale selection/detail state and safely renders an empty folder.

Verification:

- `dotnet build Miautrix.Mail.sln -warnaserror` passed with 0 warnings/errors.
- CalendarInvitationBuilder tests passed 6/6.
- PMI and final solution documentation were updated.

This checkpoint marked Miautrix Mail Server **Version 1.0 Complete / Approved**.

---

## 12. Outbound Queue “Kidnapping” Fix

The latest major technical fix addressed outbound messages being misrouted as inbound.

### Problem

Webmail-sent mail to external domains such as Gmail became stuck or dead-lettered with recipient mailbox lookup errors.

### Root Cause

`SmtpQueueItem.Direction` defaulted to `Inbound`. Outbound enqueue paths created queue rows without explicitly setting `Direction`.

Result:

- `OutboundQueueDispatcher` ignored the row because it only processes outbound rows.
- `InboundQueueDispatcher` picked it up.
- Inbound dispatcher attempted local mailbox delivery.
- External recipients failed with “Recipient mailbox not found.”

This was originally suspected to be a MIME/CRLF issue, but the MIME builder output was already correct.

### Fix

- `MessageService.SendMessageInternalAsync` now sets `Direction = "Outbound"`.
- `SmtpQueueService.EnqueueMessageAsync` now sets `Direction = "Outbound"`.
- Existing stuck production rows were repaired by flipping direction/status/attempt metadata.
- Cloudflare Worker response handling/logging was improved.
- Queue ownership migration and dispatcher filtering were documented.

Verification:

- `dotnet build Miautrix.Mail.sln -warnaserror` passed.
- Live validation confirmed outbound Cloudflare routing after direction repair.
- Targeted integration tests were attempted but blocked by PostgreSQL authentication for the test DB user, not by compilation failure.

---

## 13. Graphify and Archify Documentation Work

Graphify and Archify became part of the project documentation workflow.

Artifacts:

- `architecture.html` — architecture visualization.
- `architecture.json` — architecture topology data.
- `graphify-out/graph.html` — navigable codebase graph.
- `graphify-out/graph.json` — GraphRAG-ready graph JSON.
- `graphify-out/GRAPH_REPORT.md` — graph report and audit trail.

Latest graph report summary:

- 4,293 nodes
- 10,668 edges
- 261 communities
- 93% extracted edges
- 7% inferred edges
- 0% ambiguous edges
- Built from commit `93dc8b06`

Graphify highlighted key hubs such as:

- AdminService
- Message
- ContactService
- CalendarService
- MessageController
- SmtpQueueItem
- CloudflareApiMailTransport
- Webmail/App components

---

## 14. Presentation and Reporting Work

The final documentation/presentation phase began after v1.0 stabilization.

Created:

- `generate_presentation.py` — original Python asset packager.
- `generate_presentation.ps1` — PowerShell equivalent asset packager.
- `presentation_assets/SUMMARY.md` — concise presentation summary.
- `generate_presentation_pptx.ps1` — PowerShell script intended to generate a 12-slide PowerPoint deck through Microsoft PowerPoint COM automation.
- `PROJECT_HISTORY_SUMMARY.md` — this file.

Presentation target:

- Mixed executive/technical audience.
- 12 slides.
- Miautrix blue/teal corporate style.
- Includes architecture, Graphify, milestones, major changes, issue timeline, security posture, v1.0 evidence, and conclusion.

PowerPoint generation is still being debugged because local PowerPoint COM automation reported startup issues despite PowerPoint being installed.

---

## 15. Current Project Status

As of the latest documented change:

- Version 1.0 is complete/approved.
- Core architecture, mail engine, Webmail, Admin, worker, Cloudflare transport, and operational scripts are implemented.
- Calendar/Webmail stabilization blockers are closed.
- Outbound queue directionality bug is fixed and documented.
- Build verification passed for the final queue fix.
- Integration tests targeting outbound dispatch were blocked by database authentication configuration, not code compilation.
- Presentation assets and summary scripts exist.
- PowerPoint generation script exists but requires local COM automation troubleshooting.

---

## 16. Key Remaining/Deferred Items

Known follow-up areas:

- Expose SMTP queue, delivery attempts, and Cloudflare transport logs more fully in Admin UI.
- Generate sender replies for undeliverable recipient addresses.
- Add broader Testcontainers-backed coverage for shared mailbox delegation and cross-tenant concealment.
- Finish PowerPoint generation workflow once local PowerPoint COM registration/automation is resolved.
- Continue refreshing Graphify/Archify artifacts after major code changes.
- Consider future integrations: IdP federation, S3 storage, Rspamd, advanced analytics, federation, mobile clients.

---

## 17. High-Level Outcome

Miautrix Mail Server progressed from core schema and tenant-isolation foundations into a complete v1.0 mail platform with:

- Secure authentication and MFA
- Tenant-aware authorization
- Full mail transport pipeline
- Queue dispatching and retry lifecycle
- Anti-spam and anti-malware controls
- Webmail UX
- Admin UX
- Shared mailbox delegation
- Contacts and directory workflow
- Calendar RSVP/proposal flow
- Cloudflare Workers transport
- Production deployment scripts
- Architecture and graph documentation
- Issue tracker and stabilization evidence

The strongest recurring theme across the project was moving from feature implementation to production correctness: schema drift fixes, permission reconciliation, queue ownership, tenant concealment, deployment repeatability, and live verification.
