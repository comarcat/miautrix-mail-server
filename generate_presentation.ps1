$ErrorActionPreference = 'Stop'

$ReportDir = 'presentation_assets'

if (Test-Path -LiteralPath $ReportDir) {
    Remove-Item -LiteralPath $ReportDir -Recurse -Force
}

New-Item -ItemType Directory -Path $ReportDir | Out-Null

$Assets = @(
    @{ Source = 'architecture.html'; Destination = 'architecture.html' },
    @{ Source = 'graphify-out/graph.html'; Destination = 'graph.html' },
    @{ Source = 'graphify-out/GRAPH_REPORT.md'; Destination = 'GRAPH_REPORT.md' },
    @{ Source = 'PMI_Project_Charter_and_Plan.md'; Destination = 'README.md' },
    @{ Source = 'ERRORS_AND_ISSUES.md'; Destination = 'ERRORS_AND_ISSUES.md' }
)

Write-Host "Generating presentation assets in $ReportDir..."

foreach ($Asset in $Assets) {
    if (Test-Path -LiteralPath $Asset.Source) {
        $Target = Join-Path $ReportDir $Asset.Destination
        Copy-Item -LiteralPath $Asset.Source -Destination $Target -Force
        Write-Host "  Copied: $($Asset.Source) -> $Target"
    }
    else {
        Write-Host "  Warning: $($Asset.Source) not found."
    }
}

$Summary = @'
# Miautrix Mail Server v1.0 Presentation Summary

## 1. Project Overview
Miautrix Mail Server is a secure, high-performance, multi-tenant mail platform.
Status: **Version 1.0 Complete / Approved** (as of 2026-10-03).

## 2. Recent Technical Accomplishments
- **Outbound Delivery Fix:** Explicitly set `Direction=Outbound` for queue items, resolving delivery failures for external domains such as Gmail.
- **Cloudflare Integration:** Enabled optional, per-domain outbound transport via tenant-specific Workers, with local fallback for external addresses.
- **Stabilization Pass:** Resolved critical issues in calendar RSVP flows, Webmail bulk deletion stability, and timezone clarity.

## 3. Key Issue Log Summary
- Total Errors Tracked: See `ERRORS_AND_ISSUES.md`.
- Major Fixes:
  - **DB-06:** Outbound queue hijacking by inbound dispatcher.
  - **TRX-04:** Outbound Cloudflare integration.
  - **FE-10:** Bulk inbox delete stability.

## 4. Architecture Artifacts
- `architecture.html`: Visual component topology.
- `graph.html`: Navigable knowledge graph of the codebase.
- `GRAPH_REPORT.md`: Audit trail of codebase communities and hubs.
'@

Set-Content -Path (Join-Path $ReportDir 'SUMMARY.md') -Value $Summary -Encoding UTF8

Write-Host "Summary generated: $ReportDir/SUMMARY.md"
