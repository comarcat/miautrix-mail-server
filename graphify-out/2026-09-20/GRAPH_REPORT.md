# Graph Report - miautrix-mail-server  (2026-09-19)

## Corpus Check
- Large corpus: 784 files · ~4,643,303 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder.

## Summary
- 3790 nodes · 11405 edges · 149 communities (134 shown, 10 thin omitted)
- Extraction: 86% EXTRACTED · 14% INFERRED · 0% AMBIGUOUS · INFERRED: 1635 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- index-74ij7yKh.js
- lS
- index-CTRlFaj-.js
- index-ZHjwwJdX.js
- bw
- MailContracts.cs
- Miautrix.Mail.Persistence.Migrations
- i
- MailQueueService
- Vt
- Miautrix.Mail.Domain
- AdminApiClient
- i
- AppDbContext
- n
- TenantScopedEntityBase
- Ut
- ImapSession
- yl
- r
- pl
- c
- n
- .SimulateAsync
- devDependencies
- App.tsx
- En
- u
- b
- i
- .ProcessInboundMessageAsync
- mx
- system
- Miautrix.Mail.Persistence
- C
- Message
- devDependencies
- n
- DkimService
- .AssertPermission
- App.tsx
- devDependencies
- cg
- ft
- yl
- o
- Z
- IAdminService
- Mailbox
- Pd
- .AuthenticateAsync
- QuarantineItem
- b
- IRequestContextAccessor
- InMemorySecurityEventSink
- User
- compilerOptions
- Miautrix.Mail.sln
- Folder
- compilerOptions
- B
- ITenantAuthorizationHelper
- compilerOptions
- Miautrix.Mail.Domain.csproj
- qd
- ss
- AdminContracts.cs
- SieveTests
- compilerOptions
- Guid
- .Main
- .StageAndSwitchAsync
- compilerOptions
- vn
- Pd
- AuthApiTests
- ApiResponse
- MockSmtpQueueManager
- Wb
- Miautrix.Mail.Identity.csproj
- bx
- ISessionManager
- .Update
- SmtpQueueItem
- CommandSettings
- k
- InMemoryIdempotencyStore
- Miautrix.Mail.UnitTests.csproj
- DomainController
- .When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry
- AuthController
- SanitizedMessageBody.tsx
- .SeedTenantAndMailboxAsync
- Miautrix.Mail.IntegrationTests.csproj
- Miautrix.Mail.Cli.csproj
- .SeedTenantAndUserAsync
- MailQueueApiTests
- AuditLog
- Miautrix.Mail.Persistence.csproj
- .HandleDataAsync
- SpamVerdict
- MailboxCreateSettings
- AddMailAndSpamColumns
- MailboxDeleteSettings
- .HandleSubmissionAsync
- SystemController
- UsersScreen.tsx
- Miautrix.Mail.Web.csproj
- plugins
- DomainListCommand
- MailboxListCommand
- SystemInfoCommand.cs
- package.json
- QuarantineReleaseCommand
- QueueListCommand
- QueueRetryCommand
- .Main
- Domain
- .BuildTargetModel
- .Error
- ApiExceptionMiddleware
- ExponentialBackoffWithJitterRetryPolicy
- .List
- RestoreCommand
- .IsDomainLocal
- Miautrix.Mail.EndToEndTests.csproj
- AuthStatus
- MailFlowRule
- ref_vitejs_plugin_react
- cs
- LoginStatus
- BackupScreen.tsx
- ref_testing_library_jest_dom_vitest
- format-status.js
- .BuildTargetModel
- .BuildModel
- ApiJson.cs
- versions.mjs
- tsconfig.json
- generate_migration.sh
- init-database-scratch.sh
- update-database.sh
- tsconfig.json
- fix_tasks.py

## God Nodes (most connected - your core abstractions)
1. `lS()` - 412 edges
2. `AppDbContext` - 149 edges
3. `o()` - 98 edges
4. `i()` - 77 edges
5. `i()` - 77 edges
6. `n()` - 56 edges
7. `n()` - 56 edges
8. `TenantScopedEntityBase` - 54 edges
9. `c()` - 50 edges
10. `t()` - 50 edges

