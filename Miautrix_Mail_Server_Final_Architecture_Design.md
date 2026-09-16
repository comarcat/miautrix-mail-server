# Miautrix Mail Server
## Final Architecture & Software Design Specification

**Document status:** Architecture Review Draft  
**Version:** 1.0  
**Date:** 2026-09-03  
**Author:** Miautrix / Cristobal Arboleda  
**Target platform:** Windows Server, Linux, Debian, Proxmox, Docker/OCI  
**Primary technology:** C# / .NET 10 LTS  
**Database:** PostgreSQL  
**Frontend:** React + TypeScript  

---

## 1. Executive Summary

Miautrix Mail Server is a new secure, modular mail-server platform written in C#/.NET.

The goal is to provide small and medium organizations, IT departments, MSPs, and service providers with a modern mail platform combining:

- SMTP, IMAP, JMAP and optional POP3S
- Secure-only protocols
- Webmail
- Rich administrative GUI
- Local accounts by default
- MFA
- Google Workspace integration
- Microsoft Entra ID integration
- LDAP / Active Directory integration
- Mail-flow rules
- Graphical mail-flow designer
- Restricted SMTP relay policies for printers/scanners/copiers
- Native anti-spam
- Third-party anti-spam integrations
- Malware-scanning provider abstraction
- SPF, DKIM, DMARC and ARC
- Centralized logging and security analytics
- Backup and restore
- Near-zero-downtime updates
- Plugin architecture
- Multi-tenancy readiness
- Mailbox-based licensing
- API-first administration

The recommended starting architecture is a **modular monolith**, not microservices. Clear module boundaries and provider interfaces must allow components to be extracted into services later if scale requires it.

The architecture must prioritize security, operational simplicity, observability, maintainability, upgradeability, and deterministic mail flow.

---

## 2. Product Vision

Miautrix Mail Server should provide an experience conceptually similar to Microsoft Exchange for organizations that want:

1. A single mail platform.
2. Strong administrative control.
3. Flexible mail-flow rules.
4. Modern authentication.
5. Integration with existing identity systems.
6. Strong security controls.
7. Simple deployment and maintenance.
8. Easy backup and disaster recovery.
9. Extensibility without modifying the core product.

The platform should support:

- Windows Server
- Linux
- Debian
- Proxmox VM
- Proxmox LXC where operationally appropriate
- Docker/OCI containers

---

## 3. Core Architectural Principles

### 3.1 Security First

Security-sensitive functionality must be designed into the architecture rather than added later.

Requirements include:

- TLS by default
- No plaintext authentication
- No accidental open relay
- MFA support
- Strong password hashing
- Application passwords for legacy mail clients
- Rate limiting
- Brute-force protection
- IP restrictions
- Country restrictions where configured
- Device-specific policies
- Audit trails
- Secure secrets storage
- Signed updates
- Signed licenses
- Principle of least privilege

### 3.2 Modular Design

Each major subsystem should have a clearly defined interface.

Examples:

- `IIdentityProvider`
- `ISpamProvider`
- `IMalwareProvider`
- `IMailStorage`
- `IGeoIpProvider`
- `IQueueProvider`
- `IAuthenticationProvider`
- `IReportingProvider`

### 3.3 API First

Administrative functions should be available through APIs.

The GUI should consume the same application APIs available to:

- Administrators
- MSP tools
- Automation
- PowerShell
- External ticket systems
- Monitoring platforms
- Future mobile applications

### 3.4 Configuration as Data

Important configuration should be stored in PostgreSQL and exposed through the administrative API.

Examples:

- Domains
- Users
- Mailboxes
- Identity providers
- Mail-flow rules
- Device policies
- Anti-spam providers
- DKIM configuration
- Retention
- Backup schedules
- Licensing
- Security policies

### 3.5 Provider Abstraction

External systems must never become hard dependencies of the core mail engine.

```text
Core Mail Engine
      |
      +---- ISpamProvider
      |       +---- NativeSpamProvider
      |       +---- RspamdProvider
      |       +---- SpamAssassinProvider
      |       +---- RestApiSpamProvider
      |
      +---- IMalwareProvider
      |       +---- ClamAVProvider
      |       +---- RestApiMalwareProvider
      |
      +---- IIdentityProvider
              +---- LocalIdentityProvider
              +---- GoogleWorkspaceProvider
              +---- EntraIdProvider
              +---- ActiveDirectoryProvider
```

---

## 4. High-Level Architecture

```text
                         MIAUTRIX MAIL SERVER
                                  |
        +-------------------------+-------------------------+
        |                         |                         |
      WEBMAIL                   ADMIN                  API / JMAP
        |                         |                         |
        +-------------------------+-------------------------+
                                  |
                         +--------v--------+
                         | IDENTITY & MFA  |
                         |-----------------|
                         | Local           |
                         | Google          |
                         | Entra ID        |
                         | LDAP / AD       |
                         | TOTP            |
                         | WebAuthn        |
                         +--------+--------+
                                  |
                         +--------v--------+
                         | POLICY ENGINE   |
                         +--------+--------+
                                  |
                +-----------------+-----------------+
                |                 |                 |
               SMTP              IMAP             JMAP
                |                 |                 |
                +-----------------+-----------------+
                                  |
                         +--------v--------+
                         | MAIL FLOW       |
                         | ENGINE          |
                         +--------+--------+
                                  |
                         +--------v--------+
                         | SECURITY ENGINE |
                         |-----------------|
                         | SPF             |
                         | DKIM            |
                         | DMARC           |
                         | ARC             |
                         | Anti-Spam       |
                         | Anti-Malware    |
                         +--------+--------+
                                  |
                         +--------v--------+
                         | QUEUE / DELIVERY|
                         +--------+--------+
                                  |
                +-----------------+------------------+
                |                 |                  |
                v                 v                  v
           PostgreSQL       Mail/Object Storage   SMTP Delivery
                |
                v
       Diagnostics / Audit / Analytics
```

