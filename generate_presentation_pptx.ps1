param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$ReportDir = Join-Path $PWD 'presentation_assets'
$Logo = Join-Path $PWD 'design/assets/miautrix-logo.png'

# Color scheme
$DarkNavy   = [System.Drawing.ColorTranslator]::FromHtml('#0f172a')
$Navy       = [System.Drawing.ColorTranslator]::FromHtml('#1e293b')
$Teal       = [System.Drawing.ColorTranslator]::FromHtml('#14b8a6')
$TealDark   = [System.Drawing.ColorTranslator]::FromHtml('#0d9488')
$White      = [System.Drawing.ColorTranslator]::FromHtml('#f8fafc')
$Gray       = [System.Drawing.ColorTranslator]::FromHtml('#94a3b8')
$Green      = [System.Drawing.ColorTranslator]::FromHtml('#22c55e')
$Amber      = [System.Drawing.ColorTranslator]::FromHtml('#f59e0b')
$Red        = [System.Drawing.ColorTranslator]::FromHtml('#ef4444')

try {
    $ppt = New-Object -ComObject PowerPoint.Application
    $ppt.Visible = [Microsoft.Office.Core.MsoTriState]::msoTrue
    $pres = $ppt.Presentations.Add()
    $pres.PageSetup.SlideWidth  = 960  # 16:9
    $pres.PageSetup.SlideHeight = 540
}
catch {
    Write-Host "ERROR: PowerPoint automation failed."
    Write-Host "Details: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "Try this diagnostic command:"
    Write-Host '  powershell -ExecutionPolicy Bypass -Command "$p = New-Object -ComObject PowerPoint.Application; $p.Visible = 1; $p.Quit()"'
    Write-Host ""
    Write-Host "If that fails but PowerPoint opens normally, Office COM registration may be broken."
    Write-Host "Script is ready at: $PSCommandPath"
    exit 1
}

function Add-Slide {
    param([int]$Layout = 12)  # ppLayoutBlank
    $slide = $pres.Slides.Add($pres.Slides.Count + 1, $Layout)
    $slide.Shapes.AddShape(1, 0, 0, 960, 540).Fill.ForeColor.RGB = $DarkNavy.ToArgb()
    $slide.Shapes.AddShape(1, 0, 0, 540, 540).Fill.ForeColor.RGB = $Navy.ToArgb()  # subtle half-bg
    $slide.Shapes.AddShape(1, 0, 0, 960, 6).Fill.ForeColor.RGB = $Teal.ToArgb()   # teal top bar
    return $slide
}

function Add-TextBox {
    param($Slide, [int]$Left, [int]$Top, [int]$Width, [int]$Height, [string]$Text, [string]$Font = 'Calibri', [int]$Size = 18, $Color = $White, [bool]$Bold = $false)
    $tb = $Slide.Shapes.AddTextbox(1, $Left, $Top, $Width, $Height)
    $tb.TextFrame.TextRange.Text = $Text
    $tb.TextFrame.TextRange.Font.Name = $Font
    $tb.TextFrame.TextRange.Font.Size = $Size
    $tb.TextFrame.TextRange.Font.Color.RGB = $Color.ToArgb()
    $tb.TextFrame.TextRange.Font.Bold = [int]$Bold
    $tb.TextFrame.TextRange.ParagraphFormat.Alignment = 1
    $tb.TextFrame.WordWrap = [Microsoft.Office.Core.MsoTriState]::msoTrue
    return $tb
}

function Add-BulletList {
    param($Slide, [int]$Left, [int]$Top, [int]$Width, [int]$Height, [string[]]$Items, [string]$Font = 'Calibri', [int]$Size = 16, $Color = $White)
    $tb = $Slide.Shapes.AddTextbox(1, $Left, $Top, $Width, $Height)
    $tf = $tb.TextFrame
    $tf.TextRange.Text = $Items -join "`r`n"
    $tf.TextRange.Font.Name = $Font
    $tf.TextRange.Font.Size = $Size
    $tf.TextRange.Font.Color.RGB = $Color.ToArgb()
    $tf.TextRange.ParagraphFormat.Alignment = 1
    $tf.TextRange.ParagraphFormat.Bullet.Type = 1  # ppBulletUnnumbered
    $tf.WordWrap = [Microsoft.Office.Core.MsoTriState]::msoTrue
    return $tb
}

