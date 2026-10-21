# Graph Report - miautrix-mail-server  (2026-10-20)

## Corpus Check
- 419 files · ~4,790,580 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 635 file(s) not represented in the graph (top: .dll 560, .so 28, .pdb 24)

## Summary
- 4990 nodes · 16624 edges · 210 communities (154 shown, 56 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 1932 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `30a5ef4e`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Task
- Miautrix.Mail.Persistence
- index-SHp2FWM5.js
- system
- webmail/src/App.tsx
- ILogger
- InboundController
- Message
- system_text
- QuarantineService
- index-7_LIhLyZ.js
- admin/src/App.tsx
- AI Build Instructions
- List
- InboxView.tsx
- system
- MailboxAdminApiTests
- i
- ITenantAuthorizationHelper
- DkimService
- InMemorySecurityEventSink
- dN
- it
- Miautrix Mail Server
- AdminApiClient
- CalendarService
- compilerOptions
- TenantScopedEntityBase
- compilerOptions
- CalendarEventAttendee
- .SeedTenantAsync
- Folder
- Attachment
- SieveScript
- .TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData
- .StageAndSwitchAsync
- ISmtpQueueManager
- Miautrix Mail Server — Implementation Blueprint
- compilerOptions
- A
- SmtpQueueItem
- n
- np
- Components
- compilerOptions
- AuthService
- mu
- CancellationToken
- MimeMessageBuilder
- l
- devDependencies
- devDependencies
- compilerOptions
- devDependencies
- Miautrix Mail Server - Production Deployment Guide (Debian LXC)
- .Main
- Guid
- c
- n
- index.ts
- o
- Zc
- CloudflareApiMailTransport
- plugins
- i
- User
- Mailbox
- SieveRuleService
- ComposerView.tsx
- QueueScreen.tsx
- Xe
- Tenant
- .Main
- ISpamProvider
- SystemInfoCommand.cs
- o1
- webmail/src/types.ts
- Miautrix.Mail.sln
- ResourceNotFoundException
- qg
- Epic 1 — Core platform
- Es
- Miautrix Mail Server
- SmtpDeliveryAttempt
- Dt
- Fd
- PulsePoint
- e
- 5. Recommended Technology Stack
- package.json
- ParsedInboundMessage
- Domain
- Ue
- ImapSession
- Change log — MIA-76, `POST /send` contract fix
- 11.2 Component Remediation & Verification Findings
- IOutboundMailTransport.cs
- Miautrix Mail Server — Errors, Issues & Review Log
- CalendarSubscriptionApiTests
- MailFlowRule
- Miautrix.Mail.Identity.csproj
- MailboxArchiveService
- Epic 3 — Surfaces
- Epic 4 — Operations
- Rule: secrets and logging
- Rule: secrets and logging
- 3. Core Architectural Principles
- 9. Identity Architecture
- Change log — MIA-77, Inbound `email()` Wildcard Allow-list & Webhook Destination Remediation
- Calendar & Webmail Final Solution — 2026-10-03
- AuthApiTests
- InboundQueueDispatcherTests
- BackupJob
- Contact
- 2. Palette
- AuthStatus
- MailSignature
- Miautrix.Mail.UnitTests.csproj
- .on
- SmtpListenerService
- MalwareVerdict
- SieveFilterRule
- InboundQueueDispatcher.cs
- MailQueueService
- bb
- Project Update Checklist
- CalendarView.tsx
- UsersScreen.tsx
- DkimKey
- format-status.js
- AppDbContext
- bg
- MailboxArchiveService.cs
- SieveRulesView.tsx
- ImapAuthenticatorTests
- cloudflare-outbound-delivery-restored-2026-10-06.md
- MigrationBuilder
- EntityBase
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
- SmtpQueueService.cs
- Miautrix.Mail.Cli.csproj
- SpamVerdict
- EmailMessage
- verify-send-contract.sh
- Future implementation items
- project-status.md
- .RunOnePassAsync
- Miautrix.Mail.EndToEndTests.csproj
- verify-inbound-contract.sh
- ISessionManager
- Fact
- .ApplyMigrations
- IReadOnlyList
- client.ts
- Miautrix.Mail.Persistence.csproj
- Miautrix.Mail.IntegrationTests.csproj
- IMailStorage
- WebmailApiClient
- SimulateSampleMessage
- Miautrix.Mail.Worker.csproj
- Iris Pay
- AntiMalwareScreen.tsx
- Pro tokens
- Tokens
- UnitTestProjectSmokeTests.cs
- EndToEndTestProjectSmokeTests.cs
- lxc-prepare-storage.sh
- update-prod-database.sh
- Miautrix.Mail.Application.csproj
- inject-flow-mail-test.sh
- lxc-install-antimalware.sh
- lxc-install-worker-env.sh
- update-database.sh
- update-test-database.sh
- inject-eicar-test.sh
- lxc-restart-services.sh
- lxc-update-antimalware-signatures.sh

## God Nodes (most connected - your core abstractions)
1. `dN()` - 412 edges
2. `AppDbContext` - 195 edges
3. `o()` - 105 edges
4. `AdminApiClient` - 97 edges
5. `Miautrix.Mail.Persistence` - 91 edges
6. `AdminService` - 89 edges
7. `ApiResponse` - 82 edges
8. `i()` - 77 edges
9. `WebmailApiClient` - 70 edges
10. `Miautrix.Mail.Domain` - 65 edges

## Surprising Connections (you probably didn't know these)
- `8. Platform coverage` --references--> `sh()`  [INFERRED]
  workers/email/CHANGELOG-MIA-76.md → publish/admin/assets/index-7_LIhLyZ.js
- `Platform coverage` --references--> `sh()`  [INFERRED]
  workers/email/README.md → publish/admin/assets/index-7_LIhLyZ.js
- `T1 — Solution scaffold and CI` --references--> `Domain`  [INFERRED]
  blueprints/miautrix-mail-server/epics/01-core-platform.md → src/Miautrix.Mail.Domain/Entities.cs
- `Architecture rules` --references--> `Domain`  [INFERRED]
  blueprints/miautrix-mail-server/workspace/CLAUDE.md → src/Miautrix.Mail.Domain/Entities.cs
- `Architecture rules` --references--> `Domain`  [INFERRED]
  CLAUDE.md → src/Miautrix.Mail.Domain/Entities.cs

## Import Cycles
- None detected.

## Communities (210 total, 56 thin omitted)

### Community 0 - "Task"
Cohesion: 0.03
Nodes (41): Per-domain settings pattern, Per-domain settings pattern, AdminUserDto, AntiMalwareSettingsDto, AntiMalwareStatusDto, AntiSpamSettingsDto, AssignMailboxRequest, AuditFilter (+33 more)

### Community 1 - "Miautrix.Mail.Persistence"
Cohesion: 0.06
Nodes (31): Miautrix.Mail.IntegrationTests, Miautrix.Mail.Protocols.Imap, Miautrix.Mail.Infrastructure.MailboxArchiving, Miautrix.Mail.IntegrationTests.Licensing, Miautrix.Mail.ProtocolTests, Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Cli.Commands, Miautrix.Mail.Infrastructure.Backup (+23 more)

### Community 2 - "index-SHp2FWM5.js"
Cohesion: 0.04
Nodes (93): an(), ap(), bn(), br(), ch(), changePassword(), Cn(), Cp() (+85 more)

### Community 4 - "webmail/src/App.tsx"
Cohesion: 0.16
Nodes (13): App(), folderIconMap, mapFolders(), normalizeMailboxAccount(), ChangePasswordView(), ChangePasswordViewProps, ComposeInitialState, ContactsView() (+5 more)

### Community 5 - "ILogger"
Cohesion: 0.16
Nodes (6): ApiExceptionMiddleware, IdempotencyMiddleware, CapturedResponse, IIdempotencyStore, InMemoryIdempotencyStore, RequestIdMiddleware

### Community 6 - "InboundController"
Cohesion: 0.07
Nodes (26): 12.10 Defect: Inbound Messages Are Enqueued Into the Outbound Send Queue, 12.11 Corrected Root Cause: Unsynchronised Race Between Two Queue Dispatchers, 12.12 Resolved: Cloudflare Access Interception and False Outbound Delivery Success (MIA-92), 12.1 Objective & Governance, 12.2 Cloudflare Email Routing Configuration, 12.3 Verification Matrix & Live Evidence, 12.4 Rollback Procedure, 12.5 Defect: Inbound Allow-List Rejected All External Senders (SMTP 555 5.7.1) (+18 more)

### Community 7 - "Message"
Cohesion: 0.09
Nodes (18): Message, BodyHtml, BodyText, ContentHash, Date, Flags, FolderId, IsRead (+10 more)

### Community 8 - "system_text"
Cohesion: 0.13
Nodes (5): Miautrix.Mail.Web, Miautrix.Mail.UnitTests.Calendar, Miautrix.Mail.IntegrationTests.Calendar, Miautrix.Mail.SecurityTests.Identity, Miautrix.Mail.Application.Mail.Mime

### Community 9 - "QuarantineService"
Cohesion: 0.19
Nodes (8): Where things are, Where things are, QuarantineService, InboundMailContext, InboundProcessResult, RuleMatch, RuleBasedSpamProvider, AntiSpamTests

### Community 10 - "index-7_LIhLyZ.js"
Cohesion: 0.02
Nodes (144): Af(), AN(), av(), aw(), b0(), bj(), brighter(), bS() (+136 more)

### Community 11 - "admin/src/App.tsx"
Cohesion: 0.06
Nodes (38): apiClient, App(), AntiSpamScreen(), AntiSpamScreenProps, makeClient(), BackupScreen(), BackupScreenProps, ChangePasswordView() (+30 more)

### Community 12 - "AI Build Instructions"
Cohesion: 0.20
Nodes (10): 1 · Your role, 2 · Token compliance, 3 · Component recipes, 4 · Hard constraints, 5 · Before you finish — verify, AI Build Instructions, Buttons, Cards (+2 more)

### Community 13 - "List"
Cohesion: 0.20
Nodes (11): Miautrix.Mail.MailFlow, IMailFlowEngine, IMailFlowSimulator, MailFlowEngine, MailFlowMessageContext, MailFlowSimulationResult, RuleAction, RuleCondition (+3 more)

### Community 14 - "InboxView.tsx"
Cohesion: 0.12
Nodes (27): dompurify, buildBodyFallbackHtml(), buildMoveMenuFolders(), clamp(), clampSnippet(), compareMailboxLabels(), compareSystemFolders(), FLAG_COLORS (+19 more)

### Community 15 - "system"
Cohesion: 0.17
Nodes (12): Miautrix.Mail.Web.Infrastructure, Miautrix.Mail.Web.Controllers, Miautrix.Mail.Application.Auth, Miautrix.Mail.Application.Admin, Miautrix.Mail.Web.Contracts, Miautrix.Mail.Application.Mail, ChangePasswordRequest, LoginRequest (+4 more)

### Community 16 - "MailboxAdminApiTests"
Cohesion: 0.26
Nodes (3): MailboxAdminApiTests, Storage, TenantScope

### Community 17 - "i"
Cohesion: 0.06
Nodes (75): aa(), Ai(), al(), ba(), bc(), bo(), ca(), cl() (+67 more)

### Community 18 - "ITenantAuthorizationHelper"
Cohesion: 0.13
Nodes (6): EfPermissionRepository, IPermissionRepository, LastOwnerDemotionException, TenantAuthorizationHelper, IsolationTests, MockPermissionRepo

### Community 19 - "DkimService"
Cohesion: 0.17
Nodes (5): DkimKeyPair, DkimService, DnsAuthHelper, IDkimService, DkimTests

### Community 20 - "InMemorySecurityEventSink"
Cohesion: 0.13
Nodes (16): IPasswordHasher, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink, ISecurityEventSink, LoginResult, LoginStatus, Failed (+8 more)

### Community 21 - "dN"
Cohesion: 0.04
Nodes (75): dN(), _1(), ab(), ad(), Am(), Au(), ax(), bh() (+67 more)

### Community 22 - "it"
Cohesion: 0.10
Nodes (65): ae(), ao(), b(), C(), Ce(), D(), E(), ee() (+57 more)

### Community 23 - "Miautrix Mail Server"
Cohesion: 0.08
Nodes (24): 10. Authentication and Authorization, 11. MFA, 12. Security Event Codes, 15. Graphical Mail Flow Designer, 16. Mail Flow Simulator, 17. Mail Flow Explorer, 18. Restricted Device Relay, 19. Anti-Spam Architecture (+16 more)

### Community 24 - "AdminApiClient"
Cohesion: 0.09
Nodes (7): AdminApiClient, normalizeGuid(), AntiMalwareSettings, AntiSpamSettings, ApiResponse, SecuritySettings, SharedMailbox

### Community 25 - "CalendarService"
Cohesion: 0.07
Nodes (25): CalendarService, NormalizedInvitee, ICalendarService, CalendarEventDto, CalendarEventRequest, CalendarInvitationViewDto, CalendarInviteeRequest, CalendarRsvpRequest (+17 more)

### Community 26 - "compilerOptions"
Cohesion: 0.10
Nodes (19): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+11 more)

