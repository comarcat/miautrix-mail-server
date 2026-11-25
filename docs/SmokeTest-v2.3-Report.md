# Smoke test v2.3 report

## Usage

PowerShell 7 or later, run from an administrator workstation with direct LAN access:

```powershell
./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname>
./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname> -Domain <mail-domain>
./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname> -Domain <mail-domain> -Credential (Get-Credential) -TotpCode <totp-code> -Json
./scripts/smoke-test.ps1 -ServerIp <server-ip> -MailHost <mail-hostname> -Credential (Get-Credential) -SendTestTo <recipient-address>
```

`ServerIp` must be a literal IPv4 or IPv6 address and is the only network destination. `MailHost` is used as TLS SNI/name and the HTTPS Host header, never resolved by the script. Discovery requests use `autoconfig.<Domain>` and `mta-sts.<Domain>` as Host values. The defaults are HTTPS 443, IMAPS 993, SMTP 25/465/587, and a 10 second timeout. `-SkipCertificateCheck` downgrades certificate trust/name and expiry failures to WARN; expired certificates still include the days elapsed in the detail. `-SendTestTo` requires `-Credential` and submits one short plain text message.

## Checks

| Check | Meaning |
|---|---|
| TCP reachability | Each configured HTTPS, IMAPS, and SMTP port accepts a connection. |
| TLS 465 and IMAPS | TLS 1.2 or later, certificate name/chain and expiry; under 21 days warns, expired fails. |
| SMTP 465 greeting/EHLO | Implicit TLS SMTP greeting and EHLO response. |
| SMTP 25/587 | Greeting, EHLO, STARTTLS; AUTH must be absent before TLS on 587 and absent without TLS on 25; 587 must advertise AUTH after TLS. |
| IMAP greeting/CAPABILITY | Implicit TLS greeting and capabilities; IMAP4rev2 advertisement is included in the detail. |
| HTTPS routes | Unauthenticated `/api/v1/auth/me` must return 401; webmail and admin must load; HSTS must be present. |
| Thunderbird autoconfig | With `-Domain`, XML must name IMAP and SMTP. |
| MTA-STS | With `-Domain`, validate the policy fields; 404 is INFO for an absent/none policy. |
| Credential checks | API login, bearer `/me`, logout and revoked-token check; IMAP AUTH and read-only EXAMINE; SMTP AUTH after STARTTLS and QUIT. MFA without a supplied code is SKIP with guidance. |
| Test message | With `-SendTestTo`, submit one message and report the server's 250 acceptance response. |

Every result has PASS, WARN, FAIL, INFO, or SKIP, duration, and detail. JSON mode emits the result array. Passwords, AUTH PLAIN base64, API tokens and refresh tokens pass through centralized masking before stored output.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | No check failed. |
| 1 | At least one check failed. |
| 2 | Invalid parameters or PowerShell version below 7. |

## Troubleshooting draft

- TCP FAIL: inspect the listener, host firewall, and LAN route for that port.
- TLS FAIL: verify that the certificate chain is trusted, the name matches `MailHost`, and the certificate has not expired. Use `-SkipCertificateCheck` only to isolate a certificate issue.
- SMTP policy FAIL: port 587 should reveal AUTH only after STARTTLS; port 25 must not offer AUTH without TLS.
- `/auth/me` returning 200 without a token indicates the authentication regression the probe is designed to catch.
- HSTS missing: inspect the HTTPS reverse proxy response headers.
- Discovery FAIL: check nginx's forwarded Host header and the per-domain autoconfiguration/MTA-STS configuration.
- Authenticated IMAP FAIL: verify the mailbox credentials and read-only INBOX access; application passwords may be required by server policy.

## Test evidence and remaining work

`scripts/tests/SmokeTestFakes.psm1` provides threaded local SMTP, IMAP, and HTTPS fakes plus in-memory `CertificateRequest` test certificates. `-TestTrustedCertificateThumbprint` and `-TestSmtp25Port`/`-TestSmtp465Port`/`-TestSmtp587Port` are hidden test hooks; overrides are rejected unless `-ServerIp` is loopback. They let tests trust only the selected fake cert and assign random ports without changing production defaults.

The Pester cases cover:

- parameter failures and loopback-only test hook enforcement (exit 2);
- complete local happy path and JSON field shape (exit 0);
- anonymous `/auth/me` returning 200 and SMTP 587 AUTH before STARTTLS (each FAIL, exit 1);
- expired and wrong-name certificates (FAIL), certificate skip behavior (WARN), and a certificate expiring in 10 days (WARN);
- IMAP4rev2 present and absent capability details;
- API login/me/logout/revocation, IMAP AUTH + EXAMINE, SMTP AUTH, message submission with `TEST123`, and MFA-required login (SKIP);
- autoconfig XML, valid MTA-STS, and missing MTA-STS (INFO);
- capture of combined PowerShell streams 1–6 and JSON to ensure the fake password, AUTH PLAIN base64, and access/refresh tokens do not appear.

The suite passed **19 tests, 0 failed** on each of two consecutive runs with the available Pester 6.2.0 runner. Pester 5 was not installed in this environment; the suite uses Pester 5-compatible syntax. `dotnet build Miautrix.Mail.sln -warnaserror` succeeded with zero warnings.
