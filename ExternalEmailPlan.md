1. Correction to the earlier diagnosis
My §12.10 note said local mailbox delivery was missing. That was wrong and the plan below supersedes it.

InboundQueueDispatcher (src/Miautrix.Mail.Worker/InboundQueueDispatcher.cs) already exists, is registered at src/Miautrix.Mail.Worker/Program.cs:149, and is complete: it parses MIME, runs antimalware and antispam, enforces quota, writes to IMailStorage, creates the Message row, handles attachments, and sets Status = "Delivered". Nothing needs building.

The defect is narrower and worse than a missing feature.

2. Actual root cause — an unsynchronised race
Both dispatchers are registered and both poll the same table for the same rows:

Dispatcher	Registered	Selects
InboundQueueDispatcher	Program.cs:149	SmtpQueue where Status == "Pending" and NextAttemptAt <= now
OutboundQueueDispatcher	Program.cs:151	SmtpQueue where Status is "Pending" or "Failed" and NextAttemptAt <= now
Neither filters by direction, because SmtpQueueItem has no direction field (src/Miautrix.Mail.Domain/Entities.cs:307 — Sender, Recipient, Subject, RawMessage, Status, Attempts, NextAttemptAt, LastAttemptAt, LastError). There is no row-level claim, no SELECT … FOR UPDATE, and no status transition to an in-flight state before processing.

Whichever dispatcher polls first wins, on a 5-second interval each. Observed outcome: OutboundQueueDispatcher reached the Gmail→miautrix.tech row first, resolved @miautrix.tech in its Cloudflare domainMap, and called transport.SendAsync() with the Gmail address as from. Cloudflare refused to send from a domain outside the account — correct anti-spoofing — producing 502 send_failed and the queue last_error.

The row is now Failed, which only OutboundQueueDispatcher selects. InboundQueueDispatcher filters on Status == "Pending" exclusively. So the message is now permanently captured by the wrong dispatcher and will retry outbound until it dead-letters. That is why it is stuck rather than self-healing.

Two further consequences
Silent loss variant. An inbound message to a local-transport domain hits the outbound dispatcher's else: recipient domain is not Cloudflare-enabled => leave Pending/Failed for retries branch. It stays Pending, so the inbound dispatcher may still collect it — but only until the outbound dispatcher touches it again. Non-deterministic delivery.
Outbound mail is equally at risk. A locally composed message to an external recipient can be picked up by InboundQueueDispatcher, which looks up item.Recipient in Mailboxes, finds nothing, and sets Status = "DeadLetter" with "Recipient mailbox not found." — silently destroying outbound mail. This has not been observed yet only because outbound has been failing auth for the whole of MIA-91. It will start happening as soon as outbound works.
This is not an inbound bug. It is a queue-ownership bug that corrupts both directions.

3. The decision Chief Roger owns
One choice, then the rest is mechanical. Both options are small; they differ in blast radius and future cost.

Option A — Direction discriminator column on smtp_queue
Add Direction (string, "Inbound" / "Outbound", not null) to SmtpQueueItem, map it in AppDbContext, and filter both dispatchers.

Smallest diff. One migration, one column, two WHERE clauses, one EnqueueAsync parameter.
MailQueueController and the admin queue screen keep working unchanged.
Keeps two unrelated lifecycles in one table. Status already means different things per direction ("Quarantined"/"Delivered" are inbound-only; Attempts/NextAttemptAt backoff is outbound-only). The ambiguity stays, just guarded.
Option B — separate inbound_spool and outbound_queue tables
Correct long-term modelling; each table gets only the columns and states its direction uses. Impossible to reintroduce this class of bug.
Larger migration with real data movement, plus changes to MailQueueController, the admin queue UI, BackupService, and MailboxArchiveService if they touch the table.
More work now, and the admin "mail queue" view would need to merge two sources or be split.
My recommendation: Option A now, Option B recorded as a known debt. Reasoning — reversible vs irreversible: Option A is a reversible, additive migration that stops active mail corruption today. Option B is the better model but is a multi-surface refactor, and the bug is currently destroying outbound mail on a 5-second poll. Cost of delay favours shipping the guard immediately. Option A does not block Option B later; a Direction column is exactly the field a future table split would partition on.

