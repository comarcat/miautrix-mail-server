#!/usr/bin/env sh
# Proves the POST /send contract of workers/email/src/index.ts (MIA-76).
#
# Usage:
#   SEND_TOKEN=<token> BASE=http://127.0.0.1:8799 ./verify-send-contract.sh
#
# BASE defaults to the local `npx wrangler dev --local --port 8799` address.
# SEND_TOKEN is read from the environment and is never written to this file or to the output.
#
# Exits 0 only when every case matched. Prints one line per case: status, expectation, verdict.
# Deliberately POSIX sh with curl only — no new dependency, nothing paid.

set -u

BASE="${BASE:-http://127.0.0.1:8799}"
TOKEN="${SEND_TOKEN:-}"

if [ -z "$TOKEN" ]; then
  echo "SEND_TOKEN is not set. Export it from your secret store; do not paste it into a file." >&2
  exit 2
fi

fails=0
cases=0

# probe <name> <expected-status> <curl args...>
probe() {
  name="$1"
  expected="$2"
  shift 2
  cases=$((cases + 1))
  body_file="$(mktemp)"
  got="$(curl -s -o "$body_file" -w '%{http_code}' --max-time 20 "$@")"
  body="$(cut -c1-160 < "$body_file" | tr -d '\n')"
  rm -f "$body_file"

  case "$body" in
    *1101*)
      verdict="FAIL (1101)"
      fails=$((fails + 1))
      ;;
    *)
      if [ "$got" = "$expected" ]; then
        verdict="PASS"
      else
        verdict="FAIL"
        fails=$((fails + 1))
      fi
      ;;
  esac
  printf '%-4s %-48s expected %-3s got %-3s  %s\n' "$verdict" "$name" "$expected" "$got" "$body"
}

echo "POST /send contract against $BASE"
echo

# --- Unchanged paths. Regression guard for what QA already verified. ---
probe "GET / -> 200 {ok:true}"                 200 "$BASE/"
probe "GET /health -> 200 {ok:true}"           200 "$BASE/health"
probe "GET /nope -> 404"                       404 "$BASE/nope"

# --- Method restrictions (405). ---
probe "GET /send -> 405"                       405 "$BASE/send"
probe "POST /health -> 405"                    405 -X POST "$BASE/health"

# --- 401: authentication runs BEFORE the body is read. ---
probe "no Authorization header"                 401 -X POST "$BASE/send" \
  -H 'Content-Type: application/json' --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'
probe "wrong bearer token"                      401 -X POST "$BASE/send" \
  -H 'Authorization: Bearer definitely-not-the-token' \
  -H 'Content-Type: application/json' --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'
probe "wrong scheme (Basic)"                    401 -X POST "$BASE/send" \
  -H 'Authorization: Basic dXNlcjpwYXNz' \
  -H 'Content-Type: application/json' --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'
probe "empty bearer"                            401 -X POST "$BASE/send" \
  -H 'Authorization: Bearer ' \
  -H 'Content-Type: application/json' --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'
# The decisive ordering case: an invalid body plus a bad token must answer 401, not 400.
# A 400 here would prove the body was parsed before the caller was authenticated.
probe "bad token + invalid body -> 401 not 400"  401 -X POST "$BASE/send" \
  -H 'Authorization: Bearer definitely-not-the-token' \
  -H 'Content-Type: application/json' --data 'this is not json at all'
probe "bad token + no body -> 401 not 400"       401 -X POST "$BASE/send" \
  -H 'Authorization: Bearer definitely-not-the-token'

# --- 415: Unsupported media type. ---
probe "authenticated, non-JSON Content-Type"    415 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: text/plain' --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'

# --- 400: bad request shapes. ---
probe "authenticated, no body"                   400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json'
probe "authenticated, empty JSON object {}"      400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' --data '{}'
probe "authenticated, non-JSON body"             400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' --data 'not json'
probe "authenticated, JSON array not object"     400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' --data '[1,2,3]'
probe "authenticated, JSON null"                 400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' --data 'null'
probe "raw malformed (not base64)"               400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"d@e.f","raw":"!!!not-base64!!!"}'
probe "raw missing"                              400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"d@e.f"}'
probe "raw not a string"                         400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"d@e.f","raw":12345}'
probe "raw empty string"                         400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"d@e.f","raw":""}'
probe "raw without MIME CRLF gap"                400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@miautrix.tech","to":"d@example.com","raw":"SGk="}'
probe "from missing"                             400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"to":"d@e.f","raw":"aGk="}'
probe "from not an address"                      400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"nope","to":"d@e.f","raw":"aGk="}'
probe "to missing"                               400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","raw":"aGk="}'
probe "to not an address"                        400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"not-an-address","raw":"aGk="}'
probe "to is null"                               400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":null,"raw":"aGk="}'

# --- 413: Payload too large. ---
BIG_JSON_FILE="$(mktemp)"
# Construct raw > 35MB base64 string
python3 -c "
import json
hdr = 'RnJvbTogYUBtaWF1dHJpeC50ZWNoDQpUbzogZEBleGFtcGxlLmNvbQ0KU3ViamVjdDogYmlnDQoNCg=='
# Pad raw to > 34,952,534 characters
padding = 'AAAA' * 8740000
with open('$BIG_JSON_FILE', 'w') as f:
    json.dump({'from': 'a@miautrix.tech', 'to': 'd@example.com', 'raw': hdr + padding}, f)
"
probe "oversized raw body (> 25MB decoded)"     413 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data-binary @"$BIG_JSON_FILE"
rm -f "$BIG_JSON_FILE"

# --- Well-formed and authenticated: reaches env.EMAIL.send(). ---
# Against a worker with a mock send_email binding or local wrangler dev with Message-ID,
# success returns 202. A send failure (e.g. invalid Message-ID or provider error) returns 502.
probe "well-formed+authed -> reaches send" "${WELL_FORMED_EXPECT:-202}" -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"noreply@miautrix.tech","to":"noreply@miautrix.tech","raw":"RnJvbTogbm9yZXBseUBtaWF1dHJpeC50ZWNoDQpUbzogbm9yZXBseUBtaWF1dHJpeC50ZWNoDQpTdWJqZWN0OiBNSUEtNzYgY29udHJhY3QgcHJvYmUNCk1lc3NhZ2UtSUQ6IDxtaWE3Ni1wcm9iZUBtaWF1dHJpeC50ZWNoPg0KRGF0ZTogU3VuLCAwNCBPY3QgMjAyNiAxMTowMDowMCArMDAwMA0KDQpDb250cmFjdCBwcm9iZSBib2R5Lg0K"}'

echo
echo "$cases cases, $fails failed."
[ "$fails" -eq 0 ] || exit 1
echo "All cases matched. Zero 1101 responses: no case returned a Cloudflare error page."
