# Graph Report - miautrix-mail-server  (2026-09-22)

## Corpus Check
- 318 files · ~4,712,745 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 4416 nodes · 12430 edges · 206 communities (178 shown, 18 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 1549 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `62d16707`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- AdminService
- Miautrix.Mail.Worker.csproj
- .AssertMailboxAccess
- system
- webmail/src/App.tsx
- .Main
- MailQueueService
- AppDbContext
- Miautrix.Mail.Persistence
- ParsedInboundMessage
- ImapSession
- client.ts
- AI Build Instructions
- .SimulateAsync
- AdminApiClient
- system_threading_tasks
- UsersScreen.tsx
- TenantScopedEntityBase
- Guid
- DkimService
- InMemorySecurityEventSink
- Folder
- $_
- Miautrix Mail Server
- uN
- QuarantineItem
- compilerOptions
- SmtpDeliveryAttempt
- compilerOptions
- system_text
- TlsCertificateProvider
- f
- AuditLog
- Message
- Mailbox
- .StageAndSwitchAsync
- .HandleDataAsync
- Miautrix Mail Server — Implementation Blueprint
- compilerOptions
- ISessionManager
- SmtpQueueItem
- c
- MailboxDelegateRequest
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
- xN
- index-9M21bitv.js
- n
- pN
- Dt
- .Get
- IRequestContextAccessor
- plugins
- Ue
- Miautrix Mail Server
- MailboxCreateSettings
- ApiResponse
- Permission
- RuleDesignerScreen.tsx
- fg
- cg
- td
- Epic 2 — Mail transport and policy
- Hb
- bw
- AddCatalogFields
- Miautrix.Mail.sln
- Miautrix.Mail.Persistence.csproj
- .Main
- Epic 1 — Core platform
- Miautrix Mail Server
- Miautrix Mail Server
- Miautrix.Mail.IntegrationTests.csproj
- .RunOnePassAsync
- p2
- PulsePoint
- CloudflareApiMailTransport
- 5. Recommended Technology Stack
- package.json
- Miautrix Mail Server
- Miautrix.Mail.Application.csproj
- SystemInfoCommand.cs
- md
- .Cloudflare
- AGENTS.md — Miautrix Mail Server
- AGENTS.md — Miautrix Mail Server
- Miautrix Mail Server — Errors, Issues & Review Log
- pl
- i
- Miautrix.Mail.Identity.csproj
- Domain
- Epic 3 — Surfaces
- Epic 4 — Operations
- Rule: secrets and logging
- Rule: secrets and logging
- 3. Core Architectural Principles
- 9. Identity Architecture
- .BuildModel
- CloudflareTransportTests
- OutboundQueueDispatcher
- ref_vitejs_plugin_react
- Section 19 — Verify commands
- Section 4 — Stack
- Section 5 — Data model
- Section 9 — Build order
- verify-task
- verify-task
- Miautrix.Mail.UnitTests.csproj
- bg
- LoginStatus
- .Up
- .Up
- AddMailAndSpamColumns
- yl
- MailboxAdminApiTests
- o
- ref_testing_library_jest_dom_vitest
- Miautrix.Mail.Domain.csproj
- IAdminService
- Section 20 — Verification status (READ FIRST)
- format-status.js
- .ApplyMigrations
- update-prod-database.sh
- n
- ITotpService
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- AddUserServiceAndMailboxKind
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
- AddDomainTransportMode
- AddTenantMfaEnforced
- FixMfaEnforcedScope
- AddDomainSecurityPolicySettings
- Future implementation items
- Attachment
- ft
- Miautrix.Mail.EndToEndTests.csproj
- AddMailboxName
- .Up
- .BuildTargetModel
- C
- b
- .CreateArchiveAsync
- Z
- .BuildTargetModel
- ApiJson.cs
- Fd
- SmtpListenerService
- SharedMailboxesScreen.tsx
- DomainEntity
- ImapAuthenticatorTests
- MailQueueController
- CancellationToken
- .DeleteMailboxAsync
- WebmailApiClient
- SimulateSampleMessage
- User
- Iris Pay
- QueueScreen.test.tsx
- ImapListenerService
- SpamVerdict
- Pro tokens
- Tokens
- .Error
- EfPermissionRepository
- ApiExceptionMiddleware
- lxc-prepare-storage.sh
- ImapState
- .BuildTargetModel
- inject-flow-mail-test.sh
- lxc-install-worker-env.sh
- lxc-restart-services.sh

## God Nodes (most connected - your core abstractions)
1. `uN()` - 407 edges
2. `$_` - 382 edges
3. `AppDbContext` - 169 edges
4. `o()` - 103 edges
5. `i()` - 77 edges
6. `AdminApiClient` - 76 edges
7. `AdminService` - 71 edges
8. `Miautrix.Mail.Persistence` - 69 edges
9. `n()` - 56 edges
10. `c()` - 55 edges

## Surprising Connections (you probably didn't know these)
- `p1()` --indirect_call--> `qa()`  [INFERRED]
  publish/admin/assets/index-BLNt-0gX.js → publish/webmail/assets/index-9M21bitv.js
- `MailApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/MailApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `MailboxAdminApiTests` --references--> `IMailStorage`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/MailboxAdminApiTests.cs → src/Miautrix.Mail.Storage/MailStorage.cs
- `MailboxAdminApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/MailboxAdminApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `RecordingTransport` --implements--> `IOutboundMailTransport`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Cloudflare/OutboundQueueDispatcherTests.cs → src/Miautrix.Mail.Application/Transport/IOutboundMailTransport.cs

## Import Cycles
- None detected.

## Communities (206 total, 18 thin omitted)

### Community 0 - "AdminService"
Cohesion: 0.13
Nodes (13): LookupClient, CreateDomainRequest, CancellationToken, DateTimeOffset, Guid, IReadOnlyList, Mailbox, MailboxDelegateRequest (+5 more)

### Community 1 - "Miautrix.Mail.Worker.csproj"
Cohesion: 0.12
Nodes (16): Microsoft.Extensions.Hosting (10.0.4), Microsoft.Extensions.Logging.Console (10.0.4), Microsoft.NET.Sdk.Worker, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0 (+8 more)

### Community 2 - ".AssertMailboxAccess"
Cohesion: 0.08
Nodes (41): CancellationToken, Guid, IReadOnlyList, Task, IMailboxService, CancellationToken, Guid, Task (+33 more)

### Community 3 - "system"
Cohesion: 0.09
Nodes (22): Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, Migration, npgsql_entityframeworkcore_postgresql_metadata, InitialCreate, Guid (+14 more)

### Community 4 - "webmail/src/App.tsx"
Cohesion: 0.10
Nodes (28): INITIAL_CONTACTS, INITIAL_EVENTS, INITIAL_MAILBOXES, INITIAL_MESSAGES, INITIAL_RULES, CalendarView(), CalendarViewProps, ChangePasswordView() (+20 more)

### Community 5 - ".Main"
Cohesion: 0.08
Nodes (25): ApiBehaviorOptions, ConcurrentDictionary, CloudflareEmailOptions, ApiBase, ApiToken, IsConfigured, TimeSpan, LockoutOptions (+17 more)

### Community 6 - "MailQueueService"
Cohesion: 0.14
Nodes (19): CancellationToken, Guid, Task, IMailQueueService, CancellationToken, DateTimeOffset, Guid, Task (+11 more)

### Community 7 - "AppDbContext"
Cohesion: 0.04
Nodes (54): DbContext, DbSet, IDesignTimeDbContextFactory, ModelBuilder, AppDbContext, Aliases, ApiKeys, ApplicationPasswords (+46 more)

### Community 8 - "Miautrix.Mail.Persistence"
Cohesion: 0.07
Nodes (37): Miautrix.Mail.IntegrationTests, Miautrix.Mail.Protocols.Imap, Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.IntegrationTests.Licensing, Miautrix.Mail.Cli.Commands, Miautrix.Mail.Infrastructure.Backup, Miautrix.Mail.AntiSpam, Miautrix.Mail.Storage (+29 more)

### Community 9 - "ParsedInboundMessage"
Cohesion: 0.06
Nodes (44): ParsedAttachment, ParsedInboundMessage, CancellationToken, Guid, List, QuarantineItem, Task, IQuarantineService (+36 more)

### Community 10 - "ImapSession"
Cohesion: 0.19
Nodes (14): CancellationToken, GeneratedRegex, Guid, IReadOnlyList, Regex, Stream, Task, ImapCommandResult (+6 more)

### Community 11 - "client.ts"
Cohesion: 0.06
Nodes (51): apiClient, AntiMalwareScreen(), AntiSpamScreen(), BackupScreen(), BackupScreenProps, ChangePasswordView(), ChangePasswordViewProps, DashboardScreen() (+43 more)

### Community 12 - "AI Build Instructions"
Cohesion: 0.20
Nodes (10): 1 · Your role, 2 · Token compliance, 3 · Component recipes, 4 · Hard constraints, 5 · Before you finish — verify, AI Build Instructions, Buttons, Cards (+2 more)

### Community 13 - ".SimulateAsync"
Cohesion: 0.07
Nodes (38): Miautrix.Mail.MailFlow, IDisposable, IServiceCollection, IServiceProvider, ITypeRegistrar, ITypeResolver, Func, TypeRegistrar (+30 more)

### Community 14 - "AdminApiClient"
Cohesion: 0.10
Nodes (5): AdminApiClient, ApiResponse, DomainItem, SecuritySettings, SharedMailbox

### Community 15 - "system_threading_tasks"
Cohesion: 0.18
Nodes (15): Miautrix.Mail.Infrastructure.MailboxArchiving, Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.Application.Transport, Miautrix.Mail.Application.Auth, Miautrix.Mail.Application.Admin, Miautrix.Mail.Web.Contracts, Miautrix.Mail.Application.Mail (+7 more)

### Community 16 - "UsersScreen.tsx"
Cohesion: 0.22
Nodes (12): AssignMailboxModal(), AssignMailboxModalProps, splitEmail(), MailboxDelegatePicker(), MailboxDelegatePickerProps, UsersScreen(), UsersScreenProps, AdminUserItem (+4 more)

### Community 17 - "TenantScopedEntityBase"
Cohesion: 0.05
Nodes (74): Guid, Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, Deploy (+66 more)

### Community 18 - "Guid"
Cohesion: 0.17
Nodes (9): InvalidOperationException, Guid, IPermissionRepository, LastOwnerDemotionException, TenantAuthorizationHelper, Fact, Guid, IsolationTests (+1 more)

### Community 19 - "DkimService"
Cohesion: 0.13
Nodes (13): Body, Headers, RSA, Dictionary, GeneratedRegex, List, Regex, DkimKeyPair (+5 more)

### Community 20 - "InMemorySecurityEventSink"
Cohesion: 0.21
Nodes (14): IPasswordHasher, DateTimeOffset, Guid, IReadOnlyList, List, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink (+6 more)

### Community 21 - "Folder"
Cohesion: 0.11
Nodes (18): IReadOnlyCollection, Folder, MailboxId, Name, Role, UidNext, UidValidity, Membership (+10 more)

### Community 22 - "$_"
Cohesion: 0.02
Nodes (140): $_, A_(), aE, AN(), ao(), av, aw(), Ay() (+132 more)

### Community 23 - "Miautrix Mail Server"
Cohesion: 0.08
Nodes (24): 10. Authentication and Authorization, 11. MFA, 12. Security Event Codes, 15. Graphical Mail Flow Designer, 16. Mail Flow Simulator, 17. Mail Flow Explorer, 18. Restricted Device Relay, 19. Anti-Spam Architecture (+16 more)

### Community 24 - "uN"
Cohesion: 0.04
Nodes (81): uN(), aa(), ac(), An(), e(), bi(), bm(), Bs() (+73 more)

### Community 25 - "QuarantineItem"
Cohesion: 0.10
Nodes (23): QuarantineItem, IsDelivered, QuarantinedAt, RawMessage, ReasonsJson, Recipient, ReleasedAt, Sender (+15 more)

### Community 26 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 27 - "SmtpDeliveryAttempt"
Cohesion: 0.09
Nodes (22): DateTimeOffset, BackupJob, ArchivePath, CompletedAt, ErrorMessage, Name, SizeBytes, Status (+14 more)

### Community 28 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 29 - "system_text"
Cohesion: 0.11
Nodes (18): Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Web, Miautrix.Mail.IntegrationTests.Api, Miautrix.Mail.ProtocolTests.Smtp, Miautrix.Mail.Queue, Miautrix.Mail.SecurityTests.Identity, Miautrix.Mail.IntegrationTests.Cloudflare, Miautrix.Mail.Protocols.Smtp (+10 more)

### Community 30 - "TlsCertificateProvider"
Cohesion: 0.50
Nodes (3): X509Certificate2, TlsCertificateProvider, Certificate

### Community 31 - "f"
Cohesion: 0.09
Nodes (85): aC(), b2(), Bc(), bf(), bv(), cC(), dC(), eC (+77 more)

### Community 32 - "AuditLog"
Cohesion: 0.18
Nodes (10): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Fact (+2 more)

### Community 33 - "Message"
Cohesion: 0.10
Nodes (27): Message, BodyHtml, BodyText, ContentHash, Date, Flags, FolderId, IsRead (+19 more)

### Community 34 - "Mailbox"
Cohesion: 0.15
Nodes (18): Mailbox, Address, DomainId, IsActive, Kind, Name, QuotaBytes, UsedBytes (+10 more)

### Community 35 - ".StageAndSwitchAsync"
Cohesion: 0.19
Nodes (10): Miautrix.Mail.Infrastructure.Deployment, Miautrix.Mail.IntegrationTests.BlueGreen, CancellationToken, Func, Task, DeploymentManager, IDeploymentManager, Fact (+2 more)

### Community 36 - ".HandleDataAsync"
Cohesion: 0.09
Nodes (29): CancellationToken, Guid, Response, Task, SmtpInboundHandler, Guid, ISmtpAuthenticator, ISmtpDomainValidator (+21 more)

### Community 37 - "Miautrix Mail Server — Implementation Blueprint"
Cohesion: 0.10
Nodes (21): 2.1 In scope for v1, 2.2 Out of scope for v1, 2.3 Non-goals, Miautrix Mail Server — Implementation Blueprint, Provider seams, Section 0 — How to use this blueprint, Section 10 — Workspace, Section 11 — Testing (+13 more)

### Community 38 - "compilerOptions"
Cohesion: 0.10
Nodes (19): node, compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, noEmit (+11 more)

### Community 39 - "ISessionManager"
Cohesion: 0.19
Nodes (14): SessionToken, DateTimeOffset, Guid, RefreshToken, TimeSpan, AuthToken, ISessionManager, DefaultRefreshLifetime (+6 more)

### Community 40 - "SmtpQueueItem"
Cohesion: 0.15
Nodes (14): SmtpQueueItem, Attempts, LastAttemptAt, LastError, NextAttemptAt, RawMessage, Recipient, Sender (+6 more)

### Community 41 - "c"
Cohesion: 0.05
Nodes (64): _2(), a2(), f(), aj(), As(), cN(), dj(), dN() (+56 more)

### Community 42 - "MailboxDelegateRequest"
Cohesion: 0.19
Nodes (15): IReadOnlyList, AssignMailboxRequest, CreateSharedMailboxRequest, CreateUserRequest, MailboxDelegateRequest, CancellationToken, Guid, HttpGet (+7 more)

### Community 43 - "Components"
Cohesion: 0.12
Nodes (16): Buttons, Cards, Checkboxes, Chips, Components, Default Item, Disabled State, Filter Chip (+8 more)

### Community 44 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+10 more)

### Community 45 - "AuthService"
Cohesion: 0.12
Nodes (22): DateTimeOffset, Guid, IReadOnlyList, AuthResult, AuthStatus, Failed, LockedOut, MfaRequired (+14 more)

### Community 46 - "IAuthService"
Cohesion: 0.28
Nodes (11): IAuthService, CancellationToken, HttpGet, HttpPost, IResult, ProducesResponseType, Task, AuthController (+3 more)

### Community 47 - "DomainController"
Cohesion: 0.33
Nodes (10): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+2 more)

