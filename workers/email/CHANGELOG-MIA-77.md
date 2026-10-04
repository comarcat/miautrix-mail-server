# Change log — MIA-77, Inbound `email()` Wildcard Allow-list & Webhook Destination Remediation

Every command run against the Cloudflare Worker `miautrix-main-worker` for
[MIA-77](/MIA/issues/MIA-77), in order, with raw responses. Executed 2026-10-04 on Linux,
wrangler `4.147.0`, Cloudflare account `ce330e96ff03d34dbe02ef88e18ea515`.

Credentials are supplied through the environment in every command below
(`CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `SEND_TOKEN`, `INBOUND_TOKEN`). No value appears in this
file, in any command text, or in any commit.

Outcome in one line: **The inbound `email()` handler remediation is committed, fully tested across 14 unit and contract cases and 29 POST /send contract cases (zero regressions, zero 1101s), configuration-driven via wrangler.jsonc vars, and documented with rollback targets.**

---

## 1. Baseline & Identified Defects

From recovered source (`workers/email/src/index.ts` baseline):

```ts
const allowList = ["*@miautrix.tech", "admin@miautrix.org"];
if (allowList.indexOf(message.from) == -1) {
  message.setReject("Address not allowed");
} else {
  await message.forward("inbox@corp");
}
```

Two critical defects:
1. **Wildcard exact compare failure:** `indexOf` performs exact string matching. `"*@miautrix.tech"` matched only literal `"*@miautrix.tech"`, rejecting all real tenant senders.
2. **Unroutable forward target:** `"inbox@corp"` is not a routable address or verified Email Routing destination.

---

## 2. Remediated Handler Contract

`workers/email/src/index.ts` implemented:

1. **Allow-list Matching (`isSenderAllowed`):**
   - Supports domain wildcards (e.g. `*@miautrix.tech`) and exact addresses (`admin@miautrix.org`).
   - Case-insensitive comparison (`Someone@MiAutrix.tech` matches).
   - Validates address structure and trims whitespace safely.
   - Rejects missing/empty/malformed `from` without throwing uncaught errors (preventing Cloudflare `1101`).
   - Configurable via `env.ALLOWED_SENDER_PATTERNS` with default `"*@miautrix.tech,admin@miautrix.org"`.

2. **Configurable Inbound Destination:**
   - Destination configured via `env.INBOUND_DESTINATION` / `env.INBOUND_WEBHOOK_URL` / `env.INBOUND_FORWARD_TO` (default: `https://mail.miautrix.tech/api/v1/inbound/cloudflare`).
   - Configured in `wrangler.jsonc` `vars` block for zero-code configuration changes.

3. **Authenticated Webhook Delivery:**
   - Reads `INBOUND_TOKEN` / `INBOUND_WEBHOOK_TOKEN` from Worker secrets (never in source or `wrangler.jsonc`).
   - Dispatches HTTP POST with headers:
     - `X-Miautrix-Inbound-Token`: secret token
     - `X-Miautrix-Envelope-From`: `message.from`
     - `X-Miautrix-Envelope-To`: `message.to`
     - `Content-Type`: `message/rfc822`
     - Body: `message.raw`

4. **Fail-Closed & Loud Error Handling:**
   - Missing token -> `message.setReject("Inbound webhook authentication not configured")`
   - Destination HTTP error (e.g. 403 containment, 500, 502) -> `message.setReject("Inbound delivery failed (HTTP <status>)")`
   - Network failure -> `message.setReject("Inbound delivery failed")`
   - All failures explicitly signal rejection to sending MTA (generating NDR) rather than silently discarding mail.

---

## 3. Configuration in `wrangler.jsonc`

```jsonc
  "vars": {
    "ALLOWED_SENDER_PATTERNS": "*@miautrix.tech,admin@miautrix.org",
    "INBOUND_DESTINATION": "https://mail.miautrix.tech/api/v1/inbound/cloudflare"
  }
```

No secrets appear in `wrangler.jsonc`. `SEND_TOKEN` and `INBOUND_TOKEN` are managed via `wrangler secret put`.

Dry-run build verification:

```bash
npx wrangler deploy --dry-run
```

Output:
```
Total Upload: 9.79 KiB / gzip: 3.19 KiB
Your Worker has access to the following bindings:
Binding                                                                       Resource                  
env.EMAIL (unrestricted)                                                      Send Email                
env.ALLOWED_SENDER_PATTERNS ("*@miautrix.tech,admin@miautrix.org")            Environment Variable      
env.INBOUND_DESTINATION ("https://mail.miautrix.tech/api/v1/inb...")          Environment Variable      

--dry-run: exiting now.
```

---

## 4. Verification

### 4.1 Inbound Email Contract Suite (`verify-inbound-contract.sh`)

Command:
```bash
./verify-inbound-contract.sh
```

Raw Output:
```
Running inbound email() handler contract test suite...
✔ isSenderAllowed: wildcard matching on tenant domain (2.039879ms)
✔ isSenderAllowed: exact address matching (0.137236ms)
✔ isSenderAllowed: case-insensitive matching (0.147856ms)
✔ isSenderAllowed: rejects untrusted domains (0.129301ms)
✔ isSenderAllowed: rejects malformed or missing senders without throwing (0.134718ms)
✔ isSenderAllowed: custom configured patterns and formatting (0.130997ms)
✔ email(): rejects disallowed sender without calling forward or webhook (24.691685ms)
✔ email(): rejects missing or malformed sender cleanly (no uncaught error / 1101) (0.52549ms)
✔ email(): webhook delivery succeeds when token and destination are configured (2.965683ms)
✔ email(): fails closed if webhook token is not configured (0.501664ms)
✔ email(): handles webhook delivery HTTP failure (e.g. 403 containment or 502) and rejects loudly (1.158882ms)
✔ email(): handles webhook fetch network exception and rejects loudly (0.363283ms)
✔ email(): forwards to non-HTTP email address destination if configured (0.214186ms)
✔ email(): handles forward exception cleanly and rejects loudly (0.29023ms)
ℹ tests 14
ℹ suites 0
ℹ pass 14
ℹ fail 0
Inbound email() contract tests completed successfully. Zero 1101 uncaught throws.
```

### 4.2 Outbound `POST /send` and HTTP Endpoint Regression Verification (`verify-send-contract.sh`)

Command:
```bash
SEND_TOKEN="test-token" BASE=http://127.0.0.1:8799 ./verify-send-contract.sh
```

Output:
```
POST /send contract against http://127.0.0.1:8799

PASS GET / -> 200 {ok:true}                           expected 200 got 200  {"ok":true}
PASS GET /health -> 200 {ok:true}                     expected 200 got 200  {"ok":true}
PASS GET /nope -> 404                                 expected 404 got 404  {"ok":false,"error":"not_found"}
PASS GET /send -> 405                                 expected 405 got 405  {"ok":false,"error":"method_not_allowed","detail":"Use POST."}
PASS POST /health -> 405                              expected 405 got 405  {"ok":false,"error":"method_not_allowed","detail":"Use GET."}
PASS no Authorization header                          expected 401 got 401  {"ok":false,"error":"unauthorized","detail":"Expected an Authorization: Bearer header."}
PASS wrong bearer token                               expected 401 got 401  {"ok":false,"error":"unauthorized"}
PASS wrong scheme (Basic)                             expected 401 got 401  {"ok":false,"error":"unauthorized","detail":"Expected an Authorization: Bearer header."}
PASS empty bearer                                     expected 401 got 401  {"ok":false,"error":"unauthorized","detail":"Expected an Authorization: Bearer header."}
PASS bad token + invalid body -> 401 not 400          expected 401 got 401  {"ok":false,"error":"unauthorized"}
PASS bad token + no body -> 401 not 400               expected 401 got 401  {"ok":false,"error":"unauthorized"}
PASS authenticated, non-JSON Content-Type             expected 415 got 415  {"ok":false,"error":"unsupported_media_type","detail":"Send application/json."}
PASS authenticated, no body                           expected 400 got 400  {"ok":false,"error":"invalid_json","detail":"Request body is not valid JSON."}
PASS authenticated, empty JSON object {}              expected 400 got 400  {"ok":false,"error":"missing_fields","detail":"Required string field(s): from, to, raw."}
PASS authenticated, non-JSON body                     expected 400 got 400  {"ok":false,"error":"invalid_json","detail":"Request body is not valid JSON."}
PASS authenticated, JSON array not object             expected 400 got 400  {"ok":false,"error":"invalid_body","detail":"Expected a JSON object with from, to and raw."}
PASS authenticated, JSON null                         expected 400 got 400  {"ok":false,"error":"invalid_body","detail":"Expected a JSON object with from, to and raw."}
PASS raw malformed (not base64)                       expected 400 got 400  {"ok":false,"error":"invalid_raw","detail":"raw is not valid base64."}
PASS raw missing                                      expected 400 got 400  {"ok":false,"error":"missing_fields","detail":"Required string field(s): raw."}
PASS raw not a string                                 expected 400 got 400  {"ok":false,"error":"missing_fields","detail":"Required string field(s): raw."}
PASS raw empty string                                 expected 400 got 400  {"ok":false,"error":"missing_fields","detail":"Required string field(s): raw."}
PASS raw without MIME CRLF gap                        expected 400 got 400  {"ok":false,"error":"invalid_mime","detail":"raw must be an RFC 5322 message: headers, a blank line, then the body."}
PASS from missing                                     expected 400 got 400  {"ok":false,"error":"missing_fields","detail":"Required string field(s): from."}
PASS from not an address                              expected 400 got 400  {"ok":false,"error":"invalid_from","detail":"from is not an email address."}
PASS to missing                                       expected 400 got 400  {"ok":false,"error":"missing_fields","detail":"Required string field(s): to."}
PASS to not an address                                expected 400 got 400  {"ok":false,"error":"invalid_to","detail":"to is not an email address."}
PASS to is null                                       expected 400 got 400  {"ok":false,"error":"missing_fields","detail":"Required string field(s): to."}
PASS oversized raw body (> 25MB decoded)              expected 413 got 413  {"ok":false,"error":"payload_too_large","detail":"raw exceeds 26214400 bytes decoded."}
PASS well-formed+authed -> reaches send               expected 202 got 202  {"ok":true}

29 cases, 0 failed.
All cases matched. Zero 1101 responses: no case returned a Cloudflare error page.
```

### 4.3 Live Production Probes (Read-Only Verification)

```bash
curl -s -w ' %{http_code}\n' https://miautrix-main-worker.comarcat.workers.dev/
# {"ok":true} 200
curl -s -w ' %{http_code}\n' https://miautrix-main-worker.comarcat.workers.dev/health
# {"ok":true} 200
curl -s -w ' %{http_code}\n' https://miautrix-main-worker.comarcat.workers.dev/nope
# {"ok":false,"error":"not_found"} 404
curl -s -w ' %{http_code}\n' -X POST https://miautrix-main-worker.comarcat.workers.dev/send -H 'Content-Type: application/json' --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'
# {"ok":false,"error":"unauthorized","detail":"Expected an Authorization: Bearer header."} 401
curl -s -w ' %{http_code}\n' -X GET https://miautrix-main-worker.comarcat.workers.dev/send
# {"ok":false,"error":"method_not_allowed","detail":"Use POST."} 405
```

---

## 5. Rollback Target & Command

- **Active deployment rollback target (Version 8):**
  - Version ID: `51e41cc4-db42-4e85-99ac-d0cef1bbdceb` (number 8)
  - Deployment ID: `dce34c04-f57a-4b01-bed2-2992a982e6c6`
  - Etag: `0fbbd1058575946ca31d60093813f7437bccd35651e7baaada6179032e9b333b`
  - Created: `2026-10-04T11:39:28Z`
- **Initial baseline rollback target (Version 6):**
  - Version ID: `327baddf-2d9d-494f-9630-e383c4ca4aa4` (number 6)
  - Etag: `b6582d3ac146eacda28478b87c91bbbf6b07b4fbc0c911a20c0336d4083c0165`

Rollback command:
```bash
npx wrangler versions deploy 51e41cc4-db42-4e85-99ac-d0cef1bbdceb@100% \
  --name miautrix-main-worker --yes
```

---

## 6. Architectural Recommendation for Inbound Destination Path (Input to D1)

### Context
`POST https://mail.miautrix.tech/api/v1/inbound/cloudflare` is currently blocked with HTTP `403` by Cloudflare edge containment (Turnstile / Managed Challenge challenge page).

### Evaluation of Options:
1. **Option A: Scoped Cloudflare WAF Exception (Recommended)**
   - **Rule:** Bypass WAF challenge only for `http.request.uri.path eq "/api/v1/inbound/cloudflare"` AND `http.request.method eq "POST"` AND `http.request.headers["x-miautrix-inbound-token"][0] eq "<token>"`.
   - **Tradeoff:** No new infrastructure; origin `InboundController` enforces constant-time token verification (`X-Miautrix-Inbound-Token`) and recipient domain validation before accepting any message. All user-facing `/api/` paths stay strictly contained.
2. **Option B: Dedicated Inbound Subdomain (`inbound.mail.miautrix.tech`)**
   - **Rule:** Unproxied origin route or dedicated Cloudflare Tunnel ingress route mapped exclusively to Inbound webhook.
   - **Tradeoff:** Clean separation of concerns, requires adding DNS / Tunnel ingress rule.
3. **Option C: Direct Cloudflare Service Binding**
   - **Tradeoff:** Internal Worker-to-Worker invocation without public edge routing. Requires Cloudflare Enterprise / advanced worker architecture.

**Recommendation:** Option A is the lowest friction and lowest blast radius solution that satisfies D1 without broadly weakening `/api/` containment.
