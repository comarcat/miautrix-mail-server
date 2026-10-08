# Queue Race Fix Workspace Notes

This bundle changes an existing repository. Merge these notes with the repository's existing `CLAUDE.md`; do not overwrite project instructions.

Implementation constraints:
- Keep Cloudflare routing, DNS, WAF, token validation, and antispam/antimalware unchanged.
- Use EF Core migrations for all schema changes.
- Preserve tenant isolation and never log secrets or full message bodies.
- Run `dotnet build Miautrix.Mail.sln -warnaserror` and `dotnet test` before completion.
