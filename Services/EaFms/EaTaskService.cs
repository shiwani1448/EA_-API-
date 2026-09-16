using System.Globalization;
using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class EaTaskService(EaFmsDbContext db, IEaTaskRepository repository, ITatRuleRepository rules,
    IValidator<CreateEaTaskDto> validator, ICurrentUserService user, IAuditService audit) : IEaTaskService
{
    public async Task<List<EaTaskResponseDto>> QueryAsync(long? moduleId, string? recordId, CancellationToken ct)
    {
        var query = repository.Query();
        if (moduleId.HasValue) query = query.Where(x => x.BusinessModuleId == moduleId.Value);
        if (recordId is not null) query = query.Where(x => x.BusinessRecordId == recordId.Trim());
        return (await query.OrderByDescending(x => x.Id).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<EaTaskResponseDto> GetAsync(long id, CancellationToken ct) =>
        ToDto(await repository.Query().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"EA task {id} not found."));

    public Task<EaTaskResponseDto> CreateAsync(CreateEaTaskDto dto, CancellationToken ct) =>
        CreateCoreAsync(dto, requireTat: true, ct);

    // Backend-only Approval path; no public request can select this behavior.
    public Task<EaTaskResponseDto> CreateWithoutTatAsync(CreateEaTaskDto dto, CancellationToken ct) =>
        CreateCoreAsync(dto, requireTat: false, ct);

    private async Task<EaTaskResponseDto> CreateCoreAsync(CreateEaTaskDto dto, bool requireTat, CancellationToken ct)
    {
        dto.BusinessRecordId = dto.BusinessRecordId?.Trim()!;
        dto.Task = dto.Task?.Trim()!;
        dto.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid) throw new BusinessRuleException(string.Join("; ", validation.Errors.Select(x => x.ErrorMessage)));

        // Join an ambient transaction when Meeting (or another caller) already owns one.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        // Share locks keep the selected module, rule and optional workflow stable until insertion commits.
        var modules = await db.BusinessModules.FromSqlInterpolated(
            $"SELECT * FROM public.ea_business_modules WHERE \"Id\" = {dto.ModuleId} FOR SHARE").ToListAsync(ct);
        var module = modules.SingleOrDefault();
        if (module is null || !module.IsActive || module.IsDeleted)
            throw new BusinessRuleException("Module must exist and be active/non-deleted.");

        // Serialize creates for the same module/record, including the generic task endpoint.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({dto.ModuleId.ToString(CultureInfo.InvariantCulture) + ":" + dto.BusinessRecordId}, 0))", ct);
        if (await db.Tasks.AnyAsync(x => x.BusinessModuleId == dto.ModuleId && x.BusinessRecordId == dto.BusinessRecordId && !x.IsDeleted, ct))
            throw new BusinessRuleException("An EA task already exists for this business record.");
        var type = dto.Type;
        var subtype = dto.Subtype;
        var isApproval = string.Equals(module.Name.Trim(), "EA Approval", StringComparison.OrdinalIgnoreCase);
        if (!requireTat && !isApproval)
            throw new BusinessRuleException("Task creation without TAT is only supported for EA Approval.");
        if (string.Equals(module.Name.Trim(), "Meeting", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(dto.BusinessRecordId, NumberStyles.None, CultureInfo.InvariantCulture, out var meetingId))
                throw new BusinessRuleException("Meeting business record ID is invalid.");
            var meeting = await db.Meetings.SingleOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct)
                ?? throw new NotFoundException("Meeting business record not found.");
            if (meeting.Id.ToString(CultureInfo.InvariantCulture) != dto.BusinessRecordId)
                throw new BusinessRuleException("Meeting business record ID must be canonical.");
            type = meeting.MeetingType;
            subtype = meeting.Category;
            dto.Task = meeting.Title!;
            dto.Description = meeting.Description;
            dto.WorkflowInstanceId = meeting.WorkflowInstanceId;
        }
        int? allottedTatMinutes = null;
        if (requireTat)
        {
            if (!isApproval && (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(subtype)))
                throw new BusinessRuleException("Type and subtype are required to resolve an exact TAT rule; no legacy fallback is allowed.");
            var applicable = isApproval
                ? await rules.GetApplicableForApprovalAsync(dto.ModuleId, type, subtype, ct)
                : await rules.GetApplicableAsync(dto.ModuleId, type!.Trim(), subtype!.Trim(), ct);
            if (applicable.Count == 0) throw new BusinessRuleException("No active TAT rule is configured for this module/type/subtype combination.");
            if (applicable.Count != 1) throw new BusinessRuleException("Multiple active TAT rules are configured for this module/type/subtype combination.");
            if (applicable[0].TatMinutes <= 0) throw new BusinessRuleException("The module TAT must be greater than zero.");
            allottedTatMinutes = applicable[0].TatMinutes;
        }

        if (dto.WorkflowInstanceId.HasValue)
        {
            var workflows = await db.WorkflowInstances.FromSqlInterpolated(
                $"SELECT * FROM public.ea_workflow_instances WHERE \"Id\" = {dto.WorkflowInstanceId.Value} FOR SHARE").ToListAsync(ct);
            var workflow = workflows.SingleOrDefault();
            if (workflow is null || workflow.IsDeleted)
                throw new NotFoundException("The supplied workflow does not exist or is deleted.");
            if (workflow.BusinessModuleId != dto.ModuleId
                || !string.Equals(workflow.BusinessRecordId, dto.BusinessRecordId, StringComparison.Ordinal))
                throw new BusinessRuleException("The supplied workflow does not match the module and business record.");
        }

        var task = new EaTask
        {
            BusinessModuleId = dto.ModuleId, BusinessRecordId = dto.BusinessRecordId,
            Task = dto.Task, Description = dto.Description,
            AllottedTatMinutes = allottedTatMinutes, WorkflowInstanceId = dto.WorkflowInstanceId,
            IsActive = true, CreatedBy = user.UserId.ToString(CultureInfo.InvariantCulture), CreatedDate = Clock.UtcNowTz
        };
        await repository.AddAsync(task, ct);
        await db.SaveChangesAsync(ct);
        audit.AddAudit("EA_TASK_CREATE", "Task", nameof(EaTask), task.Id.ToString(CultureInfo.InvariantCulture), null,
            new { task.BusinessModuleId, task.BusinessRecordId, task.Task, task.AllottedTatMinutes, task.WorkflowInstanceId });
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);
        task.BusinessModule = module;
        return ToDto(task);
    }

    private static EaTaskResponseDto ToDto(EaTask task) => new()
    {
        EaTaskId = task.Id, ModuleId = task.BusinessModuleId, ModuleName = task.BusinessModule.Name,
        BusinessRecordId = task.BusinessRecordId, Task = task.Task, Description = task.Description,
        AllottedTatMinutes = task.AllottedTatMinutes, IsActive = task.IsActive,
        CreatedBy = task.CreatedBy, CreatedDate = task.CreatedDate,
        ModifiedBy = task.ModifiedBy, ModifiedDate = task.ModifiedDate
    };
}
