# Graph Report - miautrix-mail-server  (2026-09-18)

## Corpus Check
- 199 files · ~1,680,349 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1724 nodes · 3048 edges · 125 communities (99 shown, 18 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 227 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `9e0a4d8b`
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
- microsoft_entityframeworkcore
- Guid
- Miautrix Mail Server Platform
- webmail/src/App.tsx
- MockSmtpQueueManager
- .List
- Components
- ImapEngine.cs
- Miautrix.Mail.Domain.csproj
- Membership
- Miautrix.Mail.Web.csproj
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
- devDependencies
- format-status.js
- versions.mjs
- generate_migration.sh
- SimulateSampleMessage
- AI Build Instructions
- devDependencies
- fix_tasks.py
- JMAP Protocol Engine
- InMemorySecurityEventSink
- compilerOptions
- package.json
- HeaderRequestContextAccessor
- Attachment
- .TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData
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
- compilerOptions
- compilerOptions
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
- compilerOptions
- .RestoreBackupAsync
- IReadOnlyDictionary
- .StageAndSwitchAsync
- compilerOptions
- Miautrix.Mail.IntegrationTests.csproj
- SmtpQueueItem
- Miautrix Mail Server - Production Deployment Guide (Debian LXC)
- Miautrix.Mail.Cli.Commands
- .When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry
- MailboxCreateSettings
- SmtpResponse
- MailboxDeleteSettings
- Miautrix.Mail.Persistence
- Miautrix.Mail.Cli.csproj
- DomainListCommand
- MailboxListCommand
- .HandleDataAsync
- CommandSettings
- QuarantineReleaseCommand
- QueueListCommand
- QueueRetryCommand
- .Main
- MailQueueService
- QuarantineListCommand
- .When_user_in_tenant_A_requests_mailbox_belonging_to_tenant_B_returns_404_and_does_not_leak_existence
- SystemInfoCommand
- ExponentialBackoffWithJitterRetryPolicy
- .IsDomainLocal
- BackupRestoreTests.cs
- SieveScript
- React + TypeScript + Vite
- desktop/tsconfig.json
- Alias
- webmail/tsconfig.json

## God Nodes (most connected - your core abstractions)
1. `AppDbContext` - 124 edges
2. `TenantScopedEntityBase` - 54 edges
3. `SmtpQueueItem` - 30 edges
4. `Miautrix.Mail.Domain` - 29 edges
5. `Miautrix.Mail.Persistence` - 26 edges
6. `Message` - 25 edges
7. `ImapSession` - 21 edges
8. `QuarantineItem` - 20 edges
9. `Mailbox` - 19 edges
10. `compilerOptions` - 18 edges

## Surprising Connections (you probably didn't know these)
- `MockPermissionRepo` --implements--> `IPermissionRepository`  [EXTRACTED]
  tests/Miautrix.Mail.SecurityTests/Isolation/IsolationTests.cs → src/Miautrix.Mail.Security/TenantAuthorizationHelper.cs
- `MailFlowTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `MockAuthenticator` --implements--> `ISmtpAuthenticator`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Smtp/SmtpProtocolTests.cs → src/Miautrix.Mail.Protocols.Smtp/SmtpModels.cs
- `MockDomainValidator` --implements--> `ISmtpDomainValidator`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Smtp/SmtpProtocolTests.cs → src/Miautrix.Mail.Protocols.Smtp/SmtpModels.cs
- `SieveTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Sieve/SieveTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Miautrix Epics Hierarchy** — blueprints_miautrix_mail_server_epics_01_core_platform_epic_1, blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2, blueprints_miautrix_mail_server_epics_03_surfaces_epic_3, blueprints_miautrix_mail_server_epics_04_operations_epic_4 [EXTRACTED 1.00]
- **Miautrix Provider Seams** — miautrix_mail_server_final_architecture_design_iidentityprovider, miautrix_mail_server_final_architecture_design_ispamprovider, miautrix_mail_server_final_architecture_design_imalwareprovider, miautrix_mail_server_final_architecture_design_imailstorage, miautrix_mail_server_final_architecture_design_isearchprovider, miautrix_mail_server_final_architecture_design_iqueueprovider [EXTRACTED 1.00]

## Communities (125 total, 18 thin omitted)

### Community 0 - ".Main"
Cohesion: 0.24
Nodes (8): ApiBehaviorOptions, Exception, Guid, IPermissionRepository, ITenantAuthorizationHelper, LastOwnerDemotionException, ResourceNotFoundException, TenantAuthorizationHelper

### Community 1 - "Miautrix.Mail.sln"
Cohesion: 0.10
Nodes (14): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+6 more)