## Surprising Connections (you probably didn't know these)
- `AuthApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/AuthApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `MailApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/MailApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `MailQueueApiTests` --references--> `Program`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Api/MailQueueApiTests.cs → src/Miautrix.Mail.Web/Program.cs
- `BlueGreenUpdateTests` --references--> `DeploymentManager`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/BlueGreen/BlueGreenUpdateTests.cs → src/Miautrix.Mail.Infrastructure/Deployment/DeploymentManager.cs
- `MailFlowTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `MailFlowTests` --references--> `MailFlowEngine`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/MailFlow/MailFlowTests.cs → src/Miautrix.Mail.MailFlow/MailFlowEngine.cs
- `SearchTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Search/SearchTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `SearchTests` --references--> `PostgreSqlSearchProvider`  [EXTRACTED]
  tests/Miautrix.Mail.IntegrationTests/Search/SearchTests.cs → src/Miautrix.Mail.Search/SearchProvider.cs
- `ImapTests` --references--> `AppDbContext`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Imap/ImapTests.cs → src/Miautrix.Mail.Persistence/AppDbContext.cs
- `ImapTests` --references--> `FileSystemMailStorage`  [EXTRACTED]
  tests/Miautrix.Mail.ProtocolTests/Imap/ImapTests.cs → src/Miautrix.Mail.Storage/MailStorage.cs

## Import Cycles
- None detected.

## Communities (149 total, 10 thin omitted)

### Community 0 - "index-74ij7yKh.js"
Cohesion: 0.02
Nodes (138): a_(), a1, AE(), aN(), Av(), Ay, b_(), bi() (+130 more)

### Community 1 - "lS"
Cohesion: 0.03
Nodes (99): lS(), _0(), $1(), a0(), Aa(), Ab(), ah(), as() (+91 more)

### Community 2 - "index-CTRlFaj-.js"
Cohesion: 0.04
Nodes (98): Dr(), af(), ah(), ap(), bh(), Br(), cf(), ch() (+90 more)

### Community 3 - "index-ZHjwwJdX.js"
Cohesion: 0.04
Nodes (94): af(), ah(), ap(), bh(), Br(), bt(), cf(), ch() (+86 more)

### Community 4 - "bw"
Cohesion: 0.12
Nodes (83): Ad(), bp(), E(), p(), bw(), C(), c(), cN() (+75 more)

### Community 5 - "MailContracts.cs"
Cohesion: 0.07
Nodes (48): CancellationToken, Guid, IReadOnlyList, Task, IMailboxService, CancellationToken, Guid, Task (+40 more)

### Community 6 - "Miautrix.Mail.Persistence.Migrations"
Cohesion: 0.05
Nodes (36): Miautrix.Mail.Persistence.Migrations, microsoft_entityframeworkcore_infrastructure, microsoft_entityframeworkcore_migrations, microsoft_entityframeworkcore_storage_valueconversion, Migration, ModelSnapshot, npgsql_entityframeworkcore_postgresql_metadata, DateTimeOffset (+28 more)

### Community 7 - "i"
Cohesion: 0.12
Nodes (59): bo(), ce(), co(), d(), eh(), eo(), es(), fh() (+51 more)

### Community 8 - "MailQueueService"
Cohesion: 0.06
Nodes (41): CancellationToken, Guid, Task, IMailQueueService, CancellationToken, DateTimeOffset, Guid, Task (+33 more)

### Community 9 - "Vt"
Cohesion: 0.06
Nodes (59): Ae(), ag(), Ai(), bu(), C(), cl(), Cn(), cu() (+51 more)

### Community 10 - "Miautrix.Mail.Domain"
Cohesion: 0.07
Nodes (32): Miautrix.Mail.Protocols.Imap, Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.IntegrationTests.Licensing, Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Web, Miautrix.Mail.AntiSpam, Miautrix.Mail.Storage, Miautrix.Mail.IntegrationTests.AntiSpam (+24 more)

### Community 11 - "AdminApiClient"
Cohesion: 0.09
Nodes (25): AdminApiClient, apiClient, DomainsScreenProps, LicensingScreen(), LicensingScreenProps, LogsScreenProps, MailFlowScreenProps, QuarantineScreenProps (+17 more)