function Add-Logo {
    param($Slide)
    if (Test-Path $Logo) {
        $slide.Shapes.AddPicture($Logo, $false, $true, 840, 16, 100, 40)
    }
}

# ========== SLIDE 1: TITLE ==========
$s1 = Add-Slide
Add-TextBox -Slide $s1 -Left 60 -Top 120 -Width 840 -Height 60 -Text 'Miautrix Mail Server' -Size 44 -Bold $true -Color $Teal
Add-TextBox -Slide $s1 -Left 60 -Top 192 -Width 840 -Height 40 -Text 'Secure | Multi-Tenant | Self-Hosted Email Platform' -Size 22 -Color $Gray
Add-TextBox -Slide $s1 -Left 60 -Top 260 -Width 840 -Height 30 -Text 'Version 1.0 - October 2026' -Size 18 -Color $White
Add-TextBox -Slide $s1 -Left 60 -Top 320 -Width 840 -Height 30 -Text '.NET 10 | PostgreSQL | React | Cloudflare Workers' -Size 16 -Color $Teal
Add-Logo -Slide $s1

# ========== SLIDE 2: EXECUTIVE OVERVIEW ==========
$s2 = Add-Slide
Add-TextBox -Slide $s2 -Left 60 -Top 30 -Width 500 -Height 40 -Text 'Executive Overview' -Size 28 -Bold $true -Color $Teal
Add-TextBox -Slide $s2 -Left 60 -Top 80 -Width 840 -Height 40 -Text 'Full email sovereignty for organizations - no vendor lock-in.' -Size 18 -Color $White
Add-BulletList -Slide $s2 -Left 60 -Top 130 -Width 840 -Height 350 -Items @(
    'Complete SMTP/IMAPS/ManageSieve mail pipeline on a single server or cluster.',
    'Strict multi-tenant isolation: each tenant sees only its own data.',
    'Built-in anti-spam, anti-malware, DKIM/SPF/DMARC, and mail-flow rules.',
    'Webmail, Admin Console, CLI, and Desktop App on Windows & Linux.',
    'Optional Cloudflare Workers transport for enhanced deliverability.',
    'Self-hosted identity with Argon2id, TOTP, and WebAuthn.'
)
Add-Logo -Slide $s2

# ========== SLIDE 3: BUSINESS VALUE ==========
$s3 = Add-Slide
Add-TextBox -Slide $s3 -Left 60 -Top 30 -Width 500 -Height 40 -Text 'Business Value & Product Goals' -Size 28 -Bold $true -Color $Teal
Add-BulletList -Slide $s3 -Left 60 -Top 90 -Width 840 -Height 380 -Items @(
    'Data Sovereignty - your email stays on your infrastructure, not a cloud provider.',
    'Cost Predictability - flat licensing with no per-mailbox SaaS markup.',
    'Multi-Tenant by Design - one deployment serves entire organizations or customer bases.',
    'Enterprise Security - Argon2id, MFA, RBAC, full audit trail, tenant isolation enforced at the data layer.',
    'Operational Control - Blue/Green updates, automated backup/restore, zero-downtime deployments.',
    'Modular Architecture - Clean Architecture with swap-in enterprise integrations (S3, Rspamd, IdP federation).',
    'v1.0 delivers the core mail pipeline; future releases add federation, advanced analytics, and native mobile clients.'
)
Add-Logo -Slide $s3