### Community 2 - "AppDbContext"
Cohesion: 0.04
Nodes (55): DbContext, DbSet, IDesignTimeDbContextFactory, ModelBuilder, AppDbContext, Aliases, ApiKeys, ApplicationPasswords (+47 more)

### Community 3 - "QuarantineItem"
Cohesion: 0.06
Nodes (48): CancellationToken, Guid, List, Task, IQuarantineService, QuarantineService, Guid, IReadOnlyList (+40 more)

### Community 4 - "ImapSession"
Cohesion: 0.09
Nodes (28): CancellationToken, GeneratedRegex, Guid, IReadOnlyList, Regex, Stream, Task, ImapCommandResult (+20 more)

### Community 5 - "Message"
Cohesion: 0.10
Nodes (27): Message, BodyHtml, BodyText, ContentHash, Date, Flags, FolderId, IsRead (+19 more)

### Community 6 - "DkimService"
Cohesion: 0.13
Nodes (13): Body, Headers, RSA, Dictionary, GeneratedRegex, List, Regex, DkimKeyPair (+5 more)

### Community 7 - "Epic 2: Mail Transport & Policy (T7-T13)"
Cohesion: 0.25
Nodes (8): Epic 2: Mail Transport & Policy (T7-T13), Anti-Spam & Quarantine Subsystem, DNS Authentication (SPF, DKIM, DMARC, ARC), Full-Text Search (FTS) Indexing, IMAP Protocol Listener, Mail-Flow Rule Engine & Simulator, ManageSieve Script Engine, SMTP Listener & Queue

### Community 8 - "Miautrix.Mail.Domain"
Cohesion: 0.12
Nodes (14): Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Web, Miautrix.Mail.AntiSpam, Miautrix.Mail.IntegrationTests.AntiSpam, Miautrix.Mail.SecurityTests.Isolation, Miautrix.Mail.IntegrationTests.Api, Miautrix.Mail.ProtocolTests.Smtp (+6 more)

### Community 9 - "TenantScopedEntityBase"
Cohesion: 0.13
Nodes (36): ApiKey, ApplicationPassword, BackupHistory, BackupJob, Deploy, Domain, DomainDnsSetting, Group (+28 more)

### Community 10 - ".SimulateAsync"
Cohesion: 0.08
Nodes (31): Miautrix.Mail.Cli.Infrastructure, Miautrix.Mail.MailFlow, IDisposable, IServiceCollection, IServiceProvider, ITypeRegistrar, ITypeResolver, JsonSerializerOptions (+23 more)

### Community 11 - "App.tsx"
Cohesion: 0.09
Nodes (22): AdminApiClient, apiClient, App(), PLACEHOLDER_DESCRIPTIONS, DashboardScreen(), DashboardStats, Layout(), LayoutProps (+14 more)

### Community 12 - "SieveTests"
Cohesion: 0.21
Nodes (10): CancellationToken, Guid, HashSet, Task, SieveParser, SieveParseResult, SieveScriptService, Fact (+2 more)

### Community 13 - "microsoft_entityframeworkcore"
Cohesion: 0.35
Nodes (7): Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, npgsql_entityframeworkcore_postgresql_metadata, system

### Community 14 - "Guid"
Cohesion: 0.12
Nodes (17): Guid, Folder, MailboxId, Name, Role, UidNext, UidValidity, GroupMember (+9 more)

### Community 15 - "Miautrix Mail Server Platform"
Cohesion: 0.22
Nodes (9): Epic 1: Core Platform (T1-T6), Secrets & Redaction Policy, Tenant Isolation Enforcement, Clean Architecture Pattern, .NET 10 LTS Core Engine, Miautrix Mail Server Platform, Modular Monolith Architecture, PostgreSQL Storage & Schema (+1 more)

