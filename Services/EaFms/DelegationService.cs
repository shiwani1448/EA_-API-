using System.Globalization;
using System.Linq.Expressions;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Delegation CRUD/register service (Step 2).
///
/// Create resolves the canonical "Delegation" BusinessModule dynamically (never
/// hardcoded), creates exactly one central EaTask via the backend-only no-TAT path
/// (Delegation uses an explicit DueDate, not configured TAT), then the Delegation
/// itself. SourceBusinessModuleId is a completely separate concept — the module the
/// delegated work originated FROM — and is never conflated with the Delegation-owning
/// module used for EaTask.BusinessModuleId.
/// </summary>
public class DelegationService : IDelegationService
{
    public const string DelegationBusinessModuleName = "Delegation";

    // Completion PDF: same limits as Meeting's completionPdf (PDF only, 25 MiB).
    private const long MaxCompletionPdfBytes = 25 * 1024 * 1024;

    // ea_attachments association for every Delegation-owned attachment (no dedicated table or column).
    // All three kinds share this same (RelatedModule, RelatedEntity, RelatedEntityId) triple — only
    // Metadata.purpose (and, for review/rework, Metadata.reviewCycleNumber) tells them apart.
    private const string AttachmentRelatedModule = "Delegation";
    private const string AttachmentRelatedEntity = "Delegation";
    private const string CompletionPdfPurpose = "DelegationCompletionPdf";
    private const string ReviewAttachmentPurpose = "DelegationReviewAttachment";
    private const string ReworkAttachmentPurpose = "DelegationReworkAttachment";

    private readonly EaFmsDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    private readonly IDelegationNumberRepository _numbers;
    private readonly IEaTaskService _eaTaskService;
    private readonly IWebHostEnvironment _env;
    private readonly ITaskReviewService _taskReview;
    private readonly ITatRuleRepository _tatRules;