### Community 27 - "TenantScopedEntityBase"
Cohesion: 0.07
Nodes (53): Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, CalendarSubscription, TargetUserId (+45 more)

### Community 28 - "compilerOptions"
Cohesion: 0.10
Nodes (19): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+11 more)

### Community 29 - "CalendarEventAttendee"
Cohesion: 0.07
Nodes (22): CalendarIcalBuilder, CalendarIcalMethod, Cancel, Request, CalendarInvitationBuilder, CalendarInvitationMessage, CalendarEventAttendee, DisplayName (+14 more)

### Community 30 - ".SeedTenantAsync"
Cohesion: 0.17
Nodes (7): CalendarAvailabilityTests, CalendarInvitationEmailTests, CalendarOutboundMimeTests, CalendarServiceAccountTests, CalendarTestHelpers, CalendarUser, CalendarVisibilityTests

### Community 31 - "Folder"
Cohesion: 0.07
Nodes (29): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Folder (+21 more)

### Community 32 - "Attachment"
Cohesion: 0.29
Nodes (7): Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes, StoragePath

### Community 33 - "SieveScript"
Cohesion: 0.19
Nodes (9): SieveScript, Content, IsActive, MailboxId, Name, SieveParser, SieveParseResult, SieveScriptService (+1 more)

### Community 34 - ".TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData"
Cohesion: 0.35
Nodes (3): ILicenseQuotaEnforcer, LicenseQuotaEnforcer, LicenseQuotaException

