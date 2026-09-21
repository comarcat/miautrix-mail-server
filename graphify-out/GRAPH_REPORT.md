# Graph Report - miautrix-mail-server  (2026-09-20)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 2518 nodes · 5551 edges · 189 communities (140 shown, 40 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 467 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `70efee75`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- AdminService
- Miautrix.Mail.sln
- .AssertMailboxAccess
- Miautrix.Mail.Persistence.Migrations
- webmail/src/App.tsx
- .Main
- MailQueueService
- AppDbContext
- Miautrix.Mail.Persistence
- SpamVerdict
- ImapSession
- admin/src/App.tsx
- Iris Pay
- .SimulateAsync
- AdminApiClient
- system
- client.ts
- TenantScopedEntityBase
- Guid
- DkimService
- .Authenticate
- User
- AuthApiTests
- Miautrix Mail Server
- Guid
- Folder
- compilerOptions
- SmtpDeliveryAttempt
- compilerOptions
- Domain
- MailApiTests.cs
- AuthResult
- Mailbox
- Message
- .TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData
- .StageAndSwitchAsync
- MockSmtpQueueManager
- Miautrix Mail Server — Implementation Blueprint
- compilerOptions
- .IssueSessionTokens
- SmtpQueueItem
- SieveTests
- ApiResponse
- Components
- compilerOptions
- AuthService
- IAuthService
- DomainController
- RuleController
- UserController
- devDependencies
- devDependencies
- compilerOptions
- devDependencies
- Miautrix Mail Server - Production Deployment Guide (Debian LXC)
- .When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry
- webmail/package.json
- .SearchAsync
- Miautrix.Mail.Identity
- QuarantineItem
- .HandleDataAsync
- .Get
- SystemController
- typescript
- dependencies
- Miautrix Mail Server
- MailboxCreateSettings
- .Get
- .SeedTenantAndUserAsync
- RuleDesignerScreen.tsx
- IRequestContextAccessor
- MailboxDeleteSettings
- .HandleSubmissionAsync
- Epic 2 — Mail transport and policy
- DomainListCommand
- MailboxListCommand
- AddCatalogFields
- admin/package.json
- include
- AsyncCommand
- Epic 1 — Core platform
- Miautrix Mail Server
- Miautrix Mail Server
- SystemInfoCommand.cs
- CommandSettings
- ControllerBase
- PulsePoint
- desktop/package.json
- 5. Recommended Technology Stack
- package.json
- Miautrix Mail Server
- QuarantineReleaseCommand
- QueueListCommand
- QueueRetryCommand
- @testing-library/jest-dom
- AGENTS.md — Miautrix Mail Server
- AGENTS.md — Miautrix Mail Server
- Miautrix Mail Server — Errors, Issues & Review Log
- ExponentialBackoffWithJitterRetryPolicy
- RestoreCommand
- .IsDomainLocal
- README.md
- Epic 3 — Surfaces
- Epic 4 — Operations
- Rule: secrets and logging
- Rule: secrets and logging
- 3. Core Architectural Principles
- 9. Identity Architecture
- .BuildModel
- MailFlowRule
- MalwareVerdict
- ref_vitejs_plugin_react
- Section 19 — Verify commands
- Section 4 — Stack
- Section 5 — Data model
- Section 9 — Build order
- verify-task
- verify-task
- .Main
- DkimKey
- LoginStatus
- .Up
- .Up
- .Up
- .Up
- BackupScreen.tsx
- LicensingScreen.tsx
- ref_testing_library_jest_dom_vitest
- lib
- Section 2 — Scope
- Section 20 — Verification status (READ FIRST)
- format-status.js
- .ApplyMigrations
- update-prod-database.sh
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- React + TypeScript + Vite
- versions.mjs
- desktop/tsconfig.json
- generate_migration.sh
- 13. Mail Flow Architecture
- 14. Mail Flow Rule Engine
- init-database-scratch.sh
- update-database.sh
- webmail/tsconfig.json
- workspace/.claude/rules/tenancy.md
- .claude/rules/tenancy.md
- fix_tasks.py
- ApiExceptionMiddleware
- AuthService
- EfPermissionRepository
- HttpContext
- IAuthService
- IMailQueueService
- InMemoryIdempotencyStore
- InMemorySecurityEventSink
- IPasswordHasher
- IPermissionRepository
- ISecurityEventSink
- ISessionManager
- ITotpService
- JsonSerializerOptions
- MailQueueService
- MigrationBuilder
- RequestDelegate
- RequestIdMiddleware
- ITenantAuthorizationHelper
- ITenantAuthorizationHelper
- ITenantAuthorizationHelper
- HashSet
- IIdempotencyStore
- IIdempotencyStore
- ITenantAuthorizationHelper
- TenantAuthorizationHelper
- WebApplicationFactory

