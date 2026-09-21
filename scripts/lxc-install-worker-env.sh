#!/usr/bin/env bash
# Provision /opt/miautrix-mail/.env and the SMTP/IMAP TLS material for the Worker.
#
# Run this ON THE CONTAINER, as root, with the cert and key already staged on disk:
#
#   ./lxc-install-worker-env.sh --cert /tmp/tls/cert.pem --key /tmp/tls/key.pem
#
# The database connection string is *inherited* from the running Web service rather than
# retyped, so the API and the Worker can never drift apart. Nothing here prints secret
# material to stdout.
#
# Cloudflare mode is optional. Pass --cloudflare-env-file with a file containing
# CLOUDFLARE_API_TOKEN=, CLOUDFLARE_API_BASE= and MIAUTRIX_INBOUND_TOKEN= to arm the
# Worker's outbound path and the Web app's inbound webhook. Values are read from that file,
# never from argv — argv is world-readable via ps and lands in shell history.
#
# Idempotent: re-running replaces the TLS pair and rewrites the managed block of the
# environment file while preserving any variables already defined above it.

set -euo pipefail

ENV_FILE=/opt/miautrix-mail/.env
TLS_DIR=/etc/ssl/miautrix
CERT_DEST="$TLS_DIR/cert.pem"
KEY_DEST="$TLS_DIR/key.pem"
STORAGE_DEFAULT=/opt/miautrix-mail/data/mail
SERVICE=miautrix-mail-worker
WEB_SERVICE=miautrix-mail
OWNER=www-data
CF_API_BASE_DEFAULT=https://api.cloudflare.com/client/v4

CERT_SRC=""
KEY_SRC=""
DB_FILE=""
CF_FILE=""
HOSTNAME_VALUE="mail.miautrix.tech"

usage() {
    sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-2}"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --cert)              CERT_SRC="$2"; shift 2 ;;
        --key)               KEY_SRC="$2";  shift 2 ;;
        --db-connection-file) DB_FILE="$2";  shift 2 ;;
        --cloudflare-env-file) CF_FILE="$2"; shift 2 ;;
        --hostname)          HOSTNAME_VALUE="$2"; shift 2 ;;
        -h|--help)           usage 0 ;;
        *)                   echo "unknown argument: $1" >&2; usage 2 ;;
    esac
done

[[ $EUID -eq 0 ]] || { echo "must run as root" >&2; exit 1; }
[[ -n "$CERT_SRC" && -r "$CERT_SRC" ]] || { echo "--cert is required and must be readable" >&2; exit 1; }
[[ -n "$KEY_SRC"  && -r "$KEY_SRC"  ]] || { echo "--key is required and must be readable" >&2; exit 1; }

# ── 0) Cloudflare credentials: from a file, never from argv ────────────────────

cf_api_token=""
cf_api_base=""
inbound_token=""

if [[ -n "$CF_FILE" ]]; then
    [[ -r "$CF_FILE" ]] || { echo "--cloudflare-env-file not readable: $CF_FILE" >&2; exit 1; }

    while IFS= read -r line || [[ -n "$line" ]]; do
        line="${line%$'\r'}"
        [[ -z "$line" || "$line" == \#* ]] && continue

        name="${line%%=*}"
        value="${line#*=}"
        # Tolerate NAME="value" as written by hand.
        value="${value%\"}"
        value="${value#\"}"

        case "$name" in
            CLOUDFLARE_API_TOKEN)   cf_api_token="$value" ;;
            CLOUDFLARE_API_BASE)    cf_api_base="$value" ;;
            MIAUTRIX_INBOUND_TOKEN) inbound_token="$value" ;;
            *)
                # A typo here would silently disarm the integration, so it is fatal.
                echo "    unknown key in --cloudflare-env-file: $name" >&2
                exit 1
                ;;
        esac
    done < "$CF_FILE"

    if [[ -z "$cf_api_token" && -z "$inbound_token" ]]; then
        echo "    --cloudflare-env-file defines neither CLOUDFLARE_API_TOKEN nor MIAUTRIX_INBOUND_TOKEN" >&2
        exit 1
    fi
    [[ -n "$cf_api_token" ]] || echo "    No CLOUDFLARE_API_TOKEN: outbound Cloudflare delivery stays disabled." >&2
    [[ -n "$inbound_token" ]] || echo "    No MIAUTRIX_INBOUND_TOKEN: the inbound webhook stays fail-closed." >&2