---

## 5. Recommended Technology Stack

### Backend

- C#
- .NET 10 LTS
- ASP.NET Core
- Entity Framework Core
- Dependency Injection
- Background Services
- Options pattern
- Minimal APIs and/or Controllers

### Database

- PostgreSQL

PostgreSQL should contain metadata and application state, not large raw MIME objects by default.

### Frontend

- React
- TypeScript
- Tailwind CSS
- shadcn/ui
- React Flow or equivalent graphical workflow library

### Logging

- Serilog
- OpenTelemetry
- PostgreSQL event store
- Windows Event Log
- Linux journald/syslog

### Scheduling

- Quartz.NET or Hangfire

### Caching

Optional Redis. Redis should not be a mandatory dependency for the first release.

### Storage

Storage abstraction supporting:

- Local filesystem
- NAS
- SMB
- NFS
- S3
- MinIO
- Azure Blob Storage

---

## 6. Solution Structure

```text
Miautrix.Mail.sln

/src
  Miautrix.Mail.Core
  Miautrix.Mail.Domain
  Miautrix.Mail.Application
  Miautrix.Mail.Infrastructure
  Miautrix.Mail.Persistence

  Miautrix.Mail.Protocols.SMTP
  Miautrix.Mail.Protocols.IMAP
  Miautrix.Mail.Protocols.POP3
  Miautrix.Mail.Protocols.JMAP
  Miautrix.Mail.Protocols.Sieve

  Miautrix.Mail.Identity
  Miautrix.Mail.Security
  Miautrix.Mail.AntiSpam
  Miautrix.Mail.AntiMalware
  Miautrix.Mail.MailFlow
  Miautrix.Mail.Storage
  Miautrix.Mail.Search
  Miautrix.Mail.Queue

  Miautrix.Mail.Web
  Miautrix.Mail.Admin
  Miautrix.Mail.Worker

  Miautrix.Mail.Diagnostics
  Miautrix.Mail.Reporting
  Miautrix.Mail.Licensing
  Miautrix.Mail.Plugins

/tests
  Unit
  Integration
  Protocol
  Security
  AntiSpam
  Performance
  EndToEnd
```

---

## 7. Architectural Style

The recommended initial architecture is:

**Modular Monolith + Clean Architecture + domain-driven module boundaries**

```text
Presentation
    |
Application
    |
Domain
    |
Infrastructure
    |
Persistence
```

Protocol modules should interact with the application layer through defined commands/services.

---

## 8. Network Services and Protocols

| Service | Port | Security |
|---|---:|---|
| HTTPS / Webmail / API | 443 | TLS |
| SMTP Submission | 465 | Implicit TLS |
| SMTP Submission | 587 | STARTTLS + Authentication |
| SMTP Server-to-Server | 25 | TLS according to policy |
| IMAPS | 993 | TLS |
| POP3S | 995 | TLS, optional |
| ManageSieve | 4190 | TLS |

Plaintext ports 110 and 143 should be disabled.

SMTP authentication should never be accepted without encryption.

TLS 1.3 should be preferred, with TLS 1.2 as the minimum supported version where compatibility requires it.

---

## 9. Identity Architecture

### 9.1 Local Accounts

Local accounts are the default.

Capabilities:

- Username/password
- Password expiration policies
- Account lockout
- TOTP
- Recovery codes
- Application passwords
- Role assignment
- Session management

### 9.2 Google Workspace

Support:

- OAuth/OIDC authentication
- Google Workspace directory integration
- Group membership
- Configurable allowed group
- Configurable administrator group
- Optional user synchronization

The system must not automatically authorize every Google Workspace account.

```text
Google Workspace
       |
       v
Miautrix-Mail-Users
       |
       v
Authorized Miautrix Mail User
```

### 9.3 Microsoft Entra ID

Support:

- OIDC/OAuth2
- Microsoft Graph
- Group membership
- Configurable allowed group
- Configurable administrator group
- Optional synchronization

### 9.4 LDAP / Active Directory

Support:

- LDAP
- LDAPS
- Base DN
- User search
- Group search
- Allowed group
- Administrator group
- Optional synchronization

### 9.5 Recommended Groups

```text
Miautrix-Mail-Users
Miautrix-Mail-Admins
Miautrix-Mail-Helpdesk
Miautrix-Mail-Auditors
Miautrix-Mail-Restricted
```

---

## 10. Authentication and Authorization

Authentication and authorization must remain separate.

```text
Authentication
      |
      v
Who are you?
      |
      v
Identity
      |
      v
Authorization
      |
      v
What are you allowed to do?
```

A user authenticated through an external provider should still be evaluated against Miautrix roles and policies.

---

## 11. MFA

MFA must support:

- TOTP
- Google Authenticator
- Microsoft Authenticator
- Recovery codes
- Application passwords
- WebAuthn/passkeys roadmap
- OIDC authentication

Traditional mail clients cannot normally perform interactive MFA.

Therefore:

```text
Webmail / Admin
      |
      +-- Password
      +-- TOTP
      +-- WebAuthn
      +-- OIDC

SMTP / IMAP legacy clients
      |
      +-- Application Password
```

