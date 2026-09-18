# Graph Report - miautrix-mail-server  (2026-09-18)

## Corpus Check
- 146 files · ~937,792 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1246 nodes · 2357 edges · 88 communities (67 shown, 18 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 196 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `4222e96d`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Main
- Miautrix.Mail.sln
- AppDbContext
- QuarantineItem
- ImapSession
- Message
- DkimService
- Epic 2: Mail Transport & Policy (T7-T13)
- Miautrix.Mail.Domain
- TenantScopedEntityBase
- .SimulateAsync
- App.tsx
- SieveTests
- Miautrix.Mail.Persistence.Migrations
- Guid
- Miautrix Mail Server Platform
- SessionManager.cs
- SmtpQueueItem
- .List
- Components
- ImapEngine.cs
- Miautrix.Mail.Domain.csproj
- Membership
- Miautrix.Mail.IntegrationTests.csproj
- InitialCreate
- AddSmtpQueueColumns
- AddMailAndSpamColumns
- Migration
- InMemoryIdempotencyStore
- Miautrix.Mail.Web/Program.cs
- SmtpDeliveryAttempt
- Mailbox
- Miautrix.Mail.SecurityTests.csproj
- MailQueueApiTests
- .BuildModel
- devDependencies
- Miautrix.Mail.UnitTests.csproj
- MailFlowRule
- MalwareVerdict
- Miautrix.Mail.Persistence.csproj
- DkimKey
- MailQueueService
- format-status.js
- versions.mjs
- generate_migration.sh
- SimulateSampleMessage
- AI Build Instructions
- Role
- fix_tasks.py
- JMAP Protocol Engine
- InMemorySecurityEventSink
- compilerOptions
- package.json
- HeaderRequestContextAccessor
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
- ITotpService
- .When_user_in_tenant_A_requests_mailbox_belonging_to_tenant_B_returns_404_and_does_not_leak_existence
- Iris Pay
- AddMembershipAndRolePermissionFks
- .Error
- Tokens
- ApiExceptionMiddleware
- Miautrix.Mail.EndToEndTests.csproj
- 2. Palette
- Pro tokens
- States
- Buttons
- EfPermissionRepository
- Miautrix.Mail.Seeder.csproj
- RequestIdMiddleware
- 4. Buttons
- Argon2idPasswordHasher
- Dictionary
- IReadOnlyDictionary

## God Nodes (most connected - your core abstractions)
1. `AppDbContext` - 124 edges
2. `TenantScopedEntityBase` - 54 edges
3. `SmtpQueueItem` - 30 edges
4. `Miautrix.Mail.Domain` - 29 edges
5. `Miautrix.Mail.Persistence` - 26 edges
6. `Message` - 25 edges
7. `ImapSession` - 21 edges
8. `QuarantineItem` - 20 edges
9. `SpamVerdict` - 17 edges
10. `Mailbox` - 16 edges

## Surprising Connections (you probably didn't know these)
- `IdentityTests` --references--> `ISessionManager`  [EXTRACTED]
  tests/Miautrix.Mail.SecurityTests/Identity/IdentityTests.cs → src/Miautrix.Mail.Identity/SessionManager.cs
- `IdentityTests` --references--> `ITotpService`  [EXTRACTED]
  tests/Miautrix.Mail.SecurityTests/Identity/IdentityTests.cs → src/Miautrix.Mail.Identity/TotpService.cs
- `MailFlowTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `SearchTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Search/SearchTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `ImapTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Imap/ImapTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Miautrix Epics Hierarchy** — blueprints_miautrix_mail_server_epics_01_core_platform_epic_1, blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2, blueprints_miautrix_mail_server_epics_03_surfaces_epic_3, blueprints_miautrix_mail_server_epics_04_operations_epic_4 [EXTRACTED 1.00]
- **Miautrix Provider Seams** — miautrix_mail_server_final_architecture_design_iidentityprovider, miautrix_mail_server_final_architecture_design_ispamprovider, miautrix_mail_server_final_architecture_design_imalwareprovider, miautrix_mail_server_final_architecture_design_imailstorage, miautrix_mail_server_final_architecture_design_isearchprovider, miautrix_mail_server_final_architecture_design_iqueueprovider [EXTRACTED 1.00]

## Communities (88 total, 18 thin omitted)

### Community 0 - ".Main"
Cohesion: 0.23
Nodes (8): ApiBehaviorOptions, Exception, Guid, IPermissionRepository, ITenantAuthorizationHelper, LastOwnerDemotionException, ResourceNotFoundException, TenantAuthorizationHelper

### Community 1 - "Miautrix.Mail.sln"
Cohesion: 0.08
Nodes (18): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+10 more)

### Community 2 - "AppDbContext"
Cohesion: 0.04
Nodes (55): DbContext, DbSet, IDesignTimeDbContextFactory, ModelBuilder, AppDbContext, Aliases, ApiKeys, ApplicationPasswords (+47 more)

### Community 3 - "QuarantineItem"
Cohesion: 0.06
Nodes (48): CancellationToken, Guid, List, Task, IQuarantineService, QuarantineService, Guid, IReadOnlyList (+40 more)

### Community 4 - "ImapSession"
Cohesion: 0.08
Nodes (34): Folder, MailboxId, Name, Role, UidNext, UidValidity, CancellationToken, GeneratedRegex (+26 more)

### Community 5 - "Message"
Cohesion: 0.09
Nodes (28): Miautrix.Mail.Search, Message, BodyHtml, BodyText, ContentHash, Date, Flags, FolderId (+20 more)

### Community 6 - "DkimService"
Cohesion: 0.13
Nodes (13): Body, Headers, RSA, Dictionary, GeneratedRegex, List, Regex, DkimKeyPair (+5 more)

### Community 7 - "Epic 2: Mail Transport & Policy (T7-T13)"
Cohesion: 0.25
Nodes (8): Epic 2: Mail Transport & Policy (T7-T13), Anti-Spam & Quarantine Subsystem, DNS Authentication (SPF, DKIM, DMARC, ARC), Full-Text Search (FTS) Indexing, IMAP Protocol Listener, Mail-Flow Rule Engine & Simulator, ManageSieve Script Engine, SMTP Listener & Queue

### Community 8 - "Miautrix.Mail.Domain"
Cohesion: 0.11
Nodes (20): Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.AntiSpam, Miautrix.Mail.IntegrationTests.AntiSpam, Miautrix.Mail.SecurityTests.Isolation, Miautrix.Mail.IntegrationTests.Search, Miautrix.Mail.IntegrationTests.Api, Miautrix.Mail.ProtocolTests.Smtp (+12 more)

### Community 9 - "TenantScopedEntityBase"
Cohesion: 0.14
Nodes (34): Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, BackupJob, Deploy (+26 more)

### Community 10 - ".SimulateAsync"
Cohesion: 0.15
Nodes (22): Miautrix.Mail.MailFlow, IDisposable, JsonSerializerOptions, CancellationToken, Guid, IReadOnlyDictionary, IReadOnlyList, List (+14 more)

### Community 11 - "App.tsx"
Cohesion: 0.09
Nodes (22): AdminApiClient, apiClient, App(), PLACEHOLDER_DESCRIPTIONS, DashboardScreen(), DashboardStats, Layout(), LayoutProps (+14 more)

### Community 12 - "SieveTests"
Cohesion: 0.21
Nodes (10): CancellationToken, Guid, HashSet, Task, SieveParser, SieveParseResult, SieveScriptService, Fact (+2 more)

### Community 13 - "Miautrix.Mail.Persistence.Migrations"
Cohesion: 0.35
Nodes (6): Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, npgsql_entityframeworkcore_postgresql_metadata, system

### Community 14 - "Guid"
Cohesion: 0.12
Nodes (16): Guid, GroupMember, GroupId, MemberAddress, MessageFlag, Flag, MessageId, MessageRecipient (+8 more)

### Community 15 - "Miautrix Mail Server Platform"
Cohesion: 0.22
Nodes (9): Epic 1: Core Platform (T1-T6), Secrets & Redaction Policy, Tenant Isolation Enforcement, Clean Architecture Pattern, .NET 10 LTS Core Engine, Miautrix Mail Server Platform, Modular Monolith Architecture, PostgreSQL Storage & Schema (+1 more)

### Community 16 - "SessionManager.cs"
Cohesion: 0.26
Nodes (10): SessionToken, DateTimeOffset, Guid, RefreshToken, TimeSpan, AuthToken, ISessionManager, RefreshTokenInfo (+2 more)

### Community 17 - "SmtpQueueItem"
Cohesion: 0.05
Nodes (52): Random, SmtpQueueItem, Attempts, LastAttemptAt, LastError, NextAttemptAt, RawMessage, Recipient (+44 more)

### Community 18 - ".List"
Cohesion: 0.08
Nodes (28): ControllerBase, HttpDelete, HttpGet, HttpPost, ProducesResponseType, CancellationToken, Guid, Task (+20 more)

### Community 19 - "Components"
Cohesion: 0.08
Nodes (24): Border Radius, Buttons, Cards, Checkboxes, Chips, Colors, Components, Default Item (+16 more)

### Community 20 - "ImapEngine.cs"
Cohesion: 0.16
Nodes (10): Miautrix.Mail.Protocols.Imap, Miautrix.Mail.Storage, Miautrix.Mail.Identity, Miautrix.Mail.SecurityTests.Identity, Miautrix.Mail.ProtocolTests.Imap, konscious_security_cryptography, SecurityEventCodes, system_security_cryptography (+2 more)

### Community 21 - "Miautrix.Mail.Domain.csproj"
Cohesion: 0.11
Nodes (17): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+9 more)

### Community 22 - "Membership"
Cohesion: 0.14
Nodes (13): Membership, RoleId, UserId, Permission, Code, Name, RolePermission, PermissionId (+5 more)

### Community 23 - "Miautrix.Mail.IntegrationTests.csproj"
Cohesion: 0.10
Nodes (17): Microsoft.AspNetCore.Mvc.Testing (10.0.4), Microsoft.AspNetCore.OpenApi (10.0.4), Microsoft.EntityFrameworkCore.InMemory (10.0.4), Microsoft.OpenApi (2.7.5), Microsoft.NET.Sdk.Web, net10.0, Microsoft.NET.Sdk, net10.0 (+9 more)

### Community 24 - "InitialCreate"
Cohesion: 0.22
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, InitialCreate

### Community 25 - "AddSmtpQueueColumns"
Cohesion: 0.22
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddSmtpQueueColumns

### Community 26 - "AddMailAndSpamColumns"
Cohesion: 0.22
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddMailAndSpamColumns

### Community 27 - "Migration"
Cohesion: 0.12
Nodes (12): Migration, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddCatalogFields, Guid, MigrationBuilder (+4 more)

### Community 28 - "InMemoryIdempotencyStore"
Cohesion: 0.23
Nodes (9): ConcurrentDictionary, HashSet, HttpContext, RequestDelegate, Task, IdempotencyMiddleware, CapturedResponse, IIdempotencyStore (+1 more)

### Community 29 - "Miautrix.Mail.Web/Program.cs"
Cohesion: 0.17
Nodes (7): Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.Web, Miautrix.Mail.Application.Queue, Miautrix.Mail.Security, Miautrix.Mail.Web.Contracts, system_diagnostics

### Community 30 - "SmtpDeliveryAttempt"
Cohesion: 0.13
Nodes (15): DateTimeOffset, EntityBase, CreatedAt, Id, UpdatedAt, SmtpDeliveryAttempt, AttemptedAt, AttemptNumber (+7 more)

### Community 31 - "Mailbox"
Cohesion: 0.12
Nodes (16): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Mailbox (+8 more)

### Community 32 - "Miautrix.Mail.SecurityTests.csproj"
Cohesion: 0.14
Nodes (12): Konscious.Security.Cryptography.Argon2 (1.3.1), Otp.NET (1.4.1), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4) (+4 more)

### Community 33 - "MailQueueApiTests"
Cohesion: 0.18
Nodes (11): IClassFixture, Guid, Task, ISmtpQueueService, SmtpQueueService, Program, Fact, Guid (+3 more)

### Community 34 - ".BuildModel"
Cohesion: 0.33
Nodes (5): ModelSnapshot, DateTimeOffset, Guid, ModelBuilder, AppDbContextModelSnapshot

### Community 35 - "devDependencies"
Cohesion: 0.06
Nodes (33): dependencies, react, react-dom, @xyflow/react, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react (+25 more)

### Community 36 - "Miautrix.Mail.UnitTests.csproj"
Cohesion: 0.14
Nodes (11): NetArchTest.eNhancedEdition (1.4.5), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1) (+3 more)

### Community 37 - "MailFlowRule"
Cohesion: 0.33
Nodes (6): MailFlowRule, ActionsJson, ConditionsJson, IsEnabled, Name, Priority

### Community 38 - "MalwareVerdict"
Cohesion: 0.33
Nodes (6): MalwareVerdict, Engine, IsMalware, Recipient, Sender, ThreatName

### Community 39 - "Miautrix.Mail.Persistence.csproj"
Cohesion: 0.15
Nodes (10): Microsoft.EntityFrameworkCore.Design (10.0.4), net10.0, Microsoft.EntityFrameworkCore (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0 (+2 more)

### Community 40 - "DkimKey"
Cohesion: 0.40
Nodes (5): DkimKey, DomainName, PrivateKeyPem, PublicKeyPem, Selector

### Community 41 - "MailQueueService"
Cohesion: 0.36
Nodes (6): InvalidOperationException, CancellationToken, DateTimeOffset, Guid, Task, MailQueueService

### Community 42 - "format-status.js"
Cohesion: 0.50
Nodes (3): fs, tasks, ref_fs

### Community 45 - "SimulateSampleMessage"
Cohesion: 0.20
Nodes (10): Dictionary, SimulateRuleRequest, SampleMessage, SimulateSampleMessage, HasAttachment, Headers, Recipient, Sender (+2 more)

### Community 46 - "AI Build Instructions"
Cohesion: 0.20
Nodes (10): 1 · Your role, 2 · Token compliance, 3 · Component recipes, 4 · Hard constraints, 5 · Before you finish — verify, AI Build Instructions, Buttons, Cards (+2 more)

### Community 47 - "Role"
Cohesion: 0.67
Nodes (3): Role, Code, Name

### Community 50 - "InMemorySecurityEventSink"
Cohesion: 0.20
Nodes (14): IPasswordHasher, DateTimeOffset, Guid, IReadOnlyList, List, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink (+6 more)

### Community 51 - "compilerOptions"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+14 more)

### Community 52 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 53 - "HeaderRequestContextAccessor"
Cohesion: 0.27
Nodes (8): IHttpContextAccessor, Guid, HeaderRequestContextAccessor, CurrentTenantId, CurrentUserId, IRequestContextAccessor, CurrentTenantId, CurrentUserId

### Community 54 - "Attachment"
Cohesion: 0.29
Nodes (7): Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes, StoragePath

### Community 55 - "LoginStatus"
Cohesion: 0.40
Nodes (5): LoginStatus, Failed, LockedOut, MfaRequired, Success

### Community 69 - "ITotpService"
Cohesion: 0.29
Nodes (3): otpnet, ITotpService, TotpService

### Community 70 - ".When_user_in_tenant_A_requests_mailbox_belonging_to_tenant_B_returns_404_and_does_not_leak_existence"
Cohesion: 0.36
Nodes (4): Fact, Guid, IsolationTests, MockPermissionRepo

### Community 71 - "Iris Pay"
Cohesion: 0.22
Nodes (8): 1. Atmosphere, 3. Typography, 5. Cards, 6. Charts, 7. Spacing, 8. Depth & elevation, 9. Do's & don'ts, Iris Pay

### Community 72 - "AddMembershipAndRolePermissionFks"
Cohesion: 0.25
Nodes (6): Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddMembershipAndRolePermissionFks

### Community 73 - ".Error"
Cohesion: 0.25
Nodes (6): IReadOnlyDictionary, ApiError, HttpContext, IReadOnlyDictionary, IResult, ApiResults

### Community 74 - "Tokens"
Cohesion: 0.29
Nodes (7): Borders, Charts, Colors, Radius, Shadows, Tokens, Typography

### Community 75 - "ApiExceptionMiddleware"
Cohesion: 0.43
Nodes (5): ILogger, HttpContext, RequestDelegate, Task, ApiExceptionMiddleware

### Community 76 - "Miautrix.Mail.EndToEndTests.csproj"
Cohesion: 0.29
Nodes (6): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 77 - "2. Palette"
Cohesion: 0.33
Nodes (6): 2. Palette, Accent (decorative only), Brand Dark, Interactive, Neutral, Primary

### Community 78 - "Pro tokens"
Cohesion: 0.33
Nodes (6): Accessibility (WCAG 2.1), Content, Density, Elevation, Motion, Pro tokens

### Community 79 - "States"
Cohesion: 0.40
Nodes (5): Button, Card, Input, States, Tab

### Community 80 - "Buttons"
Cohesion: 0.40
Nodes (5): Buttons, Ghost, Outline, Primary, Secondary

### Community 82 - "Miautrix.Mail.Seeder.csproj"
Cohesion: 0.40
Nodes (4): net10.0, Microsoft.EntityFrameworkCore (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 83 - "RequestIdMiddleware"
Cohesion: 0.40
Nodes (4): HttpContext, RequestDelegate, Task, RequestIdMiddleware

### Community 84 - "4. Buttons"
Cohesion: 0.50
Nodes (4): 4. Buttons, Disabled / Muted, Outline, Primary Iris

## Knowledge Gaps
- **444 isolated node(s):** `Overview`, `Colors`, `Typography`, `Spacing`, `Border Radius` (+439 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 558 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **18 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AppDbContext` connect `AppDbContext` to `.Main`, `QuarantineItem`, `ImapSession`, `Message`, `Miautrix.Mail.Domain`, `TenantScopedEntityBase`, `.SimulateAsync`, `SieveTests`, `Guid`, `SmtpQueueItem`, `Membership`, `SmtpDeliveryAttempt`, `Mailbox`, `MailQueueApiTests`, `MailFlowRule`, `MalwareVerdict`, `DkimKey`, `MailQueueService`, `Role`, `Attachment`, `EfPermissionRepository`?**
  _High betweenness centrality (0.204) - this node is a cross-community bridge._
- **Why does `Miautrix.Mail.Domain` connect `Miautrix.Mail.Domain` to `.Main`, `MailQueueApiTests`, `Message`, `TenantScopedEntityBase`, `.SimulateAsync`, `.List`, `ImapEngine.cs`, `Miautrix.Mail.Web/Program.cs`?**
  _High betweenness centrality (0.061) - this node is a cross-community bridge._
- **Why does `Miautrix.Mail.Persistence` connect `Miautrix.Mail.Domain` to `Message`, `.SimulateAsync`, `Miautrix.Mail.Persistence.Migrations`, `ImapEngine.cs`, `Miautrix.Mail.Web/Program.cs`?**
  _High betweenness centrality (0.061) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `SmtpQueueItem` (e.g. with `.When_cross_tenant_retry_is_attempted_returns_404_not_found()` and `.When_listing_queue_returns_envelope_with_data_and_meta()`) actually correct?**
  _`SmtpQueueItem` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Overview`, `Colors`, `Typography` to the rest of the system?**
  _444 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Miautrix.Mail.sln` be split into smaller, more focused modules?**
  _Cohesion score 0.08172043010752689 - nodes in this community are weakly interconnected._
- **Should `AppDbContext` be split into smaller, more focused modules?**
  _Cohesion score 0.03571428571428571 - nodes in this community are weakly interconnected._