### Community 12 - "i"
Cohesion: 0.05
Nodes (52): _2(), a2(), aS(), l(), bd(), Bn(), cw(), s() (+44 more)

### Community 13 - "AppDbContext"
Cohesion: 0.04
Nodes (52): DbContext, DbSet, IDesignTimeDbContextFactory, AppDbContext, Aliases, ApiKeys, ApplicationPasswords, Attachments (+44 more)

### Community 14 - "n"
Cohesion: 0.06
Nodes (52): Bc(), bs(), c0(), Ca(), cc(), Ce(), d0(), e0() (+44 more)

### Community 15 - "TenantScopedEntityBase"
Cohesion: 0.08
Nodes (49): Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, BackupJob, Deploy (+41 more)

### Community 16 - "Ut"
Cohesion: 0.07
Nodes (50): a3(), aT(), aw(), b3(), Bu(), c1(), c3(), co (+42 more)

### Community 17 - "ImapSession"
Cohesion: 0.09
Nodes (28): CancellationToken, GeneratedRegex, Guid, IReadOnlyList, Regex, Stream, Task, ImapCommandResult (+20 more)

### Community 18 - "yl"
Cohesion: 0.07
Nodes (48): Ao(), bi(), bl(), bu(), el(), ep(), eu(), fa() (+40 more)

### Community 19 - "r"
Cohesion: 0.15
Nodes (46): C(), ce(), co(), fh(), hn(), ip(), jm(), ju() (+38 more)

### Community 20 - "pl"
Cohesion: 0.09
Nodes (47): ba(), ca(), cl(), da(), dm(), ea(), fa(), fl() (+39 more)

### Community 21 - "c"
Cohesion: 0.09
Nodes (43): bv(), C(), R(), T(), w(), cS(), $e(), c() (+35 more)

### Community 22 - "n"
Cohesion: 0.09
Nodes (45): ac(), bs(), cs(), dl(), ds(), fs(), gs(), h() (+37 more)

### Community 23 - ".SimulateAsync"
Cohesion: 0.10
Nodes (28): Miautrix.Mail.MailFlow, IDisposable, IServiceCollection, IServiceProvider, ITypeRegistrar, ITypeResolver, Func, TypeRegistrar (+20 more)

### Community 24 - "devDependencies"
Cohesion: 0.05
Nodes (42): dompurify, oxlint, @types/dompurify, @types/node, dependencies, react, react-dom, devDependencies (+34 more)

### Community 25 - "App.tsx"
Cohesion: 0.07
Nodes (27): App(), AntiMalwareScreen(), AntiSpamScreen(), DashboardScreen(), DashboardStats, DomainsScreen(), IdentityScreen(), Layout() (+19 more)

### Community 26 - "En"
Cohesion: 0.07
Nodes (42): _3(), ao(), ap(), Au(), Cv(), dN, dy, En() (+34 more)

### Community 27 - "u"
Cohesion: 0.18
Nodes (41): bb(), De(), df(), ea(), eb(), eg(), _h(), hf() (+33 more)

### Community 28 - "b"
Cohesion: 0.08
Nodes (34): al(), am(), an(), Au(), b(), bm(), Bp(), cm() (+26 more)

### Community 29 - "i"
Cohesion: 0.11
Nodes (41): ba(), Bc(), bo(), ca(), da(), dm(), ea(), es() (+33 more)

### Community 30 - ".ProcessInboundMessageAsync"
Cohesion: 0.12
Nodes (24): CancellationToken, Guid, List, QuarantineItem, Task, IQuarantineService, QuarantineService, Guid (+16 more)

### Community 31 - "mx"
Cohesion: 0.10
Nodes (38): $a(), af(), am(), Ax(), bg(), cm(), Dt(), ei() (+30 more)

### Community 32 - "system"
Cohesion: 0.23
Nodes (15): Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.Application.Queue, Miautrix.Mail.Application.Auth, Miautrix.Mail.Application.Admin, Miautrix.Mail.Web.Contracts, Miautrix.Mail.Application.Mail, microsoft_aspnetcore_http (+7 more)