If Chief Roger prefers B, the plan below still holds — step 2 becomes two tables instead of one column, and steps 5 and 6 grow.

4. Implementation steps (assumes Option A)
Step 1 — Stop the bleeding (deploy independently, before the schema change)
In src/Miautrix.Mail.Worker/Program.cs, comment out services.AddHostedService<OutboundQueueDispatcher>(); (line 151) and deploy.

Inbound delivery starts working correctly and immediately, because InboundQueueDispatcher is uncontested.
Outbound stays queued rather than corrupted, which is the safe failure.
Verifiable in minutes, reversible by uncommenting.
Do this first. The rest can then proceed without time pressure.

Step 2 — Schema
src/Miautrix.Mail.Domain/Entities.cs:307 — add to SmtpQueueItem:
public string Direction { get; set; } = "Inbound";


Copy
Default "Inbound" is deliberate: see step 4 on backfill.
src/Miautrix.Mail.Persistence/AppDbContext.cs:429 — inside the SmtpQueueItem block, alongside the existing property mappings:
entity.Property(e => e.Direction).HasColumnName("direction").IsRequired();
entity.HasIndex(e => new { e.TenantId, e.Direction, e.Status, e.NextAttemptAt });


Copy
The composite index matches both dispatchers' new predicates; without it both do a full scan every 5 seconds.
Generate the migration following the existing convention in src/Miautrix.Mail.Persistence/Migrations/ (most recent: 20261001144022_CalendarEventRecurrenceFix).
Step 3 — Enqueue with an explicit direction
src/Miautrix.Mail.Queue/SmtpQueueManager.cs:

Add a string direction parameter to ISmtpQueueManager.EnqueueAsync (interface at line 7, implementation at line 40) and set it on the new SmtpQueueItem.
Make it required, not defaulted. A default is how this bug recurs — every call site should have to state its direction.
Update all callers:
src/Miautrix.Mail.Protocols.Smtp/SmtpInboundHandler.cs:56 → "Inbound" (reached from both InboundController.Cloudflare() and SmtpListenerService, both genuinely inbound)
every submission/compose path that enqueues for sending → "Outbound"
Enumerate call sites with grep -rn "EnqueueAsync" --include="*.cs" src/ tests/ and set each one explicitly. Do not guess from the method name.
Step 4 — Backfill existing rows
In the migration's Up, after adding the column. Rows predating this change have no direction, so infer it from recipient locality — the same test IsDomainLocal applies:

UPDATE smtp_queue q
SET direction = CASE
  WHEN EXISTS (
    SELECT 1 FROM domains d
    WHERE d.tenant_id = q.tenant_id
      AND lower(d.name) = lower(split_part(q.recipient, '@', 2))
  ) THEN 'Inbound'
  ELSE 'Outbound'
END;


Copy
A local recipient means the message was received; a non-local recipient means it was being sent. This is correct for every row except a local-to-local message, which is inbound for delivery purposes anyway — so "Inbound" is the right answer there too.

Then reset the stuck row so the inbound dispatcher can collect it (it selects Status == "Pending" only):

UPDATE smtp_queue
SET status = 'Pending', attempts = 0, last_error = NULL, next_attempt_at = now()
WHERE direction = 'Inbound' AND status IN ('Failed', 'DeadLetter');


Copy
Scope this to rows whose last_error matches the Cloudflare 502 if you want to be conservative, but any inbound row sitting in Failed got there through this bug — the inbound dispatcher's own failure paths use "Quarantined" and "DeadLetter" with different messages.

