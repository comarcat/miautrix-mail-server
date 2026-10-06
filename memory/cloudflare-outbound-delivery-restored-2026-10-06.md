# Cloudflare Outbound Delivery Restored — 2026-10-06

- Access remains enforced with Service Auth; no public bypass.
- The mail transport sends Access service-token headers and rejects redirects/HTTP 200 login pages.
- The Worker SEND_TOKEN was restored securely.
- Queue 27be5443-d163-47c4-9140-e8636d78564e was delivered and received externally.
- Secrets are never stored in repository memory.