### Community 33 - "Miautrix.Mail.Persistence"
Cohesion: 0.16
Nodes (16): Miautrix.Mail.Cli.Commands, Miautrix.Mail.Infrastructure.Backup, Miautrix.Mail.Search, Miautrix.Mail.IntegrationTests.Search, Miautrix.Mail.Cli, Miautrix.Mail.Persistence, Miautrix.Mail.Security, Miautrix.Mail.Cli.Infrastructure (+8 more)

### Community 34 - "C"
Cohesion: 0.11
Nodes (35): am(), Au(), Bc(), bm(), C(), dn(), Du(), el() (+27 more)

### Community 35 - "Message"
Cohesion: 0.10
Nodes (27): Message, BodyHtml, BodyText, ContentHash, Date, Flags, FolderId, IsRead (+19 more)

### Community 36 - "devDependencies"
Cohesion: 0.06
Nodes (33): dependencies, react, react-dom, @xyflow/react, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react (+25 more)

### Community 37 - "n"
Cohesion: 0.12
Nodes (34): ae(), ct(), dp(), ee(), fe(), fm(), ft(), In() (+26 more)

### Community 38 - "DkimService"
Cohesion: 0.13
Nodes (13): Body, Headers, RSA, Dictionary, GeneratedRegex, List, Regex, DkimKeyPair (+5 more)

### Community 39 - ".AssertPermission"
Cohesion: 0.27
Nodes (6): CancellationToken, Guid, IReadOnlyList, Task, VerifyDomainResult, AdminService

### Community 40 - "App.tsx"
Cohesion: 0.11
Nodes (25): AppProps, INITIAL_CONTACTS, INITIAL_EVENTS, INITIAL_MAILBOXES, INITIAL_MESSAGES, INITIAL_RULES, CalendarView(), CalendarViewProps (+17 more)

### Community 41 - "devDependencies"
Cohesion: 0.06
Nodes (31): dependencies, react, react-dom, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @types/react (+23 more)

### Community 42 - "cg"
Cohesion: 0.12
Nodes (31): cg(), Ct(), Dc(), ds(), Ec(), Ee(), Fi(), gl() (+23 more)

### Community 43 - "ft"
Cohesion: 0.13
Nodes (31): ae(), ct(), dp(), ee(), fe(), fm(), ft(), In() (+23 more)

### Community 44 - "yl"
Cohesion: 0.10
Nodes (31): Ao(), as(), Bd(), bl(), ef(), Fi(), Gd(), gl() (+23 more)

### Community 45 - "o"
Cohesion: 0.12
Nodes (29): o(), an(), b(), bl(), Cf(), es(), eu(), Fl() (+21 more)

### Community 46 - "Z"
Cohesion: 0.13
Nodes (29): al(), bi(), bu(), eu(), Fu(), gu(), hu(), iu() (+21 more)

### Community 47 - "IAdminService"
Cohesion: 0.25
Nodes (5): CancellationToken, Guid, IReadOnlyList, Task, IAdminService

### Community 48 - "Mailbox"
Cohesion: 0.15
Nodes (18): Miautrix.Mail.Licensing, InvalidOperationException, Mailbox, Address, DomainId, IsActive, QuotaBytes, UsedBytes (+10 more)

### Community 49 - "Pd"
Cohesion: 0.13
Nodes (28): Ai(), cc(), dc(), dh(), Di(), dl(), Ei(), fc() (+20 more)

### Community 50 - ".AuthenticateAsync"
Cohesion: 0.16
Nodes (16): DateTimeOffset, Guid, IReadOnlyList, AuthResult, AuthUserDto, ChangePasswordResult, CancellationToken, Guid (+8 more)

### Community 51 - "QuarantineItem"
Cohesion: 0.07
Nodes (28): DateTimeOffset, EntityBase, CreatedAt, Id, UpdatedAt, QuarantineItem, IsDelivered, QuarantinedAt (+20 more)

### Community 52 - "b"
Cohesion: 0.13
Nodes (18): an(), b(), Bp(), cm(), em(), fp(), Gp(), lp() (+10 more)

