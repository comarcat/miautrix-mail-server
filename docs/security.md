# Security Roadmap

The Admin Security page now shows real identity/security invariants where they exist today and persists domain-level MFA enforcement.

## Implemented now

- Password hashing algorithm and Argon2id parameters are read from the Identity implementation.
- Session lifetime is read from the session manager.
- Lockout threshold/duration is centralized in `LockoutOptions`.
- Domain MFA enforcement is persisted on the `domains.mfa_enforced` column.

## Future implementation items

### TLS policy controls

The TLS toggles in the Admin Security page should eventually be backed by runtime TLS configuration rather than local UI state.

Future work:

- Add a `TlsOptions` configuration object parsed and validated at boot.
- Expose current TLS protocol floors and cipher suites through the security settings API.
- Decide whether changes require daemon restart, hot reload, or a staged deployment.

### DANE / DNSSEC validation

DANE requires real TLSA lookup and DNSSEC validation in the mail transport pipeline.

Future work:

- Add outbound SMTP TLSA lookup support.
- Validate DNSSEC trust before treating TLSA results as authoritative.
- Record validation failures in security/audit logs without exposing cross-tenant resource existence.

### DKIM key rotation

The domain model already stores DKIM selector/public key data, but automated rotation is not implemented.

Future work:

- Add a worker job to generate replacement DKIM keys.
- Support overlapping selectors during rotation.
- Add DNS publication guidance or DNS provider integration.
- Record rotation history and failures.
