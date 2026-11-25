# Smoke test v2.3 report

## Usage

Run from PowerShell 7 on an admin workstation with direct LAN routing to the server:

```powershell
./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname>
./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname> -Domain <mail-domain> -Json
```

`ServerIp` is the only network destination. `MailHost` is used for TLS target-name validation and HTTP Host; autoconfiguration checks use `autoconfig.<Domain>` and `mta-sts.<Domain>` as Host values. No DNS lookup should be needed. Defaults are HTTPS 443, IMAPS 993, SMTP 25/465/587, and a 10-second timeout. `-SkipCertificateCheck` warns and bypasses checks on each TLS connection; use it only for diagnostic runs. `-Credential` and `-SendTestTo` are reserved switches in this checkpoint and currently report SKIP.

## Checks and exit status

The script reports TCP connectability, implicit TLS on SMTP 465 and IMAPS, STARTTLS negotiation and unauthenticated AUTH policy on SMTP 25/587, IMAP greeting/capabilities, HTTPS `/api/v1/auth/me`, webmail and admin responses with HSTS, autoconfiguration XML, and MTA-STS policy syntax. IMAP4rev2 advertisement is reported in the IMAP detail. Every result includes a status, duration, and detail. Certificate expiry below 21 days is WARN and an expired certificate is FAIL. Exit code 0 means no FAIL, 1 means one or more checks failed, and 2 means invalid parameters or unsupported PowerShell.

## Troubleshooting draft

- TCP FAIL: verify listener, firewall, and LAN route for the reported port.
- TLS FAIL: inspect certificate chain/name and expiry for the public mail hostname; the script connects to the IP and uses the hostname as TLS target name.
- SMTP policy FAIL: ensure port 587 offers AUTH only after STARTTLS, and port 25 does not offer AUTH before TLS.
- HTTPS `/auth/me` returning 200 indicates an authentication regression; expected result is 401.
- Missing HSTS: inspect the TLS reverse proxy response headers.
- Autoconfig/MTA-STS failures: confirm nginx forwards the incoming Host header and discovery routes are configured for the mail domain.

## Tests and evidence

Run the Pester 5 suite with `Invoke-Pester ./scripts/tests/smoke-test.Tests.ps1`. The current tests exercise invalid IP, malformed hostname, port validation, and absence of hardcoded production addresses. They do not yet implement the requested local fake TCP/TLS/HTTP fixtures or PASS/FAIL protocol-family coverage. Build verification is `dotnet build Miautrix.Mail.sln -warnaserror`.

## Deferred work

Credential login/logout, authenticated IMAP EXAMINE, authenticated SMTP AUTH, and opt-in message submission are not implemented in this checkpoint. The Pester suite also lacks the required fake server families, positive protocol checks, exit-code coverage for network results, and credential redaction assertions. These are required before this package meets its acceptance criteria.
