# API authentication v2.2 report

## Implemented in this checkpoint

The web host now validates bearer access tokens against the stored SHA-256 token hash for `/api/**` requests. Session and refresh-token table models carry user, tenant, hashes, expirations and revocation/use timestamps; login creates session rows and stores only hashes. `IRequestContextAccessor` now reads identity from the session middleware and rejects conflicting legacy identity headers with 404. The migration `V22_ApiAuth_Sessions` adds the required columns idempotently to the existing tables.

## Compliance matrix

| Requirement | Status |
|---|---|
| Persist hashed access/refresh tokens | Partial: login persistence and schema implemented; rotation/reuse detection pending |
| Authenticate protected API requests | Partial: bearer validation is present; reviewed route allowlist and endpoint audit test pending |
| Session identity; no default user | Implemented for request context |
| Header mismatch 404 and audit | 404 implemented; audit entry pending |
| Admin permission and webmail role enforcement | Pending verification and tests |
| Logout/password change revocation | Pending |
| Client refresh and authenticated downloads | Pending |
| Security and reflection tests | Pending |

## Settings and defaults

Existing `SessionManager` defaults remain one hour for access tokens and 30 days for refresh tokens. The existing per-domain `SessionLifetimeMinutes` setting is not yet applied during issuance.

## Deploy notes

Apply the forward migration `V22_ApiAuth_Sessions`. Existing browser sessions are not migrated and users will need to log in again once the complete auth change is deployed. After deployment, verify on the server using loopback only: `curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:<api-port>/api/v1/...` without a token should return `401`. Do not probe the public hostname for this check.

## Troubleshooting draft

- If all API calls return 401, confirm the client sends `Authorization: Bearer <access-token>` and that the migration has been applied.
- If clients send `X-User-Id` or `X-Tenant-Id`, ensure each value matches the authenticated session or remove the header.
- Users may need to log in again after deployment because pre-deployment tokens have no persisted session row.

## Deferred

Refresh rotation, reuse revocation, logout and password-change revocation, complete anonymous route review, authorization auditing, frontend token refresh/download handling, and the requested test suites remain unfinished in this checkpoint. Build evidence: `dotnet build Miautrix.Mail.sln -warnaserror -m:1` succeeded with zero warnings and errors. No DB-backed migration test was run.