fi

# Re-running without the file must not silently disarm an armed deployment, so fall back to
# what is already on disk.
if [[ -z "$cf_api_token" && -f "$ENV_FILE" ]]; then
    cf_api_token=$(sed -n 's/^CLOUDFLARE_API_TOKEN=//p' "$ENV_FILE" | tail -n1 || true)
fi
if [[ -z "$cf_api_base" && -f "$ENV_FILE" ]]; then
    cf_api_base=$(sed -n 's/^CLOUDFLARE_API_BASE=//p' "$ENV_FILE" | tail -n1 || true)
fi
if [[ -z "$inbound_token" ]]; then
    existing_dropin="/etc/systemd/system/$WEB_SERVICE.service.d/10-miautrix-inbound.conf"
    if [[ -f "$existing_dropin" ]]; then
        inbound_token=$(
            sed -n 's/^Environment="MIAUTRIX_INBOUND_TOKEN=\(.*\)"$/\1/p' "$existing_dropin" \
                | head -n1 || true
        )
    fi
fi

[[ -n "$cf_api_token" ]] || cf_api_base=""

echo "==> Cloudflare integration: $(
    if [[ -n "$cf_api_token" ]]; then echo "armed"; else echo "not configured (outbound Cloudflare delivery stays disabled)"; fi
)"
[[ -n "$inbound_token" ]] || echo "    No inbound token: the webhook stays fail-closed (503) on every request."


# ── 1) TLS pair: validate, then install ────────────────────────────────────────

echo "==> Validating the certificate and key pair"

cert_modulus=$(openssl x509 -noout -modulus -in "$CERT_SRC" 2>/dev/null | openssl md5)
key_modulus=$(openssl rsa -noout -modulus -in "$KEY_SRC" 2>/dev/null | openssl md5)

if [[ -z "$cert_modulus" || "$cert_modulus" != "$key_modulus" ]]; then
    echo "    certificate and key do not match (or are not readable PEM)" >&2
    exit 1
fi

# Refuse to install a certificate that has already expired.
if ! openssl x509 -noout -checkend 0 -in "$CERT_SRC" >/dev/null 2>&1; then
    echo "    certificate is expired" >&2
    exit 1
fi

echo "    subject  : $(openssl x509 -noout -subject -in "$CERT_SRC" | sed 's/^subject= *//')"
echo "    issuer   : $(openssl x509 -noout -issuer  -in "$CERT_SRC" | sed 's/^issuer= *//')"
echo "    notAfter : $(openssl x509 -noout -enddate -in "$CERT_SRC" | sed 's/^notAfter=//')"

install -d -m 0755 "$TLS_DIR"
install -m 0644 -o root -g root       "$CERT_SRC" "$CERT_DEST"
install -m 0640 -o root -g "$OWNER"   "$KEY_SRC"  "$KEY_DEST"
echo "==> Installed $CERT_DEST (0644) and $KEY_DEST (0640 root:$OWNER)"

# ── 2) Database connection: inherit from the running Web service ───────────────

echo "==> Resolving MIAUTRIX_DB_CONNECTION"

db_connection=""
db_source=""

# 1) Already in the environment file.
if [[ -f "$ENV_FILE" ]]; then
    db_connection=$(sed -n 's/^MIAUTRIX_DB_CONNECTION=//p' "$ENV_FILE" | tail -n1 || true)
    [[ -n "$db_connection" ]] && db_source="$ENV_FILE"
fi