### Community 35 - ".StageAndSwitchAsync"
Cohesion: 0.10
Nodes (8): Miautrix.Mail.Infrastructure.Deployment, Miautrix.Mail.IntegrationTests.BlueGreen, Miautrix.Mail.Cli.Infrastructure, TypeRegistrar, TypeResolver, DeploymentManager, IDeploymentManager, BlueGreenUpdateTests

### Community 36 - "ISmtpQueueManager"
Cohesion: 0.12
Nodes (16): SmtpDomainValidator, ISmtpInboundHandler, SmtpInboundHandler, ISmtpAuthenticator, ISmtpDomainValidator, SmtpResponse, IsSuccess, ISmtpSubmissionHandler (+8 more)

### Community 37 - "Miautrix Mail Server — Implementation Blueprint"
Cohesion: 0.06
Nodes (36): 19.1 Where they run, 19.2 Settings allowlist, 19.3 Toolchain prerequisites, 19.6 Verify-critical configuration, 2.1 In scope for v1, 2.2 Out of scope for v1, 2.3 Non-goals, 4.1 Backend — NuGet, verified 2026-09-16 (+28 more)

### Community 38 - "compilerOptions"
Cohesion: 0.12
Nodes (16): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, noEmit, noFallthroughCasesInSwitch (+8 more)

### Community 39 - "A"
Cohesion: 0.10
Nodes (69): bC(), cN(), k(), T(), d_(), dC, Dl(), e1() (+61 more)

### Community 40 - "SmtpQueueItem"
Cohesion: 0.14
Nodes (14): QueueDirection, Inbound, Outbound, SmtpQueueItem, Attempts, Direction, LastAttemptAt, LastError (+6 more)

