# API authentication v2.2 report

## Implementation

The Web host validates a bearer token against the persisted SHA-256 hash on every `/api/**` request, using a fixed-time comparison. It rejects missing, unknown, expired, idle, and revoked sessions with the standard 401 error envelope, updates `last_seen_at`, and attaches the session's user and tenant to request context. Identity headers are not trusted; a supplied tenant or user ID that differs from the session returns 404 and writes a failed authorization audit row.

Login persists only the access and refresh token hashes. Refresh rotates both tokens; reusing a consumed refresh token revokes the full session. Logout revokes the current session and its refresh tokens. Password changes revoke the user's other sessions while preserving the current session. The existing per-domain `SessionLifetimeMinutes` setting controls access lifetime when it is within 1–1440 minutes; otherwise access lifetime defaults to one hour. Refresh lifetime defaults to 30 days and idle timeout defaults to 30 days.

The middleware protects all API endpoints by default. `[AllowAnonymous]` is present only on login, refresh, the independently checked inbound Cloudflare webhook, and token-protected public calendar invitation routes. The reflection test inventories every controller HTTP action and fails if an action's anonymous metadata does not match that reviewed allowlist. Static frontends remain public; `/openapi/v1.json` is mapped only in Development. The current Web host has no health controller endpoint; future public routes can opt in with `[AllowAnonymous]` and must update the reflection allowlist.

Both browser API clients persist refresh tokens, send bearer authorization for API and binary requests, and retry one time after a 401 by rotating the session. If refresh fails, they clear local auth and dispatch the existing event that returns the app to login. They no longer send client-supplied identity headers. Webmail attachments and admin mailbox archives download through authenticated `fetch` and blob URLs.

## Compliance matrix

| Requirement | Status / evidence |
|---|---|
| Persist access and refresh token hashes only | Implemented in `sessions` and `refresh_tokens`; raw tokens are returned only at issuance |
| Authenticate protected API routes and reject invalid sessions | Implemented in session middleware; API integration tests cover protected categories and expired, idle, revoked sessions |
| Session-only request identity; no default admin fallback | Implemented; accessor tests verify missing context fails |
| Header mismatch gives 404 and audit record | Implemented and integration-tested |
| Anonymous routes reviewed and explicit | Implemented with `[AllowAnonymous]` plus an every-controller-action reflection test |
| Admin permission checks and webmail denial | Uses existing tenant authorization helper; webmail-role call to user admin API is integration-tested as 404 |
| Logout, refresh reuse, and password-change revocation | Implemented and integration-tested |
| Bearer token on browser API and binary/download calls | Implemented; both Vitest suites cover 401 refresh and download paths |
| RFC conformance | Not applicable; this package secures the HTTP API and does not change mail protocol behavior |

## Migration and deploy impact

Apply the forward-only, idempotent migration `V22_ApiAuth_Sessions`. It adds identity, hash, expiry, last-seen, revocation, and client-hash fields to the existing `sessions` and `refresh_tokens` tables, plus unique token-hash indexes. No production sessions are backfilled because the prior implementation did not persist issued sessions.

Every logged-in browser session is logged out once after deployment: pre-deployment access and refresh tokens have no corresponding persisted hashes, so requests fail and the client returns to login. No new environment variables are required. Existing session lifetime settings remain supported; invalid values fall back to the one-hour default.

On the server, verify with a loopback-only request (replace `<api-port>` and the path):

```bash
curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:<api-port>/api/v1/auth/me
```

Without a bearer token this must return `401`. Do not use the public hostname for this verification.

## Troubleshooting draft

- **All API requests return 401:** confirm the migration completed and that the client sends the access token as `Authorization: Bearer <token>`.
- **A browser returned to login after deployment:** expected once; sign in again so the server can persist the new session hashes.
- **Refresh returns 401:** the refresh token may be expired, already used, revoked, or from before this deployment; sign in again. Reuse revokes that session by design.
- **An API call returns 404 after adding identity headers:** remove `X-User-Id`/`X-Tenant-Id` or ensure they match the authenticated session; clients no longer need to send them.
- **A webmail user receives 404 from an admin route:** the session is authenticated, but its membership lacks the required admin permission.

## Verification evidence

- `dotnet build Miautrix.Mail.sln -warnaserror -m:1`: passed, zero warnings and errors.
- Unit tests: 125 passed; Security tests: 7 passed; Protocol tests: 38 passed.
- Targeted integration tests: 34 passed. API/auth and allowlist integration tests: 35 passed.
- Full integration run: 104 passed, 15 failed. The 15 failures match the known fixture failures documented in `ERRORS_AND_ISSUES.md` §13.18 (CLI environment, in-memory/PostgreSQL fixtures, inbound parser DI, and calendar missing Sent-folder fixtures); the API send fixture was repaired and now passes.
- Admin Vitest: 21 passed; Webmail Vitest: 20 passed. Admin and Webmail production builds succeeded.
- DB-backed integration verification used a uniquely named `miautrix_test_apiauth_*` database created from `MIAUTRIX_TEST_SERVER_CONNECTION` and dropped after the run; no shared or production database was migrated.

## Deferred

No protocol authentication behavior was changed. The known full-integration fixture failures above remain outside this package; local-browser end-to-end testing against an independently launched API was not performed in this worker run.