---

## 12. Security Event Codes

Examples:

```text
AUTH-4001   Invalid password
AUTH-4010   Invalid TOTP
AUTH-4011   MFA timeout
AUTH-4020   Application password rejected
AUTH-4030   Unauthorized IP
AUTH-4031   Country restriction
AUTH-4040   Unknown device
AUTH-4050   Passkey validation failure
AUTH-4060   Identity provider failure
```

---

## 13. Mail Flow Architecture

### Inbound

```text
Internet
   |
   v
SMTP Connection
   |
   v
Connection Policy
   |
   v
IP Reputation
   |
   v
TLS
   |
   v
SPF
   |
   v
DKIM
   |
   v
DMARC
   |
   v
Mail Flow Rules
   |
   v
Anti-Spam
   |
   v
Anti-Malware
   |
   v
Quarantine / Delivery
```

### Outbound

```text
Mailbox
   |
   v
Authentication
   |
   v
Outbound Rules
   |
   v
Rate Limiting
   |
   v
DLP / Security
   |
   v
DKIM Signing
   |
   v
SMTP Queue
   |
   v
Internet
```

---

## 14. Mail Flow Rule Engine

### Conditions

- Sender
- Recipient
- Domain
- Source IP
- Destination IP
- Subject
- Header
- Message size
- Attachment
- File extension
- Authentication state
- User
- Group
- Device
- Country
- SPF result
- DKIM result
- DMARC result
- Spam score
- Malware verdict
- TLS state
- Time/date

### Actions

- Allow
- Reject
- Quarantine
- Redirect
- Forward
- Add header
- Remove header
- Modify subject
- Change priority
- Rate limit
- Require TLS
- Require authentication
- Apply spam policy
- Apply malware policy
- Log
- Notify administrator

---

## 15. Graphical Mail Flow Designer

The administrative GUI should include a visual workflow editor.

```text
+------------------------------------------------+
| New Mail Flow Rule                             |
+------------------------------------------------+
| WHEN                                           |
|                                                |
| [ Source IP ] [ is ] [ 10.20.30.50 ]          |
|                                                |
| AND                                            |
|                                                |
| [ Sender ] [ is ] [ printer@company.com ]     |
|                                                |
| ACTION                                         |
|                                                |
| [ Allow ]                                      |
| Recipients: Internal only                     |
| TLS: Required                                  |
| Rate: 20 messages/hour                         |
|                                                |
| [ Test Rule ]                    [ Save ]      |
+------------------------------------------------+
```

Support:

- Drag and drop
- Conditions
- Actions
- Branching
- Priority
- Enable/disable
- Rule testing
- Simulation
- Import/export
- Templates
- Rule versioning
- Change history

---

## 16. Mail Flow Simulator

Administrators should be able to test rules without sending mail.

Input:

```text
Source IP
Sender
Recipient
Subject
Authentication state
Device
Country
```

Output:

```text
Rule #1   PASS
Rule #4   PASS
Rule #8   BLOCK
Rule #12  NOT EVALUATED
```

The simulator should explain the final decision.

---

## 17. Mail Flow Explorer

Search by:

- Message ID
- Sender
- Recipient
- Subject
- Timestamp
- Source IP
- User
- Mailbox
- Rule
- Verdict

Example:

```text
SMTP Connection
      |
      v
Source IP: 10.20.30.50
      |
      v
TLS: PASS
      |
      v
Device Policy #12
      |
      v
Mail Flow Rule #21
      |
      v
Anti-Spam: CLEAN
      |
      v
DKIM: PASS
      |
      v
Delivery
```

For failed messages, explain:

- Rejected
- Quarantined
- Blocked
- Delayed
- Rate limited
- Routed
- Delivered

---

## 18. Restricted Device Relay

Printers, scanners, copiers and other low-security devices should have dedicated policies.

```text
+--------------------------------+
| SMTP Relay Policy              |
+--------------------------------+
| Authentication: IP/TLS        |
| TLS: Required                  |
| Sender: printer@company.com    |
|                                |
| Allowed recipients:            |
|   @company.com                 |
|                                |
| Internet recipients: NO       |
| Max messages/hour: 20          |
+--------------------------------+
```

Policies should support:

- IP authentication
- Certificate authentication
- SMTP authentication
- TLS requirement
- Sender restrictions
- Recipient restrictions
- Internal-only delivery
- Rate limits
- Maximum message size
- Attachment restrictions
- Logging

The system must include explicit open-relay protection.

---

## 19. Anti-Spam Architecture

Native capabilities:

- SPF
- DKIM
- DMARC
- ARC
- DNSBL/RBL
- IP reputation
- Domain reputation
- Rate limiting
- Connection throttling
- Optional greylisting
- Header analysis
- Sender analysis
- Recipient analysis
- URL analysis
- Attachment heuristics
- Spam scoring
- Allow lists
- Block lists
- Quarantine
- User feedback
- Message trace

---

## 20. Third-Party Anti-Spam Providers

Provider abstraction:

```csharp
public interface ISpamProvider
{
    Task<SpamScanResult> ScanAsync(
        MailMessage message,
        SpamScanContext context,
        CancellationToken cancellationToken);
}

public interface IMalwareProvider
{
    Task<MalwareScanResult> ScanAsync(
        MailMessage message,
        MalwareScanContext context,
        CancellationToken cancellationToken);
}
```

Potential adapters:

