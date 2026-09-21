#!/usr/bin/env bash
# Inject a test email to flow@miautrix.tech with an empty .txt attachment.
#
# Requires:
#   - swaks (https://github.com/bruceg/swaks) OR a compatible swaks binary in PATH
#   - working SMTP credentials for *some* user in the tenant that owns miautrix.tech
#
# Usage (recommended):
#   export SMTP_HOST="mail.miautrix.tech"
#   export SMTP_PORT="587"
#   export SMTP_USERNAME="user@miautrix.tech"
#   export SMTP_PASSWORD="..."   # NOT logged
#   export SMTP_FROM="user@miautrix.tech"   # optional (defaults to SMTP_USERNAME)
#   ./scripts/inject-flow-mail-test.sh
#
# Optional:
#   export TEST_SUBJECT="[miautrix] ZIP download attachment test"
#
# Output:
#   - prints swaks result
#
set -euo pipefail

TO_ADDRESS="flow@miautrix.tech"

: "${SMTP_HOST:=mail.miautrix.tech}"
: "${SMTP_PORT:=587}"
: "${SMTP_USERNAME:?SMTP_USERNAME is required (SMTP auth username)}"
: "${SMTP_PASSWORD:?SMTP_PASSWORD is required (SMTP auth password)}"
: "${SMTP_FROM:=${SMTP_USERNAME}}"
: "${TEST_SUBJECT:='[miautrix] ZIP download attachment test'}"

if ! command -v swaks >/dev/null 2>&1; then
  echo "Error: swaks not found in PATH. Install swaks or provide a swaks binary." >&2
  exit 1
fi

tmpdir="$(mktemp -d)"
cleanup() { rm -rf "$tmpdir"; }
trap cleanup EXIT

BOUNDARY="----=_miautrix_flow_test_$(date +%s)"
MSGID="$(python3 - <<'PY'
import uuid
print(uuid.uuid4())
PY
)@${SMTP_HOST}"

message_file="$tmpdir/message.eml"
python3 - "$message_file" "$BOUNDARY" "$MSGID" "$SMTP_FROM" "$TO_ADDRESS" "$TEST_SUBJECT" <<'PY'
import sys
path, boundary, msgid, from_addr, to_addr, subject = sys.argv[1:]
# Build CRLF explicitly for wire correctness.
crlf='\r\n'
body_text='This is a test message to verify ZIP export downloads.'

lines=[]
lines.append(f"From: {from_addr}")
lines.append(f"To: {to_addr}")
lines.append(f"Subject: {subject}")
lines.append(f"Message-ID: <{msgid}>")
lines.append("MIME-Version: 1.0")
lines.append(f"Date: (UTC)" )
lines.append(f"Content-Type: multipart/mixed; boundary=\"{boundary}\"")
lines.append('')

# Text part
lines.append(f"--{boundary}")
lines.append("Content-Type: text/plain; charset=utf-8")
lines.append("Content-Transfer-Encoding: 8bit")
lines.append('')
lines.append(body_text)
lines.append('')

# Empty attachment part (0 bytes, base64 is empty)
lines.append(f"--{boundary}")
lines.append("Content-Type: text/plain; name=\"empty.txt\"")
lines.append("Content-Disposition: attachment; filename=\"empty.txt\"")
lines.append("Content-Transfer-Encoding: base64")
lines.append('')
# Intentionally empty payload for an empty file.
lines.append('')

# End boundary
lines.append(f"--{boundary}--")
lines.append('')

content=crlf.join(lines)
with open(path,'wb') as f:
    f.write(content.encode('utf-8'))
PY

echo "Injecting to $TO_ADDRESS via SMTP ${SMTP_HOST}:${SMTP_PORT} ..."

# swaks will generate/handle the SMTP envelope; message headers come from message_file.
# NOTE: we do NOT echo SMTP_PASSWORD.
set +x
swaks \
  --server "$SMTP_HOST" \
  --port "$SMTP_PORT" \
  --tls \
  --auth LOGIN \
  --auth-user "$SMTP_USERNAME" \
  --auth-password "$SMTP_PASSWORD" \
  --from "$SMTP_FROM" \
  --to "$TO_ADDRESS" \
  --data "$message_file" \
  --quit-after RCPT \
  --timeout 30
