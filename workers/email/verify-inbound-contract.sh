#!/usr/bin/env sh
# Proves the inbound email() handler logic, wildcard allow-list, and webhook contract (MIA-77).
#
# Usage:
#   ./verify-inbound-contract.sh
#
# Runs unit and contract tests in Node.js, verifying:
#   1. Wildcard allow-list (*@miautrix.tech) and exact match (admin@miautrix.org)
#   2. Mixed-case and malformed/missing from rejection without throwing (no 1101)
#   3. Configurable destination (HTTP webhook vs email forward)
#   4. Webhook token authentication from secret (X-Miautrix-Inbound-Token)
#   5. Delivery failure handling (fail closed and loud, SMTP rejection / NDR)

set -e

DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$DIR"

echo "Running inbound email() handler contract test suite..."
node --loader ./test/loader.mjs --experimental-strip-types --test test/inbound-handler.test.ts

echo "Inbound email() contract tests completed successfully. Zero 1101 uncaught throws."
