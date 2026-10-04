---
name: verify-task
description: Run a blueprint task's verify command from the project root and report the outcome honestly. Use when finishing any task from tasks.json, or when asked whether a task is done.
---

# verify-task

Confirm a task is actually finished, rather than assumed finished.

## Procedure

1. Read the task's `verify` and `verify_from` from `blueprints/miautrix-mail-server/tasks.json`.
2. Run the verify command **from the project root** — not from the blueprint directory.
3. Report the exit code and the tail of the output.

## Reporting rules

- Exit 0 → the task passes. State it plainly.
- Exit non-zero → the task does **not** pass. Quote the failing output. Do not summarise it away.
- Command could not run at all (missing tool, missing service) → say so explicitly and label the
  task **unverified**, not passed.

**Never** report a task as complete when its verify command did not run or did not exit 0. A
blueprint's verify command is the whole reason the acceptance criterion is trustworthy.

## Also check

- Does the change respect the dependency direction? (`Domain` → nothing)
- Does every new tenant-scoped query pass through the authorization helper?
- Are new secrets excluded from logs?
- Were migrations written forward-only?