### Community 48 - "RuleController"
Cohesion: 0.19
Nodes (15): Dictionary, List, RuleSampleMessage, RuleSimulationRequest, RuleSimulationResult, CancellationToken, Guid, HttpDelete (+7 more)

### Community 49 - "UserController"
Cohesion: 0.33
Nodes (10): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+2 more)

### Community 50 - "devDependencies"
Cohesion: 0.06
Nodes (33): dependencies, react, react-dom, @xyflow/react, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react (+25 more)

### Community 51 - "devDependencies"
Cohesion: 0.06
Nodes (31): dependencies, react, react-dom, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @types/react (+23 more)

### Community 52 - "compilerOptions"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+14 more)

### Community 53 - "devDependencies"
Cohesion: 0.05
Nodes (42): dompurify, oxlint, @types/dompurify, @types/node, dependencies, react, react-dom, devDependencies (+34 more)

### Community 54 - "Miautrix Mail Server - Production Deployment Guide (Debian LXC)"
Cohesion: 0.13
Nodes (14): 1.1. Install System Dependencies & NGINX, 1.2. Configure Systemd Service for .NET Backend, 1.3. Configure NGINX for Cloudflare Tunnel (`mail.miautrix.tech`), 2.1. Publish .NET 10 Self-Contained Binary, 2.2. Build Frontends (Web Admin & Webmail), 2.3. Apply Database Migrations to `miautrix-mail-pro`, 3.1. Copy Published Files using Windows Built-in `scp`, Architecture & Configuration Summary (+6 more)

