# Calendar & Webmail Final Solution — 2026-10-03

## Status

**Closed for Miautrix Mail Server Version 1.0.**

This document records the final stabilization pass for the calendar invitation / Webmail issues that blocked closing Version 1.0.

## Issues Closed

1. **Public calendar RSVP page actions failed**
   - Symptom: opening `/api/v1/public/calendar/invitations/{token}` showed the new RSVP UI, but Accept/Tentative/Decline/Propose actions did not reliably submit.
   - Fix: the public page now uses link-style Accept/Tentative/Decline actions wired to the existing protected POST endpoint. `?response=accept|tentative|decline` still auto-submits.
   - File: `src/Miautrix.Mail.Web/Controllers/PublicCalendarController.cs`

2. **Reschedule proposal emails missed Accept/Decline links**
   - Symptom: organizer emails for proposed schedule changes could show fallback text instead of proposal action links.
   - Fix: reschedule responses generate and persist a fresh 30-day token, then include organizer proposal links:
     - `/api/v1/public/calendar/invitations/{token}/proposal?action=accept`
     - `/api/v1/public/calendar/invitations/{token}/proposal?action=decline`
   - File: `src/Miautrix.Mail.Application/Mail/CalendarService.cs`

3. **Schedule emails confused UTC and local display**
   - Symptom: notification schedule emails showed UTC-like times without a clear statement that users/calendar clients would view them in local timezone.
   - Fix: invitation and proposal emails explicitly label the human-readable time as UTC and include guidance that calendar clients and the web RSVP page display in the local PC timezone.
   - Files:
     - `src/Miautrix.Mail.Application/Mail/CalendarInvitationBuilder.cs`
     - `src/Miautrix.Mail.Application/Mail/CalendarService.cs`

4. **Webmail select-all delete white screen**
   - Symptom: selecting all inbox messages and deleting could leave the GUI white-screened.
   - Fix: bulk delete clears stale selected/detail message state, safely handles empty-folder refresh, and avoids unsafe `currentMessage` access after all messages are removed.
   - Files:
     - `webmail/src/components/InboxView.tsx`
     - `webmail/src/App.tsx`

## Implementation Notes

- Public RSVP links still submit by POST through JavaScript; no state-changing GET endpoints were introduced.
- Invitation tokens remain stored hashed and are only shown/used as raw tokens in generated links.
- Reschedule proposal resolution remains routed through `/proposal?action=accept|decline`.
- Calendar `.ics` behavior is unchanged; calendar clients handle local timezone rendering from event data.
- Empty inbox after bulk delete is treated as a valid state, not a transient fetch failure.

## Verification Completed

```bash
dotnet build Miautrix.Mail.sln -warnaserror
```

Result: **passed with 0 warnings and 0 errors**.

```bash
dotnet test tests/Miautrix.Mail.UnitTests/Miautrix.Mail.UnitTests.csproj --filter CalendarInvitationBuilder
```

Result: **passed — 6/6 tests**.

## Manual Post-Deploy Checklist

After deployment, verify:

1. Open a fresh public RSVP URL and click **Accept**, **Tentative**, and **Decline** links.
2. Open a public RSVP URL with `?response=accept`; confirm it auto-submits and shows `Response saved.`
3. Click **Request a different time**, submit proposed dates, and confirm organizer email includes Accept/Decline proposal links.
4. Confirm invitation and proposal emails clearly label the displayed schedule time as UTC and include local-PC timezone guidance.
5. In Webmail Inbox, select all visible messages and delete; confirm the empty folder renders without a white screen.

## Version 1.0 Closure

This final stabilization pass completes the user-visible calendar and Webmail blockers for Miautrix Mail Server Version 1.0. The PMI plan has been updated to mark Version 1.0 complete as of **2026-10-03**.
