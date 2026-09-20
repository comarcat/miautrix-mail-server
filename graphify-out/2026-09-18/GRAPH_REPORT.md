# Graph Report - miautrix-mail-server  (2026-09-18)

## Corpus Check
- 210 files · ~4,636,666 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 3483 nodes · 9791 edges · 158 communities (133 shown, 17 thin omitted)
- Extraction: 85% EXTRACTED · 15% INFERRED · 0% AMBIGUOUS · INFERRED: 1467 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `330e5f50`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- TenantAuthorizationHelper
- Miautrix.Mail.sln
- AppDbContext
- .EvaluateAsync
- ImapSession
- .SearchAsync
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
- .HandleDataAsync
- .List
- Components
- ImapEngine.cs
- Miautrix.Mail.Domain.csproj
- MailQueueApiTests
- Miautrix.Mail.Web.csproj
- InitialCreate
- AddSmtpQueueColumns
- AddMailAndSpamColumns
- Migration
- InMemoryIdempotencyStore
- index-74ij7yKh.js
- SmtpDeliveryAttempt
- Mailbox
- Miautrix.Mail.SecurityTests.csproj
- lS
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
- .Main
- index-CTRlFaj-.js
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
- bw
- Miautrix.Mail.Seeder.csproj
- index-ZHjwwJdX.js
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
- t
- MailboxCreateSettings
- c
- MailboxDeleteSettings
- f
- Miautrix.Mail.Cli.csproj
- AsyncCommand
- MailboxListCommand
- n
- CommandSettings
- QuarantineReleaseCommand
- QueueListCommand
- QueueRetryCommand
- .Main
- MailQueueService
- QuarantineListCommand
- i
- SystemInfoCommand
- Vt
- pl
- y
- n
- React + TypeScript + Vite
- desktop/tsconfig.json
- pl
- webmail/tsconfig.json
- o
- a
- i
- C
- mx
- En
- zg
- Z
- yl
- ft
- yl
- n
- rf
- C
- Z
- cg
- ss
- rf
- hx
- Pd
- .ProcessInboundMessageAsync
- SessionManager.cs
- vn
- Message
- p0
- qf
- Miautrix Mail Server
- QuarantineItem
- SpamVerdict
- oS
- Miautrix Mail Server — Errors, Issues & Review Log
- LoginStatus
- .ExecuteAsync

## God Nodes (most connected - your core abstractions)
1. `lS()` - 412 edges
2. `AppDbContext` - 124 edges
3. `o()` - 98 edges
4. `i()` - 77 edges
5. `i()` - 77 edges
6. `n()` - 56 edges
7. `n()` - 56 edges
8. `TenantScopedEntityBase` - 54 edges
9. `c()` - 50 edges
10. `t()` - 50 edges

## Surprising Connections (you probably didn't know these)
- `MailFlowTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `SieveTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Sieve/SieveTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `MockSmtpQueueManager` --references--> `SmtpQueueItem`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Smtp/SmtpProtocolTests.cs → src/Miautrix.Mail.Domain/Entities.cs
- `SearchTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Search/SearchTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `ImapTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Imap/ImapTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Miautrix Epics Hierarchy** — blueprints_miautrix_mail_server_epics_01_core_platform_epic_1, blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2, blueprints_miautrix_mail_server_epics_03_surfaces_epic_3, blueprints_miautrix_mail_server_epics_04_operations_epic_4 [EXTRACTED 1.00]
- **Miautrix Provider Seams** — miautrix_mail_server_final_architecture_design_iidentityprovider, miautrix_mail_server_final_architecture_design_ispamprovider, miautrix_mail_server_final_architecture_design_imalwareprovider, miautrix_mail_server_final_architecture_design_imailstorage, miautrix_mail_server_final_architecture_design_isearchprovider, miautrix_mail_server_final_architecture_design_iqueueprovider [EXTRACTED 1.00]

## Communities (158 total, 17 thin omitted)

### Community 0 - "TenantAuthorizationHelper"
Cohesion: 0.18
Nodes (10): Exception, Guid, IPermissionRepository, ITenantAuthorizationHelper, LastOwnerDemotionException, ResourceNotFoundException, TenantAuthorizationHelper, Fact (+2 more)

### Community 1 - "Miautrix.Mail.sln"
Cohesion: 0.10
Nodes (14): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+6 more)