### Community 55 - ".When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry"
Cohesion: 0.18
Nodes (12): Random, TimeSpan, ExponentialBackoffWithJitterRetryPolicy, IRetryPolicy, CancellationToken, Guid, Task, ISmtpQueueManager (+4 more)

### Community 56 - "xN"
Cohesion: 0.09
Nodes (50): a0(), Al(), ar(), cw(), dw(), ew(), f0(), I_() (+42 more)

### Community 57 - "index-9M21bitv.js"
Cohesion: 0.04
Nodes (88): ah(), ap(), bh(), Br(), ch(), changePassword(), constructor(), eh() (+80 more)

### Community 58 - "n"
Cohesion: 0.13
Nodes (54): _1(), at(), ax(), n(), t(), b(), bd(), _c() (+46 more)

### Community 59 - "pN"
Cohesion: 0.08
Nodes (5): gx(), pN, Qc(), Vc(), wy()

### Community 60 - "Dt"
Cohesion: 0.07
Nodes (51): ah(), Ao(), ap(), bl(), bp(), ch(), Do(), Dt() (+43 more)

### Community 61 - ".Get"
Cohesion: 0.31
Nodes (10): QuarantineFilter, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, IResult, ProducesResponseType (+2 more)

### Community 62 - "IRequestContextAccessor"
Cohesion: 0.11
Nodes (24): ControllerBase, IHttpContextAccessor, AuditFilter, CancellationToken, HttpGet, IResult, ProducesResponseType, Task (+16 more)

