# Integration Fixtures — v2.3 Report

## Findings and fixes

All twelve failures were fixture debt; none exposed a product behavior defect. The regression assertions for mailbox limits, message delivery, backup row counts, calendar content, invitations, and authorization remain intact.

| Test | Root cause and evidence | Fix | Product bug? |
|---|---|---|---|
| `CliTests.When_authorized_operator_lists_mailboxes_cli_exits_zero` | `Cli.Program.Main` resolves storage through `MIAUTRIX_STORAGE_DIR`; `StorageDirectoryResolver` rejects a missing path outside Development. The test called `Main` without setting it. | Run each CLI invocation with a unique temporary storage path, then restore the prior environment value and remove the temporary directory. | No |
| `CliTests.When_system_info_command_executes_cli_exits_zero` | Same missing storage-path assumption; even `system info` constructs the CLI service provider before dispatching the command. | Same scoped temporary storage setup. | No |
| `CliTests.When_unauthorized_user_lists_mailboxes_cli_exits_nonzero` | Same missing storage-path assumption obscured the intended authorization result. | Same scoped temporary storage setup. | No |
| `LicensingQuotaTests.TenantOverAllowance_BlocksNewMailboxes_ContinuesMailDelivery_PreservesData` | The test used EF InMemory, while `LicenseQuotaEnforcer.DeliverMessageAsync` calls `MessageUidAllocator`, which relies on PostgreSQL operations. | Use an isolated, migrated PostgreSQL database; assign unique tenant/mailbox data and clean up rows. Keep the allowance, delivery, UID, and preservation assertions unchanged. | No |
| `BackupRestoreTests.CreateAndRestoreBackup_ProducesMatchingRowCounts` | The source and target used EF InMemory even though restore allocates message UIDs through the PostgreSQL allocator. | Use separate isolated, migrated PostgreSQL source and target databases. The source remains dedicated so backup row counts are deterministic. | No |
| `CalendarOutboundMimeTests.An_external_invitee_receives_a_multipart_message_with_the_calendar_part` | `CalendarTestHelpers.SeedMailboxAsync` seeded the mailbox but no system folders; the calendar send path then lacked the expected Sent-folder fixture. After folder seeding was corrected, the MIME header assertion also failed because it expected a folded `name` parameter to remain on the preceding line. | Seed the standard mailbox folders, including Sent. Unfold MIME continuation lines while preserving their separating whitespace before checking the exact calendar content-type and filename parameters. | No |
| `CalendarOutboundMimeTests.The_plain_text_part_is_decodable_and_names_the_agenda` | The calendar mailbox fixture did not include the standard Sent folder required by the invitation send scenario. | Seed standard mailbox folders. | No |
| `CalendarOutboundMimeTests.Deleting_a_meeting_queues_a_cancellation_without_an_rsvp_link` | The create/send portion of this scenario used a mailbox fixture without standard folders. | Seed standard mailbox folders. | No |
| `CalendarOutboundMimeTests.Resending_invitations_queues_the_message_again` | The initial invitation send used a mailbox fixture without standard folders. | Seed standard mailbox folders. | No |
| `CalendarInvitationEmailTests.Rsvp_links_are_built_from_the_configured_public_base_url` | The invitation send scenario used a mailbox fixture without standard folders. | Seed standard mailbox folders. | No |
| `CalendarInvitationEmailTests.The_invitation_is_sent_from_the_organizer_to_the_invitee` | The invitation send scenario used a mailbox fixture without standard folders. | Seed standard mailbox folders. | No |
| `CalendarInvitationEmailTests.A_room_invitee_is_never_emailed_while_the_human_invitee_is` | The external human invitation send used a mailbox fixture without standard folders. | Seed standard mailbox folders. | No |

The other two `CalendarInvitationEmailTests` cases intentionally do not require a Sent folder: the no-public-URL case expects a clear 400, and the per-event opt-out case sends no invitation. Both pass unchanged.

## Test evidence

- `dotnet build Miautrix.Mail.sln -warnaserror -m:1`: passed with 0 warnings and 0 errors.
- The targeted calendar classes passed 9/9 on a fresh fully migrated database.
- Two consecutive full IntegrationTests runs on separate fresh databases passed 149/149 each, with 0 skipped: runs 3 and 4 in the verification sequence.
- An earlier full run passed 149/149. The following run had one unrelated timing failure in `SearchTests.Message_WhenDelivered_IsFindableByFullTextQueryWithinOneSecond` (1,118 ms vs. its 1,000 ms bound); the next two fresh-database full runs both passed.
- The tests added no skips and did not weaken their existing business assertions.

## Fixture/database notes

`IsolatedPostgresFixture` creates migrated databases named `miautrix_test_fixtures_<random>` and drops them after the class finishes. The UID allocator test fixture now uses the same permitted disposable-database namespace instead of `uidtest_*`. All external connection values were loaded into the test process only; no production database name was used.

## Follow-up: process environment and parallel execution

The flaky CLI helper changed `MIAUTRIX_STORAGE_DIR` process-wide while unrelated integration tests were using the web host's mail storage. The CLI now accepts a programmatic storage-directory override; the test passes a unique path without touching environment variables. The CLI registrar owns its generated service providers and disposes them before the helper attempts cleanup; cleanup retries transient file locks and never fails the test.

The other IntegrationTests environment writes were in `AutodiscoveryContractTests`, `MtaStsPolicyTests`, and `MobileConfigTests`. These tests now set the public mail hosts through isolated in-memory configuration on each `WebApplicationFactory`. `rg SetEnvironmentVariable tests/Miautrix.Mail.IntegrationTests` returns no matches. The remaining environment-mutating tests are in the separate UnitTests assembly; all three classes share the `ProcessEnvironment` collection, which disables parallel execution for them.

Five consecutive full IntegrationTests runs used separate fresh, fully migrated throwaway databases. Each passed with all 149 tests and no skips:

```text
Passed!  - Failed:     0, Passed:   149, Skipped:     0, Total:   149, Duration: 13 s - Miautrix.Mail.IntegrationTests.dll (net10.0)
Passed!  - Failed:     0, Passed:   149, Skipped:     0, Total:   149, Duration: 15 s - Miautrix.Mail.IntegrationTests.dll (net10.0)
Passed!  - Failed:     0, Passed:   149, Skipped:     0, Total:   149, Duration: 19 s - Miautrix.Mail.IntegrationTests.dll (net10.0)
Passed!  - Failed:     0, Passed:   149, Skipped:     0, Total:   149, Duration: 19 s - Miautrix.Mail.IntegrationTests.dll (net10.0)
Passed!  - Failed:     0, Passed:   149, Skipped:     0, Total:   149, Duration: 15 s - Miautrix.Mail.IntegrationTests.dll (net10.0)
```
