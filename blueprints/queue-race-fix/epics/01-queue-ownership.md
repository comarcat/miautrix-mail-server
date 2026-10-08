# Epic 01 — Queue Ownership Schema

## Goal
Add durable ownership metadata to `smtp_queue` so inbound and outbound workers cannot race for the same row.

## Tasks
- QRF-001 — Stop outbound dispatcher temporarily.
- QRF-002 — Add `direction` schema and polling index.
- QRF-003 — Backfill existing rows and reset stuck inbound rows conservatively.

## Done when
`SmtpQueueItem.Direction` exists, EF maps it, migration compiles, and existing rows are classified as `Inbound` or `Outbound`.
