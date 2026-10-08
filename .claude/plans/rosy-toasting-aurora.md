# Implementation Plan: Webmail Auto-Refresh and Manual Refresh

## Context
Users need a way to manually refresh their inboxes to check for new emails and a configurable auto-refresh mechanism to keep the UI up-to-date without manual intervention.

## Recommended Approach

### 1) Manual Refresh Button
- Add a "Refresh" button to the `wm-ribbon` (toolbar) in `InboxView.tsx`.
- The button will trigger the existing `refreshMessagesForFolder` logic in `App.tsx`.
- **Action**: Add `onRefreshMessages` callback to `InboxViewProps` and call it from the new toolbar button.

### 2) Auto-Refresh Logic
- Implement a polling mechanism in `App.tsx` using `setInterval`.
- The timer will trigger `refreshMessagesForFolder` for the currently active folder and mailbox.
- Ensure the timer is cleaned up on component unmount.

### 3) Configurable Refresh Interval
- Since there is no backend `Setting` entity specifically for "Refresh Interval" yet, this will be implemented as a **local storage preference** (client-side setting) to avoid unnecessary DB migrations for a UI preference.
- Add a "Refresh Interval" setting to `SieveRulesView.tsx` (which serves as the "Settings & Rules" tab).
- Use a combobox (select) with the following options:
  - Off (Disabled)
  - 1 Minute
  - 5 Minutes (Default)
  - 15 Minutes
  - 30 Minutes
- The selected value will be persisted in `window.localStorage`.
- `App.tsx` will read this value to determine the `setInterval` duration.

## Critical Files
- `webmail/src/App.tsx`: Implement `setInterval` logic, local storage reading, and provide the refresh callback.
- `webmail/src/components/InboxView.tsx`: Add the Refresh button to the ribbon.
- `webmail/src/components/SieveRulesView.tsx`: Add the refresh interval combobox to the settings UI.

## Verification
1. **Manual Refresh**: Click the new refresh button and verify that `webmailClient.getMessages` is called and the list updates.
2. **Auto-Refresh**: Set the interval to 1 minute, wait for the interval to pass, and verify the list refreshes automatically.
3. **Configuration**: Change the interval in the Settings tab, refresh the page, and verify the new interval is respected.
4. **Disabled State**: Set refresh to "Off" and verify no automatic API calls are made.