### Community 2 - "AppDbContext"
Cohesion: 0.04
Nodes (55): DbContext, DbSet, IDesignTimeDbContextFactory, ModelBuilder, AppDbContext, Aliases, ApiKeys, ApplicationPasswords (+47 more)

### Community 3 - ".EvaluateAsync"
Cohesion: 0.13
Nodes (16): Miautrix.Mail.AntiSpam, Miautrix.Mail.IntegrationTests.AntiSpam, InboundMailContext, RuleMatch, CancellationToken, GeneratedRegex, Guid, HashSet (+8 more)

### Community 4 - "ImapSession"
Cohesion: 0.09
Nodes (28): CancellationToken, GeneratedRegex, Guid, IReadOnlyList, Regex, Stream, Task, ImapCommandResult (+20 more)

### Community 5 - ".SearchAsync"
Cohesion: 0.23
Nodes (11): CancellationToken, DateTimeOffset, Guid, IReadOnlyList, Task, ISearchProvider, PostgreSqlSearchProvider, SearchMessageItem (+3 more)

### Community 6 - "DkimService"
Cohesion: 0.13
Nodes (14): Body, Headers, RSA, Dictionary, GeneratedRegex, List, Regex, DkimKeyPair (+6 more)

### Community 7 - "Epic 2: Mail Transport & Policy (T7-T13)"
Cohesion: 0.25
Nodes (8): Epic 2: Mail Transport & Policy (T7-T13), Anti-Spam & Quarantine Subsystem, DNS Authentication (SPF, DKIM, DMARC, ARC), Full-Text Search (FTS) Indexing, IMAP Protocol Listener, Mail-Flow Rule Engine & Simulator, ManageSieve Script Engine, SMTP Listener & Queue

### Community 8 - "Miautrix.Mail.Domain"
Cohesion: 0.07
Nodes (24): Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Web, Miautrix.Mail.Identity, Miautrix.Mail.SecurityTests.Isolation, Miautrix.Mail.Search (+16 more)

### Community 9 - "TenantScopedEntityBase"
Cohesion: 0.12
Nodes (40): Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, BackupJob, Deploy (+32 more)

### Community 10 - ".SimulateAsync"
Cohesion: 0.09
Nodes (30): Miautrix.Mail.Cli.Infrastructure, IDisposable, IServiceCollection, IServiceProvider, ITypeRegistrar, ITypeResolver, JsonSerializerOptions, Func (+22 more)

### Community 11 - "App.tsx"
Cohesion: 0.09
Nodes (22): AdminApiClient, apiClient, App(), PLACEHOLDER_DESCRIPTIONS, DashboardScreen(), DashboardStats, Layout(), LayoutProps (+14 more)

### Community 12 - "SieveTests"
Cohesion: 0.17
Nodes (12): Miautrix.Mail.Protocols.Sieve, Miautrix.Mail.ProtocolTests.Sieve, CancellationToken, Guid, HashSet, Task, SieveParser, SieveParseResult (+4 more)

### Community 13 - "microsoft_entityframeworkcore"
Cohesion: 0.28
Nodes (9): Miautrix.Mail.Persistence, Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore, microsoft_entityframeworkcore_design, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, npgsql_entityframeworkcore_postgresql_metadata (+1 more)

### Community 14 - "Guid"
Cohesion: 0.08
Nodes (26): Guid, Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes, StoragePath (+18 more)

### Community 15 - "Miautrix Mail Server Platform"
Cohesion: 0.22
Nodes (9): Epic 1: Core Platform (T1-T6), Secrets & Redaction Policy, Tenant Isolation Enforcement, Clean Architecture Pattern, .NET 10 LTS Core Engine, Miautrix Mail Server Platform, Modular Monolith Architecture, PostgreSQL Storage & Schema (+1 more)

### Community 16 - "webmail/src/App.tsx"
Cohesion: 0.07
Nodes (37): DesktopApp(), oxc, react, typescript, warn, plugins, rules, react/only-export-components (+29 more)

### Community 17 - ".HandleDataAsync"
Cohesion: 0.06
Nodes (44): Miautrix.Mail.ProtocolTests.Smtp, Miautrix.Mail.Protocols.Smtp, Random, CancellationToken, Guid, QueueItem, Response, Task (+36 more)

