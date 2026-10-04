# Change log — MIA-76, `POST /send` contract fix

Every command run against the Cloudflare Worker `miautrix-main-worker` for
[MIA-76](/MIA/issues/MIA-76), in order, with raw responses. Executed 2026-10-04 on Linux,
wrangler `4.147.0`, Cloudflare account `ce330e96ff03d34dbe02ef88e18ea515`.

Credentials are supplied through the environment in every command below
(`CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `SEND_TOKEN`). No value appears in this
file, in any command text, or in any commit.

Outcome in one line: **the fix is committed and verified, and is NOT deployed, because the
available Cloudflare API token is read-only.**

---

## 1. Baseline — state before any change

### 1.1 The live deployment, read back

```bash
npx wrangler deployments list --name miautrix-main-worker
```

Active deployment, newest entry:

```
Created:     2026-09-21T19:33:11.539Z
Author:      comarcat@gmail.com
Source:      Upload
Message:     Automatic deployment on upload.
Version(s):  (100%) 327baddf-2d9d-494f-9630-e383c4ca4aa4
```

Cross-checked through the API, sorting deployments by `created_on` so the result does not
depend on list order:

```bash
curl -s -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  "https://api.cloudflare.com/client/v4/accounts/$CLOUDFLARE_ACCOUNT_ID/workers/scripts/miautrix-main-worker/deployments"
```

```
ACTIVE deployment: db40576c-f0b2-40d9-8720-44207ad9c692
created_on: 2026-09-21T19:33:11.539664Z source: api
versions: [('327baddf-2d9d-494f-9630-e383c4ca4aa4', 100)]
```

This matches the [MIA-69](/MIA/issues/MIA-69) rollback point exactly. Full version history,
which is also the RSK-06 evidence — five of six versions came from the dashboard:

```
6 327baddf-2d9d-494f-9630-e383c4ca4aa4 2026-09-21T19:33:11Z src=api
5 c393f3aa-4e05-46d0-b6a5-3e6d795cc99d 2026-09-21T19:27:22Z src=dash
4 748ae2ad-f5ee-48a1-92b2-86550bceba96 2026-09-21T18:41:10Z src=dash
3 fa4e9a9b-1670-4aae-8aea-82b369897321 2026-09-21T18:37:21Z src=dash
2 c297d03d-42ad-48c4-aadd-9fff6e2b0307 2026-09-21T18:23:29Z src=dash
1 df8b886a-3b1d-4b1e-94ce-71262dd84153 2026-09-21T18:22:38Z src=dash
```

### 1.2 Secrets bound to the script

```bash
npx wrangler secret list --name miautrix-main-worker
```

```
[]
```

No secret at all, confirming the handler had no token to check against even if it had tried.

### 1.3 The unchanged public paths still answer

```bash
curl -s -o /dev/null -w '%{http_code}\n' https://miautrix-main-worker.comarcat.workers.dev/health
```

```
200
```

---

## 2. Build the change

```bash
cd workers/email && npx wrangler deploy --dry-run --outdir /tmp/wdry
```

```
Total Upload: 4.60 KiB / gzip: 1.76 KiB
Your Worker has access to the following bindings:
Binding                       Resource
env.EMAIL (unrestricted)      Send Email

--dry-run: exiting now.
```

Config and TypeScript both valid. Note the binding list shows `env.EMAIL` only — `SEND_TOKEN`
is a secret, so it is correctly absent from `wrangler.jsonc`.

---

## 3. The `SEND_TOKEN` secret

Generated in-run, never printed, never passed as an argument:

```bash
umask 077
openssl rand -base64 48 | tr -d '\n=' | tr '+/' '-_' > "$TOKEN_FILE"
```

64 characters, URL-safe. Proposed to Paperclip as the durable store rather than kept in a
file — the value went straight from the file into the API body via `jq --arg`:

- secret proposal `ab911453-22fe-479f-ad04-7901950e13c8`, `pending`, name
  `miautrix-mail-server/cloudflare_worker_send_token`
- binding proposal `4ef16e2d-9602-4788-b983-b2ffc4fa5fc4`, `pending`, config path
  `access.cloudflare_worker_send_token`

### 3.1 Setting it on the Worker — REFUSED

```bash
npx wrangler secret put SEND_TOKEN --name miautrix-main-worker < "$TOKEN_FILE"
```

```
✘ [ERROR] A request to the Cloudflare API
  (/accounts/ce330e96ff03d34dbe02ef88e18ea515/workers/scripts/miautrix-main-worker/secrets)
  failed.

  No access to the specified resource.
```

Retried directly against the API to rule out a wrangler problem:

```bash
jq -n --arg t "$SEND_TOKEN" '{name:"SEND_TOKEN",text:$t,type:"secret_text"}' |
curl -s -X PUT -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  -H "Content-Type: application/json" --data-binary @- \
  ".../workers/scripts/miautrix-main-worker/secrets"
```

```json
{ "result": null, "success": false,
  "errors": [ { "message": "No access to the specified resource." } ] }
```

---

## 4. Establishing that the token is read-only, not that the request was malformed

The token is valid and active:

```bash
curl -s -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  https://api.cloudflare.com/client/v4/user/tokens/verify
```

```json
{ "result": { "id": "743069de1fde2bf2c21d02d833907cb0", "status": "active" },
  "success": true,
  "messages": [ { "code": 10000, "message": "This API Token is valid and active" } ] }
```

Reads succeed, writes fail. Same token, same account, same script:

| Operation | Method | Result |
| --- | --- | --- |
| `workers/scripts/miautrix-main-worker/settings` | GET | `200` |
| `workers/scripts/miautrix-main-worker/secrets` | GET | `200` |
| `workers/scripts/miautrix-main-worker/secrets` | PUT | `No access to the specified resource.` |
| `workers/scripts/miautrix-main-worker/versions` | POST | `No access to the specified resource.` |
| `workers/scripts/miautrix-main-worker/deployments` | POST | `No access to the specified resource.` |

The version upload, which does not shift traffic and would have been the safest possible
write:

```bash
npx wrangler versions upload --name miautrix-main-worker
```

```
Total Upload: 4.60 KiB / gzip: 1.76 KiB
✘ [ERROR] A request to the Cloudflare API
  (/accounts/ce330e96ff03d34dbe02ef88e18ea515/workers/scripts/miautrix-main-worker/versions)
  failed.

  No access to the specified resource.
```

**Conclusion: the token carries Workers Scripts *Read*. Deploy needs Account → Workers
Scripts → Edit.** This is RSK-08.

---

## 5. Verification — the contract, proved locally

Because the script could not be deployed, the contract was exercised against a local runtime
instead. This is also the safer place to exercise it: a local `wrangler dev` has no real
`send_email` binding, so no probe can emit mail to a third party.

```bash
cd workers/email
printf 'SEND_TOKEN="%s"\n' "$TOKEN" > .dev.vars   # gitignored, 0600
npx wrangler dev --local --port 8799 --ip 127.0.0.1 &
SEND_TOKEN="$TOKEN" BASE=http://127.0.0.1:8799 ./verify-send-contract.sh
```

Full raw output:

```
POST /send contract against http://127.0.0.1:8799

PASS GET / -> 200 {ok:true}                         expected 200 got 200  {"ok":true}
PASS GET /health -> 200 {ok:true}                   expected 200 got 200  {"ok":true}
PASS GET /nope -> 404                               expected 404 got 404  Not found
PASS no Authorization header                        expected 401 got 401  {"ok":false,"error":"Missing or invalid bearer token."}
PASS wrong bearer token                             expected 401 got 401  {"ok":false,"error":"Missing or invalid bearer token."}
PASS wrong scheme (Basic)                           expected 401 got 401  {"ok":false,"error":"Missing or invalid bearer token."}
PASS bad token + invalid body -> 401 not 400        expected 401 got 401  {"ok":false,"error":"Missing or invalid bearer token."}
PASS bad token + no body -> 401 not 400             expected 401 got 401  {"ok":false,"error":"Missing or invalid bearer token."}
PASS authenticated, no body                         expected 400 got 400  {"ok":false,"error":"Invalid request body: SyntaxError: Unexpected end of JSON input"}
PASS authenticated, empty JSON object {}            expected 400 got 400  {"ok":false,"error":"Field 'from' is missing or is not a valid email address."}
PASS authenticated, non-JSON body                   expected 400 got 400  {"ok":false,"error":"Invalid request body: SyntaxError: Unexpected token 'o', \"not json\" is not valid JSON"}
PASS authenticated, JSON array not object           expected 400 got 400  {"ok":false,"error":"Body must be a JSON object with from, to and raw."}
PASS authenticated, JSON null                       expected 400 got 400  {"ok":false,"error":"Body must be a JSON object with from, to and raw."}
PASS raw malformed (not base64)                     expected 400 got 400  {"ok":false,"error":"Field 'raw' is not valid base64."}
PASS raw missing                                    expected 400 got 400  {"ok":false,"error":"Field 'raw' is missing or is not a base64 string."}
PASS raw not a string                               expected 400 got 400  {"ok":false,"error":"Field 'raw' is missing or is not a base64 string."}
PASS raw empty string                               expected 400 got 400  {"ok":false,"error":"Field 'raw' is missing or is not a base64 string."}
PASS from missing                                   expected 400 got 400  {"ok":false,"error":"Field 'from' is missing or is not a valid email address."}
PASS from not an address                            expected 400 got 400  {"ok":false,"error":"Field 'from' is missing or is not a valid email address."}
PASS to missing                                     expected 400 got 400  {"ok":false,"error":"Field 'to' is missing or is not a valid email address."}
PASS to not an address                              expected 400 got 400  {"ok":false,"error":"Field 'to' is missing or is not a valid email address."}
PASS to is null                                     expected 400 got 400  {"ok":false,"error":"Field 'to' is missing or is not a valid email address."}
PASS well-formed+authed -> reaches send             expected 500 got 500  {"ok":false,"error":"Error: invalid message-id"}

23 cases, 0 failed.
All cases matched. Zero 1101 responses: no case returned a Cloudflare error page.
```

Mapping to the acceptance criteria:

- **AC1** — no body, `{}`, non-JSON, malformed `raw`: all `400` with a JSON error envelope.
  Nine further `400` shapes beyond the four required. **Zero `1101` across all of them.**
- **AC2** — missing token and wrong token both `401`. The decisive case is
  `bad token + invalid body -> 401`: a `400` there would have proved the body was parsed
  before the caller was authenticated. It returns `401`, so parsing never ran.
- **AC3** — the well-formed authenticated request reaches `env.EMAIL.send()`, and a send
  failure surfaces as `500` with the envelope, not `1101`. See 5.1 for the `200` case.
- **AC4** — `GET /` → `200 {"ok":true}`, `GET /health` → `200 {"ok":true}`, `GET /nope` →
  `404`. No regression on what QA already verified.

### 5.1 The `200` path — a valid request really does complete the send

The matrix case above fails at `env.EMAIL.send()` with `invalid message-id`, which proves the
request was accepted and validated but not that a good one completes. So a MIME body carrying
a `Message-ID` was sent:

```bash
RAW=$(printf 'From: noreply@miautrix.tech\r\nTo: noreply@miautrix.tech\r\nMessage-ID: <mia76-probe@miautrix.tech>\r\nSubject: MIA-76 probe\r\n\r\nbody\r\n' | base64 -w0)
curl -s -w '\nstatus=%{http_code}\n' -X POST http://127.0.0.1:8799/send \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data "$(jq -n --arg r "$RAW" '{from:"noreply@miautrix.tech",to:"noreply@miautrix.tech",raw:$r}')"
```

```
{"ok":true}
status=200
```

Local runtime, so nothing left the machine.

### 5.2 Fail-closed with no secret

A second runtime was started from a copy of `src/` and `wrangler.jsonc` with **no**
`.dev.vars`, so `SEND_TOKEN` is genuinely unset:

```bash
curl -s -w '\nstatus=%{http_code}\n' -X POST http://127.0.0.1:8798/send \
  -H 'Authorization: Bearer anything' -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'
```

```
{"ok":false,"error":"Worker is not configured for authenticated sending."}
status=503
```

Same with no `Authorization` header at all → `503`. And the health probe is unaffected:

```
{"ok":true} 200
```

A missing secret refuses every send and never means "allow everyone", while the health check
keeps answering so an operator can still tell the Worker is up.

---

## 6. Rollback — demonstrated to the authorization boundary

The target still exists:

```bash
curl -s -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  ".../workers/scripts/miautrix-main-worker/versions/327baddf-2d9d-494f-9630-e383c4ca4aa4"
```

```
success: True
id: 327baddf-2d9d-494f-9630-e383c4ca4aa4
number: 6
created: 2026-09-21T19:33:11.539664Z source: api
handlers: ['email', 'fetch']
etag: b6582d3ac146eacda28478b87c91bbbf6b07b4fbc0c911a20c0336d4083c0165
```

The rollback command, run verbatim:

```bash
npx wrangler versions deploy 327baddf-2d9d-494f-9630-e383c4ca4aa4@100% \
  --name miautrix-main-worker --yes
```

```
├ Deploying 1 version(s)
✘ [ERROR] A request to the Cloudflare API
  (/accounts/ce330e96ff03d34dbe02ef88e18ea515/workers/scripts/miautrix-main-worker/deployments)
  failed.

  No access to the specified resource.
```

**What this does and does not prove.** It proves the target version exists and is intact, that
the command resolves the version and reaches the correct deployments endpoint, and that the
only thing between this command and a completed rollback is token scope. It does **not** prove
a completed traffic shift — that is impossible with a read-only token.

The honest statement of the current position: **version 6 is still live, so there is nothing
to roll back from.** Rollback becomes both executable and necessary in the same step as the
deploy, and both need the same `Workers Scripts: Edit` token. The sequence for whoever holds
it:

```bash
cd workers/email
# 1. secret, once per environment
npx wrangler secret put SEND_TOKEN --name miautrix-main-worker
# 2. deploy, one command
npx wrangler deploy
# 3. verify against the deployed URL; 200 because the destination is verified
SEND_TOKEN="$TOKEN" BASE=https://miautrix-main-worker.comarcat.workers.dev \
  WELL_FORMED_EXPECT=200 ./verify-send-contract.sh
# 4. if anything is wrong, roll back and re-verify
npx wrangler versions deploy 327baddf-2d9d-494f-9630-e383c4ca4aa4@100% \
  --name miautrix-main-worker --yes
curl -s https://miautrix-main-worker.comarcat.workers.dev/health   # expect {"ok":true}
```

Step 4 is the tested-shaped rollback: the same command already executed here, which failed
only on authorization.

---

## 7. Blast radius of the pending deploy

Stated before the change ships, per the blast-radius lens.

- **What changes.** Only `POST /send` behaviour. `GET /`, `GET /health` and `GET /nope` are
  byte-identical in effect, and the inbound `email()` handler is untouched.
- **What breaks if it goes wrong.** Outbound external mail for Cloudflare-mode domains. It is
  already fully broken — every send returns `1101` — so the realistic worst case is "no
  improvement", not "new outage". The one genuinely new failure mode is a `SEND_TOKEN`
  mismatch between the Worker secret and the app's `CLOUDFLARE_API_TOKEN`, which would turn
  `1101` into `401`. That is visible immediately in the queue's `last_error` and is fixed by
  setting the two to the same value.
- **Who notices.** The outbound queue dispatcher, which records `last_error` per attempt and
  retries with backoff; nothing is silently dropped. No inbound mail path is affected.
- **Ordering requirement.** The secret must be set **before** the deploy. A deploy without it
  answers `503` on every send rather than failing open — deliberate, but it is still an
  outage of a path that would otherwise work.

---

## 8. Platform coverage

| Target | Status |
| --- | --- |
| Linux (LXC / container) | **Tested.** Every command in this file ran on Linux. |
| Windows Server | **Untested.** `npx wrangler deploy` is Node-based and portable. `verify-send-contract.sh` is POSIX `sh`, so it needs WSL, Git Bash, or a PowerShell port — the one portability gap this change introduces. |
| Cloud / CI | **Untested.** Needs `CLOUDFLARE_API_TOKEN` (Edit scope) and `CLOUDFLARE_ACCOUNT_ID` as CI secrets. No other change expected. |

The Worker itself runs on Cloudflare's runtime, so the three-target parity question applies to
the deploy toolchain, not to the artefact.

---

## 9. What was deliberately not done

- **Inbound `email()` untouched** — the wildcard `indexOf` compare and the unroutable
  `inbox@corp` forward are both still there. Separate task (CF-04).
- **The unrestricted `EMAIL` `send_email` binding was not narrowed** (RSK-07). It is a
  behaviour change and Onyx's call.
- **Worker observability left off**, exactly as recovered. Noted, not bundled.
- **No DNS, no Email Routing, no `/api/` containment change.**
- **No real mail sent to a third party.** Every probe ran against a local runtime with no real
  send binding. End-to-end external delivery is QA's criterion and is gated on Email Routing
  rules that do not exist yet.
- **No app-side change.** `CloudflareApiMailTransport` already posted `from`/`to`/`raw` to
  `/send` with an `Authorization: Bearer` header; the Worker simply reads it now. The contract
  it depends on is unchanged, so modularity holds.
