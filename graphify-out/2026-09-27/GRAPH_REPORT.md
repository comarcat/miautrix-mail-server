# Graph Report - miautrix-mail-server  (2026-09-27)

## Corpus Check
- 347 files · ~4,717,378 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 4302 nodes · 11579 edges · 230 communities (190 shown, 25 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 1366 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7f863a78`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- AdminService
- Miautrix.Mail.Security
- MailboxController
- Miautrix.Mail.Persistence.Migrations
- webmail/src/App.tsx
- InMemoryIdempotencyStore
- .Cloudflare
- Message
- Miautrix.Mail.Persistence
- .ProcessItemAsync
- index-7_LIhLyZ.js
- admin/src/App.tsx
- AI Build Instructions
- .SimulateAsync
- InboxView.tsx
- system
- MailboxAdminApiTests
- .Imap_AppendAndFetch_PreservesBytePayloadAndFlags
- Guid
- DkimService
- InMemorySecurityEventSink
- dN
- MessageController
- Miautrix Mail Server
- AdminApiClient
- IMessageService
- compilerOptions
- TenantScopedEntityBase
- compilerOptions
- ISmtpQueueManager
- rN
- .Main
- OutboundQueueDispatcher
- .RunOnePassAsync
- Mailbox
- .StageAndSwitchAsync
- MockSmtpQueueManager
- Miautrix Mail Server — Implementation Blueprint
- compilerOptions
- A
- SmtpQueueItem
- .SearchAsync
- .ReplaceDelegates
- Components
- compilerOptions
- AuthService
- AuthController
- IRequestContextAccessor
- RuleController
- UserController
- devDependencies
- devDependencies
- compilerOptions
- devDependencies
- Miautrix Mail Server - Production Deployment Guide (Debian LXC)
- HeaderRequestContextAccessor
- IAdminService
- c
- n
- .Up
- o
- Zc
- IMailQueueService
- plugins
- ew
- Miautrix Mail Server
- AuditLog
- ApiResponse
- User
- QueueScreen.tsx
- Xe
- Guid
- ITenantAuthorizationHelper
- Epic 2 — Mail transport and policy
- QueueRetryCommand
- MailContracts.cs
- .Up
- Miautrix.Mail.sln
- SmtpResponse
- rb
- Epic 1 — Core platform
- Miautrix Mail Server
- Miautrix Mail Server
- QuarantineItem
- Dt
- EfPermissionRepository
- PulsePoint
- MockPermissionRepo
- 5. Recommended Technology Stack
- package.json
- Miautrix Mail Server
- Domain
- Ue
- ImapSession
- AdminContracts.cs
- AGENTS.md — Miautrix Mail Server
- AGENTS.md — Miautrix Mail Server
- Miautrix Mail Server — Errors, Issues & Review Log
- QueueStatusFilter
- Argon2idPasswordHasher
- Miautrix.Mail.Identity.csproj
- .CreateArchiveAsync
- Epic 3 — Surfaces
- Epic 4 — Operations
- Rule: secrets and logging
- Rule: secrets and logging
- 3. Core Architectural Principles
- 9. Identity Architecture
- .UpdateAntiSpamSettingsAsync
- MailQueueService
- MailFlowScreen.tsx
- ref_vitejs_plugin_react
- Section 19 — Verify commands
- Section 4 — Stack
- Section 5 — Data model
- Section 9 — Build order
- verify-task
- verify-task
- Miautrix.Mail.UnitTests.csproj
- .on
- SmtpListenerService
- .BuildTargetModel
- .Up
- TlsCertificateProvider
- MailQueueController
- lg
- 20260927020255_UpdateFlagSystem.Designer.cs
- ref_testing_library_jest_dom_vitest
- WebmailApiClient.ts
- UsersScreen.tsx
- Section 20 — Verification status (READ FIRST)
- format-status.js
- AppDbContext
- bg
- bb
- SieveRulesView.tsx
- ImapAuthenticatorTests
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- .DeleteMailboxAsync
- React + TypeScript + Vite
- versions.mjs
- desktop/tsconfig.json
- generate_migration.sh
- 13. Mail Flow Architecture
- 14. Mail Flow Rule Engine
- init-database-scratch.sh
- CloudflareTransportTests
- webmail/tsconfig.json
- workspace/.claude/rules/tenancy.md
- .claude/rules/tenancy.md
- fix_tasks.py
- QueueItemDto
- Miautrix.Mail.Cli.csproj
- SpamVerdict
- ITotpService
- SystemController
- Future implementation items
- SmtpAuthenticator
- RecordingTransport
- Miautrix.Mail.EndToEndTests.csproj
- AuthStatus
- .Up
- ImapState
- QuarantineController
- ISessionManager
- MailQueueApiTests
- .ApplyMigrations
- ImapListenerService
- MailboxService
- client.ts
- Miautrix.Mail.Domain.csproj
- Miautrix.Mail.IntegrationTests.csproj
- IMailStorage
- WebmailApiClient
- SimulateSampleMessage
- Miautrix.Mail.Worker.csproj
- Iris Pay
- AntiMalwareScreen.tsx
- ApiExceptionMiddleware
- Pro tokens
- Tokens
- .BuildModel
- .Error
- UnitTestProjectSmokeTests.cs
- EndToEndTestProjectSmokeTests.cs
- lxc-prepare-storage.sh
- update-prod-database.sh
- Miautrix.Mail.Application.csproj
- RequestIdMiddleware
- .Up
- .BuildTargetModel
- ApiJson.cs
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- .GetAlertConfigs
- inject-flow-mail-test.sh
- lxc-install-antimalware.sh
- lxc-install-worker-env.sh
- update-database.sh
- update-test-database.sh
- inject-eicar-test.sh
- lxc-restart-services.sh
- lxc-update-antimalware-signatures.sh
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- MessageService
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel

## God Nodes (most connected - your core abstractions)
1. `dN()` - 408 edges
2. `AppDbContext` - 172 edges
3. `o()` - 102 edges
4. `AdminApiClient` - 93 edges
5. `AdminService` - 87 edges
6. `Miautrix.Mail.Persistence` - 75 edges
7. `Zc` - 61 edges
8. `c()` - 59 edges
9. `IAdminService` - 58 edges
10. `yN` - 58 edges

## Surprising Connections (you probably didn't know these)
- `SearchTests` --references--> `PostgreSqlSearchProvider`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Search/SearchTests.cs → src/Miautrix.Mail.Search/SearchProvider.cs
- `MailFlowTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `SearchTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Search/SearchTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `ImapAuthenticatorTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Imap/ImapAuthenticatorTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `ImapTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Imap/ImapTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs

## Import Cycles
- None detected.

## Communities (230 total, 25 thin omitted)

### Community 0 - "AdminService"
Cohesion: 0.10
Nodes (16): LookupClient, AdminUserDto, AntiMalwareSettingsDto, CreateDomainRequest, UpdateAntiMalwareSettingsRequest, CancellationToken, DateTimeOffset, Guid (+8 more)

### Community 1 - "Miautrix.Mail.Security"
Cohesion: 0.17
Nodes (14): Command, Miautrix.Mail.Cli.Commands, Miautrix.Mail.Infrastructure.Backup, Miautrix.Mail.Cli, Miautrix.Mail.Security, Miautrix.Mail.Cli.Infrastructure, spectre_console, spectre_console_cli (+6 more)

### Community 2 - "MailboxController"
Cohesion: 0.38
Nodes (9): CancellationToken, Guid, HttpGet, HttpPatch, HttpPost, IResult, ProducesResponseType, Task (+1 more)

### Community 3 - "Miautrix.Mail.Persistence.Migrations"
Cohesion: 0.04
Nodes (42): Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, Migration, npgsql_entityframeworkcore_postgresql_metadata, InitialCreate, MigrationBuilder (+34 more)

### Community 4 - "webmail/src/App.tsx"
Cohesion: 0.15
Nodes (14): App(), folderIconMap, mapFolders(), normalizeMailboxAccount(), ChangePasswordView(), ChangePasswordViewProps, ComposerAccount, ComposerView() (+6 more)

### Community 5 - "InMemoryIdempotencyStore"
Cohesion: 0.24
Nodes (9): ConcurrentDictionary, HashSet, HttpContext, RequestDelegate, Task, IdempotencyMiddleware, CapturedResponse, IIdempotencyStore (+1 more)

### Community 6 - ".Cloudflare"
Cohesion: 0.10
Nodes (18): CancellationToken, Guid, Response, Task, ISmtpInboundHandler, Guid, CancellationToken, Guid (+10 more)

### Community 7 - "Message"
Cohesion: 0.06
Nodes (29): Folder, MailboxId, Name, ParentId, Role, UidNext, UidValidity, Message (+21 more)

### Community 8 - "Miautrix.Mail.Persistence"
Cohesion: 0.06
Nodes (50): Miautrix.Mail.IntegrationTests, Miautrix.Mail.Protocols.Imap, Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.Infrastructure.MailboxArchiving, Miautrix.Mail.IntegrationTests.Licensing, Miautrix.Mail.AntiMalware, Miautrix.Mail.ProtocolTests, Miautrix.Mail.IntegrationTests.Audit (+42 more)

### Community 9 - ".ProcessItemAsync"
Cohesion: 0.05
Nodes (50): ParsedAttachment, ParsedInboundMessage, CancellationToken, Task, AntiMalwareOptions, AntiMalwareScanResult, AttachmentPolicy, ClamAvScanner (+42 more)

### Community 10 - "index-7_LIhLyZ.js"
Cohesion: 0.02
Nodes (164): _2(), a0(), Af(), AN(), av(), b0(), b2(), bj() (+156 more)

### Community 11 - "admin/src/App.tsx"
Cohesion: 0.06
Nodes (39): apiClient, App(), AntiSpamScreen(), AntiSpamScreenProps, makeClient(), ChangePasswordView(), ChangePasswordViewProps, DashboardScreen() (+31 more)

### Community 12 - "AI Build Instructions"
Cohesion: 0.20
Nodes (10): 1 · Your role, 2 · Token compliance, 3 · Component recipes, 4 · Hard constraints, 5 · Before you finish — verify, AI Build Instructions, Buttons, Cards (+2 more)

### Community 13 - ".SimulateAsync"
Cohesion: 0.07
Nodes (38): Miautrix.Mail.MailFlow, IDisposable, IServiceCollection, IServiceProvider, ITypeRegistrar, ITypeResolver, Func, TypeRegistrar (+30 more)

### Community 14 - "InboxView.tsx"
Cohesion: 0.17
Nodes (21): buildBodyFallbackHtml(), buildMoveMenuFolders(), clamp(), clampSnippet(), compareMailboxLabels(), compareSystemFolders(), FLAG_COLORS, FLAG_LABELS (+13 more)

### Community 15 - "system"
Cohesion: 0.22
Nodes (13): Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.Application.Queue, Miautrix.Mail.Application.Auth, Miautrix.Mail.Application.Admin, Miautrix.Mail.Web.Contracts, Miautrix.Mail.Application.Mail, microsoft_aspnetcore_http (+5 more)

### Community 16 - "MailboxAdminApiTests"
Cohesion: 0.24
Nodes (12): HttpResponseMessage, Fact, Guid, HttpMethod, HttpRequestMessage, JsonElement, Task, TenantScope (+4 more)

### Community 17 - ".Imap_AppendAndFetch_PreservesBytePayloadAndFlags"
Cohesion: 0.18
Nodes (10): CancellationToken, Guid, Task, IImapAuthenticator, ImapAuthenticationResult, CancellationToken, Fact, Guid (+2 more)

### Community 18 - "Guid"
Cohesion: 0.22
Nodes (8): Guid, IPermissionRepository, LastOwnerDemotionException, ResourceNotFoundException, TenantAuthorizationHelper, Fact, Mailbox, IsolationTests

### Community 19 - "DkimService"
Cohesion: 0.13
Nodes (13): Body, Headers, RSA, Dictionary, GeneratedRegex, List, Regex, DkimKeyPair (+5 more)

### Community 20 - "InMemorySecurityEventSink"
Cohesion: 0.16
Nodes (19): IPasswordHasher, DateTimeOffset, Guid, IReadOnlyList, List, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink (+11 more)

### Community 21 - "dN"
Cohesion: 0.04
Nodes (82): dN(), _1(), ab(), ad(), Am(), Au(), ax(), bh() (+74 more)

### Community 22 - "MessageController"
Cohesion: 0.30
Nodes (12): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+4 more)

### Community 23 - "Miautrix Mail Server"
Cohesion: 0.08
Nodes (24): 10. Authentication and Authorization, 11. MFA, 12. Security Event Codes, 15. Graphical Mail Flow Designer, 16. Mail Flow Simulator, 17. Mail Flow Explorer, 18. Restricted Device Relay, 19. Anti-Spam Architecture (+16 more)

### Community 24 - "AdminApiClient"
Cohesion: 0.09
Nodes (7): AdminApiClient, normalizeGuid(), AntiMalwareSettings, AntiSpamSettings, ApiResponse, SecuritySettings, SharedMailbox

### Community 25 - "IMessageService"
Cohesion: 0.39
Nodes (5): CancellationToken, Guid, IReadOnlyList, Task, IMessageService

### Community 26 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 27 - "TenantScopedEntityBase"
Cohesion: 0.05
Nodes (69): Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, Deploy, DkimKey (+61 more)

### Community 28 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 29 - "ISmtpQueueManager"
Cohesion: 0.18
Nodes (12): Random, TimeSpan, ExponentialBackoffWithJitterRetryPolicy, IRetryPolicy, CancellationToken, Guid, Task, ISmtpQueueManager (+4 more)

### Community 30 - "rN"
Cohesion: 0.11
Nodes (10): Ay(), a(), LN(), Oy(), rN(), se(), T(), uN() (+2 more)

### Community 31 - ".Main"
Cohesion: 0.07
Nodes (30): ApiBehaviorOptions, CancellationToken, CommandContext, Task, BackupCommand, CancellationToken, CommandContext, Task (+22 more)

### Community 32 - "OutboundQueueDispatcher"
Cohesion: 0.20
Nodes (11): CloudflareEmailOptions, ApiBase, ApiToken, IsConfigured, CancellationToken, ILogger, IReadOnlyDictionary, IServiceScopeFactory (+3 more)

### Community 33 - ".RunOnePassAsync"
Cohesion: 0.49
Nodes (5): Fact, Guid, IServiceScopeFactory, Task, OutboundQueueDispatcherTests

### Community 34 - "Mailbox"
Cohesion: 0.14
Nodes (20): Miautrix.Mail.Licensing, InvalidOperationException, Mailbox, Address, DomainId, IsActive, Kind, Name (+12 more)

### Community 35 - ".StageAndSwitchAsync"
Cohesion: 0.19
Nodes (10): Miautrix.Mail.Infrastructure.Deployment, Miautrix.Mail.IntegrationTests.BlueGreen, CancellationToken, Func, Task, DeploymentManager, IDeploymentManager, Fact (+2 more)

### Community 36 - "MockSmtpQueueManager"
Cohesion: 0.18
Nodes (14): SmtpInboundHandler, ISmtpAuthenticator, ISmtpDomainValidator, CancellationToken, Fact, Guid, HashSet, List (+6 more)

### Community 37 - "Miautrix Mail Server — Implementation Blueprint"
Cohesion: 0.10
Nodes (21): 2.1 In scope for v1, 2.2 Out of scope for v1, 2.3 Non-goals, Miautrix Mail Server — Implementation Blueprint, Provider seams, Section 0 — How to use this blueprint, Section 10 — Workspace, Section 11 — Testing (+13 more)

### Community 38 - "compilerOptions"
Cohesion: 0.10
Nodes (19): node, compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, noEmit (+11 more)

### Community 39 - "A"
Cohesion: 0.10
Nodes (83): bC(), c(), cN(), i(), k(), o(), T(), d_() (+75 more)

### Community 40 - "SmtpQueueItem"
Cohesion: 0.15
Nodes (14): SmtpQueueItem, Attempts, LastAttemptAt, LastError, NextAttemptAt, RawMessage, Recipient, Sender (+6 more)

### Community 41 - ".SearchAsync"
Cohesion: 0.37
Nodes (8): CancellationToken, DateTimeOffset, Guid, IReadOnlyList, Task, ISearchProvider, PostgreSqlSearchProvider, SearchMessageItem

### Community 42 - ".ReplaceDelegates"
Cohesion: 0.26
Nodes (10): CancellationToken, Guid, HttpGet, HttpPost, HttpPut, IReadOnlyList, IResult, Task (+2 more)

### Community 43 - "Components"
Cohesion: 0.12
Nodes (16): Buttons, Cards, Checkboxes, Chips, Components, Default Item, Disabled State, Filter Chip (+8 more)

### Community 44 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+10 more)

### Community 45 - "AuthService"
Cohesion: 0.16
Nodes (17): DateTimeOffset, Guid, IReadOnlyList, AuthResult, AuthUserDto, ChangePasswordResult, CancellationToken, Guid (+9 more)

### Community 46 - "AuthController"
Cohesion: 0.31
Nodes (10): CancellationToken, HttpGet, HttpPost, IResult, ProducesResponseType, Task, AuthController, ChangePasswordRequest (+2 more)

### Community 47 - "IRequestContextAccessor"
Cohesion: 0.10
Nodes (28): ControllerBase, AuditFilter, CancellationToken, Guid, HttpGet, IResult, ProducesResponseType, Task (+20 more)

### Community 48 - "RuleController"
Cohesion: 0.33
Nodes (10): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+2 more)

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
Cohesion: 0.08
Nodes (25): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+17 more)

### Community 53 - "devDependencies"
Cohesion: 0.04
Nodes (44): dompurify, lucide-react, oxlint, @types/dompurify, @types/node, dependencies, lucide-react, react (+36 more)

### Community 54 - "Miautrix Mail Server - Production Deployment Guide (Debian LXC)"
Cohesion: 0.13
Nodes (14): 1.1. Install System Dependencies & NGINX, 1.2. Configure Systemd Service for .NET Backend, 1.3. Configure NGINX for Cloudflare Tunnel (`mail.miautrix.tech`), 2.1. Publish .NET 10 Self-Contained Binary, 2.2. Build Frontends (Web Admin & Webmail), 2.3. Apply Database Migrations to `miautrix-mail-pro`, 3.1. Copy Published Files using Windows Built-in `scp`, Architecture & Configuration Summary (+6 more)

### Community 55 - "HeaderRequestContextAccessor"
Cohesion: 0.38
Nodes (5): IHttpContextAccessor, Guid, HeaderRequestContextAccessor, CurrentTenantId, CurrentUserId

### Community 56 - "IAdminService"
Cohesion: 0.16
Nodes (7): MailFlowRuleDto, SecuritySettingsDto, CancellationToken, Guid, IReadOnlyList, Task, IAdminService

### Community 57 - "c"
Cohesion: 0.09
Nodes (74): d(), aC(), aE(), Al(), aw(), Bw(), f(), h() (+66 more)

### Community 58 - "n"
Cohesion: 0.11
Nodes (66): b(), b1(), bd(), bm(), Br(), dm(), dp(), ea() (+58 more)

### Community 59 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 60 - "o"
Cohesion: 0.06
Nodes (62): A(), A1(), ac(), ai(), at(), Bt(), bu(), cg() (+54 more)

### Community 61 - "Zc"
Cohesion: 0.07
Nodes (7): bv(), Ga(), HS(), RS(), Tx(), yN, Zc

### Community 62 - "IMailQueueService"
Cohesion: 0.40
Nodes (6): CancellationToken, Guid, Task, IMailQueueService, IReadOnlyList, QueuePage

### Community 63 - "plugins"
Cohesion: 0.22
Nodes (8): oxc, typescript, warn, plugins, rules, react/only-export-components, react/rules-of-hooks, $schema

### Community 64 - "ew"
Cohesion: 0.13
Nodes (23): ar(), dw(), ew(), f0(), Ff(), Fi(), h0(), io() (+15 more)

### Community 65 - "Miautrix Mail Server"
Cohesion: 0.15
Nodes (12): 1.1 Project Purpose and Business Case, 1.2 High-Level Objectives, 1. Project Initiation & Executive Summary, 2. Project Scope Management (WBS Overview), 3. Project Schedule & Milestone Management, 4. Quality Management & Verification Gates, 5. Risk Management Matrix, 6. Stakeholder & Resource Plan (+4 more)

### Community 66 - "AuditLog"
Cohesion: 0.18
Nodes (10): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Fact (+2 more)

### Community 67 - "ApiResponse"
Cohesion: 0.35
Nodes (11): UpdateSecuritySettingsRequest, ApiResponse, PaginationMeta, CancellationToken, Guid, HttpGet, HttpPatch, IResult (+3 more)

### Community 68 - "User"
Cohesion: 0.17
Nodes (11): Miautrix.Mail.Application.Users, IUserService, User, Email, IsActive, IsService, Name, Fact (+3 more)

### Community 69 - "QueueScreen.tsx"
Cohesion: 0.31
Nodes (4): QueueScreen(), QueueScreenProps, QueueItem, QueueQueryParams

### Community 70 - "Xe"
Cohesion: 0.08
Nodes (47): ap(), bp(), Da(), dg(), el(), em(), Es(), Fb() (+39 more)

### Community 71 - "Guid"
Cohesion: 0.05
Nodes (39): DomainEntity, Guid, Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes (+31 more)

### Community 72 - "ITenantAuthorizationHelper"
Cohesion: 0.04
Nodes (60): AsyncCommand, CommandSettings, BackupSettings, OutputPath, CancellationToken, CommandContext, Guid, Task (+52 more)

### Community 73 - "Epic 2 — Mail transport and policy"
Cohesion: 0.22
Nodes (8): Epic 2 — Mail transport and policy, T10 — IMAP, storage, and attachments, T11 — ManageSieve, T12 — Search, T13 — Rule engine, simulator, explorer, T7 — SMTP listener and queue, T8 — SPF, DKIM, DMARC, T9 — Anti-spam baseline and quarantine

### Community 74 - "QueueRetryCommand"
Cohesion: 0.29
Nodes (7): CancellationToken, CommandContext, Guid, Task, QueueRetryCommand, QueueRetrySettings, Id

### Community 75 - "MailContracts.cs"
Cohesion: 0.24
Nodes (14): DateTimeOffset, Guid, IReadOnlyList, Stream, AttachmentDownloadDto, AttachmentDto, CreateFolderRequest, MessageDetailDto (+6 more)

### Community 76 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 77 - "Miautrix.Mail.sln"
Cohesion: 0.11
Nodes (12): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+4 more)

### Community 78 - "SmtpResponse"
Cohesion: 0.29
Nodes (8): SmtpResponse, IsSuccess, CancellationToken, Guid, Response, Task, ISmtpSubmissionHandler, SmtpSubmissionHandler

### Community 79 - "rb"
Cohesion: 0.11
Nodes (30): Ar(), Ba(), bc(), Ca(), cb(), ec(), fm(), gi() (+22 more)

### Community 80 - "Epic 1 — Core platform"
Cohesion: 0.25
Nodes (7): Epic 1 — Core platform, T1 — Solution scaffold and CI, T2 — Domain model and EF Core schema, T3 — Seed data and indexes, T4 — Identity, T5 — Authorization and tenant isolation, T6 — Audit trail

### Community 81 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Architecture rules, Commands, Data rules, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 82 - "Miautrix Mail Server"
Cohesion: 0.22
Nodes (8): Architecture rules, Commands, Data rules, Discovery workflow, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 83 - "QuarantineItem"
Cohesion: 0.07
Nodes (27): DateTimeOffset, BackupJob, ArchivePath, CompletedAt, ErrorMessage, Name, SizeBytes, Status (+19 more)

### Community 84 - "Dt"
Cohesion: 0.10
Nodes (37): ah(), Ao(), d1(), Do(), Dt(), Ge(), h(), hn() (+29 more)

### Community 86 - "PulsePoint"
Cohesion: 0.17
Nodes (8): Border Radius, Colors, Do's and Don'ts, Elevation, Overview, PulsePoint, Spacing, Typography

### Community 88 - "5. Recommended Technology Stack"
Cohesion: 0.25
Nodes (8): 5. Recommended Technology Stack, Backend, Caching, Database, Frontend, Logging, Scheduling, Storage

### Community 89 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 90 - "Miautrix Mail Server"
Cohesion: 0.22
Nodes (9): 📐 Architecture & Design, Build & Test Commands, 🛠️ Deploying & Updating, ⚙️ Development Environment, Miautrix Mail Server, ✉️ Outbound transport model, 🚀 Project Status, 🔒 Security Invariants (+1 more)

### Community 91 - "Domain"
Cohesion: 0.07
Nodes (30): Domain, CloudflareWorkerUrl, CloudflareZoneId, DkimPublicKey, DkimSelector, DmarcRecord, IsPrimary, IsVerified (+22 more)

### Community 92 - "Ue"
Cohesion: 0.07
Nodes (53): Ax(), b_(), BE(), bo, cC(), cE(), ev(), _f() (+45 more)

### Community 93 - "ImapSession"
Cohesion: 0.19
Nodes (14): CancellationToken, GeneratedRegex, Guid, IReadOnlyList, Regex, Stream, Task, ImapCommandResult (+6 more)

### Community 94 - "AdminContracts.cs"
Cohesion: 0.09
Nodes (35): DateTimeOffset, Dictionary, Guid, IReadOnlyList, List, AntiMalwareStatusDto, AssignMailboxRequest, AuditLogDto (+27 more)

### Community 95 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.17
Nodes (11): Admin screens, AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Per-domain settings pattern, Provider seams, Quarantine item lifecycle, Scripts, Spam pipeline (+3 more)

### Community 96 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.17
Nodes (11): Admin screens, AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Per-domain settings pattern, Provider seams, Quarantine item lifecycle, Scripts, Spam pipeline (+3 more)

### Community 97 - "Miautrix Mail Server — Errors, Issues & Review Log"
Cohesion: 0.13
Nodes (14): 1. System & OS Environment Issues, 2. Frontend & UI Runtime Issues, 3. NGINX & Ingress Routing Issues, 4. Backend & Database Integrity Checks, 5. Items to Review & Verify Later, 6. Transport / Protocol Listener Issues, 7. Cloudflare Workers Integration, Boundary: Cloudflare is transport only (+6 more)

### Community 98 - "QueueStatusFilter"
Cohesion: 0.29
Nodes (7): DateTimeOffset, QueueFilter, QueueStatusFilter, DeadLetter, Delivered, Queued, Retrying

### Community 99 - "Argon2idPasswordHasher"
Cohesion: 0.38
Nodes (4): Argon2idHasherOptions, Argon2idHasherOptions, Argon2idPasswordHasher, CurrentOptions

### Community 100 - "Miautrix.Mail.Identity.csproj"
Cohesion: 0.14
Nodes (12): Konscious.Security.Cryptography.Argon2 (1.3.1), Otp.NET (1.4.1), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4) (+4 more)

### Community 101 - ".CreateArchiveAsync"
Cohesion: 0.18
Nodes (15): IEnumerable, CancellationToken, DateTimeOffset, Dictionary, Guid, HashSet, IReadOnlyCollection, IReadOnlyList (+7 more)

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

### Community 109 - "MailQueueService"
Cohesion: 0.39
Nodes (5): CancellationToken, DateTimeOffset, Guid, Task, MailQueueService

### Community 110 - "MailFlowScreen.tsx"
Cohesion: 0.50
Nodes (3): MailFlowScreen(), MailFlowScreenProps, MailFlowRuleItem

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

### Community 119 - ".on"
Cohesion: 0.12
Nodes (24): $a(), a2(), As(), copy(), g0(), E(), M(), h2() (+16 more)

### Community 120 - "SmtpListenerService"
Cohesion: 0.14
Nodes (16): SmtpInboundMxListener, SmtpSubmissionImplicitTlsListener, SmtpSubmissionStartTlsListener, WorkerHostOptions, Hostname, CancellationToken, ILogger, IServiceScopeFactory (+8 more)

### Community 121 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 122 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 123 - "TlsCertificateProvider"
Cohesion: 0.50
Nodes (3): X509Certificate2, TlsCertificateProvider, Certificate

### Community 124 - "MailQueueController"
Cohesion: 0.20
Nodes (13): ReassignQueueItemRequest, CancellationToken, DateTimeOffset, Guid, HttpDelete, HttpGet, HttpPost, IResult (+5 more)

### Community 125 - "lg"
Cohesion: 0.17
Nodes (23): Aa(), ag(), bi(), cd(), di(), gp(), gu(), Hb() (+15 more)

### Community 126 - "20260927020255_UpdateFlagSystem.Designer.cs"
Cohesion: 0.18
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, UpdateFlagSystem

### Community 128 - "WebmailApiClient.ts"
Cohesion: 0.18
Nodes (11): CalendarView(), CalendarViewProps, ContactsView(), ContactsViewProps, WebmailLoginResult, CalendarEvent, Contact, EmailAttachment (+3 more)

### Community 129 - "UsersScreen.tsx"
Cohesion: 0.22
Nodes (12): AssignMailboxModal(), AssignMailboxModalProps, splitEmail(), MailboxDelegatePicker(), MailboxDelegatePickerProps, UsersScreen(), UsersScreenProps, AdminUserItem (+4 more)

### Community 130 - "Section 20 — Verification status (READ FIRST)"
Cohesion: 0.50
Nodes (4): A correction to the record, Section 20 — Verification status (READ FIRST), What was NOT verified, What was verified

### Community 131 - "format-status.js"
Cohesion: 0.50
Nodes (3): fs, tasks, ref_fs

### Community 132 - "AppDbContext"
Cohesion: 0.03
Nodes (57): DbContext, DbSet, IDesignTimeDbContextFactory, ModelBuilder, AppDbContext, Aliases, ApiKeys, ApplicationPasswords (+49 more)

### Community 133 - "bg"
Cohesion: 0.18
Nodes (21): bg(), Bn(), C1(), cc(), cs(), dc(), ed(), Gs() (+13 more)

### Community 134 - "bb"
Cohesion: 0.18
Nodes (19): bb(), Fe(), fp(), gg(), hp(), ia(), it(), Lb() (+11 more)

### Community 135 - "SieveRulesView.tsx"
Cohesion: 0.25
Nodes (7): FLAG_COLORS, FLAG_LABELS, FlagAlertConfig, FlagColor, SieveRulesView(), SieveRulesViewProps, SieveFilterRule

### Community 136 - "ImapAuthenticatorTests"
Cohesion: 0.25
Nodes (10): SeedResult, CancellationToken, Task, ImapAuthenticator, Fact, Guid, List, Task (+2 more)

### Community 137 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 138 - ".BuildTargetModel"
Cohesion: 0.21
Nodes (5): DateTimeOffset, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder

### Community 139 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 140 - ".DeleteMailboxAsync"
Cohesion: 0.22
Nodes (11): DeleteMailboxRequest, DeleteMailboxResult, DeleteMailboxResult, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost (+3 more)

### Community 141 - "React + TypeScript + Vite"
Cohesion: 0.50
Nodes (3): Expanding the Oxlint configuration, React Compiler, React + TypeScript + Vite

### Community 145 - "13. Mail Flow Architecture"
Cohesion: 0.67
Nodes (3): 13. Mail Flow Architecture, Inbound, Outbound

### Community 146 - "14. Mail Flow Rule Engine"
Cohesion: 0.67
Nodes (3): 14. Mail Flow Rule Engine, Actions, Conditions

### Community 148 - "CloudflareTransportTests"
Cohesion: 0.10
Nodes (23): Exception, IClassFixture, Program, Fact, Guid, Task, WebApplicationFactory, AuthApiTests (+15 more)

### Community 155 - "Miautrix.Mail.Cli.csproj"
Cohesion: 0.17
Nodes (10): Microsoft.Extensions.Configuration (10.0.4), Microsoft.Extensions.Configuration.EnvironmentVariables (10.0.4), Microsoft.Extensions.DependencyInjection (10.0.4), Spectre.Console (0.55.0), Spectre.Console.Cli (0.55.0), net10.0, Microsoft.NET.Sdk, net10.0 (+2 more)

### Community 156 - "SpamVerdict"
Cohesion: 0.17
Nodes (12): SpamVerdict, DkimResult, DmarcResult, DnsblListed, Greylisted, IsSpam, ReasonsJson, Recipient (+4 more)

### Community 157 - "ITotpService"
Cohesion: 0.29
Nodes (3): otpnet, ITotpService, TotpService

### Community 158 - "SystemController"
Cohesion: 0.40
Nodes (8): CancellationToken, HttpGet, HttpPost, IResult, ProducesResponseType, Task, BackupActionResult, SystemController

### Community 159 - "Future implementation items"
Cohesion: 0.29
Nodes (6): DANE / DNSSEC validation, DKIM key rotation, Future implementation items, Implemented now, Security Roadmap, TLS policy controls

### Community 161 - "RecordingTransport"
Cohesion: 0.10
Nodes (23): IHttpClientFactory, CancellationToken, Task, TimeSpan, CloudflareApiMailTransport, Mode, CancellationToken, Guid (+15 more)

### Community 162 - "Miautrix.Mail.EndToEndTests.csproj"
Cohesion: 0.29
Nodes (6): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 163 - "AuthStatus"
Cohesion: 0.33
Nodes (6): AuthStatus, Failed, LockedOut, MfaRequired, Success, UserNotFound

### Community 164 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

### Community 165 - "ImapState"
Cohesion: 0.40
Nodes (5): ImapState, Authenticated, LoggedOut, NotAuthenticated, Selected

### Community 166 - "QuarantineController"
Cohesion: 0.35
Nodes (10): CancellationToken, DateTimeOffset, Guid, HttpDelete, HttpGet, HttpPost, IResult, ProducesResponseType (+2 more)

### Community 167 - "ISessionManager"
Cohesion: 0.19
Nodes (14): SessionToken, DateTimeOffset, Guid, RefreshToken, TimeSpan, AuthToken, ISessionManager, DefaultRefreshLifetime (+6 more)

### Community 169 - "MailQueueApiTests"
Cohesion: 0.35
Nodes (5): Fact, Guid, Task, WebApplicationFactory, MailQueueApiTests

### Community 178 - "ImapListenerService"
Cohesion: 0.31
Nodes (8): BackgroundService, CancellationToken, ILogger, IServiceScopeFactory, Task, TcpClient, X509Certificate2, ImapListenerService

### Community 181 - "MailboxService"
Cohesion: 0.24
Nodes (12): CancellationToken, Guid, IReadOnlyList, Task, IMailboxService, CancellationToken, Guid, IReadOnlyList (+4 more)

### Community 184 - "client.ts"
Cohesion: 0.09
Nodes (31): BackupScreen(), BackupScreenProps, DomainsScreenProps, LicensingScreen(), LicensingScreenProps, LogsScreen(), LogsScreenProps, SystemScreen() (+23 more)

### Community 186 - "Miautrix.Mail.Domain.csproj"
Cohesion: 0.09
Nodes (19): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.EntityFrameworkCore.Design (10.0.4) (+11 more)

### Community 189 - "Miautrix.Mail.IntegrationTests.csproj"
Cohesion: 0.15
Nodes (11): Microsoft.AspNetCore.Mvc.Testing (10.0.4), Microsoft.EntityFrameworkCore.InMemory (10.0.4), net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3) (+3 more)

### Community 193 - "IMailStorage"
Cohesion: 0.28
Nodes (7): CancellationToken, Stream, Task, FileSystemMailStorage, IMailStorage, StorageWriteResult, ImapTests

### Community 195 - "WebmailApiClient"
Cohesion: 0.11
Nodes (3): WebmailApiClient, EmailMessage, Mailbox

### Community 196 - "SimulateSampleMessage"
Cohesion: 0.18
Nodes (11): Dictionary, SimulateRuleRequest, SampleMessage, SimulateSampleMessage, HasAttachment, Headers, Recipient, Sender (+3 more)

### Community 198 - "Miautrix.Mail.Worker.csproj"
Cohesion: 0.12
Nodes (16): Microsoft.Extensions.Hosting (10.0.4), Microsoft.Extensions.Logging.Console (10.0.4), Microsoft.NET.Sdk.Worker, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0 (+8 more)

### Community 200 - "Iris Pay"
Cohesion: 0.11
Nodes (18): 1. Atmosphere, 2. Palette, 3. Typography, 4. Buttons, 5. Cards, 6. Charts, 7. Spacing, 8. Depth & elevation (+10 more)

### Community 201 - "AntiMalwareScreen.tsx"
Cohesion: 0.13
Nodes (13): AntiMalwareScreen(), AntiMalwareScreenProps, formatBytes(), makeClient(), NOTE: row-level controls are now limited to Release + Discard; other power…, AntiMalwareStatus, QuarantineItem, ref_dompurify (+5 more)

### Community 203 - "ApiExceptionMiddleware"
Cohesion: 0.43
Nodes (5): HttpContext, ILogger, RequestDelegate, Task, ApiExceptionMiddleware

### Community 204 - "Pro tokens"
Cohesion: 0.18
Nodes (11): Accessibility (WCAG 2.1), Button, Card, Content, Density, Elevation, Input, Motion (+3 more)

### Community 205 - "Tokens"
Cohesion: 0.17
Nodes (12): Borders, Buttons, Charts, Colors, Ghost, Outline, Primary, Radius (+4 more)

### Community 207 - ".BuildModel"
Cohesion: 0.33
Nodes (5): ModelSnapshot, DateTimeOffset, Guid, ModelBuilder, AppDbContextModelSnapshot

### Community 209 - ".Error"
Cohesion: 0.25
Nodes (6): IReadOnlyDictionary, ApiError, HttpContext, IReadOnlyDictionary, IResult, ApiResults

### Community 211 - "UnitTestProjectSmokeTests.cs"
Cohesion: 0.40
Nodes (3): Miautrix.Mail.UnitTests, Fact, UnitTestProjectSmokeTests

### Community 212 - "EndToEndTestProjectSmokeTests.cs"
Cohesion: 0.40
Nodes (3): Miautrix.Mail.EndToEndTests, Fact, EndToEndTestProjectSmokeTests

### Community 213 - "lxc-prepare-storage.sh"
Cohesion: 0.60
Nodes (4): count_blobs(), current_env_dir(), effective_dir(), lxc-prepare-storage.sh script

### Community 221 - "update-prod-database.sh"
Cohesion: 0.50
Nodes (3): ASPNETCORE_ENVIRONMENT, MIAUTRIX_DB_CONNECTION, update-prod-database.sh script

### Community 223 - "Miautrix.Mail.Application.csproj"
Cohesion: 0.14
Nodes (13): DnsClient (1.8.0), Microsoft.AspNetCore.OpenApi (10.0.4), Microsoft.OpenApi (2.7.5), Microsoft.NET.Sdk.Web, net10.0, Microsoft.Extensions.Http (10.0.4), Microsoft.NET.Sdk, net10.0 (+5 more)

### Community 224 - "RequestIdMiddleware"
Cohesion: 0.29
Nodes (5): HttpContext, RequestDelegate, Task, RequestIdMiddleware, system_diagnostics

### Community 228 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 229 - "ApiJson.cs"
Cohesion: 0.50
Nodes (3): JsonSerializerOptions, ApiJson, system_text_json_serialization

### Community 230 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 231 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 235 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 237 - ".GetAlertConfigs"
Cohesion: 0.15
Nodes (14): RetryQueueItemRequest, Reason, SetAlertConfigRequest, AlertConfigurationJson, SetFlagRequest, Color, CancellationToken, Guid (+6 more)

### Community 255 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 256 - ".BuildTargetModel"
Cohesion: 0.14
Nodes (9): DateTimeOffset, Guid, ModelBuilder, DateTimeOffset, Guid, ModelBuilder, DateTimeOffset, Guid (+1 more)

### Community 260 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 261 - "MessageService"
Cohesion: 0.34
Nodes (5): CancellationToken, Guid, IReadOnlyList, Task, MessageService

### Community 263 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 268 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 270 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 273 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 285 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

## Knowledge Gaps
- **932 isolated node(s):** `InboxViewProps`, `FLAG_COLORS`, `FlagColor`, `FLAG_LABELS`, `SYSTEM_FOLDER_ORDER` (+927 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1386 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **25 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AppDbContext` connect `AppDbContext` to `AdminService`, `MessageService`, `Message`, `Miautrix.Mail.Persistence`, `.ProcessItemAsync`, `ImapAuthenticatorTests`, `.SimulateAsync`, `MailboxAdminApiTests`, `CloudflareTransportTests`, `TenantScopedEntityBase`, `SpamVerdict`, `ISmtpQueueManager`, `.Main`, `OutboundQueueDispatcher`, `SmtpAuthenticator`, `Mailbox`, `.RunOnePassAsync`, `SmtpQueueItem`, `.SearchAsync`, `MailQueueApiTests`, `.ApplyMigrations`, `AuthService`, `ImapListenerService`, `MailboxService`, `IMailStorage`, `AuditLog`, `User`, `Guid`, `ITenantAuthorizationHelper`, `QueueRetryCommand`, `QuarantineItem`, `EfPermissionRepository`, `Domain`, `ImapSession`, `.CreateArchiveAsync`, `MailQueueService`?**
  _High betweenness centrality (0.079) - this node is a cross-community bridge._
- **Why does `dN()` connect `dN` to `bg`, `Xe`, `bb`, `A`, `index-7_LIhLyZ.js`, `rb`, `Dt`, `c`, `n`, `o`, `lg`, `rN`?**
  _High betweenness centrality (0.038) - this node is a cross-community bridge._
- **Why does `Miautrix.Mail.Domain` connect `Miautrix.Mail.Persistence` to `Miautrix.Mail.Security`, `Mailbox`, `User`, `SmtpQueueItem`, `.SimulateAsync`, `system`, `Guid`, `TenantScopedEntityBase`, `AdminContracts.cs`?**
  _High betweenness centrality (0.021) - this node is a cross-community bridge._
- **Are the 25 inferred relationships involving `dN()` (e.g. with `_1()` and `Am()`) actually correct?**
  _`dN()` has 25 INFERRED edges - model-reasoned connections that need verification._
- **Are the 3 inferred relationships involving `AppDbContext` (e.g. with `.Main()` and `.ApplyMigrations()`) actually correct?**
  _`AppDbContext` has 3 INFERRED edges - model-reasoned connections that need verification._
- **Are the 16 inferred relationships involving `o()` (e.g. with `EN()` and `ey()`) actually correct?**
  _`o()` has 16 INFERRED edges - model-reasoned connections that need verification._
- **What connects `InboxViewProps`, `FLAG_COLORS`, `FlagColor` to the rest of the system?**
  _932 weakly-connected nodes found - possible documentation gaps or missing edges._