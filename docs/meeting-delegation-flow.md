Meeting delegation now happens only through `POST /api/ea/meetings/{id}/ai/actions/confirm` after completion. Manual actions remain meeting-only until confirmed. No prior AI analysis is required.

The final JSON fields are `delegationDecision`, `delegationDecidedAt`, `delegationDecidedBy` (meeting detail and list), `delegationId` (action items), and `createdDelegationCount` (confirmation). Confirmation accepts optional `meetingActionId` and returns all saved items in `createdActions`, including updated items and their delegation IDs.

Decline route: `POST /api/ea/meetings/{id}/delegation/decline`, no body; returns updated meeting detail. Declining is permanent. Reopening after delegation preserves the first decision timestamp and actor.

Exact flow-specific HTTP 409 messages:

- `Complete the meeting before choosing whether to delegate tasks.`
- `Delegation has already been decided for this meeting.`
- `Complete the meeting before delegating tasks.`
- `Delegation was declined for this meeting.`
- `At least one action is required.`
- `Each action requires title, doerId and doerName.`
- `An action may only be submitted once per request.`
- `Action does not belong to this meeting or is deleted.`
- `Action '<title>' is already delegated.` (the stored action title replaces `<title>`)

Missing/deleted meetings return 404. Existing delegation-service configuration and priority validation still apply. There are no action edit/delete endpoints in this API; confirmation rejects updates to delegated items.

Migration: `20260924094400_MeetingDelegationDecision` adds three nullable columns, with `DelegationDecidedAt` stored as PostgreSQL `timestamp with time zone`. Historical meeting decisions remain null; historical delegations are preserved and their IDs are resolved using the Meeting module/source-action link. Even soft-deleted delegations prevent delegating the same action again.

Confirmation and decline serialize on the meeting row. Confirmation commits actions, delegations, EA tasks, audits, decision, and extraction-applied metadata in one transaction. Failure rolls everything back.

Tests use disposable PostgreSQL databases. Set `MEETING_TEST_POSTGRES` to a local PostgreSQL connection with CREATE DATABASE permission; the wider meeting completion tests also use `EA_DELEGATION_TEST_SERVER`. Run `dotnet test Tests/Studio5Jarvis.Tests.csproj --filter FullyQualifiedName~EaFms.Meetings`. The application database is not migrated by the tests.
