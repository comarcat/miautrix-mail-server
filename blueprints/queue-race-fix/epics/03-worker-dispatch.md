# Epic 03 — Worker Dispatch Isolation

## Goal
Guarantee workers only process queue rows that belong to their lifecycle.

## Tasks
- QRF-005 — Add direction filters to inbound/outbound dispatchers and re-enable outbound worker after migration/code deployment.

## Done when
- `InboundQueueDispatcher` polls only `Direction == "Inbound"`.
- `OutboundQueueDispatcher` polls only `Direction == "Outbound"`.
- Both workers may run simultaneously without corrupting each other's rows.
