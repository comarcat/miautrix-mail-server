# Graph Report - miautrix-mail-server  (2026-09-20)

## Corpus Check
- 261 files · ~4,645,334 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 3777 nodes · 10254 edges · 173 communities (153 shown, 13 thin omitted)
- Extraction: 86% EXTRACTED · 14% INFERRED · 0% AMBIGUOUS · INFERRED: 1388 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `38878260`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- index-DnIMIEnf.js
- sN
- hN
- index-BWPawaEr.js
- f
- MailContracts.cs
- system
- r
- .Retry
- n
- MailApiTests.cs
- admin/src/App.tsx
- i
- AppDbContext
- r
- TenantScopedEntityBase
- o
- ImapSession
- zb
- AdminApiClient
- Z
- n
- fN
- .SimulateAsync
- devDependencies
- react
- U
- yl
- .on
- ft
- .ProcessInboundMessageAsync
- ig
- system_collections_generic
- Miautrix.Mail.Persistence
- pl
- Message
- devDependencies
- dg
- DkimService
- AdminService
- webmail/src/App.tsx
- devDependencies
- Fd
- Miautrix Mail Server
- .addEventListener
- Miautrix Mail Server — Implementation Blueprint
- af
- IAdminService
- Mailbox
- Ye
- AuthService
- SmtpDeliveryAttempt
- jo
- .Get
- InMemorySecurityEventSink
- .Main
- compilerOptions
- Miautrix.Mail.sln
- QuarantineItem
- compilerOptions
- nr
- ITenantAuthorizationHelper
- compilerOptions
- Miautrix.Mail.Domain.csproj
- kg
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
- CommandSettings
- Tokens
- InMemoryIdempotencyStore
- Miautrix.Mail.UnitTests.csproj
- DomainController
- Pro tokens
- AuthController
- MailQueueService
- .SeedTenantAndMailboxAsync
- Miautrix.Mail.IntegrationTests.csproj
- Miautrix.Mail.Application.csproj
- AI Build Instructions
- MailQueueApiTests
- AuditLog
- Miautrix.Mail.Persistence.csproj
- Epic 2 — Mail transport and policy
- SpamVerdict
- MailboxCreateSettings
- AddMailAndSpamColumns
- MailboxDeleteSettings
- AddAuditLogColumns
- SystemController
- UsersScreen.tsx
- Miautrix.Mail.Web.csproj
- plugins
- AddUserAndDomainFields
- MailboxListCommand
- SystemInfoCommand.cs
- package.json
- Epic 1 — Core platform
- QueueListCommand
- Miautrix Mail Server
- .Main
- Domain
- AddCatalogFields
- .Error
- ApiExceptionMiddleware
- Miautrix Mail Server
- IRequestContextAccessor
- User
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
- `AuthApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/AuthApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `MailApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/MailApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `MailQueueApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/MailQueueApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `MailFlowTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `SearchTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Search/SearchTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs

## Import Cycles
- None detected.

## Communities (173 total, 13 thin omitted)

### Community 0 - "index-DnIMIEnf.js"
Cohesion: 0.02
Nodes (160): a_, Aj(), Ar(), aS(), av, ay, bf(), bN() (+152 more)

### Community 1 - "sN"
Cohesion: 0.04
Nodes (80): sN(), A1(), ar(), Au(), bg(), bh(), Cl(), cx() (+72 more)

### Community 2 - "hN"
Cohesion: 0.09
Nodes (69): aE, de(), fe(), me(), ne(), aN(), ee(), Q() (+61 more)

### Community 3 - "index-BWPawaEr.js"
Cohesion: 0.05
Nodes (66): ah(), ap(), bh(), Br(), cn(), constructor(), eh(), en() (+58 more)

### Community 4 - "f"
Cohesion: 0.08
Nodes (71): _0, f(), h(), m(), p(), i(), o(), z() (+63 more)

### Community 5 - "MailContracts.cs"
Cohesion: 0.07
Nodes (48): CancellationToken, Guid, IReadOnlyList, Task, IMailboxService, CancellationToken, Guid, Task (+40 more)

### Community 6 - "system"
Cohesion: 0.11
Nodes (21): Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, Migration, npgsql_entityframeworkcore_postgresql_metadata, DateTimeOffset, Guid (+13 more)

### Community 7 - "r"
Cohesion: 0.05
Nodes (68): _2(), A0(), a2(), B0(), b2(), bj(), f(), m() (+60 more)

### Community 8 - ".Retry"
Cohesion: 0.06
Nodes (36): CancellationToken, Guid, Task, IMailQueueService, IReadOnlyList, QueueFilter, QueuePage, QueueStatusFilter (+28 more)

### Community 9 - "n"
Cohesion: 0.07
Nodes (58): ax(), bb(), bm(), C(), C1(), cg(), cm(), Ct() (+50 more)

### Community 10 - "MailApiTests.cs"
Cohesion: 0.18
Nodes (13): Miautrix.Mail.Web, Miautrix.Mail.Identity, Miautrix.Mail.SecurityTests.Isolation, Miautrix.Mail.IntegrationTests.Api, Miautrix.Mail.IntegrationTests.Cli, Miautrix.Mail.Seeder, Miautrix.Mail.SecurityTests.Identity, microsoft_aspnetcore_mvc_testing (+5 more)

### Community 11 - "admin/src/App.tsx"
Cohesion: 0.08
Nodes (37): apiClient, AntiMalwareScreen(), AntiSpamScreen(), BackupScreen(), BackupScreenProps, DashboardScreen(), DashboardStats, DomainsScreen() (+29 more)

### Community 12 - "i"
Cohesion: 0.08
Nodes (58): Ai(), am(), Au(), Bc(), bm(), bo(), C(), ca() (+50 more)

### Community 13 - "AppDbContext"
Cohesion: 0.04
Nodes (53): DbContext, DbSet, IDesignTimeDbContextFactory, ModelBuilder, AppDbContext, Aliases, ApiKeys, ApplicationPasswords (+45 more)

### Community 14 - "r"
Cohesion: 0.12
Nodes (48): b(), ce(), ch(), co(), fh(), fp(), Gp(), hn() (+40 more)

### Community 15 - "TenantScopedEntityBase"
Cohesion: 0.07
Nodes (57): Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, Deploy, DkimKey (+49 more)

### Community 16 - "o"
Cohesion: 0.07
Nodes (46): o(), _1(), ab(), ao(), bc(), Cb(), Do(), dr() (+38 more)

### Community 17 - "ImapSession"
Cohesion: 0.09
Nodes (28): CancellationToken, GeneratedRegex, Guid, IReadOnlyList, Regex, Stream, Task, ImapCommandResult (+20 more)

### Community 18 - "zb"
Cohesion: 0.12
Nodes (41): Ae(), as(), Cc(), cd(), cs(), db(), Dt(), Ee() (+33 more)

### Community 19 - "AdminApiClient"
Cohesion: 0.13
Nodes (4): AdminApiClient, AdminUserItem, ApiResponse, DomainItem

### Community 20 - "Z"
Cohesion: 0.09
Nodes (38): bi(), bu(), cl(), Cp(), Cu(), eu(), Fu(), gm() (+30 more)

### Community 21 - "n"
Cohesion: 0.11
Nodes (37): ac(), bs(), cs(), ds(), es(), fs(), In(), gs() (+29 more)

### Community 22 - "fN"
Cohesion: 0.11
Nodes (3): fN, hT(), mT()

### Community 23 - ".SimulateAsync"
Cohesion: 0.07
Nodes (38): Miautrix.Mail.MailFlow, IDisposable, IServiceCollection, IServiceProvider, ITypeRegistrar, ITypeResolver, Func, TypeRegistrar (+30 more)

### Community 24 - "devDependencies"
Cohesion: 0.05
Nodes (42): dompurify, oxlint, @types/dompurify, @types/node, dependencies, react, react-dom, devDependencies (+34 more)

### Community 25 - "react"
Cohesion: 0.10
Nodes (19): App(), initialEdges, initialNodes, RuleDesignerScreen(), RuleDesignerScreenProps, admin_src_styles_admin, DesktopApp(), ref_dompurify (+11 more)

### Community 26 - "U"
Cohesion: 0.11
Nodes (36): U(), B(), ah(), Al(), b1(), bx(), ch(), Cu() (+28 more)

### Community 27 - "yl"
Cohesion: 0.09
Nodes (36): Ao(), as(), Bd(), bl(), d(), ef(), ep(), fa() (+28 more)

### Community 28 - ".on"
Cohesion: 0.08
Nodes (32): bw(), copy(), Cr(), d2(), e2(), E(), f2(), g2() (+24 more)

### Community 29 - "ft"
Cohesion: 0.15
Nodes (29): ae(), ct(), dp(), ee(), fe(), fm(), ft(), He() (+21 more)

### Community 30 - ".ProcessInboundMessageAsync"
Cohesion: 0.12
Nodes (23): CancellationToken, Guid, List, QuarantineItem, Task, IQuarantineService, QuarantineService, Guid (+15 more)

### Community 31 - "ig"
Cohesion: 0.24
Nodes (27): Dc(), em(), Ce(), D(), g(), ge(), I(), j() (+19 more)

### Community 32 - "system_collections_generic"
Cohesion: 0.18
Nodes (16): Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.Application.Queue, Miautrix.Mail.Application.Auth, Miautrix.Mail.Application.Admin, Miautrix.Mail.Web.Contracts, Miautrix.Mail.Application.Mail, microsoft_aspnetcore_http (+8 more)

### Community 33 - "Miautrix.Mail.Persistence"
Cohesion: 0.09
Nodes (31): Miautrix.Mail.Protocols.Imap, Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.IntegrationTests.Licensing, Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Cli.Commands, Miautrix.Mail.Infrastructure.Backup, Miautrix.Mail.AntiSpam, Miautrix.Mail.Storage (+23 more)

### Community 34 - "pl"
Cohesion: 0.13
Nodes (28): dl(), ea(), Gc(), go(), hh(), t(), ho(), ic() (+20 more)

### Community 35 - "Message"
Cohesion: 0.10
Nodes (27): Message, BodyHtml, BodyText, ContentHash, Date, Flags, FolderId, IsRead (+19 more)

### Community 36 - "devDependencies"
Cohesion: 0.06
Nodes (33): dependencies, react, react-dom, @xyflow/react, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react (+25 more)

### Community 37 - "dg"
Cohesion: 0.13
Nodes (27): At(), bo(), dg(), Fi(), ga(), Gc(), Ic(), jc() (+19 more)

### Community 38 - "DkimService"
Cohesion: 0.13
Nodes (13): Body, Headers, RSA, Dictionary, GeneratedRegex, List, Regex, DkimKeyPair (+5 more)

### Community 39 - "AdminService"
Cohesion: 0.17
Nodes (11): LookupClient, CreateDomainRequest, DomainDto, CancellationToken, DateTimeOffset, Guid, IReadOnlyList, Task (+3 more)

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

### Community 44 - ".addEventListener"
Cohesion: 0.13
Nodes (18): al(), an(), Bp(), cm(), em(), _f(), gf(), jl() (+10 more)

### Community 45 - "Miautrix Mail Server — Implementation Blueprint"
Cohesion: 0.10
Nodes (21): 2.1 In scope for v1, 2.2 Out of scope for v1, 2.3 Non-goals, Miautrix Mail Server — Implementation Blueprint, Provider seams, Section 0 — How to use this blueprint, Section 10 — Workspace, Section 11 — Testing (+13 more)

### Community 46 - "af"
Cohesion: 0.14
Nodes (21): af, aw(), Ba(), cw(), d_(), $f(), Hr(), If() (+13 more)

### Community 47 - "IAdminService"
Cohesion: 0.22
Nodes (7): AdminUserDto, MailFlowRuleDto, CancellationToken, Guid, IReadOnlyList, Task, IAdminService

### Community 48 - "Mailbox"
Cohesion: 0.15
Nodes (18): Miautrix.Mail.Licensing, InvalidOperationException, Mailbox, Address, DomainId, IsActive, QuotaBytes, UsedBytes (+10 more)

### Community 49 - "Ye"
Cohesion: 0.19
Nodes (21): ag(), am(), ea(), fn(), gg(), hi(), Hn(), Io() (+13 more)

### Community 50 - "AuthService"
Cohesion: 0.12
Nodes (23): DateTimeOffset, Guid, IReadOnlyList, AuthResult, AuthStatus, Failed, LockedOut, MfaRequired (+15 more)

### Community 51 - "SmtpDeliveryAttempt"
Cohesion: 0.05
Nodes (38): DateTimeOffset, Guid, Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes (+30 more)

### Community 52 - "jo"
Cohesion: 0.17
Nodes (21): jo(), mm(), eo(), fl(), Fo(), Ha(), io(), Ja() (+13 more)

### Community 53 - ".Get"
Cohesion: 0.35
Nodes (9): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, IResult, ProducesResponseType, Task (+1 more)

### Community 54 - "InMemorySecurityEventSink"
Cohesion: 0.16
Nodes (19): IPasswordHasher, DateTimeOffset, Guid, IReadOnlyList, List, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink (+11 more)

### Community 55 - ".Main"
Cohesion: 0.10
Nodes (22): DomainEntity, Membership, RoleId, UserId, Permission, Code, Name, RolePermission (+14 more)

### Community 56 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 57 - "Miautrix.Mail.sln"
Cohesion: 0.10
Nodes (14): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+6 more)

### Community 58 - "QuarantineItem"
Cohesion: 0.08
Nodes (29): Folder, MailboxId, Name, Role, UidNext, UidValidity, QuarantineItem, IsDelivered (+21 more)

### Community 59 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 60 - "nr"
Cohesion: 0.22
Nodes (19): ed(), eg(), gm(), Il(), Kl(), nd(), ni(), nr() (+11 more)

### Community 61 - "ITenantAuthorizationHelper"
Cohesion: 0.20
Nodes (10): Guid, IPermissionRepository, ITenantAuthorizationHelper, LastOwnerDemotionException, ResourceNotFoundException, TenantAuthorizationHelper, Fact, Guid (+2 more)

### Community 62 - "compilerOptions"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+14 more)

### Community 63 - "Miautrix.Mail.Domain.csproj"
Cohesion: 0.12
Nodes (15): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+7 more)

### Community 64 - "kg"
Cohesion: 0.21
Nodes (17): Ai(), Ca(), Ci(), fr(), $g(), hd(), Jh(), Kc() (+9 more)

### Community 65 - "Components"
Cohesion: 0.12
Nodes (16): Buttons, Cards, Checkboxes, Chips, Components, Default Item, Disabled State, Filter Chip (+8 more)

### Community 66 - "AdminContracts.cs"
Cohesion: 0.12
Nodes (20): DateTimeOffset, Dictionary, Guid, List, AuditLogDto, BackupJobDto, CreateRuleRequest, CreateUserRequest (+12 more)

### Community 67 - "Yb"
Cohesion: 0.19
Nodes (16): ds(), Fo(), gb(), hr(), ib(), ix(), kb(), Mt() (+8 more)

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
Nodes (42): Miautrix.Mail.ProtocolTests.Smtp, Miautrix.Mail.Protocols.Smtp, Random, CancellationToken, Guid, Response, Task, ISmtpInboundHandler (+34 more)

### Community 78 - "Miautrix Mail Server"
Cohesion: 0.17
Nodes (8): 📐 Architecture & Design, Build & Test Commands, 🛠️ Deploying & Updating, ⚙️ Development Environment, Miautrix Mail Server, 🚀 Project Status, 🔒 Security Invariants, 🛠️ Tech Stack & Prerequisites

### Community 79 - "Miautrix.Mail.Identity.csproj"
Cohesion: 0.11
Nodes (16): Konscious.Security.Cryptography.Argon2 (1.3.1), Otp.NET (1.4.1), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4) (+8 more)

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

### Community 84 - "CommandSettings"
Cohesion: 0.06
Nodes (36): AsyncCommand, CommandSettings, CancellationToken, CommandContext, Task, BackupCommand, BackupSettings, OutputPath (+28 more)

### Community 85 - "Tokens"
Cohesion: 0.17
Nodes (12): Borders, Buttons, Charts, Colors, Ghost, Outline, Primary, Radius (+4 more)

### Community 86 - "InMemoryIdempotencyStore"
Cohesion: 0.21
Nodes (10): ConcurrentDictionary, HashSet, HttpContext, RequestDelegate, Task, IdempotencyMiddleware, CapturedResponse, IIdempotencyStore (+2 more)

### Community 87 - "Miautrix.Mail.UnitTests.csproj"
Cohesion: 0.25
Nodes (7): NetArchTest.eNhancedEdition (1.4.5), net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 88 - "DomainController"
Cohesion: 0.33
Nodes (10): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IResult, ProducesResponseType (+2 more)

### Community 89 - "Pro tokens"
Cohesion: 0.18
Nodes (11): Accessibility (WCAG 2.1), Button, Card, Content, Density, Elevation, Input, Motion (+3 more)

### Community 90 - "AuthController"
Cohesion: 0.31
Nodes (10): CancellationToken, HttpGet, HttpPost, IResult, ProducesResponseType, Task, AuthController, ChangePasswordRequest (+2 more)

### Community 91 - "MailQueueService"
Cohesion: 0.40
Nodes (5): CancellationToken, DateTimeOffset, Guid, Task, MailQueueService

### Community 92 - ".SeedTenantAndMailboxAsync"
Cohesion: 0.32
Nodes (7): Exception, IClassFixture, Fact, Guid, Task, WebApplicationFactory, MailApiTests

### Community 93 - "Miautrix.Mail.IntegrationTests.csproj"
Cohesion: 0.11
Nodes (15): Microsoft.AspNetCore.Mvc.Testing (10.0.4), Microsoft.EntityFrameworkCore.InMemory (10.0.4), net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.NET.Sdk, net10.0, Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk (+7 more)

### Community 94 - "Miautrix.Mail.Application.csproj"
Cohesion: 0.13
Nodes (13): DnsClient (1.8.0), Microsoft.Extensions.Configuration (10.0.4), Microsoft.Extensions.Configuration.EnvironmentVariables (10.0.4), Microsoft.Extensions.DependencyInjection (10.0.4), Spectre.Console (0.55.0), Spectre.Console.Cli (0.55.0), net10.0, Microsoft.NET.Sdk (+5 more)

### Community 95 - "AI Build Instructions"
Cohesion: 0.20
Nodes (10): 1 · Your role, 2 · Token compliance, 3 · Component recipes, 4 · Hard constraints, 5 · Before you finish — verify, AI Build Instructions, Buttons, Cards (+2 more)

### Community 96 - "MailQueueApiTests"
Cohesion: 0.36
Nodes (5): Fact, Guid, Task, WebApplicationFactory, MailQueueApiTests

### Community 97 - "AuditLog"
Cohesion: 0.18
Nodes (10): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Fact (+2 more)

### Community 98 - "Miautrix.Mail.Persistence.csproj"
Cohesion: 0.15
Nodes (11): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, net10.0 (+3 more)

### Community 99 - "Epic 2 — Mail transport and policy"
Cohesion: 0.22
Nodes (8): Epic 2 — Mail transport and policy, T10 — IMAP, storage, and attachments, T11 — ManageSieve, T12 — Search, T13 — Rule engine, simulator, explorer, T7 — SMTP listener and queue, T8 — SPF, DKIM, DMARC, T9 — Anti-spam baseline and quarantine

### Community 100 - "SpamVerdict"
Cohesion: 0.17
Nodes (12): SpamVerdict, DkimResult, DmarcResult, DnsblListed, Greylisted, IsSpam, ReasonsJson, Recipient (+4 more)

### Community 101 - "MailboxCreateSettings"
Cohesion: 0.20
Nodes (10): CancellationToken, CommandContext, Guid, Task, MailboxCreateCommand, MailboxCreateSettings, Address, QuotaBytes (+2 more)

### Community 102 - "AddMailAndSpamColumns"
Cohesion: 0.22
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddMailAndSpamColumns

### Community 103 - "MailboxDeleteSettings"
Cohesion: 0.22
Nodes (9): CancellationToken, CommandContext, Guid, Task, MailboxDeleteCommand, MailboxDeleteSettings, Address, Tenant (+1 more)

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

### Community 109 - "AddUserAndDomainFields"
Cohesion: 0.25
Nodes (6): Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddUserAndDomainFields

### Community 110 - "MailboxListCommand"
Cohesion: 0.25
Nodes (8): CancellationToken, CommandContext, Guid, Task, MailboxListCommand, MailboxListSettings, Tenant, UserId

### Community 111 - "SystemInfoCommand.cs"
Cohesion: 0.32
Nodes (6): Command, CancellationToken, CommandContext, SystemInfoCommand, SystemInfoSettings, system_runtime_interopservices

### Community 112 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 113 - "Epic 1 — Core platform"
Cohesion: 0.25
Nodes (7): Epic 1 — Core platform, T1 — Solution scaffold and CI, T2 — Domain model and EF Core schema, T3 — Seed data and indexes, T4 — Identity, T5 — Authorization and tenant isolation, T6 — Audit trail

### Community 114 - "QueueListCommand"
Cohesion: 0.29
Nodes (7): CancellationToken, CommandContext, Task, QueueListCommand, QueueListSettings, Status, Tenant

### Community 115 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Architecture rules, Commands, Data rules, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 116 - ".Main"
Cohesion: 0.09
Nodes (20): ApiBehaviorOptions, CancellationToken, CommandContext, Task, RestoreCommand, RestoreSettings, ArchivePath, Task (+12 more)

### Community 117 - "Domain"
Cohesion: 0.25
Nodes (8): Domain, DkimPublicKey, DkimSelector, DmarcRecord, IsPrimary, IsVerified, Name, SpfRecord

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

### Community 122 - "IRequestContextAccessor"
Cohesion: 0.12
Nodes (16): ControllerBase, IHttpContextAccessor, AuditFilter, CancellationToken, HttpGet, IResult, ProducesResponseType, Task (+8 more)

### Community 123 - "User"
Cohesion: 0.25
Nodes (6): Miautrix.Mail.Application.Users, IUserService, User, Email, IsActive, Name

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

### Community 136 - "ApiJson.cs"
Cohesion: 0.50
Nodes (3): JsonSerializerOptions, ApiJson, system_text_json_serialization

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

## Knowledge Gaps
- **808 isolated node(s):** `name`, `version`, `type`, `dev`, `build` (+803 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1124 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Z()` connect `Z` to `pl`, `i`?**
  _High betweenness centrality (0.016) - this node is a cross-community bridge._
- **Why does `vo()` connect `i` to `index-BWPawaEr.js`, `r`?**
  _High betweenness centrality (0.012) - this node is a cross-community bridge._
- **Why does `f()` connect `f` to `o`, `hN`, `.on`, `r`?**
  _High betweenness centrality (0.010) - this node is a cross-community bridge._
- **Are the 34 inferred relationships involving `sN()` (e.g. with `Q()` and `A1()`) actually correct?**
  _`sN()` has 34 INFERRED edges - model-reasoned connections that need verification._
- **What connects `name`, `version`, `type` to the rest of the system?**
  _808 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `index-DnIMIEnf.js` be split into smaller, more focused modules?**
  _Cohesion score 0.017635680964842415 - nodes in this community are weakly interconnected._
- **Should `sN` be split into smaller, more focused modules?**
  _Cohesion score 0.036899608328179755 - nodes in this community are weakly interconnected._