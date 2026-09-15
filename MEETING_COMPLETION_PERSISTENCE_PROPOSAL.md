> Superseded by [the implemented final contract](MEETING_FINAL_CONTRACT_REPORT.md). This file records the earlier investigation/proposal only.

# Completion-specific MOM and PDF persistence proposal

This correction supersedes all earlier suggestions to use MeetingMinutes for completion. Existing Meeting minutes/MOM and assignmentSummary remain independent and unchanged. This document is a proposal; no application/schema implementation or migration has been made.

Inspected: Entities/EaFms/Meeting.cs, Entities/EaFms/Attachment.cs, Data/EaFms/EaFmsDbContext.cs, Services/EaFms/MeetingLifecycleService.cs, Services/EaFms/WorkflowExecutionService.cs, and existing attachment storage interfaces/services. The prior read-only live inspection confirmed these tables and columns.

Findings:

- Meeting stores CompletedAt but has neither completion MOM nor a completion attachment link.
- ea_attachments has bigint Id, object key, file metadata, generic RelatedModule/RelatedEntity/RelatedEntityId and JSONB Metadata. It can store a separate completion PDF without a new table.
- Current Complete accepts Notes, writes Meeting.CompletedAt, then calls workflow completion inside a database transaction. It persists no MOM/PDF. Workflow completion rejects nonempty EvidenceAttachmentIds; that guard must not simply be removed globally.
- Existing AttachmentService targets SCIHAttachment. Existing file writers do not provide PDF validation and transactional cleanup for this Meeting flow. A Meeting completion storage adapter is required.

Recommended minimum explicit-link schema:

```sql
ALTER TABLE public.ea_meetings
    ADD COLUMN "CompletionMom" character varying(4000) NULL,
    ADD COLUMN "CompletionPdfAttachmentId" bigint NULL,
    ADD CONSTRAINT "FK_ea_meetings_CompletionPdfAttachmentId"
        FOREIGN KEY ("CompletionPdfAttachmentId")
        REFERENCES public.ea_attachments ("Id") ON DELETE RESTRICT;
```

Two columns and one foreign key; no new table, no minutes changes and no attachment column changes. The 4000-character limit follows the existing Meeting text convention but does not reuse minutes storage. Nullability preserves existing Meetings, including historical completed records; do not invent completion evidence for them. New completion requires nonblank text and a valid file at the service/API boundary. A database constraint requiring evidence on every CompletedAt row would reject historical completions and is not part of this proposal.

The explicit Meeting.CompletionPdfAttachmentId is the authoritative completion role/link. Store only a newly uploaded completion PDF through this flow, with backend-derived attachment ownership:

- RelatedModule = Meeting
- RelatedEntity = Meeting
- RelatedEntityId = invariant Meeting.Id string
- Metadata.purpose = MeetingCompletionPdf

The role marker supports queries; it does not replace the explicit FK. The service validates matching ownership, role, active/nondeleted metadata and actual PDF content. A general Meeting attachment is not completion evidence just because it is a PDF. The frontend supplies a file, not an existing attachment ID or object key. General attachment mutation/deletion paths must not invalidate a referenced completion PDF; the FK prevents physical metadata deletion, while application guards cover soft deletion and file deletion.

A one-column variant could add only CompletionMom and resolve the PDF by reserved attachment role plus Meeting ID, protected by a partial unique index. It is feasible, but requires role-based lookup conventions and has no FK from Meeting to evidence. The two-column design above is the smallest recommended design with an explicit relational link; it avoids a separate completion table and role inference.

Request target: POST /api/ea/meetings/{meetingId}/complete with multipart/form-data fields completionMom and completionPdf, both mandatory. Reject null/empty/whitespace text, text over 4000 characters, missing/empty/non-PDF files, invalid PDF content and oversize uploads. The inspected generic attachment validators define no reusable EA PDF size limit; implementation must define a bounded Meeting-specific upload setting and align request-body limits. No generic Notes input or minutes ID is needed.

Consistency sequence:

1. Begin the shared EF transaction; lock/validate the Meeting and linked workflow in a consistent order. Confirm the workflow belongs to this Meeting and meets existing completion rules, including started state and no open operational stop. Serialize concurrent complete calls and reject an already-completed request without overwriting evidence.
2. Validate completionMom and completionPdf before persisting either. Generate a backend-owned unique storage key.
3. Save CompletionMom within the uncommitted transaction, durably write the PDF, create ea_attachments metadata, and save CompletionPdfAttachmentId. Keep both completion timestamps/statuses unchanged through these steps.
4. Only after successful evidence persistence, execute the existing workflow completion mechanics and set Meeting.CompletedAt; save audit and commit all database writes together. Keep Meeting-specific evidence validation in the facade/adapter and preserve unrelated modules' evidence guard behavior.
5. Any MOM/file/metadata/link/lifecycle failure before commit rolls back database changes, leaving an initially incomplete Meeting/workflow incomplete. Remove the newly written file on confirmed rollback. File storage is not enlisted in the database transaction: crashes can leave unreferenced files, so use cleanup/reconciliation for orphans. On an uncertain commit result, reconcile database state before deleting the object; do not risk deleting evidence from a successfully committed completion.

Existing historical completed Meetings remain readable with null completion evidence. This design does not redefine revision/reopening behavior; evidence replacement on a later completion cycle is outside this change and must not be silently inferred.

Verification required after implementation: missing/blank MOM; missing/invalid/oversize PDF; successful independent persistence; injected MOM save, upload, attachment save and workflow failures; concurrent/repeated completion; historical GET; unchanged minutes records and assignmentSummary. No implementation tests were run for this proposal.

The original Type/Subtype TAT redesign, direct Start/Pause/Resume changes, snapshot corrections and Doer clarification remain outstanding. This correction changes only the completion persistence proposal.
