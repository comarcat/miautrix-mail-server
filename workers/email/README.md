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

[MIA-69](/MIA/issues/MIA-69) changed no behaviour and redeployed nothing.
[MIA-76](/MIA/issues/MIA-76) rewrote the **outbound `POST /send`** handler only — see
"`POST /send` contract" below. The inbound `email()` handler is still byte-for-byte as
recovered; its two defects belong to a later task.

## `POST /send` contract

What the handler answers, and the only contract the app may rely on:

| Status | Condition | Body |
| --- | --- | --- |
| `401` | missing or wrong bearer token — checked **before** the body is read | `{"ok":false,"error":...}` |
| `400` | unreadable or invalid body: non-JSON, non-object, bad/missing `raw`, bad/missing `from`/`to` | `{"ok":false,"error":...}` |
| `200` | accepted and handed to `env.EMAIL.send()` | `{"ok":true}` |
| `500` | `env.EMAIL.send()` failed, or an unforeseen throw | `{"ok":false,"error":...}` |
| `503` | no `SEND_TOKEN` secret is configured — fails closed, never open | `{"ok":false,"error":...}` |

**No request shape returns a Cloudflare `1101`.** `1101` is what the runtime emits when a
handler throws uncaught; every parse now sits inside a guard and the whole handler has an
outer `catch`. Authentication runs first, so a bad token on an unreadable body answers
`401`, not `400` — an unauthenticated caller learns nothing about request validation.

`GET /`, `GET /health` (`200 {"ok":true}`) and `GET /nope` (`404`) are unchanged.

The app side needs **no change**: `CloudflareApiMailTransport` already posts `/send` with
`from`/`to`/`raw` (base64) and already sends `Authorization: Bearer <CLOUDFLARE_API_TOKEN>`.
The Worker simply reads that header now, where before it ignored it.

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

Rollback status as of [MIA-76](/MIA/issues/MIA-76): **target confirmed live and intact; the
rollback command was executed and refused by permissions, not by state.**

Read back from the versions and deployments API on 2026-10-04:

- version `327baddf-2d9d-494f-9630-e383c4ca4aa4` still exists, number `6`, handlers
  `email`/`fetch`, etag `b6582d3ac146eacda28478b87c91bbbf6b07b4fbc0c911a20c0336d4083c0165`
- it is still the **active** deployment: `db40576c-f0b2-40d9-8720-44207ad9c692`, `api`
  source, `327baddf...@100%`

The rollback command above was run verbatim and reached Cloudflare's
`/workers/scripts/miautrix-main-worker/deployments` endpoint, which answered
`No access to the specified resource.` — the available API token is **Workers Scripts:
Read**. So the rollback path is proven correct up to the authorization boundary: the target
exists, the command addresses the right endpoint, and only a write-scoped token is missing.
Full execution needs the deploy token described below.

## Deploying

Requires `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` in the environment. Neither is
committed; both are injected at runtime. The token needs **Account -> Workers Scripts ->
Edit**. Read is enough for inspection and *not* enough for deploy, secret writes, or
rollback — all three return `No access to the specified resource.` with a read-only token.

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

## Required bindings

| Binding | Type | Purpose |
| --- | --- | --- |
| `EMAIL` | `send_email` | Backs `env.EMAIL.send()` on `POST /send`. Declared in `wrangler.jsonc`. Currently unrestricted — no `allowed_destination_addresses`. |
| `SEND_TOKEN` | secret | Shared bearer token for `POST /send`. Set with `wrangler secret put`, never in `wrangler.jsonc`. Absent -> handler answers `503`. |

Also required, and **not** in this file:

- **Email Routing rules** (zone-level, Cloudflare dashboard) bind the inbound address to the
  `email()` handler. Configured outside this repo.
- **MX records** for the mail domain point at Cloudflare. Configured outside this repo.
- **workers.dev subdomain** enabled, serving
  `https://miautrix-main-worker.<account>.workers.dev`. Preview URLs are disabled.

Before [MIA-76](/MIA/issues/MIA-76) no bearer-token secret was bound to this Worker: the app
sent `Authorization: Bearer <token>` and the deployed code never read it — see the root
cause. The committed source now reads it and requires `SEND_TOKEN`; the secret still has to
be set on the script, which needs a write-scoped token.

## Verification

### The `POST /send` contract check (MIA-76)

`verify-send-contract.sh` is the committed proof of the table above: 23 cases covering the
unchanged `GET` paths, every `401` ordering case, every `400` body shape, and the
well-formed request reaching `env.EMAIL.send()`. One command:

```bash
cd workers/email
npx wrangler dev --local --port 8799 &          # local runtime, reads .dev.vars
SEND_TOKEN="$YOUR_TOKEN" BASE=http://127.0.0.1:8799 ./verify-send-contract.sh
```

It exits non-zero if any case misses its expected status, and prints the status and body of
each case so the result is checkable rather than asserted.

**Local by default, deliberately.** A local `wrangler dev` has no real `send_email` binding,
so no case can emit real mail to a third party. Against the local runtime the well-formed
case answers `500` (`{"ok":false,"error":...}` — accepted, validated, send failed), which is
exactly the "a send failure is not a client error, and never a `1101`" guarantee. Override
with `WELL_FORMED_EXPECT=200` when running against a deployment whose destination is
verified.

