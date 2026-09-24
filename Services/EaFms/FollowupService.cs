using AutoMapper;
using System.Globalization;
using System.Net.Mail;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class FollowupService : IFollowupService
{
    /// <summary>The Follow-up-owning BusinessModule name — the Followup's OWN execution task
    /// (Start/Pause/Resume/Complete, Actual phase only), a completely separate concept from
    /// BusinessModuleId/BusinessRecordId (what other record this follow-up is chasing).</summary>
    public const string FollowupBusinessModuleName = "Follow-up";

    private readonly IFollowupRepository _repo;
    private readonly EaFmsDbContext _context;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IFollowupSourceResolver _sourceResolver;
    private readonly IEaTaskService _eaTaskService;
    private readonly ITatRuleRepository _tatRules;

    public FollowupService(IFollowupRepository repo, EaFmsDbContext context, IMapper mapper, ICurrentUserService currentUser,
        IAuditService auditService, IFollowupSourceResolver sourceResolver, IEaTaskService eaTaskService, ITatRuleRepository tatRules)
    {
        _repo = repo;
        _context = context;
        _mapper = mapper;
        _currentUser = currentUser;
        _auditService = auditService;
        _sourceResolver = sourceResolver;
        _eaTaskService = eaTaskService;
        _tatRules = tatRules;
    }

    private string Actor() => _currentUser.ActorDisplay();

    public async Task<FollowupResponseDto> CreateAsync(CreateFollowupRequestDto dto, CancellationToken ct = default)
    {
        var validation = new Jarvis5.Validators.CreateFollowupRequestDtoValidator().Validate(dto);
        if (!validation.IsValid) throw new BadRequestException(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        if (dto.IntakeRequestId.HasValue && !await _context.IntakeRequests.AnyAsync(i => i.Id == dto.IntakeRequestId && !i.IsDeleted, ct))
            throw new NotFoundException($"Intake {dto.IntakeRequestId} not found.");
        if (dto.BusinessModuleId.HasValue)
        {
            // Source identity is BusinessModuleId + BusinessRecordId. The resolver matches modules by
            // name (no numeric ids) and proves the source record exists.
            var source = await _sourceResolver.ValidateAsync(dto.BusinessModuleId.Value, dto.BusinessRecordId!, ct);
            if ((dto.IntakeRequestId.HasValue && source.IntakeRequestId.HasValue && dto.IntakeRequestId != source.IntakeRequestId)
                || (dto.WorkflowInstanceId.HasValue && source.WorkflowInstanceId.HasValue && dto.WorkflowInstanceId != source.WorkflowInstanceId))
                throw new BadRequestException($"Follow-up intake/workflow source does not match the {source.ModuleName} record.");
            dto.BusinessRecordId = source.BusinessRecordId;
        }

        if (dto.WorkflowInstanceId.HasValue)
        {
            var wf = await _context.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == dto.WorkflowInstanceId.Value && !w.IsDeleted, ct);
            if (wf is null) throw new Jarvis5.Common.NotFoundException($"Workflow {dto.WorkflowInstanceId} not found.");
            if (dto.BusinessModuleId.HasValue && wf.BusinessModuleId.HasValue
                && (dto.BusinessModuleId != wf.BusinessModuleId || dto.BusinessRecordId != wf.BusinessRecordId))
                throw new BadRequestException("Follow-up source does not match the workflow source.");
            // ensure workflow belongs to intake
            if (dto.IntakeRequestId.HasValue && wf.IntakeRequestId != dto.IntakeRequestId) throw new Jarvis5.Common.BadRequestException("Workflow does not belong to intake.");
        }

        if (dto.PriorityLevelId.HasValue)
        {
            var pr = await _context.PriorityLevels.FirstOrDefaultAsync(p => p.Id == dto.PriorityLevelId.Value && p.IsActive && !p.IsDeleted, ct);
            if (pr is null) throw new Jarvis5.Common.NotFoundException($"PriorityLevel {dto.PriorityLevelId} not found or inactive.");
        }

        var reminderWhatsApp = ValidateReminder(dto.ReminderRecipientEmail, dto.ReminderAt, dto.ReminderSendEmail, dto.ReminderSendWhatsApp,
            dto.ReminderRecipientUserId, dto.ReminderWhatsAppNumber);

        var now = Clock.UtcNowTz;
        var actor = EaActorSnapshot.From(_currentUser.ActorId(), _currentUser.ActorName());
        var by = actor.DisplayName ?? _currentUser.ActorDisplay();
        var type = dto.Type?.Trim();

        // ---- Resolve the Follow-up-owning BusinessModule (canonical name; never hardcode ids) ----
        // Completely separate from dto.BusinessModuleId above (WHERE the follow-up comes from) —
        // this is the module that owns the Followup's OWN execution task.
        var followupModule = await _context.BusinessModules.AsNoTracking()
            .Where(m => !m.IsDeleted && m.IsActive && m.Name == FollowupBusinessModuleName)
            .FirstOrDefaultAsync(ct);
        if (followupModule is null)
            throw new BusinessRuleException(
                "FOLLOW-UP BUSINESS MODULE CONFIGURATION REQUIRED BEFORE RUNTIME FOLLOWUP CREATION. " +
                $"Insert an active '{FollowupBusinessModuleName}' record into ea_business_modules.");

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        // ---- Create the Followup's own central EaTask before the Followup ----
        // Followup.Id is not known yet, so BusinessRecordId is seeded with a temporary unique
        // value and corrected to the real Followup.Id below — the same two-phase pattern
        // DelegationService.CreateCoreAsync already uses for the identical reason.
        var eaTaskDto = await _eaTaskService.CreateWithoutTatAsync(new CreateEaTaskDto
        {
            ModuleId = followupModule.Id,
            BusinessRecordId = Guid.NewGuid().ToString("N"),
            Task = string.IsNullOrWhiteSpace(dto.Subject) ? "Follow-up" : dto.Subject.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            WorkflowInstanceId = null
        }, ct);

        // Soft type-only TAT lookup: "no rule configured" is never an error here (unlike
        // Delegation's hard-required Actual-phase rule) — see FollowupBusinessModuleName's own
        // comment and EaTaskService.NoTatAuthorizedModules for why.
        var (allottedTatMinutes, tatRuleId) = await ResolveFollowupTatAsync(followupModule.Id, type, ct);

        var followup = new Followup
        {
            EaTaskId = eaTaskDto.EaTaskId,
            BusinessModuleId = dto.BusinessModuleId,
            BusinessRecordId = dto.BusinessRecordId,
            IntakeRequestId = dto.IntakeRequestId,
            WorkflowInstanceId = dto.WorkflowInstanceId,
            Subject = dto.Subject?.Trim(),
            Type = dto.Type?.Trim(),
            DueAt = dto.DueAt,
            Note = ResolveRemark(dto.Remark, dto.Note),
            DoerId = dto.DoerId,
            DoerName = dto.DoerName,
            PriorityLevelId = dto.PriorityLevelId,
            ReminderAt = dto.ReminderAt,
            ReminderSendEmail = dto.ReminderSendEmail,
            ReminderSendWhatsApp = dto.ReminderSendWhatsApp,
            ReminderRecipientUserId = dto.ReminderRecipientUserId,
            ReminderRecipientEmployeeId = NullIfBlank(dto.ReminderRecipientEmployeeId),
            ReminderRecipientName = NullIfBlank(dto.ReminderRecipientName),
            ReminderWhatsAppNumber = reminderWhatsApp,
            ReminderRecipientEmail = NullIfBlank(dto.ReminderRecipientEmail),
            NextFollowupAt = dto.NextFollowupAt,
            ResponseOwnerId = dto.ResponseOwnerId,
            ResponseOwnerName = dto.ResponseOwnerName,
            ExpectedResponseAt = dto.ExpectedResponseAt,
            WaitingOnId = dto.WaitingOnId,
            WaitingOnName = dto.WaitingOnName,
            WaitingOnExternal = dto.WaitingOnExternal,
            SequenceNumber = dto.SequenceNumber,
            IsDeleted = false,
            CreatedBy = by,
            CreatedByEmployeeId = actor.EmployeeId,
            CreatedByEmployeeName = actor.EmployeeName,
            CreatedDate = now
        };

        await _repo.AddAsync(followup, ct);
        await _context.SaveChangesAsync(ct);

        // ---- Correct the EaTask's BusinessRecordId to the real Followup.Id, and write its
        // resolved (possibly null) TAT snapshot — same two-phase pattern as Delegation. ----
        var eaTask = await _context.Tasks.FirstAsync(t => t.Id == eaTaskDto.EaTaskId, ct);
        eaTask.BusinessRecordId = followup.Id.ToString(CultureInfo.InvariantCulture);
        eaTask.AllottedTatMinutes = allottedTatMinutes;
        eaTask.TatRuleId = tatRuleId;
        await _context.SaveChangesAsync(ct);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_CREATE",
            module: "Followup",
            entityName: nameof(Followup),
            entityId: followup.Id.ToString(),
            oldValues: null,
            newValues: new
            {
                followup.Id,
                followup.EaTaskId,
                AllottedTatMinutes = allottedTatMinutes,
                followup.BusinessModuleId,
                followup.BusinessRecordId,
                followup.IntakeRequestId,
                followup.WorkflowInstanceId,
                followup.Subject,
                followup.Type,
                followup.Note,
                followup.DoerId,
                followup.DoerName,
                followup.PriorityLevelId,
                followup.DueAt,
                followup.ReminderAt,
                followup.ReminderSendEmail,
                followup.ReminderSendWhatsApp,
                followup.ReminderRecipientUserId,
                followup.ReminderWhatsAppNumber,
                LastFollowupAt = (DateTime?)null,
                followup.NextFollowupAt,
                followup.WaitingOnId,
                followup.WaitingOnName,
                followup.WaitingOnExternal,
                followup.ResponseOwnerId,
                followup.ResponseOwnerName,
                followup.ExpectedResponseAt,
                followup.SequenceNumber,
                Actor = actor
            },
            description: "Followup created");

        await _context.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return await EnrichAsync(followup, ct);
    }

    /// <summary>
    /// Soft lookup: null (no TAT) when nothing is configured or Type is blank — never an error.
    /// Unlike Delegation's Actual-phase rule (hard-required once delegationType is set), a
    /// missing Follow-up TAT rule must never block Followup creation. Ambiguous configuration
    /// (more than one active rule) is still a real error — that is never silently swallowed.
    /// </summary>
    private async Task<(int? AllottedTatMinutes, long? TatRuleId)> ResolveFollowupTatAsync(long followupModuleId, string? type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(type)) return (null, null);
        var applicable = await _tatRules.GetApplicableByTypeOnlyAsync(followupModuleId, type, DelegationTaskType.Actual, ct);
        if (applicable.Count > 1)
            throw new BusinessRuleException("Multiple active TAT rules are configured for this Follow-up type.");
        return applicable.Count == 1 ? (applicable[0].TatMinutes, applicable[0].Id) : (null, null);
    }

    /// <summary>
    /// Technical rules for the reminder configuration (configuration only - nothing is sent).
    /// Returns the trimmed WhatsApp number (null when blank).
    /// </summary>
    /// <summary>
    /// Remark is the canonical name for Followup.Note. A caller may send either (old callers send Note);
    /// sending both with different values is ambiguous and rejected, so there is one stored value.
    /// </summary>
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ResolveRemark(string? remark, string? note)
    {
        var r = remark?.Trim();
        var n = note?.Trim();
        if (!string.IsNullOrEmpty(r) && !string.IsNullOrEmpty(n) && !string.Equals(r, n, StringComparison.Ordinal))
            throw new BadRequestException("Remark and Note are the same field; send only Remark (or identical values).");
        return string.IsNullOrEmpty(r) ? (string.IsNullOrEmpty(n) ? null : n) : r;
    }

    private static string? ValidateReminder(string? reminderEmail, DateTime? reminderAt, bool sendEmail, bool sendWhatsApp,
        int? recipientUserId, string? whatsAppNumber)
    {
        var number = NullIfBlank(whatsAppNumber);
        var email = NullIfBlank(reminderEmail);
        if (number is { Length: > 50 }) throw new BadRequestException("ReminderWhatsAppNumber must not exceed 50 characters.");
        if (recipientUserId.HasValue && recipientUserId.Value <= 0)
            throw new BadRequestException("ReminderRecipientUserId must be greater than 0.");
        if (sendEmail)
        {
            if (!reminderAt.HasValue) throw new BadRequestException("ReminderAt is required when ReminderSendEmail is true.");
            if (!IsUsableEmail(email)) throw new BadRequestException("ReminderRecipientEmail is required and must be a valid email address when ReminderSendEmail is true.");
        }
        if (sendWhatsApp)
        {
            if (!reminderAt.HasValue) throw new BadRequestException("ReminderAt is required when ReminderSendWhatsApp is true.");
            if (number is null) throw new BadRequestException("ReminderWhatsAppNumber is required when ReminderSendWhatsApp is true.");
        }
        return number;
    }

    private static bool IsUsableEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try { return string.Equals(new MailAddress(email).Address, email, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }

    /// <summary>
    /// Returns the persisted frontend-supplied recipient snapshot with a prefilled subject/body and a mailto: URL for a user-initiated
    /// email (same handoff pattern as SendWhatsAppAsync). Nothing is sent from the server, so no SMTP configuration is involved.
    /// </summary>
    public async Task<FollowupEmailActionResponseDto> SendEmailAsync(long id, CancellationToken ct = default)
    {
        var followup = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Followup {id} not found.");
        if (!followup.ReminderSendEmail) throw new BadRequestException("ReminderSendEmail must be true to send a reminder email.");
        if (!IsUsableEmail(followup.ReminderRecipientEmail))
            throw new BadRequestException("ReminderRecipientEmail is required and must be a valid email address to send a reminder email.");
        var context = await EnrichAsync(followup, ct);
        var email = followup.ReminderRecipientEmail!.Trim();
        var subject = $"Reminder / Follow-up - {context.Task ?? string.Empty}";
        var body = BuildReminderEmailBody(context);
        return new FollowupEmailActionResponseDto { Email = email, Subject = subject, Body = body, MailtoUrl = BuildMailtoUrl(email, subject, body) };
    }

    /// <summary>RFC 6068: mailto:{address}?subject={encoded}&amp;body={encoded}; spaces become %20 and line breaks %0D%0A.</summary>
    internal static string BuildMailtoUrl(string email, string subject, string body)
    {
        var address = Uri.EscapeDataString(email).Replace("%40", "@");
        var crlfBody = body.Replace("\r\n", "\n").Replace("\n", "\r\n");
        return $"mailto:{address}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(crlfBody)}";
    }
    /// <summary>Returns the persisted frontend-supplied phone snapshot and message for a frontend-controlled WhatsApp handoff.</summary>
    public async Task<FollowupWhatsAppActionResponseDto> SendWhatsAppAsync(long id, CancellationToken ct = default)
    {
        var followup = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Followup {id} not found.");
        if (!followup.ReminderSendWhatsApp)
            throw new BadRequestException("ReminderSendWhatsApp must be true to send a WhatsApp reminder.");
        var phone = NullIfBlank(followup.ReminderWhatsAppNumber);
        if (phone is null)
            throw new BadRequestException("ReminderWhatsAppNumber is required to send a WhatsApp reminder.");

        var context = await EnrichAsync(followup, ct);
        return new FollowupWhatsAppActionResponseDto { Phone = phone, Message = BuildWhatsAppMessage(context) };
    }
    public async Task<FollowupResponseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        return await EnrichAsync(f, ct);
    }

    public async Task<List<FollowupResponseDto>> GetByIntakeRequestIdAsync(long intakeRequestId, CancellationToken ct = default)
    {
        var list = await _repo.GetByIntakeRequestIdAsync(intakeRequestId, ct);
        return await EnrichListAsync(list, ct);
    }

    public async Task<FollowupResponseDto> UpdateAsync(long id, UpdateFollowupRequestDto dto, CancellationToken ct = default)
    {
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        if (dto.PriorityLevelId.HasValue && dto.PriorityLevelId != f.PriorityLevelId
            && !await _context.PriorityLevels.AnyAsync(p => p.Id == dto.PriorityLevelId && p.IsActive && !p.IsDeleted, ct))
            throw new NotFoundException($"PriorityLevel {dto.PriorityLevelId} not found or inactive.");
        var reminderWhatsApp = ValidateReminder(dto.ReminderRecipientEmail, dto.ReminderAt, dto.ReminderSendEmail, dto.ReminderSendWhatsApp,
            dto.ReminderRecipientUserId, dto.ReminderWhatsAppNumber);
        var oldSnap = new { f.Id, f.DueAt, f.Note, f.ReminderAt, f.ReminderSendEmail, f.ReminderSendWhatsApp, f.ReminderRecipientUserId, f.ReminderRecipientEmployeeId, f.ReminderRecipientName, f.ReminderWhatsAppNumber, f.ReminderRecipientEmail };
        var newType = dto.Type?.Trim();
        if (f.EaTaskId.HasValue && !string.Equals(f.Type, newType, StringComparison.Ordinal))
        {
            // Re-resolve the Actual-phase TAT rule only before Start — once started, the phase's
            // snapshot (taken from the EaTask at Start time) is frozen, exactly like Delegation's
            // own Actual phase never re-resolves after the doer has begun.
            var eaTaskForType = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == f.EaTaskId!.Value, ct);
            if (eaTaskForType is not null && eaTaskForType.ExecutionStatus == EaTaskExecutionStatus.NotStarted)
            {
                var (allotted, ruleId) = await ResolveFollowupTatAsync(eaTaskForType.BusinessModuleId, newType, ct);
                eaTaskForType.AllottedTatMinutes = allotted;
                eaTaskForType.TatRuleId = ruleId;
                eaTaskForType.Task = string.IsNullOrWhiteSpace(dto.Subject) ? eaTaskForType.Task : dto.Subject.Trim();
                eaTaskForType.Type = newType;
            }
        }
        f.Subject = dto.Subject?.Trim();
        f.Type = newType;
        f.DueAt = dto.DueAt;
        f.Note = ResolveRemark(dto.Remark, dto.Note);
        f.DoerId = dto.DoerId;
        f.DoerName = dto.DoerName;
        f.PriorityLevelId = dto.PriorityLevelId;
        f.ReminderAt = dto.ReminderAt;
        f.ReminderSendEmail = dto.ReminderSendEmail;
        f.ReminderSendWhatsApp = dto.ReminderSendWhatsApp;
        f.ReminderRecipientUserId = dto.ReminderRecipientUserId;
        f.ReminderRecipientEmployeeId = NullIfBlank(dto.ReminderRecipientEmployeeId);
        f.ReminderRecipientName = NullIfBlank(dto.ReminderRecipientName);
        f.ReminderWhatsAppNumber = reminderWhatsApp;
        f.ReminderRecipientEmail = NullIfBlank(dto.ReminderRecipientEmail);
        f.NextFollowupAt = dto.NextFollowupAt;
        f.ResponseOwnerId = dto.ResponseOwnerId;
        f.ResponseOwnerName = dto.ResponseOwnerName;
        f.ExpectedResponseAt = dto.ExpectedResponseAt;
        f.WaitingOnId = dto.WaitingOnId;
        f.WaitingOnName = dto.WaitingOnName;
        f.WaitingOnExternal = dto.WaitingOnExternal;
        f.SequenceNumber = dto.SequenceNumber;
        var actor = EaActorSnapshot.From(_currentUser.ActorId(), _currentUser.ActorName());
        f.ModifiedBy = actor.DisplayName ?? _currentUser.ActorDisplay();
        f.ModifiedByEmployeeId = actor.EmployeeId;
        f.ModifiedByEmployeeName = actor.EmployeeName;
        f.ModifiedDate = Clock.UtcNowTz;

        _repo.Update(f);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_UPDATE",
            module: "Followup",
            entityName: nameof(Followup),
            entityId: f.Id.ToString(),
            oldValues: oldSnap,
            newValues: new
            {
                f.Id,
                f.Subject,
                f.Type,
                f.Note,
                f.DoerId,
                f.DoerName,
                f.PriorityLevelId,
                f.DueAt,
                f.ReminderAt,
                f.ReminderSendEmail,
                f.ReminderSendWhatsApp,
                f.ReminderRecipientUserId,
                f.ReminderWhatsAppNumber,
                f.NextFollowupAt,
                f.WaitingOnId,
                f.WaitingOnName,
                f.WaitingOnExternal,
                f.ResponseOwnerId,
                f.ResponseOwnerName,
                f.ExpectedResponseAt,
                f.SequenceNumber,
                f.ModifiedBy,
                f.ModifiedDate,
                Actor = actor
            },
            description: "Followup updated");

        await _context.SaveChangesAsync(ct);

        return await EnrichAsync(f, ct);
    }

    // ============================================================
    // OWN EXECUTION LIFECYCLE — START / PAUSE / RESUME / COMPLETE (Actual phase only, no Review/Rework)
    // ============================================================

    public async Task<FollowupResponseDto> StartAsync(long id, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var f = await RequireFollowupWithTaskAsync(id, ct);
        var eaTask = await _context.Tasks.FirstAsync(t => t.Id == f.EaTaskId!.Value, ct);
        if (eaTask.ExecutionStatus != EaTaskExecutionStatus.NotStarted)
            throw new BusinessRuleException($"Follow-up cannot be started from its current status '{eaTask.ExecutionStatus}'.");
        if (await _context.FollowupPhaseTats.AnyAsync(p => p.FollowupId == id, ct))
            throw new BusinessRuleException("Cannot start a Follow-up with existing phase history.");

        var now = Clock.UtcNowTz;
        var actor = Actor();
        eaTask.ExecutionStatus = EaTaskExecutionStatus.InProgress;
        eaTask.StartedAt = now;

        _context.FollowupPhaseTats.Add(new FollowupPhaseTat
        {
            FollowupId = f.Id, TaskType = DelegationTaskType.Actual, ReviewCycleNumber = 0,
            StartedAt = now, StartedById = _currentUser.ActorId(), StartedByName = _currentUser.ActorName(),
            AllottedTatMinutes = eaTask.AllottedTatMinutes, TatRuleId = eaTask.TatRuleId,
            CreatedBy = actor, CreatedDate = now
        });

        f.ModifiedBy = actor;
        f.ModifiedDate = now;
        _repo.Update(f);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_START", module: "Followup", entityName: nameof(Followup),
            entityId: f.Id.ToString(),
            oldValues: new { ExecutionStatus = EaTaskExecutionStatus.NotStarted },
            newValues: new { ExecutionStatus = eaTask.ExecutionStatus, eaTask.StartedAt },
            description: "Followup started");

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await EnrichAsync(f, ct);
    }

    public async Task<FollowupResponseDto> PauseAsync(long id, FollowupPauseRequestDto? request, CancellationToken ct = default)
    {
        var reason = string.IsNullOrWhiteSpace(request?.PauseReason) ? "Follow-up paused" : request!.PauseReason!.Trim();
        if (reason.Length > 2000) throw new BadRequestException("pauseReason must be at most 2000 characters.");

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var f = await RequireFollowupWithTaskAsync(id, ct);
        var eaTask = await _context.Tasks.FirstAsync(t => t.Id == f.EaTaskId!.Value, ct);
        RequireInProgress(eaTask, "paused");

        var now = Clock.UtcNowTz;
        var anchor = await EnsureFollowupAnchorAsync(f, eaTask, now, ct);

        if (await _context.WorkPauses.AnyAsync(p => p.WorkflowInstanceId == anchor.Id && !p.IsDeleted && p.EndAt == null, ct))
            throw new BusinessRuleException("Follow-up is already paused. Resume it first.");

        var actor = Actor();
        // FollowupId is deliberately left null here (never set to f.Id): WorkPauseClassifier
        // treats a non-null FollowupId as a dependency-waiting marker, which would exclude this
        // pause from GetPausedDuration's TAT calculation entirely — exactly the same reason
        // DelegationService.PauseAsync always sets FollowupId = null on its own pause rows.
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
        _context.WorkPauses.Add(pause);
        AddSameStatusHistory(anchor, reason, "SIMPLE_PAUSE", actor, now);

        f.ModifiedBy = actor;
        f.ModifiedDate = now;
        _repo.Update(f);
        await _context.SaveChangesAsync(ct);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_PAUSE", module: "Followup", entityName: nameof(Followup),
            entityId: f.Id.ToString(),
            oldValues: new { IsPaused = false },
            newValues: new { IsPaused = true, PauseId = pause.Id, pause.StartAt, pause.Reason },
            description: "Followup paused");
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await EnrichAsync(f, ct);
    }

    public async Task<FollowupResponseDto> ResumeAsync(long id, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var f = await RequireFollowupWithTaskAsync(id, ct);
        var eaTask = await _context.Tasks.FirstAsync(t => t.Id == f.EaTaskId!.Value, ct);
        RequireInProgress(eaTask, "resumed");

        var open = eaTask.WorkflowInstanceId.HasValue
            ? await _context.WorkPauses
                .Where(p => p.WorkflowInstanceId == eaTask.WorkflowInstanceId && !p.IsDeleted && p.EndAt == null)
                .OrderByDescending(p => p.StartAt).ThenByDescending(p => p.Id)
                .ToListAsync(ct)
            : new List<WorkPause>();
        if (open.Count == 0) throw new BusinessRuleException("No open pause found. The Follow-up is not paused.");
        if (open.Count > 1) throw new BusinessRuleException("Multiple open operational stops exist; resolve manually.");

        var pause = open[0];
        var now = Clock.UtcNowTz;
        var actor = Actor();
        pause.EndAt = now;
        pause.ResumedById = _currentUser.ActorId();
        pause.ResumedByName = _currentUser.ActorName();
        pause.ModifiedBy = actor;
        pause.ModifiedDate = now;

        var anchor = await _context.WorkflowInstances.FirstAsync(w => w.Id == eaTask.WorkflowInstanceId!.Value, ct);
        AddSameStatusHistory(anchor, "Work resumed", "SIMPLE_RESUME", actor, now);

        f.ModifiedBy = actor;
        f.ModifiedDate = now;
        _repo.Update(f);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_RESUME", module: "Followup", entityName: nameof(Followup),
            entityId: f.Id.ToString(),
            oldValues: new { IsPaused = true, PauseId = pause.Id },
            newValues: new { IsPaused = false, PauseId = pause.Id, pause.EndAt, pause.ResumedById, pause.ResumedByName },
            description: "Followup resumed");
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await EnrichAsync(f, ct);
    }

    public async Task<FollowupResponseDto> CompleteAsync(long id, CompleteFollowupRequestDto dto, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var f = await RequireFollowupWithTaskAsync(id, ct);
        if (f.CompletedAt.HasValue) throw new BusinessRuleException("Follow-up already completed.");

        EaTask? eaTask = f.EaTaskId.HasValue ? await _context.Tasks.FirstOrDefaultAsync(t => t.Id == f.EaTaskId.Value, ct) : null;
        if (eaTask is not null)
        {
            if (eaTask.ExecutionStatus != EaTaskExecutionStatus.InProgress)
                throw new BusinessRuleException("Start the follow-up before completing it.");
            if (eaTask.WorkflowInstanceId.HasValue && await _context.WorkPauses.AnyAsync(
                    p => p.WorkflowInstanceId == eaTask.WorkflowInstanceId && !p.IsDeleted && p.EndAt == null, ct))
                throw new BusinessRuleException("Resume the follow-up before completing it.");
        }

        var oldSnap2 = new { f.Id, f.CompletedAt };
        var now = Clock.UtcNowTz;
        var actor = Actor();

        // apply completion data: client may suggest CompletionNote/OutcomeCode but actor/time are server-controlled
        f.CompletionNote = dto.CompletionNote?.Trim();
        f.OutcomeCode = dto.OutcomeCode?.Trim();
        f.CompletedAt = now;
        f.CompletedById = _currentUser.ActorId();
        f.CompletedByName = _currentUser.ActorName();
        f.ModifiedBy = actor;
        f.ModifiedDate = now;

        _repo.Update(f);

        if (eaTask is not null)
        {
            var phase = await _context.FollowupPhaseTats.FirstOrDefaultAsync(p => p.FollowupId == id && p.EndedAt == null, ct);
            if (phase is not null) await CloseFollowupPhaseAsync(phase, eaTask.WorkflowInstanceId, actor, now, ct);

            eaTask.ExecutionStatus = EaTaskExecutionStatus.Completed;
            eaTask.CompletedAt = now;
            if (eaTask.AllottedTatMinutes.HasValue && eaTask.StartedAt.HasValue)
            {
                var pausesForTat = eaTask.WorkflowInstanceId.HasValue
                    ? await _context.WorkPauses.AsNoTracking()
                        .Where(p => p.WorkflowInstanceId == eaTask.WorkflowInstanceId && !p.IsDeleted).ToListAsync(ct)
                    : new List<WorkPause>();
                eaTask.TatUsedMinutes = EaTaskService.CalculateActiveTatMinutes(eaTask.StartedAt.Value, now, pausesForTat);
            }
            if (eaTask.WorkflowInstanceId.HasValue)
                await CompleteFollowupAnchorAsync(eaTask.WorkflowInstanceId.Value, actor, now, ct);
        }

        _auditService.AddAudit(
            actionType: "FOLLOWUP_COMPLETE",
            module: "Followup",
            entityName: nameof(Followup),
            entityId: f.Id.ToString(),
            oldValues: oldSnap2,
            newValues: new
            {
                f.Id,
                f.CompletedAt,
                f.CompletedById,
                f.CompletedByName,
                f.CompletionNote,
                f.OutcomeCode,
                f.ModifiedBy,
                f.ModifiedDate,
                eaTask?.TatUsedMinutes
            },
            description: "Followup completed");

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await EnrichAsync(f, ct);
    }

    private async Task<Followup> RequireFollowupWithTaskAsync(long id, CancellationToken ct)
    {
        var f = await LockFollowupAsync(id, ct);
        if (!f.EaTaskId.HasValue)
            throw new BusinessRuleException("This follow-up has no execution task; it cannot be started, paused, resumed, or completed this way.");
        return f;
    }

    private async Task<Followup> LockFollowupAsync(long id, CancellationToken ct)
    {
        if (_context.Database.IsNpgsql())
        {
            var rows = await _context.Followups.FromSqlInterpolated(
                $"SELECT * FROM public.ea_followups WHERE \"Id\" = {id} AND NOT \"IsDeleted\" FOR UPDATE").ToListAsync(ct);
            return rows.SingleOrDefault() ?? throw new NotFoundException($"Followup {id} not found.");
        }
        return await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Followup {id} not found.");
    }

    private static void RequireInProgress(EaTask eaTask, string action)
    {
        if (eaTask.ExecutionStatus != EaTaskExecutionStatus.InProgress)
            throw new BusinessRuleException($"Follow-up cannot be {action} from its current status '{eaTask.ExecutionStatus}'. It must be InProgress.");
    }

    /// <summary>
    /// A Followup has no Meeting-style workflow, but the shared pause model (WorkPause, EaTask
    /// history) is keyed on EaTask.WorkflowInstanceId — exactly the same reason
    /// DelegationService.EnsureAnchorAsync exists. The first Pause lazily creates one minimal
    /// WorkflowInstance anchor (BusinessModule = "Follow-up", BusinessRecordId = Followup.Id) and
    /// links it through EaTask.WorkflowInstanceId. No pause table/entity/status is added: Paused
    /// stays derived (InProgress + open WorkPause), identical to Delegation.
    /// </summary>
    private async Task<WorkflowInstance> EnsureFollowupAnchorAsync(Followup entity, EaTask eaTask, DateTime now, CancellationToken ct)
    {
        if (eaTask.WorkflowInstanceId.HasValue)
            return await _context.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == eaTask.WorkflowInstanceId.Value && !w.IsDeleted, ct)
                ?? throw new BusinessRuleException("The Follow-up's pause workflow is missing or deleted.");

        var module = await _context.BusinessModules.AsNoTracking()
            .Where(m => !m.IsDeleted && m.IsActive && m.Name == FollowupBusinessModuleName)
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException($"An active '{FollowupBusinessModuleName}' record is required in ea_business_modules.");
        var inProgress = await SingleStatusAsync("In Progress", ct);

        var startedAt = eaTask.StartedAt ?? now;
        var anchor = new WorkflowInstance
        {
            BusinessModuleId = module.Id,
            BusinessRecordId = entity.Id.ToString(CultureInfo.InvariantCulture),
            StatusId = inProgress.Id,
            StartedAt = startedAt,
            TatStartedAt = startedAt,
            UpdatedAt = now,
            DoerId = entity.DoerId,
            DoerName = entity.DoerName,
            IsActive = true,
            CreatedBy = Actor(),
            CreatedDate = now
        };
        _context.WorkflowInstances.Add(anchor);
        await _context.SaveChangesAsync(ct);
        eaTask.WorkflowInstanceId = anchor.Id;
        return anchor;
    }

    private async Task CompleteFollowupAnchorAsync(long workflowId, string actor, DateTime now, CancellationToken ct)
    {
        var anchor = await _context.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted, ct);
        if (anchor is null) return;
        var completed = await SingleStatusAsync("Completed", ct);
        var fromStatusId = anchor.StatusId;
        anchor.StatusId = completed.Id;
        anchor.UpdatedAt = now;
        anchor.CompletedAt ??= now;
        anchor.IsActive = false;
        anchor.ModifiedBy = actor;
        anchor.ModifiedDate = now;
        _context.WorkflowHistory.Add(new WorkflowHistory
        {
            WorkflowInstanceId = anchor.Id,
            FromStatusId = fromStatusId,
            ToStatusId = completed.Id,
            Notes = "Follow-up completed",
            ChangedAt = now,
            CreatedBy = actor,
            CreatedDate = now
        });
    }

    private async Task<Status> SingleStatusAsync(string name, CancellationToken ct)
    {
        var lower = name.ToLower();
        var matches = await _context.Statuses.Where(s => s.IsActive && !s.IsDeleted && s.Name.ToLower() == lower).Take(2).ToListAsync(ct);
        if (matches.Count != 1) throw new BusinessRuleException($"Exactly one active '{name}' status is required.");
        return matches[0];
    }

    private void AddSameStatusHistory(WorkflowInstance anchor, string? notes, string transitionType, string actor, DateTime now) =>
        _context.WorkflowHistory.Add(new WorkflowHistory
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

    /// <summary>Closes the Followup's one-and-only Actual phase, freezing Used/Paused/PauseCount/seconds — same TatSummaryCalculator formula Delegation's own ClosePhaseAsync uses.</summary>
    private async Task CloseFollowupPhaseAsync(FollowupPhaseTat phase, long? workflowInstanceId, string actor, DateTime now, CancellationToken ct)
    {
        var pauses = workflowInstanceId.HasValue
            ? await _context.WorkPauses.AsNoTracking().Where(p => p.WorkflowInstanceId == workflowInstanceId && !p.IsDeleted).ToListAsync(ct)
            : new List<WorkPause>();
        var relevant = pauses.Where(p => WorkPauseClassifier.IsSimplePause(p) && p.StartAt < now && (p.EndAt ?? DateTime.MaxValue) > phase.StartedAt).ToList();
        var summary = TatSummaryCalculator.Calculate(phase.AllottedTatMinutes ?? 0, phase.StartedAt, now, relevant, now);
        phase.EndedAt = now;
        phase.EndedById = _currentUser.ActorId();
        phase.EndedByName = _currentUser.ActorName();
        phase.TatUsedMinutes = phase.AllottedTatMinutes.HasValue ? (int)summary.Tat!.Value.TotalMinutes : null;
        phase.TatPausedMinutes = (int)summary.PauseTime.TotalMinutes;
        phase.TatUsedSeconds = phase.AllottedTatMinutes.HasValue ? DurationSeconds(summary.Tat!.Value) : null;
        phase.TatPausedSeconds = DurationSeconds(summary.PauseTime);
        phase.PauseCount = summary.PauseCount;
        phase.ModifiedBy = actor;
        phase.ModifiedDate = now;
    }

    // Decimal tick conversion avoids floating-point loss and retains TimeSpan's 100 ns precision.
    private static decimal DurationSeconds(TimeSpan duration) => duration.Ticks / (decimal)TimeSpan.TicksPerSecond;

    /// <summary>Meeting/Delegation's own exact "unavailable TAT" fallback shape, reused
    /// verbatim for a Followup with no configured TAT rule.</summary>
    private static MeetingTatSummaryDto NoTatSummary() => new()
    {
        Tat = null, TotalTat = TimeSpan.Zero, TatDifference = TimeSpan.Zero, PauseTime = TimeSpan.Zero, PauseCount = 0
    };

    /// <summary>Same shape/formula as DelegationService.ToPhaseTatDto, plus explicit StartedBy/EndedBy.</summary>
    private static FollowupPhaseTatDto ToFollowupPhaseTatDto(FollowupPhaseTat phase, IReadOnlyCollection<WorkPause> pauses, DateTime now)
    {
        if (phase.EndedAt.HasValue)
        {
            return new FollowupPhaseTatDto
            {
                TaskType = phase.TaskType, ReviewCycleNumber = phase.ReviewCycleNumber,
                StartedAt = phase.StartedAt, EndedAt = phase.EndedAt,
                StartedById = phase.StartedById, StartedByName = phase.StartedByName,
                EndedById = phase.EndedById, EndedByName = phase.EndedByName,
                AllottedTatMinutes = phase.AllottedTatMinutes, TatUsedMinutes = phase.TatUsedMinutes,
                TatPausedMinutes = phase.TatPausedMinutes, PauseCount = phase.PauseCount,
                TatUsedSeconds = phase.TatUsedSeconds, TatPausedSeconds = phase.TatPausedSeconds,
                TatDifferenceSeconds = phase.AllottedTatMinutes.HasValue && phase.TatUsedSeconds.HasValue
                    ? phase.AllottedTatMinutes.Value * 60m - phase.TatUsedSeconds.Value : null,
                TatDifferenceMinutes = phase.AllottedTatMinutes.HasValue && phase.TatUsedMinutes.HasValue
                    ? phase.AllottedTatMinutes - phase.TatUsedMinutes : null,
            };
        }
        // The current, still-open phase — live-ticking, same formula, window end is "now".
        var relevant = pauses.Where(p => WorkPauseClassifier.IsSimplePause(p) && p.StartAt < now && (p.EndAt ?? DateTime.MaxValue) > phase.StartedAt).ToList();
        var summary = TatSummaryCalculator.Calculate(phase.AllottedTatMinutes ?? 0, phase.StartedAt, null, relevant, now);
        int? used = phase.AllottedTatMinutes.HasValue ? (int)summary.Tat!.Value.TotalMinutes : null;
        decimal? usedSeconds = phase.AllottedTatMinutes.HasValue ? DurationSeconds(summary.Tat!.Value) : null;
        return new FollowupPhaseTatDto
        {
            TaskType = phase.TaskType, ReviewCycleNumber = phase.ReviewCycleNumber,
            StartedAt = phase.StartedAt, EndedAt = null,
            StartedById = phase.StartedById, StartedByName = phase.StartedByName,
            EndedById = null, EndedByName = null,
            AllottedTatMinutes = phase.AllottedTatMinutes, TatUsedMinutes = used,
            TatPausedMinutes = (int)summary.PauseTime.TotalMinutes, PauseCount = summary.PauseCount,
            TatUsedSeconds = usedSeconds, TatPausedSeconds = DurationSeconds(summary.PauseTime),
            TatDifferenceSeconds = phase.AllottedTatMinutes.HasValue && usedSeconds.HasValue
                ? phase.AllottedTatMinutes.Value * 60m - usedSeconds.Value : null,
            TatDifferenceMinutes = phase.AllottedTatMinutes.HasValue && used.HasValue ? phase.AllottedTatMinutes - used : null,
        };
    }

    // ============================================================
    // REMINDER LOG (§5) — an explicit record that a reminder handoff happened. The backend
    // sends nothing; the frontend calls this when the user actually opens the handoff.
    // ============================================================

    public async Task<FollowupReminderLogResponseDto> LogReminderAsync(long id, LogFollowupReminderRequestDto dto, CancellationToken ct = default)
    {
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        var channel = dto.Channel?.Trim();
        if (channel != "Email" && channel != "WhatsApp")
            throw new BadRequestException("Channel must be Email or WhatsApp.");
        var recipient = NullIfBlank(dto.Recipient);
        if (recipient is null) throw new BadRequestException("Recipient must not be empty.");
        if (string.IsNullOrWhiteSpace(dto.Message)) throw new BadRequestException("Message must not be empty.");

        var actor = EaActorSnapshot.From(_currentUser.ActorId(), _currentUser.ActorName());
        var now = Clock.UtcNowTz;
        var log = new FollowupReminderLog
        {
            FollowupId = f.Id,
            Channel = channel,
            Recipient = recipient,
            RecipientName = NullIfBlank(dto.RecipientName),
            Message = dto.Message.Trim(),
            SentAt = now,
            SentById = _currentUser.ActorId(),
            SentByName = actor.EmployeeName ?? _currentUser.ActorName(),
            CreatedDate = now
        };
        _context.FollowupReminderLogs.Add(log);
        await _context.SaveChangesAsync(ct);

        return new FollowupReminderLogResponseDto
        {
            Id = log.Id, FollowupId = log.FollowupId, Channel = log.Channel, Recipient = log.Recipient,
            RecipientName = log.RecipientName, Message = log.Message, SentAt = log.SentAt,
            SentById = log.SentById, SentByName = log.SentByName
        };
    }

    public async Task<List<FollowupReminderLogResponseDto>> GetReminderLogAsync(long id, CancellationToken ct = default)
    {
        if (!await _context.Followups.AnyAsync(f => f.Id == id && !f.IsDeleted, ct))
            throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        return await _context.FollowupReminderLogs.AsNoTracking()
            .Where(l => l.FollowupId == id)
            .OrderByDescending(l => l.SentAt).ThenByDescending(l => l.Id)
            .Select(l => new FollowupReminderLogResponseDto
            {
                Id = l.Id, FollowupId = l.FollowupId, Channel = l.Channel, Recipient = l.Recipient,
                RecipientName = l.RecipientName, Message = l.Message, SentAt = l.SentAt,
                SentById = l.SentById, SentByName = l.SentByName
            }).ToListAsync(ct);
    }

    /// <summary>
    /// The one business action for "EA performed another follow-up": updates the Followup's current
    /// snapshot AND appends exactly one FollowupCycle history row (who, when, remark, next dates,
    /// outcome), atomically. Callers must not also call POST /cycles for the same event.
    /// </summary>
    public async Task RecordFollowupAsync(long id, RecordFollowupRequestDto dto, CancellationToken ct = default)
    {
        if (dto.Note?.Length > 2000) throw new BadRequestException("Note must not exceed 2000 characters.");
        if (dto.OutcomeCode?.Trim().Length > 100) throw new BadRequestException("OutcomeCode must not exceed 100 characters.");
        var actor = EaActorSnapshot.From(_currentUser.ActorId(), _currentUser.ActorName());

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        // Serialize concurrent follow-ups on the same Followup so cycle sequence numbers stay unique.
        if (_context.Database.IsRelational())
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.ea_followups WHERE \"Id\" = {id} FOR UPDATE", ct);
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        if (f.IsDeleted) throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        if (f.CompletedAt.HasValue) throw new BadRequestException("Cannot record an attempt on a completed follow-up.");

        var now = Clock.UtcNowTz;
        var by = actor.DisplayName ?? _currentUser.ActorDisplay();
        var next = dto.NextFollowupAt.HasValue ? ToUtc(dto.NextFollowupAt.Value) : (DateTime?)null;
        var expected = dto.ExpectedResponseAt.HasValue ? ToUtc(dto.ExpectedResponseAt.Value) : (DateTime?)null;
        var outcome = string.IsNullOrWhiteSpace(dto.OutcomeCode) ? null : dto.OutcomeCode.Trim();

        // current snapshot on the main Followup
        f.Note = dto.Note?.Trim() ?? f.Note;
        f.NextFollowupAt = next ?? f.NextFollowupAt;
        f.ExpectedResponseAt = expected ?? f.ExpectedResponseAt;
        f.LastFollowupAt = now;
        f.ModifiedBy = by;
        f.ModifiedByEmployeeId = actor.EmployeeId;
        f.ModifiedByEmployeeName = actor.EmployeeName;
        f.ModifiedDate = now;
        _repo.Update(f);

        // history: one row per recorded follow-up
        var sequence = (await _context.FollowupCycles.Where(c => c.FollowupId == f.Id)
            .MaxAsync(c => (int?)c.SequenceNumber, ct) ?? 0) + 1;
        var cycle = new FollowupCycle
        {
            FollowupId = f.Id, SequenceNumber = sequence, FollowedUpAt = now,
            FollowedUpByEmployeeId = actor.EmployeeId, FollowedUpByEmployeeName = actor.EmployeeName,
            Note = dto.Note?.Trim(), NextFollowupAt = next, ExpectedResponseAt = expected, OutcomeCode = outcome,
            CreatedBy = by, CreatedDate = now
        };
        _context.FollowupCycles.Add(cycle);
        await _context.SaveChangesAsync(ct);

        _auditService.AddAudit(
            actionType: "FOLLOWUP_RECORDED",
            module: "Followup",
            entityName: nameof(Followup),
            entityId: f.Id.ToString(),
            oldValues: null,
            newValues: new
            {
                f.Id, f.LastFollowupAt, f.Note, f.NextFollowupAt, f.ExpectedResponseAt,
                CycleId = cycle.Id, cycle.SequenceNumber, cycle.OutcomeCode,
                Actor = actor.EmployeeId is null && actor.EmployeeName is null ? (object)by : actor
            },
            description: "Followup action recorded");

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task<PagedResult<FollowupResponseDto>> GetPagedAsync(FollowupListQueryDto query, CancellationToken ct = default)
    {
        NormalizeFilter(query, validatePaging: true);
        var page = await _repo.GetPagedAsync(query, Clock.UtcNowTz, ct);
        return new PagedResult<FollowupResponseDto> { Items = await EnrichListAsync(page.Items, ct),
            PageNumber = page.PageNumber, PageSize = page.PageSize, TotalCount = page.TotalCount };
    }

    public async Task<FollowupSummaryResponseDto> GetSummaryAsync(FollowupListQueryDto query, CancellationToken ct = default)
    {
        NormalizeFilter(query, validatePaging: false);
        return await _repo.GetSummaryAsync(query, Clock.UtcNowTz, IndiaBusinessCalendar.Today, ct);
    }

    private static void NormalizeFilter(FollowupListQueryDto query, bool validatePaging)
    {
        if (validatePaging)
        {
            if (query.Page < 1 || query.PageSize < 1) throw new BadRequestException("Page and PageSize must be positive.");
            query.PageSize = Math.Min(query.PageSize, 100);
            if (((long)query.Page - 1) * query.PageSize > int.MaxValue) throw new BadRequestException("Page offset is too large.");
        }
        if (query.BusinessModuleId <= 0 || query.PriorityLevelId <= 0) throw new BadRequestException("Filter IDs must be positive.");
        if (query.Status != null && !string.Equals(query.Status, "Pending", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(query.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Status must be Pending or Completed.");
        if (query.Status != null && query.IsCompleted.HasValue
            && query.IsCompleted.Value != string.Equals(query.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Status and IsCompleted conflict.");
        if (query.DueFrom.HasValue) query.DueFrom = ToUtc(query.DueFrom.Value);
        if (query.DueTo.HasValue) query.DueTo = ToUtc(query.DueTo.Value);
        if (query.DueFrom > query.DueTo) throw new BadRequestException("DueFrom must not exceed DueTo.");
    }
    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    public async Task<List<FollowupResponseDto>> GetBySourceAsync(long moduleId, string recordId, CancellationToken ct = default)
        => await EnrichListAsync(await _repo.GetBySourceAsync(moduleId, recordId, ct), ct);

    private sealed record TaskContext(long Id, long BusinessModuleId, string BusinessRecordId, string Task, string ExecutionStatus, long? WorkflowInstanceId);

    private async Task<FollowupResponseDto> EnrichAsync(Followup f, CancellationToken ct)
        => (await EnrichListAsync(new[] { f }, ct))[0];

    /// <summary>
    /// Enriches a page of follow-ups with a constant number of queries (modules, tasks, open pauses,
    /// priorities) - never one query per row. Task context is matched on BusinessModuleId +
    /// BusinessRecordId, the canonical source identity; nothing is persisted on Followup for it.
    /// </summary>
    private async Task<List<FollowupResponseDto>> EnrichListAsync(IEnumerable<Followup> items, CancellationToken ct)
    {
        var list = items.ToList();
        var now = Clock.UtcNowTz;
        var dtos = list.Select(f =>
        {
            var d = _mapper.Map<FollowupResponseDto>(f);
            d.IsCompleted = f.CompletedAt.HasValue;
            d.IsOverdue = !d.IsCompleted && f.DueAt != default && f.DueAt < now;
            d.Recipient = new ReminderRecipientResponseDto { EmployeeId = f.ReminderRecipientEmployeeId, Name = f.ReminderRecipientName, Phone = f.ReminderWhatsAppNumber, Email = f.ReminderRecipientEmail };
            return d;
        }).ToList();
        if (list.Count == 0) return dtos;

        var sourced = list.Where(f => f.BusinessModuleId.HasValue).ToList();
        var moduleIds = sourced.Select(f => f.BusinessModuleId!.Value).Distinct().ToList();
        var recordIds = sourced.Where(f => f.BusinessRecordId != null).Select(f => f.BusinessRecordId!).Distinct().ToList();

        var modules = moduleIds.Count == 0 ? new Dictionary<long, string>()
            : await _context.BusinessModules.AsNoTracking().Where(m => moduleIds.Contains(m.Id) && !m.IsDeleted)
                .ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        var taskRows = recordIds.Count == 0 ? new List<TaskContext>()
            : await _context.Tasks.AsNoTracking()
                .Where(t => !t.IsDeleted && moduleIds.Contains(t.BusinessModuleId) && recordIds.Contains(t.BusinessRecordId))
                .Select(t => new TaskContext(t.Id, t.BusinessModuleId, t.BusinessRecordId, t.Task, t.ExecutionStatus, t.WorkflowInstanceId))
                .ToListAsync(ct);
        // At most one live task exists per module + record; if data ever disagrees, the newest wins.
        var tasks = taskRows.GroupBy(t => (t.BusinessModuleId, t.BusinessRecordId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Id).First());

        var workflowIds = tasks.Values.Where(t => t.WorkflowInstanceId.HasValue).Select(t => t.WorkflowInstanceId!.Value).Distinct().ToList();
        var pausedWorkflows = workflowIds.Count == 0 ? new HashSet<long>()
            : (await _context.WorkPauses.AsNoTracking()
                .Where(p => p.WorkflowInstanceId.HasValue && workflowIds.Contains(p.WorkflowInstanceId.Value) && !p.IsDeleted && p.EndAt == null)
                .Select(p => p.WorkflowInstanceId!.Value).Distinct().ToListAsync(ct)).ToHashSet();

        var priorityIds = list.Where(f => f.PriorityLevelId.HasValue).Select(f => f.PriorityLevelId!.Value).Distinct().ToList();
        var priorities = priorityIds.Count == 0 ? new Dictionary<int, string>()
            : await _context.PriorityLevels.AsNoTracking().Where(p => priorityIds.Contains(p.Id) && !p.IsDeleted)
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        // ---- The Followup's OWN execution task/phase/pauses — batched for the whole page,
        // same "constant number of queries, never per-row" rule as everything else here. ----
        var ownFollowupIds = list.Select(f => f.Id).ToList();
        var ownEaTaskIds = list.Where(f => f.EaTaskId.HasValue).Select(f => f.EaTaskId!.Value).Distinct().ToList();
        var ownTasks = ownEaTaskIds.Count == 0 ? new Dictionary<long, EaTask>()
            : await _context.Tasks.AsNoTracking().Where(t => ownEaTaskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var ownWorkflowIds = ownTasks.Values.Where(t => t.WorkflowInstanceId.HasValue).Select(t => t.WorkflowInstanceId!.Value).Distinct().ToList();
        var ownPausesByWorkflow = ownWorkflowIds.Count == 0 ? new Dictionary<long, List<WorkPause>>()
            : (await _context.WorkPauses.AsNoTracking()
                .Where(p => p.WorkflowInstanceId.HasValue && ownWorkflowIds.Contains(p.WorkflowInstanceId.Value) && !p.IsDeleted)
                .ToListAsync(ct)).GroupBy(p => p.WorkflowInstanceId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        // Followup never has more than one phase ever (no Review/Rework), but this stays
        // defensive (newest wins) rather than assuming exactly one row exists.
        var ownPhaseByFollowup = ownFollowupIds.Count == 0 ? new Dictionary<long, FollowupPhaseTat>()
            : (await _context.FollowupPhaseTats.AsNoTracking().Where(p => ownFollowupIds.Contains(p.FollowupId)).ToListAsync(ct))
                .GroupBy(p => p.FollowupId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id).First());

        var resolvedSources = new Dictionary<(long, string), FollowupSourceResponseDto>();
        for (var i = 0; i < list.Count; i++)
        {
            var f = list[i]; var dto = dtos[i];
            dto.FollowupEaTaskId = f.EaTaskId;
            dto.EaTaskId = f.EaTaskId;
            dto.IsPaused = false;
            if (f.EaTaskId.HasValue && ownTasks.TryGetValue(f.EaTaskId.Value, out var ownTask))
            {
                dto.ExecutionStatus = ownTask.ExecutionStatus;
                dto.StartedAt = ownTask.StartedAt;
                dto.AllottedTatMinutes = ownTask.AllottedTatMinutes;
                var ownPauses = ownTask.WorkflowInstanceId.HasValue && ownPausesByWorkflow.TryGetValue(ownTask.WorkflowInstanceId.Value, out var wp)
                    ? wp : new List<WorkPause>();
                dto.IsFollowupPaused = ownTask.ExecutionStatus == EaTaskExecutionStatus.InProgress && ownPauses.Any(p => p.EndAt == null);

                dto.IsPaused = dto.IsFollowupPaused;
                dto.TatSummary = ownTask.AllottedTatMinutes.HasValue
                    ? TatSummaryCalculator.Calculate(ownTask.AllottedTatMinutes.Value, ownTask.StartedAt, ownTask.CompletedAt, ownPauses, now)
                    : NoTatSummary();
                if (ownTask.AllottedTatMinutes.HasValue && ownTask.StartedAt.HasValue)
                {
                    dto.TatUsedMinutes = (int)dto.TatSummary.Tat!.Value.TotalMinutes;
                    dto.TatPausedMinutes = (int)dto.TatSummary.PauseTime.TotalMinutes;
                }

                if (ownPhaseByFollowup.TryGetValue(f.Id, out var phase))
                {
                    dto.StartedById = phase.StartedById;
                    dto.StartedByName = phase.StartedByName;
                    dto.CurrentPhase = phase.EndedAt.HasValue ? null : phase.TaskType;
                    dto.CurrentPhaseStartedAt = phase.EndedAt.HasValue ? null : phase.StartedAt;
                    dto.PhaseTat.Add(ToFollowupPhaseTatDto(phase, ownPauses, now));
                }
            }
            else
            {
                dto.TatSummary = NoTatSummary();
            }

            if (f.PriorityLevelId.HasValue) dto.PriorityLevelName = priorities.GetValueOrDefault(f.PriorityLevelId.Value);
            if (!f.BusinessModuleId.HasValue) continue;

            if (modules.TryGetValue(f.BusinessModuleId.Value, out var moduleName))
            {
                dto.BusinessModuleName = moduleName;
                dto.ModuleName = moduleName;
                if (string.Equals(moduleName.Trim(), "Meeting", StringComparison.OrdinalIgnoreCase)) dto.BusinessModuleCode = "Meeting";
            }
            if (f.BusinessRecordId != null && tasks.TryGetValue((f.BusinessModuleId.Value, f.BusinessRecordId), out var task))
            {
                dto.SourceEaTaskId = task.Id;
                dto.Task = task.Task;
                dto.Stage = task.ExecutionStatus;
                dto.SourceIsPaused = task.WorkflowInstanceId.HasValue ? pausedWorkflows.Contains(task.WorkflowInstanceId.Value) : null;
                dto.BusinessRecordTitle = task.Task;
            }
            else
            {
                // No central task for this source: title comes from the source record itself,
                // resolved once per distinct (module, record) on the page, never once per row.
                var key = (f.BusinessModuleId.Value, f.BusinessRecordId ?? string.Empty);
                if (!resolvedSources.TryGetValue(key, out var source))
                    resolvedSources[key] = source = await _sourceResolver.ResolveAsync(f.BusinessModuleId, f.BusinessRecordId, ct);
                dto.BusinessModuleCode ??= source.BusinessModuleCode;
                dto.BusinessRecordTitle = source.BusinessRecordTitle;
            }
        }
        var followupIds = list.Select(f => f.Id).ToList();
        var escalations = followupIds.Count == 0 ? new List<Escalation>() : await _context.Escalations.AsNoTracking().Where(e => !e.IsDeleted && e.FollowupId.HasValue && followupIds.Contains(e.FollowupId.Value)).ToListAsync(ct);
        var levelIds = escalations.Select(e => e.EscalationLevelId).Distinct().ToList();
        var levelNames = levelIds.Count == 0 ? new Dictionary<int, string>() : await _context.EscalationLevels.AsNoTracking().Where(l => !l.IsDeleted && levelIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var dtoByFollowupId = list.Select((followup, index) => new { followup.Id, Dto = dtos[index] })
            .ToDictionary(x => x.Id, x => x.Dto);
        foreach (var group in escalations.GroupBy(e => e.FollowupId!.Value))
        {
            var current = group.Where(e => !e.ResolvedAt.HasValue).OrderByDescending(e => e.InitiatedAt).ThenByDescending(e => e.Id).FirstOrDefault()
                ?? group.OrderByDescending(e => e.InitiatedAt).ThenByDescending(e => e.Id).First();
            var dto = dtoByFollowupId[group.Key];
            dto.Escalation = new FollowupEscalationResponseDto { Id = current.Id, EscalationLevelId = current.EscalationLevelId, EscalationLevelName = levelNames.GetValueOrDefault(current.EscalationLevelId), AcknowledgedAt = current.AcknowledgedAt, ResolvedAt = current.ResolvedAt, IsAcknowledged = current.AcknowledgedAt.HasValue, IsResolved = current.ResolvedAt.HasValue };
        }
        foreach (var dto in dtos) { if (dto.ReminderSendWhatsApp && !string.IsNullOrWhiteSpace(dto.Recipient?.Phone)) dto.WhatsApp = new FollowupWhatsAppHandoffResponseDto { Message = BuildWhatsAppMessage(dto) }; }
        return dtos;
    }

    private static string BuildReminderEmailBody(FollowupResponseDto dto)
    {
        var greeting = string.IsNullOrWhiteSpace(dto.Recipient?.Name) ? "Hello," : $"Hello {dto.Recipient.Name},";
        var followupDate = dto.ReminderAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
        return $"{greeting}\n\nReminder / Follow-up\n\nModule: {dto.ModuleName ?? string.Empty}\nTask ID: {dto.SourceEaTaskId?.ToString() ?? string.Empty}\nTask: {dto.Task ?? string.Empty}\nFollow-up Date: {followupDate}\nRemark: {dto.Remark ?? string.Empty}";
    }
    private static string BuildWhatsAppMessage(FollowupResponseDto dto)
    {
        var greeting = string.IsNullOrWhiteSpace(dto.Recipient?.Name) ? "Hello," : $"Hello {dto.Recipient.Name},";
        var followupDate = dto.ReminderAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
        return $"{greeting}\n\nReminder / Follow-up\n\nModule: {dto.ModuleName ?? string.Empty}\nTask ID: {dto.SourceEaTaskId?.ToString() ?? string.Empty}\nTask: {dto.Task ?? string.Empty}\nFollow-up Date: {followupDate}\nRemark: {dto.Remark ?? string.Empty}";
    }
}
