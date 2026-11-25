# Outbound v2.1 report

## Architecture

Outbound queue rows are dispatched according to the **sending domain's** `TransportMode`. `cloudflare` remains the existing default for miautrix.tech and uses the Cloudflare Worker API. `direct` selects the direct SMTP transport; `local` remains unchanged. The dispatcher signs with the sending domain's configured selector and private key before handing the same final message to either transport.

Direct delivery uses the `IDnsResolver` seam for MX, A/AAAA and TXT queries. MX records are tried by ascending preference and each MX name is resolved to addresses through the seam. When no MX exists, the domain's A/AAAA addresses are used. A null MX is treated as a permanent failure. SMTP uses port 25, an EHLO name, command/connect timeouts, dot-stuffed DATA and opportunistic STARTTLS with TLS 1.2 or 1.3. MTA-STS policy is discovered by `_mta-sts.<domain>` TXT, fetched over HTTPS, cached for its `max_age`, and applied to MX matching and TLS requirements in `enforce`; `testing` is logged without enforcement.

Permanent SMTP 5xx and permanent Cloudflare HTTP errors are marked permanent and dead-lettered immediately. 4xx, network errors, Cloudflare 408/429 and 5xx are retryable. Retry uses exponential backoff with jitter and the default five-day lifetime, configurable with `OUTBOUND_RETRY_LIFETIME_DAYS` (1–30). Existing queue `CreatedAt` timestamps define the lifetime; no migration or new queue field is needed.

Permanent failure and expiry produce an RFC 3464 `multipart/report` queued as inbound mail to the original sender's local active mailbox. The DSN uses an empty envelope sender (`<>`), contains human-readable and `message/delivery-status` parts, and omits the original message body. External senders and null reverse-path senders receive no DSN.

## Per-domain setup

In **Admin → Domains**, edit a domain and choose **Direct — this server delivers to recipient MX hosts**. The API accepts `transport_mode: "direct"`; no schema change is required. Set `OUTBOUND_EHLO_NAME` to the server's public DNS hostname. Optional `OUTBOUND_SMTP_CONNECT_TIMEOUT_SECONDS` and `OUTBOUND_SMTP_COMMAND_TIMEOUT_SECONDS` accept 1–300 seconds and default to 20 and 30.

Before enabling direct sending, the customer/operator needs:

- Outbound TCP port 25 permitted by the host, provider and firewall.
- A stable public sending IP with PTR/rDNS set to the server hostname; the hostname's A/AAAA should resolve back to that IP.
- An SPF TXT record authorizing the sending IP, for example `v=spf1 ip4:<sending-ip> -all` (merge into the domain's existing SPF record rather than publishing a second SPF record).
- A DKIM TXT record at `<selector>._domainkey.<domain>` containing the public key matching the configured per-domain private key.
- A DMARC TXT record at `_dmarc.<domain>` according to the customer's policy.

MTA-STS is honored when the recipient publishes `_mta-sts.<recipient-domain>` TXT with `v=STSv1; id=<policy-id>` and serves a valid policy at `https://mta-sts.<recipient-domain>/.well-known/mta-sts.txt`. Outbound DANE/TLSA validation is not implemented.

## Compliance and coverage matrix

| Requirement | Implementation / verification |
|---|---|
| Per-domain Cloudflare/direct mode | Existing domain settings API now recognizes `direct`; admin edit/create screens expose it. Integration tests verify direct and Cloudflare selection, direct API acceptance, and cross-tenant 404. |
| MX ordering and address fallback | DNS lookups go through `IDnsResolver`; MX ordering, failover, and no-MX A/AAAA fallback are tested against fake DNS and local sockets. |
| Null MX | Permanent failure (556); unit test covers the null-MX branch. |
| SMTP replies and connection failures | 4xx and network failures retry; 5xx are permanent. Local SMTP socket tests cover failover, recipient 450/550, no STARTTLS, and successful STARTTLS with TLS session EHLO restart. Cloudflare tests cover HTTP 400, 429 and 503 classes. |
| MTA-STS | Fetcher is injectable; TXT discovery and HTTPS policy fetch are isolated, policy is cached to `max_age`, enforce filters MX and requires valid TLS, testing logs without enforcement. Unit tests cover cache expiry, parser/matching, enforce MX mismatch and testing behavior. |
| DKIM | Existing `DkimService` signs outgoing data using the sender domain's selector/key before either transport. Existing DKIM headers are preserved so messages are not signed twice. Unit and DB-backed dispatcher tests verify the signature using the corresponding public key. |
| Retry lifetime | Exponential backoff with bounded jitter remains in use; lifetime defaults to five days and is configurable. A deterministic `TimeProvider` integration test checks immediate permanent failure and expiry; retry schedule/jitter bounds have unit coverage. |
| DSN | RFC 3464 multipart report queued through inbound delivery for permanent failure and expiry; null reverse-path, loop avoidance and local mailbox gating are covered in integration tests. |

## Test evidence

Final validation passed on 2026-11-24: `dotnet build Miautrix.Mail.sln -warnaserror -m:1` (0 warnings, 0 errors); UnitTests 121/121; SecurityTests 7/7; ProtocolTests 28/28; targeted IntegrationTests 29/29 (`Category=Cloudflare|FullyQualifiedName~SenderAuthorizationWiring|FullyQualifiedName~MessageUidAllocator|Category=Smtp`); admin Vitest 18/18. Tests use fake DNS and local in-process SMTP sockets; the database-backed filters use the configured non-production test database. No live external SMTP, public DNS lookup, production database, or deployment was used. No migration was added because retry lifetime uses the existing queue `CreatedAt` timestamp.

## Limitations

- DANE (RFC 7672) is not implemented.
- DSNs are deliberately limited to active local sender mailboxes. External senders are suppressed because outbound queue rows do not persist an explicit authenticated-submission actor; local mailbox ownership is the available authorization evidence.
- RFC 3461 DSN ESMTP parameters are not advertised or sent.
- Queue rows remain one recipient each; recipients for the same domain are delivered independently.
- This implementation does not certify public DNS, PTR/rDNS, provider port-25 policy, or customer SPF/DKIM/DMARC publication.
