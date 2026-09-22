EA CENTRAL TASK REVIEW + REWORK — PHASE 1

The workspace already contained the central review implementation and its one migration at the start of this turn. This work completed and hardened it, added contract and regression coverage, and verified the result. Pre-existing unrelated Document Register work and TAT refactoring were preserved. No TAT calculator, WorkPause classifier, WorkRevision, EaTask entity, or historical migration was edited in this turn.

1. Table/entity: TaskReview mapped to public.ea_task_reviews in EaFmsDbContext.
2. Exactly one feature migration: 20260922075026_AddEaTaskReviews (plus its designer and model snapshot).
3. Migration applied: YES. Already applied when inspected; verified read-only through EaFmsDbContext and the existing __EFMigrationsHistory convention. No runtime migration application or business data insertion was performed in this turn. Integration tests apply migrations to disposable scratch databases and drop them afterward.
4. Fields: Id bigint identity PK; EaTaskId bigint required FK; ReviewCycleNo integer required; ReviewStatus varchar(30) required; ReviewerId/SubmittedById/ReviewedById nullable varchar(100); ReviewerName/SubmittedByName/ReviewedByName nullable varchar(200); SubmittedAt required timestamptz; ReviewedAt nullable timestamptz; ReviewRemark/ReworkRemark nullable varchar(2000); CreatedBy/ModifiedBy nullable varchar(100); CreatedDate required timestamptz; ModifiedDate nullable timestamptz. Navigation: EaTask. Unique (EaTaskId, ReviewCycleNo), EaTaskId index, restrictive FK delete behavior, and review-status check constraint.
5. Internal service: ITaskReviewService/TaskReviewService with submit, approve, request rework, current, history, and batch-current operations. Repository and shared EF context provide persistence and transaction boundaries, following EA service conventions. PostgreSQL EaTask row locking serializes decisions/submissions. Locked task reads and current-cycle reloads prevent stale tracked state from authorizing duplicate actions. Latest-cycle batching is filtered in SQL. Business conflicts use BusinessRuleException/HTTP 409; missing records use HTTP 404. Reviewer, submitter and decision snapshots come solely from optional request fields. Existing current-user infrastructure is used only for audit attribution, never to fill those snapshots.
6. Routes: each base below exposes POST submit-for-review, POST review/approve, POST review/rework, GET review/history:
   /api/ea/delegations/{id}
   /api/ea/meetings/{id}
   /api/ea/travel/requests/{id}
   /api/ea/approvals/{id}
   Actual route parameter names match each module's existing convention. There is no generic public Task Review write API.