### Community 63 - "plugins"
Cohesion: 0.22
Nodes (8): oxc, typescript, warn, plugins, rules, react/only-export-components, react/rules-of-hooks, $schema

### Community 64 - "Ue"
Cohesion: 0.08
Nodes (48): b_, cE(), dE(), ev(), Fc(), fE, fy(), g_() (+40 more)

### Community 65 - "Miautrix Mail Server"
Cohesion: 0.15
Nodes (12): 1.1 Project Purpose and Business Case, 1.2 High-Level Objectives, 1. Project Initiation & Executive Summary, 2. Project Scope Management (WBS Overview), 3. Project Schedule & Milestone Management, 4. Quality Management & Verification Gates, 5. Risk Management Matrix, 6. Stakeholder & Resource Plan (+4 more)

### Community 66 - "MailboxCreateSettings"
Cohesion: 0.20
Nodes (10): CancellationToken, CommandContext, Guid, Task, MailboxCreateCommand, MailboxCreateSettings, Address, QuotaBytes (+2 more)

### Community 67 - "ApiResponse"
Cohesion: 0.18
Nodes (18): HttpPatch, UpdateSecuritySettingsRequest, ApiResponse, PaginationMeta, CancellationToken, Guid, HttpGet, IResult (+10 more)

### Community 68 - "Permission"
Cohesion: 0.30
Nodes (7): Permission, Code, Name, Fact, Guid, Task, CliTests

