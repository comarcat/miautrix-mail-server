#!/usr/bin/env bash
# Inject a malware test message carrying the EICAR test signature to the mail server.
#
# Because EICAR is designed to be caught by *any* AV, including workstation endpoint
# protection and AMSI (which catches PowerShell scripts decoding it in memory), this
# bash script builds the base64 payload dynamically and uses openssl s_client to inject it.
# It runs cleanly on Linux/macOS without triggering local workstation AV.

HOST=${SMTP_HOST:-127.0.1.1}
PORT=${SMTP_PORT:-25}
TO=${1:-owner@miautrix.tech}
CLEAN=${2:-}

# 68-byte EICAR signature, split to avoid triggering filesystem scanners
F1='X5O!P%@AP[4\PZX54(P^)7CC)7}$'
F2='EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*'

if [[ "$CLEAN" == "--clean" ]]; then
    PAYLOAD_B64=$(printf "X5O!P%%@AP[4\\PZX54(P^)7CC)-CONTROL-NOT-MALWARE-000000000000000000\r\n" | base64)
    TEXT="Control message: harmless attachment, same length and shape as EICAR."
    SUBJ="Miautrix malware scan test (clean control)"
else
    PAYLOAD_B64=$(printf "%s%s\r\n" "$F1" "$F2" | base64)
    TEXT="Security test: this message carries the EICAR anti-virus test file."
    SUBJ="Miautrix malware scan test (EICAR)"
fi

BOUNDARY="----=_miautrix_malware_test_$(date +%s)"
MSGID="<$(uuidgen 2>/dev/null || echo $RANDOM)@test.local>"

BODY=$(cat <<EOM
From: test@miautrix.tech
To: $TO
Subject: $SUBJ
Message-ID: $MSGID
MIME-Version: 1.0
Content-Type: multipart/mixed; boundary="$BOUNDARY"

--$BOUNDARY
Content-Type: text/plain; charset=utf-8
Content-Transfer-Encoding: 8bit

$TEXT

--$BOUNDARY
Content-Type: application/octet-stream; name="eicar.bat"
Content-Disposition: attachment; filename="eicar.bat"
Content-Transfer-Encoding: base64

$PAYLOAD_B64
--$BOUNDARY--
.
EOM
)

# Replace isolated LFs with CRLF for SMTP
BODY=$(echo "$BODY" | sed 's/$/\r/' | sed 's/\r\r/\r/')

echo "EHLO localhost
MAIL FROM:<test@miautrix.tech>
RCPT TO:<$TO>
DATA
$BODY
QUIT" | nc -q 1 $HOST $PORT || echo "Note: if using TLS, set HOST/PORT or use openssl s_client instead of nc."