### Community 16 - "webmail/src/App.tsx"
Cohesion: 0.07
Nodes (37): DesktopApp(), oxc, react, typescript, warn, plugins, rules, react/only-export-components (+29 more)

### Community 17 - "MockSmtpQueueManager"
Cohesion: 0.22
Nodes (11): CancellationToken, Fact, Guid, HashSet, List, Task, MockAuthenticator, MockDomainValidator (+3 more)

### Community 18 - ".List"
Cohesion: 0.08
Nodes (28): ControllerBase, HttpDelete, HttpGet, HttpPost, ProducesResponseType, CancellationToken, Guid, Task (+20 more)

### Community 19 - "Components"
Cohesion: 0.08
Nodes (24): Border Radius, Buttons, Cards, Checkboxes, Chips, Colors, Components, Default Item (+16 more)

### Community 20 - "ImapEngine.cs"
Cohesion: 0.14
Nodes (11): Miautrix.Mail.Protocols.Imap, Miautrix.Mail.Storage, Miautrix.Mail.Identity, Miautrix.Mail.Seeder, Miautrix.Mail.SecurityTests.Identity, Miautrix.Mail.ProtocolTests.Imap, konscious_security_cryptography, SecurityEventCodes (+3 more)

### Community 21 - "Miautrix.Mail.Domain.csproj"
Cohesion: 0.12
Nodes (15): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+7 more)

### Community 22 - "Membership"
Cohesion: 0.14
Nodes (13): Membership, RoleId, UserId, Permission, Code, Name, RolePermission, PermissionId (+5 more)

### Community 23 - "Miautrix.Mail.Web.csproj"
Cohesion: 0.25
Nodes (6): Microsoft.AspNetCore.OpenApi (10.0.4), Microsoft.OpenApi (2.7.5), Microsoft.NET.Sdk.Web, net10.0, Microsoft.NET.Sdk, net10.0

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
Cohesion: 0.24
Nodes (9): ConcurrentDictionary, HashSet, HttpContext, RequestDelegate, Task, IdempotencyMiddleware, CapturedResponse, IIdempotencyStore (+1 more)

### Community 29 - "Miautrix.Mail.Web/Program.cs"
Cohesion: 0.23
Nodes (5): Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.Application.Queue, Miautrix.Mail.Security, Miautrix.Mail.Web.Contracts

### Community 30 - "SmtpDeliveryAttempt"
Cohesion: 0.15
Nodes (13): DateTimeOffset, EntityBase, CreatedAt, Id, UpdatedAt, SmtpDeliveryAttempt, AttemptedAt, AttemptNumber (+5 more)

### Community 31 - "Mailbox"
Cohesion: 0.12
Nodes (16): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Mailbox (+8 more)

### Community 32 - "Miautrix.Mail.SecurityTests.csproj"
Cohesion: 0.14
Nodes (12): Konscious.Security.Cryptography.Argon2 (1.3.1), Otp.NET (1.4.1), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4) (+4 more)

### Community 33 - "MailQueueApiTests"
Cohesion: 0.29
Nodes (7): IClassFixture, Program, Fact, Guid, Task, MailQueueApiTests, WebApplicationFactory

### Community 34 - ".BuildModel"
Cohesion: 0.33
Nodes (5): ModelSnapshot, DateTimeOffset, Guid, ModelBuilder, AppDbContextModelSnapshot

### Community 35 - "devDependencies"
Cohesion: 0.06
Nodes (33): dependencies, react, react-dom, @xyflow/react, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react (+25 more)

### Community 36 - "Miautrix.Mail.UnitTests.csproj"
Cohesion: 0.18
Nodes (9): NetArchTest.eNhancedEdition (1.4.5), net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4) (+1 more)

### Community 37 - "MailFlowRule"
Cohesion: 0.33
Nodes (6): MailFlowRule, ActionsJson, ConditionsJson, IsEnabled, Name, Priority

### Community 38 - "MalwareVerdict"
Cohesion: 0.33
Nodes (6): MalwareVerdict, Engine, IsMalware, Recipient, Sender, ThreatName