### Community 69 - "RuleDesignerScreen.tsx"
Cohesion: 0.20
Nodes (8): App(), initialEdges, initialNodes, RuleDesignerScreen(), RuleDesignerScreenProps, admin_src_styles_admin, ref_xyflow_react, ref_xyflow_react_dist_style_css

### Community 70 - "fg"
Cohesion: 0.09
Nodes (40): _a(), bu(), ci(), cl(), cm(), dg(), Dr(), Eb() (+32 more)

### Community 71 - "cg"
Cohesion: 0.11
Nodes (34): A1(), ag(), am(), cg(), cu(), Da(), Ea(), Fh() (+26 more)

### Community 72 - "td"
Cohesion: 0.11
Nodes (34): ad(), bh(), br(), cp(), ed(), eh(), ex(), Fn() (+26 more)

### Community 73 - "Epic 2 — Mail transport and policy"
Cohesion: 0.22
Nodes (8): Epic 2 — Mail transport and policy, T10 — IMAP, storage, and attachments, T11 — ManageSieve, T12 — Search, T13 — Rule engine, simulator, explorer, T7 — SMTP listener and queue, T8 — SPF, DKIM, DMARC, T9 — Anti-spam baseline and quarantine

### Community 74 - "Hb"
Cohesion: 0.16
Nodes (24): au(), bb(), gi(), gp(), gu(), Hb(), Hd(), jg() (+16 more)

### Community 75 - "bw"
Cohesion: 0.17
Nodes (23): _0(), Af(), bw(), d_(), $f(), Fi(), Gf(), i0 (+15 more)

### Community 76 - "AddCatalogFields"
Cohesion: 0.29
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddCatalogFields

### Community 77 - "Miautrix.Mail.sln"
Cohesion: 0.11
Nodes (12): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+4 more)

### Community 78 - "Miautrix.Mail.Persistence.csproj"
Cohesion: 0.11
Nodes (16): Microsoft.AspNetCore.OpenApi (10.0.4), Microsoft.OpenApi (2.7.5), Microsoft.NET.Sdk.Web, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.EntityFrameworkCore.Design (10.0.4) (+8 more)

### Community 79 - ".Main"
Cohesion: 0.03
Nodes (72): AsyncCommand, CommandSettings, CancellationToken, CommandContext, Task, BackupCommand, BackupSettings, OutputPath (+64 more)

### Community 80 - "Epic 1 — Core platform"
Cohesion: 0.25
Nodes (7): Epic 1 — Core platform, T1 — Solution scaffold and CI, T2 — Domain model and EF Core schema, T3 — Seed data and indexes, T4 — Identity, T5 — Authorization and tenant isolation, T6 — Audit trail

### Community 81 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Architecture rules, Commands, Data rules, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 82 - "Miautrix Mail Server"
Cohesion: 0.22
Nodes (8): Architecture rules, Commands, Data rules, Discovery workflow, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 83 - "Miautrix.Mail.IntegrationTests.csproj"
Cohesion: 0.11
Nodes (15): Microsoft.AspNetCore.Mvc.Testing (10.0.4), Microsoft.EntityFrameworkCore.InMemory (10.0.4), net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.NET.Sdk, net10.0, Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk (+7 more)

### Community 84 - ".RunOnePassAsync"
Cohesion: 0.21
Nodes (15): Guid, OutboundMessage, CancellationToken, Fact, Guid, IServiceScopeFactory, List, Task (+7 more)