### Community 53 - "IRequestContextAccessor"
Cohesion: 0.14
Nodes (20): ControllerBase, IHttpContextAccessor, QuarantineFilter, AuditController, CancellationToken, Guid, HttpDelete, HttpGet (+12 more)

### Community 54 - "InMemorySecurityEventSink"
Cohesion: 0.20
Nodes (14): IPasswordHasher, DateTimeOffset, Guid, IReadOnlyList, List, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink (+6 more)

### Community 55 - "User"
Cohesion: 0.08
Nodes (21): Miautrix.Mail.Application.Users, DomainEntity, IUserService, Membership, RoleId, UserId, Permission, Code (+13 more)

### Community 56 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 57 - "Miautrix.Mail.sln"
Cohesion: 0.10
Nodes (14): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+6 more)

### Community 58 - "Folder"
Cohesion: 0.13
Nodes (19): Folder, MailboxId, Name, Role, UidNext, UidValidity, Tenant, CancellationToken (+11 more)

### Community 59 - "compilerOptions"
Cohesion: 0.08
Nodes (24): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+16 more)

### Community 60 - "B"
Cohesion: 0.14
Nodes (24): Fv(), B(), cb(), db(), Dm(), Fe(), ia(), Ii() (+16 more)

### Community 61 - "ITenantAuthorizationHelper"
Cohesion: 0.20
Nodes (10): Guid, IPermissionRepository, ITenantAuthorizationHelper, LastOwnerDemotionException, ResourceNotFoundException, TenantAuthorizationHelper, Fact, Guid (+2 more)

### Community 62 - "compilerOptions"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+14 more)

### Community 63 - "Miautrix.Mail.Domain.csproj"
Cohesion: 0.11
Nodes (17): net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk (+9 more)

### Community 64 - "qd"
Cohesion: 0.16
Nodes (22): as(), Bd(), bt(), d(), ef(), Fd(), Gd(), Hd() (+14 more)

### Community 65 - "ss"
Cohesion: 0.16
Nodes (22): ac(), bs(), cs(), ds(), fs(), j(), ks(), ls() (+14 more)

### Community 66 - "AdminContracts.cs"
Cohesion: 0.16
Nodes (19): DateTimeOffset, Dictionary, Guid, List, AdminUserDto, AuditLogDto, BackupJobDto, CreateRuleRequest (+11 more)

### Community 67 - "SieveTests"
Cohesion: 0.17
Nodes (12): Miautrix.Mail.Protocols.Sieve, Miautrix.Mail.ProtocolTests.Sieve, CancellationToken, Guid, HashSet, Task, SieveParser, SieveParseResult (+4 more)

### Community 68 - "compilerOptions"
Cohesion: 0.10
Nodes (19): node, compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, noEmit (+11 more)

### Community 69 - "Guid"
Cohesion: 0.10
Nodes (20): Guid, Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes, StoragePath (+12 more)

### Community 70 - ".Main"
Cohesion: 0.14
Nodes (10): ApiBehaviorOptions, otpnet, AuthService, ITotpService, TotpService, HttpContext, RequestDelegate, Task (+2 more)

### Community 71 - ".StageAndSwitchAsync"
Cohesion: 0.19
Nodes (10): Miautrix.Mail.Infrastructure.Deployment, Miautrix.Mail.IntegrationTests.BlueGreen, CancellationToken, Func, Task, DeploymentManager, IDeploymentManager, Fact (+2 more)

### Community 72 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+10 more)

### Community 73 - "vn"
Cohesion: 0.18
Nodes (19): Ar(), Be(), brighter(), by(), cE(), clamp(), darker(), formatHsl() (+11 more)

### Community 74 - "Pd"
Cohesion: 0.20
Nodes (19): Ai(), cc(), dc(), dh(), Di(), Ei(), fc(), hh() (+11 more)

### Community 75 - "AuthApiTests"
Cohesion: 0.26
Nodes (6): Argon2idPasswordHasher, Fact, Guid, Task, WebApplicationFactory, AuthApiTests

