#!/usr/bin/env bash
# Prepare the blob store used by the mailbox export / delete / assign endpoints.
#
# The API resolves its content-addressed store from MIAUTRIX_STORAGE_DIR and falls back to
# <AppContext.BaseDirectory>/mail_data. On the container that fallback is
# /opt/miautrix-mail/app/mail_data — *inside* the deploy target, which is fine as long as it
# is never wiped, but it is safer to pin it to the persistent /opt/miautrix-mail/data path.
#
#   ./lxc-prepare-storage.sh          # report only; change nothing
#   ./lxc-prepare-storage.sh --pin    # move blobs to the persistent path and pin the env var
#
# Idempotent. Never deletes a blob.

set -euo pipefail

APP_DIR=/opt/miautrix-mail/app
LEGACY_DIR="$APP_DIR/mail_data"
PERSISTENT_DIR=/opt/miautrix-mail/data/mail
DROP_IN_DIR=/etc/systemd/system/miautrix-mail.service.d
DROP_IN="$DROP_IN_DIR/storage.conf"
SERVICE=miautrix-mail

PIN=0
[[ "${1:-}" == "--pin" ]] && PIN=1

count_blobs() {
    local dir="$1"
    [[ -d "$dir" ]] || { echo 0; return; }
    find "$dir" -type f 2>/dev/null | wc -l | tr -d ' '
}

current_env_dir() {
    local unit
    unit=$(systemctl show -p Environment --value "$SERVICE" 2>/dev/null || true)
    [[ -n "$unit" ]] || { echo ""; return; }
    printf '%s\n' "$unit" \
        | tr ' ' '\n' \
        | sed -n 's/^MIAUTRIX_STORAGE_DIR=//p' \
        | tr -d '"' | head -n1 || echo ""
}

effective_dir() {
    local configured
    configured=$(current_env_dir)
    if [[ -n "$configured" ]]; then echo "$configured"; else echo "$LEGACY_DIR"; fi
}

echo "==> Blob store status"
printf '    legacy   (%s): %s file(s)\n' "$LEGACY_DIR" "$(count_blobs "$LEGACY_DIR")"
printf '    pinned   (%s): %s file(s)\n' "$PERSISTENT_DIR" "$(count_blobs "$PERSISTENT_DIR")"
printf '    effective: %s\n' "$(effective_dir)"

if [[ "$PIN" -eq 0 ]]; then
    echo "    (report only — re-run with --pin to move blobs to the persistent path)"
    exit 0
fi

# 1) Create the persistent path. Ownership must match the service user.
install -d -o www-data -g www-data -m 0750 "$PERSISTENT_DIR"

# 2) Copy any existing blobs across exactly once. Only copy into an empty target so a
#    re-run can never overwrite newer content with older content.
persistent_count=$(count_blobs "$PERSISTENT_DIR")
legacy_count=$(count_blobs "$LEGACY_DIR")

if [[ "$persistent_count" -eq 0 && "$legacy_count" -gt 0 ]]; then
    echo "==> Copying $legacy_count blob(s): $LEGACY_DIR -> $PERSISTENT_DIR"
    cp -a "$LEGACY_DIR/." "$PERSISTENT_DIR/"
    chown -R www-data:www-data "$PERSISTENT_DIR"
elif [[ "$persistent_count" -gt 0 && "$legacy_count" -gt 0 ]]; then
    echo "==> Both directories hold blobs; leaving them untouched." >&2
    echo "    Verify which one is authoritative before pinning." >&2
    exit 1
fi

# 3) Pin the env var via a drop-in so the app and the IMAP/SMTP path agree.
install -d "$DROP_IN_DIR"
cat > "$DROP_IN" <<EOF
[Service]
Environment=MIAUTRIX_STORAGE_DIR=$PERSISTENT_DIR
EOF

systemctl daemon-reload
echo "==> Pinned MIAUTRIX_STORAGE_DIR=$PERSISTENT_DIR in $DROP_IN"
echo "==> The legacy directory is left in place as a backup; remove it once exports look correct."