### Community 85 - "p2"
Cohesion: 0.15
Nodes (17): c2(), Cf(), copy(), d0(), go(), gw(), kf(), kx() (+9 more)

### Community 86 - "PulsePoint"
Cohesion: 0.17
Nodes (8): Border Radius, Colors, Do's and Don'ts, Elevation, Overview, PulsePoint, Spacing, Typography

### Community 87 - "CloudflareApiMailTransport"
Cohesion: 0.19
Nodes (11): IHttpClientFactory, CancellationToken, Task, TimeSpan, CloudflareApiMailTransport, Mode, CancellationToken, Task (+3 more)

### Community 88 - "5. Recommended Technology Stack"
Cohesion: 0.25
Nodes (8): 5. Recommended Technology Stack, Backend, Caching, Database, Frontend, Logging, Scheduling, Storage

### Community 89 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 90 - "Miautrix Mail Server"
Cohesion: 0.22
Nodes (9): 📐 Architecture & Design, Build & Test Commands, 🛠️ Deploying & Updating, ⚙️ Development Environment, Miautrix Mail Server, ✉️ Outbound transport model, 🚀 Project Status, 🔒 Security Invariants (+1 more)

### Community 91 - "Miautrix.Mail.Application.csproj"
Cohesion: 0.12
Nodes (14): DnsClient (1.8.0), Microsoft.Extensions.Configuration (10.0.4), Microsoft.Extensions.Configuration.EnvironmentVariables (10.0.4), Microsoft.Extensions.DependencyInjection (10.0.4), Spectre.Console (0.55.0), Spectre.Console.Cli (0.55.0), net10.0, Microsoft.Extensions.Http (10.0.4) (+6 more)

### Community 92 - "SystemInfoCommand.cs"
Cohesion: 0.32
Nodes (6): Command, CancellationToken, CommandContext, SystemInfoCommand, SystemInfoSettings, system_runtime_interopservices

### Community 93 - "md"
Cohesion: 0.16
Nodes (17): bc(), Er(), gd(), Gs(), ih(), Ja(), ju(), ks() (+9 more)

### Community 94 - ".Cloudflare"
Cohesion: 0.14
Nodes (13): ISmtpInboundHandler, CancellationToken, Guid, HttpPost, ILogger, IResult, ProducesResponseType, Task (+5 more)

### Community 95 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.29
Nodes (6): AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Provider seams, Task tracking, Things that will bite you, Where things are

### Community 96 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.29
Nodes (6): AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Provider seams, Task tracking, Things that will bite you, Where things are

### Community 97 - "Miautrix Mail Server — Errors, Issues & Review Log"
Cohesion: 0.13
Nodes (14): 1. System & OS Environment Issues, 2. Frontend & UI Runtime Issues, 3. NGINX & Ingress Routing Issues, 4. Backend & Database Integrity Checks, 5. Items to Review & Verify Later, 6. Transport / Protocol Listener Issues, 7. Cloudflare Workers Integration, Boundary: Cloudflare is transport only (+6 more)

### Community 98 - "pl"
Cohesion: 0.11
Nodes (40): ba(), cl(), dl(), ea(), es(), fa(), fl(), Fo() (+32 more)

### Community 99 - "i"
Cohesion: 0.15
Nodes (48): bo(), co(), d(), dc(), fc(), fh(), hn(), hs() (+40 more)

### Community 100 - "Miautrix.Mail.Identity.csproj"
Cohesion: 0.14
Nodes (12): Konscious.Security.Cryptography.Argon2 (1.3.1), Otp.NET (1.4.1), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4) (+4 more)

### Community 101 - "Domain"
Cohesion: 0.13
Nodes (15): Domain, CloudflareWorkerUrl, CloudflareZoneId, DkimPublicKey, DkimSelector, DmarcRecord, IsPrimary, IsVerified (+7 more)

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

### Community 109 - "CloudflareTransportTests"
Cohesion: 0.08
Nodes (26): Argon2idHasherOptions, IClassFixture, Argon2idHasherOptions, Argon2idPasswordHasher, CurrentOptions, Program, Fact, Guid (+18 more)

### Community 110 - "OutboundQueueDispatcher"
Cohesion: 0.28
Nodes (9): IOutboundMailTransport, Mode, CancellationToken, ILogger, IReadOnlyDictionary, IServiceScopeFactory, Task, TimeSpan (+1 more)

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

### Community 118 - "Miautrix.Mail.UnitTests.csproj"
Cohesion: 0.18
Nodes (9): NetArchTest.eNhancedEdition (1.4.5), net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4) (+1 more)

### Community 119 - "bg"
Cohesion: 0.31
Nodes (10): ai(), bg(), cc(), cd(), cs(), D1(), ep(), nd() (+2 more)

### Community 120 - "LoginStatus"
Cohesion: 0.40
Nodes (5): LoginStatus, Failed, LockedOut, MfaRequired, Success

