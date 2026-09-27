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

# Rich HTML body (multipart/alternative): colors, font sizes, table, lists.
# Keep it simple but clearly formatted so we can verify BodyHtml rendering.
html_body = """<div style='font-family: Arial, sans-serif; color:#0f172a;'>
  <h2 style='margin:0 0 12px; color:#4f46e5;'>Miautrix Webmail HTML Render Test</h2>
  <p style='font-size:14px; color:#334155;'>This message includes <span style='color:#ef4444; font-weight:bold;'>colors</span>,
  <span style='color:#0ea5e9; font-weight:bold;'>sizes</span>, and structured content.</p>

  <table style='border-collapse:collapse; width:100%; max-width:520px; margin:14px 0; font-size:13px;'>
    <tr>
      <th style='border:1px solid #cbd5e1; padding:8px; background:#f1f5f9; text-align:left;'>Item</th>
      <th style='border:1px solid #cbd5e1; padding:8px; background:#f1f5f9; text-align:left;'>Value</th>
    </tr>
    <tr>
      <td style='border:1px solid #cbd5e1; padding:8px;'>Tenant</td>
      <td style='border:1px solid #cbd5e1; padding:8px;'>{{miautrix}}</td>
    </tr>
    <tr>
      <td style='border:1px solid #cbd5e1; padding:8px;'>Format</td>
      <td style='border:1px solid #cbd5e1; padding:8px;'>multipart/alternative</td>
    </tr>
  </table>

  <h3 style='font-size:13px; margin:14px 0 8px; color:#0f766e;'>List</h3>
  <ul style='margin:0 0 14px 18px; color:#1f2937; font-size:13px;'>
    <li>Bullet one</li>
    <li>Bullet two</li>
    <li>Bullet three</li>
  </ul>

  <p style='font-size:13px; color:#475569;'>End of HTML test.</p>
</div>"""

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

# HTML part (multipart/mixed sibling; backend parser looks for first text/html part)
lines.append(f"--{boundary}")
lines.append("Content-Type: text/html; charset=utf-8")
lines.append("Content-Transfer-Encoding: 8bit")
lines.append('')
lines.append(html_body)
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
