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
| **Production Database** | `Host=10.11.1.52;Port=5432;Database=miautrix-mail-pro;Username=mmdb-user;Password=Mi@usito#2026!` |
| **Default Admin Account** | `admin@miautrix.org` |
| **Default Admin Password** | `CH@nGEm3!` *(Must be changed on 1st login)* |
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
Environment=MIAUTRIX_DB_CONNECTION="Host=10.11.1.52;Port=5432;Database=miautrix-mail-pro;Username=mmdb-user;Password=Mi@usito#2026!"

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

## PART 2: Build & Publish Artifacts (Run from Windows PowerShell)

Run these commands in PowerShell from the project root: `C:\Users\miauadmin\OneDrive\Documentos\GitHub\miautrix-mail-server`

### 2.1. Publish .NET 10 Self-Contained Binary
*(Because it is published as `self-contained`, the Debian container does not need any .NET SDK/Runtime pre-installed)*

```powershell
# Publish the Web & API server for Linux x64
dotnet publish src/Miautrix.Mail.Web/Miautrix.Mail.Web.csproj -c Release -r linux-x64 --self-contained true -o ./publish/app
```

### 2.2. Build Frontends (Web Admin & Webmail)

```powershell
# Build Admin Console
pnpm --filter admin build
New-Item -ItemType Directory -Force -Path ./publish/admin | Out-Null
Copy-Item -Path ./admin/dist/* -Destination ./publish/admin/ -Recurse -Force

# Build Webmail Client
pnpm --filter webmail build
New-Item -ItemType Directory -Force -Path ./publish/webmail | Out-Null
Copy-Item -Path ./webmail/dist/* -Destination ./publish/webmail/ -Recurse -Force
```

### 2.3. Apply Database Migrations to `miautrix-mail-pro`

```powershell
$env:MIAUTRIX_DB_CONNECTION = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-pro;Username=mmdb-user;Password=Mi@usito#2026!"
dotnet ef database update --project src/Miautrix.Mail.Persistence --startup-project src/Miautrix.Mail.Web
```

---

## PART 3: Deploy Artifacts to Debian LXC via Windows SCP

Since Debian uses standard password authentication:

### 3.1. Copy Published Files using Windows Built-in `scp`

Run each of these commands in PowerShell (Windows will prompt you to enter the container's root password):

```powershell
# 1. Copy Backend Executable & Dependencies
scp -r ./publish/app/* root@10.11.1.51:/opt/miautrix-mail/app/

# 2. Copy Webmail Frontend
scp -r ./publish/webmail/* root@10.11.1.51:/opt/miautrix-mail/webmail/

# 3. Copy Admin Frontend
scp -r ./publish/admin/* root@10.11.1.51:/opt/miautrix-mail/admin/
```

---

## PART 4: Start and Verify Services (Run on Container)

Log back into the container (or run via SSH) to set permissions and start the service:

```bash
# Ensure executable permissions on the self-contained binary
chmod +x /opt/miautrix-mail/app/Miautrix.Mail.Web
chown -R www-data:www-data /opt/miautrix-mail

# Start and enable the Miautrix Mail service
systemctl enable --now miautrix-mail

# Verify service status and logs
systemctl status miautrix-mail
journalctl -u miautrix-mail -n 50 --no-pager
```

---

## PART 5: Verification & Testing

1. **Local Container Verification**:
   ```bash
   curl -I http://localhost/api/v1/queue
   ```
2. **Cloudflare Tunnel Access**:
   - Open your browser to `https://mail.miautrix.tech` (Webmail Interface)
   - Open your browser to `https://mail.miautrix.tech/admin` (Admin Console)
   - Test REST API endpoint at `https://mail.miautrix.tech/openapi/v1.json`