## God Nodes (most connected - your core abstractions)
1. `AppDbContext` - 152 edges
2. `AdminApiClient` - 61 edges
3. `AdminService` - 59 edges
4. `TenantScopedEntityBase` - 55 edges
5. `Miautrix.Mail.Persistence` - 54 edges
6. `Miautrix.Mail.Domain` - 46 edges
7. `IAdminService` - 42 edges
8. `ApiResponse` - 37 edges
9. `react` - 35 edges
10. `Mailbox` - 33 edges

## Surprising Connections (you probably didn't know these)
- `ImapTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Imap/ImapTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `MailFlowTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `IdentityTests` --references--> `ISessionManager`  [EXTRACTED]
  tests/Miautrix.Mail.SecurityTests/Identity/IdentityTests.cs → src/Miautrix.Mail.Identity/SessionManager.cs
- `AuthApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/AuthApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `MailApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/MailApiTests.cs → src/Miautrix.Mail.Web/Program.cs

## Import Cycles
- None detected.

## Communities (189 total, 40 thin omitted)

### Community 0 - "AdminService"
Cohesion: 0.06
Nodes (51): LookupClient, DateTimeOffset, Dictionary, Guid, IReadOnlyList, List, AdminUserDto, AuditFilter (+43 more)

### Community 1 - "Miautrix.Mail.sln"
Cohesion: 0.07
Nodes (44): coverlet.collector (6.0.4), DnsClient (1.8.0), Microsoft.AspNetCore.Mvc.Testing (10.0.4), Microsoft.AspNetCore.OpenApi (10.0.4), Microsoft.EntityFrameworkCore.InMemory (10.0.4), Microsoft.Extensions.Configuration (10.0.4), Microsoft.Extensions.Configuration.EnvironmentVariables (10.0.4), Microsoft.Extensions.DependencyInjection (10.0.4) (+36 more)

### Community 2 - ".AssertMailboxAccess"
Cohesion: 0.08
Nodes (41): CancellationToken, Guid, IReadOnlyList, Task, IMailboxService, CancellationToken, Guid, Task (+33 more)

### Community 3 - "Miautrix.Mail.Persistence.Migrations"
Cohesion: 0.06
Nodes (33): Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, Migration, npgsql_entityframeworkcore_postgresql_metadata, InitialCreate, Guid (+25 more)

### Community 4 - "webmail/src/App.tsx"
Cohesion: 0.06
Nodes (38): ref_dompurify, ref_react_dom_client, ref_testing_library_react, ref_vitest, App(), INITIAL_CONTACTS, INITIAL_EVENTS, INITIAL_MAILBOXES (+30 more)

### Community 5 - ".Main"
Cohesion: 0.05
Nodes (41): ApiBehaviorOptions, Argon2idPasswordHasher, ConcurrentDictionary, HashSet, ILogger, SessionManager, BackupOptions, IReadOnlyDictionary (+33 more)

### Community 6 - "MailQueueService"
Cohesion: 0.06
Nodes (41): CancellationToken, Guid, Task, IMailQueueService, CancellationToken, DateTimeOffset, Guid, Task (+33 more)

### Community 7 - "AppDbContext"
Cohesion: 0.04
Nodes (56): DbContext, DbSet, IDesignTimeDbContextFactory, ModelBuilder, AppDbContext, Aliases, ApiKeys, ApplicationPasswords (+48 more)

### Community 8 - "Miautrix.Mail.Persistence"
Cohesion: 0.10
Nodes (27): Miautrix.Mail.IntegrationTests, Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.IntegrationTests.Licensing, Miautrix.Mail.Cli.Commands, Miautrix.Mail.Infrastructure.Backup, Miautrix.Mail.AntiSpam, Miautrix.Mail.IntegrationTests.AntiSpam, Miautrix.Mail.SecurityTests.Isolation (+19 more)

### Community 9 - "SpamVerdict"
Cohesion: 0.07
Nodes (38): CancellationToken, Guid, List, QuarantineItem, Task, IQuarantineService, QuarantineService, Guid (+30 more)

### Community 10 - "ImapSession"
Cohesion: 0.09
Nodes (29): DomainEntity, CancellationToken, GeneratedRegex, Guid, IReadOnlyList, Regex, Stream, Task (+21 more)