### Community 18 - ".List"
Cohesion: 0.13
Nodes (17): ControllerBase, Miautrix.Mail.Web.Contracts, HttpDelete, HttpGet, HttpPost, ProducesResponseType, ApiResponse, PaginationMeta (+9 more)

### Community 19 - "Components"
Cohesion: 0.08
Nodes (24): Border Radius, Buttons, Cards, Checkboxes, Chips, Colors, Components, Default Item (+16 more)

### Community 20 - "ImapEngine.cs"
Cohesion: 0.24
Nodes (7): Miautrix.Mail.Protocols.Imap, Miautrix.Mail.Storage, Miautrix.Mail.Seeder, Miautrix.Mail.ProtocolTests.Imap, konscious_security_cryptography, system_security_cryptography, system_text

### Community 21 - "Miautrix.Mail.Domain.csproj"
Cohesion: 0.12
Nodes (15): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+7 more)

### Community 22 - "MailQueueApiTests"
Cohesion: 0.12
Nodes (19): IClassFixture, Membership, RoleId, UserId, Permission, Code, Name, RolePermission (+11 more)

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
Cohesion: 0.21
Nodes (9): ConcurrentDictionary, HashSet, HttpContext, RequestDelegate, Task, IdempotencyMiddleware, CapturedResponse, IIdempotencyStore (+1 more)

### Community 29 - "index-74ij7yKh.js"
Cohesion: 0.02
Nodes (136): a_(), a1, AE(), aN(), Ar(), Av(), Ay, b_() (+128 more)

### Community 30 - "SmtpDeliveryAttempt"
Cohesion: 0.13
Nodes (15): DateTimeOffset, EntityBase, CreatedAt, Id, UpdatedAt, SmtpDeliveryAttempt, AttemptedAt, AttemptNumber (+7 more)

### Community 31 - "Mailbox"
Cohesion: 0.12
Nodes (16): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Mailbox (+8 more)

### Community 32 - "Miautrix.Mail.SecurityTests.csproj"
Cohesion: 0.14
Nodes (12): Konscious.Security.Cryptography.Argon2 (1.3.1), Otp.NET (1.4.1), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4) (+4 more)

### Community 33 - "lS"
Cohesion: 0.04
Nodes (86): lS(), $1(), Aa(), ah(), Ai(), au(), b0(), bb() (+78 more)

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
Cohesion: 0.12
Nodes (18): otpnet, Argon2idPasswordHasher, IPasswordHasher, DateTimeOffset, Guid, IReadOnlyList, List, AuthenticationService (+10 more)

### Community 51 - "compilerOptions"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+14 more)

### Community 52 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 53 - ".Main"
Cohesion: 0.12
Nodes (15): ApiBehaviorOptions, IHttpContextAccessor, Guid, EfPermissionRepository, Guid, HeaderRequestContextAccessor, CurrentTenantId, CurrentUserId (+7 more)

### Community 54 - "index-CTRlFaj-.js"
Cohesion: 0.04
Nodes (82): Dr(), ah(), ap(), bh(), Br(), ch(), cn(), Cp() (+74 more)

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

### Community 81 - "bw"
Cohesion: 0.08
Nodes (81): E(), bw(), $e(), ep, fa(), fp(), gi(), w() (+73 more)

### Community 82 - "Miautrix.Mail.Seeder.csproj"
Cohesion: 0.18
Nodes (7): Miautrix.Mail.Application.Users, IUserService, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, User

### Community 83 - "index-ZHjwwJdX.js"
Cohesion: 0.05
Nodes (63): ah(), ba(), bh(), Br(), cn(), Cp(), Cu(), en() (+55 more)

### Community 84 - "4. Buttons"
Cohesion: 0.50
Nodes (4): 4. Buttons, Disabled / Muted, Outline, Primary Iris

### Community 85 - "compilerOptions"
Cohesion: 0.10
Nodes (19): node, compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, noEmit (+11 more)

### Community 86 - ".RestoreBackupAsync"
Cohesion: 0.13
Nodes (20): AuditLog, DateTimeOffset, Dictionary, DomainEntity, Folder, List, QuarantineItem, SmtpQueueItem (+12 more)

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

