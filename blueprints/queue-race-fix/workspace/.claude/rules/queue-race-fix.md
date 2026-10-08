# Queue Race Fix Rule

Every `smtp_queue` producer must assign exactly one direction: `Inbound` or `Outbound`.
Every dispatcher query must filter by that direction.
Do not add a fallback direction to hide missing call-site updates.
Do not change Cloudflare routing, DNS, WAF, token validation, antispam, or antimalware behavior as part of this fix.
The production inbound endpoint is already validated and must remain unchanged:
`https://mail.miautrix.tech/api/v1/inbound/cloudflare`
Schema changes must go through EF Core migrations only.
Migration must be additive and forward-only for production.
Queue direction is operational metadata. It is not sensitive, but raw message bodies, tokens, passwords, and secrets must never be logged.