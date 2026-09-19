# Miautrix Mail Server - Deployment Setup (Debian LXC)

This document outlines everything required to manually or automatically deploy the Miautrix Mail Server to your Debian LXC container at `10.11.1.51`.

## 1. What I Need to Publish the Site

To perform a fully automated deployment from this session, I need:
1. **Network & SSH Access**: The ability to run `ssh root@10.11.1.51` (or whichever user you prefer) from this machine without an interactive password prompt.
2. **Database Details**: Confirmation if we are using the existing PostgreSQL database (`10.11.1.52`) or if you want a fresh PostgreSQL instance installed on this LXC container.
3. **Domain Name**: The domain or IP address you want to use for the Webmail and Admin UI (e.g., `mail.yourdomain.com` or just `10.11.1.51`), so I can configure the NGINX reverse proxy.

## 2. Steps to Grant Me SSH Access

Because I run locally on your Windows machine as Claude Code, I use your machine's network and credentials. To give me SSH access to the LXC container:

**Step A: Generate an SSH Key (if you don't have one)**
Open your terminal and run:
`ssh-keygen -t ed25519 -f $HOME/.ssh/miautrix_lxc -N ""`

**Step B: Copy the Key to the Container**
Run this command to copy your key to the container (it will ask for the container's root password):
`ssh-copy-id -i $HOME/.ssh/miautrix_lxc.pub root@10.11.1.51`

**Step C: Configure SSH Agent/Config**
Tell your computer to use this key automatically by adding it to your SSH config. You can run this command for me, or let me know and I will configure it:
```bash
Add-Content $HOME\.ssh\config -Value @"
Host miautrix-lxc
    HostName 10.11.1.51
    User root
    IdentityFile ~/.ssh/miautrix_lxc
"@
```

Once this is done, you can reply saying "SSH is ready," and I can remotely control the container!

## 3. Preparation Script for the Debian LXC

Below is the bash script to prepare your Debian container. It installs **NGINX** (preferred over Apache for .NET Core apps due to its performance and seamless Kestrel integration), the **.NET 10 Runtime**, and basic system utilities. 

I can execute this script directly on the container once SSH access is granted, or you can run it manually.

```bash
#!/bin/bash
set -e

echo "Updating system packages..."
apt-get update && apt-get upgrade -y

echo "Installing essential utilities..."
apt-get install -y curl wget gnupg2 software-properties-common apt-transport-https unzip systemd nginx

echo "Installing .NET 10 Runtime..."
# Download Microsoft signing key and repository
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install ASP.NET Core Runtime (We don't need the SDK on the server, just the runtime)
apt-get update
# Note: At the time of script execution, .NET 10 is available via preview/daily channels or officially soon.
# Use standard apt commands for the ASP.NET runtime:
apt-get install -y aspnetcore-runtime-10.0 || echo "Please verify .NET 10 apt availability on your specific Debian version"

echo "Configuring NGINX base setup..."
# Remove default NGINX site
rm -f /etc/nginx/sites-enabled/default

# Create Application Directory structure
mkdir -p /opt/miautrix-mail/{app,config,logs,data}
chown -R www-data:www-data /opt/miautrix-mail

echo "Install Complete. The server is ready to receive the application artifacts."
```

## Next Steps

1. Let me know if you want to use the `10.11.1.52` database or install a local one.
2. Grant me SSH access using the steps in Section 2, and type **"SSH is ready."**
3. Once ready, I will compile the backend (`dotnet publish`) and the web frontends (`pnpm build`), securely copy them to `/opt/miautrix-mail/app` over SSH, configure the `systemd` daemon, and configure NGINX to route HTTP traffic cleanly to the .NET Kestrel server.