### Community 94 - "t"
Cohesion: 0.07
Nodes (67): Ae(), ae(), ap(), b(), co(), ct(), dp(), ee() (+59 more)

### Community 95 - "MailboxCreateSettings"
Cohesion: 0.16
Nodes (12): AppDbContext, CancellationToken, CommandContext, Guid, ITenantAuthorizationHelper, Task, MailboxCreateCommand, MailboxCreateSettings (+4 more)

### Community 96 - "c"
Cohesion: 0.06
Nodes (65): _2(), a2(), aS(), l(), Au(), bd(), Bn(), cS() (+57 more)

### Community 97 - "MailboxDeleteSettings"
Cohesion: 0.18
Nodes (11): AppDbContext, CancellationToken, CommandContext, Guid, ITenantAuthorizationHelper, Task, MailboxDeleteCommand, MailboxDeleteSettings (+3 more)

### Community 98 - "f"
Cohesion: 0.06
Nodes (67): _3(), a3(), Ad(), aT(), aw(), b3(), Bu(), c1() (+59 more)

### Community 99 - "Miautrix.Mail.Cli.csproj"
Cohesion: 0.17
Nodes (10): Microsoft.Extensions.Configuration (10.0.4), Microsoft.Extensions.Configuration.EnvironmentVariables (10.0.4), Microsoft.Extensions.DependencyInjection (10.0.4), Spectre.Console (0.55.0), Spectre.Console.Cli (0.55.0), net10.0, Microsoft.NET.Sdk, net10.0 (+2 more)

### Community 100 - "AsyncCommand"
Cohesion: 0.25
Nodes (8): AsyncCommand, AppDbContext, Guid, ITenantAuthorizationHelper, DomainListCommand, DomainListSettings, Tenant, UserId

### Community 101 - "MailboxListCommand"
Cohesion: 0.20
Nodes (10): AppDbContext, CancellationToken, CommandContext, Guid, ITenantAuthorizationHelper, Task, MailboxListCommand, MailboxListSettings (+2 more)

### Community 102 - "n"
Cohesion: 0.11
Nodes (62): b(), d0(), De(), ea(), eb(), eh(), f0(), h0() (+54 more)

### Community 103 - "CommandSettings"
Cohesion: 0.25
Nodes (8): CommandSettings, AppDbContext, CancellationToken, CommandContext, Task, BackupCommand, BackupSettings, OutputPath

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
Cohesion: 0.14
Nodes (18): InvalidOperationException, CancellationToken, Guid, Task, IMailQueueService, CancellationToken, DateTimeOffset, Guid (+10 more)

### Community 109 - "QuarantineListCommand"
Cohesion: 0.28
Nodes (7): AppDbContext, CancellationToken, CommandContext, Task, QuarantineListCommand, QuarantineListSettings, Tenant

### Community 110 - "i"
Cohesion: 0.13
Nodes (53): Au(), bo(), co(), d(), dc(), dn(), fc(), gs() (+45 more)

### Community 111 - "SystemInfoCommand"
Cohesion: 0.38
Nodes (5): Command, CancellationToken, CommandContext, SystemInfoCommand, SystemInfoSettings

### Community 112 - "Vt"
Cohesion: 0.07
Nodes (51): ag(), Ce(), Cn(), cu(), Dc(), dg(), di(), El() (+43 more)

### Community 113 - "pl"
Cohesion: 0.09
Nodes (47): Te(), e(), cl(), dl(), ea(), es(), Fi(), fl() (+39 more)

### Community 114 - "y"
Cohesion: 0.09
Nodes (42): bp(), bT(), bv(), C(), N(), p(), R(), T() (+34 more)

### Community 115 - "n"
Cohesion: 0.09
Nodes (35): al(), an(), b(), Bp(), bt(), cm(), em(), es() (+27 more)

### Community 116 - "React + TypeScript + Vite"
Cohesion: 0.50
Nodes (3): Expanding the Oxlint configuration, React Compiler, React + TypeScript + Vite

### Community 118 - "pl"
Cohesion: 0.10
Nodes (42): ba(), cl(), ea(), fa(), Fi(), fl(), Fo(), Gc() (+34 more)

### Community 125 - "o"
Cohesion: 0.09
Nodes (41): o(), an(), bl(), Cf(), Ct(), ds(), e0(), Ec() (+33 more)

