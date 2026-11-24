# Miautrix Mail Server for Windows

This archive installs the v2.0 server as two Windows services and includes its admin and webmail interfaces. It is intended for Windows 11 or Windows Server 2022/2025.

## Before installing

- Install PostgreSQL 18 and create a dedicated database and login. Allow the mail server host to connect, and keep the database password ready for the secure prompt.
- Point your domain's MX record at the public mail host. Publish SPF, DKIM and DMARC records after configuring the domain in the admin console.
- Obtain a PEM certificate containing the full chain and a matching PEM private key for the mail hostname. The certificate SAN must cover that hostname; Kestrel uses this pair for public HTTPS as well as the mail TLS services. Restrict access to the key file to Administrators and SYSTEM.
- Run ClamAV with its daemon bound to `127.0.0.1:3310`; the package uses this local TCP endpoint for malware scanning. Keep the ClamAV port inaccessible from other machines.
- Allow inbound TCP 25, 465, 587, 993 and HTTPS (443 by default) through the host firewall and any network firewall. The configurable HTTP port (5000 by default) is loopback-only and is never opened in the firewall. Many residential ISPs block outbound or inbound TCP 25; use a relay-capable connection if so.
- Configure an A/AAAA record for the mail hostname and ensure it resolves to this server.

## Install

Extract the zip, open PowerShell 7 as Administrator, then run the included installer. The connection string must omit `Password`; the installer prompts for that value securely.

```powershell
Set-Location C:\Path\To\Extracted\miautrix-mail
pwsh -File .\install.ps1 `
  -DbConnection 'Host=<postgres-host>;Port=5432;Database=<database>;Username=<database-user>' `
  -TlsCertPath 'C:\certs\mail-fullchain.pem' `
  -TlsKeyPath 'C:\certs\mail-private-key.pem' `
  -AdminEmail 'admin@<your-domain>' `
  -MailHostname 'mail.<your-domain>'
```

The installer prompts for the PostgreSQL password, applies every EF migration with `efbundle.exe`, and seeds roles and mailbox folders without creating a password. On first install, it invokes the CLI, which securely prompts twice for the initial admin password; the password is never accepted as an argument. The CLI creates the admin credential, mailbox and owner membership, and records an audit event. Use a password of at least eight characters. Upgrades retain the existing admin credential and do not prompt for a replacement. The installer creates `MiautrixMail` and `MiautrixMailWorker` with delayed automatic start and restart-on-failure, writes service environment settings in protected service registry keys, and opens the mail and HTTPS firewall ports.

For a non-mutating preflight, add `-WhatIf`; it validates package inputs and prints the planned actions without prompting for secrets or contacting PostgreSQL.

Admin UI: `https://<mail-hostname>/admin/` (or `https://<mail-hostname>:<https-port>/admin/` for a non-default port). Webmail: `https://<mail-hostname>/`. HTTPS defaults to port 443; the matching PEM certificate and key are configured directly in Kestrel. Plain HTTP is bound only to `127.0.0.1:<http-port>` (default 5000) for local verification and is not reachable remotely. Production Windows responses include HSTS; only rely on HSTS after the hostname and certificate are correct, since HSTS does not replace TLS certificate validation.

## Upgrade

Use a newer complete release zip and run its `install.ps1` with the same parameters. It stops the services, stages the new binaries, saves the former installation as `<install-dir>.previous`, migrates the database, and verifies the services and local listeners. If installation fails, it restores the prior binaries and service environment and removes only firewall rules created during that run. If failure occurs after `efbundle.exe` has run, the database remains migrated (migrations are forward-only) while the old binaries are restored.

## Logs and data

Check **Event Viewer → Windows Logs → Application** for Miautrix Mail and .NET runtime events, and Windows Service Control Manager events for startup failures. Mail storage and backup archives are under `C:\ProgramData\Miautrix\mail` by default. The installer does not create a `.env` file.

## Uninstall

Run `uninstall.ps1` from PowerShell 7 as Administrator. It removes both services, firewall rules and installed binaries while retaining the database and data directory. To remove local mail storage and backups as well, pass `-RemoveData` and type the exact confirmation shown by the script. The database is never dropped by the uninstaller.

## Troubleshooting

Check that the services have the expected PostgreSQL connection and PEM paths in their protected environment, that the certificate and key match, and that the configured ports are not already occupied. Do not send passwords, private keys, message bodies, session cookies, or unredacted connection strings in a support report.