### Community 76 - "ApiResponse"
Cohesion: 0.27
Nodes (12): ApiResponse, PaginationMeta, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 77 - "MockSmtpQueueManager"
Cohesion: 0.23
Nodes (11): CancellationToken, Fact, Guid, HashSet, List, Task, MockAuthenticator, MockDomainValidator (+3 more)

### Community 78 - "Wb"
Cohesion: 0.11
Nodes (9): nS(), g(), Wb(), et(), nt(), tt(), ut(), X() (+1 more)

### Community 79 - "Miautrix.Mail.Identity.csproj"
Cohesion: 0.12
Nodes (14): Konscious.Security.Cryptography.Argon2 (1.3.1), Otp.NET (1.4.1), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk (+6 more)

### Community 80 - "bx"
Cohesion: 0.18
Nodes (17): bx(), em(), fr(), fx(), gf(), Gx(), ku(), Lx() (+9 more)

### Community 81 - "ISessionManager"
Cohesion: 0.26
Nodes (10): SessionToken, DateTimeOffset, Guid, RefreshToken, TimeSpan, AuthToken, ISessionManager, RefreshTokenInfo (+2 more)

### Community 82 - ".Update"
Cohesion: 0.26
Nodes (12): CreateUserRequest, UpdateUserRequest, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 83 - "SmtpQueueItem"
Cohesion: 0.15
Nodes (14): SmtpQueueItem, Attempts, LastAttemptAt, LastError, NextAttemptAt, RawMessage, Recipient, Sender (+6 more)

### Community 84 - "CommandSettings"
Cohesion: 0.15
Nodes (14): AsyncCommand, CommandSettings, CancellationToken, CommandContext, Task, BackupCommand, BackupSettings, OutputPath (+6 more)

### Community 85 - "k"
Cohesion: 0.22
Nodes (16): cl(), fl(), Fo(), Ja(), k(), Ka(), ko(), No() (+8 more)

### Community 86 - "InMemoryIdempotencyStore"
Cohesion: 0.24
Nodes (9): ConcurrentDictionary, HashSet, HttpContext, RequestDelegate, Task, IdempotencyMiddleware, CapturedResponse, IIdempotencyStore (+1 more)

### Community 87 - "Miautrix.Mail.UnitTests.csproj"
Cohesion: 0.13
Nodes (12): NetArchTest.eNhancedEdition (1.4.5), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4) (+4 more)

### Community 88 - "DomainController"
Cohesion: 0.32
Nodes (10): CreateDomainRequest, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, IResult, ProducesResponseType (+2 more)

### Community 89 - ".When_message_is_enqueued_and_fails_the_system_persists_queue_row_and_strictly_increasing_retry"
Cohesion: 0.26
Nodes (8): CancellationToken, Guid, Task, ISmtpQueueManager, SmtpQueueManager, Fact, Task, SmtpQueueDatabaseTests

### Community 90 - "AuthController"
Cohesion: 0.31
Nodes (10): CancellationToken, HttpGet, HttpPost, IResult, ProducesResponseType, Task, AuthController, ChangePasswordRequest (+2 more)

### Community 91 - "SanitizedMessageBody.tsx"
Cohesion: 0.20
Nodes (9): ref_dompurify, ref_react_dom_client, ref_testing_library_react, ref_vitest, App(), SanitizedMessageBody(), SanitizedMessageBodyProps, sanitizeEmailHtml() (+1 more)

### Community 92 - ".SeedTenantAndMailboxAsync"
Cohesion: 0.32
Nodes (7): Exception, IClassFixture, Fact, Guid, Task, WebApplicationFactory, MailApiTests

### Community 93 - "Miautrix.Mail.IntegrationTests.csproj"
Cohesion: 0.14
Nodes (12): Microsoft.AspNetCore.Mvc.Testing (10.0.4), Microsoft.EntityFrameworkCore.InMemory (10.0.4), net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1) (+4 more)

### Community 94 - "Miautrix.Mail.Cli.csproj"
Cohesion: 0.15
Nodes (11): Microsoft.Extensions.Configuration (10.0.4), Microsoft.Extensions.Configuration.EnvironmentVariables (10.0.4), Microsoft.Extensions.DependencyInjection (10.0.4), Spectre.Console (0.55.0), Spectre.Console.Cli (0.55.0), net10.0, Microsoft.NET.Sdk, net10.0 (+3 more)