### Community 126 - "a"
Cohesion: 0.10
Nodes (40): al(), bi(), bu(), ce(), ch(), fh(), hu(), iu() (+32 more)

### Community 127 - "i"
Cohesion: 0.12
Nodes (34): am(), an(), Au(), bm(), Bp(), cm(), dc(), dn() (+26 more)

### Community 128 - "C"
Cohesion: 0.10
Nodes (36): am(), Bc(), bm(), C(), da(), dm(), el(), Hc() (+28 more)

### Community 129 - "mx"
Cohesion: 0.11
Nodes (35): af(), am(), Ax(), Ca(), cc(), cm(), Dt(), f() (+27 more)

### Community 130 - "En"
Cohesion: 0.09
Nodes (34): ao(), ap(), bi(), Cv(), Cy(), dy, En(), f2() (+26 more)

### Community 131 - "zg"
Cohesion: 0.09
Nodes (34): $a(), bg(), bx(), Ci(), ei(), em(), fc(), fr() (+26 more)

### Community 132 - "Z"
Cohesion: 0.10
Nodes (34): Ai(), cc(), dh(), Di(), Ei(), eu(), _f(), fc() (+26 more)

### Community 133 - "yl"
Cohesion: 0.10
Nodes (34): Ao(), as(), Bd(), bl(), d(), ef(), ep(), fa() (+26 more)

### Community 134 - "ft"
Cohesion: 0.13
Nodes (33): ae(), ce(), ct(), dp(), ee(), fe(), fm(), ft() (+25 more)

### Community 135 - "yl"
Cohesion: 0.10
Nodes (32): Ao(), as(), Bd(), bl(), ca(), ef(), ep(), Gd() (+24 more)

### Community 136 - "n"
Cohesion: 0.13
Nodes (27): ac(), bs(), cs(), ds(), fs(), gs(), hs(), j() (+19 more)

### Community 137 - "rf"
Cohesion: 0.11
Nodes (27): af(), bt(), cf(), df(), Fd(), ff(), ht(), Ia() (+19 more)

### Community 138 - "C"
Cohesion: 0.14
Nodes (27): Bc(), bo(), C(), ca(), da(), dm(), el(), Hc() (+19 more)

### Community 139 - "Z"
Cohesion: 0.14
Nodes (26): bi(), bu(), eu(), Fu(), gu(), hu(), iu(), kp() (+18 more)

### Community 140 - "cg"
Cohesion: 0.17
Nodes (24): as(), cg(), cs(), eg(), he(), ia(), Ii(), j0() (+16 more)

### Community 141 - "ss"
Cohesion: 0.15
Nodes (22): ac(), bs(), cs(), ds(), fs(), j(), ks(), ls() (+14 more)

### Community 142 - "rf"
Cohesion: 0.14
Nodes (22): af(), cf(), df(), Fd(), ff(), ht(), Ia(), jf() (+14 more)

### Community 143 - "hx"
Cohesion: 0.18
Nodes (19): bu(), cl(), df(), ff(), fm(), hx(), Kf(), Kg() (+11 more)

### Community 144 - "Pd"
Cohesion: 0.16
Nodes (18): ld(), Ai(), cc(), dh(), Di(), Ei(), jd(), Kd() (+10 more)

### Community 145 - ".ProcessInboundMessageAsync"
Cohesion: 0.30
Nodes (9): CancellationToken, Guid, List, Task, IQuarantineService, QuarantineService, Guid, IReadOnlyList (+1 more)

### Community 146 - "SessionManager.cs"
Cohesion: 0.26
Nodes (10): SessionToken, DateTimeOffset, Guid, RefreshToken, TimeSpan, AuthToken, ISessionManager, RefreshTokenInfo (+2 more)

### Community 147 - "vn"
Cohesion: 0.21
Nodes (16): Be(), brighter(), cE(), darker(), displayable(), gy(), Lv(), ma() (+8 more)

### Community 148 - "Message"
Cohesion: 0.12
Nodes (16): Message, BodyHtml, BodyText, ContentHash, Date, Flags, FolderId, IsRead (+8 more)

### Community 149 - "p0"
Cohesion: 0.17
Nodes (15): _0(), a0(), Bc(), bs(), gs(), Hc(), i0(), Lc() (+7 more)