# 2) Supplied as a file (keeps the secret out of argv, shell history and ps output).
if [[ -z "$db_connection" && -n "$DB_FILE" ]]; then
    [[ -r "$DB_FILE" ]] || { echo "    --db-connection-file not readable: $DB_FILE" >&2; exit 1; }
    db_connection=$(tr -d '\r\n' < "$DB_FILE")
    [[ -n "$db_connection" ]] && db_source="$DB_FILE"
fi

# 3) Inherited from the Web service's systemd environment.
if [[ -z "$db_connection" ]]; then
    # systemd renders Environment= lines space-separated; pull the DB var out of them.
    db_connection=$(
        systemctl show -p Environment --value "$WEB_SERVICE" 2>/dev/null \
            | tr ' ' '\n' \
            | sed -n 's/^MIAUTRIX_DB_CONNECTION=//p' \
            | tr -d '"' | head -n1 || true
    )
    [[ -n "$db_connection" ]] && db_source="systemd environment of $WEB_SERVICE"
fi

# 4) Ask, if a human is attached.
if [[ -z "$db_connection" && -t 0 ]]; then
    echo "    Not configured anywhere. Paste the PostgreSQL connection string"
    echo "    (the same one $WEB_SERVICE uses), then press Enter."
    read -rs -p "    MIAUTRIX_DB_CONNECTION: " db_connection
    echo
    [[ -n "$db_connection" ]] && db_source="interactive input"
fi

if [[ -n "$db_connection" ]]; then
    echo "    resolved from $db_source (value not shown)"
else
    echo "    NOT RESOLVED — $WEB_SERVICE has no MIAUTRIX_DB_CONNECTION in its environment," >&2
    echo "    and none was supplied. The Worker cannot start without it." >&2
    echo "    Fix with either:" >&2
    echo "      $0 --db-connection-file /path/to/file --cert ... --key ..." >&2
    echo "      printf '%s' 'Host=...;Database=...' > $ENV_FILE" >&2
fi

# ── 3) Storage directory: prefer an already-pinned value ───────────────────────

storage_dir=$(
    systemctl show -p Environment --value "$WEB_SERVICE" 2>/dev/null \
        | tr ' ' '\n' \
        | sed -n 's/^MIAUTRIX_STORAGE_DIR=//p' \
        | tr -d '"' | head -n1 || true
)
[[ -n "$storage_dir" ]] || storage_dir="$STORAGE_DEFAULT"
echo "==> Storage directory: $storage_dir"

# ── 4) Write the environment file atomically ───────────────────────────────────

echo "==> Writing $ENV_FILE"

tmp_env=$(mktemp)
chmod 0600 "$tmp_env"

# Preserve any variable the operator added that we do not manage.
if [[ -f "$ENV_FILE" ]]; then
    grep -v -E '^(MIAUTRIX_DB_CONNECTION|MIAUTRIX_STORAGE_DIR|MIAUTRIX_HOSTNAME|MIAUTRIX_TLS_CERT_PATH|MIAUTRIX_TLS_KEY_PATH|CLOUDFLARE_API_TOKEN|CLOUDFLARE_API_BASE)=' "$ENV_FILE" \
        | grep -v -E '^# Managed by lxc-install-worker-env.sh' \
        >> "$tmp_env" || true
fi

{
    echo "# Managed by lxc-install-worker-env.sh — edit by re-running the script, not by hand."
    if [[ -n "$db_connection" ]]; then
        echo "MIAUTRIX_DB_CONNECTION=$db_connection"
    else
        # Left commented so the Worker fails loudly and this line shows what is missing.
        echo "# MIAUTRIX_DB_CONNECTION=Host=...;Database=...;Username=...;Password=..."
    fi
    echo "MIAUTRIX_STORAGE_DIR=$storage_dir"
    echo "MIAUTRIX_HOSTNAME=$HOSTNAME_VALUE"
    echo "MIAUTRIX_TLS_CERT_PATH=$CERT_DEST"
    echo "MIAUTRIX_TLS_KEY_PATH=$KEY_DEST"
    if [[ -n "$cf_api_token" ]]; then
        echo "CLOUDFLARE_API_TOKEN=$cf_api_token"
        echo "CLOUDFLARE_API_BASE=${cf_api_base:-$CF_API_BASE_DEFAULT}"
    fi
} >> "$tmp_env"