### Community 11 - "admin/src/App.tsx"
Cohesion: 0.09
Nodes (28): apiClient, AntiMalwareScreen(), AntiSpamScreen(), ChangePasswordView(), ChangePasswordViewProps, DashboardScreen(), DashboardStats, IdentityScreen() (+20 more)

### Community 12 - "Iris Pay"
Cohesion: 0.05
Nodes (47): 1. Atmosphere, 1 · Your role, 2. Palette, 2 · Token compliance, 3 · Component recipes, 3. Typography, 4. Buttons, 4 · Hard constraints (+39 more)

### Community 13 - ".SimulateAsync"
Cohesion: 0.10
Nodes (28): Miautrix.Mail.MailFlow, IDisposable, IServiceCollection, IServiceProvider, ITypeRegistrar, ITypeResolver, Func, TypeRegistrar (+20 more)

### Community 14 - "AdminApiClient"
Cohesion: 0.12
Nodes (5): AdminApiClient, AdminUserItem, ApiResponse, CreateSharedMailboxRequest, SharedMailbox

### Community 15 - "system"
Cohesion: 0.21
Nodes (16): Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.Application.Queue, Miautrix.Mail.Application.Auth, Miautrix.Mail.Application.Admin, Miautrix.Mail.Web.Contracts, Miautrix.Mail.Application.Mail, microsoft_aspnetcore_http (+8 more)

### Community 16 - "client.ts"
Cohesion: 0.10
Nodes (27): DomainsScreen(), DomainsScreenProps, LogsScreen(), LogsScreenProps, MailFlowScreen(), MailFlowScreenProps, SystemScreen(), SystemScreenProps (+19 more)

### Community 17 - "TenantScopedEntityBase"
Cohesion: 0.13
Nodes (36): Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, Deploy, DomainDnsSetting (+28 more)

### Community 18 - "Guid"
Cohesion: 0.13
Nodes (12): Guid, EfPermissionRepository, Guid, ISecurityEventSink, IPermissionRepository, LastOwnerDemotionException, ResourceNotFoundException, TenantAuthorizationHelper (+4 more)

### Community 19 - "DkimService"
Cohesion: 0.13
Nodes (13): Body, Headers, RSA, Dictionary, GeneratedRegex, List, Regex, DkimKeyPair (+5 more)

### Community 20 - ".Authenticate"
Cohesion: 0.15
Nodes (16): IPasswordHasher, DateTimeOffset, Guid, IReadOnlyList, List, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink (+8 more)

### Community 21 - "User"
Cohesion: 0.07
Nodes (26): Miautrix.Mail.Application.Users, IReadOnlyCollection, IUserService, Role, Membership, RoleId, UserId, Permission (+18 more)

### Community 22 - "AuthApiTests"
Cohesion: 0.17
Nodes (12): Exception, IClassFixture, Fact, Guid, Task, WebApplicationFactory, AuthApiTests, Fact (+4 more)

### Community 23 - "Miautrix Mail Server"
Cohesion: 0.08
Nodes (24): 10. Authentication and Authorization, 11. MFA, 12. Security Event Codes, 15. Graphical Mail Flow Designer, 16. Mail Flow Simulator, 17. Mail Flow Explorer, 18. Restricted Device Relay, 19. Anti-Spam Architecture (+16 more)

### Community 24 - "Guid"
Cohesion: 0.08
Nodes (24): Guid, Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes, StoragePath (+16 more)

### Community 25 - "Folder"
Cohesion: 0.14
Nodes (18): Folder, MailboxId, Name, UidNext, UidValidity, Tenant, CancellationToken, DateTimeOffset (+10 more)

### Community 26 - "compilerOptions"
Cohesion: 0.09
Nodes (21): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+13 more)

### Community 27 - "SmtpDeliveryAttempt"
Cohesion: 0.09
Nodes (22): DateTimeOffset, BackupJob, ArchivePath, CompletedAt, ErrorMessage, Name, SizeBytes, Status (+14 more)

### Community 28 - "compilerOptions"
Cohesion: 0.09
Nodes (21): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+13 more)

### Community 29 - "Domain"
Cohesion: 0.12
Nodes (15): Miautrix.Mail.Protocols.Imap, Miautrix.Mail.Storage, Miautrix.Mail.Seeder, Miautrix.Mail.ProtocolTests.Imap, Domain, DkimPublicKey, DkimSelector, DmarcRecord (+7 more)

