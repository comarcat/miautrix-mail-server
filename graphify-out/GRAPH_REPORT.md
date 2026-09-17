# Graph Report - miautrix-mail-server  (2026-09-16)

## Corpus Check
- Corpus is ~15,927 words - fits in a single context window. You may not need a graph.

## Summary
- 53 nodes · 67 edges · 10 communities (8 shown, 2 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 2 edges (avg confidence: 0.9)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Core Platform & Identity (Epic 1)
- Mail Transport & Protocols (Epic 2)
- Operations & Governance (Epic 4)
- SMTP, Anti-Spam & Malware Seams
- Platform Architecture & Core Stack
- Client Surfaces & APIs (Epic 3)
- IMAP Storage & Sieve Filtering
- Search Indexing & Mail Flow Rules
- Dependency Version Verifier
- JMAP Protocol Engine

## God Nodes (most connected - your core abstractions)
1. `Epic 2: Mail Transport & Policy (T7-T13)` - 14 edges
2. `Epic 1: Core Platform (T1-T6)` - 9 edges
3. `Epic 4: Operations (T18-T21)` - 5 edges
4. `T4: Identity: credentials, MFA, sessions` - 5 edges
5. `T9: Anti-spam baseline and quarantine` - 5 edges
6. `Miautrix Mail Server Platform` - 4 edges
7. `Epic 3: Surfaces (T14-T17)` - 4 edges
8. `T7: SMTP listener and queue` - 4 edges
9. `T10: IMAP, storage abstraction, attachments` - 4 edges
10. `T12: Full-text search indexing` - 4 edges

## Surprising Connections (you probably didn't know these)
- `Epic 1: Core Platform (T1-T6)` --conceptually_related_to--> `Clean Architecture Pattern`  [INFERRED]
  blueprints/miautrix-mail-server/epics/01-core-platform.md → Miautrix_Mail_Server_Final_Architecture_Design.md
- `T4: Identity: credentials, MFA, sessions` --implements--> `IIdentityProvider`  [EXTRACTED]
  blueprints/miautrix-mail-server/tasks.json → Miautrix_Mail_Server_Final_Architecture_Design.md
- `T9: Anti-spam baseline and quarantine` --implements--> `ISpamProvider`  [EXTRACTED]
  blueprints/miautrix-mail-server/tasks.json → Miautrix_Mail_Server_Final_Architecture_Design.md
- `T9: Anti-spam baseline and quarantine` --implements--> `IMalwareProvider`  [EXTRACTED]
  blueprints/miautrix-mail-server/tasks.json → Miautrix_Mail_Server_Final_Architecture_Design.md
- `T4: Identity: credentials, MFA, sessions` --implements--> `IAuthenticationProvider`  [EXTRACTED]
  blueprints/miautrix-mail-server/tasks.json → Miautrix_Mail_Server_Final_Architecture_Design.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Miautrix Provider Seams** — miautrix_mail_server_final_architecture_design_iidentityprovider, miautrix_mail_server_final_architecture_design_ispamprovider, miautrix_mail_server_final_architecture_design_imalwareprovider, miautrix_mail_server_final_architecture_design_imailstorage, miautrix_mail_server_final_architecture_design_isearchprovider, miautrix_mail_server_final_architecture_design_iqueueprovider [EXTRACTED 1.00]
- **Miautrix Epics Hierarchy** — blueprints_miautrix_mail_server_epics_01_core_platform_epic_1, blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2, blueprints_miautrix_mail_server_epics_03_surfaces_epic_3, blueprints_miautrix_mail_server_epics_04_operations_epic_4 [EXTRACTED 1.00]

## Communities (10 total, 2 thin omitted)

### Community 0 - "Core Platform & Identity (Epic 1)"
Cohesion: 0.27
Nodes (11): Epic 1: Core Platform (T1-T6), T1: Solution scaffold and CI, T2: Domain model and EF Core schema, T3: Seed data and indexes, T4: Identity: credentials, MFA, sessions, T5: Authorization, roles, tenant isolation, T6: Audit trail, Secrets & Redaction Policy (+3 more)

### Community 1 - "Mail Transport & Protocols (Epic 2)"
Cohesion: 0.25
Nodes (8): Epic 2: Mail Transport & Policy (T7-T13), Anti-Spam & Quarantine Subsystem, DNS Authentication (SPF, DKIM, DMARC, ARC), Full-Text Search (FTS) Indexing, IMAP Protocol Listener, Mail-Flow Rule Engine & Simulator, ManageSieve Script Engine, SMTP Listener & Queue

### Community 2 - "Operations & Governance (Epic 4)"
Cohesion: 0.43
Nodes (7): Epic 4: Operations (T18-T21), T18: Desktop admin application, T19: Backup and restore, T20: Blue/green update & migrations ladder, T21: Security hardening & licence gating, Verify-Task Protocol, Licensing & Over-Allowance Policy

### Community 3 - "SMTP, Anti-Spam & Malware Seams"
Cohesion: 0.33
Nodes (6): T7: SMTP listener and queue, T8: SPF, DKIM, DMARC, T9: Anti-spam baseline and quarantine, IMalwareProvider, IQueueProvider, ISpamProvider

### Community 4 - "Platform Architecture & Core Stack"
Cohesion: 0.33
Nodes (6): Clean Architecture Pattern, .NET 10 LTS Core Engine, Miautrix Mail Server Platform, Modular Monolith Architecture, PostgreSQL Storage & Schema, React & TypeScript Frontend

### Community 5 - "Client Surfaces & APIs (Epic 3)"
Cohesion: 0.70
Nodes (5): Epic 3: Surfaces (T14-T17), T14: API contract and OpenAPI, T15: Web admin GUI, T16: Webmail, T17: CLI

### Community 6 - "IMAP Storage & Sieve Filtering"
Cohesion: 0.67
Nodes (3): T10: IMAP, storage abstraction, attachments, T11: ManageSieve, IMailStorage

### Community 7 - "Search Indexing & Mail Flow Rules"
Cohesion: 0.67
Nodes (3): T12: Full-text search indexing, T13: Mail-flow rule engine & simulator, ISearchProvider

## Knowledge Gaps
- **21 isolated node(s):** `nuget`, `npm`, `.NET 10 LTS Core Engine`, `PostgreSQL Storage & Schema`, `React & TypeScript Frontend` (+16 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 24 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **2 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Epic 2: Mail Transport & Policy (T7-T13)` connect `Mail Transport & Protocols (Epic 2)` to `SMTP, Anti-Spam & Malware Seams`, `IMAP Storage & Sieve Filtering`, `Search Indexing & Mail Flow Rules`?**
  _High betweenness centrality (0.586) - this node is a cross-community bridge._
- **Why does `T7: SMTP listener and queue` connect `SMTP, Anti-Spam & Malware Seams` to `Core Platform & Identity (Epic 1)`, `Mail Transport & Protocols (Epic 2)`?**
  _High betweenness centrality (0.420) - this node is a cross-community bridge._
- **Why does `T6: Audit trail` connect `Core Platform & Identity (Epic 1)` to `SMTP, Anti-Spam & Malware Seams`?**
  _High betweenness centrality (0.386) - this node is a cross-community bridge._
- **What connects `nuget`, `npm`, `.NET 10 LTS Core Engine` to the rest of the system?**
  _21 weakly-connected nodes found - possible documentation gaps or missing edges._