```text
RspamdProvider.dll
SpamAssassinProvider.dll
RestApiSpamProvider.dll
GatewaySpamProvider.dll

ClamAVProvider.dll
RestApiMalwareProvider.dll
SandboxMalwareProvider.dll
```

Potential integrations include:

- Rspamd
- Apache SpamAssassin
- External REST APIs
- Mail gateways
- Milter/socket-style filtering
- Future ICAP-based providers
- URL reputation services
- Malware sandbox services

---

## 21. Anti-Spam Provider Aggregation

Normalize provider results into:

```text
Provider
Verdict
Score
Confidence
Categories
Rules/Symbols
LatencyMs
CorrelationId
FailureState
```

Example:

```text
Native Engine
  Score: 2.1
  Verdict: Clean

Rspamd
  Score: 1.7
  Verdict: Clean

SpamAssassin
  Score: 2.5
  Verdict: Clean

Final
  Verdict: Clean
```

The aggregation engine should support:

- Provider precedence
- Score thresholds
- Minimum confidence
- Fail-open/fail-closed
- Provider overrides
- Timeout
- Retry
- Provider health
- Provider-specific policies

A malware provider outage must never silently disable malware protection.

---

## 22. Anti-Spam Configuration GUI

```text
+------------------------------------------------------------+
| Anti-Spam Provider                                         |
+------------------------------------------------------------+
| Provider: [ Rspamd ▼ ]                                     |
| Mode:     [ REST/API ▼ ]                                   |
| Endpoint: https://spam01:11334                             |
| TLS:      Required                                         |
| API Key:  ***************                                  |
| Timeout:  3000 ms                                          |
| Failure:  [ Fail Closed ▼ ]                                |
|                                                            |
| Score Mapping:                                             |
|   0 - 4.9  Clean                                           |
|   5 - 7.9  Spam                                            |
|   8+       High Confidence Spam                            |
|                                                            |
| [ Test ]                                  [ Save ]         |
+------------------------------------------------------------+
```

---

## 23. Mail Storage

PostgreSQL should store metadata. Large mail bodies and attachments should use a storage abstraction.

```text
IMailStorage
   |
   +-- LocalFileStorage
   +-- S3Storage
   +-- MinIOStorage
   +-- AzureBlobStorage
   +-- NASStorage
```

Recommended separation:

```text
PostgreSQL
  |
  +-- Mailbox metadata
  +-- Message metadata
  +-- Flags
  +-- Recipients
  +-- Rules
  +-- Security data

Mail Storage
  |
  +-- Raw MIME
  +-- Attachments
  +-- Large objects
```

---

## 24. Search

Initial implementation:

- PostgreSQL full-text search
- Indexed message metadata
- Subject
- Sender
- Recipient
- Body where appropriate

Future option:

- OpenSearch
- Elasticsearch-compatible backend

Search should remain behind an abstraction.

---

## 25. Database Model

Suggested entities:

```text
tenants
domains
users
user_credentials
user_identities
identity_providers
identity_provider_groups

roles
permissions

mailboxes
folders
messages
message_recipients
message_flags
attachments

aliases
groups
group_members

sieve_scripts
mail_flow_rules

smtp_queue
smtp_delivery_attempts

dkim_keys
domain_dns_settings

spam_verdicts
malware_verdicts

sessions
refresh_tokens
application_passwords

security_events
audit_logs
system_events

backup_jobs
backup_history

licenses
license_entitlements
license_activations
license_usage
license_events

settings
```

All tenant-owned records must include a tenant relationship where applicable.

---

## 26. Logging Architecture

```text
Application
SMTP
IMAP
JMAP
Security
Anti-Spam
Backup
Update
Identity
Mail Flow
       |
       v
Central Event Engine
       |
       +---- Text Logs
       +---- PostgreSQL
       +---- Windows Event Log
       +---- Linux journald/syslog
```

---

## 27. File Logging

Windows:

```text
C:\ProgramData\Miautrix\Mail\Logs```

Linux:

```text
/var/log/miautrix-mail/
```

Structure:

```text
Logs
├── 2026
│   └── 09
│       └── 03
│           ├── application.log
│           ├── smtp.log
│           ├── imap.log
│           ├── jmap.log
│           ├── mailflow.log
│           ├── security.log
│           ├── antispam.log
│           ├── backup.log
│           ├── update.log
│           ├── audit.log
│           └── errors.log
```

Expired data should be removed automatically.

Empty day folders should then be removed, followed by empty month and year folders.

---

## 28. Log Retention

| Severity | Retention |
|---|---:|
| INFO | 30 days |
| NOTICE | 60 days |
| WARNING | 90 days |
| ERROR | 180 days |
| CRITICAL | 365 days |

Retention should be configurable.

Additional controls:

- Maximum log file size
- Rotation
- Compression
- Maximum total log storage
- PostgreSQL event retention
- Event partition cleanup

---

## 29. Event Database

Suggested event fields:

```text
ID
Timestamp
Severity
Code
Component
Server
SourceIP
User
MessageID
Description
Exception
CorrelationID
```

Examples:

```text
SMTP-2001   Connection accepted
SMTP-3001   TLS negotiation failed
SMTP-4012   Authentication failed
SMTP-5002   Relay denied
SMTP-6001   Delivery failed

MAILFLOW-5007   External relay denied

ANTISPAM-5001   Spam detected
ANTISPAM-5002   Provider timeout

ANTIMALWARE-5001   Malware detected

DB-5001   Database connection failure
BACKUP-5001   Backup failure
UPDATE-5001   Update validation failure
```

---

## 30. Security Logging Rules

Never log:

