# Miautrix email Worker (`miautrix-main-worker`)

Cloudflare Worker that owns both halves of the external mail path:

```
INBOUND   sender -> MX (Cloudflare Email Routing) -> email() handler
OUTBOUND  Miautrix.Mail app -> POST /send -> env.EMAIL.send() -> recipient
```

The app side that calls this Worker is
`src/Miautrix.Mail.Application/Transport/CloudflareApiMailTransport.cs`
(`SendPath = "/send"`).

This directory was recovered from the **live deployment** under [MIA-69](/MIA/issues/MIA-69).
Before that, the source existed only in an external repo
(`lee-miautrix-email-worker-acdc7e2a`, named in `ERRORS_AND_ISSUES.md`) that this project
could not read, so the deployed code was unreviewable and unrevertable.

[MIA-69](/MIA/issues/MIA-69) recovered the source into version control.
[MIA-76](/MIA/issues/MIA-76) rewrote the **outbound `POST /send`** handler (strict status contract, authentication, payload limits).
[MIA-77](/MIA/issues/MIA-77) remediated the **inbound `email()`** handler (domain wildcard allow-list matching, configuration-driven destination, secret webhook token authentication, fail-closed delivery error handling).

## `POST /send` contract

What the handler answers, and the only contract the app may rely on:

| Status | Condition | Body |
| --- | --- | --- |
| `401` | missing or wrong bearer token — checked **before** the body is read | `{"ok":false,"error":"unauthorized",...}` |
| `415` | non-JSON `Content-Type` header | `{"ok":false,"error":"unsupported_media_type",...}` |
| `400` | unreadable or invalid body: non-JSON, non-object, missing fields, invalid address, invalid MIME structure | `{"ok":false,"error":...}` |
| `413` | raw payload exceeds 25 MB decoded limit | `{"ok":false,"error":"payload_too_large",...}` |
| `202` | accepted and handed to `env.EMAIL.send()` | `{"ok":true}` |
| `502` | `env.EMAIL.send()` failed | `{"ok":false,"error":"send_failed",...}` |
| `503` | no `SEND_TOKEN` secret or missing `EMAIL` binding — fails closed, never open | `{"ok":false,"error":...}` |
| `405` | unsupported HTTP method (e.g. `GET /send`) | `{"ok":false,"error":"method_not_allowed",...}` |

**No request shape returns a Cloudflare `1101`.** `1101` is what the runtime emits when a
handler throws uncaught; every parse now sits inside a guard and the whole handler has an
outer `catch`. Authentication runs first, so a bad token on an unreadable body answers
`401`, not `400` — an unauthenticated caller learns nothing about request validation.

`GET /`, `GET /health` (`200 {"ok":true}`) and `GET /nope` (`404`) are unchanged.

The app side needs **no change**: `CloudflareApiMailTransport` already posts `/send` with
`from`/`to`/`raw` (base64) and already sends `Authorization: Bearer <CLOUDFLARE_API_TOKEN>`.
The Worker simply reads that header now, where before it ignored it.

## `email()` inbound handler contract (MIA-77)

What the inbound email handler does when triggered by Cloudflare Email Routing:

| Scenario | Behavior |
| --- | --- |
| Allowed sender at tenant domain (`someone@miautrix.tech` or mixed case) | Matches wildcard `*@miautrix.tech`, proceeds to delivery |
| Allowed exact sender (`admin@miautrix.org`) | Matches exact pattern, proceeds to delivery |
| Disallowed sender / outside domain (`attacker@evil.com`, `user@sub.miautrix.tech`) | Rejects via `message.setReject("Address not allowed")` |
| Malformed / missing / non-string `from` | Rejects via `message.setReject("Address not allowed")` without throwing uncaught errors |
| HTTP webhook destination with `INBOUND_TOKEN` secret | Posts MIME stream to webhook with `X-Miautrix-Inbound-Token`, `X-Miautrix-Envelope-From`, `X-Miautrix-Envelope-To` |
| HTTP webhook destination with missing `INBOUND_TOKEN` | Fails closed: logs error and calls `message.setReject("Inbound webhook authentication not configured")` |
| HTTP webhook destination returns non-2xx status (e.g. `403` containment, `500`) | Rejects via `message.setReject("Inbound delivery failed (HTTP <status>)")` so sending MTA generates NDR/bounce |
| Email forwarding destination (e.g. `ops@miautrix.org`) | Forwards via `await message.forward(destination)` |

