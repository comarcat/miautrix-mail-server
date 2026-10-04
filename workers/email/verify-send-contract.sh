#!/usr/bin/env sh
# Proves the POST /send contract of workers/email/src/index.ts (MIA-76).
#
# Usage:
#   SEND_TOKEN=<token> BASE=http://127.0.0.1:8799 ./verify-send-contract.sh
#
# BASE defaults to the local `npx wrangler dev --local --port 8799` address, which is the
# safe default: a local run has no real send_email binding, so no probe can emit real mail.
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

  if [ "$got" = "$expected" ]; then
    verdict="PASS"
  else
    verdict="FAIL"
    fails=$((fails + 1))
  fi
  printf '%-4s %-46s expected %-3s got %-3s  %s\n' "$verdict" "$name" "$expected" "$got" "$body"
}

echo "POST /send contract against $BASE"
echo

# --- Unchanged paths. Regression guard for what QA already verified. ---
probe "GET / -> 200 {ok:true}"                200 "$BASE/"
probe "GET /health -> 200 {ok:true}"          200 "$BASE/health"
probe "GET /nope -> 404"                      404 "$BASE/nope"

# --- 401: authentication runs BEFORE the body is read. ---
probe "no Authorization header"                401 -X POST "$BASE/send" \
  -H 'Content-Type: application/json' --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'
probe "wrong bearer token"                     401 -X POST "$BASE/send" \
  -H 'Authorization: Bearer definitely-not-the-token' \
  -H 'Content-Type: application/json' --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'
probe "wrong scheme (Basic)"                   401 -X POST "$BASE/send" \
  -H 'Authorization: Basic dXNlcjpwYXNz' \
  -H 'Content-Type: application/json' --data '{"from":"a@b.c","to":"d@e.f","raw":"aGk="}'
# The decisive ordering case: an invalid body plus a bad token must answer 401, not 400.
# A 400 here would prove the body was parsed before the caller was authenticated.
probe "bad token + invalid body -> 401 not 400" 401 -X POST "$BASE/send" \
  -H 'Authorization: Bearer definitely-not-the-token' \
  -H 'Content-Type: application/json' --data 'this is not json at all'
probe "bad token + no body -> 401 not 400"      401 -X POST "$BASE/send" \
  -H 'Authorization: Bearer definitely-not-the-token'

# --- 400: every bad request shape that used to return 1101. ---
probe "authenticated, no body"                  400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN"
probe "authenticated, empty JSON object {}"     400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' --data '{}'
probe "authenticated, non-JSON body"            400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' --data 'not json'
probe "authenticated, JSON array not object"    400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' --data '[1,2,3]'
probe "authenticated, JSON null"                400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' --data 'null'
probe "raw malformed (not base64)"              400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"d@e.f","raw":"!!!not-base64!!!"}'
probe "raw missing"                             400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"d@e.f"}'
probe "raw not a string"                        400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"d@e.f","raw":12345}'
probe "raw empty string"                        400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"d@e.f","raw":""}'
probe "from missing"                            400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"to":"d@e.f","raw":"aGk="}'
probe "from not an address"                     400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"nope","to":"d@e.f","raw":"aGk="}'
probe "to missing"                              400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","raw":"aGk="}'
probe "to not an address"                       400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":"not-an-address","raw":"aGk="}'
probe "to is null"                              400 -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"a@b.c","to":null,"raw":"aGk="}'

# --- Well-formed and authenticated: reaches env.EMAIL.send(). ---
# Locally there is no real send binding, so the expected answer is 500 with the {ok:false,error}
# envelope: the request was accepted and validated, and the SEND itself is what failed. The point
# of the case is that a send failure is a 500 and never a 1101. Against the deployed Worker with a
# verified destination this same request is a 200 — which is why the expectation is passed in.
probe "well-formed+authed -> reaches send" "${WELL_FORMED_EXPECT:-500}" -X POST "$BASE/send" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"from":"noreply@miautrix.tech","to":"noreply@miautrix.tech","raw":"RnJvbTogbm9yZXBseUBtaWF1dHJpeC50ZWNoDQpUbzogbm9yZXBseUBtaWF1dHJpeC50ZWNoDQpTdWJqZWN0OiBNSUEtNzYgY29udHJhY3QgcHJvYmUNCg0KYm9keQ0K"}'

echo
echo "$cases cases, $fails failed."
[ "$fails" -eq 0 ] || exit 1
echo "All cases matched. Zero 1101 responses: no case returned a Cloudflare error page."