### Community 150 - "qf"
Cohesion: 0.23
Nodes (15): cb(), Dm(), Fe(), jb(), jm(), mr(), mu(), Oi() (+7 more)

### Community 151 - "Miautrix Mail Server"
Cohesion: 0.15
Nodes (12): 1.1 Project Purpose and Business Case, 1.2 High-Level Objectives, 1. Project Initiation & Executive Summary, 2. Project Scope Management (WBS Overview), 3. Project Schedule & Milestone Management, 4. Quality Management & Verification Gates, 5. Risk Management Matrix, 6. Stakeholder & Resource Plan (+4 more)

### Community 152 - "QuarantineItem"
Cohesion: 0.15
Nodes (13): QuarantineItem, IsDelivered, QuarantinedAt, RawMessage, ReasonsJson, Recipient, ReleasedAt, Sender (+5 more)

### Community 153 - "SpamVerdict"
Cohesion: 0.17
Nodes (12): SpamVerdict, DkimResult, DmarcResult, DnsblListed, Greylisted, IsSpam, ReasonsJson, Recipient (+4 more)

### Community 154 - "oS"
Cohesion: 0.25
Nodes (4): Mv(), oS, pw(), yS()

### Community 155 - "Miautrix Mail Server — Errors, Issues & Review Log"
Cohesion: 0.29
Nodes (6): 1. System & OS Environment Issues, 2. Frontend & UI Runtime Issues, 3. NGINX & Ingress Routing Issues, 4. Backend & Database Integrity Checks, 5. Items to Review & Verify Later, Miautrix Mail Server — Errors, Issues & Review Log

### Community 156 - "LoginStatus"
Cohesion: 0.40
Nodes (5): LoginStatus, Failed, LockedOut, MfaRequired, Success

### Community 157 - ".ExecuteAsync"
Cohesion: 0.50
Nodes (3): CancellationToken, CommandContext, Task

## Knowledge Gaps
- **660 isolated node(s):** `1. System & OS Environment Issues`, `2. Frontend & UI Runtime Issues`, `3. NGINX & Ingress Routing Issues`, `4. Backend & Database Integrity Checks`, `5. Items to Review & Verify Later` (+655 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 949 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **17 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `lS()` connect `lS` to `c`, `mx`, `zg`, `n`, `cg`, `hx`, `Vt`, `bw`, `y`, `index-74ij7yKh.js`, `pl`, `p0`, `qf`, `o`, `t`?**
  _High betweenness centrality (0.115) - this node is a cross-community bridge._
- **Why does `AppDbContext` connect `AppDbContext` to `.EvaluateAsync`, `ImapSession`, `.SearchAsync`, `TenantScopedEntityBase`, `.SimulateAsync`, `SieveTests`, `microsoft_entityframeworkcore`, `Guid`, `.ProcessInboundMessageAsync`, `.HandleDataAsync`, `Message`, `MailQueueApiTests`, `QuarantineItem`, `SpamVerdict`, `SmtpDeliveryAttempt`, `Mailbox`, `MailFlowRule`, `MalwareVerdict`, `DkimKey`, `.Main`, `SmtpQueueItem`, `MailQueueService`?**
  _High betweenness centrality (0.046) - this node is a cross-community bridge._
- **Why does `ft()` connect `ft` to `lS`, `i`, `rf`, `Vt`, `bw`, `vn`, `n`, `qf`, `index-CTRlFaj-.js`, `pl`?**
  _High betweenness centrality (0.024) - this node is a cross-community bridge._
- **Are the 27 inferred relationships involving `lS()` (e.g. with `_0()` and `a0()`) actually correct?**
  _`lS()` has 27 INFERRED edges - model-reasoned connections that need verification._
- **Are the 19 inferred relationships involving `o()` (e.g. with `bp()` and `p()`) actually correct?**
  _`o()` has 19 INFERRED edges - model-reasoned connections that need verification._
- **Are the 14 inferred relationships involving `i()` (e.g. with `index-CTRlFaj-.js` and `b()`) actually correct?**
  _`i()` has 14 INFERRED edges - model-reasoned connections that need verification._
- **Are the 14 inferred relationships involving `i()` (e.g. with `index-ZHjwwJdX.js` and `b()`) actually correct?**
  _`i()` has 14 INFERRED edges - model-reasoned connections that need verification._