## Rollback point

The deployment live at recovery time, and the version to roll back to:

| Field | Value |
| --- | --- |
| Script | `miautrix-main-worker` |
| Version id | `327baddf-2d9d-494f-9630-e383c4ca4aa4` |
| Version number | `6` |
| Deployment id | `db40576c-f0b2-40d9-8720-44207ad9c692` |
| Script etag | `b6582d3ac146eacda28478b87c91bbbf6b07b4fbc0c911a20c0336d4083c0165` |
| Deployed on | `2026-09-21T19:33:11.539664Z` |
| Deployed from | `api` (previous five versions came from the dashboard quick editor) |
| Handlers | `email`, `fetch` |
| Artefact sha256 | `58e7c87472e1b24e0b89f45811c528f5b3621255b3723e2fd01378be97b50e9d` (`deployed-artifact.index.js`) |

`deployed-artifact.index.js` is the byte-exact deployed module, kept as the rollback
artefact and as the diff baseline for any future change. `src/index.ts` is the same code
with types and diagnosis comments added.

### Rolling back

Cloudflare keeps prior versions server-side, so rollback does not require rebuilding:

```bash
# Re-point 100% of traffic at the recorded good version.
npx wrangler versions deploy 327baddf-2d9d-494f-9630-e383c4ca4aa4@100% \
  --name miautrix-main-worker --yes
```

Verify afterwards with the health probe in "Verification" below, and confirm the active
version id:

```bash
npx wrangler deployments list --name miautrix-main-worker
```

## Deploying

Requires `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` in the environment. Neither is
committed; both are injected at runtime. The token needs **Account -> Workers Scripts ->
Edit**.

```bash
cd workers/email
CLOUDFLARE_API_TOKEN="$CF_TOKEN" CLOUDFLARE_ACCOUNT_ID="$CF_ACCOUNT_ID" \
  npx wrangler deploy
```

One command, no other steps. `wrangler` is the only tool required and is free.

### The `SEND_TOKEN` secret

`POST /send` requires a `SEND_TOKEN` Worker secret. It is **not** in `wrangler.jsonc` and
not in any source file — an unset secret makes the handler answer `503` rather than allow
anyone through. Set it once per environment, before the first authenticated deploy:

```bash
cd workers/email
# Reads the value from stdin; it is never an argument and never appears in shell history.
CLOUDFLARE_API_TOKEN="$CF_TOKEN" CLOUDFLARE_ACCOUNT_ID="$CF_ACCOUNT_ID" \
  npx wrangler secret put SEND_TOKEN --name miautrix-main-worker
```

The same value must be the app's `CLOUDFLARE_API_TOKEN` for the Cloudflare transport, since
that is the header `CloudflareApiMailTransport` already sends. It is held as the Paperclip
secret `miautrix-mail-server/cloudflare_worker_send_token`.

For local runs, `wrangler dev` reads `workers/email/.dev.vars`
(`SEND_TOKEN="..."`). That file is gitignored and must never be committed.

Dry run, which changes nothing and needs no credential:

```bash
cd workers/email && npx wrangler deploy --dry-run
```

## Required bindings & variables

| Binding / Variable | Type | Purpose |
| --- | --- | --- |
| `EMAIL` | `send_email` | Backs `env.EMAIL.send()` on `POST /send`. Declared in `wrangler.jsonc`. Currently unrestricted — no `allowed_destination_addresses`. |
| `SEND_TOKEN` | secret | Shared bearer token for `POST /send`. Set with `wrangler secret put SEND_TOKEN`, never in `wrangler.jsonc`. Absent -> handler answers `503`. |
| `INBOUND_TOKEN` | secret | Shared webhook token sent in `X-Miautrix-Inbound-Token` on `POST /api/v1/inbound/cloudflare`. Set with `wrangler secret put INBOUND_TOKEN`, never in `wrangler.jsonc`. Absent -> email() fails closed. |
| `ALLOWED_SENDER_PATTERNS` | var | Allowed sender pattern list (e.g. `*@miautrix.tech,admin@miautrix.org`). Declared in `wrangler.jsonc` `vars`. |
| `INBOUND_DESTINATION` | var | Inbound delivery URL or email forward target. Declared in `wrangler.jsonc` `vars`. |

Also required, and **not** in this file:

