# Sieve v2.2 implementation report

## Implemented

Scripts are compiled and executed for accepted inbound delivery, per mailbox, before folder selection. Scripts are stored per mailbox with one active script enforced by a unique partial index; webmail provides a script list/editor and an out-of-office form that emits a vacation script. REST endpoints provide list/get/create/update/activate/deactivate/delete and return compile diagnostics with line and column. Authorization uses the tenant authorization helper and maps cross-tenant mailbox access to not found.

Delivery supports folder routing (including create), flags, discard, copy, redirects, reject DSNs and vacation replies. Runtime exceptions and execution budgets fall back to implicit keep and log only script identifiers and execution metadata. Vacation dedupe defaults to seven days and clamps to 1–30; null senders, automated/list mail and own addresses are suppressed. Redirects are capped at five, preserve the envelope sender and carry loop markers. This version does not apply SRS; forwarding may fail SPF at the recipient.

## RFC compliance matrix

| RFC | Coverage | Status / limits |
|---|---|---|
| 5228 core | `require`, conditional control, keep/discard/redirect, header/address/envelope/exists/size/boolean tests, comparators, match types, address parts, lists, comments and `text:` | Implemented in compiler/interpreter; extension requirements are checked at compile time. |
| 5229 | `body` | Not implemented; optional and deferred. |
| 5230 | vacation tags, suppression, null envelope sender, response headers and dedupe | Implemented; `:mime` handling is not complete. |
| 5232 | set/add/remove/hasflag and flags on keep/fileinto | Implemented for delivery storage. |
| 5429 | reject / ereject with DSN | Implemented after acceptance; null envelope senders are not sent a DSN. |
| 3894 | `:copy` | Implemented for fileinto and redirect queueing. |
| 5490 | `:create`, `mailboxexists` | Implemented. |
| 3834 | vacation reply suppression and response headers | Implemented for listed automated/list traffic and own addresses. |

## Migration and settings

Migration `V22_Sieve_Scripts` creates the vacation dedupe table and active-script partial unique index. It deactivates any pre-existing duplicate active scripts by retaining the latest updated/created record before creating the constraint. No new environment settings were added. Deploy by applying the migration before enabling the Worker and Web changes; no DNS or nginx changes are required.

## Troubleshooting draft

- A script rejected by the editor includes its source line and column; fix the reported syntax or unsupported `require` and save again.
- Delivery runtime errors and budget exhaustion use implicit keep. Worker logs include the script identifier and execution counts, never message bodies.
- A `fileinto` destination is kept in the default folder if it does not exist and the script omitted `:create`.
- Redirect SPF alignment is not rewritten with SRS in this version; recipients can reject forwarded mail when SPF fails.

## Test evidence

- Sieve parser/interpreter unit tests and migration/API/delivery integration tests were added in their respective commits; migration coverage uses a throwaway PostgreSQL database.
- Delivery integration coverage exercises routing, create, discard, flags, redirect guard, reject, vacation dedupe/suppression, runtime fallback and submission bypass.
- Webmail Vitest: 20 passed; webmail production build passed.
- Full .NET suite evidence is pending the final verification run.

## Deferred

RFC 5173 body tests and complete vacation `:mime` serialization remain deferred. The report should be updated with final full-suite counts before release.
