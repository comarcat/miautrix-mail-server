#!/usr/bin/env python3
import os
import shutil

# Configuration
report_dir = "presentation_assets"
if os.path.exists(report_dir):
    shutil.rmtree(report_dir)
os.makedirs(report_dir)

# Assets to copy
assets = [
    ("architecture.html", "architecture.html"),
    ("graphify-out/graph.html", "graph.html"),
    ("graphify-out/GRAPH_REPORT.md", "GRAPH_REPORT.md"),
    ("PMI_Project_Charter_and_Plan.md", "README.md"),
    ("ERRORS_AND_ISSUES.md", "ERRORS_AND_ISSUES.md")
]

print(f"Generating presentation assets in {report_dir}...")
for src, dst in assets:
    if os.path.exists(src):
        shutil.copy(src, os.path.join(report_dir, dst))
        print(f"  Copied: {src} -> {report_dir}/{dst}")
    else:
        print(f"  Warning: {src} not found.")

# Summary generation
with open(os.path.join(report_dir, "SUMMARY.md"), "w") as f:
    f.write("# Miautrix Mail Server v1.0 Presentation Summary\n\n")
    f.write("## 1. Project Overview\n")
    f.write("Miautrix Mail Server is a secure, high-performance, multi-tenant mail platform.\n")
    f.write("Status: **Version 1.0 Complete / Approved** (as of 2026-10-03).\n\n")

    f.write("## 2. Recent Technical Accomplishments\n")
    f.write("- **Outbound Delivery Fix:** Explicitly set `Direction=Outbound` for queue items, resolving delivery failures for external external domains (e.g., Gmail).\n")
    f.write("- **Cloudflare Integration:** Enabled optional, per-domain outbound transport via tenant-specific Workers, with local fallback for external addresses.\n")
    f.write("- **Stabilization Pass:** Resolved critical issues in calendar RSVP flows, Webmail bulk deletion stability, and timezone clarity.\n\n")

    f.write("## 3. Key Issue Log Summary\n")
    f.write("- Total Errors Tracked: See `ERRORS_AND_ISSUES.md`.\n")
    f.write("- Major Fixes:\n")
    f.write("  - **DB-06:** Outbound queue hijacking by inbound dispatcher.\n")
    f.write("  - **TRX-04:** Outbound cloudflare integration.\n")
    f.write("  - **FE-10:** Bulk inbox delete stability.\n\n")

    f.write("## 4. Architecture Artifacts\n")
    f.write("- `architecture.html`: Visual component topology.\n")
    f.write("- `graph.html`: Navigable knowledge graph of the codebase (October 5 status).\n")
    f.write("- `GRAPH_REPORT.md`: Audit trail of codebase communities and hubs.\n")

print("Summary generated: presentation_assets/SUMMARY.md")