Executed on 2026-10-04 against `wrangler dev --local` (wrangler 4.147.0): **23 cases, 0
failed.** Two paths were additionally proved by hand:

- a MIME body carrying a `Message-ID` -> `200 {"ok":true}`, i.e. a valid request really does
  reach and complete `env.EMAIL.send()`
- a runtime started with **no** `SEND_TOKEN` -> `503` on `POST /send` while `GET /health`
  still answers `200`, i.e. misconfiguration fails closed without taking down the health
  probe

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

Confirm the committed source still matches what is deployed:

```bash
curl -s -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  "https://api.cloudflare.com/client/v4/accounts/$CLOUDFLARE_ACCOUNT_ID/workers/scripts/miautrix-main-worker/content/v2" \
  -o /tmp/live.multipart
# extract the index.js part, then:
sha256sum workers/email/deployed-artifact.index.js
# expect 58e7c87472e1b24e0b89f45811c528f5b3621255b3723e2fd01378be97b50e9d
```

### How the committed source was confirmed to match the deployment

Three independent checks, all run during recovery:

1. **Byte provenance.** `src/index.ts` and `deployed-artifact.index.js` were extracted from
   the Workers API `content/v2` endpoint for the live script, which returns the deployed
   module itself. This is the deployed artefact, not a reconstruction.
2. **Build equivalence.** `npx wrangler deploy --dry-run --outdir <tmp>` compiles
   `src/index.ts` and the result was diffed against `deployed-artifact.index.js`. After
   normalising comments, whitespace and esbuild's `var index_default` export wrapping, the
   two differ only by insignificant whitespace and one trailing comma. No statement differs.
   So the committed TypeScript compiles to the deployed module.
3. **Black-box behaviour.** Every branch of the `fetch` handler that can be exercised without
   sending mail was probed against the live URL and matched the recovered code: `GET /`
   -> `200 {"ok":true}`, `GET /health` -> `200 {"ok":true}`, `GET /nope`
   -> `404 Not found`.

Not confirmed by execution during recovery: the `POST /send` and `email()` paths. Exercising
them would send or reject real mail, which MIA-69 forbids. Their behaviour is asserted from
the recovered bytes plus the QA evidence on [MIA-64](/MIA/issues/MIA-64).

[MIA-76](/MIA/issues/MIA-76) has since exercised `POST /send` in full against a local
runtime — see "The `POST /send` contract check" above. `email()` remains unexercised, by
design.

## Root cause of Cloudflare `1101` on `POST /send`

**One line: in `POST /send`, `request.json()`, `atob(body.raw)` and `new EmailMessage(...)`
all run outside the `try` block, so any bad or missing field throws uncaught and Cloudflare
returns `1101` instead of a `4xx`.**

That explains the full QA differential on [MIA-64](/MIA/issues/MIA-64) — `1101` for a
well-formed body, a malformed body, and a wrong bearer token alike:

- `{}` or `{"to":...}` -> `body.raw` is `undefined`, `atob("undefined")` throws
  `InvalidCharacterError`.
- `not-json` -> `request.json()` throws `SyntaxError`.
- Wrong bearer token -> **no authentication check exists anywhere in the handler**, so the
  request falls through to the same unguarded parse and throws there.
- A well-formed body still reaches `env.EMAIL.send()` with an unrestricted binding and an
  unverified sender; `1101` here is consistent with a throw before or inside that call that
  the `try` does cover only for `send()` itself.

The `try` wraps only `env.EMAIL.send()`, which is the one statement least likely to be the
first thing to fail.

Two further defects found in the same recovery, documented and **not** fixed:

- `email()` allow-list uses `allowList.indexOf(message.from)`, an exact string compare, so the
  `"*@miautrix.tech"` wildcard entry never matches a real sender. Only the literal
  `admin@miautrix.org` can pass.
- `email()` forwards to `"inbox@corp"`, which is not a routable address or a verified Email
  Routing destination, so the forward cannot succeed. The architecture calls for
  `POST /api/v1/inbound/cloudflare` into the app instead.

## Platform coverage

This is a Cloudflare Workers artefact and runs on Cloudflare's runtime, so the
LXC / Windows Server / cloud parity question applies to the **deploy toolchain**, not to the
Worker:

| Target | Status |
| --- | --- |
| Linux (LXC / container) | Tested. All recovery and verification commands in this file, including the 23-case contract check, were executed on Linux. |
| Windows Server | Untested. `npx wrangler deploy` is Node-based and portable; `verify-send-contract.sh` is POSIX `sh` and needs WSL, Git Bash, or a PowerShell port. No path separator or line-ending assumption is baked into the config. |
| Cloud / CI | Untested. Needs `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` as CI secrets; no other change expected. |

`verify-send-contract.sh` is the one portability gap this change introduces: it is a POSIX
shell script, so Windows Server needs WSL, Git Bash, or a PowerShell equivalent. The Worker
itself and `wrangler deploy` carry no platform assumption.

## Secrets

No credential, token, account id or zone id is committed in this directory. The Cloudflare
token used for recovery is held as a Paperclip secret and injected at runtime. Do not add
`account_id` to `wrangler.jsonc`; pass `CLOUDFLARE_ACCOUNT_ID` instead.
