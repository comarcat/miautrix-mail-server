#!/usr/bin/env bash
set -euo pipefail

systemctl daemon-reload
systemctl restart miautrix-mail miautrix-mail-worker nginx