# ========== SLIDE 4: ARCHITECTURE ==========
$s4 = Add-Slide
Add-TextBox -Slide $s4 -Left 60 -Top 30 -Width 500 -Height 40 -Text 'High-Level Architecture' -Size 28 -Bold $true -Color $Teal
Add-BulletList -Slide $s4 -Left 60 -Top 90 -Width 840 -Height 380 -Items @(
    'Modular Monolith - Clean Architecture with Domain/Application/Infrastructure/Presentation layers.',
    'Domain Layer: Zero dependencies. Contains core entities, value objects, and business invariants.',
    'Application Layer: Use cases, DTOs, service contracts. Orchestrates domain logic.',
    'Persistence: EF Core with PostgreSQL. Tenant-scoped query filters enforce isolation.',
    'Transport Layer: SMTP (submission/receiving), IMAPS, ManageSieve protocol handlers.',
    'Queue System: SQL-backed outbound/inbound queue with retry, backoff, and dead-lettering.',
    'Workers: Background services for queue dispatching, anti-spam, mail-flow rules.',
    'Identity: Argon2id password hashing, TOTP/WebAuthn MFA, session management, granular RBAC.'
)
Add-Logo -Slide $s4

# ========== SLIDE 5: MAIL FLOW + TENANCY ==========
$s5 = Add-Slide
Add-TextBox -Slide $s5 -Left 60 -Top 30 -Width 700 -Height 40 -Text 'Mail Flow & Tenant Isolation' -Size 28 -Bold $true -Color $Teal
Add-BulletList -Slide $s5 -Left 60 -Top 90 -Width 840 -Height 380 -Items @(
    'Inbound: SMTP -> Queue -> Anti-Spam -> Anti-Malware -> Mailbox delivery.',
    'Outbound: Webmail/SMTP -> Queue -> DKIM sign -> Cloudflare Worker or local transport.',
    'Queue lifecycle: Pending -> active dispatch -> success | retry (backoff) -> dead-letter.',
    'Tenant Isolation: Single authorization helper - every read and write passes through it.',
    'Cross-tenant resource queries return HTTP 404 (never 403), no resource existence oracle.',
    'Global query filters provide defence-in-depth but are never the sole enforcement point.',
    'Roles bind to memberships, not users; permissions checked by string, never by role === admin.'
)
Add-Logo -Slide $s5

# ========== SLIDE 6: GRAPHIFY ==========
$s6 = Add-Slide
Add-TextBox -Slide $s6 -Left 60 -Top 30 -Width 700 -Height 40 -Text 'Codebase Network - Graphify' -Size 28 -Bold $true -Color $Teal
Add-TextBox -Slide $s6 -Left 60 -Top 80 -Width 840 -Height 30 -Text 'Knowledge graph of the full repository (last refreshed 2026-10-05)' -Size 16 -Color $Gray
Add-BulletList -Slide $s6 -Left 60 -Top 120 -Width 840 -Height 350 -Items @(
    '4,293 nodes | 10,668 edges | 261 communities detected.',
    'Extraction: 93% EXTRACTED | 7% INFERRED | 0% AMBIGUOUS.',
    'Top hubs: MessageService, CalendarService, AuthService, AdminService, ContactService.',
    'Cross-module edges link Domain entities, Application services, Persistence mappings, and UI.',
    'Community clustering surfaces hidden coupling between Sieve rules and spam classification.',
    'Interactive graph at graph.html - search, zoom, and explore the full network.',
    'GraphRAG-ready JSON available for machine query and traversal.'
)
Add-Logo -Slide $s6

# ========== SLIDE 7: PMI MILESTONES ==========
$s7 = Add-Slide
Add-TextBox -Slide $s7 -Left 60 -Top 30 -Width 700 -Height 40 -Text 'PMI Milestones & Delivery Status' -Size 28 -Bold $true -Color $Teal
Add-TextBox -Slide $s7 -Left 60 -Top 80 -Width 840 -Height 30 -Text '21 work packages across 4 epics - all complete.' -Size 16 -Color $Gray