- Passwords
- TOTP secrets
- Session cookies
- Refresh tokens
- API secrets
- Private keys
- Application passwords
- Full message bodies unless explicitly enabled for controlled diagnostics

Sensitive identifiers should be masked where appropriate.

---

## 31. Security Analytics

Dashboard:

- Failed logins
- MFA failures
- Blocked IP addresses
- Unauthorized relay attempts
- Suspicious activity
- Spam
- Malware
- Quarantine
- Identity provider failures
- TLS failures
- Delivery failures
- Backup failures

Reports:

- Users with most login failures
- Login failures by IP
- Login failures by country
- MFA failures
- Account lockouts
- Suspicious locations
- Relay attempts
- Blocked IPs
- Spam volume
- Malware volume
- Quarantine activity
- Anti-spam provider latency
- Provider failures
- Delivery failures
- TLS failures
- System failures

IP geolocation should be treated as an approximate security signal. VPNs, proxies, NAT and mobile networks can make geolocation inaccurate.

Impossible-travel detection should initially be alert-only.

---

## 32. Web Administration

Recommended menu:

```text
Dashboard
Mail Flow
Users
Domains
Identity
Security
Anti-Spam
Anti-Malware
Queue
Quarantine
Logs
Reports
Backup
System
Licensing
```

The UI should be responsive and support desktop, tablet and mobile.

---

## 33. Webmail

JMAP-first architecture.

Features:

- Inbox
- Threading
- Compose
- Attachments
- Inline images
- HTML sandboxing
- Search
- Folders
- Labels
- Archive
- Spam
- Trash
- Read/unread
- Flags
- Signatures
- Templates
- Scheduled send
- Undo send
- Contacts
- Autocomplete
- Vacation responder
- Rules
- Responsive design
- Dark/light mode
- Keyboard shortcuts
- PWA support

---

## 34. Backup Architecture

Backups must include:

```text
PostgreSQL
Mail Storage
Configuration
Certificates/keys where required
Licensing metadata
Plugin metadata
```

Supported destinations:

- Local disk
- NAS
- SMB
- NFS
- S3
- MinIO

CLI examples:

```text
miautrix-mail backup
miautrix-mail restore
miautrix-mail restore --backup 2026-09-01
```

Target recovery:

```text
New VM/LXC
    |
    v
Install Miautrix
    |
    v
Restore
    |
    v
Verify
    |
    v
Health Checks
    |
    v
Operational
```

The GUI must provide backup creation, scheduling, history, validation and restore workflows.

---

## 35. Update Architecture

The update system should support near-zero downtime.

```text
                 Load Balancer
                 /                   Miautrix v1.8      Miautrix v1.9
           ACTIVE            STANDBY
                \             /
                  PostgreSQL
```

Process:

1. Download update.
2. Validate digital signature.
3. Validate package hash.
4. Install side-by-side.
5. Start new version.
6. Run health checks.
7. Validate database compatibility.
8. Apply safe migrations.
9. Switch traffic.
10. Drain old instance.
11. Stop old instance.
12. Keep rollback capability.

Database migrations should follow:

```text
EXPAND
  |
MIGRATE
  |
CONTRACT
```

Avoid destructive one-step schema changes.

---

## 36. Licensing Architecture

Recommended primary licensing metric:

**Active mailbox count**

Do not separately license:

- Domains
- Aliases
- Groups
- Mail-flow rules
- Identity integrations
- Anti-spam providers
- Server instances

Both local users and external identities should map to one mailbox entitlement.

Suggested editions:

```text
Community
Professional
Enterprise
MSP
```

### Community

- Core mail server
- Local accounts
- Basic security
- Basic administration

### Professional

- Advanced mail flow
- Advanced security
- External identity providers
- Advanced reporting
- Third-party providers

### Enterprise

- HA
- Advanced API
- Multi-tenancy
- Enterprise support
- Advanced operations

### MSP

- Multi-tenant
- Delegated administration
- Tenant-specific policies
- MSP automation
- Central management

Exact pricing should be determined after MVP and competitive analysis.

---

## 37. Licensing Behavior

Offline licensing should be supported.

If the licensing service becomes unavailable:

**Mail service must continue operating.**

If the configured mailbox limit is exceeded:

- Prevent creation of new mailboxes.
- Do not lock existing mailboxes.
- Do not stop mail delivery.
- Do not destroy or quarantine existing data.

---

## 38. Licensing Module

```text
Miautrix.Mail.Licensing
├── LicenseManager
├── LicenseValidator
├── LicenseStore
├── LicensePolicy
├── EntitlementService
├── UsageMeter
├── ActivationService
├── OfflineActivation
├── OnlineActivation
└── LicenseEvents
```

Database:

```text
licenses
license_entitlements
license_activations
license_usage
license_events
```

Licenses should be cryptographically signed.

---

## 39. Plugin Architecture

Plugins allow new functionality without recompiling the core server.

Examples:

```text
RspamdProvider.dll
SpamAssassinProvider.dll
ClamAVProvider.dll
S3StorageProvider.dll
MinioStorageProvider.dll
GoogleWorkspaceProvider.dll
EntraIdProvider.dll
ActiveDirectoryProvider.dll
```

Plugin requirements:

- Versioning
- Dependency declarations
- Enable/disable
- Health checks
- Configuration UI
- Digital signatures
- Compatibility validation
- Upgrade
- Rollback
- Audit trail

Plugin interfaces should remain stable and versioned.

---

## 40. Multi-Tenancy

The architecture should be multi-tenant ready from the beginning even if the first release targets single organizations.