### Community 41 - "n"
Cohesion: 0.08
Nodes (49): Eh(), ac(), ah(), bh(), cc(), Ci(), constructor(), dc() (+41 more)

### Community 42 - "np"
Cohesion: 0.10
Nodes (35): am(), Au(), bm(), Bp(), cm(), dl(), em(), fp() (+27 more)

### Community 43 - "Components"
Cohesion: 0.12
Nodes (16): Buttons, Cards, Checkboxes, Chips, Components, Default Item, Disabled State, Filter Chip (+8 more)

### Community 44 - "compilerOptions"
Cohesion: 0.12
Nodes (16): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+8 more)

### Community 45 - "AuthService"
Cohesion: 0.24
Nodes (5): AuthResult, AuthUserDto, ChangePasswordResult, AuthService, IAuthService

### Community 46 - "mu"
Cohesion: 0.10
Nodes (36): bl(), Bu(), Cu(), eu(), fu(), gi(), gm(), gu() (+28 more)

### Community 47 - "CancellationToken"
Cohesion: 0.07
Nodes (25): ApiResponse, PaginationMeta, QueueItemDto, AttachmentController, AuditController, AuthController, CalendarController, DomainController (+17 more)

### Community 48 - "MimeMessageBuilder"
Cohesion: 0.13
Nodes (5): MimeAttachment, MimeEncoders, BuiltMimeMessage, MimeMessageBuilder, MimeMessageRequest

### Community 49 - "l"
Cohesion: 0.12
Nodes (31): Bd(), be(), Do(), ef(), eo(), ga(), Gd(), gl() (+23 more)

### Community 50 - "devDependencies"
Cohesion: 0.06
Nodes (32): dependencies, react, react-dom, @xyflow/react, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react (+24 more)

### Community 51 - "devDependencies"
Cohesion: 0.06
Nodes (31): dependencies, react, react-dom, devDependencies, jsdom, @testing-library/jest-dom, @testing-library/react, @types/react (+23 more)

### Community 52 - "compilerOptions"
Cohesion: 0.11
Nodes (18): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+10 more)

### Community 53 - "devDependencies"
Cohesion: 0.05
Nodes (43): lucide-react, oxlint, @types/dompurify, @types/node, dependencies, lucide-react, react, react-dom (+35 more)

### Community 54 - "Miautrix Mail Server - Production Deployment Guide (Debian LXC)"
Cohesion: 0.13
Nodes (14): 1.1. Install System Dependencies & NGINX, 1.2. Configure Systemd Service for .NET Backend, 1.3. Configure NGINX for Cloudflare Tunnel (`mail.miautrix.tech`), 2.1. Publish .NET 10 Self-Contained Binary, 2.2. Build Frontends (Web Admin & Webmail), 2.3. Apply Database Migrations to `miautrix-mail-pro`, 3.1. Copy Published Files using Windows Built-in `scp`, Architecture & Configuration Summary (+6 more)

### Community 55 - ".Main"
Cohesion: 0.18
Nodes (6): CalendarInvitationOptions, PublicBaseUrl, BackupOptions, HeaderRequestContextAccessor, CurrentTenantId, CurrentUserId

### Community 56 - "Guid"
Cohesion: 0.05
Nodes (18): OrphanMailboxDto, QuarantineFilter, QuarantineItemDto, AvailabilityCompareDto, AvailabilityCompareRequest, CalendarAttendeeDto, CalendarAvailabilityDto, CalendarBusyBlockDto (+10 more)

### Community 57 - "c"
Cohesion: 0.06
Nodes (85): _2(), a0(), d(), aC(), aE(), Bw(), f(), h() (+77 more)

### Community 58 - "n"
Cohesion: 0.09
Nodes (58): b(), b1(), bd(), bm(), Bt(), ci(), cm(), cr() (+50 more)

### Community 59 - "index.ts"
Cohesion: 0.11
Nodes (21): authorize(), decodeBase64(), email(), fetch(), handleSend(), isSenderAllowed(), jsonError(), looksLikeAddress() (+13 more)

### Community 60 - "o"
Cohesion: 0.06
Nodes (59): A(), A1(), ac(), ai(), at(), bu(), cb(), cg() (+51 more)

### Community 61 - "Zc"
Cohesion: 0.07
Nodes (7): bv(), Ga(), HS(), RS(), Tx(), yN, Zc

### Community 62 - "CloudflareApiMailTransport"
Cohesion: 0.08
Nodes (28): 12.9 Open: Cloudflare `env.EMAIL.send()` Rejects the Outbound Message (HTTP 502 `send_failed`), 7. Cloudflare Workers Integration, Deployment, Flows, Inbound `email()` wildcard allow-list & webhook forwarding remediated (MIA-77, 2026-10-04), Outstanding, `POST /send` fixed: 400 on a bad body, 401 on a bad token, no 1101 (MIA-76, 2026-10-04), What the Worker repository provides (+20 more)

### Community 63 - "plugins"
Cohesion: 0.33
Nodes (5): plugins, rules, react/only-export-components, react/rules-of-hooks, $schema

### Community 64 - "i"
Cohesion: 0.06
Nodes (58): Al(), ar(), Ay(), a(), c2(), cj(), cw(), d0() (+50 more)

