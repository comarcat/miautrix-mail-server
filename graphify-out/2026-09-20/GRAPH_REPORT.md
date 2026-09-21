# Graph Report - miautrix-mail-server  (2026-09-20)

## Corpus Check
- 261 files · ~4,645,334 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 3807 nodes · 10232 edges · 187 communities (158 shown, 22 thin omitted)
- Extraction: 86% EXTRACTED · 14% INFERRED · 0% AMBIGUOUS · INFERRED: 1387 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `70efee75`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- index-DnIMIEnf.js
- sN
- hN
- index-BWPawaEr.js
- r
- MailboxService
- system
- a
- .ListAsync
- n
- Miautrix.Mail.Domain
- admin/src/App.tsx
- C
- AppDbContext
- r
- TenantScopedEntityBase
- ab
- ImapSession
- zb
- AdminApiClient
- Z
- i
- fN
- .SimulateAsync
- devDependencies
- react
- U
- yl
- N
- ft
- .ProcessInboundMessageAsync
- u
- system_collections_generic
- Miautrix.Mail.Persistence
- pl
- .Main
- devDependencies
- o
- DkimService
- AdminService
- webmail/src/App.tsx
- devDependencies
- Fd
- Miautrix Mail Server
- b
- Miautrix Mail Server — Implementation Blueprint
- af
- IAdminService
- Message
- t
- AuthService
- QuarantineItem
- k
- IRequestContextAccessor
- InMemorySecurityEventSink
- User
- compilerOptions
- Miautrix.Mail.sln
- Folder
- compilerOptions
- nr
- TenantAuthorizationHelper
- compilerOptions
- Miautrix.Mail.ProtocolTests.csproj
- Ut
- Components
- AdminContracts.cs
- Yb
- compilerOptions
- Miautrix Mail Server - Production Deployment Guide (Debian LXC)
- ITotpService
- .StageAndSwitchAsync
- compilerOptions
- WebmailApiClient
- Miautrix Mail Server
- AuthApiTests
- ApiResponse
- .HandleDataAsync
- Miautrix Mail Server
- Miautrix.Mail.Identity.csproj
- Iris Pay
- ISessionManager
- .Update
- SmtpQueueItem
- .Main
- Tokens
- IdempotencyStore.cs
- Miautrix.Mail.Application.csproj
- DomainController
- Pro tokens
- AuthController
- .List
- .SeedTenantAndMailboxAsync
- Miautrix.Mail.IntegrationTests.csproj
- Miautrix.Mail.Cli.csproj
- AI Build Instructions
- MailQueueApiTests
- Ia
- Miautrix.Mail.Persistence.csproj
- Epic 2 — Mail transport and policy
- SpamVerdict
- Mailbox
- AddMailAndSpamColumns
- MailContracts.cs
- AddAuditLogColumns
- SystemController
- UsersScreen.tsx
- Miautrix.Mail.Web.csproj
- plugins
- .BuildTargetModel
- MessageService
- SystemInfoCommand.cs
- package.json
- Epic 1 — Core platform
- .Get
- Miautrix Mail Server
- SearchTests.cs
- Domain
- AddCatalogFields
- .Error
- ApiExceptionMiddleware
- Miautrix Mail Server
- .SeedTenantAndUserAsync
- IUserService.cs
- PulsePoint
- Miautrix.Mail.EndToEndTests.csproj
- 5. Recommended Technology Stack
- AGENTS.md — Miautrix Mail Server
- ref_vitejs_plugin_react
- AGENTS.md — Miautrix Mail Server
- Miautrix Mail Server — Errors, Issues & Review Log
- Epic 3 — Surfaces
- ref_testing_library_jest_dom_vitest
- format-status.js
- .BuildTargetModel
- .BuildModel
- ApiJson.cs
- versions.mjs
- desktop/tsconfig.json
- generate_migration.sh
- init-database-scratch.sh
- update-database.sh
- webmail/tsconfig.json
- fix_tasks.py
- Epic 4 — Operations
- Rule: secrets and logging
- Rule: secrets and logging
- 2. Palette
- Argon2idPasswordHasher
- 3. Core Architectural Principles
- 9. Identity Architecture
- Section 19 — Verify commands
- Section 4 — Stack
- Section 5 — Data model
- Section 9 — Build order
- verify-task
- verify-task
- Section 20 — Verification status (READ FIRST)
- .BuildTargetModel
- .BuildTargetModel
- .BuildTargetModel
- React + TypeScript + Vite
- 13. Mail Flow Architecture
- 14. Mail Flow Rule Engine
- workspace/.claude/rules/tenancy.md
- .claude/rules/tenancy.md
- SanitizedMessageBody.tsx
- IMessageService
- Miautrix.Mail.Domain.csproj
- Attachment
- AuthStatus
- MailFlowRule
- EfPermissionRepository
- .Up
- JsonSerializerOptions
- MigrationBuilder
- HashSet
- HttpContext
- RequestDelegate
- WebApplicationFactory