### Community 30 - "MailApiTests.cs"
Cohesion: 0.18
Nodes (12): Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Web, Miautrix.Mail.IntegrationTests.Api, Miautrix.Mail.ProtocolTests.Smtp, Miautrix.Mail.Queue, Miautrix.Mail.Protocols.Smtp, Miautrix.Mail.IntegrationTests.Smtp, microsoft_aspnetcore_mvc_testing (+4 more)

### Community 31 - "AuthResult"
Cohesion: 0.15
Nodes (15): DateTimeOffset, Guid, IReadOnlyList, AuthResult, AuthStatus, Failed, LockedOut, MfaRequired (+7 more)

### Community 32 - "Mailbox"
Cohesion: 0.10
Nodes (18): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Mailbox (+10 more)

### Community 33 - "Message"
Cohesion: 0.10
Nodes (19): Message, BodyHtml, BodyText, ContentHash, Date, Flags, FolderId, IsRead (+11 more)

### Community 34 - ".TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData"
Cohesion: 0.24
Nodes (11): InvalidOperationException, CancellationToken, Guid, Mailbox, Task, ILicenseQuotaEnforcer, LicenseQuotaEnforcer, LicenseQuotaException (+3 more)

### Community 35 - ".StageAndSwitchAsync"
Cohesion: 0.19
Nodes (10): Miautrix.Mail.Infrastructure.Deployment, Miautrix.Mail.IntegrationTests.BlueGreen, CancellationToken, Func, Task, DeploymentManager, IDeploymentManager, Fact (+2 more)

### Community 36 - "MockSmtpQueueManager"
Cohesion: 0.23
Nodes (11): CancellationToken, Fact, Guid, HashSet, List, Task, MockAuthenticator, MockDomainValidator (+3 more)

### Community 37 - "Miautrix Mail Server — Implementation Blueprint"
Cohesion: 0.12
Nodes (17): Miautrix Mail Server — Implementation Blueprint, Provider seams, Section 0 — How to use this blueprint, Section 10 — Workspace, Section 11 — Testing, Section 12 — Observability, Section 13 — Deployment, Section 14 — Licensing (+9 more)

### Community 38 - "compilerOptions"
Cohesion: 0.12
Nodes (17): node, compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, noEmit (+9 more)

### Community 39 - ".IssueSessionTokens"
Cohesion: 0.26
Nodes (10): SessionToken, DateTimeOffset, Guid, RefreshToken, TimeSpan, AuthToken, ISessionManager, RefreshTokenInfo (+2 more)

### Community 40 - "SmtpQueueItem"
Cohesion: 0.15
Nodes (14): SmtpQueueItem, Attempts, LastAttemptAt, LastError, NextAttemptAt, RawMessage, Recipient, Sender (+6 more)

### Community 41 - "SieveTests"
Cohesion: 0.21
Nodes (10): CancellationToken, Guid, HashSet, Task, SieveParser, SieveParseResult, SieveScriptService, Fact (+2 more)

### Community 42 - "ApiResponse"
Cohesion: 0.23
Nodes (12): ApiResponse, PaginationMeta, CancellationToken, Guid, HttpGet, HttpPost, HttpPut, IReadOnlyList (+4 more)

### Community 43 - "Components"
Cohesion: 0.12
Nodes (16): Buttons, Cards, Checkboxes, Chips, Components, Default Item, Disabled State, Filter Chip (+8 more)

### Community 44 - "compilerOptions"
Cohesion: 0.12
Nodes (16): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+8 more)

### Community 45 - "AuthService"
Cohesion: 0.26
Nodes (9): CancellationToken, Guid, IPasswordHasher, IReadOnlyList, ISecurityEventSink, ISessionManager, ITotpService, Task (+1 more)

### Community 46 - "IAuthService"
Cohesion: 0.28
Nodes (11): IAuthService, CancellationToken, HttpGet, HttpPost, IResult, ProducesResponseType, Task, AuthController (+3 more)

### Community 47 - "DomainController"
Cohesion: 0.33
Nodes (10): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+2 more)

### Community 48 - "RuleController"
Cohesion: 0.33
Nodes (10): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+2 more)

### Community 49 - "UserController"
Cohesion: 0.33
Nodes (10): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+2 more)

### Community 50 - "devDependencies"
Cohesion: 0.13
Nodes (15): devDependencies, jsdom, @testing-library/react, @types/react, vite, vitest, jsdom, @testing-library/react (+7 more)