$milestones = @(
    @{ M = 'M1: Core Foundation'; S = '[Complete]'; D = 'Scaffold, schema, auth/MFA, RBAC, audit trail' },
    @{ M = 'M2: Mail Engine'; S = '[Complete]'; D = 'SMTP, SPF/DKIM/DMARC, anti-spam, IMAP, Sieve, FTS, rules' },
    @{ M = 'M3: User Surfaces'; S = '[Complete]'; D = 'REST API, Web Admin, Webmail, CLI' },
    @{ M = 'M4: Operational'; S = '[Complete]'; D = 'Desktop client, backup/restore, Blue/Green updates, license' }
)
$y = 120
foreach ($m in $milestones) {
    $s7.Shapes.AddShape(1, 60, $y, 840, 65).Fill.ForeColor.RGB = $Navy.ToArgb()
    Add-TextBox -Slide $s7 -Left 80 -Top ($y + 8) -Width 60 -Height 30 -Text $m.S -Size 20
    Add-TextBox -Slide $s7 -Left 140 -Top ($y + 5) -Width 700 -Height 24 -Text $m.M -Size 16 -Bold $true -Color $Teal
    Add-TextBox -Slide $s7 -Left 140 -Top ($y + 32) -Width 700 -Height 24 -Text $m.D -Size 14 -Color $Gray
    $y += 80
}
Add-Logo -Slide $s7

# ========== SLIDE 8: DELIVERED CHANGES ==========
$s8 = Add-Slide
Add-TextBox -Slide $s8 -Left 60 -Top 30 -Width 700 -Height 40 -Text 'Major Changes Delivered' -Size 28 -Bold $true -Color $Teal
Add-BulletList -Slide $s8 -Left 60 -Top 90 -Width 840 -Height 380 -Items @(
    'Database schema: 5 EF Core migrations, production schema drift resolved, forward-only deployment.',
    'Authentication: Argon2id login, TOTP, WebAuthn, session management, refresh tokens.',
    'Mail pipeline: SMTP submission/receiving, IMAPS with credential verification, ManageSieve.',
    'Queue system: Inbound/outbound dispatch with retry/backoff, direction partitioning (fixes DB-06).',
    'Cloudflare transport: Per-domain optional Worker routing, fallback to primary tenant Worker.',
    'Anti-spam + anti-malware: Classification pipeline, quarantine lifecycle, auto-discard policies.',
    'Webmail: Composer, drafts, folders, contacts, directory, calendar RSVP, recipient picker.',
    'Admin UI: Domains, users, shared mailboxes, queue management, quarantine, audit log.'
)
Add-Logo -Slide $s8

# ========== SLIDE 9: ERRORS TIMELINE ==========
$s9 = Add-Slide
Add-TextBox -Slide $s9 -Left 60 -Top 30 -Width 700 -Height 40 -Text 'Error & Issue Resolution Timeline' -Size 28 -Bold $true -Color $Teal

$issues = @(
    @{ I = 'FE-01'; D = 'Auth bypass (dev flag left true)'; S = 'Fixed' },
    @{ I = 'FE-02'; D = 'Admin white screen (Vite base path)'; S = 'Fixed' },
    @{ I = 'FE-04'; D = 'Company Directory 0 contacts (permissions)'; S = 'Fixed' },
    @{ I = 'DB-04'; D = 'Production login 500 (column name drift)'; S = 'Fixed' },
    @{ I = 'DB-06'; D = 'Outbound queue hijacked as inbound'; S = 'Fixed' },
    @{ I = 'FE-07'; D = 'Calendar RSVP submission failure'; S = 'Fixed' },
    @{ I = 'FE-10'; D = 'Bulk delete white screen'; S = 'Fixed' },
    @{ I = 'TRX-01'; D = 'No SMTP listener deployed'; S = 'Fixed' },
    @{ I = 'TRX-04'; D = 'No outbound delivery (queue sat empty)'; S = 'Fixed' },
    @{ I = 'TRX-07'; D = 'Quarantine release not delivering'; S = 'Fixed' }
)

$y = 85
foreach ($ix in $issues) {
    Add-TextBox -Slide $s9 -Left 60 -Top $y -Width 80 -Height 22 -Text $ix.I -Size 12 -Color $Teal -Bold $true
    Add-TextBox -Slide $s9 -Left 140 -Top $y -Width 600 -Height 22 -Text $ix.D -Size 12
    Add-TextBox -Slide $s9 -Left 780 -Top $y -Width 60 -Height 22 -Text $ix.S -Size 14 -Color $Green
    $y += 35
}
Add-TextBox -Slide $s9 -Left 60 -Top 480 -Width 700 -Height 22 -Text 'Full log: ERRORS_AND_ISSUES.md (65+ tracked issues, all resolved for v1.0)' -Size 14 -Color $Gray
Add-Logo -Slide $s9

