# Epic 2 — Mail transport and policy

**Tasks:** T7–T13 · **Depends on:** Epic 1 · **Unblocks:** Epic 3

The hard core of the product. Mail must arrive, be judged, and be delivered — correctly, at scale,
and without ever relaying for a stranger.

---

## T7 — SMTP listener and queue

Inbound on 25, submission on 465 and 587, as hosted services in the same process. The queue is a
PostgreSQL table, not Redis — Redis stays optional and off the delivery hot path.

Retry uses exponential backoff **with jitter**. Without jitter, a thousand queued messages retry
in lockstep and hammer a recovering peer.

A dead-letter store that nobody reads is decoration. It must alert.

**Open-relay protection is not optional.** WHEN a message arrives for a recipient outside every
tenant domain THE SYSTEM SHALL reject at RCPT TO with `550 5.7.1` and SHALL NOT enqueue.

WHEN an unauthenticated client attempts submission on 587 THE SYSTEM SHALL reject with `530 5.7.0`.
There is no configuration flag that permits SMTP authentication without encryption.

**Done when** an accepted message produces exactly one queue row, and a failing delivery produces a
strictly increasing retry delay.

---

## T8 — SPF, DKIM, DMARC

Per-domain DKIM key generation and storage. Sign on outbound, verify on inbound. Publish
`domain_dns_settings` so the admin UI can show the operator exactly which records to create —
and detect when they are missing.

**Done when** an outbound message carries a `DKIM-Signature` that verifies against the published key.

---

## T9 — Anti-spam baseline and quarantine

**This is the largest under-scoped item in v1.** Read this before estimating.

v1 ships: rule-based scoring, DNSBL lookups, greylisting, SPF/DKIM/DMARC-driven verdicts, and
verdict aggregation — all behind `ISpamProvider`. It does **not** ship a trained Bayes classifier.

That is a deliberate trade. A half-trained Bayes filter performs worse than no Bayes filter, and
the seam means Rspamd drops in later without touching the delivery path.

Quarantine supports release and **release-and-train** — the latter being the hook that makes a
future classifier worth having.

**Done when** a message over threshold lands in quarantine and does not reach the mailbox.

---

## T10 — IMAP, storage, and attachments

IMAPS on 993. `IMailStorage` with a filesystem adapter, keyed by content hash so a duplicate
attachment costs one copy.

Stream everything. A 100 MB attachment must never be buffered whole into memory — bounded buffers
with a configured ceiling, on both upload and download.

**Done when** an APPEND followed by a reconnect returns identical bytes and flags.

---

## T11 — ManageSieve

Listener on 4190, script CRUD, validation **before** activation.

WHEN an invalid script is uploaded THE SYSTEM SHALL reject it with the offending line number and
SHALL leave the previously active script untouched. Never partially apply a filter change.

**Done when** a syntactically broken script is rejected and the prior script still runs.

---

## T12 — Search

`ISearchProvider` with the PostgreSQL FTS adapter. Maintain the index on delivery. Provide a
reindex command for operators who restore from backup — a restored database has correct rows but a
stale index, and the operator needs a supported way to fix that.

**Done when** a delivered message is findable by full-text query within one second.

---

## T13 — Rule engine, simulator, explorer

Conditions and actions over the delivery pipeline, with a **dry-run simulator** that reports what
would happen and mutates nothing.

The simulator is what makes rules safe to write. An operator who cannot test a rule before
enabling it will not enable rules at all.

**Done when** a simulated rule reports every match and every action without mutating any message.
