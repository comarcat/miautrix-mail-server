# Webmail & Calendar Fixes Blueprint

## 1. Project Overview
Addressing specific functional, security, and UI UX issues across the webmail client and backend REST API identified during testing.

### Current state
- Rescheduling proposals incorrectly mutate events or fail serialization.
- Service accounts pollute the user contact picker.
- Calendar events lack RSVP visualization and in-place RSVP controls.
- Composer dropdown leaks unauthorized system addresses.
- Settings/Rules UI is outdated.

### Target state
- RSVP payloads bind correctly; proposals do not alter the main event.
- Composer "From" dropdown strictly filters to `<user_email>` + authorized shared mailboxes.
- Contact instances (email + calendar) use one `ContactPickerDialog` explicitly filtering `isService`.
- Unaccepted events style as tentative; clicks allow RSVP.
- Settings match the modern UI ribbon/sidebar design.

## 2. API Design & Delta
- `CalendarRsvpRequest`: Ensure `ProposedStartTime` and `ProposedEndTime` parse standard ISO8601 local strings (or normalize to UTC) mapped from `proposed_start_time` / `proposed_end_time`.
- `ComposerView.tsx`: Backend `/api/v1/mailboxes/{id}/send-as` or frontend filtering using `current_user` and `shared_mailboxes` with `write` delegation.

## 3. Build Order

**Step 1: Fix RSVP JSON Binding and Logic (Backend)**
- Inspect `src/Miautrix.Mail.Application/Mail/CalendarRsvpRequest.cs` (or equivalent). Fix `[JsonPropertyName]` casing or date formats. 
- Ensure `RespondToInvitationAsync` correctly saves the proposal to the attendee record but does NOT mutate the master `CalendarEvent` or re-trigger global `SendInvitationsAsync`.
- Verify: `dotnet test --filter Category=Calendar` passes.

**Step 2: Secure the Composer "From" Dropdown (Frontend/Backend)**
- In `webmail/src/components/ComposerView.tsx`, ensure the "From" select box only allows the logged-in user's email, plus any `SharedMailbox` entries from the API where the user's delegate permission includes `'write'`.
- Verify: Dropdown omits system addresses and unauthorized domains.

**Step 3: Unify and Filter Contact Picker (Frontend)**
- Update `webmail/src/components/ContactPickerDialog.tsx` and data source passing to strictly exclude accounts where `kind === 'service'`.
- Integrate `ContactPickerDialog` directly into `SchedulingActivitiesDialog.tsx` for calendar invites, matching the compose email flow.
- Verify: Service accounts do not appear in calendar or compose scenarios.

**Step 4: Calendar Event RSVP Styling and Actions (Frontend)**
- In `webmail/src/components/CalendarView.tsx`, read `attendees` matching the current user. If `responseStatus === 'needs_action'`, style the event block with a distinct color/border (e.g., striped overlay).
- Add a click-target or context menu on unaccepted events allowing the user to click "Accept", "Tentative", "Decline", or "Propose New Time".
- Verify: Backend RSVP endpoint is successfully called from UI.

**Step 5: Redesign Settings and Rules UI (Frontend)**
- Refactor `webmail/src/components/SieveRulesView.tsx` (and global settings) to use the `wm-ribbon`, `wm-sidebar`, `wm-content` layout found in `ContactsView.tsx`.
- Verify: `pnpm --filter webmail build` completes without TypeScript errors.