### Community 51 - "devDependencies"
Cohesion: 0.13
Nodes (15): @types/react-dom, @vitejs/plugin-react, @types/react-dom, @vitejs/plugin-react, devDependencies, jsdom, @types/react-dom, vite (+7 more)

### Community 52 - "compilerOptions"
Cohesion: 0.13
Nodes (15): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, module, moduleResolution, noEmit, noFallthroughCasesInSwitch (+7 more)

### Community 53 - "devDependencies"
Cohesion: 0.13
Nodes (15): dompurify, oxlint, @types/dompurify, @types/node, devDependencies, dompurify, jsdom, oxlint (+7 more)

### Community 54 - "Miautrix Mail Server - Production Deployment Guide (Debian LXC)"
Cohesion: 0.13
Nodes (14): 1.1. Install System Dependencies & NGINX, 1.2. Configure Systemd Service for .NET Backend, 1.3. Configure NGINX for Cloudflare Tunnel (`mail.miautrix.tech`), 2.1. Publish .NET 10 Self-Contained Binary, 2.2. Build Frontends (Web Admin & Webmail), 2.3. Apply Database Migrations to `miautrix-mail-pro`, 3.1. Copy Published Files using Windows Built-in `scp`, Architecture & Configuration Summary (+6 more)

### Community 55 - ".When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry"
Cohesion: 0.26
Nodes (8): CancellationToken, Guid, Task, ISmtpQueueManager, SmtpQueueManager, Fact, Task, SmtpQueueDatabaseTests

### Community 56 - "webmail/package.json"
Cohesion: 0.13
Nodes (14): dependencies, react, react-dom, react, name, private, scripts, build (+6 more)

### Community 57 - ".SearchAsync"
Cohesion: 0.37
Nodes (8): CancellationToken, DateTimeOffset, Guid, IReadOnlyList, Task, ISearchProvider, PostgreSqlSearchProvider, SearchMessageItem

### Community 58 - "Miautrix.Mail.Identity"
Cohesion: 0.18
Nodes (6): Miautrix.Mail.Identity, Miautrix.Mail.SecurityTests.Identity, konscious_security_cryptography, otpnet, Argon2idPasswordHasher, SecurityEventCodes

### Community 59 - "QuarantineItem"
Cohesion: 0.15
Nodes (13): QuarantineItem, IsDelivered, QuarantinedAt, RawMessage, ReasonsJson, Recipient, ReleasedAt, Sender (+5 more)

### Community 60 - ".HandleDataAsync"
Cohesion: 0.29
Nodes (8): CancellationToken, Guid, Response, Task, ISmtpInboundHandler, SmtpInboundHandler, SmtpResponse, IsSuccess

### Community 61 - ".Get"
Cohesion: 0.35
Nodes (9): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, IResult, ProducesResponseType, Task (+1 more)

### Community 62 - "SystemController"
Cohesion: 0.40
Nodes (8): CancellationToken, HttpGet, HttpPost, IResult, ProducesResponseType, Task, BackupActionResult, SystemController

### Community 63 - "typescript"
Cohesion: 0.17
Nodes (11): typescript, typescript, oxc, typescript, warn, plugins, rules, react/only-export-components (+3 more)

### Community 64 - "dependencies"
Cohesion: 0.18
Nodes (11): dependencies, react, react-dom, @xyflow/react, react, react-dom, dependencies, react (+3 more)

### Community 65 - "Miautrix Mail Server"
Cohesion: 0.18
Nodes (11): 1.1 Project Purpose and Business Case, 1.2 High-Level Objectives, 1. Project Initiation & Executive Summary, 2. Project Scope Management (WBS Overview), 3. Project Schedule & Milestone Management, 4. Quality Management & Verification Gates, 5. Risk Management Matrix, 6. Stakeholder & Resource Plan (+3 more)

### Community 66 - "MailboxCreateSettings"
Cohesion: 0.20
Nodes (10): CancellationToken, CommandContext, Guid, Task, MailboxCreateCommand, MailboxCreateSettings, Address, QuotaBytes (+2 more)

### Community 67 - ".Get"
Cohesion: 0.47
Nodes (7): CancellationToken, Guid, HttpGet, IResult, ProducesResponseType, Task, MailboxController

### Community 68 - ".SeedTenantAndUserAsync"
Cohesion: 0.44
Nodes (4): Fact, Guid, Task, CliTests

### Community 69 - "RuleDesignerScreen.tsx"
Cohesion: 0.20
Nodes (8): App(), initialEdges, initialNodes, RuleDesignerScreen(), RuleDesignerScreenProps, admin_src_styles_admin, ref_xyflow_react, ref_xyflow_react_dist_style_css