### Community 121 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 122 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 123 - "AddMailAndSpamColumns"
Cohesion: 0.22
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddMailAndSpamColumns

### Community 124 - "yl"
Cohesion: 0.10
Nodes (32): Ga(), Ao(), as(), Bd(), bl(), ef(), ep(), Fi() (+24 more)

### Community 125 - "MailboxAdminApiTests"
Cohesion: 0.24
Nodes (12): HttpResponseMessage, Fact, Guid, HttpMethod, HttpRequestMessage, JsonElement, Task, TenantScope (+4 more)

### Community 126 - "o"
Cohesion: 0.09
Nodes (38): o(), al(), cr(), Db(), dm(), dp(), fp(), Fr() (+30 more)

### Community 128 - "Miautrix.Mail.Domain.csproj"
Cohesion: 0.18
Nodes (8): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 129 - "IAdminService"
Cohesion: 0.11
Nodes (30): DateTimeOffset, Guid, AdminUserDto, AuditLogDto, BackupJobDto, CreateRuleRequest, DashboardSummaryDto, DomainDto (+22 more)

### Community 130 - "Section 20 — Verification status (READ FIRST)"
Cohesion: 0.50
Nodes (4): A correction to the record, Section 20 — Verification status (READ FIRST), What was NOT verified, What was verified

### Community 131 - "format-status.js"
Cohesion: 0.50
Nodes (3): fs, tasks, ref_fs

### Community 133 - "update-prod-database.sh"
Cohesion: 0.50
Nodes (3): ASPNETCORE_ENVIRONMENT, MIAUTRIX_DB_CONNECTION, update-prod-database.sh script

### Community 134 - "n"
Cohesion: 0.10
Nodes (39): ac(), bs(), bt(), cs(), ds(), fs(), gs(), If() (+31 more)

### Community 135 - "ITotpService"
Cohesion: 0.29
Nodes (3): otpnet, ITotpService, TotpService

### Community 136 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 137 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 138 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 139 - "AddUserServiceAndMailboxKind"
Cohesion: 0.29
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddUserServiceAndMailboxKind

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

### Community 154 - "AddDomainTransportMode"
Cohesion: 0.29
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddDomainTransportMode

### Community 155 - "AddTenantMfaEnforced"
Cohesion: 0.29
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddTenantMfaEnforced

### Community 157 - "FixMfaEnforcedScope"
Cohesion: 0.29
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, FixMfaEnforcedScope

### Community 158 - "AddDomainSecurityPolicySettings"
Cohesion: 0.29
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddDomainSecurityPolicySettings

### Community 159 - "Future implementation items"
Cohesion: 0.29
Nodes (6): DANE / DNSSEC validation, DKIM key rotation, Future implementation items, Implemented now, Security Roadmap, TLS policy controls

### Community 160 - "Attachment"
Cohesion: 0.29
Nodes (7): Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes, StoragePath

### Community 161 - "ft"
Cohesion: 0.13
Nodes (35): ae(), ce(), ct(), dn(), dp(), ee(), fe(), fm() (+27 more)

### Community 162 - "Miautrix.Mail.EndToEndTests.csproj"
Cohesion: 0.29
Nodes (6): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 163 - "AddMailboxName"
Cohesion: 0.40
Nodes (3): MigrationBuilder, ModelBuilder, AddMailboxName

### Community 164 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 165 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 166 - "C"
Cohesion: 0.10
Nodes (36): E(), H(), oa(), Ai(), Bc(), C(), ca(), cc() (+28 more)

### Community 167 - "b"
Cohesion: 0.09
Nodes (32): am(), an(), b(), bm(), Bp(), cm(), dh(), em() (+24 more)

### Community 168 - ".CreateArchiveAsync"
Cohesion: 0.18
Nodes (15): IEnumerable, CancellationToken, DateTimeOffset, Dictionary, Guid, HashSet, IReadOnlyCollection, IReadOnlyList (+7 more)

### Community 169 - "Z"
Cohesion: 0.10
Nodes (37): al(), Au(), bi(), bu(), Cp(), Cu(), Du(), eu() (+29 more)

### Community 170 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 182 - "Fd"
Cohesion: 0.13
Nodes (25): it(), af(), cf(), df(), Fd(), ff(), ht(), Ia() (+17 more)

### Community 183 - "SmtpListenerService"
Cohesion: 0.14
Nodes (16): SmtpInboundMxListener, SmtpSubmissionImplicitTlsListener, SmtpSubmissionStartTlsListener, WorkerHostOptions, Hostname, CancellationToken, ILogger, IServiceScopeFactory (+8 more)

### Community 184 - "SharedMailboxesScreen.tsx"
Cohesion: 0.15
Nodes (14): Layout(), LayoutProps, MENU_ITEMS, MenuItem, ScreenId, QuarantineScreen(), QuarantineScreenProps, formatBytes() (+6 more)

