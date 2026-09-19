# Miautrix Mail Server — Errors, Issues & Review Log

This document tracks identified errors, configuration discrepancies, environment-specific constraints, and verification items for the Miautrix Mail Server deployment on Debian LXC (`10.11.1.51`) and PostgreSQL (`10.11.1.52`) behind Cloudflare Tunnel (`mail.miautrix.tech`).

---

## 1. System & OS Environment Issues

| Issue ID | Component | Description & Error Message | Root Cause | Status / Notes |
|---|---|---|---|---|
| **SYS-01** | Debian LXC Package Manager | `E: Unable to locate package software-properties-common` | Minimal Debian 12 LXC images do not include PPA/Ubuntu tools in default repositories. | **Documented in SETUP.md**: Removed `software-properties-common` from standard apt install sequence. |
| **SYS-02** | SSH / Remote Auth | SSH key-based / passwordless login rejected on container `10.11.1.51`. | Container OS policy enforces password authentication for `root`. | Manual password prompt required during `scp`/`ssh` until SSH key is added to `~/.ssh/authorized_keys`. |
| **SYS-03** | Windows Deploy Script | PowerShell script execution restrictions (`ExecutionPolicy`). | Default Windows client policy prevents running `.ps1` automation scripts directly without review. | Individual `scp`/`ssh` commands provided in `SETUP.md` as alternative to `deploy.ps1`. |

---

## 2. Frontend & UI Runtime Issues

| Issue ID | Component | Description & Error Message | Root Cause | Status / Notes |
|---|---|---|---|---|
| **FE-01** | Webmail Client | Webmail loaded directly into default inbox (`alex.vance@...`) without requiring login. | Development flag `useState<boolean>(true)` was left enabled in `webmail/src/App.tsx`. | **Fixed in source**: Changed default state to `false` to render `LoginView`. Needs live verification. |
| **FE-02** | Admin Console | `/admin` rendered blank page / white screen on `mail.miautrix.tech/admin`. | Vite `base` was set to root `/`. JavaScript & CSS chunks resolved to `/assets/` instead of `/admin/assets/`, colliding with Webmail. | **Fixed in source**: Configured `base: '/admin/'` in `admin/vite.config.ts`. Needs live verification. |
| **FE-03** | Asset Packaging | Branding logos and icons (`LogoIcon.png`, `images/icons/*`) missing on deployed frontends. | Image assets were located outside the Vite `public/` directories and were omitted during `dist` bundling. | **Fixed in source**: Copied icon sets and logos into `public/images/` for both Webmail and Admin. |

---

## 3. NGINX & Ingress Routing Issues

| Issue ID | Component | Description & Error Message | Root Cause | Status / Notes |
|---|---|---|---|---|
| **NGX-01** | NGINX Subpath | Direct access to `https://mail.miautrix.tech/admin` (no trailing slash) fails or serves wrong index. | NGINX `alias` requires an exact match and explicit 301 redirection from `/admin` to `/admin/`. | **Updated in SETUP.md**: Added `location = /admin { return 301 /admin/; }` rule. |
| **NGX-02** | Attachment Uploads | Large attachments (over 1MB) fail with HTTP `413 Request Entity Too Large`. | Default NGINX `client_max_body_size` is 1MB. | Configured `client_max_body_size 50M;` in NGINX site configuration. |
| **NGX-03** | Cloudflare Headers | Backend logs show `127.0.0.1` as client IP for incoming API requests. | Missing `X-Forwarded-For` and `CF-Connecting-IP` proxy header propagation. | Added proxy headers in NGINX configuration block. |

---

## 4. Backend & Database Integrity Checks

| Issue ID | Component | Description & Error Message | Root Cause | Status / Notes |
|---|---|---|---|---|
| **DB-01** | EF Core Migrations | Database migration failure risk during runtime startup. | Multi-tenant schema and RLS policies require elevated DDL permissions on `miautrix-mail-pro`. | Migration must be run via `dotnet ef database update` before starting service. |
| **DB-02** | Tenant Isolation | Risk of cross-tenant enumeration via HTTP status codes. | Standard REST APIs often return 403 Forbidden on existing unauthorized items. | Architecture rule enforced: Cross-tenant accesses return **404 Not Found**. |
| **DB-03** | Environment Variables | Service startup crash if connection string or secrets are missing. | Strict boot validation halts execution on missing environment variables. | Systemd unit file must include `MIAUTRIX_DB_CONNECTION` and `ASPNETCORE_ENVIRONMENT=Production`. |

---

## 5. Items to Review & Verify Later

- [ ] **Live Webmail Login Flow**: Test login screen behavior, invalid credentials handling, and session persistence at `https://mail.miautrix.tech`.
- [ ] **Live Admin Console Navigation**: Test routing between Dashboard, Tenants, Mailboxes, Domains, Queue, Audit Logs, and Settings at `https://mail.miautrix.tech/admin`.
- [ ] **Image & Icon Rendering**: Check that all SVG and PNG icons load cleanly in both dark and light modes.
- [ ] **REST API / OpenAPI Endpoint**: Test `https://mail.miautrix.tech/openapi/v1.json` behind Cloudflare Tunnel.
- [ ] **Database Connection Health**: Verify `systemctl status miautrix-mail` on the container to confirm active connection to PostgreSQL (`10.11.1.52`).