# ========== SLIDE 10: SECURITY ==========
$s10 = Add-Slide
Add-TextBox -Slide $s10 -Left 60 -Top 30 -Width 700 -Height 40 -Text 'Security & Compliance Posture' -Size 28 -Bold $true -Color $Teal
Add-BulletList -Slide $s10 -Left 60 -Top 90 -Width 840 -Height 380 -Items @(
    'Passwords: Argon2id (memory-hard, parallel-resistant) - never logged, never stored in plaintext.',
    'Authentication: TOTP and WebAuthn/Passkeys for second factor.',
    'Session Management: Short-lived tokens, refresh token rotation, server-side revocation.',
    'Tenant Isolation: 404-on-cross-tenant, single auth helper, role-permission string checks.',
    'Transport Security: No SMTP auth without TLS, HTTPS-only API, HSTS headers.',
    'Audit Trail: Every security-sensitive operation logged (login, tenant change, permission change).',
    'Secrets Management: Redaction enforced at the logger level, not per call site.',
    'Compliance: Forward-only migrations, backup/restore verified, Blue/Green deployment safe.'
)
Add-Logo -Slide $s10

# ========== SLIDE 11: STABILIZATION ==========
$s11 = Add-Slide
Add-TextBox -Slide $s11 -Left 60 -Top 30 -Width 700 -Height 40 -Text 'v1.0 Stabilization Evidence' -Size 28 -Bold $true -Color $Teal
Add-BulletList -Slide $s11 -Left 60 -Top 90 -Width 840 -Height 380 -Items @(
    'Build: dotnet build Miautrix.Mail.sln -warnaserror - 0 warnings, 0 errors across 29 projects.',
    'Webmail: pnpm --filter webmail build passes; all views compile without TypeScript errors.',
    'Admin: pnpm --filter admin build passes; queue, quarantine, domains all functional.',
    'Tests: 6 CalendarInvitationBuilder tests pass; QueueOwnershipTests pass.',
    'Deployment: Web, worker, anti-spam binaries published to Debian LXC behind Cloudflare Tunnel.',
    'Live verification: Inbound/outbound queue dispatch, Cloudflare Worker routing confirmed.',
    'Production migration: 20260920223000_AddMailboxName applied, login 500 resolved.',
    'Issue tracker: All tracked ERRORS_AND_ISSUES.md items resolved or documented as deferred.'
)
Add-Logo -Slide $s11

# ========== SLIDE 12: CONCLUSION ==========
$s12 = Add-Slide
Add-TextBox -Slide $s12 -Left 60 -Top 120 -Width 840 -Height 50 -Text 'Version 1.0 Complete' -Size 40 -Bold $true -Color $Teal
Add-TextBox -Slide $s12 -Left 60 -Top 190 -Width 840 -Height 40 -Text 'Miautrix Mail Server is production-ready.' -Size 22 -Color $White
Add-BulletList -Slide $s12 -Left 60 -Top 250 -Width 840 -Height 200 -Items @(
    'Core mail pipeline, multi-tenant, Webmail, Admin Console, CLI, Desktop App.',
    'Deployed at mail.miautrix.tech with Cloudflare Tunnel and optional Worker transport.',
    'Future: Federation (ActivityPub), advanced analytics, native mobile SDK.',
    'Licensing: Fail-open, flat pricing, MFA and admin access are free in every edition.'
)
Add-TextBox -Slide $s12 -Left 60 -Top 460 -Width 840 -Height 30 -Text 'mail.miautrix.tech | .NET 10 | PostgreSQL | React' -Size 14 -Color $Gray
Add-Logo -Slide $s12

# ========== SAVE ==========
$OutputPath = Join-Path $ReportDir 'Miautrix-Mail-Server-v1.0-Presentation.pptx'
if (-not (Test-Path $ReportDir)) { New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null }
$pres.SaveAs($OutputPath)
$pres.Close()
$ppt.Quit()

Write-Host "OK Presentation saved to: $OutputPath"
Write-Host "   Slides: 12  |  Format: 16:9 widescreen"
Write-Host "   Open in PowerPoint to review and customize."