### Community 189 - "DomainEntity"
Cohesion: 0.15
Nodes (12): DomainEntity, CancellationToken, Guid, Task, IImapAuthenticator, ImapAuthenticationResult, CancellationToken, Fact (+4 more)

### Community 191 - "ImapAuthenticatorTests"
Cohesion: 0.25
Nodes (10): SeedResult, CancellationToken, Task, ImapAuthenticator, Fact, Guid, List, Task (+2 more)

### Community 192 - "MailQueueController"
Cohesion: 0.16
Nodes (15): ReassignQueueItemRequest, DateTimeOffset, QueueItemDto, CancellationToken, DateTimeOffset, Guid, HttpDelete, HttpGet (+7 more)

### Community 193 - "CancellationToken"
Cohesion: 0.38
Nodes (5): CancellationToken, Stream, Task, FileSystemMailStorage, StorageWriteResult

### Community 194 - ".DeleteMailboxAsync"
Cohesion: 0.22
Nodes (11): DeleteMailboxRequest, DeleteMailboxResult, DeleteMailboxResult, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost (+3 more)

### Community 196 - "SimulateSampleMessage"
Cohesion: 0.14
Nodes (13): RetryQueueItemRequest, Reason, Dictionary, SimulateRuleRequest, SampleMessage, SimulateSampleMessage, HasAttachment, Headers (+5 more)

### Community 197 - "User"
Cohesion: 0.16
Nodes (13): Miautrix.Mail.Application.Users, Exception, IUserService, User, Email, IsActive, IsService, Name (+5 more)

### Community 200 - "Iris Pay"
Cohesion: 0.11
Nodes (18): 1. Atmosphere, 2. Palette, 3. Typography, 4. Buttons, 5. Cards, 6. Charts, 7. Spacing, 8. Depth & elevation (+10 more)

### Community 201 - "QueueScreen.test.tsx"
Cohesion: 0.14
Nodes (11): QueueItem, DesktopApp(), ref_dompurify, ref_react_dom_client, ref_testing_library_react, ref_vitest, App(), SanitizedMessageBody() (+3 more)

### Community 202 - "ImapListenerService"
Cohesion: 0.31
Nodes (8): BackgroundService, CancellationToken, ILogger, IServiceScopeFactory, Task, TcpClient, X509Certificate2, ImapListenerService

### Community 203 - "SpamVerdict"
Cohesion: 0.17
Nodes (12): SpamVerdict, DkimResult, DmarcResult, DnsblListed, Greylisted, IsSpam, ReasonsJson, Recipient (+4 more)

### Community 204 - "Pro tokens"
Cohesion: 0.18
Nodes (11): Accessibility (WCAG 2.1), Button, Card, Content, Density, Elevation, Input, Motion (+3 more)

### Community 205 - "Tokens"
Cohesion: 0.17
Nodes (12): Borders, Buttons, Charts, Colors, Ghost, Outline, Primary, Radius (+4 more)

### Community 209 - ".Error"
Cohesion: 0.25
Nodes (6): IReadOnlyDictionary, ApiError, HttpContext, IReadOnlyDictionary, IResult, ApiResults

### Community 211 - "ApiExceptionMiddleware"
Cohesion: 0.43
Nodes (5): HttpContext, ILogger, RequestDelegate, Task, ApiExceptionMiddleware

### Community 215 - "lxc-prepare-storage.sh"
Cohesion: 0.60
Nodes (4): count_blobs(), current_env_dir(), effective_dir(), lxc-prepare-storage.sh script

### Community 216 - "ImapState"
Cohesion: 0.40
Nodes (5): ImapState, Authenticated, LoggedOut, NotAuthenticated, Selected

### Community 218 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

## Knowledge Gaps
- **882 isolated node(s):** `name`, `version`, `type`, `dev`, `build` (+877 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1291 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **18 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `f()` connect `f` to `c`, `bw`, `o`?**
  _High betweenness centrality (0.009) - this node is a cross-community bridge._
- **Why does `l0()` connect `f` to `xN`, `c`, `C`?**
  _High betweenness centrality (0.009) - this node is a cross-community bridge._
- **Why does `ne()` connect `ft` to `pl`, `n`, `b`?**
  _High betweenness centrality (0.008) - this node is a cross-community bridge._
- **Are the 32 inferred relationships involving `uN()` (e.g. with `Oy()` and `A()`) actually correct?**
  _`uN()` has 32 INFERRED edges - model-reasoned connections that need verification._
- **Are the 53 inferred relationships involving `$_` (e.g. with `ao()` and `b2()`) actually correct?**
  _`$_` has 53 INFERRED edges - model-reasoned connections that need verification._
- **What connects `name`, `version`, `type` to the rest of the system?**
  _882 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `AdminService` be split into smaller, more focused modules?**
  _Cohesion score 0.12712215320910972 - nodes in this community are weakly interconnected._