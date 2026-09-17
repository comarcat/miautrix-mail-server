# Rule: secrets and logging

## Never log

Passwords · TOTP secrets · session cookies · refresh tokens · API secrets · private keys ·
application passwords · full message bodies (unless explicitly enabled for controlled diagnostics).

Mask sensitive identifiers.

The redaction list lives **in the logger itself**, not at each call site. A redaction list spread
across call sites will be forgotten at exactly the call site that matters.

## Never store in plaintext

Invitation tokens and API keys are hashed. Shown once, at creation.

## Never commit

`.env`, certificates, private keys, or anything in the deny list in `.claude/settings.json`.
Commit `.env.example` with placeholders only.

## Environment

Parse and validate every environment variable at boot. **Crash on missing.** A defaulted empty
string turns a misconfiguration into a mystery three hours later.