### Community 95 - ".SeedTenantAndUserAsync"
Cohesion: 0.30
Nodes (7): RolePermission, PermissionId, RoleId, Fact, Guid, Task, CliTests

### Community 96 - "MailQueueApiTests"
Cohesion: 0.36
Nodes (5): Fact, Guid, Task, WebApplicationFactory, MailQueueApiTests

### Community 97 - "AuditLog"
Cohesion: 0.18
Nodes (10): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Fact (+2 more)

### Community 98 - "Miautrix.Mail.Persistence.csproj"
Cohesion: 0.15
Nodes (10): net10.0, Microsoft.EntityFrameworkCore (10.0.4), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0 (+2 more)

### Community 99 - ".HandleDataAsync"
Cohesion: 0.29
Nodes (8): CancellationToken, Guid, Response, Task, ISmtpInboundHandler, SmtpInboundHandler, SmtpResponse, IsSuccess

### Community 100 - "SpamVerdict"
Cohesion: 0.17
Nodes (12): SpamVerdict, DkimResult, DmarcResult, DnsblListed, Greylisted, IsSpam, ReasonsJson, Recipient (+4 more)

### Community 101 - "MailboxCreateSettings"
Cohesion: 0.20
Nodes (10): CancellationToken, CommandContext, Guid, Task, MailboxCreateCommand, MailboxCreateSettings, Address, QuotaBytes (+2 more)

### Community 102 - "AddMailAndSpamColumns"
Cohesion: 0.20
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, AddMailAndSpamColumns

### Community 103 - "MailboxDeleteSettings"
Cohesion: 0.22
Nodes (9): CancellationToken, CommandContext, Guid, Task, MailboxDeleteCommand, MailboxDeleteSettings, Address, Tenant (+1 more)

### Community 104 - ".HandleSubmissionAsync"
Cohesion: 0.38
Nodes (6): CancellationToken, Guid, Response, Task, ISmtpSubmissionHandler, SmtpSubmissionHandler

### Community 105 - "SystemController"
Cohesion: 0.53
Nodes (6): CancellationToken, HttpGet, IResult, ProducesResponseType, Task, SystemController

### Community 106 - "UsersScreen.tsx"
Cohesion: 0.28
Nodes (5): UsersScreen(), UsersScreenProps, AdminUserItem, CreateUserRequest, UpdateUserRequest

### Community 107 - "Miautrix.Mail.Web.csproj"
Cohesion: 0.22
Nodes (7): Microsoft.AspNetCore.OpenApi (10.0.4), Microsoft.OpenApi (2.7.5), Microsoft.NET.Sdk.Web, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.EntityFrameworkCore.Design (10.0.4)

### Community 108 - "plugins"
Cohesion: 0.22
Nodes (8): oxc, typescript, warn, plugins, rules, react/only-export-components, react/rules-of-hooks, $schema

### Community 109 - "DomainListCommand"
Cohesion: 0.25
Nodes (8): CancellationToken, CommandContext, Guid, Task, DomainListCommand, DomainListSettings, Tenant, UserId

### Community 110 - "MailboxListCommand"
Cohesion: 0.25
Nodes (8): CancellationToken, CommandContext, Guid, Task, MailboxListCommand, MailboxListSettings, Tenant, UserId

### Community 111 - "SystemInfoCommand.cs"
Cohesion: 0.32
Nodes (6): Command, CancellationToken, CommandContext, SystemInfoCommand, SystemInfoSettings, system_runtime_interopservices

### Community 112 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 113 - "QuarantineReleaseCommand"
Cohesion: 0.29
Nodes (7): CancellationToken, CommandContext, Guid, Task, QuarantineReleaseCommand, QuarantineReleaseSettings, Id

### Community 114 - "QueueListCommand"
Cohesion: 0.29
Nodes (7): CancellationToken, CommandContext, Task, QueueListCommand, QueueListSettings, Status, Tenant

