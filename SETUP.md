# Miautrix Mail Server - Production Deployment Guide (Debian LXC)

This guide provides step-by-step instructions for deploying the Miautrix Mail Server from **Windows** to the **Debian LXC container** at `10.11.1.51` behind the Cloudflare Tunnel `mail.miautrix.tech`, connecting to the PostgreSQL production database `miautrix-mail-pro` at `10.11.1.52`.

---

## Architecture & Configuration Summary

| Parameter | Value |
|---|---|
| **Target Server** | Debian LXC Container (`10.11.1.51`) |
| **Domain (Cloudflare Tunnel)** | `mail.miautrix.tech` |
| **Reverse Proxy** | NGINX (Port 80 → Kestrel Backend & Static Frontends) |
| **Backend Service** | .NET 10 Self-Contained Linux x64 executable (Port 5000) |
| **Production Database** | Configure `MIAUTRIX_DB_CONNECTION` in the protected server environment; obtain credentials from the owner. |
| **Default Admin Account** | `admin@miautrix.org` |
| **Initial Admin Password** | No default password exists; set it with the CLI secure prompt after seeding. |
| **Service Manager** | systemd (`miautrix-mail.service`) |

---

## PART 1: Prepare the Debian LXC Container (Run on Container)

Log in to the Debian LXC container (via Proxmox console or SSH) and run the following commands:

### 1.1. Install System Dependencies & NGINX
*(Note: `software-properties-common` is removed as minimal Debian uses direct apt repos)*

```bash
# Update repositories and install essential packages
apt-get update && apt-get upgrade -y
apt-get install -y curl wget gnupg2 ca-certificates unzip nginx

# Create deployment directory
mkdir -p /opt/miautrix-mail/{app,config,logs,data,webmail,admin}
chown -R www-data:www-data /opt/miautrix-mail
```

### 1.2. Configure Systemd Service for .NET Backend

Create the systemd service file:
```bash
cat << 'EOF' > /etc/systemd/system/miautrix-mail.service
[Unit]
Description=Miautrix Mail Server API & Core Engine
After=network.target

[Service]
Type=simple
User=www-data
WorkingDirectory=/opt/miautrix-mail/app
ExecStart=/opt/miautrix-mail/app/Miautrix.Mail.Web
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=miautrix-mail
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
EnvironmentFile=/opt/miautrix-mail/.env

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
```

### 1.3. Configure NGINX for Cloudflare Tunnel (`mail.miautrix.tech`)

Configure NGINX to forward API traffic to the .NET backend and serve the Webmail & Admin SPAs:

```bash
cat << 'EOF' > /etc/nginx/sites-available/miautrix-mail.conf
server {
    listen 80;
    server_name mail.miautrix.tech 10.11.1.51 localhost;

    # Maximum attachment/upload size (50MB)
    client_max_body_size 50M;

    # Web Admin Console (Subpath)
    location = /admin {
        return 301 /admin/;
    }

    location /admin/ {
        alias /opt/miautrix-mail/admin/;
        index index.html;
        try_files $uri $uri/ /admin/index.html;
    }

    # REST API & Mail Engine
    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $http_x_forwarded_proto;
        proxy_set_header X-Real-IP $remote_addr;
    }

    # OpenAPI Specifications
    location /openapi/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    # Webmail Client (Root UI)
    location / {
        root /opt/miautrix-mail/webmail;
        index index.html;
        try_files $uri $uri/ /index.html;
    }
}
EOF

# Enable site and test configuration
rm -f /etc/nginx/sites-enabled/default
ln -sf /etc/nginx/sites-available/miautrix-mail.conf /etc/nginx/sites-enabled/
nginx -t && systemctl restart nginx
```

---

## PART 2: Versioned Release Deployment

Do not publish directly into `/opt/miautrix-mail` or copy files over active directories. Use the guarded, versioned workflow in [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md): pin `MIAUTRIX_STORAGE_DIR`, verify server configuration, run `scripts/deploy-release.ps1 -DryRun`, then coordinate and run the owner-approved deployment. The script publishes Web, Worker, CLI, admin and webmail as one versioned release, takes a custom-format database backup before migration/switch, and keeps the active and previous releases for rollback.

For v2.0, both systemd services must resolve `MIAUTRIX_STORAGE_DIR` to the same existing path outside release directories before deployment. The CLI is deployed with the release so that `messages reparse --tenant all --dry-run` is available after deploy. Never put database credentials in command arguments or checked-in files; configure them through the protected server environment.