### Community 39 - "Miautrix.Mail.Persistence.csproj"
Cohesion: 0.15
Nodes (11): Microsoft.EntityFrameworkCore.Design (10.0.4), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, net10.0 (+3 more)

### Community 40 - "DkimKey"
Cohesion: 0.40
Nodes (5): DkimKey, DomainName, PrivateKeyPem, PublicKeyPem, Selector

### Community 41 - "devDependencies"
Cohesion: 0.05
Nodes (42): dompurify, oxlint, @types/dompurify, @types/node, dependencies, react, react-dom, devDependencies (+34 more)

### Community 42 - "format-status.js"
Cohesion: 0.50
Nodes (3): fs, tasks, ref_fs

### Community 45 - "SimulateSampleMessage"
Cohesion: 0.20
Nodes (10): Dictionary, SimulateRuleRequest, SampleMessage, SimulateSampleMessage, HasAttachment, Headers, Recipient, Sender (+2 more)

### Community 46 - "AI Build Instructions"
Cohesion: 0.20
Nodes (10): 1 · Your role, 2 · Token compliance, 3 · Component recipes, 4 · Hard constraints, 5 · Before you finish — verify, AI Build Instructions, Buttons, Cards (+2 more)

### Community 47 - "devDependencies"
Cohesion: 0.06
Nodes (31): dependencies, react, react-dom, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @types/react (+23 more)

### Community 50 - "InMemorySecurityEventSink"
Cohesion: 0.07
Nodes (33): otpnet, SessionToken, Argon2idPasswordHasher, IPasswordHasher, DateTimeOffset, Guid, IReadOnlyList, List (+25 more)

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

### Community 55 - ".TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData"
Cohesion: 0.18
Nodes (14): Miautrix.Mail.IntegrationTests.Licensing, Miautrix.Mail.Licensing, AppDbContext, CancellationToken, Guid, Message, Task, ILicenseQuotaEnforcer (+6 more)

### Community 69 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 70 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

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
Cohesion: 0.18
Nodes (7): Miautrix.Mail.Application.Users, IUserService, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, User

### Community 83 - "RequestIdMiddleware"
Cohesion: 0.40
Nodes (4): HttpContext, RequestDelegate, Task, RequestIdMiddleware

### Community 84 - "4. Buttons"
Cohesion: 0.50
Nodes (4): 4. Buttons, Disabled / Muted, Outline, Primary Iris

### Community 85 - "compilerOptions"
Cohesion: 0.10
Nodes (19): node, compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, noEmit (+11 more)

### Community 86 - ".RestoreBackupAsync"
Cohesion: 0.16
Nodes (18): AuditLog, DateTimeOffset, Dictionary, DomainEntity, Folder, List, QuarantineItem, SmtpQueueItem (+10 more)

### Community 88 - ".StageAndSwitchAsync"
Cohesion: 0.19
Nodes (10): Miautrix.Mail.Infrastructure.Deployment, Miautrix.Mail.IntegrationTests.BlueGreen, CancellationToken, Func, Task, DeploymentManager, IDeploymentManager, Fact (+2 more)

### Community 89 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+10 more)

### Community 90 - "Miautrix.Mail.IntegrationTests.csproj"
Cohesion: 0.11
Nodes (15): coverlet.collector (6.0.4), Microsoft.AspNetCore.Mvc.Testing (10.0.4), Microsoft.EntityFrameworkCore.InMemory (10.0.4), Microsoft.NET.Test.Sdk (17.14.1), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), net10.0 (+7 more)

### Community 91 - "SmtpQueueItem"
Cohesion: 0.15
Nodes (14): SmtpQueueItem, Attempts, LastAttemptAt, LastError, NextAttemptAt, RawMessage, Recipient, Sender (+6 more)

### Community 92 - "Miautrix Mail Server - Production Deployment Guide (Debian LXC)"
Cohesion: 0.13
Nodes (14): 1.1. Install System Dependencies & NGINX, 1.2. Configure Systemd Service for .NET Backend, 1.3. Configure NGINX for Cloudflare Tunnel (`mail.miautrix.tech`), 2.1. Publish .NET 10 Self-Contained Binary, 2.2. Build Frontends (Web Admin & Webmail), 2.3. Apply Database Migrations to `miautrix-mail-pro`, 3.1. Copy Published Files using Windows Built-in `scp`, Architecture & Configuration Summary (+6 more)