### Community 115 - "QueueRetryCommand"
Cohesion: 0.29
Nodes (7): CancellationToken, CommandContext, Guid, Task, QueueRetryCommand, QueueRetrySettings, Id

### Community 116 - ".Main"
Cohesion: 0.32
Nodes (4): Task, Program, Guid, EfPermissionRepository

### Community 117 - "Domain"
Cohesion: 0.25
Nodes (8): Domain, DkimPublicKey, DkimSelector, DmarcRecord, IsPrimary, IsVerified, Name, SpfRecord

### Community 118 - ".BuildTargetModel"
Cohesion: 0.21
Nodes (6): DateTimeOffset, Guid, ModelBuilder, DateTimeOffset, Guid, ModelBuilder

### Community 119 - ".Error"
Cohesion: 0.25
Nodes (6): IReadOnlyDictionary, ApiError, HttpContext, IReadOnlyDictionary, IResult, ApiResults

### Community 120 - "ApiExceptionMiddleware"
Cohesion: 0.43
Nodes (5): ILogger, HttpContext, RequestDelegate, Task, ApiExceptionMiddleware

### Community 121 - "ExponentialBackoffWithJitterRetryPolicy"
Cohesion: 0.52
Nodes (4): Random, TimeSpan, ExponentialBackoffWithJitterRetryPolicy, IRetryPolicy

### Community 122 - ".List"
Cohesion: 0.29
Nodes (6): AuditFilter, CancellationToken, HttpGet, IResult, ProducesResponseType, Task

### Community 123 - "RestoreCommand"
Cohesion: 0.33
Nodes (6): CancellationToken, CommandContext, Task, RestoreCommand, RestoreSettings, ArchivePath

### Community 124 - ".IsDomainLocal"
Cohesion: 0.33
Nodes (3): Guid, ISmtpAuthenticator, ISmtpDomainValidator

### Community 125 - "Miautrix.Mail.EndToEndTests.csproj"
Cohesion: 0.29
Nodes (6): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 126 - "AuthStatus"
Cohesion: 0.33
Nodes (6): AuthStatus, Failed, LockedOut, MfaRequired, Success, UserNotFound

### Community 127 - "MailFlowRule"
Cohesion: 0.33
Nodes (6): MailFlowRule, ActionsJson, ConditionsJson, IsEnabled, Name, Priority

### Community 129 - "cs"
Cohesion: 0.40
Nodes (5): bf(), cs(), dx(), qc(), Sx()

### Community 130 - "LoginStatus"
Cohesion: 0.40
Nodes (5): LoginStatus, Failed, LockedOut, MfaRequired, Success

### Community 131 - "BackupScreen.tsx"
Cohesion: 0.50
Nodes (3): BackupScreen(), BackupScreenProps, admin_src_types_backupitem

### Community 133 - "format-status.js"
Cohesion: 0.50
Nodes (3): fs, tasks, ref_fs

### Community 134 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 135 - ".BuildModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

## Knowledge Gaps
- **575 isolated node(s):** `name`, `version`, `type`, `dev`, `build` (+570 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 867 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **10 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `x()` connect `bw` to `Vt`, `cg`, `o`, `n`?**
  _High betweenness centrality (0.014) - this node is a cross-community bridge._
- **Why does `T()` connect `bw` to `index-74ij7yKh.js`, `En`, `i`, `c`?**
  _High betweenness centrality (0.012) - this node is a cross-community bridge._
- **Are the 25 inferred relationships involving `lS()` (e.g. with `Uo()` and `Ft()`) actually correct?**
  _`lS()` has 25 INFERRED edges - model-reasoned connections that need verification._
- **What connects `name`, `version`, `type` to the rest of the system?**
  _575 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `index-74ij7yKh.js` be split into smaller, more focused modules?**
  _Cohesion score 0.018391787852865698 - nodes in this community are weakly interconnected._
- **Should `lS` be split into smaller, more focused modules?**
  _Cohesion score 0.03325705568268497 - nodes in this community are weakly interconnected._
- **Should `index-CTRlFaj-.js` be split into smaller, more focused modules?**
  _Cohesion score 0.03552960800667223 - nodes in this community are weakly interconnected._