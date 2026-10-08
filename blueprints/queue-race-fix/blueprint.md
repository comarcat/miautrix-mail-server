# Miautrix Mail Server — Queue Race Fix Blueprint

See `../queue-race-fix-blueprint.md` for the full narrative source. This bundle version is the canonical `/architect-next` entrypoint.

## Change Summary

Fix `smtp_queue` worker ownership corruption by adding an explicit `Direction` discriminator (`Inbound` / `Outbound`) and requiring every enqueue path plus dispatcher query to respect it.

## Required Build Order

1. Temporarily disable `OutboundQueueDispatcher` to stop queue corruption.
2. Add `SmtpQueueItem.Direction`, EF mapping, and index `(TenantId, Direction, Status, NextAttemptAt)`.
3. Add EF migration with direction backfill based on recipient domain locality and conservative reset of stuck inbound rows.
4. Make `ISmtpQueueManager.EnqueueAsync` require explicit direction and update all call sites.
5. Filter both dispatchers by direction and re-enable outbound worker.
6. Expose/filter direction through `MailQueueController` and admin queue UI.
7. Add ownership regression tests and inbound/outbound functional tests.
8. Update `ERRORS_AND_ISSUES.md` §12.10 with production evidence.

## Acceptance Criteria

- Both dispatchers are registered and filter on `Direction`.
- Inbound webhook delivery creates a mailbox message and marks queue row `Delivered`.
- External outbound compose dispatches via Cloudflare and is never dead-lettered by inbound processing.
- No `Inbound` row has Cloudflare outbound send errors.
- `MailQueueController` returns and filters `Direction`.
- `dotnet build Miautrix.Mail.sln -warnaserror` passes.
- Relevant unit/integration tests pass.

## Scope Boundaries

Do not change Cloudflare routing, DNS, WAF, token validation, antispam/antimalware behavior, or `https://mail.miautrix.tech/api/v1/inbound/cloudflare`.