### Community 70 - "IRequestContextAccessor"
Cohesion: 0.27
Nodes (8): IHttpContextAccessor, Guid, HeaderRequestContextAccessor, CurrentTenantId, CurrentUserId, IRequestContextAccessor, CurrentTenantId, CurrentUserId

### Community 71 - "MailboxDeleteSettings"
Cohesion: 0.22
Nodes (9): CancellationToken, CommandContext, Guid, Task, MailboxDeleteCommand, MailboxDeleteSettings, Address, Tenant (+1 more)

### Community 72 - ".HandleSubmissionAsync"
Cohesion: 0.38
Nodes (6): CancellationToken, Guid, Response, Task, ISmtpSubmissionHandler, SmtpSubmissionHandler

### Community 73 - "Epic 2 — Mail transport and policy"
Cohesion: 0.22
Nodes (8): Epic 2 — Mail transport and policy, T10 — IMAP, storage, and attachments, T11 — ManageSieve, T12 — Search, T13 — Rule engine, simulator, explorer, T7 — SMTP listener and queue, T8 — SPF, DKIM, DMARC, T9 — Anti-spam baseline and quarantine

### Community 74 - "DomainListCommand"
Cohesion: 0.25
Nodes (8): CancellationToken, CommandContext, Guid, Task, DomainListCommand, DomainListSettings, Tenant, UserId

### Community 75 - "MailboxListCommand"
Cohesion: 0.25
Nodes (8): CancellationToken, CommandContext, Guid, Task, MailboxListCommand, MailboxListSettings, Tenant, UserId

### Community 76 - "AddCatalogFields"
Cohesion: 0.25
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddCatalogFields

### Community 77 - "admin/package.json"
Cohesion: 0.25
Nodes (7): name, scripts, build, dev, test, type, version

### Community 78 - "include"
Cohesion: 0.25
Nodes (5): include, src, vite.config.ts, include, include

### Community 79 - "AsyncCommand"
Cohesion: 0.29
Nodes (7): AsyncCommand, CancellationToken, CommandContext, Task, QuarantineListCommand, QuarantineListSettings, Tenant

### Community 80 - "Epic 1 — Core platform"
Cohesion: 0.25
Nodes (7): Epic 1 — Core platform, T1 — Solution scaffold and CI, T2 — Domain model and EF Core schema, T3 — Seed data and indexes, T4 — Identity, T5 — Authorization and tenant isolation, T6 — Audit trail

### Community 81 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Architecture rules, Commands, Data rules, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 82 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Architecture rules, Commands, Data rules, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 83 - "SystemInfoCommand.cs"
Cohesion: 0.32
Nodes (6): Command, CancellationToken, CommandContext, SystemInfoCommand, SystemInfoSettings, system_runtime_interopservices

### Community 84 - "CommandSettings"
Cohesion: 0.29
Nodes (7): CommandSettings, CancellationToken, CommandContext, Task, BackupCommand, BackupSettings, OutputPath

### Community 85 - "ControllerBase"
Cohesion: 0.25
Nodes (7): ControllerBase, CancellationToken, HttpGet, IResult, ProducesResponseType, Task, AuditController

### Community 86 - "PulsePoint"
Cohesion: 0.25
Nodes (8): Border Radius, Colors, Do's and Don'ts, Elevation, Overview, PulsePoint, Spacing, Typography

### Community 87 - "desktop/package.json"
Cohesion: 0.25
Nodes (7): name, scripts, build, dev, test, type, version

### Community 88 - "5. Recommended Technology Stack"
Cohesion: 0.25
Nodes (8): 5. Recommended Technology Stack, Backend, Caching, Database, Frontend, Logging, Scheduling, Storage

### Community 89 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 90 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (8): 📐 Architecture & Design, Build & Test Commands, 🛠️ Deploying & Updating, ⚙️ Development Environment, Miautrix Mail Server, 🚀 Project Status, 🔒 Security Invariants, 🛠️ Tech Stack & Prerequisites

### Community 91 - "QuarantineReleaseCommand"
Cohesion: 0.29
Nodes (7): CancellationToken, CommandContext, Guid, Task, QuarantineReleaseCommand, QuarantineReleaseSettings, Id

### Community 92 - "QueueListCommand"
Cohesion: 0.29
Nodes (7): CancellationToken, CommandContext, Task, QueueListCommand, QueueListSettings, Status, Tenant