```text
Tenant
 |
 +-- Domains
 +-- Users
 +-- Mailboxes
 +-- Policies
 +-- Identity Providers
 +-- Mail Flow Rules
 +-- Security
 +-- Logs
 +-- Reports
 +-- Quarantine
 +-- Storage
```

Tenant isolation must apply to:

- Users
- Mailboxes
- Domains
- Policies
- Rules
- Logs
- Reports
- Quarantine
- API access
- Storage
- Identity integrations

---

## 41. API Architecture

API areas:

```text
/api/v1/auth
/api/v1/users
/api/v1/domains
/api/v1/mailboxes
/api/v1/groups
/api/v1/identity
/api/v1/mailflow
/api/v1/security
/api/v1/antispam
/api/v1/antimalware
/api/v1/queue
/api/v1/quarantine
/api/v1/logs
/api/v1/reports
/api/v1/backups
/api/v1/system
/api/v1/licenses
/api/v1/plugins
```

The API should support:

- Authentication
- RBAC
- Tenant isolation
- Pagination
- Filtering
- Sorting
- Idempotency where appropriate
- Correlation IDs
- Audit logging
- Rate limiting

---

## 42. Future MSP / Ticketing Integration

The API should support future integrations with:

- ConnectWise
- Autotask
- HaloPSA
- Freshservice
- Jira
- ServiceNow
- Generic REST APIs
- Email alerts
- Webhooks

Example:

```text
Miautrix Event
      |
      v
Event Rules
      |
      +---- Email
      +---- Webhook
      +---- REST API
      +---- Ticket System
      +---- Monitoring
```

Potential alerts:

```text
Server certificate expiring
SMTP queue growing
Database failure
Backup failure
Mailbox quota exceeded
Spam provider unavailable
License threshold reached
```

---

## 43. Health Monitoring

Health checks:

```text
Database
Storage
SMTP
IMAP
JMAP
Queue
Anti-Spam
Anti-Malware
Identity Providers
Backup
Plugins
Licensing
```

Each health result should expose:

- Status
- Version
- Last successful check
- Last error
- Latency
- Dependency

Example:

```text
SMTP             HEALTHY
IMAP             HEALTHY
JMAP             HEALTHY
PostgreSQL       HEALTHY
Storage          HEALTHY
Rspamd           DEGRADED
Backup           HEALTHY
Entra ID         HEALTHY
```

---

## 44. Development Roadmap

### Phase 1 - Foundation

- Solution structure
- Dependency injection
- Configuration
- PostgreSQL
- EF Core
- Migrations
- Event engine
- Secrets management

### Phase 2 - Identity

- Local users
- Password authentication
- Roles
- Sessions
- TOTP
- Application passwords

### Phase 3 - SMTP

- SMTP listener
- TLS
- Authentication
- Queue
- Outbound delivery
- Retry
- Relay protection

### Phase 4 - Storage

- Mailboxes
- Folders
- Messages
- MIME
- Attachments
- Quotas
- Storage abstraction

### Phase 5 - IMAP/JMAP

- IMAP TLS
- JMAP
- Basic webmail integration

### Phase 6 - Web

- Administration
- Webmail
- Authentication
- MFA

### Phase 7 - Mail Flow

- Rule engine
- Device policies
- Graphical designer
- Simulator
- Mail-flow trace

### Phase 8 - Security

- SPF
- DKIM
- DMARC
- ARC
- Native anti-spam
- Quarantine
- Malware abstraction

### Phase 9 - Third-Party Providers

- Rspamd
- SpamAssassin
- External REST providers
- Malware providers
- Verdict aggregation

### Phase 10 - Directory Integration

- Google Workspace
- Entra ID
- LDAP
- Active Directory
- Group authorization
- Optional synchronization

### Phase 11 - Reporting

- Security analytics
- Delivery analytics
- Anti-spam reports
- Login reports
- Administrative reports

### Phase 12 - Operations

- Backup
- Restore
- Signed updates
- Blue/green deployment
- Licensing

### Phase 13 - Hardening

- Protocol conformance
- Security testing
- Performance
- Load testing
- Failover
- Recovery testing
- Documentation

---

## 45. MVP Definition

The MVP should contain:

- C# / .NET
- PostgreSQL
- Local users
- Domains
- Mailboxes
- SMTP TLS
- Authenticated submission
- IMAP TLS
- Basic JMAP
- Filesystem mail storage
- Web administration
- Basic webmail
- Password authentication
- TOTP
- SMTP queue
- Outbound delivery
- SPF
- DKIM
- DMARC baseline
- Native anti-spam baseline
- Anti-spam provider abstraction
- Central logging
- Text logs
- PostgreSQL logs
- Windows Event Log / Linux logging
- Basic mail-flow rules
- Restricted device relay
- Backup/restore

External identity integrations, advanced anti-spam, advanced reporting and enterprise functionality should follow after the core mail platform is stable.

---

## 46. Testing Strategy

### Unit Tests

- Mail flow
- Authentication
- Authorization
- MFA
- Spam scoring
- Event generation
- Licensing
- Storage
- Rule evaluation

### Integration Tests

- PostgreSQL
- SMTP
- IMAP
- JMAP
- LDAP/AD
- Google Workspace
- Entra ID
- Rspamd
- SpamAssassin
- Malware providers

### Security Tests

- TLS
- Brute force
- Open relay
- Session security
- MFA bypass
- API authorization
- SQL injection
- MIME parsing
- Attachment handling
- Header injection
- Malicious SMTP commands

### Performance Tests

