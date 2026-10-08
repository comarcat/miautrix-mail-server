# Miautrix Mail Server — Queue Race Fix Blueprint

## 1. Project Overview

### Current state
Miautrix Mail Server is an existing .NET 10 modular monolith with PostgreSQL/EF Core and separate worker dispatchers for inbound and outbound email queue processing.

`InboundQueueDispatcher` and `OutboundQueueDispatcher` both poll `smtp_queue` without an ownership discriminator. Inbound mail can be processed as outbound, and outbound mail can be processed as inbound.

### Target state
`smtp_queue` rows carry an explicit non-null `direction` value: `Inbound` or `Outbound`. Dispatchers only process rows matching their direction. Enqueue call sites must state direction explicitly.

## 2. Non-Goals

- Do not change Cloudflare Worker routing.
- Do not change DNS, WAF, Cloudflare token validation, or external endpoint routing.
- Do not change antispam/antimalware behavior.
- Do not redesign queue storage into separate tables in this change.
- Do not change `https://mail.miautrix.tech/api/v1/inbound/cloudflare`.

## 3. Directory Structure

### Delta
Modify:
- `src/Miautrix.Mail.Domain/Entities.cs`
- `src/Miautrix.Mail.Persistence/AppDbContext.cs`
- `src/Miautrix.Mail.Queue/SmtpQueueManager.cs`
- `src/Miautrix.Mail.Protocols.Smtp/SmtpInboundHandler.cs`
- `src/Miautrix.Mail.Protocols.Smtp/SmtpSubmissionHandler.cs`
- `src/Miautrix.Mail.Application/Admin/AdminService.cs`
- `src/Miautrix.Mail.Worker/InboundQueueDispatcher.cs`
- `src/Miautrix.Mail.Worker/OutboundQueueDispatcher.cs`
- `src/Miautrix.Mail.Web/Controllers/MailQueueController.cs`
- Admin queue UI file, if it currently renders queue DTO fields.

Add:
- EF migration under `src/Miautrix.Mail.Persistence/Migrations/`.
- Ownership isolation tests under existing test projects.

## 4. Data Model

Add to `SmtpQueueItem`:

```csharp
public string Direction { get; set; } = "Inbound";
```

Map in `AppDbContext`:

```csharp
entity.Property(e => e.Direction).HasColumnName("direction").IsRequired();
entity.HasIndex(e => new { e.TenantId, e.Direction, e.Status, e.NextAttemptAt });
```

Allowed values:
- `Inbound`
- `Outbound`

No enum is required for this change unless the existing codebase already uses queue status constants nearby.

## 5. API Design

### Delta
`ISmtpQueueManager.EnqueueAsync` must require `direction` explicitly. No default parameter.

`MailQueueController` response must include `Direction`; list endpoint should support optional direction filtering.

### Interfaces held constant
- Existing Cloudflare inbound endpoint remains unchanged.
- Existing SMTP/submit transports remain unchanged except queue direction assignment.
- Existing queue status values remain unchanged.

## 6. UX Design

Admin queue UI should expose `Direction` as a visible column/filter if the API surface already feeds that screen.

Done when an operator can identify whether a stuck row is inbound or outbound without database access.

## 7. Auth & Authorization

No new permissions. Existing queue/admin permissions remain authoritative.

Cross-tenant behavior must remain 404 where applicable. Do not expose queue rows across tenants.

## 8. Integrations

Cloudflare routing and transport are out of scope and already validated. The change only prevents the wrong worker from owning a queue row.

## 9. Build Plan

### Step 1 — Emergency mitigation
Temporarily unregister `OutboundQueueDispatcher` in `src/Miautrix.Mail.Worker/Program.cs`.

Done when inbound worker is uncontested and outbound rows remain queued instead of corrupted.

Verify:
```bash
dotnet build Miautrix.Mail.sln -warnaserror
```

Checkpoint: outbound dispatcher disabled in code or deployment config.

### Step 2 — Add queue direction schema
Add `Direction` to `SmtpQueueItem`, map it in `AppDbContext`, add the composite index, and create EF migration.

Done when EF model and migration contain `direction` and the composite index.

Verify:
```bash
dotnet build Miautrix.Mail.sln -warnaserror
```

Checkpoint: migration compiles.

### Step 3 — Backfill existing rows
In migration `Up`, infer direction by recipient domain locality:

```sql
UPDATE smtp_queue q
SET direction = CASE
  WHEN EXISTS (
    SELECT 1 FROM domains d
    WHERE d.tenant_id = q.tenant_id
      AND lower(d.name) = lower(split_part(q.recipient, '@', 2))
  ) THEN 'Inbound'
  ELSE 'Outbound'
END;
```

Reset stuck inbound rows conservatively:

```sql
UPDATE smtp_queue
SET status = 'Pending', attempts = 0, last_error = NULL, next_attempt_at = now()
WHERE direction = 'Inbound'
  AND status IN ('Failed', 'DeadLetter')
  AND (last_error ILIKE '%Cloudflare%' OR last_error ILIKE '%send_failed%' OR last_error ILIKE '%502%');
```

Done when migration can classify existing queue rows and restore known mis-owned inbound rows.

Verify:
```bash
dotnet ef migrations list --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web
```

Checkpoint: migration exists and is forward-only.

### Step 4 — Require explicit direction at enqueue boundaries
Change `ISmtpQueueManager.EnqueueAsync` to require `direction`. Update all current call sites:
- `src/Miautrix.Mail.Protocols.Smtp/SmtpInboundHandler.cs` → `Inbound`
- `src/Miautrix.Mail.Protocols.Smtp/SmtpSubmissionHandler.cs` → `Outbound`
- `src/Miautrix.Mail.Application/Admin/AdminService.cs` queue retry/requeue/send paths → inspect semantics and set `Outbound` for send/retry of outbound queue records; preserve original direction when requeueing existing rows if applicable.

