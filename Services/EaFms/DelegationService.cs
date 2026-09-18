using System.Globalization;
using System.Linq.Expressions;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
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

    private readonly EaFmsDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    private readonly IDelegationNumberRepository _numbers;
    private readonly IEaTaskService _eaTaskService;

    public DelegationService(
        EaFmsDbContext db,
        ICurrentUserService user,
        IAuditService audit,
        IDelegationNumberRepository numbers,
        IEaTaskService eaTaskService)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _numbers = numbers;
        _eaTaskService = eaTaskService;
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
        AssignedToId = dto.AssignedToId, AssignedToNameSnapshot = dto.AssignedToNameSnapshot,
        Priority = dto.Priority, DueDate = dto.DueDate,
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
        // classification (explicit DueDate instead), so this always goes through the
        // backend-only no-TAT path.
        var eaTaskDto = await _eaTaskService.CreateWithoutTatAsync(new CreateEaTaskDto
        {
            ModuleId = delegationModule.Id,
            BusinessRecordId = referenceNo,
            Task = string.IsNullOrWhiteSpace(command.Title) ? referenceNo : command.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            WorkflowInstanceId = null
        }, ct);

        var entity = new Delegation
        {
            ReferenceNo = referenceNo,
            EaTaskId = eaTaskDto.EaTaskId,

            Title = command.Title?.Trim() ?? string.Empty,
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),

            AssignedToId = command.AssignedToId?.Trim() ?? string.Empty,
            AssignedToNameSnapshot = string.IsNullOrWhiteSpace(command.AssignedToNameSnapshot) ? null : command.AssignedToNameSnapshot.Trim(),

            AssignedById = actor,
            AssignedByNameSnapshot = actorName,

            Priority = priority,
            DueDate = command.DueDate,
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
                entity.ReferenceNo, entity.EaTaskId, entity.Title, entity.AssignedToId, entity.Status,
                entity.Priority, entity.DueDate, entity.SourceBusinessModuleId, entity.SourceEntityId, entity.SourceReference
            },
            "Delegation created");
        await _db.SaveChangesAsync(ct);

        return ToDto(entity, sourceModule?.Name);
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

        return ToDto(entity, entity.SourceBusinessModule?.Name);
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

        var snapshot = new
        {
            entity.Title, entity.Description, entity.AssignedToId, entity.AssignedToNameSnapshot,
            entity.DueDate, entity.Priority, entity.SourceBusinessModuleId, entity.SourceEntityId,
            entity.SourceReference, entity.AdditionalNotes
        };

        // Editable business fields only. EaTask.Task/Description are set once at create
        // time and never re-synced on update — matching Travel/Approval's own update
        // paths, neither of which touches EaTask after creation.
        entity.Title = dto.Title?.Trim() ?? string.Empty;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.AssignedToId = dto.AssignedToId?.Trim() ?? string.Empty;
        entity.AssignedToNameSnapshot = string.IsNullOrWhiteSpace(dto.AssignedToNameSnapshot) ? null : dto.AssignedToNameSnapshot.Trim();
        entity.DueDate = dto.DueDate;
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
                entity.Title, entity.Description, entity.AssignedToId, entity.AssignedToNameSnapshot,
                entity.DueDate, entity.Priority, entity.SourceBusinessModuleId, entity.SourceEntityId,
                entity.SourceReference, entity.AdditionalNotes
            },
            "Delegation updated");

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ToDto(entity, sourceModule?.Name);
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

        _audit.AddAudit(
            "DELEGATION_START", "Delegation", nameof(Delegation),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            new { Status = DelegationStatus.Pending },
            new { entity.Status, entity.StartedAt },
            "Delegation started");

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var sourceModuleName = await ResolveSourceModuleNameAsync(entity.SourceBusinessModuleId, ct);
        return ToDto(entity, sourceModuleName);
    }

    public async Task<DelegationResponseDto> CompleteAsync(long delegationId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var entity = await LockDelegationAsync(delegationId, ct);
        if (entity.Status != DelegationStatus.InProgress)
            throw new BusinessRuleException($"Delegation cannot be completed from its current status '{entity.Status}'.");

        var now = Clock.UtcNowTz;
        entity.Status = DelegationStatus.Completed;
        entity.CompletedAt = now;
        // Same actor convention already established for AssignedBy at create time — never
        // accepted from the request payload.
        entity.CompletedById = Actor();
        entity.CompletedByNameSnapshot = _user.UserName;
        entity.ModifiedBy = Actor();
        entity.ModifiedDate = now;

        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == entity.EaTaskId, ct);
        eaTask.ExecutionStatus = EaTaskExecutionStatus.Completed;
        eaTask.CompletedAt = now;

        _audit.AddAudit(
            "DELEGATION_COMPLETE", "Delegation", nameof(Delegation),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            new { Status = DelegationStatus.InProgress },
            new { entity.Status, entity.CompletedAt, entity.CompletedById, entity.CompletedByNameSnapshot },
            "Delegation completed");

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var sourceModuleName = await ResolveSourceModuleNameAsync(entity.SourceBusinessModuleId, ct);
        return ToDto(entity, sourceModuleName);
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
                (d.AssignedToNameSnapshot != null && d.AssignedToNameSnapshot.ToLower().Contains(term)) ||
                (d.SourceReference != null && d.SourceReference.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedToId))
        {
            var id = query.AssignedToId.Trim();
            q = q.Where(d => d.AssignedToId == id);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            var p = query.Priority.Trim().ToLower();
            q = q.Where(d => d.Priority != null && d.Priority.ToLower() == p);
        }

        if (query.SourceBusinessModuleId.HasValue)
            q = q.Where(d => d.SourceBusinessModuleId == query.SourceBusinessModuleId.Value);

        if (query.DueDate.HasValue)
        {
            var day = query.DueDate.Value.Date;
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

        return new PagedResult<DelegationResponseDto>
        {
            Items = items.Select(d => ToDto(d, d.SourceBusinessModule?.Name)).ToArray(),
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

    private static DelegationResponseDto ToDto(Delegation d, string? sourceModuleName)
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

            AssignedToId = d.AssignedToId,
            AssignedToName = d.AssignedToNameSnapshot,

            AssignedById = d.AssignedById,
            AssignedByName = d.AssignedByNameSnapshot,

            Priority = d.Priority,
            DueDate = d.DueDate,

            Status = d.Status,

            SourceBusinessModuleId = d.SourceBusinessModuleId,
            SourceModuleName = sourceModuleName,
            SourceEntityId = d.SourceEntityId,
            SourceReference = d.SourceReference,

            AdditionalNotes = d.AdditionalNotes,

            StartedAt = d.StartedAt,
            CompletedAt = d.CompletedAt,
            CompletedById = d.CompletedById,
            CompletedByName = d.CompletedByNameSnapshot,

            IsDueToday = isDueToday,
            IsOverdue = isOverdue,

            CreatedAt = d.CreatedDate,
            UpdatedAt = d.ModifiedDate
        };
    }
}