install -m 0640 -o root -g "$OWNER" "$tmp_env" "$ENV_FILE"
rm -f "$tmp_env"
echo "==> Installed $ENV_FILE (0640 root:$OWNER)"

# ── 5) Web service: install the inbound webhook token ─────────────────────────

# The inbound webhook is served by the Web app (miautrix-mail.service), not the Worker, so
# its shared secret must live in that unit's environment. A drop-in is used so the operator
# does not edit the packaged unit file directly.
if [[ -n "$inbound_token" ]]; then
    echo "==> Installing MIAUTRIX_INBOUND_TOKEN into $WEB_SERVICE"

    load_state=$(systemctl show -p LoadState --value "$WEB_SERVICE" 2>/dev/null || true)
    if [[ "$load_state" != "loaded" ]]; then
        echo "    $WEB_SERVICE is not a loaded systemd unit; cannot arm the inbound webhook" >&2
        exit 1
    fi

    dropin_dir="/etc/systemd/system/$WEB_SERVICE.service.d"
    dropin="$dropin_dir/10-miautrix-inbound.conf"
    install -d -m 0755 "$dropin_dir"

    tmp_dropin=$(mktemp)
    chmod 0600 "$tmp_dropin"
    {
        echo "# Managed by lxc-install-worker-env.sh — edit by re-running the script, not by hand."
        echo "[Service]"
        echo "Environment=\"MIAUTRIX_INBOUND_TOKEN=$inbound_token\""
    } > "$tmp_dropin"
    install -m 0600 -o root -g root "$tmp_dropin" "$dropin"
    rm -f "$tmp_dropin"
    echo "    Installed $dropin (0600 root:root)"

    systemctl daemon-reload
    systemctl restart "$WEB_SERVICE"
    sleep 2
    if systemctl is-active --quiet "$WEB_SERVICE"; then
        echo "==> $WEB_SERVICE restarted with the inbound webhook armed"
    else
        echo "==> $WEB_SERVICE failed to start. Last log lines:" >&2
        journalctl -u "$WEB_SERVICE" --no-pager -n 40 >&2
        exit 1
    fi
else
    echo "==> No MIAUTRIX_INBOUND_TOKEN supplied; the inbound webhook stays fail-closed (503)"
fi

# ── 6) Report anything already holding the mail ports ─────────────────────────

echo "==> Checking ports 25/465/587/993 for conflicts"
conflicts=""
for port in 25 465 587 993; do
    holder=$(ss -lntpH 2>/dev/null | grep -E "[:.]$port\b" || true)
    if [[ -n "$holder" ]]; then
        conflicts="yes"
        echo "    $port is already held by:"
        printf '      %s\n' "$holder"
    fi
done

if [[ -n "$conflicts" ]]; then
    echo "    The Worker cannot bind a port another process holds. If these are Postfix/Dovecot," >&2
    echo "    stop and disable them first:" >&2
    echo "      systemctl disable --now postfix dovecot 2>/dev/null || true" >&2
fi

# ── 6) Restart ────────────────────────────────────────────────────────────────

echo "==> Restarting $SERVICE"
systemctl daemon-reload
systemctl restart "$SERVICE"

sleep 2
if systemctl is-active --quiet "$SERVICE"; then
    echo "==> $SERVICE is active"
    ss -lntup 2>/dev/null | grep -E ':(25|465|587|993)\b' || \
        echo "    WARNING: no listener bound on 25/465/587/993 yet — check: journalctl -u $SERVICE -n 50"
    exit 0
fi

echo "==> $SERVICE is not running. Last log lines:" >&2
journalctl -u "$SERVICE" --no-pager -n 40 >&2

if [[ -z "$db_connection" ]]; then
    # Expected: the missing variable is the reported cause, not a script failure.
    echo "==> Expected while MIAUTRIX_DB_CONNECTION is unset. Re-run with --db-connection-file." >&2
    exit 0
fi

exit 1
