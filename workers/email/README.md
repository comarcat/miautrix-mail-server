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

**No behaviour was changed and nothing was redeployed by the recovery task.** The
`/send` fix is a separate task.

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

Rollback status: **documented, and the target version is confirmed to exist** (read back
from the versions API). Not executed — executing it would be a deployment, which MIA-69
forbids.

## Deploying

Requires `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` in the environment. Neither is
committed; both are injected at runtime. The token needs
**Account -> Workers Scripts -> Edit** (read-only is enough for inspection, not for deploy).

```bash
cd workers/email
CLOUDFLARE_API_TOKEN="$CF_TOKEN" CLOUDFLARE_ACCOUNT_ID="$CF_ACCOUNT_ID" \
  npx wrangler deploy
```

One command, no other steps. `wrangler` is the only tool required and is free.

Dry run, which changes nothing and needs no credential:

```bash
cd workers/email && npx wrangler deploy --dry-run
```

## Required bindings

| Binding | Type | Purpose |
| --- | --- | --- |
| `EMAIL` | `send_email` | Backs `env.EMAIL.send()` on `POST /send`. Declared in `wrangler.jsonc`. Currently unrestricted — no `allowed_destination_addresses`. |

Also required, and **not** in this file:

- **Email Routing rules** (zone-level, Cloudflare dashboard) bind the inbound address to the
  `email()` handler. Configured outside this repo.
- **MX records** for the mail domain point at Cloudflare. Configured outside this repo.
- **workers.dev subdomain** enabled, serving
  `https://miautrix-main-worker.<account>.workers.dev`. Preview URLs are disabled.

There is no bearer-token secret bound to this Worker. The app sends
`Authorization: Bearer <token>`, but the deployed code never reads it — see the root cause.

## Verification

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

Not confirmed by execution: the `POST /send` and `email()` paths. Exercising them would send
or reject real mail, which MIA-69 forbids. Their behaviour is asserted from the recovered
bytes plus the QA evidence on [MIA-64](/MIA/issues/MIA-64).

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
| Linux (LXC / container) | Tested. All recovery and verification commands in this file were executed on Linux. |
| Windows Server | Untested. `npx wrangler deploy` is Node-based and portable; the `curl` verification lines need PowerShell equivalents. No path separator or line-ending assumption is baked into the config. |
| Cloud / CI | Untested. Needs `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` as CI secrets; no other change expected. |

## Secrets

No credential, token, account id or zone id is committed in this directory. The Cloudflare
token used for recovery is held as a Paperclip secret and injected at runtime. Do not add
`account_id` to `wrangler.jsonc`; pass `CLOUDFLARE_ACCOUNT_ID` instead.