### Community 65 - "User"
Cohesion: 0.06
Nodes (33): Miautrix.Mail.Application.Users, 1.1 Project Purpose and Business Case, 1.2 High-Level Objectives, 1. Project Initiation & Executive Summary, 2. Project Scope Management (WBS Overview), 3. Project Schedule & Milestone Management, 4. Quality Management & Verification Gates, 5. Risk Management Matrix (+25 more)

### Community 66 - "Mailbox"
Cohesion: 0.15
Nodes (12): Mailbox, Address, Department, DomainId, IsActive, Kind, Name, Organization (+4 more)

### Community 67 - "SieveRuleService"
Cohesion: 0.19
Nodes (5): ISieveRuleService, SieveRuleService, SieveFilterRuleDto, SieveFilterRuleRequest, SieveRuleController

### Community 68 - "ComposerView.tsx"
Cohesion: 0.13
Nodes (21): ComposerAccount, ComposerView(), ComposerViewProps, escapeHtml(), htmlToText(), insertSignatureBeforeQuote(), signatureHtml(), stripSignature() (+13 more)

### Community 69 - "QueueScreen.tsx"
Cohesion: 0.31
Nodes (4): QueueScreen(), QueueScreenProps, QueueItem, QueueQueryParams

### Community 70 - "Xe"
Cohesion: 0.11
Nodes (36): ap(), Ar(), bi(), C1(), Ca(), el(), em(), Fb() (+28 more)

### Community 71 - "Tenant"
Cohesion: 0.11
Nodes (17): Membership, RoleId, UserId, Permission, Code, Name, Role, Code (+9 more)

### Community 72 - ".Main"
Cohesion: 0.07
Nodes (42): BackupCommand, BackupSettings, OutputPath, DomainListCommand, DomainListSettings, Tenant, UserId, MailboxCreateCommand (+34 more)

### Community 73 - "ISpamProvider"
Cohesion: 0.06
Nodes (36): Admin screens, AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Provider seams, Scripts, Spam pipeline, Task tracking, Things that will bite you (+28 more)

### Community 75 - "o1"
Cohesion: 0.20
Nodes (27): Br(), ea(), ep(), eu(), hl(), il(), kr(), kt() (+19 more)

### Community 76 - "webmail/src/types.ts"
Cohesion: 0.11
Nodes (12): normalizeEmailAttachment(), WebmailLoginResult, AvailabilityCompare, CalendarAttendee, CalendarAvailability, CalendarConflict, DirectoryParticipant, EmailAttachment (+4 more)

### Community 77 - "Miautrix.Mail.sln"
Cohesion: 0.14
Nodes (8): Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk

### Community 78 - "ResourceNotFoundException"
Cohesion: 0.21
Nodes (6): ContactService, IContactService, ContactDto, ContactRequest, ResourceNotFoundException, ContactsController

### Community 79 - "qg"
Cohesion: 0.20
Nodes (15): Ba(), bc(), Eo(), Gb(), gd(), Kb(), pd(), Ph() (+7 more)

### Community 80 - "Epic 1 — Core platform"
Cohesion: 0.12
Nodes (14): Epic 1 — Core platform, T1 — Solution scaffold and CI, T2 — Domain model and EF Core schema, T3 — Seed data and indexes, T4 — Identity, T5 — Authorization and tenant isolation, T6 — Audit trail, Architecture rules (+6 more)

### Community 81 - "Es"
Cohesion: 0.11
Nodes (25): bp(), Da(), dg(), dr(), Es(), Fd(), hc(), ic() (+17 more)

### Community 82 - "Miautrix Mail Server"
Cohesion: 0.22
Nodes (8): Architecture rules, Commands, Data rules, Discovery workflow, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 83 - "SmtpDeliveryAttempt"
Cohesion: 0.25
Nodes (8): SmtpDeliveryAttempt, AttemptedAt, AttemptNumber, ErrorMessage, NextRetryDelaySeconds, QueueItemId, ResponseCode, Success

### Community 84 - "Dt"
Cohesion: 0.07
Nodes (49): ah(), Ao(), ch(), d1(), Do(), Dt(), fp(), Ge() (+41 more)

### Community 85 - "Fd"
Cohesion: 0.16
Nodes (20): af(), at(), cf(), df(), Fd(), ff(), ht(), jf() (+12 more)

### Community 86 - "PulsePoint"
Cohesion: 0.25
Nodes (8): Border Radius, Colors, Do's and Don'ts, Elevation, Overview, PulsePoint, Spacing, Typography

### Community 87 - "e"
Cohesion: 0.16
Nodes (20): as(), bs(), cs(), ds(), fs(), is(), ks(), ls() (+12 more)

### Community 88 - "5. Recommended Technology Stack"
Cohesion: 0.25
Nodes (8): 5. Recommended Technology Stack, Backend, Caching, Database, Frontend, Logging, Scheduling, Storage

### Community 89 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 90 - "ParsedInboundMessage"
Cohesion: 0.21
Nodes (8): ParsedAttachment, ParsedInboundMessage, Attachments, BodyHtml, BodyText, Date, RawHeaders, Subject

### Community 91 - "Domain"
Cohesion: 0.05
Nodes (38): Also check, Procedure, Reporting rules, verify-task, Also check, Procedure, Reporting rules, verify-task (+30 more)

### Community 92 - "Ue"
Cohesion: 0.07
Nodes (54): Ax(), b_(), BE(), bo, c(), cC(), cE(), dE() (+46 more)

### Community 93 - "ImapSession"
Cohesion: 0.09
Nodes (17): 6. Transport / Protocol Listener Issues, ImapCommandResult, ImapSession, MailboxId, SelectedFolder, State, TenantId, Username (+9 more)