Step 5 — Filter both dispatchers
src/Miautrix.Mail.Worker/InboundQueueDispatcher.cs:66 — add q.Direction == "Inbound" to the Where.
src/Miautrix.Mail.Worker/OutboundQueueDispatcher.cs:177 — add q.Direction == "Outbound" to the Where.
Then re-enable OutboundQueueDispatcher in Program.cs (undo step 1).

Step 6 — Admin queue surface
src/Miautrix.Mail.Web/Controllers/MailQueueController.cs references SmtpQueueItem. Expose Direction in the response and add it as a filter. An operator looking at a stuck message needs to see which direction it is in — that single field would have made this diagnosis immediate instead of four rounds of error-chasing. Pass the UI requirement to Luna if the queue screen needs a column.

5. Tests
The gap that let this ship is that no test asserts dispatcher ownership. Required:

Inbound is never dispatched outbound. Enqueue Direction = "Inbound", run OutboundQueueDispatcher.DispatchBatchAsync directly (it is internal for exactly this), assert no transport call and the row is untouched. Cover both a Cloudflare-transport recipient domain and a local-transport recipient domain — the two branches fail differently.
Outbound is never dispatched inbound. Enqueue Direction = "Outbound" with an external recipient, run the inbound dispatcher, assert the row is not set to "DeadLetter". This is the silent-outbound-loss case from §2.2 and is the most important new test.
Inbound still delivers. POST to /api/v1/inbound/cloudflare with a valid token, external sender, local recipient → Message row created, Status = "Delivered", readable via the message API. Extend tests/Miautrix.Mail.IntegrationTests/Cloudflare/CloudflareTransportTests.cs, which already has the webhook fixtures and InboundWebhookOptions wiring.
Outbound still sends. Locally composed message to an external recipient → dispatched through CloudflareApiMailTransport, 202.
Backfill correctness. Unit-test the inference: local recipient → "Inbound", external recipient → "Outbound".
6. Acceptance criteria
Checkable by someone who was not in this conversation:

services.AddHostedService<OutboundQueueDispatcher>() is registered and both dispatchers filter on Direction.
A message POSTed to /api/v1/inbound/cloudflare (valid token, external sender, local recipient) appears in the recipient's mailbox and is readable in Webmail. smtp_queue.status = 'Delivered', last_error null.
A locally composed message to an external recipient is delivered via CloudflareApiMailTransport with 202, and is never set to "DeadLetter" by the inbound dispatcher.
No row in smtp_queue with direction = 'Inbound' ever has a last_error mentioning the Cloudflare Worker.
All five test groups in §5 pass, including both recipient-domain transport modes in test 1.
The previously stuck production row is Delivered, with the outcome recorded in ERRORS_AND_ISSUES.md §12.10.
ERRORS_AND_ISSUES.md §12.10 updated: chosen option, migration applied, backfill row counts, pass/fail evidence.
dotnet build Miautrix.Mail.sln -warnaserror clean; unit + integration suites green.
MailQueueController returns Direction; the admin queue can filter on it.
Founder's six criteria
Criterion	How this plan satisfies it
Matches the agreed plan, no scope drift	Scope is the queue-ownership defect only. Cloudflare config, Worker code, DNS, and WAF are explicitly out of scope — all verified under MIA-91 §12.5–§12.9.
Rich UI, not a utility screen	Only surface change is a Direction column/filter on the existing admin queue screen. Routed to Luna rather than bolted on by engineering.
Under budget	No new dependency, no paid service. One nullable-free column, two WHERE clauses, one index, five tests.
Fast deployment	Step 1 is a one-line change deployable in minutes and reversible. Full fix is one EF migration via the existing scripts/update-database.sh path.
Modular	A Direction field on the queue is transport-agnostic. It does not couple the queue to Cloudflare; the same guard holds for a future SMTP-relay or SES transport.
Runs on LXC, Windows Server, cloud	No platform-specific code. The migration runs through the existing cross-platform EF tooling (update-database.sh / update-database.ps1).