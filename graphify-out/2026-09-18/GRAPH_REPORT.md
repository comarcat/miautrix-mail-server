# Graph Report - miautrix-mail-server  (2026-09-18)

## Corpus Check
- 125 files · ~875,865 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 938 nodes · 1901 edges · 69 communities (52 shown, 14 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 158 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `ae41e929`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- TenantAuthorizationHelper
- Miautrix.Mail.sln
- AppDbContext
- QuarantineItem
- ImapSession
- Message
- DkimService
- Epic 2: Mail Transport & Policy (T7-T13)
- Miautrix.Mail.Domain
- TenantScopedEntityBase
- MailFlowEngine.cs
- App.tsx
- SieveTests
- Miautrix.Mail.Persistence.Migrations
- Guid
- Miautrix Mail Server Platform
- SessionManager.cs
- MockSmtpQueueManager
- .When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry
- SmtpResponse
- Miautrix.Mail.Identity
- .IsDomainLocal
- .Main
- SmtpQueueItem
- InitialCreate
- AddSmtpQueueColumns
- AddMailAndSpamColumns
- AddCatalogFields
- AddAuditLogColumns
- Miautrix.Mail.Queue
- SmtpDeliveryAttempt
- AuditLog
- .HandleDataAsync
- Miautrix.Mail.Protocols.Smtp
- .BuildModel
- devDependencies
- Mailbox
- MailFlowRule
- MalwareVerdict
- .When_an_audited_change_is_rolled_back_the_system_shall_not_persist_its_audit_row
- DkimKey
- SieveScript
- format-status.js
- versions.mjs
- generate_migration.sh
- Alias
- Group
- Role
- fix_tasks.py
- JMAP Protocol Engine
- InMemorySecurityEventSink
- compilerOptions
- package.json
- .GetEvents
- Attachment
- LoginStatus
- Epic 4: Operations (T18-T21)
- Epic 3: Surfaces (T14-T17)
- Licensing & Over-Allowance Policy
- IAuthenticationProvider
- IIdentityProvider
- IMailStorage
- IMalwareProvider
- IQueueProvider
- ISearchProvider
- ISpamProvider

## God Nodes (most connected - your core abstractions)
1. `AppDbContext` - 120 edges
2. `TenantScopedEntityBase` - 54 edges
3. `net10.0` - 30 edges
4. `Microsoft.NET.Sdk` - 30 edges
5. `Message` - 25 edges
6. `SmtpQueueItem` - 24 edges
7. `Miautrix.Mail.Domain` - 23 edges
8. `ImapSession` - 21 edges
9. `Miautrix.Mail.Persistence` - 21 edges
10. `Miautrix.Mail.Domain` - 20 edges

## Surprising Connections (you probably didn't know these)
- `IdentityTests` --references--> `ISessionManager`  [EXTRACTED]
  tests/Miautrix.Mail.SecurityTests/Identity/IdentityTests.cs → src/Miautrix.Mail.Identity/SessionManager.cs
- `MailFlowTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `SieveTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Sieve/SieveTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `MockSmtpQueueManager` --references--> `SmtpQueueItem`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Smtp/SmtpProtocolTests.cs → src/Miautrix.Mail.Domain/Entities.cs
- `MockSmtpQueueManager` --implements--> `ISmtpQueueManager`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Smtp/SmtpProtocolTests.cs → src/Miautrix.Mail.Queue/SmtpQueueManager.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Miautrix Epics Hierarchy** — blueprints_miautrix_mail_server_epics_01_core_platform_epic_1, blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2, blueprints_miautrix_mail_server_epics_03_surfaces_epic_3, blueprints_miautrix_mail_server_epics_04_operations_epic_4 [EXTRACTED 1.00]
- **Miautrix Provider Seams** — miautrix_mail_server_final_architecture_design_iidentityprovider, miautrix_mail_server_final_architecture_design_ispamprovider, miautrix_mail_server_final_architecture_design_imalwareprovider, miautrix_mail_server_final_architecture_design_imailstorage, miautrix_mail_server_final_architecture_design_isearchprovider, miautrix_mail_server_final_architecture_design_iqueueprovider [EXTRACTED 1.00]

## Communities (69 total, 14 thin omitted)

### Community 0 - "TenantAuthorizationHelper"
Cohesion: 0.15
Nodes (14): Miautrix.Mail.SecurityTests.Isolation, Miautrix.Mail.Security, Exception, InvalidOperationException, Guid, IPermissionRepository, ITenantAuthorizationHelper, LastOwnerDemotionException (+6 more)

### Community 1 - "Miautrix.Mail.sln"
Cohesion: 0.09
Nodes (63): Konscious.Security.Cryptography.Argon2 (1.3.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Microsoft.EntityFrameworkCore.InMemory (10.0.4), NetArchTest.eNhancedEdition (1.4.5), Otp.NET (1.4.1), Miautrix.Mail.Admin, Miautrix.Mail.AntiMalware, Miautrix.Mail.AntiSpam (+55 more)

### Community 2 - "AppDbContext"
Cohesion: 0.04
Nodes (54): DbContext, DbSet, IDesignTimeDbContextFactory, AppDbContext, Aliases, ApiKeys, ApplicationPasswords, Attachments (+46 more)

### Community 3 - "QuarantineItem"
Cohesion: 0.06
Nodes (49): Miautrix.Mail.AntiSpam, CancellationToken, Guid, List, Task, IQuarantineService, QuarantineService, Guid (+41 more)

### Community 4 - "ImapSession"
Cohesion: 0.09
Nodes (28): CancellationToken, GeneratedRegex, Guid, IReadOnlyList, Regex, Stream, Task, ImapCommandResult (+20 more)

### Community 5 - "Message"
Cohesion: 0.10
Nodes (27): Message, BodyHtml, BodyText, ContentHash, Date, Flags, FolderId, IsRead (+19 more)

### Community 6 - "DkimService"
Cohesion: 0.12
Nodes (14): Body, Dictionary, Headers, RSA, GeneratedRegex, List, Regex, DkimKeyPair (+6 more)

### Community 7 - "Epic 2: Mail Transport & Policy (T7-T13)"
Cohesion: 0.25
Nodes (8): Epic 2: Mail Transport & Policy (T7-T13), Anti-Spam & Quarantine Subsystem, DNS Authentication (SPF, DKIM, DMARC, ARC), Full-Text Search (FTS) Indexing, IMAP Protocol Listener, Mail-Flow Rule Engine & Simulator, ManageSieve Script Engine, SMTP Listener & Queue

### Community 8 - "Miautrix.Mail.Domain"
Cohesion: 0.12
Nodes (20): Miautrix.Mail.Protocols.Imap, Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Storage, Miautrix.Mail.IntegrationTests.AntiSpam, Miautrix.Mail.Search, Miautrix.Mail.IntegrationTests.Search, Miautrix.Mail.Persistence (+12 more)

### Community 9 - "TenantScopedEntityBase"
Cohesion: 0.17
Nodes (29): ApiKey, ApplicationPassword, BackupHistory, BackupJob, Deploy, Domain, DomainDnsSetting, IdempotencyKey (+21 more)

### Community 10 - "MailFlowEngine.cs"
Cohesion: 0.17
Nodes (19): IDisposable, IReadOnlyDictionary, CancellationToken, Guid, IReadOnlyList, List, Task, IMailFlowEngine (+11 more)

### Community 11 - "App.tsx"
Cohesion: 0.09
Nodes (22): AdminApiClient, apiClient, App(), PLACEHOLDER_DESCRIPTIONS, DashboardScreen(), DashboardStats, Layout(), LayoutProps (+14 more)

### Community 12 - "SieveTests"
Cohesion: 0.20
Nodes (10): CancellationToken, Guid, HashSet, Task, SieveParser, SieveParseResult, SieveScriptService, Fact (+2 more)

### Community 13 - "Miautrix.Mail.Persistence.Migrations"
Cohesion: 0.37
Nodes (6): Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, npgsql_entityframeworkcore_postgresql_metadata, system

### Community 14 - "Guid"
Cohesion: 0.12
Nodes (17): Guid, Folder, MailboxId, Name, Role, UidNext, UidValidity, GroupMember (+9 more)

### Community 15 - "Miautrix Mail Server Platform"
Cohesion: 0.22
Nodes (9): Epic 1: Core Platform (T1-T6), Secrets & Redaction Policy, Tenant Isolation Enforcement, Clean Architecture Pattern, .NET 10 LTS Core Engine, Miautrix Mail Server Platform, Modular Monolith Architecture, PostgreSQL Storage & Schema (+1 more)

### Community 16 - "SessionManager.cs"
Cohesion: 0.26
Nodes (10): SessionToken, DateTimeOffset, Guid, RefreshToken, TimeSpan, AuthToken, ISessionManager, RefreshTokenInfo (+2 more)

### Community 17 - "MockSmtpQueueManager"
Cohesion: 0.24
Nodes (11): CancellationToken, Fact, Guid, HashSet, List, Task, MockAuthenticator, MockDomainValidator (+3 more)

### Community 18 - ".When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry"
Cohesion: 0.24
Nodes (8): CancellationToken, Guid, Task, ISmtpQueueManager, SmtpQueueManager, Fact, Task, SmtpQueueDatabaseTests

### Community 19 - "SmtpResponse"
Cohesion: 0.26
Nodes (9): SmtpResponse, IsSuccess, CancellationToken, Guid, QueueItem, Response, Task, ISmtpSubmissionHandler (+1 more)

### Community 20 - "Miautrix.Mail.Identity"
Cohesion: 0.16
Nodes (7): Miautrix.Mail.Identity, Miautrix.Mail.SecurityTests.Identity, konscious_security_cryptography, otpnet, Argon2idPasswordHasher, SecurityEventCodes, system_security_cryptography

### Community 21 - ".IsDomainLocal"
Cohesion: 0.29
Nodes (3): Guid, ISmtpAuthenticator, ISmtpDomainValidator

### Community 22 - ".Main"
Cohesion: 0.20
Nodes (9): Membership, Permission, Code, Name, RolePermission, User, Guid, Task (+1 more)

### Community 23 - "SmtpQueueItem"
Cohesion: 0.20
Nodes (10): SmtpQueueItem, Attempts, LastAttemptAt, LastError, NextAttemptAt, RawMessage, Recipient, Sender (+2 more)

### Community 24 - "InitialCreate"
Cohesion: 0.22
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, InitialCreate

### Community 25 - "AddSmtpQueueColumns"
Cohesion: 0.22
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddSmtpQueueColumns

### Community 26 - "AddMailAndSpamColumns"
Cohesion: 0.22
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddMailAndSpamColumns

### Community 27 - "AddCatalogFields"
Cohesion: 0.25
Nodes (6): Migration, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddCatalogFields

### Community 28 - "AddAuditLogColumns"
Cohesion: 0.25
Nodes (6): Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddAuditLogColumns

### Community 29 - "Miautrix.Mail.Queue"
Cohesion: 0.43
Nodes (5): Miautrix.Mail.Queue, Random, TimeSpan, ExponentialBackoffWithJitterRetryPolicy, IRetryPolicy

### Community 30 - "SmtpDeliveryAttempt"
Cohesion: 0.13
Nodes (15): DateTimeOffset, EntityBase, CreatedAt, Id, UpdatedAt, SmtpDeliveryAttempt, AttemptedAt, AttemptNumber (+7 more)

### Community 31 - "AuditLog"
Cohesion: 0.29
Nodes (7): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType

### Community 32 - ".HandleDataAsync"
Cohesion: 0.32
Nodes (7): CancellationToken, Guid, QueueItem, Response, Task, ISmtpInboundHandler, SmtpInboundHandler

### Community 33 - "Miautrix.Mail.Protocols.Smtp"
Cohesion: 0.26
Nodes (7): Miautrix.Mail.ProtocolTests.Smtp, Miautrix.Mail.Protocols.Smtp, Guid, Task, ISmtpQueueService, SmtpQueueService, xunit

### Community 34 - ".BuildModel"
Cohesion: 0.33
Nodes (5): ModelSnapshot, DateTimeOffset, Guid, ModelBuilder, AppDbContextModelSnapshot

### Community 35 - "devDependencies"
Cohesion: 0.06
Nodes (33): dependencies, react, react-dom, @xyflow/react, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react (+25 more)

### Community 36 - "Mailbox"
Cohesion: 0.33
Nodes (6): Mailbox, Address, DomainId, IsActive, QuotaBytes, UsedBytes

### Community 37 - "MailFlowRule"
Cohesion: 0.33
Nodes (6): MailFlowRule, ActionsJson, ConditionsJson, IsEnabled, Name, Priority

### Community 38 - "MalwareVerdict"
Cohesion: 0.33
Nodes (6): MalwareVerdict, Engine, IsMalware, Recipient, Sender, ThreatName

### Community 39 - ".When_an_audited_change_is_rolled_back_the_system_shall_not_persist_its_audit_row"
Cohesion: 0.47
Nodes (3): Fact, Task, AuditTests

### Community 40 - "DkimKey"
Cohesion: 0.40
Nodes (5): DkimKey, DomainName, PrivateKeyPem, PublicKeyPem, Selector

### Community 41 - "SieveScript"
Cohesion: 0.40
Nodes (5): SieveScript, Content, IsActive, MailboxId, Name

### Community 42 - "format-status.js"
Cohesion: 0.50
Nodes (3): fs, tasks, ref_fs

### Community 45 - "Alias"
Cohesion: 0.67
Nodes (3): Alias, Address, TargetAddress

### Community 46 - "Group"
Cohesion: 0.67
Nodes (3): Group, Address, Name

### Community 47 - "Role"
Cohesion: 0.67
Nodes (3): Role, Code, Name

### Community 50 - "InMemorySecurityEventSink"
Cohesion: 0.18
Nodes (12): IPasswordHasher, List, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink, ISecurityEventSink, LoginResult, UserAccount (+4 more)

### Community 51 - "compilerOptions"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+14 more)

### Community 52 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 53 - ".GetEvents"
Cohesion: 0.39
Nodes (4): DateTimeOffset, Guid, IReadOnlyList, SecurityEventRecord

### Community 54 - "Attachment"
Cohesion: 0.29
Nodes (7): Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes, StoragePath

### Community 55 - "LoginStatus"
Cohesion: 0.40
Nodes (5): LoginStatus, Failed, LockedOut, MfaRequired, Success

## Knowledge Gaps
- **299 isolated node(s):** `name`, `version`, `type`, `dev`, `build` (+294 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 384 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AppDbContext` connect `AppDbContext` to `QuarantineItem`, `ImapSession`, `Message`, `Miautrix.Mail.Domain`, `TenantScopedEntityBase`, `MailFlowEngine.cs`, `SieveTests`, `Guid`, `.When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry`, `.Main`, `SmtpQueueItem`, `SmtpDeliveryAttempt`, `AuditLog`, `Miautrix.Mail.Protocols.Smtp`, `Mailbox`, `MailFlowRule`, `MalwareVerdict`, `.When_an_audited_change_is_rolled_back_the_system_shall_not_persist_its_audit_row`, `DkimKey`, `SieveScript`, `Alias`, `Group`, `Role`, `Attachment`?**
  _High betweenness centrality (0.243) - this node is a cross-community bridge._
- **Why does `Miautrix.Mail.Domain` connect `Miautrix.Mail.Domain` to `.HandleDataAsync`, `Miautrix.Mail.Protocols.Smtp`, `TenantAuthorizationHelper`, `QuarantineItem`, `Message`, `TenantScopedEntityBase`, `MailFlowEngine.cs`, `SieveTests`, `.When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry`, `SmtpResponse`?**
  _High betweenness centrality (0.095) - this node is a cross-community bridge._
- **Why does `SmtpQueueItem` connect `SmtpQueueItem` to `.HandleDataAsync`, `Miautrix.Mail.Protocols.Smtp`, `AppDbContext`, `TenantScopedEntityBase`, `MockSmtpQueueManager`, `.When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry`, `SmtpResponse`, `SmtpDeliveryAttempt`?**
  _High betweenness centrality (0.066) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `Message` (e.g. with `.HandleAppendAsync()` and `.Message_WhenDelivered_IsFindableByFullTextQueryWithinOneSecond()`) actually correct?**
  _`Message` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `name`, `version`, `type` to the rest of the system?**
  _299 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Miautrix.Mail.sln` be split into smaller, more focused modules?**
  _Cohesion score 0.08735733099209833 - nodes in this community are weakly interconnected._
- **Should `AppDbContext` be split into smaller, more focused modules?**
  _Cohesion score 0.037037037037037035 - nodes in this community are weakly interconnected._