### Community 94 - "Change log — MIA-76, `POST /send` contract fix"
Cohesion: 0.12
Nodes (16): 10. Reconciliation and Verification (Path 3), 1.1 The live deployment, read back, 1.2 Secrets bound to the script, 1.3 The unchanged public paths still answer, 1. Baseline — state before any change, 2. Build the change, 3.1 Setting it on the Worker — REFUSED, 3. The `SEND_TOKEN` secret (+8 more)

### Community 95 - "11.2 Component Remediation & Verification Findings"
Cohesion: 0.17
Nodes (13): 11.1 Context & Scope, 11.2 Component Remediation & Verification Findings, 11.3 Comprehensive Pass/Fail Matrix, 11.4 Residual Risks & Ongoing Controls, 11. External Email Flow Remediation, DNS/DKIM Validation, and End-to-End Verification (MIA-90), 1. Worker Source Recovery and Version Control, 2. Worker `POST /send` 1101 Elimination & Outbound Contract, 3. Inbound Email Routing & Worker `email()` Handler (+5 more)

### Community 96 - "IOutboundMailTransport.cs"
Cohesion: 0.29
Nodes (4): CloudflareProbeResult, ICloudflareTransport, OutboundMessage, TransportSendResult

### Community 97 - "Miautrix Mail Server — Errors, Issues & Review Log"
Cohesion: 0.12
Nodes (14): 10.1 Issue Summary & Root Cause, 10.2 Complete Request Paths Inventory, 10.3 Active Cloudflare Rule Expression & Verification, 10.4 Validation & Rollback Guidance, 10. Cloudflare API Containment Rule Diagnosis & Frontend Auth Analysis (MIA-88), 1. System & OS Environment Issues, 2. Frontend & UI Runtime Issues, 3. NGINX & Ingress Routing Issues (+6 more)

### Community 99 - "MailFlowRule"
Cohesion: 0.25
Nodes (8): Quarantine item lifecycle, Quarantine item lifecycle, MailFlowRule, ActionsJson, ConditionsJson, IsEnabled, Name, Priority

### Community 100 - "Miautrix.Mail.Identity.csproj"
Cohesion: 0.14
Nodes (13): Konscious.Security.Cryptography.Argon2 (1.3.1), Otp.NET (1.4.1), Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.EntityFrameworkCore (8.0.10), Npgsql.EntityFrameworkCore.PostgreSQL (8.0.10), Microsoft.NET.Sdk (+5 more)

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

### Community 108 - "Change log — MIA-77, Inbound `email()` Wildcard Allow-list & Webhook Destination Remediation"
Cohesion: 0.15
Nodes (12): 1. Baseline & Identified Defects, 2. Remediated Handler Contract, 3. Configuration in `wrangler.jsonc`, 4.1 Inbound Email Contract Suite (`verify-inbound-contract.sh`), 4.2 Outbound `POST /send` and HTTP Endpoint Regression Verification (`verify-send-contract.sh`), 4.3 Live Production Probes (Read-Only Verification), 4. Verification, 5. Rollback Target & Command (+4 more)

### Community 109 - "Calendar & Webmail Final Solution — 2026-10-03"
Cohesion: 0.17
Nodes (7): Calendar & Webmail Final Solution — 2026-10-03, Implementation Notes, Issues Closed, Manual Post-Deploy Checklist, Status, Verification Completed, Version 1.0 Closure

### Community 113 - "BackupJob"
Cohesion: 0.29
Nodes (7): BackupJob, ArchivePath, CompletedAt, ErrorMessage, Name, SizeBytes, Status

### Community 114 - "Contact"
Cohesion: 0.29
Nodes (7): Contact, Department, Email, Name, Organization, Phone, UserId

### Community 115 - "2. Palette"
Cohesion: 0.33
Nodes (6): 2. Palette, Accent (decorative only), Brand Dark, Interactive, Neutral, Primary

### Community 116 - "AuthStatus"
Cohesion: 0.33
Nodes (6): AuthStatus, Failed, LockedOut, MfaRequired, Success, UserNotFound

### Community 117 - "MailSignature"
Cohesion: 0.33
Nodes (6): MailSignature, ContentHtml, ContentText, IsDefault, Name, UserId

### Community 118 - "Miautrix.Mail.UnitTests.csproj"
Cohesion: 0.22
Nodes (7): NetArchTest.eNhancedEdition (1.4.5), Microsoft.NET.Sdk, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 119 - ".on"
Cohesion: 0.13
Nodes (21): $a(), a2(), As(), b2(), copy(), g0(), E(), M() (+13 more)

### Community 120 - "SmtpListenerService"
Cohesion: 0.11
Nodes (14): Boundary: Cloudflare is transport only, ImapListenerService, SmtpInboundMxListener, SmtpSubmissionImplicitTlsListener, SmtpSubmissionStartTlsListener, WorkerHostOptions, Hostname, SmtpListenerMode (+6 more)

### Community 121 - "MalwareVerdict"
Cohesion: 0.33
Nodes (6): MalwareVerdict, Engine, IsMalware, Recipient, Sender, ThreatName

### Community 123 - "InboundQueueDispatcher.cs"
Cohesion: 0.22
Nodes (7): Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.AntiMalware, Miautrix.Mail.Application.Transport, Miautrix.Mail.AntiSpam, Miautrix.Mail.Storage, Miautrix.Mail.Worker, Miautrix.Mail.IntegrationTests.Cloudflare