## God Nodes (most connected - your core abstractions)
1. `sN()` - 409 edges
2. `AppDbContext` - 149 edges
3. `o()` - 99 edges
4. `i()` - 77 edges
5. `r()` - 60 edges
6. `AdminApiClient` - 56 edges
7. `n()` - 56 edges
8. `dg()` - 55 edges
9. `TenantScopedEntityBase` - 54 edges
10. `n()` - 53 edges

## Surprising Connections (you probably didn't know these)
- `MailApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/MailApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `ImapTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Imap/ImapTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `MailFlowTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `SieveTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Sieve/SieveTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `SearchTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Search/SearchTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs

## Import Cycles
- None detected.

## Communities (187 total, 22 thin omitted)

### Community 0 - "index-DnIMIEnf.js"
Cohesion: 0.02
Nodes (156): a_, A0(), Aj(), Ar(), aS(), av, bf(), bN() (+148 more)

### Community 1 - "sN"
Cohesion: 0.04
Nodes (66): sN(), A1(), Au(), bh(), Cl(), cx(), de(), dl() (+58 more)

### Community 2 - "hN"
Cohesion: 0.06
Nodes (73): aE, de(), fe(), me(), ne(), aN(), ee(), i() (+65 more)

### Community 3 - "index-BWPawaEr.js"
Cohesion: 0.04
Nodes (87): Dr(), ah(), ap(), bh(), Br(), ch(), cn(), constructor() (+79 more)

### Community 4 - "r"
Cohesion: 0.07
Nodes (93): _0, f(), h(), m(), p(), z(), aT, b_() (+85 more)

### Community 5 - "MailboxService"
Cohesion: 0.25
Nodes (12): CancellationToken, Guid, IReadOnlyList, Task, IMailboxService, CancellationToken, Guid, IReadOnlyList (+4 more)

### Community 6 - "system"
Cohesion: 0.11
Nodes (21): Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, Migration, MigrationBuilder, npgsql_entityframeworkcore_postgresql_metadata, DateTimeOffset (+13 more)

### Community 7 - "a"
Cohesion: 0.06
Nodes (40): _2(), a2(), aw(), B0(), bj(), f(), h(), m() (+32 more)

### Community 8 - ".ListAsync"
Cohesion: 0.05
Nodes (44): Miautrix.Mail.Application.Queue, Exception, CancellationToken, Guid, Task, IMailQueueService, CancellationToken, DateTimeOffset (+36 more)

### Community 9 - "n"
Cohesion: 0.09
Nodes (41): ax(), bb(), bm(), bo(), C(), cm(), d(), l() (+33 more)

### Community 10 - "Miautrix.Mail.Domain"
Cohesion: 0.09
Nodes (25): Miautrix.Mail.Protocols.Imap, Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Web, Miautrix.Mail.Storage, Miautrix.Mail.Identity, Miautrix.Mail.SecurityTests.Isolation, Miautrix.Mail.IntegrationTests.Api (+17 more)

### Community 11 - "admin/src/App.tsx"
Cohesion: 0.10
Nodes (32): apiClient, BackupScreen(), BackupScreenProps, DashboardScreen(), DashboardStats, DomainsScreen(), DomainsScreenProps, LicensingScreen() (+24 more)

### Community 12 - "C"
Cohesion: 0.11
Nodes (37): am(), Au(), Bc(), bm(), C(), da(), dm(), Du() (+29 more)

### Community 13 - "AppDbContext"
Cohesion: 0.04
Nodes (53): DbContext, DbSet, IDesignTimeDbContextFactory, ModelBuilder, AppDbContext, Aliases, ApiKeys, ApplicationPasswords (+45 more)

### Community 14 - "r"
Cohesion: 0.19
Nodes (37): co(), fh(), hn(), ip(), ju(), km(), lh(), Li() (+29 more)

### Community 15 - "TenantScopedEntityBase"
Cohesion: 0.05
Nodes (73): Guid, Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, Deploy (+65 more)

### Community 16 - "ab"
Cohesion: 0.11
Nodes (36): _1(), ab(), Ae(), Cb(), Cc(), e1(), ec(), ga() (+28 more)

### Community 17 - "ImapSession"
Cohesion: 0.09
Nodes (28): CancellationToken, GeneratedRegex, Guid, IReadOnlyList, Regex, Stream, Task, ImapCommandResult (+20 more)

### Community 18 - "zb"
Cohesion: 0.18
Nodes (24): as(), cd(), cs(), db(), Dt(), Ee(), fg(), hg() (+16 more)

### Community 19 - "AdminApiClient"
Cohesion: 0.13
Nodes (4): AdminApiClient, AdminUserItem, ApiResponse, DomainItem

### Community 20 - "Z"
Cohesion: 0.12
Nodes (30): bi(), bu(), eu(), Fu(), gu(), hu(), iu(), jl() (+22 more)

### Community 21 - "i"
Cohesion: 0.08
Nodes (55): ac(), al(), bs(), cl(), cs(), dc(), ds(), eo() (+47 more)

### Community 22 - "fN"
Cohesion: 0.12
Nodes (3): fN, Pp(), t_()

### Community 23 - ".SimulateAsync"
Cohesion: 0.07
Nodes (38): IDisposable, IServiceCollection, IServiceProvider, ITypeRegistrar, ITypeResolver, Func, TypeRegistrar, TypeResolver (+30 more)

### Community 24 - "devDependencies"
Cohesion: 0.05
Nodes (42): dompurify, oxlint, @types/dompurify, @types/node, dependencies, react, react-dom, devDependencies (+34 more)

### Community 25 - "react"
Cohesion: 0.08
Nodes (18): App(), AntiMalwareScreen(), AntiSpamScreen(), IdentityScreen(), ReportsScreen(), initialEdges, initialNodes, RuleDesignerScreen() (+10 more)

### Community 26 - "U"
Cohesion: 0.10
Nodes (38): U(), ah(), Al(), b1(), bg(), bx(), ch(), Cu() (+30 more)

### Community 27 - "yl"
Cohesion: 0.09
Nodes (36): Ao(), as(), Bd(), bl(), ca(), d(), ef(), ep() (+28 more)

### Community 28 - "N"
Cohesion: 0.08
Nodes (42): b2(), bw(), copy(), Cr(), d2(), e2(), E(), f2() (+34 more)

### Community 29 - "ft"
Cohesion: 0.14
Nodes (31): ae(), ce(), ct(), dn(), dp(), ee(), fe(), fm() (+23 more)

### Community 30 - ".ProcessInboundMessageAsync"
Cohesion: 0.10
Nodes (26): Miautrix.Mail.AntiSpam, Miautrix.Mail.IntegrationTests.AntiSpam, CancellationToken, Guid, List, QuarantineItem, Task, IQuarantineService (+18 more)

### Community 31 - "u"
Cohesion: 0.19
Nodes (36): cg(), Dc(), em(), c(), Ce(), D(), g(), ge() (+28 more)

### Community 32 - "system_collections_generic"
Cohesion: 0.18
Nodes (15): Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.Application.Auth, Miautrix.Mail.Application.Admin, Miautrix.Mail.Web.Contracts, Miautrix.Mail.Application.Mail, microsoft_aspnetcore_http, microsoft_aspnetcore_mvc (+7 more)

### Community 33 - "Miautrix.Mail.Persistence"
Cohesion: 0.17
Nodes (16): Miautrix.Mail.IntegrationTests.Licensing, Miautrix.Mail.Cli.Commands, Miautrix.Mail.Infrastructure.Backup, Miautrix.Mail.Cli, Miautrix.Mail.Persistence, Miautrix.Mail.Protocols.Sieve, Miautrix.Mail.Security, Miautrix.Mail.ProtocolTests.Sieve (+8 more)

### Community 34 - "pl"
Cohesion: 0.10
Nodes (40): Ai(), bo(), cc(), dh(), Di(), dl(), ea(), Ei() (+32 more)

### Community 35 - ".Main"
Cohesion: 0.07
Nodes (27): ApiBehaviorOptions, ApiExceptionMiddleware, Argon2idPasswordHasher, AuthService, EfPermissionRepository, HashSet, IAuthService, IMailQueueService (+19 more)

### Community 36 - "devDependencies"
Cohesion: 0.06
Nodes (33): dependencies, react, react-dom, @xyflow/react, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react (+25 more)

### Community 37 - "o"
Cohesion: 0.10
Nodes (31): o(), ao(), ar(), At(), dg(), dx(), en(), ex() (+23 more)

### Community 38 - "DkimService"
Cohesion: 0.08
Nodes (24): Body, Headers, RSA, CancellationToken, Guid, HashSet, Task, SieveParser (+16 more)

### Community 39 - "AdminService"
Cohesion: 0.16
Nodes (13): LookupClient, CreateDomainRequest, DomainDto, UpdateDomainRequest, CancellationToken, DateTimeOffset, Guid, IReadOnlyList (+5 more)

### Community 40 - "webmail/src/App.tsx"
Cohesion: 0.11
Nodes (26): INITIAL_CONTACTS, INITIAL_EVENTS, INITIAL_MAILBOXES, INITIAL_MESSAGES, INITIAL_RULES, CalendarView(), CalendarViewProps, ComposerView() (+18 more)

### Community 41 - "devDependencies"
Cohesion: 0.06
Nodes (31): dependencies, react, react-dom, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @types/react (+23 more)

### Community 42 - "Fd"
Cohesion: 0.11
Nodes (27): af(), bt(), cf(), df(), Fd(), ff(), ht(), Ia() (+19 more)

### Community 43 - "Miautrix Mail Server"
Cohesion: 0.08
Nodes (24): 10. Authentication and Authorization, 11. MFA, 12. Security Event Codes, 15. Graphical Mail Flow Designer, 16. Mail Flow Simulator, 17. Mail Flow Explorer, 18. Restricted Device Relay, 19. Anti-Spam Architecture (+16 more)

### Community 44 - "b"
Cohesion: 0.13
Nodes (18): Hp(), an(), b(), Bp(), cm(), em(), fp(), Gp() (+10 more)

### Community 45 - "Miautrix Mail Server — Implementation Blueprint"
Cohesion: 0.10
Nodes (21): 2.1 In scope for v1, 2.2 Out of scope for v1, 2.3 Non-goals, Miautrix Mail Server — Implementation Blueprint, Provider seams, Section 0 — How to use this blueprint, Section 10 — Workspace, Section 11 — Testing (+13 more)

### Community 46 - "af"
Cohesion: 0.13
Nodes (23): af, Ba(), cw(), d_(), ew(), $f(), Hr(), If() (+15 more)

### Community 47 - "IAdminService"
Cohesion: 0.18
Nodes (13): DateTimeOffset, Guid, AdminUserDto, AuditLogDto, BackupJobDto, LicensingDto, MailFlowRuleDto, QuarantineItemDto (+5 more)

### Community 48 - "Message"
Cohesion: 0.10
Nodes (28): Miautrix.Mail.Licensing, InvalidOperationException, Message, BodyHtml, BodyText, ContentHash, Date, Flags (+20 more)

### Community 49 - "t"
Cohesion: 0.10
Nodes (38): ag(), am(), C1(), ea(), n(), Eo(), et(), fn() (+30 more)

### Community 50 - "AuthService"
Cohesion: 0.16
Nodes (16): DateTimeOffset, Guid, IReadOnlyList, AuthResult, AuthUserDto, ChangePasswordResult, CancellationToken, Guid (+8 more)

### Community 51 - "QuarantineItem"
Cohesion: 0.07
Nodes (29): DateTimeOffset, BackupJob, ArchivePath, CompletedAt, ErrorMessage, Name, SizeBytes, Status (+21 more)

### Community 52 - "k"
Cohesion: 0.42
Nodes (9): Fo(), Ja(), k(), Ka(), ko(), Oo(), qa(), Vc() (+1 more)

### Community 53 - "IRequestContextAccessor"
Cohesion: 0.14
Nodes (19): ControllerBase, IHttpContextAccessor, AuditController, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost (+11 more)

### Community 54 - "InMemorySecurityEventSink"
Cohesion: 0.16
Nodes (19): IPasswordHasher, DateTimeOffset, Guid, IReadOnlyList, List, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink (+11 more)

### Community 55 - "User"
Cohesion: 0.12
Nodes (16): DomainEntity, Permission, Code, Name, User, Email, IsActive, Name (+8 more)

### Community 56 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 57 - "Miautrix.Mail.sln"
Cohesion: 0.08
Nodes (18): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+10 more)

### Community 58 - "Folder"
Cohesion: 0.11
Nodes (22): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Folder (+14 more)

### Community 59 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 60 - "nr"
Cohesion: 0.18
Nodes (20): ed(), eg(), ep(), Il(), jr(), k1(), Kl(), nd() (+12 more)

### Community 61 - "TenantAuthorizationHelper"
Cohesion: 0.20
Nodes (8): Guid, IPermissionRepository, LastOwnerDemotionException, TenantAuthorizationHelper, Fact, Guid, IsolationTests, MockPermissionRepo

### Community 62 - "compilerOptions"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+14 more)

### Community 63 - "Miautrix.Mail.ProtocolTests.csproj"
Cohesion: 0.15
Nodes (11): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3) (+3 more)

### Community 64 - "Ut"
Cohesion: 0.13
Nodes (23): Ai(), Ca(), Ci(), Do(), dr(), fr(), $g(), hd() (+15 more)

### Community 65 - "Components"
Cohesion: 0.12
Nodes (16): Buttons, Cards, Checkboxes, Chips, Components, Default Item, Disabled State, Filter Chip (+8 more)

### Community 66 - "AdminContracts.cs"
Cohesion: 0.10
Nodes (19): Dictionary, List, AuditFilter, CreateRuleRequest, CreateUserRequest, DashboardSummaryDto, QuarantineFilter, RuleSampleMessage (+11 more)

### Community 67 - "Yb"
Cohesion: 0.23
Nodes (14): ds(), gb(), hr(), ib(), ix(), kb(), nx(), pd() (+6 more)

### Community 68 - "compilerOptions"
Cohesion: 0.10
Nodes (19): node, compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, noEmit (+11 more)

### Community 69 - "Miautrix Mail Server - Production Deployment Guide (Debian LXC)"
Cohesion: 0.13
Nodes (14): 1.1. Install System Dependencies & NGINX, 1.2. Configure Systemd Service for .NET Backend, 1.3. Configure NGINX for Cloudflare Tunnel (`mail.miautrix.tech`), 2.1. Publish .NET 10 Self-Contained Binary, 2.2. Build Frontends (Web Admin & Webmail), 2.3. Apply Database Migrations to `miautrix-mail-pro`, 3.1. Copy Published Files using Windows Built-in `scp`, Architecture & Configuration Summary (+6 more)

### Community 70 - "ITotpService"
Cohesion: 0.29
Nodes (3): otpnet, ITotpService, TotpService

### Community 71 - ".StageAndSwitchAsync"
Cohesion: 0.19
Nodes (10): Miautrix.Mail.Infrastructure.Deployment, Miautrix.Mail.IntegrationTests.BlueGreen, CancellationToken, Func, Task, DeploymentManager, IDeploymentManager, Fact (+2 more)

### Community 72 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+10 more)

### Community 74 - "Miautrix Mail Server"
Cohesion: 0.15
Nodes (12): 1.1 Project Purpose and Business Case, 1.2 High-Level Objectives, 1. Project Initiation & Executive Summary, 2. Project Scope Management (WBS Overview), 3. Project Schedule & Milestone Management, 4. Quality Management & Verification Gates, 5. Risk Management Matrix, 6. Stakeholder & Resource Plan (+4 more)

### Community 75 - "AuthApiTests"
Cohesion: 0.36
Nodes (5): Fact, Guid, Task, WebApplicationFactory, AuthApiTests

### Community 76 - "ApiResponse"
Cohesion: 0.27
Nodes (12): ApiResponse, PaginationMeta, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 77 - ".HandleDataAsync"
Cohesion: 0.06
Nodes (40): Random, CancellationToken, Guid, Response, Task, ISmtpInboundHandler, SmtpInboundHandler, Guid (+32 more)

### Community 78 - "Miautrix Mail Server"
Cohesion: 0.17
Nodes (8): 📐 Architecture & Design, Build & Test Commands, 🛠️ Deploying & Updating, ⚙️ Development Environment, Miautrix Mail Server, 🚀 Project Status, 🔒 Security Invariants, 🛠️ Tech Stack & Prerequisites

### Community 79 - "Miautrix.Mail.Identity.csproj"
Cohesion: 0.12
Nodes (14): Konscious.Security.Cryptography.Argon2 (1.3.1), Otp.NET (1.4.1), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk (+6 more)

### Community 80 - "Iris Pay"
Cohesion: 0.17
Nodes (12): 1. Atmosphere, 3. Typography, 4. Buttons, 5. Cards, 6. Charts, 7. Spacing, 8. Depth & elevation, 9. Do's & don'ts (+4 more)

### Community 81 - "ISessionManager"
Cohesion: 0.26
Nodes (10): SessionToken, DateTimeOffset, Guid, RefreshToken, TimeSpan, AuthToken, ISessionManager, RefreshTokenInfo (+2 more)

### Community 82 - ".Update"
Cohesion: 0.31
Nodes (10): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+2 more)

### Community 83 - "SmtpQueueItem"
Cohesion: 0.15
Nodes (14): SmtpQueueItem, Attempts, LastAttemptAt, LastError, NextAttemptAt, RawMessage, Recipient, Sender (+6 more)

### Community 84 - ".Main"
Cohesion: 0.03
Nodes (73): AsyncCommand, CommandSettings, JsonSerializerOptions, CancellationToken, CommandContext, Task, BackupCommand, BackupSettings (+65 more)

### Community 85 - "Tokens"
Cohesion: 0.17
Nodes (12): Borders, Buttons, Charts, Colors, Ghost, Outline, Primary, Radius (+4 more)

### Community 86 - "IdempotencyStore.cs"
Cohesion: 0.27
Nodes (7): ConcurrentDictionary, HttpContext, Task, CapturedResponse, IIdempotencyStore, InMemoryIdempotencyStore, system_collections_concurrent

### Community 87 - "Miautrix.Mail.Application.csproj"
Cohesion: 0.13
Nodes (13): net10.0, DnsClient (1.8.0), NetArchTest.eNhancedEdition (1.4.5), Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.NET.Sdk, net10.0 (+5 more)

### Community 88 - "DomainController"
Cohesion: 0.33
Nodes (10): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+2 more)

### Community 89 - "Pro tokens"
Cohesion: 0.18
Nodes (11): Accessibility (WCAG 2.1), Button, Card, Content, Density, Elevation, Input, Motion (+3 more)

### Community 90 - "AuthController"
Cohesion: 0.28
Nodes (11): IAuthService, CancellationToken, HttpGet, HttpPost, IResult, ProducesResponseType, Task, AuthController (+3 more)

### Community 91 - ".List"
Cohesion: 0.33
Nodes (11): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, IResult, ProducesResponseType, Task (+3 more)

### Community 92 - ".SeedTenantAndMailboxAsync"
Cohesion: 0.36
Nodes (6): IClassFixture, Fact, Guid, Task, MailApiTests, WebApplicationFactory

### Community 93 - "Miautrix.Mail.IntegrationTests.csproj"
Cohesion: 0.15
Nodes (11): Microsoft.AspNetCore.Mvc.Testing (10.0.4), Microsoft.EntityFrameworkCore.InMemory (10.0.4), net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3) (+3 more)

### Community 94 - "Miautrix.Mail.Cli.csproj"
Cohesion: 0.25
Nodes (7): Microsoft.Extensions.Configuration (10.0.4), Microsoft.Extensions.Configuration.EnvironmentVariables (10.0.4), Microsoft.Extensions.DependencyInjection (10.0.4), Spectre.Console (0.55.0), Spectre.Console.Cli (0.55.0), net10.0, Microsoft.NET.Sdk

### Community 95 - "AI Build Instructions"
Cohesion: 0.20
Nodes (10): 1 · Your role, 2 · Token compliance, 3 · Component recipes, 4 · Hard constraints, 5 · Before you finish — verify, AI Build Instructions, Buttons, Cards (+2 more)

### Community 96 - "MailQueueApiTests"
Cohesion: 0.36
Nodes (5): Fact, Guid, Task, WebApplicationFactory, MailQueueApiTests

### Community 97 - "Ia"
Cohesion: 0.20
Nodes (14): bc(), Ct(), dm(), fm(), hm(), Ia(), Km(), ku() (+6 more)

### Community 98 - "Miautrix.Mail.Persistence.csproj"
Cohesion: 0.20
Nodes (8): net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, net10.0, Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 99 - "Epic 2 — Mail transport and policy"
Cohesion: 0.22
Nodes (8): Epic 2 — Mail transport and policy, T10 — IMAP, storage, and attachments, T11 — ManageSieve, T12 — Search, T13 — Rule engine, simulator, explorer, T7 — SMTP listener and queue, T8 — SPF, DKIM, DMARC, T9 — Anti-spam baseline and quarantine

### Community 100 - "SpamVerdict"
Cohesion: 0.17
Nodes (12): SpamVerdict, DkimResult, DmarcResult, DnsblListed, Greylisted, IsSpam, ReasonsJson, Recipient (+4 more)

### Community 101 - "Mailbox"
Cohesion: 0.10
Nodes (18): CancellationToken, CommandContext, Guid, Task, MailboxCreateSettings, Address, QuotaBytes, Tenant (+10 more)

### Community 102 - "AddMailAndSpamColumns"
Cohesion: 0.22
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddMailAndSpamColumns

### Community 103 - "MailContracts.cs"
Cohesion: 0.32
Nodes (11): DateTimeOffset, Guid, IReadOnlyList, AttachmentDto, FolderDto, MessageDetailDto, MessageListFilter, MessageListPage (+3 more)

### Community 104 - "AddAuditLogColumns"
Cohesion: 0.25
Nodes (6): Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddAuditLogColumns

### Community 105 - "SystemController"
Cohesion: 0.40
Nodes (8): CancellationToken, HttpGet, HttpPost, IResult, ProducesResponseType, Task, BackupActionResult, SystemController

### Community 106 - "UsersScreen.tsx"
Cohesion: 0.13
Nodes (15): Layout(), LayoutProps, MENU_ITEMS, MenuItem, ScreenId, QuarantineScreen(), QuarantineScreenProps, UsersScreen() (+7 more)

### Community 107 - "Miautrix.Mail.Web.csproj"
Cohesion: 0.22
Nodes (7): Microsoft.AspNetCore.OpenApi (10.0.4), Microsoft.OpenApi (2.7.5), Microsoft.NET.Sdk.Web, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore.Design (10.0.4)

### Community 108 - "plugins"
Cohesion: 0.22
Nodes (8): oxc, typescript, warn, plugins, rules, react/only-export-components, react/rules-of-hooks, $schema

### Community 109 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 110 - "MessageService"
Cohesion: 0.41
Nodes (5): CancellationToken, Guid, ITenantAuthorizationHelper, Task, MessageService

### Community 111 - "SystemInfoCommand.cs"
Cohesion: 0.32
Nodes (6): Command, CancellationToken, CommandContext, SystemInfoCommand, SystemInfoSettings, system_runtime_interopservices

### Community 112 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 113 - "Epic 1 — Core platform"
Cohesion: 0.25
Nodes (7): Epic 1 — Core platform, T1 — Solution scaffold and CI, T2 — Domain model and EF Core schema, T3 — Seed data and indexes, T4 — Identity, T5 — Authorization and tenant isolation, T6 — Audit trail

### Community 114 - ".Get"
Cohesion: 0.47
Nodes (7): CancellationToken, Guid, HttpGet, IResult, ProducesResponseType, Task, MailboxController

### Community 115 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Architecture rules, Commands, Data rules, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 116 - "SearchTests.cs"
Cohesion: 0.20
Nodes (7): Miautrix.Mail.Search, Miautrix.Mail.IntegrationTests.Search, HttpContext, RequestDelegate, Task, RequestIdMiddleware, system_diagnostics

### Community 117 - "Domain"
Cohesion: 0.17
Nodes (10): Miautrix.Mail.Seeder, Domain, DkimPublicKey, DkimSelector, DmarcRecord, IsPrimary, IsVerified, Name (+2 more)

### Community 118 - "AddCatalogFields"
Cohesion: 0.29
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddCatalogFields

### Community 119 - ".Error"
Cohesion: 0.25
Nodes (6): IReadOnlyDictionary, ApiError, HttpContext, IReadOnlyDictionary, IResult, ApiResults

### Community 120 - "ApiExceptionMiddleware"
Cohesion: 0.43
Nodes (5): ILogger, HttpContext, RequestDelegate, Task, ApiExceptionMiddleware

### Community 121 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Architecture rules, Commands, Data rules, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 122 - ".SeedTenantAndUserAsync"
Cohesion: 0.44
Nodes (4): Fact, Guid, Task, CliTests

### Community 124 - "PulsePoint"
Cohesion: 0.25
Nodes (8): Border Radius, Colors, Do's and Don'ts, Elevation, Overview, PulsePoint, Spacing, Typography

### Community 125 - "Miautrix.Mail.EndToEndTests.csproj"
Cohesion: 0.29
Nodes (6): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 126 - "5. Recommended Technology Stack"
Cohesion: 0.25
Nodes (8): 5. Recommended Technology Stack, Backend, Caching, Database, Frontend, Logging, Scheduling, Storage

### Community 127 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.29
Nodes (6): AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Provider seams, Task tracking, Things that will bite you, Where things are

### Community 129 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.29
Nodes (6): AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Provider seams, Task tracking, Things that will bite you, Where things are

### Community 130 - "Miautrix Mail Server — Errors, Issues & Review Log"
Cohesion: 0.29
Nodes (6): 1. System & OS Environment Issues, 2. Frontend & UI Runtime Issues, 3. NGINX & Ingress Routing Issues, 4. Backend & Database Integrity Checks, 5. Items to Review & Verify Later, Miautrix Mail Server — Errors, Issues & Review Log

### Community 131 - "Epic 3 — Surfaces"
Cohesion: 0.33
Nodes (5): Epic 3 — Surfaces, T14 — API contract and OpenAPI, T15 — Web admin GUI, T16 — Webmail, T17 — CLI

### Community 133 - "format-status.js"
Cohesion: 0.50
Nodes (3): fs, tasks, ref_fs

### Community 134 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 135 - ".BuildModel"
Cohesion: 0.33
Nodes (5): ModelSnapshot, DateTimeOffset, Guid, ModelBuilder, AppDbContextModelSnapshot

### Community 149 - "Epic 4 — Operations"
Cohesion: 0.33
Nodes (5): Epic 4 — Operations, T18 — Desktop admin application, T19 — Backup and restore, T20 — Blue/green update and the migrations ladder, T21 — Security hardening and licence gating

### Community 150 - "Rule: secrets and logging"
Cohesion: 0.33
Nodes (5): Environment, Never commit, Never log, Never store in plaintext, Rule: secrets and logging

### Community 151 - "Rule: secrets and logging"
Cohesion: 0.33
Nodes (5): Environment, Never commit, Never log, Never store in plaintext, Rule: secrets and logging

### Community 152 - "2. Palette"
Cohesion: 0.33
Nodes (6): 2. Palette, Accent (decorative only), Brand Dark, Interactive, Neutral, Primary

### Community 154 - "3. Core Architectural Principles"
Cohesion: 0.33
Nodes (6): 3.1 Security First, 3.2 Modular Design, 3.3 API First, 3.4 Configuration as Data, 3.5 Provider Abstraction, 3. Core Architectural Principles

### Community 155 - "9. Identity Architecture"
Cohesion: 0.33
Nodes (6): 9.1 Local Accounts, 9.2 Google Workspace, 9.3 Microsoft Entra ID, 9.4 LDAP / Active Directory, 9.5 Recommended Groups, 9. Identity Architecture

### Community 156 - "Section 19 — Verify commands"
Cohesion: 0.40
Nodes (5): 19.1 Where they run, 19.2 Settings allowlist, 19.3 Toolchain prerequisites, 19.6 Verify-critical configuration, Section 19 — Verify commands

### Community 157 - "Section 4 — Stack"
Cohesion: 0.40
Nodes (5): 4.1 Backend — NuGet, verified 2026-09-16, 4.2 Frontend — npm, verified 2026-09-16, 4.3 Testing — NuGet, verified, 4.4 Runtime — locally confirmed, Section 4 — Stack

### Community 158 - "Section 5 — Data model"
Cohesion: 0.40
Nodes (5): 5.1 Conventions, 5.2 Entities, 5.3 Isolation, 5.4 Migrations, Section 5 — Data model

### Community 159 - "Section 9 — Build order"
Cohesion: 0.40
Nodes (5): Epic 1 — Core platform (T1–T6), Epic 2 — Mail transport and policy (T7–T13), Epic 3 — Surfaces (T14–T17), Epic 4 — Operations (T18–T21), Section 9 — Build order

### Community 160 - "verify-task"
Cohesion: 0.40
Nodes (4): Also check, Procedure, Reporting rules, verify-task

### Community 161 - "verify-task"
Cohesion: 0.40
Nodes (4): Also check, Procedure, Reporting rules, verify-task

### Community 162 - "Section 20 — Verification status (READ FIRST)"
Cohesion: 0.50
Nodes (4): A correction to the record, Section 20 — Verification status (READ FIRST), What was NOT verified, What was verified

### Community 163 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 164 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 165 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 166 - "React + TypeScript + Vite"
Cohesion: 0.50
Nodes (3): Expanding the Oxlint configuration, React Compiler, React + TypeScript + Vite

### Community 167 - "13. Mail Flow Architecture"
Cohesion: 0.67
Nodes (3): 13. Mail Flow Architecture, Inbound, Outbound

### Community 168 - "14. Mail Flow Rule Engine"
Cohesion: 0.67
Nodes (3): 14. Mail Flow Rule Engine, Actions, Conditions

### Community 173 - "SanitizedMessageBody.tsx"
Cohesion: 0.31
Nodes (6): ref_dompurify, ref_testing_library_react, ref_vitest, SanitizedMessageBody(), SanitizedMessageBodyProps, sanitizeEmailHtml()

### Community 174 - "IMessageService"
Cohesion: 0.53
Nodes (4): CancellationToken, Guid, Task, IMessageService

### Community 175 - "Miautrix.Mail.Domain.csproj"
Cohesion: 0.20
Nodes (7): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk

### Community 176 - "Attachment"
Cohesion: 0.29
Nodes (7): Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes, StoragePath

### Community 177 - "AuthStatus"
Cohesion: 0.33
Nodes (6): AuthStatus, Failed, LockedOut, MfaRequired, Success, UserNotFound

### Community 178 - "MailFlowRule"
Cohesion: 0.33
Nodes (6): MailFlowRule, ActionsJson, ConditionsJson, IsEnabled, Name, Priority

### Community 180 - ".Up"
Cohesion: 0.40
Nodes (3): DateTimeOffset, Guid, MigrationBuilder

## Knowledge Gaps
- **809 isolated node(s):** `📐 Architecture & Design`, `🚀 Project Status`, `🛠️ Deploying & Updating`, `🛠️ Tech Stack & Prerequisites`, `Build & Test Commands` (+804 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1154 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **22 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Z()` connect `Z` to `pl`, `index-BWPawaEr.js`, `C`, `i`, `yl`?**
  _High betweenness centrality (0.017) - this node is a cross-community bridge._
- **Why does `vo()` connect `C` to `pl`, `index-BWPawaEr.js`, `r`?**
  _High betweenness centrality (0.013) - this node is a cross-community bridge._
- **Why does `sm()` connect `C` to `index-BWPawaEr.js`, `r`?**
  _High betweenness centrality (0.010) - this node is a cross-community bridge._
- **Are the 34 inferred relationships involving `sN()` (e.g. with `Q()` and `A1()`) actually correct?**
  _`sN()` has 34 INFERRED edges - model-reasoned connections that need verification._
- **What connects `📐 Architecture & Design`, `🚀 Project Status`, `🛠️ Deploying & Updating` to the rest of the system?**
  _809 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `index-DnIMIEnf.js` be split into smaller, more focused modules?**
  _Cohesion score 0.018353528153955807 - nodes in this community are weakly interconnected._
- **Should `sN` be split into smaller, more focused modules?**
  _Cohesion score 0.04257703081232493 - nodes in this community are weakly interconnected._