    public DelegationService(
        EaFmsDbContext db,
        ICurrentUserService user,
        IAuditService audit,
        IDelegationNumberRepository numbers,
        IEaTaskService eaTaskService,
        IWebHostEnvironment env,
        ITaskReviewService taskReview,
        ITatRuleRepository tatRules)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _numbers = numbers;
        _eaTaskService = eaTaskService;
        _env = env;
        _taskReview = taskReview;
        _tatRules = tatRules;
    }

    // ============================================================
    // CREATE
    // ============================================================

    /// <summary>
    /// Public standalone creation. Owns its own transaction — unchanged Step 2 contract
    /// and behavior. All actual creation logic lives in CreateCoreAsync so a future
    /// source-module caller (Meeting/Travel/Approval) can reuse the exact same rules
    /// inside its own already-open transaction instead of this one.
    /// </summary>
    public async Task<DelegationResponseDto> CreateAsync(DelegationCreateRequestDto dto, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var result = await CreateCoreAsync(ToCommand(dto), ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private static DelegationCreateCommand ToCommand(DelegationCreateRequestDto dto) => new()
    {
        Title = dto.Title, Description = dto.Description,
        DelegationType = dto.DelegationType, StartDate = dto.StartDate,
        DoerId = dto.DoerId, DoerNameSnapshot = dto.DoerNameSnapshot,
        Priority = dto.Priority, DueDate = dto.EndDate,
        SourceBusinessModuleId = dto.SourceBusinessModuleId, SourceEntityId = dto.SourceEntityId,
        SourceReference = dto.SourceReference, AdditionalNotes = dto.AdditionalNotes
    };

    /// <summary>
    /// Canonical Delegation creation. Transaction-composable: never begins, commits, or
    /// rolls back a transaction itself — the caller (CreateAsync for the standalone public
    /// path, or a future source-module caller such as Meeting completion) fully owns the
    /// surrounding transaction. Everything a Delegation needs — BusinessModule resolution,
    /// priority validation, reference generation, EaTask creation, source linkage, initial
    /// status, assignment snapshots, audit — lives here exactly once so manual and
    /// source-triggered creation can never drift apart.
    ///
    /// AssignedBy is always resolved from the current authenticated execution context
    /// (ICurrentUserService) — the same server-owned actor a manual caller already gets —
    /// never accepted as a parameter, so no caller can forge who created the Delegation.
    /// </summary>
    internal async Task<DelegationResponseDto> CreateCoreAsync(DelegationCreateCommand command, CancellationToken ct)
    {
        // ---- 1. Resolve the Delegation-owning BusinessModule (canonical name; never hardcode IDs) ----
        var delegationModule = await _db.BusinessModules.AsNoTracking()
            .Where(m => !m.IsDeleted && m.IsActive && m.Name == DelegationBusinessModuleName)
            .FirstOrDefaultAsync(ct);
        if (delegationModule is null)
            throw new BusinessRuleException(
                "DELEGATION BUSINESS MODULE CONFIGURATION REQUIRED BEFORE RUNTIME DELEGATION CREATION. " +
                $"Insert an active '{DelegationBusinessModuleName}' record into ea_business_modules.");

        // ---- 2. Validate the SOURCE module (a different concept — WHERE the work came from) ----
        // Null means a direct/manual Delegation: no originating module or record, never
        // defaulted to the Delegation module itself and never fabricated.
        BusinessModule? sourceModule = null;
        if (command.SourceBusinessModuleId.HasValue)
        {
            sourceModule = await _db.BusinessModules.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == command.SourceBusinessModuleId.Value && m.IsActive && !m.IsDeleted, ct)
                ?? throw new BusinessRuleException("SourceBusinessModuleId must refer to an active business module.");
        }

        var priority = NormalizePriority(command.Priority);
        var delegationType = NormalizeDelegationType(command.DelegationType);

        var actor = Actor();
        var actorName = _user.UserName;
        var now = Clock.UtcNowTz;
        var referenceNo = await _numbers.GenerateNextReferenceNoAsync(ct);

        // ---- 3. Create the required central EaTask before the Delegation ----
        // Delegation.EaTaskId is a required, non-deferrable FK, so a valid EaTask must
        // exist before Delegation can be inserted. Delegation.Id is not known yet, so
        // BusinessRecordId is seeded with ReferenceNo and corrected to the real
        // Delegation.Id below — the same pattern already proven for Travel.
        // EaTaskService.CreateWithoutTatAsync is itself ambient-transaction-aware (joins
        // the caller's transaction rather than owning its own), so this composes cleanly
        // regardless of who owns the outer transaction. Delegation has no approved TAT
        // classification apart from delegationType, so TAT is resolved only when a delegationType was
        // supplied: module + Type = delegationType, no subtype (backend-only type-only path; a missing rule fails
        // like any canonical TAT resolution). A blank delegationType, and every Meeting-created Delegation,
        // keeps the backend-only no-TAT path — no Type is ever fabricated.
        var eaTaskRequest = new CreateEaTaskDto
        {
            ModuleId = delegationModule.Id,
            BusinessRecordId = referenceNo,
            Task = string.IsNullOrWhiteSpace(command.Title) ? referenceNo : command.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            WorkflowInstanceId = null
        };
        EaTaskResponseDto eaTaskDto;
        if (delegationType is null)
            eaTaskDto = await _eaTaskService.CreateWithoutTatAsync(eaTaskRequest, ct);
        else
        {
            eaTaskRequest.Type = delegationType;
            eaTaskRequest.Subtype = null;
            eaTaskDto = await _eaTaskService.CreateWithTypeOnlyTatAsync(eaTaskRequest, ct);
        }

        var entity = new Delegation
        {
            ReferenceNo = referenceNo,
            EaTaskId = eaTaskDto.EaTaskId,

            Title = command.Title?.Trim() ?? string.Empty,
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),

            DoerId = command.DoerId?.Trim() ?? string.Empty,
            DoerNameSnapshot = string.IsNullOrWhiteSpace(command.DoerNameSnapshot) ? null : command.DoerNameSnapshot.Trim(),

            AssignedById = actor,
            AssignedByNameSnapshot = actorName,

            Priority = priority,
            DueDate = command.DueDate,
            DelegationType = delegationType,
            StartDate = command.StartDate,
            Status = DelegationStatus.Pending,

            SourceBusinessModuleId = sourceModule?.Id,
            SourceEntityId = string.IsNullOrWhiteSpace(command.SourceEntityId) ? null : command.SourceEntityId.Trim(),
            SourceReference = string.IsNullOrWhiteSpace(command.SourceReference) ? null : command.SourceReference.Trim(),

            AdditionalNotes = string.IsNullOrWhiteSpace(command.AdditionalNotes) ? null : command.AdditionalNotes.Trim(),

            CreatedBy = actor,
            CreatedDate = now
        };

        _db.Delegations.Add(entity);
        await _db.SaveChangesAsync(ct);

        // ---- 4. Correct the EaTask's BusinessRecordId to the real Delegation.Id ----
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == eaTaskDto.EaTaskId, ct);
        eaTask.BusinessRecordId = entity.Id.ToString(CultureInfo.InvariantCulture);
        await _db.SaveChangesAsync(ct);

        _audit.AddAudit(
            "DELEGATION_CREATE", "Delegation", nameof(Delegation),
            entity.Id.ToString(CultureInfo.InvariantCulture), null,
            new
            {
                entity.ReferenceNo, entity.EaTaskId, entity.Title, entity.DoerId, entity.Status,
                entity.Priority, entity.DueDate, entity.DelegationType, entity.StartDate,
                entity.SourceBusinessModuleId, entity.SourceEntityId, entity.SourceReference
            },
            "Delegation created");
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(entity, sourceModule?.Name, null, ct);
    }

    // ============================================================
    // GET DETAIL
    // ============================================================

    public async Task<DelegationResponseDto> GetByIdAsync(long delegationId, CancellationToken ct = default)
    {
        var entity = await _db.Delegations.AsNoTracking()
            .Include(d => d.SourceBusinessModule)
            .FirstOrDefaultAsync(d => d.Id == delegationId && !d.IsDeleted, ct)
            ?? throw new NotFoundException($"Delegation {delegationId} not found.");

        var pdfIds = await LoadCompletionPdfIdsAsync(new[] { entity.Id }, ct);
        return await ToDtoAsync(entity, entity.SourceBusinessModule?.Name, PdfId(pdfIds, entity.Id), ct);
    }

    // ============================================================
    // UPDATE (editable business fields only)
    // ============================================================

    public async Task<DelegationResponseDto> UpdateAsync(long delegationId, DelegationUpdateRequestDto dto, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var entity = await _db.Delegations
            .FirstOrDefaultAsync(d => d.Id == delegationId && !d.IsDeleted, ct)
            ?? throw new NotFoundException($"Delegation {delegationId} not found.");

        // Mirrors Travel's editability gate: a terminal lifecycle state is not editable
        // via the plain update endpoint. Start/Complete themselves arrive in a later step.
        if (entity.Status == DelegationStatus.Completed)
            throw new BusinessRuleException("Completed delegations cannot be edited.");

        BusinessModule? sourceModule = null;
        if (dto.SourceBusinessModuleId.HasValue)
        {
            sourceModule = await _db.BusinessModules.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == dto.SourceBusinessModuleId.Value && m.IsActive && !m.IsDeleted, ct)
                ?? throw new BusinessRuleException("SourceBusinessModuleId must refer to an active business module.");
        }

        var priority = NormalizePriority(dto.Priority);
        var delegationType = NormalizeDelegationType(dto.DelegationType);

        var snapshot = new
        {
            entity.Title, entity.Description, entity.DoerId, entity.DoerNameSnapshot,
            entity.DueDate, entity.DelegationType, entity.StartDate, entity.Priority, entity.SourceBusinessModuleId, entity.SourceEntityId,
            entity.SourceReference, entity.AdditionalNotes
        };

        // Editable business fields only. EaTask.Task/Description are set once at create
        // time and never re-synced on update — matching Travel/Approval's own update
        // paths, neither of which touches EaTask after creation.
        entity.Title = dto.Title?.Trim() ?? string.Empty;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.DoerId = dto.DoerId?.Trim() ?? string.Empty;
        entity.DoerNameSnapshot = string.IsNullOrWhiteSpace(dto.DoerNameSnapshot) ? null : dto.DoerNameSnapshot.Trim();
        entity.DueDate = dto.EndDate;
        entity.DelegationType = delegationType;
        entity.StartDate = dto.StartDate;
        entity.Priority = priority;
        entity.SourceBusinessModuleId = sourceModule?.Id;
        entity.SourceEntityId = string.IsNullOrWhiteSpace(dto.SourceEntityId) ? null : dto.SourceEntityId.Trim();
        entity.SourceReference = string.IsNullOrWhiteSpace(dto.SourceReference) ? null : dto.SourceReference.Trim();
        entity.AdditionalNotes = string.IsNullOrWhiteSpace(dto.AdditionalNotes) ? null : dto.AdditionalNotes.Trim();

        // Backend-owned, never touched here: Id, ReferenceNo, EaTaskId, AssignedById,
        // AssignedByNameSnapshot, Status, StartedAt, CompletedAt, CompletedById,
        // CompletedByNameSnapshot, CreatedBy, CreatedDate.

        entity.ModifiedBy = Actor();
        entity.ModifiedDate = Clock.UtcNowTz;

        _audit.AddAudit(
            "DELEGATION_UPDATE", "Delegation", nameof(Delegation),
            entity.Id.ToString(CultureInfo.InvariantCulture), snapshot,
            new
            {
                entity.Title, entity.Description, entity.DoerId, entity.DoerNameSnapshot,
                entity.DueDate, entity.DelegationType, entity.StartDate, entity.Priority, entity.SourceBusinessModuleId, entity.SourceEntityId,
                entity.SourceReference, entity.AdditionalNotes
            },
            "Delegation updated");

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await ToDtoAsync(entity, sourceModule?.Name, null, ct);
    }

    // ============================================================
    // LIFECYCLE — START / COMPLETE (Step 4)
    // ============================================================

    public async Task<DelegationResponseDto> StartAsync(long delegationId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var entity = await LockDelegationAsync(delegationId, ct);
        if (entity.Status != DelegationStatus.Pending)
            throw new BusinessRuleException($"Delegation cannot be started from its current status '{entity.Status}'.");

        if (await _db.DelegationPhaseTats.AnyAsync(p => p.DelegationId == delegationId, ct)
            || await _db.TaskReviews.AnyAsync(r => r.EaTaskId == entity.EaTaskId, ct))
            throw new BusinessRuleException("Cannot start a Delegation with existing phase or review history.");

        var now = Clock.UtcNowTz;
        entity.Status = DelegationStatus.InProgress;
        entity.StartedAt = now;
        entity.ModifiedBy = Actor();
        entity.ModifiedDate = now;

        // Same EaTask row, updated in place — never a second EaTask, never a WorkflowInstance.
        // Delegation has no TAT: TatRuleId/AllottedTatMinutes/TatUsedMinutes stay untouched (null).
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == entity.EaTaskId, ct);
        eaTask.ExecutionStatus = EaTaskExecutionStatus.InProgress;
        eaTask.StartedAt = now;

        // The Actual phase's TAT rule already resolved (and was required to exist) at Delegation
        // creation time — reuse EaTask's own resolved values rather than looking the rule up again.
        _db.DelegationPhaseTats.Add(new DelegationPhaseTat
        {
            DelegationId = entity.Id, TaskType = DelegationTaskType.Actual, ReviewCycleNumber = 0,
            StartedAt = now, AllottedTatMinutes = eaTask.AllottedTatMinutes, TatRuleId = eaTask.TatRuleId,
            CreatedBy = Actor(), CreatedDate = now
        });

        _audit.AddAudit(
            "DELEGATION_START", "Delegation", nameof(Delegation),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            new { Status = DelegationStatus.Pending },
            new { entity.Status, entity.StartedAt },
            "Delegation started");

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var sourceModuleName = await ResolveSourceModuleNameAsync(entity.SourceBusinessModuleId, ct);
        return await ToDtoAsync(entity, sourceModuleName, null, ct);
    }

    /// <summary>
    /// Doer-facing "I'm done" action. Uploads the optional completion PDF exactly as before, but no
    /// longer finalizes the Delegation itself — it now opens a Task Review cycle instead (see the
    /// TASK REVIEW / REWORK region below). The Delegation stays InProgress; ApproveReviewAsync is
    /// what actually transitions it to Completed. This lets an assignee send work back for rework
    /// (the Delegation is simply never marked Completed until they approve it) without needing any
    /// separate "reopen" capability. Rejects (via SubmitForReviewAsync) if a review is already
    /// pending — i.e. this cannot be called twice in a row without an intervening Approve/Rework.
    /// </summary>
    public Task<DelegationResponseDto> CompleteAsync(long delegationId, IFormFile? completionPdf, CancellationToken ct = default) =>
        SubmitWorkForReviewAsync(delegationId, completionPdf, new SubmitForReviewRequestDto
        {
            SubmittedById = Actor(), SubmittedByName = _user.UserName
        }, ct);

    private async Task<DelegationResponseDto> SubmitWorkForReviewAsync(long delegationId, IFormFile? completionPdf,
        SubmitForReviewRequestDto request, CancellationToken ct)
    {
        // Validate the PDF before opening the transaction so a bad upload fails fast, leaving the
        // Delegation, its EaTask and the storage untouched.
        var pdfBytes = completionPdf is null ? null : await ReadValidatedPdfAsync(completionPdf, ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var entity = await LockDelegationAsync(delegationId, ct);
        if (entity.Status != DelegationStatus.InProgress)
            throw new BusinessRuleException($"Delegation cannot be completed from its current status '{entity.Status}'.");

        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == entity.EaTaskId, ct);
        await RequireNoOpenPauseAsync(eaTask.WorkflowInstanceId, ct);
        var currentReview = await _db.TaskReviews.AsNoTracking().Where(r => r.EaTaskId == entity.EaTaskId)
            .OrderByDescending(r => r.ReviewCycleNo).FirstOrDefaultAsync(ct);
        if (currentReview is not null && currentReview.ReviewStatus != TaskReviewStatus.ReworkRequested)
            throw new BusinessRuleException("Work can only be submitted initially or after a rework request.");
        var phase = await RequireOpenPhaseAsync(delegationId,
            currentReview is null ? DelegationTaskType.Actual : DelegationTaskType.Rework,
            currentReview?.ReviewCycleNo ?? 0, ct);

        var now = Clock.UtcNowTz;
        entity.ModifiedBy = Actor();
        entity.ModifiedDate = now;

        // Delegation + attachment metadata commit together, same as before. The file is written just
        // before the commit; any failure from that point deletes it again so no orphan file remains.
        string? objectKey = null;
        Attachment? attachment = null;
        try
        {
            if (pdfBytes is not null)
            {
                objectKey = await WriteDelegationAttachmentAsync("DelegationCompletion", delegationId, pdfBytes, ct);
                var actor = Actor();
                attachment = new Attachment
                {
                    RelatedModule = AttachmentRelatedModule,
                    RelatedEntity = AttachmentRelatedEntity,
                    RelatedEntityId = delegationId.ToString(CultureInfo.InvariantCulture),
                    OriginalFileName = Path.GetFileName(completionPdf!.FileName.Replace('\\', '/')),
                    ObjectKey = objectKey,
                    ContentType = "application/pdf",
                    Size = pdfBytes.LongLength,
                    AccessUrl = null, // no storage path is exposed; download via GET /api/ea/documents/{attachmentId}/download
                    UploadedBy = actor,
                    UploadedAt = now,
                    Metadata = $"{{\"purpose\":\"{CompletionPdfPurpose}\",\"delegationId\":{delegationId}}}",
                    IsActive = true,
                    IsDeleted = false,
                    CreatedBy = actor,
                    CreatedDate = now
                };
                _db.Attachments.Add(attachment);
            }

            // Opens (or, after a rework, re-opens) a review cycle for the doer's completed work.
            // Throws if one is already pending — the caller must wait for Approve/Rework first.
            var reviewSummary = await _taskReview.SubmitForReviewAsync(entity.EaTaskId, request, ct);
            await ClosePhaseAsync(phase, eaTask.WorkflowInstanceId, now, ct);
            await OpenPhaseAsync(delegationId, eaTask.BusinessModuleId, entity.DelegationType, DelegationTaskType.Review, reviewSummary.ReviewCycleNumber, now, ct);

            _audit.AddAudit(
                "DELEGATION_SUBMIT_FOR_REVIEW", "Delegation", nameof(Delegation),
                entity.Id.ToString(CultureInfo.InvariantCulture),
                null,
                new { HasCompletionPdf = attachment is not null },
                "Delegation work submitted for review");

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            if (objectKey is not null) TryDeleteFile(objectKey);
            throw;
        }

        var sourceModuleName = await ResolveSourceModuleNameAsync(entity.SourceBusinessModuleId, ct);
        return await ToDtoAsync(entity, sourceModuleName, attachment?.Id, ct);
    }

    /// <summary>
    /// The completion tail CompleteAsync used to run directly, now run from ApproveReviewAsync
    /// instead: freezes TAT, closes the pause anchor and marks the Delegation/EaTask Completed.
    /// Shared by ApproveReviewAsync only — CompleteAsync itself no longer calls this.
    /// </summary>
    private async Task FinalizeCompletionAsync(Delegation entity, EaTask eaTask, string? completedById, string? completedByName, DateTime now, CancellationToken ct)
    {
        entity.Status = DelegationStatus.Completed;
        entity.CompletedAt = now;
        entity.CompletedById = completedById ?? Actor();
        entity.CompletedByNameSnapshot = completedByName;
        entity.ModifiedBy = Actor();
        entity.ModifiedDate = now;

        eaTask.ExecutionStatus = EaTaskExecutionStatus.Completed;
        eaTask.CompletedAt = now;

        // TAT-enabled Delegations only: freeze the final active TAT (actual StartedAt to now, simple
        // pauses excluded) with the same shared calculation Meeting uses. Review time counts toward
        // this, same as the rest of the InProgress window — no separate carve-out for it.
        if (eaTask.AllottedTatMinutes.HasValue && eaTask.StartedAt.HasValue)
        {
            var pausesForTat = eaTask.WorkflowInstanceId.HasValue
                ? await _db.WorkPauses.AsNoTracking()
                    .Where(p => p.WorkflowInstanceId == eaTask.WorkflowInstanceId && !p.IsDeleted).ToListAsync(ct)
                : new List<WorkPause>();
            eaTask.TatUsedMinutes = EaTaskService.CalculateActiveTatMinutes(eaTask.StartedAt.Value, now, pausesForTat);
        }

        // A pause anchor exists only if the Delegation was ever paused; close it like Meeting closes its workflow.
        if (eaTask.WorkflowInstanceId.HasValue)
            await CompleteAnchorAsync(eaTask.WorkflowInstanceId.Value, now, ct);
    }

    // ============================================================
    // LIFECYCLE — PAUSE / RESUME (shared WorkPause architecture)
    // ============================================================
    //
    // A Delegation has no Meeting-style workflow, but the shared pause model (WorkPause, EaTask/EM Report pause
    // detection, EaTask history) is keyed on EaTask.WorkflowInstanceId. So the first Pause lazily creates one
    // minimal WorkflowInstance anchor (BusinessModule = Delegation, BusinessRecordId = Delegation.Id, In Progress,
    // TatStartedAt = Delegation.StartedAt) and links it through the existing EaTask.WorkflowInstanceId column.
    // No pause table, entity, status or ExecutionStatus is added: Paused stays derived (InProgress + open WorkPause).

    public async Task<DelegationResponseDto> PauseAsync(long delegationId, DelegationPauseRequestDto? request, CancellationToken ct = default)
    {
        var reason = string.IsNullOrWhiteSpace(request?.PauseReason) ? "Delegation paused" : request!.PauseReason!.Trim();
        if (reason.Length > 2000) throw new BadRequestException("pauseReason must be at most 2000 characters.");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var entity = await LockDelegationAsync(delegationId, ct);
        RequireInProgress(entity, "paused");

        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == entity.EaTaskId, ct);
        var now = Clock.UtcNowTz;
        var anchor = await EnsureAnchorAsync(entity, eaTask, now, ct);

        if (await _db.WorkPauses.AnyAsync(p => p.WorkflowInstanceId == anchor.Id && !p.IsDeleted && p.EndAt == null, ct))
            throw new BusinessRuleException("Delegation is already paused. Resume it first.");

        var actor = Actor();
        var pause = new WorkPause
        {
            WorkflowInstanceId = anchor.Id,
            IntakeRequestId = null,
            FollowupId = null,
            StartAt = now,
            Reason = reason,
            CreatedBy = actor,
            CreatedDate = now
        };
        _db.WorkPauses.Add(pause);
        AddSameStatusHistory(anchor, reason, "SIMPLE_PAUSE", actor, now);

        entity.ModifiedBy = actor;
        entity.ModifiedDate = now;
        await _db.SaveChangesAsync(ct);

        _audit.AddAudit(
            "DELEGATION_PAUSE", "Delegation", nameof(Delegation),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            new { entity.Status, IsPaused = false },
            new { entity.Status, IsPaused = true, PauseId = pause.Id, pause.StartAt, pause.Reason },
            "Delegation paused");
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await BuildResponseAsync(entity, ct);
    }

    public async Task<DelegationResponseDto> ResumeAsync(long delegationId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var entity = await LockDelegationAsync(delegationId, ct);
        RequireInProgress(entity, "resumed");

        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == entity.EaTaskId, ct);
        var open = eaTask.WorkflowInstanceId.HasValue
            ? await _db.WorkPauses
                .Where(p => p.WorkflowInstanceId == eaTask.WorkflowInstanceId && !p.IsDeleted && p.EndAt == null)
                .OrderByDescending(p => p.StartAt).ThenByDescending(p => p.Id)
                .ToListAsync(ct)
            : new List<WorkPause>();
        if (open.Count == 0)
            throw new BusinessRuleException("No open pause found. The Delegation is not paused.");
        if (open.Count > 1)
            throw new BusinessRuleException("Multiple open operational stops exist; resolve manually.");

        var pause = open[0];
        var now = Clock.UtcNowTz;
        var actor = Actor();
        pause.EndAt = now;
        pause.ResumedById = _user.UserId.ToString(CultureInfo.InvariantCulture);
        pause.ResumedByName = _user.UserName;
        pause.ModifiedBy = actor;
        pause.ModifiedDate = now;

        var anchor = await _db.WorkflowInstances.FirstAsync(w => w.Id == eaTask.WorkflowInstanceId!.Value, ct);
        AddSameStatusHistory(anchor, "Work resumed", "SIMPLE_RESUME", actor, now);

        entity.ModifiedBy = actor;
        entity.ModifiedDate = now;

        _audit.AddAudit(
            "DELEGATION_RESUME", "Delegation", nameof(Delegation),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            new { entity.Status, IsPaused = true, PauseId = pause.Id },
            new { entity.Status, IsPaused = false, PauseId = pause.Id, pause.EndAt, pause.ResumedById, pause.ResumedByName },
            "Delegation resumed");
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await BuildResponseAsync(entity, ct);
    }

    // ============================================================
    // TASK REVIEW / REWORK: Delegation owns phase transitions; the shared service owns review history.
    // All transitions hold the Delegation lock and commit phase snapshots and review state together.
    // ============================================================

    /// <summary>Same phase transition as Complete, with caller-supplied review participants and no PDF.</summary>
    public Task<DelegationResponseDto> SubmitForReviewAsync(long delegationId, SubmitForReviewRequestDto dto, CancellationToken ct = default) =>
        SubmitWorkForReviewAsync(delegationId, null, dto, ct);

    /// <summary>
    /// Approving the pending review cycle is what finalizes the Delegation now (Complete only opens
    /// the cycle — see CompleteAsync above). Runs the same completion tail CompleteAsync used to run
    /// directly: freezes TAT, closes the pause anchor, marks Completed. completedBy reflects the doer
    /// whose work was just approved (the review cycle's own submitter), not the approving assignee —
    /// that identity already lives on the review cycle (reviewedById/reviewedByName) returned here.
    /// </summary>
    public async Task<DelegationResponseDto> ApproveReviewAsync(long delegationId, ApproveTaskReviewRequestDto dto, IFormFile? attachment, CancellationToken ct = default)
    {
        // Validate before opening the transaction so a bad upload fails fast, leaving everything unchanged.
        var attachmentBytes = attachment is null ? null : await ReadValidatedPdfAsync(attachment, ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var entity = await LockDelegationAsync(delegationId, ct);
        if (entity.Status != DelegationStatus.InProgress)
            throw new BusinessRuleException($"Delegation review cannot be approved from its current status '{entity.Status}'.");

        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == entity.EaTaskId, ct);
        await RequireNoOpenPauseAsync(eaTask.WorkflowInstanceId, ct);
        var phase = await RequireReviewPhaseAsync(entity, ct);
        var summary = await _taskReview.ApproveAsync(entity.EaTaskId, dto, ct);
        var now = Clock.UtcNowTz;
        await FinalizeCompletionAsync(entity, eaTask, summary.SubmittedById, summary.SubmittedByName, now, ct);
        // Close out this cycle's Review phase — approved, so nothing new opens after it.
        await ClosePhaseAsync(phase, eaTask.WorkflowInstanceId, now, ct);

        string? objectKey = null;
        try
        {
            // The assignee's own optional document for this decision — separate ea_attachments row
            // from the doer's completion PDF, tagged with this review cycle so it's traceable in history.
            if (attachmentBytes is not null)
                objectKey = await AddReviewAttachmentAsync(delegationId, ReviewAttachmentPurpose, summary.ReviewCycleNumber, attachment!, attachmentBytes, now, ct);

            _audit.AddAudit(
                "DELEGATION_COMPLETE", "Delegation", nameof(Delegation),
                entity.Id.ToString(CultureInfo.InvariantCulture),
                new { Status = DelegationStatus.InProgress },
                new { entity.Status, entity.CompletedAt, entity.CompletedById, entity.CompletedByNameSnapshot, ReviewCycle = summary.ReviewCycleNumber },
                "Delegation review approved and completed");

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            if (objectKey is not null) TryDeleteFile(objectKey);
            throw;
        }

        return await GetByIdAsync(delegationId, ct);
    }

    /// <summary>
    /// Marks the current cycle ReworkRequested only — no Delegation/EaTask state change, because
    /// CompleteAsync never marked it Completed in the first place. It is simply still InProgress, so
    /// the doer can act on the rework remark and call Complete again for the next review cycle.
    /// </summary>
    public async Task<DelegationResponseDto> RequestReworkAsync(long delegationId, RequestTaskReworkRequestDto dto, IFormFile? attachment, CancellationToken ct = default)
    {
        var attachmentBytes = attachment is null ? null : await ReadValidatedPdfAsync(attachment, ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var entity = await LockDelegationAsync(delegationId, ct);
        RequireInProgress(entity, "sent for rework");
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == entity.EaTaskId, ct);
        await RequireNoOpenPauseAsync(eaTask.WorkflowInstanceId, ct);
        var phase = await RequireReviewPhaseAsync(entity, ct);
        var summary = await _taskReview.RequestReworkAsync(entity.EaTaskId, dto, ct);
        var now = Clock.UtcNowTz;

        // Close out this cycle's Review phase and open a Rework phase (same cycle number — a review
        // cycle is either approved or reworked, never both) so the doer's redo has its own TAT window.
        await ClosePhaseAsync(phase, eaTask.WorkflowInstanceId, now, ct);
        await OpenPhaseAsync(delegationId, eaTask.BusinessModuleId, entity.DelegationType, DelegationTaskType.Rework, summary.ReviewCycleNumber, now, ct);

        string? objectKey = null;
        try
        {
            // The assignee's own optional document explaining what needs to be redone — separate
            // ea_attachments row, tagged with this review cycle so it's traceable in history.
            if (attachmentBytes is not null)
                objectKey = await AddReviewAttachmentAsync(delegationId, ReworkAttachmentPurpose, summary.ReviewCycleNumber, attachment!, attachmentBytes, now, ct);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            if (objectKey is not null) TryDeleteFile(objectKey);
            throw;
        }

        return await GetByIdAsync(delegationId, ct);
    }

    /// <summary>Adds (tracked, not yet saved) the assignee's review/rework decision attachment. Returns the object key for cleanup-on-failure.</summary>
    private async Task<string> AddReviewAttachmentAsync(long delegationId, string purpose, int reviewCycleNumber, IFormFile file, byte[] bytes, DateTime now, CancellationToken ct)
    {
        var objectKey = await WriteDelegationAttachmentAsync("DelegationReview", delegationId, bytes, ct);
        var actor = Actor();
        _db.Attachments.Add(new Attachment
        {
            RelatedModule = AttachmentRelatedModule,
            RelatedEntity = AttachmentRelatedEntity,
            RelatedEntityId = delegationId.ToString(CultureInfo.InvariantCulture),
            OriginalFileName = Path.GetFileName(file.FileName.Replace('\\', '/')),
            ObjectKey = objectKey,
            ContentType = "application/pdf",
            Size = bytes.LongLength,
            AccessUrl = null, // no storage path is exposed; download via GET /api/ea/documents/{attachmentId}/download
            UploadedBy = actor,
            UploadedAt = now,
            Metadata = $"{{\"purpose\":\"{purpose}\",\"delegationId\":{delegationId},\"reviewCycleNumber\":{reviewCycleNumber}}}",
            IsActive = true,
            IsDeleted = false,
            CreatedBy = actor,
            CreatedDate = now
        });
        return objectKey;
    }

    public async Task<List<TaskReviewHistoryItemDto>> GetReviewHistoryAsync(long delegationId, CancellationToken ct = default)
    {
        var entity = await RequireDelegationAsync(delegationId, ct);
        var history = await _taskReview.GetHistoryAsync(entity.EaTaskId, ct);
        var reviewAttachmentIds = await LoadReviewAttachmentIdsAsync(new[] { delegationId }, ct);
        foreach (var cycle in history)
            cycle.AttachmentId = reviewAttachmentIds.TryGetValue((delegationId, cycle.ReviewCycleNumber), out var id) ? id : null;
        return history;
    }

    private async Task<Delegation> RequireDelegationAsync(long delegationId, CancellationToken ct) =>
        await _db.Delegations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == delegationId && !d.IsDeleted, ct)
            ?? throw new NotFoundException($"Delegation {delegationId} not found.");

    private static void RequireInProgress(Delegation entity, string action)
    {
        if (entity.Status != DelegationStatus.InProgress)
            throw new BusinessRuleException($"Delegation cannot be {action} from its current status '{entity.Status}'. It must be InProgress.");
    }

    private async Task<DelegationResponseDto> BuildResponseAsync(Delegation entity, CancellationToken ct)
    {
        var sourceModuleName = await ResolveSourceModuleNameAsync(entity.SourceBusinessModuleId, ct);
        var pdfIds = await LoadCompletionPdfIdsAsync(new[] { entity.Id }, ct);
        return await ToDtoAsync(entity, sourceModuleName, PdfId(pdfIds, entity.Id), ct);
    }

    /// <summary>Adds the Task Review summary (batched-per-entity here; ListAsync uses the true batch path instead) to the shared TAT-view-based ToDto mapping.</summary>
    private async Task<DelegationResponseDto> ToDtoAsync(Delegation entity, string? sourceModuleName, long? completionPdfAttachmentId, CancellationToken ct)
    {
        var tat = await LoadTatViewAsync(entity, ct);
        var review = await _taskReview.GetCurrentAsync(entity.EaTaskId, ct);
        if (review.ReviewCycleNumber > 0)
            ApplyReviewAttachment(review, entity.Id, await LoadReviewAttachmentIdsAsync(new[] { entity.Id }, ct));
        var workflowInstanceId = await _db.Tasks.AsNoTracking().Where(t => t.Id == entity.EaTaskId).Select(t => t.WorkflowInstanceId).FirstAsync(ct);
        var phaseTat = await BuildPhaseTatAsync(entity.Id, workflowInstanceId, ct);
        return ToDto(entity, sourceModuleName, completionPdfAttachmentId, tat, review, phaseTat);
    }

    /// <summary>Returns the Delegation's pause anchor, creating and linking it on first use.</summary>
    private async Task<WorkflowInstance> EnsureAnchorAsync(Delegation entity, EaTask eaTask, DateTime now, CancellationToken ct)
    {
        if (eaTask.WorkflowInstanceId.HasValue)
            return await _db.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == eaTask.WorkflowInstanceId.Value && !w.IsDeleted, ct)
                ?? throw new BusinessRuleException("The Delegation's pause workflow is missing or deleted.");

        var module = await _db.BusinessModules.AsNoTracking()
            .Where(m => !m.IsDeleted && m.IsActive && m.Name == DelegationBusinessModuleName)
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException(
                $"An active '{DelegationBusinessModuleName}' record is required in ea_business_modules.");
        var inProgress = await SingleStatusAsync("In Progress", ct);

        var startedAt = entity.StartedAt ?? now;
        var anchor = new WorkflowInstance
        {
            BusinessModuleId = module.Id,
            BusinessRecordId = entity.Id.ToString(CultureInfo.InvariantCulture),
            StatusId = inProgress.Id,
            StartedAt = startedAt,
            TatStartedAt = startedAt,
            UpdatedAt = now,
            DoerId = entity.DoerId,
            DoerName = entity.DoerNameSnapshot,
            IsActive = true,
            CreatedBy = Actor(),
            CreatedDate = now
        };
        _db.WorkflowInstances.Add(anchor);
        await _db.SaveChangesAsync(ct);
        eaTask.WorkflowInstanceId = anchor.Id;
        return anchor;
    }

    private async Task CompleteAnchorAsync(long workflowId, DateTime now, CancellationToken ct)
    {
        var anchor = await _db.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted, ct);
        if (anchor is null) return;
        var completed = await SingleStatusAsync("Completed", ct);
        var fromStatusId = anchor.StatusId;
        anchor.StatusId = completed.Id;
        anchor.UpdatedAt = now;
        anchor.CompletedAt ??= now;
        anchor.IsActive = false;
        anchor.ModifiedBy = Actor();
        anchor.ModifiedDate = now;
        _db.WorkflowHistory.Add(new WorkflowHistory
        {
            WorkflowInstanceId = anchor.Id,
            FromStatusId = fromStatusId,
            ToStatusId = completed.Id,
            Notes = "Delegation completed",
            ChangedAt = now,
            CreatedBy = Actor(),
            CreatedDate = now
        });
    }

    private async Task<Status> SingleStatusAsync(string name, CancellationToken ct)
    {
        var lower = name.ToLower();
        var matches = await _db.Statuses.Where(s => s.IsActive && !s.IsDeleted && s.Name.ToLower() == lower).Take(2).ToListAsync(ct);
        if (matches.Count != 1) throw new BusinessRuleException($"Exactly one active '{name}' status is required.");
        return matches[0];
    }

    private void AddSameStatusHistory(WorkflowInstance anchor, string? notes, string transitionType, string actor, DateTime now) =>
        _db.WorkflowHistory.Add(new WorkflowHistory
        {
            WorkflowInstanceId = anchor.Id,
            FromStatusId = anchor.StatusId,
            ToStatusId = anchor.StatusId,
            Notes = notes,
            ChangedAt = now,
            CreatedBy = actor,
            CreatedDate = now,
            TransitionType = transitionType
        });

    /// <summary>
    /// Execution/TAT values shown on a Delegation response. Every value comes from the central EaTask and its
    /// WorkPauses via the shared TatSummaryCalculator — the exact same formula Meeting itself uses — so
    /// nothing is duplicated onto ea_delegations. The public tatUsedMinutes here is the strict Meeting-
    /// aligned live value (matches tatSummary.tat in whole minutes); it is a different figure from the
    /// EaTask API's own internal CurrentTatUsedMinutes/TatUsedMinutes split, which stays untouched for the
    /// EaTask API and EM Report and is not surfaced on this response.
    /// </summary>
    private sealed record DelegationTatView(
        string ExecutionStatus, bool IsPaused, DateTime? StartedAt, DateTime? CompletedAt,
        int? AllottedTatMinutes, int? TatUsedMinutes,
        int? TatPausedMinutes, MeetingTatSummaryDto TatSummary);

    /// <summary>
    /// Meeting's own exact "unavailable TAT" fallback shape (MeetingService.GetByIdAsync's
    /// dto.TatSummary ??= ...): Tat null, TotalTat/PauseTime zero, PauseCount 0, no start/end/last-active.
    /// Reused verbatim for a Delegation with no delegationType (no TAT) rather than inventing a new shape.
    /// </summary>
    private static MeetingTatSummaryDto NoTatSummary() => new()
    {
        Tat = null, TotalTat = TimeSpan.Zero, TatDifference = TimeSpan.Zero, PauseTime = TimeSpan.Zero, PauseCount = 0
    };

    private async Task<DelegationTatView?> LoadTatViewAsync(Delegation delegation, CancellationToken ct) =>
        (await LoadTatViewsAsync(new[] { delegation }, ct)).GetValueOrDefault(delegation.EaTaskId);

    /// <summary>Two queries for any number of Delegations (tasks, then their pauses) — no per-row lookup.</summary>
    private async Task<Dictionary<long, DelegationTatView>> LoadTatViewsAsync(IEnumerable<Delegation> delegations, CancellationToken ct)
    {
        var eaTaskIds = delegations.Select(d => d.EaTaskId).Distinct().ToList();
        if (eaTaskIds.Count == 0) return new();
        var tasks = await _db.Tasks.AsNoTracking().Where(t => eaTaskIds.Contains(t.Id)).ToListAsync(ct);
        var workflowIds = tasks.Where(t => t.WorkflowInstanceId.HasValue).Select(t => t.WorkflowInstanceId!.Value).Distinct().ToList();
        var pausesByWorkflow = workflowIds.Count == 0
            ? new Dictionary<long, List<WorkPause>>()
            : (await _db.WorkPauses.AsNoTracking()
                .Where(p => p.WorkflowInstanceId != null && workflowIds.Contains(p.WorkflowInstanceId.Value) && !p.IsDeleted)
                .ToListAsync(ct)).GroupBy(p => p.WorkflowInstanceId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var now = Clock.UtcNowTz;
        return tasks.ToDictionary(t => t.Id, t =>
        {
            var pauses = t.WorkflowInstanceId.HasValue && pausesByWorkflow.TryGetValue(t.WorkflowInstanceId.Value, out var p) ? p : new List<WorkPause>();
            var isPaused = t.ExecutionStatus == EaTaskExecutionStatus.InProgress && pauses.Any(x => x.EndAt == null);

            // Strict Meeting-aligned public TAT contract: tatUsedMinutes/tatPausedMinutes/tatSummary all
            // come from the one shared TatSummaryCalculator formula Meeting itself uses (elapsed-minus-
            // paused, anchored on the central EaTask's StartedAt/CompletedAt rather than a WorkflowInstance),
            // gated and echoed in whole minutes exactly the way Meeting's own list flat fields are: null
            // until AllottedTatMinutes exists and the clock has started, live afterwards (including frozen-
            // while-paused, since an open pause's own formula holds "used" steady), stable once Completed
            // because tatSummary's end anchor becomes CompletedAt. No separate "current" value is computed
            // or exposed here — the central EaTask keeps its own internal frozen EaTask.TatUsedMinutes
            // column for the EaTask API and EM Report; that is untouched and is not part of this response.
            int? tatUsedMinutes = null;
            int? tatPausedMinutes = null;
            MeetingTatSummaryDto tatSummary;
            if (t.AllottedTatMinutes.HasValue)
            {
                tatSummary = TatSummaryCalculator.Calculate(t.AllottedTatMinutes.Value, t.StartedAt, t.CompletedAt, pauses, now);
                if (t.StartedAt.HasValue)
                {
                    tatUsedMinutes = (int)tatSummary.Tat!.Value.TotalMinutes;
                    tatPausedMinutes = (int)tatSummary.PauseTime.TotalMinutes;
                }
            }
            else
            {
                tatSummary = NoTatSummary();
            }

            return new DelegationTatView(t.ExecutionStatus, isPaused, t.StartedAt, t.CompletedAt, t.AllottedTatMinutes,
                tatUsedMinutes, tatPausedMinutes, tatSummary);
        });
    }

    // ============================================================
    // PER-PHASE TAT (Actual / Review N / Rework N) — each phase has its own TAT window and its own
    // Allotted/Used/Paused/PauseCount, resolved against its own (DelegationType, TaskType) TAT Rule.
    // See DelegationPhaseTat's own doc comment for the full model. Reuses TatSummaryCalculator
    // verbatim (never a second pause-math implementation), just scoped to each phase's own window —
    // GetPausedDuration already clips any pause to whatever [start, end) it is given.
    // ============================================================

    /// <summary>
    /// Soft lookup: null (no TAT for this phase) when nothing is configured, not an error — unlike the
    /// Actual phase's rule, which is a hard requirement enforced once at Delegation creation, a
    /// missing Review/Rework rule must never block Approve/Rework from proceeding.
    /// </summary>
    private async Task<(int? AllottedTatMinutes, long? TatRuleId)> ResolvePhaseTatAsync(long moduleId, string? delegationType, string taskType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(delegationType)) return (null, null);
        var applicable = await _tatRules.GetApplicableByTypeOnlyAsync(moduleId, delegationType, taskType, ct);
        return applicable.Count == 1 ? (applicable[0].TatMinutes, applicable[0].Id) : (null, null);
    }

    /// <summary>Opens (tracked, not yet saved) a new phase, resolving its own TAT rule fresh.</summary>
    private async Task OpenPhaseAsync(long delegationId, long moduleId, string? delegationType, string taskType, int reviewCycleNumber, DateTime now, CancellationToken ct)
    {
        var (allotted, ruleId) = await ResolvePhaseTatAsync(moduleId, delegationType, taskType, ct);
        var actor = Actor();
        _db.DelegationPhaseTats.Add(new DelegationPhaseTat
        {
            DelegationId = delegationId, TaskType = taskType, ReviewCycleNumber = reviewCycleNumber,
            StartedAt = now, AllottedTatMinutes = allotted, TatRuleId = ruleId,
            CreatedBy = actor, CreatedDate = now
        });
    }

    private async Task RequireNoOpenPauseAsync(long? workflowInstanceId, CancellationToken ct)
    {
        if (workflowInstanceId.HasValue && await _db.WorkPauses.AnyAsync(p =>
            p.WorkflowInstanceId == workflowInstanceId && !p.IsDeleted && p.EndAt == null, ct))
            throw new BusinessRuleException("Resume or continue open pauses/waiting before a phase transition.");
    }

    private async Task<DelegationPhaseTat> RequireReviewPhaseAsync(Delegation entity, CancellationToken ct)
    {
        var review = await _db.TaskReviews.AsNoTracking().Where(r => r.EaTaskId == entity.EaTaskId)
            .OrderByDescending(r => r.ReviewCycleNo).FirstOrDefaultAsync(ct);
        if (review is null || review.ReviewStatus != TaskReviewStatus.PendingReview)
            throw new BusinessRuleException("A pending review is required for this transition.");
        return await RequireOpenPhaseAsync(entity.Id, DelegationTaskType.Review, review.ReviewCycleNo, ct);
    }

    private async Task<DelegationPhaseTat> RequireOpenPhaseAsync(long delegationId, string taskType, int cycle, CancellationToken ct)
    {
        var phases = await _db.DelegationPhaseTats.Where(p => p.DelegationId == delegationId && p.EndedAt == null)
            .Take(2).ToListAsync(ct);
        if (phases.Count != 1 || phases[0].TaskType != taskType || phases[0].ReviewCycleNumber != cycle)
            throw new BusinessRuleException($"Delegation phase data is inconsistent: expected exactly one open {taskType} phase for cycle {cycle}. Historical data requires explicit repair.");
        return phases[0];
    }

    private async Task ClosePhaseAsync(DelegationPhaseTat phase, long? workflowInstanceId, DateTime now, CancellationToken ct)
    {
        var pauses = PausesOverlapping(await LoadDelegationWorkPausesAsync(workflowInstanceId, ct), phase.StartedAt, now);
        var summary = TatSummaryCalculator.Calculate(phase.AllottedTatMinutes ?? 0, phase.StartedAt, now, pauses, now);
        phase.EndedAt = now;
        phase.TatUsedMinutes = phase.AllottedTatMinutes.HasValue ? (int)summary.Tat!.Value.TotalMinutes : null;
        phase.TatPausedMinutes = (int)summary.PauseTime.TotalMinutes;
        phase.TatUsedSeconds = phase.AllottedTatMinutes.HasValue ? DurationSeconds(summary.Tat!.Value) : null;
        phase.TatPausedSeconds = DurationSeconds(summary.PauseTime);
        phase.PauseCount = summary.PauseCount;
        phase.ModifiedBy = Actor();
        phase.ModifiedDate = now;
        // Release the single-open-phase index entry before inserting the successor, within the same transaction.
        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<WorkPause>> LoadDelegationWorkPausesAsync(long? workflowInstanceId, CancellationToken ct) =>
        workflowInstanceId.HasValue
            ? await _db.WorkPauses.AsNoTracking().Where(p => p.WorkflowInstanceId == workflowInstanceId && !p.IsDeleted).ToListAsync(ct)
            : new List<WorkPause>();

    /// <summary>Simple pauses overlapping [start, windowEnd) — an open pause (EndAt null) always overlaps since it has no upper bound yet.</summary>
    private static List<WorkPause> PausesOverlapping(IReadOnlyCollection<WorkPause> pauses, DateTime start, DateTime windowEnd) =>
        pauses.Where(p => WorkPauseClassifier.IsSimplePause(p) && p.StartAt < windowEnd && (p.EndAt ?? DateTime.MaxValue) > start).ToList();

    /// <summary>Every phase this Delegation has been through, oldest first, frozen phases as stored and the current open one (if any) computed live.</summary>
    private async Task<List<DelegationPhaseTatDto>> BuildPhaseTatAsync(long delegationId, long? workflowInstanceId, CancellationToken ct)
    {
        var phases = await _db.DelegationPhaseTats.AsNoTracking()
            .Where(p => p.DelegationId == delegationId).OrderBy(p => p.Id).ToListAsync(ct);
        if (phases.Count == 0) return new();
        var pauses = await LoadDelegationWorkPausesAsync(workflowInstanceId, ct);
        var now = Clock.UtcNowTz;
        return phases.Select(p => ToPhaseTatDto(p, pauses, now)).ToList();
    }

    /// <summary>Batched version of BuildPhaseTatAsync for the register page — one query for every Delegation's phases, tasks and pauses, no per-row lookups.</summary>
    private async Task<Dictionary<long, List<DelegationPhaseTatDto>>> BuildPhaseTatBatchAsync(IReadOnlyCollection<Delegation> delegations, CancellationToken ct)
    {
        var delegationIds = delegations.Select(d => d.Id).ToList();
        if (delegationIds.Count == 0) return new();
        var phases = await _db.DelegationPhaseTats.AsNoTracking()
            .Where(p => delegationIds.Contains(p.DelegationId)).OrderBy(p => p.Id).ToListAsync(ct);
        if (phases.Count == 0) return new();

        var eaTaskIds = delegations.Select(d => d.EaTaskId).Distinct().ToList();
        var workflowIdByEaTaskId = await _db.Tasks.AsNoTracking().Where(t => eaTaskIds.Contains(t.Id))
            .Select(t => new { t.Id, t.WorkflowInstanceId }).ToDictionaryAsync(t => t.Id, t => t.WorkflowInstanceId, ct);
        var eaTaskIdByDelegation = delegations.ToDictionary(d => d.Id, d => d.EaTaskId);

        var workflowIds = workflowIdByEaTaskId.Values.Where(w => w.HasValue).Select(w => w!.Value).Distinct().ToList();
        var pausesByWorkflow = workflowIds.Count == 0
            ? new Dictionary<long, List<WorkPause>>()
            : (await _db.WorkPauses.AsNoTracking()
                .Where(p => p.WorkflowInstanceId != null && workflowIds.Contains(p.WorkflowInstanceId.Value) && !p.IsDeleted)
                .ToListAsync(ct)).GroupBy(p => p.WorkflowInstanceId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var now = Clock.UtcNowTz;
        return phases.GroupBy(p => p.DelegationId).ToDictionary(g => g.Key, g =>
        {
            var workflowId = eaTaskIdByDelegation.TryGetValue(g.Key, out var eaTaskId) ? workflowIdByEaTaskId.GetValueOrDefault(eaTaskId) : null;
            var pauses = workflowId.HasValue && pausesByWorkflow.TryGetValue(workflowId.Value, out var p) ? p : new List<WorkPause>();
            return g.Select(phase => ToPhaseTatDto(phase, pauses, now)).ToList();
        });
    }

    private static DelegationPhaseTatDto ToPhaseTatDto(DelegationPhaseTat phase, IReadOnlyCollection<WorkPause> pauses, DateTime now)
    {
        if (phase.EndedAt.HasValue)
        {
            return new DelegationPhaseTatDto
            {
                TaskType = phase.TaskType, ReviewCycleNumber = phase.ReviewCycleNumber,
                StartedAt = phase.StartedAt, EndedAt = phase.EndedAt,
                AllottedTatMinutes = phase.AllottedTatMinutes, TatUsedMinutes = phase.TatUsedMinutes,
                TatPausedMinutes = phase.TatPausedMinutes, PauseCount = phase.PauseCount,
                TatUsedSeconds = phase.TatUsedSeconds, TatPausedSeconds = phase.TatPausedSeconds,
                TatDifferenceSeconds = phase.AllottedTatMinutes.HasValue && phase.TatUsedSeconds.HasValue
                    ? phase.AllottedTatMinutes.Value * 60m - phase.TatUsedSeconds.Value : null,
                TatDifferenceMinutes = phase.AllottedTatMinutes.HasValue && phase.TatUsedMinutes.HasValue
                    ? phase.AllottedTatMinutes - phase.TatUsedMinutes : null,
            };
        }
        // The current, still-open phase — live-ticking, same formula, window end is "now" instead of a frozen EndedAt.
        var relevant = PausesOverlapping(pauses, phase.StartedAt, now);
        var summary = TatSummaryCalculator.Calculate(phase.AllottedTatMinutes ?? 0, phase.StartedAt, null, relevant, now);
        int? used = phase.AllottedTatMinutes.HasValue ? (int)summary.Tat!.Value.TotalMinutes : null;
        decimal? usedSeconds = phase.AllottedTatMinutes.HasValue ? DurationSeconds(summary.Tat!.Value) : null;
        return new DelegationPhaseTatDto
        {
            TaskType = phase.TaskType, ReviewCycleNumber = phase.ReviewCycleNumber,
            StartedAt = phase.StartedAt, EndedAt = null,
            AllottedTatMinutes = phase.AllottedTatMinutes, TatUsedMinutes = used,
            TatPausedMinutes = (int)summary.PauseTime.TotalMinutes, PauseCount = summary.PauseCount,
            TatUsedSeconds = usedSeconds, TatPausedSeconds = DurationSeconds(summary.PauseTime),
            TatDifferenceSeconds = phase.AllottedTatMinutes.HasValue && usedSeconds.HasValue
                ? phase.AllottedTatMinutes.Value * 60m - usedSeconds.Value : null,
            TatDifferenceMinutes = phase.AllottedTatMinutes.HasValue && used.HasValue ? phase.AllottedTatMinutes - used : null,
        };
    }

    // Decimal tick conversion avoids floating-point loss and retains TimeSpan's 100 ns precision.
    private static decimal DurationSeconds(TimeSpan duration) => duration.Ticks / (decimal)TimeSpan.TicksPerSecond;

    private static long? PdfId(Dictionary<long, long> ids, long delegationId) => ids.TryGetValue(delegationId, out var id) ? id : null;

    /// <summary>One ea_attachments row tied to a Delegation, with its purpose/reviewCycleNumber already parsed out of Metadata.</summary>
    private sealed record DelegationAttachmentRow(long Id, long DelegationId, string? Purpose, int? ReviewCycleNumber);

    /// <summary>
    /// Every active ea_attachments row for the given Delegations, in one query — completion PDFs and
    /// review/rework attachments alike (they share the same RelatedModule/RelatedEntity/RelatedEntityId
    /// triple; only Metadata.purpose tells them apart, exactly like DocumentRegisterService's own
    /// classification). Callers filter by Purpose for the specific kind they need.
    /// </summary>
    private async Task<List<DelegationAttachmentRow>> LoadDelegationAttachmentRowsAsync(IReadOnlyCollection<long> delegationIds, CancellationToken ct)
    {
        if (delegationIds.Count == 0) return new();
        var keys = delegationIds.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList();
        var rows = await _db.Attachments.AsNoTracking()
            .Where(a => a.RelatedModule == AttachmentRelatedModule && a.RelatedEntity == AttachmentRelatedEntity
                && a.RelatedEntityId != null && keys.Contains(a.RelatedEntityId) && a.IsActive && !a.IsDeleted)
            .Select(a => new { a.Id, a.RelatedEntityId, a.Metadata })
            .ToListAsync(ct);
        return rows.Select(r =>
        {
            var (purpose, cycle) = ParseAttachmentMetadata(r.Metadata);
            return new DelegationAttachmentRow(r.Id, long.Parse(r.RelatedEntityId!, CultureInfo.InvariantCulture), purpose, cycle);
        }).ToList();
    }

    /// <summary>Same best-effort JSON-metadata reading DocumentRegisterService uses — malformed/missing Metadata just yields nulls, never throws.</summary>
    private static (string? Purpose, int? ReviewCycleNumber) ParseAttachmentMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return (null, null);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadata);
            var purpose = doc.RootElement.TryGetProperty("purpose", out var p) ? p.GetString() : null;
            var cycle = doc.RootElement.TryGetProperty("reviewCycleNumber", out var c) && c.TryGetInt32(out var n) ? (int?)n : null;
            return (purpose, cycle);
        }
        catch (System.Text.Json.JsonException) { return (null, null); }
    }

    private static Dictionary<long, long> LatestIdPerDelegation(IEnumerable<DelegationAttachmentRow> rows) =>
        rows.GroupBy(r => r.DelegationId).ToDictionary(g => g.Key, g => g.Max(r => r.Id));

    /// <summary>Latest completion-PDF attachment id per Delegation.</summary>
    private async Task<Dictionary<long, long>> LoadCompletionPdfIdsAsync(IReadOnlyCollection<long> delegationIds, CancellationToken ct) =>
        LatestIdPerDelegation((await LoadDelegationAttachmentRowsAsync(delegationIds, ct)).Where(r => r.Purpose == CompletionPdfPurpose));

    /// <summary>Latest review/rework decision attachment id per (Delegation, review cycle) — approve and rework share one id space per cycle since a cycle is only ever one or the other.</summary>
    private async Task<Dictionary<(long DelegationId, int Cycle), long>> LoadReviewAttachmentIdsAsync(IReadOnlyCollection<long> delegationIds, CancellationToken ct)
    {
        var rows = await LoadDelegationAttachmentRowsAsync(delegationIds, ct);
        return rows.Where(r => (r.Purpose == ReviewAttachmentPurpose || r.Purpose == ReworkAttachmentPurpose) && r.ReviewCycleNumber.HasValue)
            .GroupBy(r => (r.DelegationId, Cycle: r.ReviewCycleNumber!.Value))
            .ToDictionary(g => g.Key, g => g.Max(r => r.Id));
    }

    /// <summary>Sets review.AttachmentId from the batch loaded above, for a single delegation's current cycle. No-op when never submitted (cycle 0).</summary>
    private static void ApplyReviewAttachment(TaskReviewSummaryDto? review, long delegationId, Dictionary<(long DelegationId, int Cycle), long> reviewAttachmentIds)
    {
        if (review is null || review.ReviewCycleNumber <= 0) return;
        review.AttachmentId = reviewAttachmentIds.TryGetValue((delegationId, review.ReviewCycleNumber), out var id) ? id : null;
    }

    /// <summary>
    /// Same rules as Meeting's completionPdf (MeetingCompletionFileStore.ValidateAsync): non-empty, at most 25 MiB,
    /// .pdf and application/pdf, %PDF- signature and a readable document with at least one page. Shared by the
    /// completion PDF and the review/rework decision attachment — all three are optional PDF uploads with
    /// identical validation, just stored under a different purpose/folder.
    /// </summary>
    private static async Task<byte[]> ReadValidatedPdfAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length <= 0 || file.Length > MaxCompletionPdfBytes)
            throw new BusinessRuleException("A nonempty PDF attachment of at most 25 MiB is required.");
        var originalFileName = Path.GetFileName(file.FileName?.Replace('\\', '/') ?? string.Empty);
        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 500)
            throw new BusinessRuleException("PDF attachment filename must contain 1 to 500 characters.");
        if (!string.Equals(Path.GetExtension(originalFileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Attachment must be a PDF.");

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > MaxCompletionPdfBytes) throw new BusinessRuleException("PDF attachment exceeds 25 MiB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var bytes = buffer.ToArray();
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new BusinessRuleException("PDF attachment signature is invalid.");
        try { using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes); if (pdf.NumberOfPages < 1) throw new InvalidDataException(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { throw new BusinessRuleException("Attachment is not a readable PDF."); }
        return bytes;
    }

    /// <summary>Writes the PDF to Content/{subfolder}/{delegationId}/ and returns the relative object key. Removes a partial file on failure.</summary>
    private async Task<string> WriteDelegationAttachmentAsync(string subfolder, long delegationId, byte[] content, CancellationToken ct)
    {
        var key = $"Content/{subfolder}/{delegationId.ToString(CultureInfo.InvariantCulture)}/{Guid.NewGuid():N}.pdf";
        var absolute = ResolveStoragePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        try
        {
            await using var stream = new FileStream(absolute, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await stream.WriteAsync(content, ct);
            await stream.FlushAsync(ct);
        }
        catch { TryDeleteFile(key); throw; }
        return key;
    }

    /// <summary>Resolves an object key under Content/ (path-traversal guarded).</summary>
    private string ResolveStoragePath(string objectKey)
    {
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Content")) + Path.DirectorySeparatorChar;
        var absolute = Path.GetFullPath(Path.Combine(_env.ContentRootPath, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid completion storage key.");
        return absolute;
    }

    private void TryDeleteFile(string objectKey)
    {
        try { var path = ResolveStoragePath(objectKey); if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
    }

    // ============================================================
    // LIST / SEARCH / FILTER / REGISTER
    // ============================================================

    public async Task<PagedResult<DelegationResponseDto>> ListAsync(DelegationListQueryDto query, CancellationToken ct = default)
    {
        var status = NormalizeStatus(query.Status);
        var view = NormalizeView(query.View);
        ValidateViewStatusCombination(view, status);

        var q = _db.Delegations.AsNoTracking()
            .Include(d => d.SourceBusinessModule)
            .Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(d =>
                d.ReferenceNo.ToLower().Contains(term) ||
                d.Title.ToLower().Contains(term) ||
                (d.DoerNameSnapshot != null && d.DoerNameSnapshot.ToLower().Contains(term)) ||
                (d.SourceReference != null && d.SourceReference.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.DoerId))
        {
            var id = query.DoerId.Trim();
            q = q.Where(d => d.DoerId == id);
        }

        if (!string.IsNullOrWhiteSpace(query.DelegationType))
        {
            var type = query.DelegationType.Trim().ToLower();
            q = q.Where(d => d.DelegationType != null && d.DelegationType.ToLower() == type);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            var p = query.Priority.Trim().ToLower();
            q = q.Where(d => d.Priority != null && d.Priority.ToLower() == p);
        }

        if (query.SourceBusinessModuleId.HasValue)
            q = q.Where(d => d.SourceBusinessModuleId == query.SourceBusinessModuleId.Value);

        if (query.EndDate.HasValue)
        {
            var day = query.EndDate.Value.Date;
            q = q.Where(d => d.DueDate.HasValue && d.DueDate.Value.Date == day);
        }

        var today = IndiaBusinessCalendar.Today;
        switch (view)
        {
            case "pending": q = q.Where(d => d.Status == DelegationStatus.Pending); break;
            case "inprogress": q = q.Where(d => d.Status == DelegationStatus.InProgress); break;
            case "completed": q = q.Where(d => d.Status == DelegationStatus.Completed); break;
            case "duetoday": q = q.Where(IsDueTodayExpr(today)); break;
            case "overdue": q = q.Where(IsOverdueExpr(today)); break;
                // "all" / null: no additional constraint from view.
        }

        if (status is not null)
            q = q.Where(d => d.Status == status);

        var page = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 50 : query.PageSize > 200 ? 200 : query.PageSize;

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderBy(d => d.DueDate.HasValue ? 0 : 1)
            .ThenBy(d => d.DueDate)
            .ThenByDescending(d => d.CreatedDate)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // One batched attachment lookup for the whole page (no per-row query) — completion PDFs and
        // review/rework decision attachments both come out of it. Not gated to Completed rows: Complete
        // now uploads the PDF and opens a review cycle while the Delegation is still InProgress
        // ("Pending Review"), so the document must already be visible before the assignee approves it.
        var delegationIds = items.Select(d => d.Id).ToList();
        var attachmentRows = await LoadDelegationAttachmentRowsAsync(delegationIds, ct);
        var pdfIds = LatestIdPerDelegation(attachmentRows.Where(r => r.Purpose == CompletionPdfPurpose));
        var reviewAttachmentIds = attachmentRows
            .Where(r => (r.Purpose == ReviewAttachmentPurpose || r.Purpose == ReworkAttachmentPurpose) && r.ReviewCycleNumber.HasValue)
            .GroupBy(r => (DelegationId: r.DelegationId, Cycle: r.ReviewCycleNumber!.Value))
            .ToDictionary(g => g.Key, g => g.Max(r => r.Id));
        var tatViews = await LoadTatViewsAsync(items, ct);
        var reviewSummaries = await _taskReview.BatchGetCurrentAsync(items.Select(d => d.EaTaskId).Distinct().ToList(), ct) ?? new();
        var phaseTatByDelegation = await BuildPhaseTatBatchAsync(items, ct);
        return new PagedResult<DelegationResponseDto>
        {
            Items = items.Select(d =>
            {
                var review = reviewSummaries.GetValueOrDefault(d.EaTaskId);
                ApplyReviewAttachment(review, d.Id, reviewAttachmentIds);
                return ToDto(d, d.SourceBusinessModule?.Name, PdfId(pdfIds, d.Id), tatViews.GetValueOrDefault(d.EaTaskId), review, phaseTatByDelegation.GetValueOrDefault(d.Id));
            }).ToArray(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // ============================================================
    // KPI SUMMARY
    // ============================================================

    public async Task<DelegationSummaryResponseDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = IndiaBusinessCalendar.Today;
        var active = _db.Delegations.AsNoTracking().Where(d => !d.IsDeleted);

        return new DelegationSummaryResponseDto
        {
            Total = await active.CountAsync(ct),
            Pending = await active.CountAsync(d => d.Status == DelegationStatus.Pending, ct),
            InProgress = await active.CountAsync(d => d.Status == DelegationStatus.InProgress, ct),
            // Same predicate expressions the register's view=dueToday/overdue filters use —
            // one canonical calculation, not a separately maintained copy.
            DueToday = await active.CountAsync(IsDueTodayExpr(today), ct),
            Overdue = await active.CountAsync(IsOverdueExpr(today), ct),
            Completed = await active.CountAsync(d => d.Status == DelegationStatus.Completed, ct)
        };
    }

    // ============================================================
    // HELPERS
    // ============================================================

    /// <summary>
    /// Canonical Due Today / Overdue conditions, shared verbatim between the register's
    /// view=dueToday/overdue filter, the KPI summary counts, and (compiled) the per-item
    /// IsDueToday/IsOverdue response flags — one calculation, not three. A completed
    /// Delegation is never due-today/overdue regardless of DueDate.
    /// </summary>
    private static Expression<Func<Delegation, bool>> IsDueTodayExpr(DateTime today) =>
        d => d.Status != DelegationStatus.Completed && d.DueDate.HasValue && d.DueDate.Value.Date == today;

    private static Expression<Func<Delegation, bool>> IsOverdueExpr(DateTime today) =>
        d => d.Status != DelegationStatus.Completed && d.DueDate.HasValue && d.DueDate.Value.Date < today;

    /// <summary>
    /// Same row-locking convention already proven by Travel's LockTravelParentAsync:
    /// FOR UPDATE under Postgres so two concurrent Start (or two concurrent Complete)
    /// requests serialize instead of both reading the pre-transition status and both
    /// succeeding; a plain tracked read under EF InMemory, which supports the lifecycle
    /// state-machine tests but not row locking/rollback.
    /// </summary>
    private async Task<Delegation> LockDelegationAsync(long id, CancellationToken ct)
    {
        Delegation? entity;
        if (_db.Database.IsRelational())
        {
            var rows = await _db.Delegations.FromSqlInterpolated(
                $"SELECT * FROM public.ea_delegations WHERE \"Id\" = {id} AND NOT \"IsDeleted\" FOR UPDATE")
                .ToListAsync(ct);
            entity = rows.SingleOrDefault();
            if (entity is not null) await _db.Entry(entity).ReloadAsync(ct);
        }
        else
        {
            entity = await _db.Delegations.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        }
        return entity ?? throw new NotFoundException($"Delegation {id} not found.");
    }

    /// <summary>SourceBusinessModuleId never changes during a lifecycle action, so it's
    /// resolved fresh here rather than requiring the caller to have an Include loaded.
    /// Null means a direct/manual Delegation with no originating module — returns null
    /// rather than resolving anything.</summary>
    private async Task<string?> ResolveSourceModuleNameAsync(long? sourceBusinessModuleId, CancellationToken ct) =>
        sourceBusinessModuleId.HasValue
            ? await _db.BusinessModules.AsNoTracking()
                .Where(m => m.Id == sourceBusinessModuleId.Value)
                .Select(m => m.Name)
                .SingleAsync(ct)
            : null;

    private string Actor() => _user.UserName ?? _user.UserId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Priority is a frontend-owned business string (PriorityLevel is optional discovery
    /// data for a dropdown, not a persistence gate). Only whitespace is trimmed; any
    /// submitted value, including one PriorityLevel doesn't know about, is stored as-is.
    /// </summary>
    /// <summary>Frontend-supplied free text: trimmed, blank becomes null, at most 200 characters. No enum or catalog.</summary>
    private static string? NormalizeDelegationType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > 200) throw new BadRequestException("delegationType must be at most 200 characters.");
        return trimmed;
    }

    private static string? NormalizePriority(string? priority) =>
        string.IsNullOrWhiteSpace(priority) ? null : priority.Trim();

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var s = status.Trim();
        if (string.Equals(s, DelegationStatus.Pending, StringComparison.OrdinalIgnoreCase)) return DelegationStatus.Pending;
        if (string.Equals(s, DelegationStatus.InProgress, StringComparison.OrdinalIgnoreCase)) return DelegationStatus.InProgress;
        if (string.Equals(s, DelegationStatus.Completed, StringComparison.OrdinalIgnoreCase)) return DelegationStatus.Completed;
        throw new BadRequestException($"Unsupported status '{status}'.");
    }

    private static string? NormalizeView(string? view)
    {
        if (string.IsNullOrWhiteSpace(view)) return null;
        var v = view.Trim().ToLowerInvariant();
        if (v is "all" or "pending" or "inprogress" or "duetoday" or "overdue" or "completed") return v == "all" ? null : v;
        throw new BadRequestException($"Unsupported view '{view}'.");
    }

    /// <summary>
    /// Deterministically rejects a status/view combination that can never match anything,
    /// rather than silently letting one filter shadow the other.
    /// </summary>
    private static void ValidateViewStatusCombination(string? view, string? status)
    {
        if (view is null || status is null) return;
        var expected = view switch
        {
            "pending" => DelegationStatus.Pending,
            "inprogress" => DelegationStatus.InProgress,
            "completed" => DelegationStatus.Completed,
            _ => null
        };
        if (expected is not null && expected != status)
            throw new BadRequestException($"view '{view}' conflicts with status '{status}'.");
        if ((view is "duetoday" or "overdue") && status == DelegationStatus.Completed)
            throw new BadRequestException($"view '{view}' conflicts with status '{status}' (Completed is excluded from {view}).");
    }

    private static DelegationResponseDto ToDto(Delegation d, string? sourceModuleName, long? completionPdfAttachmentId = null, DelegationTatView? tat = null, TaskReviewSummaryDto? reviewSummary = null, List<DelegationPhaseTatDto>? phaseTat = null)
    {
        var today = IndiaBusinessCalendar.Today;
        // Compiled from the exact same expressions used for the register view filter and
        // the KPI summary counts — see IsDueTodayExpr/IsOverdueExpr.
        var isDueToday = IsDueTodayExpr(today).Compile()(d);
        var isOverdue = IsOverdueExpr(today).Compile()(d);

        return new DelegationResponseDto
        {
            DelegationId = d.Id,
            ReferenceNo = d.ReferenceNo,
            EaTaskId = d.EaTaskId,

            Title = d.Title,
            Description = d.Description,

            DelegationType = d.DelegationType,

            DoerId = d.DoerId,
            DoerName = d.DoerNameSnapshot,

            AssignedById = d.AssignedById,
            AssignedByName = d.AssignedByNameSnapshot,

            Priority = d.Priority,
            StartDate = d.StartDate,
            EndDate = d.DueDate,

            Status = d.Status,

            SourceBusinessModuleId = d.SourceBusinessModuleId,
            SourceModuleName = sourceModuleName,
            SourceEntityId = d.SourceEntityId,
            SourceReference = d.SourceReference,

            AdditionalNotes = d.AdditionalNotes,

            // The central EaTask is authoritative (the TAT clock runs from its StartedAt); the Delegation's own copy is the fallback.
            StartedAt = tat?.StartedAt ?? d.StartedAt,
            CompletedAt = tat?.CompletedAt ?? d.CompletedAt,
            CompletedById = d.CompletedById,
            CompletedByName = d.CompletedByNameSnapshot,
            CompletionPdfAttachmentId = completionPdfAttachmentId,
            IsPaused = tat?.IsPaused ?? false,
            ExecutionStatus = tat?.ExecutionStatus ?? EaTaskExecutionStatus.NotStarted,
            AllottedTatMinutes = tat?.AllottedTatMinutes,
            TatUsedMinutes = tat?.TatUsedMinutes,
            TatPausedMinutes = tat?.TatPausedMinutes,
            TatSummary = tat?.TatSummary ?? NoTatSummary(),
            ReviewSummary = reviewSummary ?? new TaskReviewSummaryDto(),
            PhaseTat = phaseTat ?? new List<DelegationPhaseTatDto>(),

            IsDueToday = isDueToday,
            IsOverdue = isOverdue,

            CreatedAt = d.CreatedDate,
            UpdatedAt = d.ModifiedDate
        };
    }
}