### Community 124 - "MailQueueService"
Cohesion: 0.23
Nodes (8): MailQueueService, QueueFilter, QueuePage, QueueStatusFilter, DeadLetter, Delivered, Queued, Retrying

### Community 125 - "bb"
Cohesion: 0.13
Nodes (35): Aa(), ag(), bb(), cd(), di(), Fe(), gg(), gp() (+27 more)

### Community 126 - "Project Update Checklist"
Cohesion: 0.40
Nodes (4): Always review/update, Project Update Checklist, Recent example: Webmail contacts/address book, Update process

### Community 128 - "CalendarView.tsx"
Cohesion: 0.13
Nodes (19): CalendarEventContextMenu(), CalendarEventContextMenuProps, clamp(), CalendarEventDialog(), CalendarEventDialogProps, CalendarRsvpDialog(), CalendarRsvpDialogProps, CalendarView() (+11 more)

### Community 129 - "UsersScreen.tsx"
Cohesion: 0.20
Nodes (15): AssignMailboxModal(), AssignMailboxModalProps, splitEmail(), MailboxDelegatePicker(), MailboxDelegatePickerProps, formatBytes(), SharedMailboxesScreen(), SharedMailboxesScreenProps (+7 more)

### Community 130 - "DkimKey"
Cohesion: 0.40
Nodes (5): DkimKey, DomainName, PrivateKeyPem, PublicKeyPem, Selector

### Community 132 - "AppDbContext"
Cohesion: 0.03
Nodes (58): AppDbContext, Aliases, ApiKeys, ApplicationPasswords, Attachments, AuditLogs, BackupHistories, BackupJobs (+50 more)

### Community 133 - "bg"
Cohesion: 0.30
Nodes (15): bg(), Bn(), cc(), cs(), dc(), ed(), jg(), Jn() (+7 more)

### Community 134 - "MailboxArchiveService.cs"
Cohesion: 0.60
Nodes (3): MailboxArchiveAttachment, MailboxArchiveManifest, MailboxArchiveMessage

### Community 135 - "SieveRulesView.tsx"
Cohesion: 0.12
Nodes (19): FLAG_COLORS, FLAG_LABELS, FlagAlertConfig, FlagColor, SieveRulesView(), SieveRulesViewProps, WORK_DAY_OPTIONS, DEFAULT_WORK_DAYS (+11 more)

### Community 136 - "ImapAuthenticatorTests"
Cohesion: 0.19
Nodes (6): Argon2idHasherOptions, Argon2idPasswordHasher, CurrentOptions, ImapAuthenticator, ImapAuthenticatorTests, SeedResult

### Community 138 - "MigrationBuilder"
Cohesion: 0.04
Nodes (30): InitialCreate, AddCatalogFields, AddAuditLogColumns, AddSmtpQueueColumns, AddMailAndSpamColumns, AddMembershipAndRolePermissionFks, AddUserAndDomainFields, AddBackupJobColumns (+22 more)

### Community 139 - "EntityBase"
Cohesion: 0.50
Nodes (4): EntityBase, CreatedAt, Id, UpdatedAt

### Community 140 - ".DeleteMailboxAsync"
Cohesion: 0.16
Nodes (5): DeleteMailboxRequest, DeleteMailboxResult, MailboxArchiveDto, IMailboxArchiveService, MailboxAdminController

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
Cohesion: 0.22
Nodes (3): Program, CloudflareTransportTests, TenantScope

### Community 155 - "Miautrix.Mail.Cli.csproj"
Cohesion: 0.20
Nodes (8): Microsoft.Extensions.Configuration (8.0.0), Microsoft.Extensions.Configuration.EnvironmentVariables (8.0.0), Microsoft.Extensions.DependencyInjection (8.0.1), Spectre.Console (0.55.0), Spectre.Console.Cli (0.55.0), Microsoft.NET.Sdk, Microsoft.EntityFrameworkCore (8.0.10), Microsoft.NET.Sdk

### Community 156 - "SpamVerdict"
Cohesion: 0.17
Nodes (12): SpamVerdict, DkimResult, DmarcResult, DnsblListed, Greylisted, IsSpam, ReasonsJson, Recipient (+4 more)

### Community 159 - "Future implementation items"
Cohesion: 0.18
Nodes (9): DANE / DNSSEC validation, DKIM key rotation, Future implementation items, Implemented now, Security Roadmap, TLS policy controls, LockoutOptions, LockoutDuration (+1 more)

### Community 161 - ".RunOnePassAsync"
Cohesion: 0.33
Nodes (7): OutboundQueueDispatcherTests, RecordingTransport, Mode, Result, Sent, ThrowingTransport, Mode

### Community 162 - "Miautrix.Mail.EndToEndTests.csproj"
Cohesion: 0.33
Nodes (5): coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 167 - "ISessionManager"
Cohesion: 0.14
Nodes (11): AuthToken, ISessionManager, DefaultRefreshLifetime, DefaultSessionLifetime, SessionManager, DefaultRefreshLifetime, DefaultSessionLifetime, ExponentialBackoffWithJitterRetryPolicy (+3 more)

### Community 181 - "IReadOnlyList"
Cohesion: 0.05
Nodes (21): IMailboxService, IMessageService, MailboxService, AttachmentDownloadDto, AttachmentDto, AttachmentUploadInput, CreateFolderRequest, DraftMessageRequest (+13 more)

### Community 184 - "client.ts"
Cohesion: 0.12
Nodes (25): DomainsScreenProps, LicensingScreen(), LicensingScreenProps, SystemScreen(), SystemScreenProps, ApiMeta, AssignMailboxRequest, BackupItem (+17 more)

