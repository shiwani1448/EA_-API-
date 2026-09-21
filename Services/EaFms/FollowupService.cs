using AutoMapper;
using System.Globalization;
using System.Net.Mail;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class FollowupService : IFollowupService
{
    private readonly IFollowupRepository _repo;
    private readonly EaFmsDbContext _context;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IFollowupSourceResolver _sourceResolver;
    private readonly IEaReminderEmailSender? _emailSender;

    public FollowupService(IFollowupRepository repo, EaFmsDbContext context, IMapper mapper, ICurrentUserService currentUser, IAuditService auditService, IFollowupSourceResolver sourceResolver, IEaReminderEmailSender? emailSender = null)
    {
        _repo = repo;
        _context = context;
        _mapper = mapper;
        _currentUser = currentUser;
        _auditService = auditService;
        _sourceResolver = sourceResolver;
        _emailSender = emailSender;
    }

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
        var actor = EaActorSnapshot.From(dto.EmployeeId, dto.EmployeeName);
        var by = actor.DisplayName ?? _currentUser.UserName ?? _currentUser.UserId.ToString();

        var followup = new Followup
        {
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

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await _repo.AddAsync(followup, ct);
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

    public async Task SendEmailAsync(long id, CancellationToken ct = default)
    {
        var followup = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Followup {id} not found.");
        if (!followup.ReminderSendEmail) throw new BadRequestException("ReminderSendEmail must be true to send a reminder email.");
        if (!IsUsableEmail(followup.ReminderRecipientEmail))
            throw new BadRequestException("ReminderRecipientEmail is required and must be a valid email address to send a reminder email.");
        var sender = _emailSender ?? throw new BusinessRuleException("EA email sender is unavailable.");
        var context = await EnrichAsync(followup, ct);
        await sender.SendAsync(new EaReminderEmailMessage(
            followup.ReminderRecipientEmail.Trim(),
            $"Reminder / Follow-up - {context.Task ?? string.Empty}",
            BuildReminderEmailBody(context)), ct);
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
        f.Subject = dto.Subject?.Trim();
        f.Type = dto.Type?.Trim();
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
        var actor = EaActorSnapshot.From(dto.EmployeeId, dto.EmployeeName);
        f.ModifiedBy = actor.DisplayName ?? _currentUser.UserName ?? _currentUser.UserId.ToString();
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

    public async Task<FollowupResponseDto> CompleteAsync(long id, CompleteFollowupRequestDto dto, CancellationToken ct = default)
    {
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        if (f.CompletedAt.HasValue) throw new BadRequestException("Follow-up already completed.");
        var oldSnap2 = new { f.Id, f.CompletedAt };
        // apply completion data: client may suggest CompletionNote/OutcomeCode but actor/time are server-controlled
        f.CompletionNote = dto.CompletionNote?.Trim();
        f.OutcomeCode = dto.OutcomeCode?.Trim();
        f.CompletedAt = Clock.UtcNowTz;
        f.CompletedById = _currentUser.UserId.ToString();
        f.CompletedByName = _currentUser.UserName;
        f.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString();
        f.ModifiedDate = Clock.UtcNowTz;

        _repo.Update(f);

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
                f.ModifiedDate
            },
            description: "Followup completed");

        await _context.SaveChangesAsync(ct);
        return await EnrichAsync(f, ct);
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
        var actor = EaActorSnapshot.From(dto.EmployeeId, dto.EmployeeName);

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        // Serialize concurrent follow-ups on the same Followup so cycle sequence numbers stay unique.
        if (_context.Database.IsRelational())
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.ea_followups WHERE \"Id\" = {id} FOR UPDATE", ct);
        var f = await _repo.GetByIdAsync(id, ct) ?? throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        if (f.IsDeleted) throw new Jarvis5.Common.NotFoundException($"Followup {id} not found.");
        if (f.CompletedAt.HasValue) throw new BadRequestException("Cannot record an attempt on a completed follow-up.");

        var now = Clock.UtcNowTz;
        var by = actor.DisplayName ?? _currentUser.UserName ?? _currentUser.UserId.ToString();
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

        for (var i = 0; i < list.Count; i++)
        {
            var f = list[i]; var dto = dtos[i];
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
                dto.EaTaskId = task.Id;
                dto.Task = task.Task;
                dto.Stage = task.ExecutionStatus;
                dto.IsPaused = task.WorkflowInstanceId.HasValue ? pausedWorkflows.Contains(task.WorkflowInstanceId.Value) : null;
                dto.BusinessRecordTitle = task.Task;
            }
            else
            {
                // No central task for this source: fall back to the source resolver for the title only.
                var source = await _sourceResolver.ResolveAsync(f.BusinessModuleId, f.BusinessRecordId, ct);
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
        return $"{greeting}\n\nReminder / Follow-up\n\nModule: {dto.ModuleName ?? string.Empty}\nTask ID: {dto.EaTaskId?.ToString() ?? string.Empty}\nTask: {dto.Task ?? string.Empty}\nFollow-up Date: {followupDate}\nRemark: {dto.Remark ?? string.Empty}";
    }
    private static string BuildWhatsAppMessage(FollowupResponseDto dto)
    {
        var greeting = string.IsNullOrWhiteSpace(dto.Recipient?.Name) ? "Hello," : $"Hello {dto.Recipient.Name},";
        var followupDate = dto.ReminderAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
        return $"{greeting}\n\nReminder / Follow-up\n\nModule: {dto.ModuleName ?? string.Empty}\nTask ID: {dto.EaTaskId?.ToString() ?? string.Empty}\nTask: {dto.Task ?? string.Empty}\nFollow-up Date: {followupDate}\nRemark: {dto.Remark ?? string.Empty}";
    }
}