- **Email Routing rules** (zone-level, Cloudflare dashboard) bind the inbound address to the
  `email()` handler. Configured outside this repo.
- **MX records** for the mail domain point at Cloudflare. Configured outside this repo.
- **workers.dev subdomain** enabled, serving
  `https://miautrix-main-worker.<account>.workers.dev`. Preview URLs are disabled.

## Verification

### The Inbound `email()` contract check (MIA-77)

`verify-inbound-contract.sh` proves the inbound matching logic, edge cases, webhook formatting, secret token injection, and fail-closed error handling:

```bash
cd workers/email
./verify-inbound-contract.sh
```

Executed: **14 cases, 0 failed, 0 `1101`s.**

### The `POST /send` contract check (MIA-76)

`verify-send-contract.sh` is the committed proof of the outbound table: 29 cases covering the
unchanged `GET` paths, method guards (`405`), media type check (`415`), every `401` ordering case,
every `400` body shape, `413` oversize payload, and the well-formed request reaching
`env.EMAIL.send()`. One command:

```bash
cd workers/email
npx wrangler dev --local --port 8799 &          # local runtime, reads .dev.vars
SEND_TOKEN="$YOUR_TOKEN" BASE=http://127.0.0.1:8799 ./verify-send-contract.sh
```

It exits non-zero if any case misses its expected status, and prints the status and body of
each case so the result is checkable rather than asserted.

Executed against `wrangler dev --local` (wrangler 4.147.0): **29 cases, 0 failed, 0 `1101`s.**

### Unchanged paths against production

Read-only, no credential needed, safe to run against production:

```bash
curl -s -o /dev/null -w '%{http_code}\n' https://miautrix-main-worker.comarcat.workers.dev/health
# expect 200
curl -s https://miautrix-main-worker.comarcat.workers.dev/health
# expect {"ok":true}
curl -s -w ' %{http_code}\n' https://miautrix-main-worker.comarcat.workers.dev/nope
# expect: Not found 404
```

## Root cause of Cloudflare `1101` on `POST /send`

**One line: in `POST /send`, `request.json()`, `atob(body.raw)` and `new EmailMessage(...)`
all run outside the `try` block, so any bad or missing field throws uncaught and Cloudflare
returns `1101` instead of a `4xx`.**

That explains the full QA differential on [MIA-64](/MIA/issues/MIA-64) — `1101` for a
well-formed body, a malformed body, and a wrong bearer token alike:

- `{}` or `{"to":...}` -> `body.raw` is `undefined`, `atob("undefined")` throws
  `InvalidCharacterError`.
- `not-json` -> `request.json()` throws `SyntaxError`.
- Wrong bearer token -> **no authentication check existed anywhere in the handler**, so the
  request fell through to the same unguarded parse and threw there.
- A well-formed body still reached `env.EMAIL.send()` with an unrestricted binding and an
  unverified sender; `1101` here was consistent with a throw before or inside that call that
  the `try` did cover only for `send()` itself.

The `try` wrapped only `env.EMAIL.send()`, which is the one statement least likely to be the
first thing to fail.

Two further defects found in the same recovery, documented and addressed in MIA-77:

- `email()` allow-list uses `allowList.indexOf(message.from)`, an exact string compare, so the
  `"*@miautrix.tech"` wildcard entry never matches a real sender. Only the literal
  `admin@miautrix.org` can pass.
- `email()` forwards to `"inbox@corp"`, which is not a routable address or a verified Email
  Routing destination, so the forward cannot succeed. The architecture calls for
  `POST /api/v1/inbound/cloudflare` into the app instead.

## Platform coverage

| Target | Status |
| --- | --- |
| Linux (LXC / container) | Tested. All recovery, reconciliation and verification commands in this file, including the 29-case contract check, were executed on Linux. |
| Windows Server | Untested. `npx wrangler deploy` is Node-based and portable; `verify-send-contract.sh` is POSIX `sh` and needs WSL, Git Bash, or a PowerShell port. No path separator or line-ending assumption is baked into the config. |
| Cloud / CI | Untested. Needs `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` as CI secrets; no other change expected. |

## Secrets

No credential, token, account id or zone id is committed in this directory. The Cloudflare
token used for recovery and deploy is held as a Paperclip secret and injected at runtime. Do not add
`account_id` to `wrangler.jsonc`; pass `CLOUDFLARE_ACCOUNT_ID` instead.
