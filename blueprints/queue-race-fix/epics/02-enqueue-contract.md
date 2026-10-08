# Epic 02 — Explicit Enqueue Contract

## Goal
Make queue direction explicit at every producer boundary.

## Tasks
- QRF-004 — Require direction in `ISmtpQueueManager.EnqueueAsync` and update all call sites.

## Required call-site review
Search with:

```bash
grep -rn "EnqueueAsync" src tests --include="*.cs"
```

Expected production call-site areas:
- `AdminService.cs`
- `SmtpInboundHandler.cs`
- `SmtpSubmissionHandler.cs`
- `SmtpQueueManager.cs`

## Done when
No queue row can be created without explicitly choosing `Inbound` or `Outbound`.