### Community 93 - "Miautrix.Mail.Cli.Commands"
Cohesion: 0.17
Nodes (11): Miautrix.Mail.Cli.Commands, Miautrix.Mail.Infrastructure.Backup, Miautrix.Mail.Cli, AppDbContext, CancellationToken, CommandContext, Task, RestoreCommand (+3 more)

### Community 94 - ".When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry"
Cohesion: 0.26
Nodes (8): CancellationToken, Guid, Task, ISmtpQueueManager, SmtpQueueManager, Fact, Task, SmtpQueueDatabaseTests

### Community 95 - "MailboxCreateSettings"
Cohesion: 0.16
Nodes (12): AppDbContext, CancellationToken, CommandContext, Guid, ITenantAuthorizationHelper, Task, MailboxCreateCommand, MailboxCreateSettings (+4 more)

### Community 96 - "SmtpResponse"
Cohesion: 0.27
Nodes (9): SmtpResponse, IsSuccess, CancellationToken, Guid, QueueItem, Response, Task, ISmtpSubmissionHandler (+1 more)

### Community 97 - "MailboxDeleteSettings"
Cohesion: 0.18
Nodes (11): AppDbContext, CancellationToken, CommandContext, Guid, ITenantAuthorizationHelper, Task, MailboxDeleteCommand, MailboxDeleteSettings (+3 more)

### Community 98 - "Miautrix.Mail.Persistence"
Cohesion: 0.18
Nodes (7): Miautrix.Mail.Search, Miautrix.Mail.IntegrationTests.Search, Miautrix.Mail.Persistence, Miautrix.Mail.Protocols.Sieve, Miautrix.Mail.ProtocolTests.Sieve, microsoft_entityframeworkcore_design, system_diagnostics

### Community 99 - "Miautrix.Mail.Cli.csproj"
Cohesion: 0.17
Nodes (10): Microsoft.Extensions.Configuration (10.0.4), Microsoft.Extensions.Configuration.EnvironmentVariables (10.0.4), Microsoft.Extensions.DependencyInjection (10.0.4), Spectre.Console (0.55.0), Spectre.Console.Cli (0.55.0), net10.0, Microsoft.NET.Sdk, net10.0 (+2 more)

### Community 100 - "DomainListCommand"
Cohesion: 0.20
Nodes (10): AppDbContext, CancellationToken, CommandContext, Guid, ITenantAuthorizationHelper, Task, DomainListCommand, DomainListSettings (+2 more)

### Community 101 - "MailboxListCommand"
Cohesion: 0.20
Nodes (10): AppDbContext, CancellationToken, CommandContext, Guid, ITenantAuthorizationHelper, Task, MailboxListCommand, MailboxListSettings (+2 more)

### Community 102 - ".HandleDataAsync"
Cohesion: 0.35
Nodes (7): CancellationToken, Guid, QueueItem, Response, Task, ISmtpInboundHandler, SmtpInboundHandler

### Community 103 - "CommandSettings"
Cohesion: 0.22
Nodes (9): AsyncCommand, CommandSettings, AppDbContext, CancellationToken, CommandContext, Task, BackupCommand, BackupSettings (+1 more)

### Community 104 - "QuarantineReleaseCommand"
Cohesion: 0.24
Nodes (8): AppDbContext, CancellationToken, CommandContext, Guid, Task, QuarantineReleaseCommand, QuarantineReleaseSettings, Id

### Community 105 - "QueueListCommand"
Cohesion: 0.24
Nodes (8): AppDbContext, CancellationToken, CommandContext, Task, QueueListCommand, QueueListSettings, Status, Tenant

### Community 106 - "QueueRetryCommand"
Cohesion: 0.24
Nodes (8): AppDbContext, CancellationToken, CommandContext, Guid, Task, QueueRetryCommand, QueueRetrySettings, Id

### Community 107 - ".Main"
Cohesion: 0.22
Nodes (8): EfPermissionRepository, InMemorySecurityEventSink, IPermissionRepository, ISecurityEventSink, AppDbContext, ITenantAuthorizationHelper, Task, TenantAuthorizationHelper