- SMTP concurrency
- IMAP concurrency
- JMAP concurrency
- Large attachments
- Queue throughput
- PostgreSQL performance
- Provider latency
- Storage throughput

---

## 47. Disaster Recovery

Define:

- RPO
- RTO
- Backup frequency
- Retention
- Off-site backup
- Restore validation
- Database restore
- Mail storage restore
- Configuration restore
- Key/certificate recovery
- License recovery

A backup that cannot be restored successfully should not be considered valid.

Automated restore verification is strongly recommended.

---

## 48. Security Threat Model

The architect should explicitly model:

### External threats

- SMTP abuse
- Open relay attempts
- Credential stuffing
- Brute force
- Malicious attachments
- Spam
- Malware
- Header injection
- SMTP command abuse
- TLS downgrade
- Denial of service

### Internal threats

- Privilege escalation
- Malicious administrator
- Tenant isolation failure
- Unauthorized mailbox access
- Rule abuse
- Audit tampering

### Infrastructure threats

- Database compromise
- Storage compromise
- Plugin compromise
- Update package compromise
- Credential leakage
- Backup theft

---

## 49. Important Security Requirements

The final implementation must enforce:

1. No accidental open relay.
2. No plaintext authentication.
3. Secure secret storage.
4. Strong password hashing.
5. MFA for administrative access.
6. Rate limiting.
7. Account lockout or progressive delays.
8. API authorization on every protected operation.
9. Tenant isolation.
10. Audit logging.
11. Signed updates.
12. Signed plugins where applicable.
13. Signed licenses.
14. Secure backup handling.
15. No sensitive data in normal logs.

---

## 50. Observability

Use OpenTelemetry where practical.

Metrics should include:

```text
SMTP connections
SMTP authentication failures
SMTP delivery success
SMTP delivery failures
Queue size
Queue age
Messages per minute
Spam rate
Malware rate
Quarantine rate
IMAP sessions
JMAP requests
API requests
Database latency
Storage latency
Provider latency
Provider failures
Backup duration
Backup failures
Login failures
MFA failures
```

---

## 51. Configuration Management

Configuration should have:

- Defaults
- Environment overrides
- GUI management
- CLI management
- API management
- Validation
- Versioning
- Audit history

Sensitive configuration must use a secret abstraction.

Examples:

```text
Database password
API keys
OAuth client secrets
SMTP credentials
Encryption keys
Provider credentials
```

---

## 52. CLI

Recommended commands:

```text
miautrix-mail status

miautrix-mail backup

miautrix-mail restore

miautrix-mail restore --backup 2026-09-01

miautrix-mail user list

miautrix-mail user create

miautrix-mail domain list

miautrix-mail queue list

miautrix-mail queue retry

miautrix-mail logs export

miautrix-mail health

miautrix-mail license status

miautrix-mail plugin list
```

The CLI should use the same application services as the GUI.

---

## 53. Deployment

### Windows

- Windows Service
- Program files under controlled directory
- Data under `C:\ProgramData\Miautrix\Mail\`
- Windows Event Log integration

### Linux

- systemd
- `/etc/miautrix-mail/`
- `/var/lib/miautrix-mail/`
- `/var/log/miautrix-mail/`

### Containers

Support:

- Docker
- OCI
- Kubernetes in future

### Proxmox

```text
Proxmox
 |
 +-- VM
 |    +-- Debian
 |    +-- Miautrix
 |
 +-- LXC
      +-- Debian
           +-- Miautrix
```

The architect should define which features require VM deployment versus which are safe for LXC.

---

## 54. Upgrade Compatibility

Plugin and API compatibility must be versioned.

Recommended semantic versioning:

```text
MAJOR.MINOR.PATCH
```

Example:

```text
1.4.2
```

Compatibility rules should be documented for:

- Database schema
- Plugin API
- REST API
- JMAP behavior
- SMTP behavior
- IMAP behavior
- Configuration format

---

## 55. Documentation Requirements

The final product should include:

- Installation guide
- Administrator guide
- User guide
- API documentation
- CLI documentation
- Mail-flow documentation
- Security hardening guide
- Backup/restore guide
- Upgrade guide
- Plugin development guide
- Identity integration guide
- Anti-spam integration guide
- Troubleshooting guide
- Event-code reference

---

## 56. Definition of Done

A feature is not complete until:

- Unit tests exist.
- Integration tests exist where appropriate.
- Security implications are reviewed.
- Logs/events are implemented.
- Errors have event codes.
- API authorization is implemented.
- Documentation exists.
- Configuration validation exists.
- Failure behavior is defined.
- Recovery behavior is defined.
- Monitoring/health checks exist where applicable.

---

## 57. Architecture Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Implementing mail protocols from scratch | High | Strict RFC/conformance testing |
| MIME parsing vulnerabilities | High | Hardened parser and fuzz testing |
| Open relay | Critical | Explicit relay policy engine |
| External provider outage | High | Provider health and failure policy |
| Database bottleneck | High | Indexing, partitioning, caching |
| Large attachments | Medium | Object/file storage abstraction |
| Plugin security | High | Signing and isolation strategy |
| Upgrade failure | High | Blue/green + rollback |
| Backup corruption | Critical | Restore verification |
| Identity provider outage | Medium | Local emergency admin account |
| Multi-tenancy leakage | Critical | Tenant-scoped authorization |
| Spam provider inconsistency | Medium | Normalized verdict aggregation |

---

## 58. Recommended Changes Before Coding

The architect should finalize:

1. Exact domain model.
2. PostgreSQL ERD.
3. Module boundaries.
4. API contract.
5. Authentication architecture.
6. Plugin API.
7. Storage API.
8. Anti-spam provider API.
9. Malware provider API.
10. Mail-flow rule schema.
11. Queue architecture.
12. MIME storage format.
13. Encryption strategy.
14. Secret-management strategy.
15. Backup format.
16. Restore process.
17. Update package format.
18. License format.
19. Multi-tenant isolation model.
20. Protocol conformance strategy.
21. Threat model.
22. Performance targets.
23. HA architecture.
24. Disaster recovery targets.
25. Deployment architecture.

---

## 59. Architect Deliverables

Before production implementation, the architect should produce:

1. C4 context diagram
2. C4 container diagram
3. Component diagrams
4. Domain model
5. PostgreSQL ERD
6. API specification
7. Protocol implementation strategy
8. Security threat model
9. Identity architecture
10. Anti-spam architecture
11. Anti-malware architecture
12. Mail-flow engine design
13. Plugin architecture
14. Storage architecture
15. Backup architecture
16. Update architecture
17. Licensing architecture
18. Deployment architecture
19. MVP sprint breakdown
20. Development standards
21. Testing strategy
22. Definition of Done
23. Technical debt register
24. Architecture decision records

---

## 60. Final Architectural Recommendation

Miautrix Mail Server should be developed as a **secure modular mail platform in C#/.NET**, beginning with a modular monolith and maintaining strict boundaries between:

- Protocols
- Identity
- Authentication
- Authorization
- Mail flow
- Security
- Queue
- Storage
- Reporting
- Diagnostics
- Licensing
- Plugins

The most important architectural decision is to avoid making third-party products mandatory.

```text
                 MIAUTRIX CORE
                      |
       +--------------+--------------+
       |              |              |
   Identity       Anti-Spam      Storage
       |              |              |
    Providers      Providers      Providers
       |              |              |
       +--------------+--------------+
                      |
                Mail Flow Engine
                      |
                 Mail Delivery