Done when `grep -rn "EnqueueAsync" src tests` shows every call passing an explicit direction.

Verify:
```bash
dotnet build Miautrix.Mail.sln -warnaserror
```

Checkpoint: no defaulted direction call remains.

### Step 5 — Enforce worker ownership
Filter dispatchers:
- `InboundQueueDispatcher`: only `Direction == "Inbound"`
- `OutboundQueueDispatcher`: only `Direction == "Outbound"`

Re-enable `OutboundQueueDispatcher` after the migration and code are deployed together.

Done when both workers are registered and both queries filter by direction.

Verify:
```bash
dotnet build Miautrix.Mail.sln -warnaserror
```

Checkpoint: no worker polls directionless rows.

### Step 6 — Admin/API visibility
Expose `Direction` in `MailQueueController` DTO and optional filters. Add admin UI column/filter if the existing screen consumes the field.

Done when an operator can see and filter queue direction.

Verify:
```bash
pnpm --filter admin build
dotnet build Miautrix.Mail.sln -warnaserror
```

Checkpoint: queue visibility restored.

### Step 7 — Tests
Add tests for:
1. Inbound rows are never dispatched outbound, including Cloudflare and local transport recipient domains.
2. Outbound rows are never dispatched inbound and never dead-lettered by inbound ownership checks.
3. Cloudflare inbound webhook creates a mailbox message and marks queue row `Delivered`.
4. Local compose to external recipient dispatches through Cloudflare transport and returns 202 path behavior.
5. Backfill classifier: local recipient → `Inbound`, external recipient → `Outbound`.

Done when tests fail before direction filtering and pass after implementation.

Verify:
```bash
dotnet test
dotnet build Miautrix.Mail.sln -warnaserror
```

Checkpoint: ownership regression coverage exists.

### Step 8 — Documentation and production evidence
Update `ERRORS_AND_ISSUES.md` §12.10 with chosen option, migration name, backfill counts, stuck row reset count, and verification evidence.

Done when production validation records:
- inbound webhook delivery works;
- outbound external delivery works;
- no inbound row has Cloudflare send errors.

Verify:
```bash
dotnet build Miautrix.Mail.sln -warnaserror
```

Checkpoint: operational record complete.

## 9.1 Parity and Cutover

### Parity checklist
- Existing inbound Cloudflare webhook route still accepts validated inbound messages.
- Existing inbound delivery still parses MIME, scans, stores body, creates message, attachments, and marks Delivered.
- Existing outbound submission still queues external messages.
- Existing Cloudflare outbound transport remains unchanged.
- Admin queue listing still works and gains direction visibility.

### Parity harness
Use:
```bash
dotnet build Miautrix.Mail.sln -warnaserror
dotnet test
pnpm --filter admin build
```

Live validation is performed by the user against `https://mail.miautrix.tech/api/v1/inbound/cloudflare`.

### Coexistence
Before schema/code cutover, disable outbound dispatcher so inbound rows cannot be corrupted. After migration and code deploy, re-enable outbound dispatcher. Both workers can coexist safely because ownership is direction-filtered.

### Cutover sequence
1. Disable outbound dispatcher.
2. Deploy mitigation.
3. Apply migration.
4. Deploy direction-aware code.
5. Re-enable outbound dispatcher.
6. Validate inbound and outbound flows.

### Kill criteria
Rollback if any of these occur after deployment:
- inbound rows with `direction = 'Inbound'` receive Cloudflare outbound send errors;
- outbound rows with `direction = 'Outbound'` become `DeadLetter` from inbound mailbox lookup;
- queue polling causes elevated DB CPU from missing index;
- build/test gate fails.

### Decommission
Option B, splitting `inbound_spool` and `outbound_queue`, remains future debt and is not part of this change.

## 10. Testing Strategy

Primary risk is queue ownership, not transport. Tests must directly exercise dispatchers and not rely only on end-to-end happy paths.

Required commands:
```bash
dotnet test
dotnet build Miautrix.Mail.sln -warnaserror
pnpm --filter admin build
```

## 11. Observability

Record failed authorization and queue failures as existing code does. Do not log message bodies, tokens, passwords, or secrets.

Add no new secret logging.

## 12. Release and Rollback

Rollback steps:
1. Disable outbound dispatcher again.
2. Revert code deployment if needed.
3. Leave `direction` column in place; it is additive and safe.
4. If migration must be reverted in non-production, drop index then column via EF Down.
5. In production, prefer forward fix over destructive rollback.

## 13. Security

Do not expose queue rows cross-tenant. Do not log raw MIME bodies or secrets. Direction is non-sensitive metadata.

## 14. Performance

Composite index `(tenant_id, direction, status, next_attempt_at)` is required because workers poll every few seconds.

## 15. Reliability

The fix removes nondeterministic worker races by making ownership explicit and queryable.

## 16. Accessibility

If the admin UI adds a filter/column, preserve existing keyboard and contrast behavior.

## 17. Localization

Not applicable — internal admin metadata only.

## 18. Deployment

Use existing EF migration deployment scripts. Follow production forward-only migration policy.

## 19. Open Questions

None blocking. During implementation, verify exact AdminService enqueue semantics before assigning `Outbound` blindly.

## 20. Decision Log

### 20.1 Chosen option
Use Option A: direction discriminator column now.

### 20.2 Rejected option
Option B: split into separate inbound/outbound tables. Better long-term model, but too broad for the active corruption bug.

### 20.3 Would reverse if
Reverse toward Option B if queue states continue diverging, admin needs separate lifecycles, or direction-based filtering becomes insufficient.