### Community 93 - "QueueRetryCommand"
Cohesion: 0.29
Nodes (7): CancellationToken, CommandContext, Guid, Task, QueueRetryCommand, QueueRetrySettings, Id

### Community 94 - "@testing-library/jest-dom"
Cohesion: 0.33
Nodes (7): @testing-library/jest-dom, @testing-library/jest-dom, @testing-library/jest-dom, types, vite/client, @testing-library/jest-dom, types

### Community 95 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.29
Nodes (6): AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Provider seams, Task tracking, Things that will bite you, Where things are

### Community 96 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.29
Nodes (6): AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Provider seams, Task tracking, Things that will bite you, Where things are

### Community 97 - "Miautrix Mail Server — Errors, Issues & Review Log"
Cohesion: 0.29
Nodes (6): 1. System & OS Environment Issues, 2. Frontend & UI Runtime Issues, 3. NGINX & Ingress Routing Issues, 4. Backend & Database Integrity Checks, 5. Items to Review & Verify Later, Miautrix Mail Server — Errors, Issues & Review Log

### Community 98 - "ExponentialBackoffWithJitterRetryPolicy"
Cohesion: 0.52
Nodes (4): Random, TimeSpan, ExponentialBackoffWithJitterRetryPolicy, IRetryPolicy

### Community 99 - "RestoreCommand"
Cohesion: 0.33
Nodes (6): CancellationToken, CommandContext, Task, RestoreCommand, RestoreSettings, ArchivePath

### Community 100 - ".IsDomainLocal"
Cohesion: 0.33
Nodes (3): Guid, ISmtpAuthenticator, ISmtpDomainValidator

### Community 102 - "Epic 3 — Surfaces"
Cohesion: 0.33
Nodes (5): Epic 3 — Surfaces, T14 — API contract and OpenAPI, T15 — Web admin GUI, T16 — Webmail, T17 — CLI

### Community 103 - "Epic 4 — Operations"
Cohesion: 0.33
Nodes (5): Epic 4 — Operations, T18 — Desktop admin application, T19 — Backup and restore, T20 — Blue/green update and the migrations ladder, T21 — Security hardening and licence gating

### Community 104 - "Rule: secrets and logging"
Cohesion: 0.33
Nodes (5): Environment, Never commit, Never log, Never store in plaintext, Rule: secrets and logging

### Community 105 - "Rule: secrets and logging"
Cohesion: 0.33
Nodes (5): Environment, Never commit, Never log, Never store in plaintext, Rule: secrets and logging

### Community 106 - "3. Core Architectural Principles"
Cohesion: 0.33
Nodes (6): 3.1 Security First, 3.2 Modular Design, 3.3 API First, 3.4 Configuration as Data, 3.5 Provider Abstraction, 3. Core Architectural Principles

### Community 107 - "9. Identity Architecture"
Cohesion: 0.33
Nodes (6): 9.1 Local Accounts, 9.2 Google Workspace, 9.3 Microsoft Entra ID, 9.4 LDAP / Active Directory, 9.5 Recommended Groups, 9. Identity Architecture

### Community 108 - ".BuildModel"
Cohesion: 0.33
Nodes (5): ModelSnapshot, DateTimeOffset, Guid, ModelBuilder, AppDbContextModelSnapshot

### Community 109 - "MailFlowRule"
Cohesion: 0.33
Nodes (6): MailFlowRule, ActionsJson, ConditionsJson, IsEnabled, Name, Priority

### Community 110 - "MalwareVerdict"
Cohesion: 0.33
Nodes (6): MalwareVerdict, Engine, IsMalware, Recipient, Sender, ThreatName

### Community 112 - "Section 19 — Verify commands"
Cohesion: 0.40
Nodes (5): 19.1 Where they run, 19.2 Settings allowlist, 19.3 Toolchain prerequisites, 19.6 Verify-critical configuration, Section 19 — Verify commands

### Community 113 - "Section 4 — Stack"
Cohesion: 0.40
Nodes (5): 4.1 Backend — NuGet, verified 2026-09-16, 4.2 Frontend — npm, verified 2026-09-16, 4.3 Testing — NuGet, verified, 4.4 Runtime — locally confirmed, Section 4 — Stack

### Community 114 - "Section 5 — Data model"
Cohesion: 0.40
Nodes (5): 5.1 Conventions, 5.2 Entities, 5.3 Isolation, 5.4 Migrations, Section 5 — Data model