### Community 186 - "Miautrix.Mail.Persistence.csproj"
Cohesion: 0.25
Nodes (6): Microsoft.EntityFrameworkCore (8.0.10), Microsoft.EntityFrameworkCore.Design (8.0.10), Npgsql.EntityFrameworkCore.PostgreSQL (8.0.10), Microsoft.NET.Sdk, Npgsql.EntityFrameworkCore.PostgreSQL (8.0.10), Microsoft.NET.Sdk

### Community 189 - "Miautrix.Mail.IntegrationTests.csproj"
Cohesion: 0.17
Nodes (10): Microsoft.AspNetCore.Mvc.Testing (8.0.10), Microsoft.EntityFrameworkCore.InMemory (8.0.10), Microsoft.EntityFrameworkCore (8.0.10), Microsoft.NET.Sdk, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), Npgsql.EntityFrameworkCore.PostgreSQL (8.0.10), xunit (2.9.3) (+2 more)

### Community 193 - "IMailStorage"
Cohesion: 0.19
Nodes (10): AntiMalwareOptions, AntiMalwareScanResult, AttachmentPolicy, ClamAvScanner, IAntiMalwareScanner, IQuarantineService, FileSystemMailStorage, IMailStorage (+2 more)

### Community 196 - "SimulateSampleMessage"
Cohesion: 0.12
Nodes (15): RetryQueueItemRequest, Reason, SetAlertConfigRequest, AlertConfigurationJson, SetFlagRequest, Color, SimulateRuleRequest, SampleMessage (+7 more)

### Community 198 - "Miautrix.Mail.Worker.csproj"
Cohesion: 0.15
Nodes (12): Microsoft.Extensions.Hosting (8.0.0), Microsoft.Extensions.Logging.Console (8.0.0), Microsoft.NET.Sdk.Worker, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.Extensions.Http (8.0.0), coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1) (+4 more)

### Community 200 - "Iris Pay"
Cohesion: 0.15
Nodes (13): 1. Atmosphere, 3. Typography, 4. Buttons, 5. Cards, 6. Charts, 7. Spacing, 8. Depth & elevation, 9. Do's & don'ts (+5 more)

### Community 201 - "AntiMalwareScreen.tsx"
Cohesion: 0.16
Nodes (10): AntiMalwareScreen(), AntiMalwareScreenProps, formatBytes(), makeClient(), QuarantineScreen(), QuarantineScreenProps, AntiMalwareStatus, QuarantineItem (+2 more)

### Community 204 - "Pro tokens"
Cohesion: 0.18
Nodes (11): Accessibility (WCAG 2.1), Button, Card, Content, Density, Elevation, Input, Motion (+3 more)

### Community 205 - "Tokens"
Cohesion: 0.17
Nodes (12): Borders, Buttons, Charts, Colors, Ghost, Outline, Primary, Radius (+4 more)

### Community 213 - "lxc-prepare-storage.sh"
Cohesion: 0.60
Nodes (4): count_blobs(), current_env_dir(), effective_dir(), lxc-prepare-storage.sh script

### Community 221 - "update-prod-database.sh"
Cohesion: 0.50
Nodes (3): ASPNETCORE_ENVIRONMENT, MIAUTRIX_DB_CONNECTION, update-prod-database.sh script

### Community 223 - "Miautrix.Mail.Application.csproj"
Cohesion: 0.19
Nodes (9): DnsClient (1.8.0), Microsoft.AspNetCore.OpenApi (8.0.10), Microsoft.OpenApi (2.7.5), Microsoft.NET.Sdk.Web, Microsoft.Extensions.Http (8.0.0), Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk (+1 more)

## Knowledge Gaps
- **485 isolated node(s):** `react`, `react-dom`, `@types/react`, `@types/react-dom`, `@vitejs/plugin-react` (+480 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1259 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **56 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CloudflareApiMailTransport` connect `CloudflareApiMailTransport` to `IOutboundMailTransport.cs`, `InboundController`, `ISessionManager`, `system_text`, `.Main`, `11.2 Component Remediation & Verification Findings`?**
  _High betweenness centrality (0.311) - this node is a cross-community bridge._
- **Are the 29 inferred relationships involving `dN()` (e.g. with `_1()` and `Am()`) actually correct?**
  _`dN()` has 29 INFERRED edges - model-reasoned connections that need verification._
- **What connects `react`, `react-dom`, `@types/react` to the rest of the system?**
  _485 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task` be split into smaller, more focused modules?**
  _Cohesion score 0.03203806502775575 - nodes in this community are weakly interconnected._
- **Why does `dN()` connect `dN` to `i`, `index-SHp2FWM5.js`, `bg`, `Xe`, `A`, `n`, `index-7_LIhLyZ.js`, `o1`, `qg`, `Es`, `Dt`, `Fd`, `c`, `n`, `o`, `bb`, `11.2 Component Remediation & Verification Findings`?**
  _High betweenness centrality (0.277) - this node is a cross-community bridge._
- **Are the 8 inferred relationships involving `AppDbContext` (e.g. with `Per-domain settings pattern` and `Where things are`) actually correct?**
  _`AppDbContext` has 8 INFERRED edges - model-reasoned connections that need verification._
- **Should `Miautrix.Mail.Persistence` be split into smaller, more focused modules?**
  _Cohesion score 0.06074561403508772 - nodes in this community are weakly interconnected._