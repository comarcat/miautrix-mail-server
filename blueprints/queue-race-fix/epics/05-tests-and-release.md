# Epic 05 — Tests and Release Evidence

## Goal
Lock the ownership fix with regression tests and production evidence.

## Tasks
- QRF-007 — Add ownership and functional tests.
- QRF-008 — Update `ERRORS_AND_ISSUES.md` §12.10 with implementation and validation evidence.

## Required tests
1. Inbound rows are never dispatched outbound.
2. Outbound rows are never dispatched inbound.
3. Cloudflare inbound webhook creates a message and marks queue row `Delivered`.
4. Local external compose dispatches via Cloudflare path and is not dead-lettered by inbound.
5. Backfill classifier maps local recipient to `Inbound` and external recipient to `Outbound`.

## Done when
Build and tests pass, and live validation confirms inbound/outbound mail behavior on production routing.