### Community 115 - "Section 9 — Build order"
Cohesion: 0.40
Nodes (5): Epic 1 — Core platform (T1–T6), Epic 2 — Mail transport and policy (T7–T13), Epic 3 — Surfaces (T14–T17), Epic 4 — Operations (T18–T21), Section 9 — Build order

### Community 116 - "verify-task"
Cohesion: 0.40
Nodes (4): Also check, Procedure, Reporting rules, verify-task

### Community 117 - "verify-task"
Cohesion: 0.40
Nodes (4): Also check, Procedure, Reporting rules, verify-task

### Community 118 - ".Main"
Cohesion: 0.40
Nodes (4): InMemorySecurityEventSink, ISecurityEventSink, Task, Program

### Community 119 - "DkimKey"
Cohesion: 0.40
Nodes (5): DkimKey, DomainName, PrivateKeyPem, PublicKeyPem, Selector

### Community 120 - "LoginStatus"
Cohesion: 0.40
Nodes (5): LoginStatus, Failed, LockedOut, MfaRequired, Success

### Community 121 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 122 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 123 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 124 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 125 - "BackupScreen.tsx"
Cohesion: 0.50
Nodes (3): BackupScreen(), BackupScreenProps, BackupJobItem

### Community 126 - "LicensingScreen.tsx"
Cohesion: 0.50
Nodes (3): LicensingScreen(), LicensingScreenProps, LicensingInfo

### Community 128 - "lib"
Cohesion: 0.50
Nodes (4): lib, DOM, DOM.Iterable, ES2022

### Community 129 - "Section 2 — Scope"
Cohesion: 0.50
Nodes (4): 2.1 In scope for v1, 2.2 Out of scope for v1, 2.3 Non-goals, Section 2 — Scope

### Community 130 - "Section 20 — Verification status (READ FIRST)"
Cohesion: 0.50
Nodes (4): A correction to the record, Section 20 — Verification status (READ FIRST), What was NOT verified, What was verified

### Community 131 - "format-status.js"
Cohesion: 0.50
Nodes (3): fs, tasks, ref_fs

### Community 133 - "update-prod-database.sh"
Cohesion: 0.50
Nodes (3): ASPNETCORE_ENVIRONMENT, MIAUTRIX_DB_CONNECTION, update-prod-database.sh script

### Community 134 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 135 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 136 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 137 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 138 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 139 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 140 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 141 - "React + TypeScript + Vite"
Cohesion: 0.50
Nodes (3): Expanding the Oxlint configuration, React Compiler, React + TypeScript + Vite

### Community 145 - "13. Mail Flow Architecture"
Cohesion: 0.67
Nodes (3): 13. Mail Flow Architecture, Inbound, Outbound

### Community 146 - "14. Mail Flow Rule Engine"
Cohesion: 0.67
Nodes (3): 14. Mail Flow Rule Engine, Actions, Conditions

## Knowledge Gaps
- **698 isolated node(s):** `ChangePasswordViewProps`, `DashboardStats`, `LayoutProps`, `MenuItem`, `LoginScreenProps` (+693 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 989 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **40 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AppDbContext` connect `AppDbContext` to `Mailbox`, `Message`, `SmtpQueueItem`, `QuarantineItem`, `SpamVerdict`, `MailFlowRule`, `MalwareVerdict`, `TenantScopedEntityBase`, `User`, `DkimKey`, `Guid`, `Folder`, `SmtpDeliveryAttempt`, `Domain`?**
  _High betweenness centrality (0.007) - this node is a cross-community bridge._
- **Why does `QueueListCommand` connect `QueueListCommand` to `AppDbContext`, `AsyncCommand`?**
  _High betweenness centrality (0.001) - this node is a cross-community bridge._
- **Why does `QueueRetryCommand` connect `QueueRetryCommand` to `AppDbContext`, `AsyncCommand`?**
  _High betweenness centrality (0.001) - this node is a cross-community bridge._
- **What connects `ChangePasswordViewProps`, `DashboardStats`, `LayoutProps` to the rest of the system?**
  _698 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `AdminService` be split into smaller, more focused modules?**
  _Cohesion score 0.06368159203980099 - nodes in this community are weakly interconnected._
- **Should `Miautrix.Mail.sln` be split into smaller, more focused modules?**
  _Cohesion score 0.0695970695970696 - nodes in this community are weakly interconnected._
- **Should `.AssertMailboxAccess` be split into smaller, more focused modules?**
  _Cohesion score 0.08485540334855403 - nodes in this community are weakly interconnected._