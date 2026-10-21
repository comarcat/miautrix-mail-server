# Graph Report - miautrix-mail-server  (2026-10-20)

## Corpus Check
- 419 files · ~4,791,501 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 627 file(s) not represented in the graph (top: .dll 552, .pdb 26, .so 26)

## Summary
- 5029 nodes · 16671 edges · 204 communities (154 shown, 50 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 1917 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7b0690e0`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Task
- Miautrix.Mail.Persistence
- index-DpCImcBM.js
- ol
- webmail/src/App.tsx
- ILogger
- InboundController
- n
- system_text
- QuarantineService
- index-7_LIhLyZ.js
- admin/src/App.tsx
- AI Build Instructions
- List
- InboxView.tsx
- system
- MailboxAdminApiTests
- t
- ITenantAuthorizationHelper
- DkimService
- InMemorySecurityEventSink
- dN
- ff
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
- tc
- i
- Components
- compilerOptions
- AuthService
- o
- CancellationToken
- MimeMessageBuilder
- ew
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
- Miautrix Mail Server
- Mailbox
- SieveRuleService
- ComposerView.tsx
- QueueScreen.tsx
- ad
- Tenant
- .Main
- AGENTS.md — Miautrix Mail Server
- bb
- o1
- webmail/src/types.ts
- Miautrix.Mail.sln
- ResourceNotFoundException
- qg
- Miautrix Mail Server
- rg
- Miautrix Mail Server
- SmtpDeliveryAttempt
- Dt
- CloudflareApiMailTransport
- PulsePoint
- QuarantineItem
- 5. Recommended Technology Stack
- package.json
- SmtpQueueManager
- Domain
- Ue
- ImapSession
- Change log — MIA-76, `POST /send` contract fix
- 11.2 Component Remediation & Verification Findings
- IOutboundMailTransport.cs
- Miautrix Mail Server — Errors, Issues & Review Log
- CalendarSubscriptionApiTests
- MailFlowRule
- ITotpService
- MailboxArchiveService
- Epic 3 — Surfaces
- Epic 4 — Operations
- Rule: secrets and logging
- Rule: secrets and logging
- 3. Core Architectural Principles
- 9. Identity Architecture
- Change log — MIA-77, Inbound `email()` Wildcard Allow-list & Webhook Destination Remediation
- Calendar & Webmail Final Solution — 2026-10-03
- Epic 2 — Mail transport and policy
- AGENTS.md — Miautrix Mail Server
- BackupJob
- Contact
- 2. Palette
- AuthStatus
- OutboundQueueDispatcher
- Miautrix Mail Server
- Argon2idPasswordHasher
- .ProcessItemAsync
- README.md
- SieveFilterRule
- RequestDtos.cs
- CloudflareEmailOptions
- og
- Project Update Checklist
- CalendarView.tsx
- UsersScreen.tsx
- Section 19 — Verify commands
- format-status.js
- AppDbContext
- bg
- Section 4 — Stack
- SieveRulesView.tsx
- ImapAuthenticatorTests
- cloudflare-outbound-delivery-restored-2026-10-06.md
- MigrationBuilder
- Section 5 — Data model
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
- LoginStatus
- ImapState
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
- IUserService.cs
- IReadOnlyList
- client.ts
- AntiMalware.cs
- WebmailApiClient
- SimulateSampleMessage
- Iris Pay
- AntiMalwareScreen.tsx
- Pro tokens
- Tokens
- lxc-prepare-storage.sh
- update-prod-database.sh
- inject-flow-mail-test.sh
- lxc-install-antimalware.sh
- lxc-install-worker-env.sh
- update-database.sh
- update-test-database.sh
- inject-eicar-test.sh
- lxc-restart-services.sh
- lxc-update-antimalware-signatures.sh

## God Nodes (most connected - your core abstractions)
1. `dN()` - 408 edges
2. `AppDbContext` - 191 edges
3. `o()` - 105 edges
4. `AdminApiClient` - 97 edges
5. `AdminService` - 89 edges
6. `Miautrix.Mail.Persistence` - 89 edges
7. `i()` - 82 edges
8. `ApiResponse` - 82 edges
9. `WebmailApiClient` - 70 edges
10. `n()` - 64 edges

## Surprising Connections (you probably didn't know these)
- `Boundary: Cloudflare is transport only` --references--> `ImapListenerService`  [INFERRED]
  ERRORS_AND_ISSUES.md → src/Miautrix.Mail.Worker/ImapListenerService.cs
- `10.1 Issue Summary & Root Cause` --references--> `ISessionManager`  [INFERRED]
  ERRORS_AND_ISSUES.md → src/Miautrix.Mail.Identity/SessionManager.cs
- `Section 8 — Data layer and storage` --references--> `IMailStorage`  [INFERRED]
  blueprints/miautrix-mail-server/blueprint.md → src/Miautrix.Mail.Storage/MailStorage.cs
- `T10 — IMAP, storage, and attachments` --references--> `IMailStorage`  [INFERRED]
  blueprints/miautrix-mail-server/epics/02-mail-transport.md → src/Miautrix.Mail.Storage/MailStorage.cs
- `Evaluation of Options:` --references--> `InboundController`  [INFERRED]
  workers/email/CHANGELOG-MIA-77.md → src/Miautrix.Mail.Web/Controllers/InboundController.cs

## Import Cycles
- None detected.

## Communities (204 total, 50 thin omitted)

### Community 0 - "Task"
Cohesion: 0.04
Nodes (38): Per-domain settings pattern, Per-domain settings pattern, AdminUserDto, AntiMalwareSettingsDto, AntiSpamSettingsDto, AssignMailboxRequest, AuditFilter, AuditLogDto (+30 more)

### Community 1 - "Miautrix.Mail.Persistence"
Cohesion: 0.05
Nodes (36): Miautrix.Mail.IntegrationTests, Miautrix.Mail.Protocols.Imap, Miautrix.Mail.IntegrationTests.MailFlow, Miautrix.Mail.Infrastructure.MailboxArchiving, Miautrix.Mail.IntegrationTests.Licensing, Miautrix.Mail.ProtocolTests, Miautrix.Mail.IntegrationTests.Audit, Miautrix.Mail.Cli.Commands (+28 more)

### Community 2 - "index-DpCImcBM.js"
Cohesion: 0.04
Nodes (109): acceptRescheduleProposal(), addSubscription(), Ar(), Bf(), bo(), br(), calendarEventBody(), ch() (+101 more)

### Community 3 - "ol"
Cohesion: 0.07
Nodes (64): Ao(), aa(), ac(), al(), am(), Ba(), bc(), be() (+56 more)

### Community 4 - "webmail/src/App.tsx"
Cohesion: 0.16
Nodes (13): App(), folderIconMap, mapFolders(), normalizeMailboxAccount(), ChangePasswordView(), ChangePasswordViewProps, ComposeInitialState, ContactsView() (+5 more)

### Community 5 - "ILogger"
Cohesion: 0.32
Nodes (4): IdempotencyMiddleware, CapturedResponse, IIdempotencyStore, InMemoryIdempotencyStore

### Community 6 - "InboundController"
Cohesion: 0.11
Nodes (16): 12.10 Defect: Inbound Messages Are Enqueued Into the Outbound Send Queue, 12.11 Corrected Root Cause: Unsynchronised Race Between Two Queue Dispatchers, 12.12 Resolved: Cloudflare Access Interception and False Outbound Delivery Success (MIA-92), 12.1 Objective & Governance, 12.2 Cloudflare Email Routing Configuration, 12.3 Verification Matrix & Live Evidence, 12.4 Rollback Procedure, 12.5 Defect: Inbound Allow-List Rejected All External Senders (SMTP 555 5.7.1) (+8 more)

### Community 7 - "n"
Cohesion: 0.10
Nodes (56): A(), Ae(), af(), Ai(), b(), Ci(), createHTML(), createScriptURL() (+48 more)

### Community 8 - "system_text"
Cohesion: 0.15
Nodes (7): Miautrix.Mail.Web, Miautrix.Mail.UnitTests.Calendar, Miautrix.Mail.IntegrationTests.Api, Miautrix.Mail.IntegrationTests.Calendar, Miautrix.Mail.SecurityTests.Identity, Miautrix.Mail.IntegrationTests.Cloudflare, Miautrix.Mail.Application.Mail.Mime

### Community 9 - "QuarantineService"
Cohesion: 0.17
Nodes (9): Where things are, Where things are, IQuarantineService, QuarantineService, InboundMailContext, InboundProcessResult, RuleMatch, RuleBasedSpamProvider (+1 more)

### Community 10 - "index-7_LIhLyZ.js"
Cohesion: 0.02
Nodes (155): _2(), $a(), a0(), a2(), Af(), AN(), av(), bj() (+147 more)

### Community 11 - "admin/src/App.tsx"
Cohesion: 0.06
Nodes (38): apiClient, App(), AntiSpamScreen(), AntiSpamScreenProps, makeClient(), BackupScreen(), BackupScreenProps, ChangePasswordView() (+30 more)

### Community 12 - "AI Build Instructions"
Cohesion: 0.20
Nodes (10): 1 · Your role, 2 · Token compliance, 3 · Component recipes, 4 · Hard constraints, 5 · Before you finish — verify, AI Build Instructions, Buttons, Cards (+2 more)

### Community 13 - "List"
Cohesion: 0.22
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

### Community 17 - "t"
Cohesion: 0.06
Nodes (47): an(), Bp(), Bt(), cm(), Dn(), em(), en(), ep() (+39 more)

### Community 18 - "ITenantAuthorizationHelper"
Cohesion: 0.13
Nodes (6): EfPermissionRepository, IPermissionRepository, LastOwnerDemotionException, TenantAuthorizationHelper, IsolationTests, MockPermissionRepo

### Community 19 - "DkimService"
Cohesion: 0.14
Nodes (5): DkimKeyPair, DkimService, DnsAuthHelper, IDkimService, DkimTests

### Community 20 - "InMemorySecurityEventSink"
Cohesion: 0.26
Nodes (9): IPasswordHasher, AuthenticationService, IAuthenticationService, InMemorySecurityEventSink, ISecurityEventSink, LoginResult, SecurityEventRecord, UserAccount (+1 more)

### Community 21 - "dN"
Cohesion: 0.03
Nodes (81): dN(), _1(), ac(), ah(), Am(), ap(), Au(), ax() (+73 more)

### Community 22 - "ff"
Cohesion: 0.08
Nodes (49): Au(), bl(), bm(), bu(), Cp(), di(), du(), Eu() (+41 more)

### Community 23 - "Miautrix Mail Server"
Cohesion: 0.08
Nodes (24): 10. Authentication and Authorization, 11. MFA, 12. Security Event Codes, 15. Graphical Mail Flow Designer, 16. Mail Flow Simulator, 17. Mail Flow Explorer, 18. Restricted Device Relay, 19. Anti-Spam Architecture (+16 more)

### Community 24 - "AdminApiClient"
Cohesion: 0.09
Nodes (7): AdminApiClient, normalizeGuid(), AntiMalwareSettings, AntiSpamSettings, ApiResponse, SecuritySettings, SharedMailbox

### Community 25 - "CalendarService"
Cohesion: 0.05
Nodes (34): CalendarService, NormalizedInvitee, ICalendarService, AvailabilityCompareDto, AvailabilityCompareRequest, CalendarAttendeeDto, CalendarAvailabilityDto, CalendarBusyBlockDto (+26 more)

### Community 26 - "compilerOptions"
Cohesion: 0.10
Nodes (19): compilerOptions, allowArbitraryExtensions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection (+11 more)

### Community 27 - "TenantScopedEntityBase"
Cohesion: 0.05
Nodes (75): Alias, Address, TargetAddress, ApiKey, ApplicationPassword, BackupHistory, CalendarSubscription, TargetUserId (+67 more)

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
Cohesion: 0.06
Nodes (33): AuditLog, Action, ActorId, DetailsJson, IpAddress, TargetId, TargetType, Folder (+25 more)

### Community 32 - "Attachment"
Cohesion: 0.29
Nodes (7): Attachment, ContentHash, ContentType, FileName, MessageId, SizeBytes, StoragePath

### Community 33 - "SieveScript"
Cohesion: 0.29
Nodes (3): SieveParser, SieveParseResult, SieveTests

### Community 34 - ".TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData"
Cohesion: 0.35
Nodes (3): ILicenseQuotaEnforcer, LicenseQuotaEnforcer, LicenseQuotaException

### Community 35 - ".StageAndSwitchAsync"
Cohesion: 0.10
Nodes (8): Miautrix.Mail.Infrastructure.Deployment, Miautrix.Mail.IntegrationTests.BlueGreen, TypeRegistrar, TypeResolver, DeploymentManager, IDeploymentManager, BlueGreenUpdateTests, SearchTests

### Community 36 - "ISmtpQueueManager"
Cohesion: 0.12
Nodes (16): SmtpDomainValidator, ISmtpInboundHandler, SmtpInboundHandler, ISmtpAuthenticator, ISmtpDomainValidator, SmtpResponse, IsSuccess, ISmtpSubmissionHandler (+8 more)

### Community 37 - "Miautrix Mail Server — Implementation Blueprint"
Cohesion: 0.07
Nodes (36): Provider seams, 2.1 In scope for v1, 2.2 Out of scope for v1, 2.3 Non-goals, A correction to the record, Epic 1 — Core platform (T1–T6), Epic 2 — Mail transport and policy (T7–T13), Epic 3 — Surfaces (T14–T17) (+28 more)

### Community 38 - "compilerOptions"
Cohesion: 0.12
Nodes (16): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, noEmit, noFallthroughCasesInSwitch (+8 more)

### Community 39 - "A"
Cohesion: 0.08
Nodes (83): As(), bC(), cN(), k(), T(), Dl(), e1(), j() (+75 more)

### Community 40 - "SmtpQueueItem"
Cohesion: 0.14
Nodes (14): QueueDirection, Inbound, Outbound, SmtpQueueItem, Attempts, Direction, LastAttemptAt, LastError (+6 more)

### Community 41 - "tc"
Cohesion: 0.06
Nodes (48): Eh(), at(), bh(), bi(), Cc(), co(), constructor(), ct() (+40 more)

### Community 42 - "i"
Cohesion: 0.10
Nodes (45): Ad(), as(), bs(), cs(), ds(), es(), fs(), gs() (+37 more)

### Community 43 - "Components"
Cohesion: 0.12
Nodes (16): Buttons, Cards, Checkboxes, Chips, Components, Default Item, Disabled State, Filter Chip (+8 more)

### Community 44 - "compilerOptions"
Cohesion: 0.12
Nodes (16): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+8 more)

### Community 45 - "AuthService"
Cohesion: 0.24
Nodes (5): AuthResult, AuthUserDto, ChangePasswordResult, AuthService, IAuthService

### Community 46 - "o"
Cohesion: 0.10
Nodes (40): ah(), ap(), Bd(), cf(), ef(), eh(), et(), Fd() (+32 more)

### Community 47 - "CancellationToken"
Cohesion: 0.07
Nodes (24): ApiResponse, PaginationMeta, ApiError, AttachmentController, AuditController, AuthController, CalendarController, DomainController (+16 more)

### Community 48 - "MimeMessageBuilder"
Cohesion: 0.08
Nodes (6): MimeAttachment, MimeEncoders, BuiltMimeMessage, MimeMessageBuilder, MimeMessageRequest, MimeMessageBuilderTests

### Community 49 - "ew"
Cohesion: 0.08
Nodes (39): Al(), ar(), aw(), d0(), Df(), dw(), E0(), ew() (+31 more)

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
Cohesion: 0.13
Nodes (8): CalendarInvitationOptions, PublicBaseUrl, BackupOptions, ApiExceptionMiddleware, HeaderRequestContextAccessor, CurrentTenantId, CurrentUserId, RequestIdMiddleware

### Community 56 - "Guid"
Cohesion: 0.04
Nodes (23): AntiMalwareStatusDto, BackupJobDto, OrphanMailboxDto, QuarantineFilter, QuarantineItemDto, TenantDto, IMailQueueService, MailQueueService (+15 more)

### Community 57 - "c"
Cohesion: 0.09
Nodes (66): d(), aC(), aE(), Bw(), f(), h(), c(), i() (+58 more)

### Community 58 - "n"
Cohesion: 0.10
Nodes (49): b1(), bm(), Bt(), bu(), cm(), Cu(), dm(), eg() (+41 more)

### Community 59 - "index.ts"
Cohesion: 0.11
Nodes (21): authorize(), decodeBase64(), email(), fetch(), handleSend(), isSenderAllowed(), jsonError(), looksLikeAddress() (+13 more)

### Community 60 - "o"
Cohesion: 0.12
Nodes (26): A(), A1(), at(), cg(), Ct(), Dn(), du(), em() (+18 more)

### Community 61 - "Zc"
Cohesion: 0.07
Nodes (8): bv(), dC, Ga(), HS(), RS(), Tx(), yN, Zc

### Community 62 - "CloudflareApiMailTransport"
Cohesion: 0.12
Nodes (15): Deploying, `email()` inbound handler contract (MIA-77), Miautrix email Worker (`miautrix-main-worker`), Platform coverage, `POST /send` contract, Required bindings & variables, Rollback point, Rolling back (+7 more)

### Community 63 - "plugins"
Cohesion: 0.33
Nodes (5): plugins, rules, react/only-export-components, react/rules-of-hooks, $schema

### Community 64 - "i"
Cohesion: 0.08
Nodes (33): Ay(), a(), b2(), c2(), cj(), Ds(), e2(), fN() (+25 more)

### Community 65 - "Miautrix Mail Server"
Cohesion: 0.20
Nodes (10): 1.1 Project Purpose and Business Case, 1.2 High-Level Objectives, 1. Project Initiation & Executive Summary, 2. Project Scope Management (WBS Overview), 3. Project Schedule & Milestone Management, 5. Risk Management Matrix, 6. Stakeholder & Resource Plan, Active Execution Phase: Production Integration & Functional Delivery (+2 more)

### Community 66 - "Mailbox"
Cohesion: 0.15
Nodes (12): Mailbox, Address, Department, DomainId, IsActive, Kind, Name, Organization (+4 more)

### Community 67 - "SieveRuleService"
Cohesion: 0.22
Nodes (5): ISieveRuleService, SieveRuleService, SieveFilterRuleDto, SieveFilterRuleRequest, SieveScriptService

### Community 68 - "ComposerView.tsx"
Cohesion: 0.13
Nodes (21): ComposerAccount, ComposerView(), ComposerViewProps, escapeHtml(), htmlToText(), insertSignatureBeforeQuote(), signatureHtml(), stripSignature() (+13 more)

### Community 69 - "QueueScreen.tsx"
Cohesion: 0.31
Nodes (4): QueueScreen(), QueueScreenProps, QueueItem, QueueQueryParams

### Community 70 - "ad"
Cohesion: 0.13
Nodes (23): ab(), ad(), Ar(), bh(), _c(), Ca(), ci(), cr() (+15 more)

### Community 71 - "Tenant"
Cohesion: 0.07
Nodes (33): 4. Quality Management & Verification Gates, 🔒 Security Invariants, EntityBase, CreatedAt, Id, UpdatedAt, Membership, RoleId (+25 more)

### Community 72 - ".Main"
Cohesion: 0.06
Nodes (42): BackupCommand, BackupSettings, OutputPath, DomainListCommand, DomainListSettings, Tenant, UserId, MailboxCreateCommand (+34 more)

### Community 73 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Admin screens, AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Scripts, Spam pipeline, Task tracking, Things that will bite you

### Community 74 - "bb"
Cohesion: 0.19
Nodes (18): bb(), Fe(), fp(), gg(), hp(), ia(), it(), Lb() (+10 more)

### Community 75 - "o1"
Cohesion: 0.13
Nodes (39): b(), bd(), Br(), dp(), ea(), ep(), eu(), g1() (+31 more)

### Community 76 - "webmail/src/types.ts"
Cohesion: 0.11
Nodes (12): normalizeEmailAttachment(), WebmailLoginResult, AvailabilityCompare, CalendarAttendee, CalendarAvailability, CalendarConflict, DirectoryParticipant, EmailAttachment (+4 more)

### Community 77 - "Miautrix.Mail.sln"
Cohesion: 0.04
Nodes (75): Miautrix.Mail.Application.Transport, Miautrix.Mail.Worker, DnsClient (1.8.0), Konscious.Security.Cryptography.Argon2 (1.3.1), Microsoft.AspNetCore.Mvc.Testing (8.0.10), Microsoft.AspNetCore.OpenApi (8.0.10), Microsoft.EntityFrameworkCore.InMemory (8.0.10), Microsoft.Extensions.Configuration (8.0.0) (+67 more)

### Community 78 - "ResourceNotFoundException"
Cohesion: 0.23
Nodes (5): ContactService, IContactService, ContactDto, ContactRequest, ContactsController

### Community 79 - "qg"
Cohesion: 0.22
Nodes (15): Ba(), Eo(), Gb(), gd(), Kb(), pd(), Ph(), ps() (+7 more)

### Community 80 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Architecture rules, Commands, Data rules, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 81 - "rg"
Cohesion: 0.08
Nodes (47): bp(), C1(), Da(), dg(), el(), Es(), fu(), hc() (+39 more)

### Community 82 - "Miautrix Mail Server"
Cohesion: 0.22
Nodes (8): Architecture rules, Commands, Data rules, Discovery workflow, Licensing rules, Miautrix Mail Server, Security rules — these are not preferences, Style

### Community 83 - "SmtpDeliveryAttempt"
Cohesion: 0.25
Nodes (8): SmtpDeliveryAttempt, AttemptedAt, AttemptNumber, ErrorMessage, NextRetryDelaySeconds, QueueItemId, ResponseCode, Success

### Community 84 - "Dt"
Cohesion: 0.14
Nodes (28): cd(), d1(), Do(), Dt(), h(), hn(), ih(), js() (+20 more)

### Community 85 - "CloudflareApiMailTransport"
Cohesion: 0.14
Nodes (14): 12.9 Open: Cloudflare `env.EMAIL.send()` Rejects the Outbound Message (HTTP 502 `send_failed`), 7. Cloudflare Workers Integration, Boundary: Cloudflare is transport only, Deployment, Flows, Inbound `email()` wildcard allow-list & webhook forwarding remediated (MIA-77, 2026-10-04), Outstanding, `POST /send` fixed: 400 on a bad body, 401 on a bad token, no 1101 (MIA-76, 2026-10-04) (+6 more)

### Community 86 - "PulsePoint"
Cohesion: 0.25
Nodes (8): Border Radius, Colors, Do's and Don'ts, Elevation, Overview, PulsePoint, Spacing, Typography

### Community 87 - "QuarantineItem"
Cohesion: 0.15
Nodes (13): QuarantineItem, IsDelivered, QuarantinedAt, RawMessage, ReasonsJson, Recipient, ReleasedAt, Sender (+5 more)

### Community 88 - "5. Recommended Technology Stack"
Cohesion: 0.25
Nodes (8): 5. Recommended Technology Stack, Backend, Caching, Database, Frontend, Logging, Scheduling, Storage

### Community 89 - "package.json"
Cohesion: 0.25
Nodes (7): name, private, scripts, build, dev, test, version

### Community 90 - "SmtpQueueManager"
Cohesion: 0.27
Nodes (4): ExponentialBackoffWithJitterRetryPolicy, IRetryPolicy, SmtpQueueManager, SmtpQueueDatabaseTests

### Community 91 - "Domain"
Cohesion: 0.04
Nodes (45): Epic 1 — Core platform, T1 — Solution scaffold and CI, T2 — Domain model and EF Core schema, T3 — Seed data and indexes, T4 — Identity, T5 — Authorization and tenant isolation, T6 — Audit trail, Also check (+37 more)

### Community 92 - "Ue"
Cohesion: 0.06
Nodes (54): Ax(), b_(), b0(), BE(), bo, cE(), d_(), ev() (+46 more)

### Community 93 - "ImapSession"
Cohesion: 0.12
Nodes (15): 6. Transport / Protocol Listener Issues, ImapCommandResult, ImapSession, MailboxId, SelectedFolder, State, TenantId, Username (+7 more)

### Community 94 - "Change log — MIA-76, `POST /send` contract fix"
Cohesion: 0.11
Nodes (17): 10. Reconciliation and Verification (Path 3), 1.1 The live deployment, read back, 1.2 Secrets bound to the script, 1.3 The unchanged public paths still answer, 1. Baseline — state before any change, 2. Build the change, 3.1 Setting it on the Worker — REFUSED, 3. The `SEND_TOKEN` secret (+9 more)

### Community 95 - "11.2 Component Remediation & Verification Findings"
Cohesion: 0.17
Nodes (13): 11.1 Context & Scope, 11.2 Component Remediation & Verification Findings, 11.3 Comprehensive Pass/Fail Matrix, 11.4 Residual Risks & Ongoing Controls, 11. External Email Flow Remediation, DNS/DKIM Validation, and End-to-End Verification (MIA-90), 1. Worker Source Recovery and Version Control, 2. Worker `POST /send` 1101 Elimination & Outbound Contract, 3. Inbound Email Routing & Worker `email()` Handler (+5 more)

### Community 96 - "IOutboundMailTransport.cs"
Cohesion: 0.32
Nodes (6): OutboundMessage, TransportSendResult, RecordingTransport, Mode, Result, Sent

### Community 97 - "Miautrix Mail Server — Errors, Issues & Review Log"
Cohesion: 0.12
Nodes (14): 10.1 Issue Summary & Root Cause, 10.2 Complete Request Paths Inventory, 10.3 Active Cloudflare Rule Expression & Verification, 10.4 Validation & Rollback Guidance, 10. Cloudflare API Containment Rule Diagnosis & Frontend Auth Analysis (MIA-88), 1. System & OS Environment Issues, 2. Frontend & UI Runtime Issues, 3. NGINX & Ingress Routing Issues (+6 more)

### Community 99 - "MailFlowRule"
Cohesion: 0.25
Nodes (8): Quarantine item lifecycle, Quarantine item lifecycle, MailFlowRule, ActionsJson, ConditionsJson, IsEnabled, Name, Priority

### Community 101 - "MailboxArchiveService"
Cohesion: 0.25
Nodes (5): MailboxArchiveAttachment, MailboxArchiveManifest, MailboxArchiveMessage, MailboxArchiveService, ApiJson

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
Cohesion: 0.40
Nodes (5): 3.1 Security First, 3.3 API First, 3.4 Configuration as Data, 3.5 Provider Abstraction, 3. Core Architectural Principles

### Community 107 - "9. Identity Architecture"
Cohesion: 0.33
Nodes (6): 9.1 Local Accounts, 9.2 Google Workspace, 9.3 Microsoft Entra ID, 9.4 LDAP / Active Directory, 9.5 Recommended Groups, 9. Identity Architecture

### Community 108 - "Change log — MIA-77, Inbound `email()` Wildcard Allow-list & Webhook Destination Remediation"
Cohesion: 0.15
Nodes (12): 1. Baseline & Identified Defects, 2. Remediated Handler Contract, 3. Configuration in `wrangler.jsonc`, 4.1 Inbound Email Contract Suite (`verify-inbound-contract.sh`), 4.2 Outbound `POST /send` and HTTP Endpoint Regression Verification (`verify-send-contract.sh`), 4.3 Live Production Probes (Read-Only Verification), 4. Verification, 5. Rollback Target & Command (+4 more)

### Community 109 - "Calendar & Webmail Final Solution — 2026-10-03"
Cohesion: 0.29
Nodes (7): Calendar & Webmail Final Solution — 2026-10-03, Implementation Notes, Issues Closed, Manual Post-Deploy Checklist, Status, Verification Completed, Version 1.0 Closure

### Community 110 - "Epic 2 — Mail transport and policy"
Cohesion: 0.25
Nodes (7): Epic 2 — Mail transport and policy, T10 — IMAP, storage, and attachments, T11 — ManageSieve, T13 — Rule engine, simulator, explorer, T7 — SMTP listener and queue, T8 — SPF, DKIM, DMARC, T9 — Anti-spam baseline and quarantine

### Community 112 - "AGENTS.md — Miautrix Mail Server"
Cohesion: 0.25
Nodes (7): Admin screens, AGENTS.md — Miautrix Mail Server, Before you claim a task is done, Scripts, Spam pipeline, Task tracking, Things that will bite you

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

### Community 117 - "OutboundQueueDispatcher"
Cohesion: 0.39
Nodes (4): Implementation notes, IOutboundMailTransport, Mode, OutboundQueueDispatcher

### Community 118 - "Miautrix Mail Server"
Cohesion: 0.25
Nodes (8): 📐 Architecture & Design, Build & Test Commands, 🛠️ Deploying & Updating, ⚙️ Development Environment, Miautrix Mail Server, ✉️ Outbound transport model, 🚀 Project Status, 🛠️ Tech Stack & Prerequisites

### Community 119 - "Argon2idPasswordHasher"
Cohesion: 0.38
Nodes (3): Argon2idHasherOptions, Argon2idPasswordHasher, CurrentOptions

### Community 120 - ".ProcessItemAsync"
Cohesion: 0.05
Nodes (29): AntiMalwareOptions, AntiMalwareScanResult, AttachmentPolicy, ClamAvScanner, IAntiMalwareScanner, ImapListenerService, InboundQueueDispatcher, ParsedAttachment (+21 more)

### Community 123 - "RequestDtos.cs"
Cohesion: 0.29
Nodes (6): RetryQueueItemRequest, Reason, SetAlertConfigRequest, AlertConfigurationJson, SetFlagRequest, Color

### Community 124 - "CloudflareEmailOptions"
Cohesion: 0.33
Nodes (6): CloudflareEmailOptions, AccessClientId, AccessClientSecret, ApiBase, ApiToken, IsConfigured

### Community 125 - "og"
Cohesion: 0.09
Nodes (46): Aa(), ag(), ai(), bc(), bi(), di(), ec(), gi() (+38 more)

### Community 126 - "Project Update Checklist"
Cohesion: 0.40
Nodes (4): Always review/update, Project Update Checklist, Recent example: Webmail contacts/address book, Update process

### Community 128 - "CalendarView.tsx"
Cohesion: 0.13
Nodes (19): CalendarEventContextMenu(), CalendarEventContextMenuProps, clamp(), CalendarEventDialog(), CalendarEventDialogProps, CalendarRsvpDialog(), CalendarRsvpDialogProps, CalendarView() (+11 more)

### Community 129 - "UsersScreen.tsx"
Cohesion: 0.20
Nodes (15): AssignMailboxModal(), AssignMailboxModalProps, splitEmail(), MailboxDelegatePicker(), MailboxDelegatePickerProps, formatBytes(), SharedMailboxesScreen(), SharedMailboxesScreenProps (+7 more)

### Community 130 - "Section 19 — Verify commands"
Cohesion: 0.40
Nodes (5): 19.1 Where they run, 19.2 Settings allowlist, 19.3 Toolchain prerequisites, 19.6 Verify-critical configuration, Section 19 — Verify commands

### Community 132 - "AppDbContext"
Cohesion: 0.03
Nodes (60): AppDbContext, Aliases, ApiKeys, ApplicationPasswords, Attachments, AuditLogs, BackupHistories, BackupJobs (+52 more)

### Community 133 - "bg"
Cohesion: 0.33
Nodes (14): bg(), Bn(), cc(), cs(), dc(), ed(), Jn(), nd() (+6 more)

### Community 134 - "Section 4 — Stack"
Cohesion: 0.40
Nodes (5): 4.1 Backend — NuGet, verified 2026-09-16, 4.2 Frontend — npm, verified 2026-09-16, 4.3 Testing — NuGet, verified, 4.4 Runtime — locally confirmed, Section 4 — Stack

### Community 135 - "SieveRulesView.tsx"
Cohesion: 0.12
Nodes (19): FLAG_COLORS, FLAG_LABELS, FlagAlertConfig, FlagColor, SieveRulesView(), SieveRulesViewProps, WORK_DAY_OPTIONS, DEFAULT_WORK_DAYS (+11 more)

### Community 136 - "ImapAuthenticatorTests"
Cohesion: 0.32
Nodes (3): ImapAuthenticator, ImapAuthenticatorTests, SeedResult

### Community 138 - "MigrationBuilder"
Cohesion: 0.03
Nodes (32): Miautrix.Mail.Persistence.Migrations, InitialCreate, AddCatalogFields, AddAuditLogColumns, AddSmtpQueueColumns, AddMailAndSpamColumns, AddMembershipAndRolePermissionFks, AddUserAndDomainFields (+24 more)

### Community 139 - "Section 5 — Data model"
Cohesion: 0.40
Nodes (5): 5.1 Conventions, 5.2 Entities, 5.3 Isolation, 5.4 Migrations, Section 5 — Data model

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

### Community 154 - "LoginStatus"
Cohesion: 0.40
Nodes (5): LoginStatus, Failed, LockedOut, MfaRequired, Success

### Community 155 - "ImapState"
Cohesion: 0.40
Nodes (5): ImapState, Authenticated, LoggedOut, NotAuthenticated, Selected

### Community 156 - "SpamVerdict"
Cohesion: 0.17
Nodes (12): SpamVerdict, DkimResult, DmarcResult, DnsblListed, Greylisted, IsSpam, ReasonsJson, Recipient (+4 more)

### Community 159 - "Future implementation items"
Cohesion: 0.18
Nodes (9): DANE / DNSSEC validation, DKIM key rotation, Future implementation items, Implemented now, Security Roadmap, TLS policy controls, LockoutOptions, LockoutDuration (+1 more)

### Community 161 - ".RunOnePassAsync"
Cohesion: 0.48
Nodes (3): OutboundQueueDispatcherTests, ThrowingTransport, Mode

### Community 162 - "Miautrix.Mail.EndToEndTests.csproj"
Cohesion: 0.33
Nodes (5): coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 167 - "ISessionManager"
Cohesion: 0.28
Nodes (7): AuthToken, ISessionManager, DefaultRefreshLifetime, DefaultSessionLifetime, SessionManager, DefaultRefreshLifetime, DefaultSessionLifetime

### Community 169 - "Fact"
Cohesion: 0.13
Nodes (6): Miautrix.Mail.UnitTests, Miautrix.Mail.EndToEndTests, EndToEndTestProjectSmokeTests, AuthApiTests, MailQueueApiTests, UnitTestProjectSmokeTests

### Community 181 - "IReadOnlyList"
Cohesion: 0.05
Nodes (23): IMailboxService, IMessageService, MailboxService, AttachmentDownloadDto, AttachmentDto, AttachmentUploadInput, CreateFolderRequest, DraftMessageRequest (+15 more)

### Community 184 - "client.ts"
Cohesion: 0.12
Nodes (25): DomainsScreenProps, LicensingScreen(), LicensingScreenProps, SystemScreen(), SystemScreenProps, ApiMeta, AssignMailboxRequest, BackupItem (+17 more)

### Community 196 - "SimulateSampleMessage"
Cohesion: 0.22
Nodes (9): SimulateRuleRequest, SampleMessage, SimulateSampleMessage, HasAttachment, Headers, Recipient, Sender, SpamScore (+1 more)

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

## Knowledge Gaps
- **485 isolated node(s):** `RawHeaders`, `Subject`, `Date`, `BodyText`, `BodyHtml` (+480 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1265 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **50 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `dN()` connect `dN` to `i`, `ol`, `bg`, `ad`, `A`, `tc`, `index-7_LIhLyZ.js`, `o1`, `bb`, `qg`, `rg`, `Dt`, `c`, `n`, `o`, `og`, `11.2 Component Remediation & Verification Findings`?**
  _High betweenness centrality (0.400) - this node is a cross-community bridge._
- **Are the 25 inferred relationships involving `dN()` (e.g. with `_1()` and `Am()`) actually correct?**
  _`dN()` has 25 INFERRED edges - model-reasoned connections that need verification._
- **What connects `RawHeaders`, `Subject`, `Date` to the rest of the system?**
  _485 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task` be split into smaller, more focused modules?**
  _Cohesion score 0.03669064748201439 - nodes in this community are weakly interconnected._
- **Why does `m1()` connect `11.2 Component Remediation & Verification Findings` to `o`, `dN`?**
  _High betweenness centrality (0.359) - this node is a cross-community bridge._
- **Are the 8 inferred relationships involving `AppDbContext` (e.g. with `Per-domain settings pattern` and `Where things are`) actually correct?**
  _`AppDbContext` has 8 INFERRED edges - model-reasoned connections that need verification._
- **Should `Miautrix.Mail.Persistence` be split into smaller, more focused modules?**
  _Cohesion score 0.04887406171809842 - nodes in this community are weakly interconnected._