### Community 108 - "MailQueueService"
Cohesion: 0.33
Nodes (6): InvalidOperationException, CancellationToken, DateTimeOffset, Guid, Task, MailQueueService

### Community 109 - "QuarantineListCommand"
Cohesion: 0.28
Nodes (7): AppDbContext, CancellationToken, CommandContext, Task, QuarantineListCommand, QuarantineListSettings, Tenant

### Community 110 - ".When_user_in_tenant_A_requests_mailbox_belonging_to_tenant_B_returns_404_and_does_not_leak_existence"
Cohesion: 0.36
Nodes (4): Fact, Guid, IsolationTests, MockPermissionRepo

### Community 111 - "SystemInfoCommand"
Cohesion: 0.38
Nodes (5): Command, CancellationToken, CommandContext, SystemInfoCommand, SystemInfoSettings

### Community 112 - "ExponentialBackoffWithJitterRetryPolicy"
Cohesion: 0.52
Nodes (4): Random, TimeSpan, ExponentialBackoffWithJitterRetryPolicy, IRetryPolicy

### Community 113 - ".IsDomainLocal"
Cohesion: 0.40
Nodes (3): Guid, ISmtpAuthenticator, ISmtpDomainValidator

### Community 114 - "BackupRestoreTests.cs"
Cohesion: 0.40
Nodes (3): Miautrix.Mail.IntegrationTests.Backup, AppDbContext, BackupRestoreTests

### Community 115 - "SieveScript"
Cohesion: 0.40
Nodes (5): SieveScript, Content, IsActive, MailboxId, Name

### Community 116 - "React + TypeScript + Vite"
Cohesion: 0.50
Nodes (3): Expanding the Oxlint configuration, React Compiler, React + TypeScript + Vite

### Community 118 - "Alias"
Cohesion: 0.67
Nodes (3): Alias, Address, TargetAddress

## Knowledge Gaps
- **615 isolated node(s):** `Architecture & Configuration Summary`, `1.1. Install System Dependencies & NGINX`, `1.2. Configure Systemd Service for .NET Backend`, `1.3. Configure NGINX for Cloudflare Tunnel (`mail.miautrix.tech`)`, `2.1. Publish .NET 10 Self-Contained Binary` (+610 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 817 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **18 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AppDbContext` connect `AppDbContext` to `.Main`, `QuarantineItem`, `ImapSession`, `Message`, `TenantScopedEntityBase`, `.SimulateAsync`, `SieveTests`, `Guid`, `Membership`, `SmtpDeliveryAttempt`, `Mailbox`, `MailQueueApiTests`, `MailFlowRule`, `MalwareVerdict`, `DkimKey`, `Attachment`, `EfPermissionRepository`, `SmtpQueueItem`, `.When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry`, `Miautrix.Mail.Persistence`, `MailQueueService`, `SieveScript`, `Alias`?**
  _High betweenness centrality (0.151) - this node is a cross-community bridge._
- **Why does `Mailbox` connect `webmail/src/App.tsx` to `.TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData`?**
  _High betweenness centrality (0.071) - this node is a cross-community bridge._
- **Why does `Mailbox` connect `Mailbox` to `AppDbContext`, `ImapSession`, `Message`, `TenantScopedEntityBase`, `SieveTests`, `Guid`, `.When_user_in_tenant_A_requests_mailbox_belonging_to_tenant_B_returns_404_and_does_not_leak_existence`, `.RestoreBackupAsync`, `.TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData`, `MailboxCreateSettings`?**
  _High betweenness centrality (0.062) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `SmtpQueueItem` (e.g. with `.When_cross_tenant_retry_is_attempted_returns_404_not_found()` and `.When_listing_queue_returns_envelope_with_data_and_meta()`) actually correct?**
  _`SmtpQueueItem` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Architecture & Configuration Summary`, `1.1. Install System Dependencies & NGINX`, `1.2. Configure Systemd Service for .NET Backend` to the rest of the system?**
  _615 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Miautrix.Mail.sln` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `AppDbContext` be split into smaller, more focused modules?**
  _Cohesion score 0.03571428571428571 - nodes in this community are weakly interconnected._