```

The recommended development strategy is:

```text
FOUNDATION
    |
IDENTITY
    |
SMTP
    |
STORAGE
    |
IMAP/JMAP
    |
WEBMAIL / ADMIN
    |
MAIL FLOW
    |
SECURITY
    |
THIRD-PARTY PROVIDERS
    |
DIRECTORY INTEGRATION
    |
REPORTING
    |
BACKUP / UPDATE
    |
LICENSING
    |
MULTI-TENANCY / MSP
```

The project should not attempt to implement every enterprise feature simultaneously. Priority should be a secure, reliable, standards-compliant mail core with excellent observability and deterministic mail-flow behavior.

Once the core is stable, advanced integrations and MSP capabilities can be added without redesigning the foundation.

---

# Appendix A - Key Interfaces

```csharp
public interface IIdentityProvider
{
    Task<IdentityResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken);

    Task<bool> IsUserAllowedAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken);
}

public interface ISpamProvider
{
    Task<SpamScanResult> ScanAsync(
        MailMessage message,
        SpamScanContext context,
        CancellationToken cancellationToken);
}

public interface IMalwareProvider
{
    Task<MalwareScanResult> ScanAsync(
        MailMessage message,
        MalwareScanContext context,
        CancellationToken cancellationToken);
}

public interface IMailStorage
{
    Task<MailStorageResult> StoreAsync(
        MailMessage message,
        CancellationToken cancellationToken);

    Task<Stream> OpenAsync(
        string messageId,
        CancellationToken cancellationToken);
}

public interface IGeoIpProvider
{
    Task<GeoIpResult?> LookupAsync(
        IPAddress address,
        CancellationToken cancellationToken);
}

public interface IQueueProvider
{
    Task EnqueueAsync(
        OutboundMessage message,
        CancellationToken cancellationToken);
}
```

---

# Appendix B - Example Mail Event

```text
Event Code: SMTP-4012
Severity: WARNING
Component: SMTP
Timestamp: 2026-09-03T15:00:00
Source IP: 203.0.113.10
User: user@example.com
Message ID: <abc123@example.com>
Correlation ID: 2f3b8d1e-...
Description: SMTP authentication failed
```

---

# Appendix C - Example Security Workflow

```text
Incoming Connection
        |
        v
TLS Validation
        |
        v
IP Reputation
        |
        v
Connection Policy
        |
        v
SMTP Authentication
        |
        v
Sender Validation
        |
        v
SPF
        |
        v
DKIM
        |
        v
DMARC
        |
        v
Mail Flow Rules
        |
        v
Anti-Spam
        |
        v
Anti-Malware
        |
        +---- BLOCK
        |
        +---- QUARANTINE
        |
        v
Mailbox / Queue
```

---

# Appendix D - Initial Success Criteria

Miautrix Mail Server is ready for a production pilot when:

- Local users can send and receive mail securely.
- SMTP submission is TLS protected.
- IMAP is TLS protected.
- No open relay exists.
- Mail queues retry correctly.
- SPF/DKIM/DMARC work correctly.
- Basic anti-spam works.
- Malware provider integration has defined failure behavior.
- MFA works for web access.
- Administrative actions are audited.
- Security events have standardized codes.
- PostgreSQL and file logging work.
- Backup can be restored to a clean server.
- Updates can be rolled back.
- Health checks detect failed dependencies.
- API authorization is enforced.
- Restricted printer/scanner relay works without allowing unrestricted internet relay.
- Mail-flow rules are deterministic and testable.

---

**End of Architecture Review Draft**
