# Project Update Checklist

Use this checklist whenever a feature, fix, milestone, or production verification is completed so project records stay synchronized.

## Always review/update

| Area | File | When to update |
|---|---|---|
| PMI project plan | `PMI_Project_Charter_and_Plan.md` | Milestones, task status, quality gates, verification results, project change log. |
| Errors / issues / bugs log | `ERRORS_AND_ISSUES.md` | Any fixed bug, known issue, deployment problem, production discrepancy, or item needing later verification. |
| Webmail design notes | `DESIGN-WEBMAIL.md` | Webmail UX/component behavior, visual implementation notes, completed UI capabilities. |
| Admin design notes | `DESIGN-Admin.md` | Admin UI/component behavior, visual implementation notes, completed Admin capabilities. |
| Project status memory | `C:\Users\miauadmin\.claude\projects\C--Users-miauadmin-OneDrive-Documentos-GitHub-miautrix-mail-server\memory\project-status.md` | Durable project milestone/status facts that should be remembered across sessions. |
| Memory index | `C:\Users\miauadmin\.claude\projects\C--Users-miauadmin-OneDrive-Documentos-GitHub-miautrix-mail-server\memory\MEMORY.md` | Add one-line pointer when creating a new memory file. |
| Dedicated memory file | `C:\Users\miauadmin\.claude\projects\C--Users-miauadmin-OneDrive-Documentos-GitHub-miautrix-mail-server\memory\*.md` | Non-obvious durable project facts, completed milestones, constraints, or backlog items. Create/update one fact per file. |
| Verification skill | `.claude/skills/verify-task/SKILL.md` | If verification procedure/checklist changes or a new recurring verification concern appears. |
| Repo instructions/rules | `CLAUDE.md`, `.claude/rules/*.md` | If the way future agents must work changes, especially security, tenancy, migrations, deployment, or logging rules. |
| Blueprint/tasks docs | `blueprints/miautrix-mail-server/**/*.md`, `blueprints/miautrix-mail-server/tasks.json` | If the change completes, alters, or supersedes a blueprint task or acceptance criterion. |

## Update process

1. Record the code/user-facing change in the relevant design or architecture document.
2. Record bugs/fixes/known gaps in `ERRORS_AND_ISSUES.md`.
3. Update `PMI_Project_Charter_and_Plan.md`:
   - Active task status if applicable.
   - Quality gate if a new acceptance rule exists.
   - Change Log entry with verification commands.
4. Update memory:
   - Use existing memory if it already covers the topic.
   - Create a new memory only for durable, non-obvious facts.
   - Add the new memory to `MEMORY.md`.
5. Update `.claude/skills/verify-task/SKILL.md` only when future verification behavior should change.
6. Report a table of all updated files to the user.

## Recent example: Webmail contacts/address book

When Webmail contacts and address-book picker were completed, the following records were updated:

| File | Reason |
|---|---|
| `DESIGN-WEBMAIL.md` | Added implementation notes for contacts, Company Directory, and recipient picker. |
| `ERRORS_AND_ISSUES.md` | Added FE-04, FE-05, FE-06 fixed issues. |
| `PMI_Project_Charter_and_Plan.md` | Updated #16, quality gates, and change log. |
| `memory/webmail-contacts-directory-picker-complete.md` | Added durable project memory. |
| `memory/MEMORY.md` | Indexed the new memory. |
| `memory/project-status.md` | Updated Webmail milestone status. |
| `.claude/skills/verify-task/SKILL.md` | Added Webmail composer/contact verification reminder. |