7. Meeting: business record resolves its existing central EaTask; list/detail/lifecycle responses expose the shared summary. No WorkflowInstanceId is required by the central review service.
8. Delegation: resolves Delegation.EaTaskId; review actions use the shared service; list/detail/lifecycle responses use the shared summary.
9. Travel: resolves TravelRequest.EaTaskId; detail/list/create/action contracts expose the shared summary. Review action responses retain the current business approval cycle. TravelRequestCycle remains independent.
10. Approval: resolves ApprovalRequest.EaTaskId; list/detail expose the summary and review writes return TaskReviewSummaryDto. Business approval states/cycles remain independent. Typed Swagger metadata was added for EA Approval list/detail and review routes. Only EA ApprovalDetailDto receives the distinct Swagger schema name EaApprovalDetailDto, avoiding a collision with the existing non-EA ApprovalDetailDto; JSON fields are unchanged.
11. reviewSummary contract: status, reviewCycleNumber, reviewerId, reviewerName, submittedById, submittedByName, submittedForReviewAt, reviewedById, reviewedByName, reviewedAt, reviewRemark, reworkRemark. Never-submitted representation is an object with status null, reviewCycleNumber 0, and all other values null. Shared TaskReviewSummaryDto across modules; request business fields are optional.
12. History: ascending reviewCycleNumber; persisted status, identity snapshots, submission/decision timestamps, reviewRemark and reworkRemark. No WorkflowInstanceId.
13. Full cycle: PASS for all four modules: Cycle 1 PendingReview -> ReworkRequested -> new Cycle 2 PendingReview -> Approved. Prior-cycle snapshots and remarks remain unchanged. Double submit, missing pending cycle, double approval, completed/cancelled submission, and concurrency rejection verified.
14. ExecutionStatus unchanged during review: PASS. StartedAt, CompletedAt and TatUsedMinutes preservation also tested.
15. TAT architecture unchanged by this work: PASS. Existing pre-turn Meeting/Delegation TAT refactor preserved; no review-specific timing logic added.
16. TAT continues during PendingReview: PASS, measured through Meeting and Delegation module responses.
17. WorkPause untouched by review actions: PASS. No pause rows created. WorkRevision untouched.
18. Existing Complete behavior: PASS. No review guards or automatic module completion. Delegation can complete with PendingReview; existing module lifecycle regressions pass.
19. Travel approval lifecycle regression: PASS. Existing business cycle remains byte-for-byte unchanged through both review cycles; existing Travel tests pass.
20. Approval business lifecycle regression: PASS. Existing ApprovalCycle remains PendingApproval while Task Review reaches Approved; existing Approval tests pass.
21. EM Report regression: PASS. 94 focused tests matching EmReport passed.
22. Swagger: PASS. Generated contract verifies all 16 routes, typed success responses, optional request fields, no generic write route, and the shared TaskReviewSummaryDto reference across module response schemas. Artifact: swagger.json. Generated from the compiled controllers and production schema/operation filters without starting background jobs.
23. Focused tests: 850 passed, 0 failed, 1 skipped (851 total), including 29 TaskReview tests. Artifact: task-review-focused.trx; log: focused.log.
24. Full tests: 1077 passed, 0 failed, 1 skipped (1078 total). Artifact: task-review-full.trx; log: full.log. The existing skipped test is ApprovalDocumentServiceComprehensiveTests.CycleLinking_CurrentCycleChangeDuringTransaction_IsRejected; its existing skip states that the fixture lacks a concurrent-cycle-change hook. It was not enabled or newly skipped here.
25. Build: PASS, 0 errors, 4 existing nullable warnings (ApprovalsController, ApprovalDocumentService, EaTaskHistoryBuilder). The initial requested default build passed. Final source built to bin/TaskReviewVerification because an existing running API locks the normal output. No running API was stopped. Test restore used the local package cache after NuGet TLS access failed. Test compilation also reports the existing EF Relational 8.0.8/8.0.30 reference warning; all executed tests pass.
26. Database/schema changes: only the one new review table, FK, indexes/check constraint and model mapping. No ea_tasks schema changes; no historical migration edits. EaFmsDbContext reports no pending model changes. Logs: migration-state.log, model-state.log.
27. Exact source-file inventory: files-changed.txt lists the files edited/created during this turn separately from the complete feature work already present in the working tree. Mixed existing TAT changes in module files are explicitly not attributed to this turn. Generated verification artifacts reside in this directory.

Verification commands:

dotnet restore Tests/Studio5Jarvis.Tests.csproj --source <local-user-package-cache> -p:NuGetAudit=false --force
dotnet test Tests/Studio5Jarvis.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=bin/TaskReviewVerification/ --filter "FullyQualifiedName~TaskReview|FullyQualifiedName~Meeting|FullyQualifiedName~Delegation|FullyQualifiedName~Travel|FullyQualifiedName~Approval|FullyQualifiedName~EaTask|FullyQualifiedName~EmReport" --logger "trx;LogFileName=task-review-focused.trx" --results-directory artifacts/task-review
dotnet test Tests/Studio5Jarvis.Tests.csproj --no-build --no-restore -p:UseAppHost=false -p:OutputPath=bin/TaskReviewVerification/ --logger "trx;LogFileName=task-review-full.trx" --results-directory artifacts/task-review
dotnet build Studio5JarvisMasterApi.csproj --no-restore -p:UseAppHost=false -p:OutputPath=bin/TaskReviewVerification/
dotnet ef migrations list --context EaFmsDbContext --no-build
dotnet ef migrations has-pending-model-changes --context EaFmsDbContext --no-build

EA CENTRAL TASK REVIEW + REWORK PHASE 1: PASS