# Miautrix Mail Server

Self-hosted, multi-tenant mail platform designed as a modular monolith in .NET 10 LTS with PostgreSQL and React frontend surfaces.

## 📐 Architecture & Design

- **Architecture Diagram:** [`architecture.html`](architecture.html) (interactive diagram with showcase quality profile)
- **Architecture Blueprint:** [`blueprints/miautrix-mail-server/blueprint.md`](blueprints/miautrix-mail-server/blueprint.md)
- **Charter & Plan:** [`PMI_Project_Charter_and_Plan.md`](PMI_Project_Charter_and_Plan.md)
- **Admin UI Specification:** [`DESIGN-Admin.md`](DESIGN-Admin.md) — Comprehensive technical design for the Admin Website
- **Webmail UI Specification:** [`DESIGN-WEBMAIL.md`](DESIGN-WEBMAIL.md) — Comprehensive technical design for the Webmail Frontend

## 🚀 Project Status

Tasks completed and verified (T1–T13):

| Task | Title | Status | Verification Gate |
|---|---|---|---|
| **T1** | Solution scaffold & CI | ✅ Done | `dotnet build Miautrix.Mail.sln -warnaserror` |
| **T2** | Domain model & EF Core schema | ✅ Done | `dotnet ef database update` |
| **T3** | Seed data and indexes | ✅ Done | `dotnet run --project src/Miautrix.Mail.Seeder` |
| **T4** | Identity: credentials, MFA, sessions | ✅ Done | `dotnet test --filter Category=Identity` |
| **T5** | Authorization, roles, tenant isolation | ✅ Done | `dotnet test --filter Category=Isolation` |
| **T6** | Audit trail | ✅ Done | `dotnet test --filter Category=Audit` |
| **T7** | SMTP listener and queue | ✅ Done | `dotnet test --filter Category=Smtp` |
| **T8** | SPF, DKIM, DMARC | ✅ Done | `dotnet test --filter Category=Dkim` |
| **T9** | Anti-spam baseline and quarantine | ✅ Done | `dotnet test --filter Category=AntiSpam` |
| **T10** | IMAP, storage abstraction, attachments | ✅ Done | `dotnet test --filter Category=Imap` |
| **T11** | ManageSieve | ✅ Done | `dotnet test --filter Category=Sieve` |
| **T12** | Full-text search indexing | ✅ Done | `dotnet test --filter Category=Search` |
| **T13** | Mail-flow rule engine & simulator | ✅ Done | `dotnet test --filter Category=Rules` |
| **T14–T21** | API, Admin GUI, Webmail, CLI, Desktop, Operations | ⏳ In Queue | Surface & operational buildout |

## 🛠️ Tech Stack & Prerequisites

- **Backend:** .NET 10 LTS / C# 13
- **Database:** PostgreSQL 17 (`Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3` / `EF Core 10.0.4`)
- **Frontend Surfaces:** React 19, TypeScript, Tailwind CSS, Vite
- **Storage:** Content-addressable storage (CAS) with SHA-256 deduplication and bounded streaming
- **Authentication:** Argon2id / bcrypt, TOTP MFA, WebAuthn

## ⚙️ Development Environment

Set the PostgreSQL connection string:

```bash
export MIAUTRIX_DB_CONNECTION="Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!"
```

### Build & Test Commands

```bash
# Build the entire solution (warnings treated as errors)
dotnet build Miautrix.Mail.sln -warnaserror

# Run full test suite
dotnet test Miautrix.Mail.sln

# Run targeted test categories
dotnet test Miautrix.Mail.sln --filter Category=Dkim
dotnet test Miautrix.Mail.sln --filter Category=AntiSpam
dotnet test Miautrix.Mail.sln --filter Category=Imap
dotnet test Miautrix.Mail.sln --filter Category=Sieve
dotnet test Miautrix.Mail.sln --filter Category=Search
dotnet test Miautrix.Mail.sln --filter Category=Rules
```

## 🔒 Security Invariants

- **Multi-Tenant Isolation:** Cross-tenant resource lookups return `404 Not Found` (never `403 Forbidden`) and log `AUTH-4060`.
- **Sensitive Data Redaction:** Passwords, TOTP secrets, session tokens, private keys, and message bodies are never logged.
- **Strict Transport Security:** SMTP authentication without encryption is unconditionally rejected (`530 5.7.0`).
- **Open-Relay Protection:** Inbound messages for non-local recipients are rejected at `RCPT TO` with `550 5.7.1`.
