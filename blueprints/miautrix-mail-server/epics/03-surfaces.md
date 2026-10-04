# Epic 3 — Surfaces

**Tasks:** T14–T17 · **Depends on:** Epic 2 · **Unblocks:** Epic 4

One service layer, three doors. The REST API, the web surfaces, and the future MCP server all sit
on the same application services. A transport is thin; if business logic appears in a controller,
something has gone wrong.

---

## T14 — API contract and OpenAPI

REST under `/api/v1/*`. Two envelopes, no exceptions:

```
{ "data": ..., "meta": { "next_cursor": ..., "has_more": false } }
{ "error": { "code": ..., "message": ..., "details": ..., "request_id": ... } }
```

**Returning 200 with `{"success": false}` is the single most common API design mistake.** A
failing request gets a failing status code.

Parse, do not validate: reject unknown fields, return 422 with field-level details. Put
`Idempotency-Key` on every mutating endpoint. Generate OpenAPI from schemas and check it in.

Version with `/v1`; additive changes only within a version. Two versions maximum, ever.

**Done when** a request missing a required field returns 422 with field-level detail, and never a
200 carrying a failure.

---

## T15 — Web admin GUI

React admin covering the source document's §32 menu: Dashboard, Mail Flow, Users, Domains,
Identity, Security, Anti-Spam, Anti-Malware, Queue, Quarantine, Logs, Reports, Backup, System,
Licensing.

The rule designer uses `@xyflow/react` and drives T13's simulator — an operator drags a
condition, hits simulate, and sees the result without touching live mail.

Tables use cursor pagination with a server-side page cap. Watch for the N+1 on the dashboard:
one query per tenant, not one per row.

**Done when** the queue screen renders paginated and issues no more than one request per screen.

---

## T16 — Webmail

JMAP-first: mailbox list, message list, reading pane, composer, contacts, calendar, and Sieve
filter management.

**Message bodies are hostile input.** WHEN a body containing a `<script>` tag is rendered THE
SYSTEM SHALL sanitise it with DOMPurify and SHALL NOT execute it. This is not a nicety — an
unsanitised mail body is stored XSS with a delivery mechanism.

**Done when** a script-bearing body renders inert and the sanitiser test passes.

---

## T17 — CLI

`miautrix-mail` on Spectre.Console.Cli, covering every administrative function the desktop app
offers. `System.CommandLine` was rejected: it is still beta.

Commands honour the same authorization as the GUI. A CLI that bypasses permissions is a privilege
escalation waiting to be found.

**Done when** an authorized operator lists a tenant's mailboxes and exits 0, while an unauthorized
user exits non-zero with a clear message.
