# Windows installation and real-world testing

This guide installs Miautrix Mail v2.0 on Windows 11 or Windows Server 2022/2025. Replace every `<placeholder>` with your own values; do not publish credentials, keys, or message content in support requests.

## Prerequisites

- PostgreSQL 18 reachable from the Windows host. Use a dedicated database login with ownership of the application database. From `psql`, create the role and database interactively (the `\password` prompt avoids putting a password in shell history):

  ```sql
  CREATE ROLE <mail_user> LOGIN;
  \password <mail_user>
  CREATE DATABASE <mail_database> OWNER <mail_user>;
  ```

- A DNS zone you control. Point its MX record to `<mail-hostname>`, publish an SPF TXT record authorizing your sending path, publish the DKIM selector and public key generated/configured by the server, and publish a DMARC TXT record beginning with `v=DMARC1;` at `_dmarc.<your-domain>`.
- A PEM TLS certificate containing the full chain and a matching PEM private key for `<mail-hostname>`. Its SAN must cover that hostname; the installer uses the same certificate and key for Kestrel HTTPS and mail TLS. Restrict private-key access to Administrators and SYSTEM.
- Inbound TCP 25, 465, 587, 993 and HTTPS (443 by default) allowed by Windows Firewall, the router/cloud firewall, and the host network. The configurable HTTP port (5000 by default) is bound only to `127.0.0.1` for local verification and is never opened in Windows Firewall. Check your ISP: many residential providers block port 25, sometimes outbound and sometimes inbound. You may need a static IP and an SMTP relay.
- PowerShell 7, Python 3 for the IMAP probe, and a public DNS name resolving to the host.
- A ClamAV daemon accepting connections only on `127.0.0.1:3310`; the package uses this local TCP endpoint for malware scanning. Do not expose ClamAV's unauthenticated TCP port to other machines.

## Install

Download and extract one release zip. In PowerShell 7 running as Administrator, provide a connection string without a password; the installer reads that password through a secure prompt. It later invokes the CLI, which prompts twice for the initial admin password without echoing it.

```powershell
pwsh -File .\install.ps1 `
  -DbConnection 'Host=<postgres-host>;Port=5432;Database=<mail_database>;Username=<mail_user>' `
  -TlsCertPath 'C:\certificates\<mail-hostname>-fullchain.pem' `
  -TlsKeyPath 'C:\certificates\<mail-hostname>-private-key.pem' `
  -AdminEmail 'admin@<your-domain>' `
  -MailHostname '<mail-hostname>'
```

The migration bundle applies all EF migrations to an empty database. The seeder adds tenant/role bootstrap data but does not create a credential; on first install, `users bootstrap-admin` securely prompts for a password and creates an Argon2id credential, mailbox and owner membership, then writes an audit event. No default password is configured. Upgrades retain the existing admin credential. The app and mail data are stored under `C:\Program Files\Miautrix Mail` and `C:\ProgramData\Miautrix\mail` unless overridden.

Use `-WhatIf` to inspect planned actions without password prompts, database access, or machine changes. The services are `MiautrixMail` and `MiautrixMailWorker`. Admin UI is at `https://<mail-hostname>:<https-port>/admin/`; webmail is at `https://<mail-hostname>:<https-port>/` (HTTPS port defaults to 443). Kestrel serves HTTPS directly with the configured PEM certificate and key. HTTP is bound only to `127.0.0.1:<http-port>` for local verification and is not externally accessible. Production Windows responses include HSTS; use it only after confirming the hostname and certificate, because HSTS does not replace TLS certificate validation.

## Upgrade

Extract the newer complete release and run its `install.ps1` with the same database, certificate and admin-email parameters. The installer stops services, stages binaries, preserves the prior installation as `<install-dir>.previous`, migrates and verifies. On failure it restores the prior binaries and service environment and removes only firewall rules created in that run. If failure occurs after `efbundle.exe` has run, the database stays migrated (migrations are forward-only) while the old binaries are restored.

## Uninstall

Run the included `uninstall.ps1` as Administrator. It removes services, Windows Firewall rules and binaries; it preserves data and the database. To remove mail storage and backup archives, pass `-RemoveData` and type the exact confirmation. The database is never dropped.

## Logs

Check **Event Viewer → Windows Logs → Application** for Miautrix Mail/.NET events and **Windows Logs → System** for Service Control Manager failures. The package does not create file logs by default; mail blobs and backup archives are data, not logs.

## Real-world test checklist

- [ ] From an external account, send a message to the installed domain and confirm it arrives in the intended mailbox.
- [ ] Send outbound mail to an unrelated external domain; inspect SPF, DKIM and DMARC results at the recipient.
- [ ] Open webmail and verify a known HTML message renders safely; confirm remote images remain blocked unless explicitly enabled.
- [ ] Add the mailbox to Thunderbird using IMAP over SSL/TLS on 993 and SMTP submission on 587 with STARTTLS (or implicit TLS on 465).
- [ ] Set `MIAUTRIX_IMAP_HOST`, `MIAUTRIX_IMAP_USER`, and `MIAUTRIX_IMAP_PASSWORD` in a protected local environment, then run `python scripts/imap-external-validation.py`; its output reports checks only, not message bodies.
- [ ] Configure a mobile mail client with the same IMAP/SMTP endpoints and verify send, receive, folders and sent-mail placement.
- [ ] Restart Windows and confirm both services return to Running and HTTPS plus IMAPS respond; loopback HTTP should still respond locally.
- [ ] Record a folder's UIDVALIDITY and message UIDs, restart both services, reconnect, and confirm UIDVALIDITY and previously observed UIDs remain stable.

## Report a problem safely

Include the release ID, Windows edition/build, the failing checklist step, approximate UTC time, and redacted Event Viewer excerpts. Follow `docs/TROUBLESHOOTING.md` §12: redact public IPs/hostnames if desired, usernames, email addresses, tenant IDs, message IDs, database names, private filesystem paths, tokens, and message content. Never send `.env` files, connection strings with credentials, PEM/private-key material, passwords, cookies, raw message bodies, or unredacted authentication/protocol transcripts. Review diagnostic output locally before sharing it.
