# Rule: tenant isolation

Every tenant-scoped read and write passes through the single authorization helper. There is no
exception and no "this one is internal".

- Roles bind to **memberships**, never to users. A user's role is a property of their membership
  in a tenant.
- Permissions are checked by string (`mailbox.create`), never by `role === 'admin'`.
- A resource belonging to another tenant returns **404**, never 403. A 403 confirms the resource
  exists and turns an ID into an oracle.
- Record **failed** authorization attempts, not only successes.
- The last owner of a tenant cannot be removed or demoted.
- The global query filter is defence in depth. It is not the enforcement point, and relying on it
  alone is a defect.
