using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class CalendarService : ICalendarService
{
    private static readonly string[] AllEventTypes =
        { CalendarEventType.ClientMeeting, CalendarEventType.InternalMeeting, CalendarEventType.Personal, CalendarEventType.Travel };
    /// <summary>Types a caller may filter on (Task only ever comes from the modules).</summary>
    private static readonly string[] FilterEventTypes = [.. AllEventTypes, CalendarEventType.Task];

    private readonly EaFmsDbContext _db;
    private readonly ICurrentUserService _user;

    public CalendarService(EaFmsDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    private string Actor => _user.ActorDisplay();

    public async Task<List<CalendarEventDto>> GetEventsAsync(CalendarEventsQueryDto query, CancellationToken ct = default) =>
        await LoadAsync(query, query.IncludeCompleted, ct);

    /// <summary>The EA's own entries plus (by default) her module work, filtered and sorted by start time.</summary>
    private async Task<List<CalendarEventDto>> LoadAsync(CalendarEventsQueryDto query, bool includeCompleted, CancellationToken ct)
    {
        var sources = NormalizeSources(query.Sources);
        var eventTypes = NormalizeEventTypes(query.EventTypes);
        var result = new List<CalendarEventDto>();

        if (sources.Contains(CalendarEventSource.Calendar))
        {
            var own = await FilteredQuery(query, includeCompleted).ToListAsync(ct);
            var explicitPerson = Person.From(query.EmployeeId, query.EmployeeName);
            result.AddRange(own.Where(e => explicitPerson.IsEveryone || explicitPerson.Is(e.OrganizerEmployeeId, e.OrganizerName, e.CreatedBy)).Select(ToDto));
        }

        if (query.IncludeLinked)
        {
            var person = Person.From(query.EmployeeId ?? _user.ActorId(), query.EmployeeName ?? (query.EmployeeId is null ? _user.ActorName() : null));
            var linked = await LoadLinkedAsync(query.From, query.To, sources, person, ct);
            result.AddRange(linked.Where(e =>
                eventTypes.Contains(e.EventType)
                && (includeCompleted || !e.IsCompleted)
                && (string.IsNullOrWhiteSpace(query.OrganizerEmployeeId) || string.Equals(e.OrganizerEmployeeId, query.OrganizerEmployeeId, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(query.Priority) || string.Equals(e.Priority, query.Priority, StringComparison.OrdinalIgnoreCase))));
        }

        return result.OrderBy(e => e.StartDateTime).ThenBy(e => e.EventKey, StringComparer.Ordinal).ToList();
    }

    public async Task<CalendarStatsDto> GetStatsAsync(CalendarEventsQueryDto query, CancellationToken ct = default)
    {
        // Stats always look across every event in range regardless of the caller's
        // IncludeCompleted filter — "how many are done" needs the completed ones counted.
        var events = await LoadAsync(query, includeCompleted: true, ct);
        var now = Clock.UtcNowTz;

        return new CalendarStatsDto
        {
            Total = events.Count,
            Completed = events.Count(e => e.IsCompleted),
            Upcoming = events.Count(e => !e.IsCompleted && e.StartDateTime >= now),
            ScheduledHours = events
                .Where(e => !e.IsAllDay && e.EndDateTime != null)
                .Sum(e => (e.EndDateTime!.Value - e.StartDateTime).TotalHours),
        };
    }

    private IQueryable<CalendarEvent> FilteredQuery(CalendarEventsQueryDto query, bool includeCompleted)
    {
        if (query.From > query.To)
            throw new BadRequestException("From must be on or before To.");

        var eventTypes = NormalizeEventTypes(query.EventTypes);

        var q = _db.CalendarEvents.AsNoTracking()
            .Where(c => !c.IsDeleted && c.StartDateTime >= query.From && c.StartDateTime < query.To
                && eventTypes.Contains(c.EventType));

        if (!includeCompleted)
            q = q.Where(c => !c.IsCompleted);
        if (!string.IsNullOrWhiteSpace(query.OrganizerEmployeeId))
            q = q.Where(c => c.OrganizerEmployeeId == query.OrganizerEmployeeId);
        if (!string.IsNullOrWhiteSpace(query.Priority))
            q = q.Where(c => c.Priority == query.Priority);

        return q;
    }

    public async Task<CalendarEventDto> CreateEventAsync(CreateCalendarEventDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new BusinessRuleException("Title must not be empty.");
        if (dto.StartDateTime is null)
            throw new BusinessRuleException("StartDateTime must be provided.");

        var eventType = string.IsNullOrWhiteSpace(dto.EventType) ? null : dto.EventType.Trim();
        if (!CalendarEventType.IsValid(eventType))
            throw new BadRequestException($"Unsupported eventType '{dto.EventType}'. Valid values: {string.Join(", ", AllEventTypes)}.");

        var now = Clock.UtcNowTz;
        var entity = new CalendarEvent
        {
            Title = dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            EventType = eventType!,
            StartDateTime = dto.StartDateTime.Value,
            EndDateTime = dto.EndDateTime,
            IsAllDay = dto.IsAllDay,
            Location = dto.Location,
            Priority = dto.Priority,
            OrganizerEmployeeId = dto.OrganizerEmployeeId,
            OrganizerName = dto.OrganizerName,
            Notes = dto.Notes,
            CreatedBy = Actor,
            CreatedDate = now,
        };

        _db.CalendarEvents.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<CalendarEventDto> GetEventAsync(long id, CancellationToken ct = default)
    {
        var entity = await _db.CalendarEvents.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException($"Calendar event {id} not found.");
        return ToDto(entity);
    }

    public async Task<CalendarEventDto> UpdateEventAsync(long id, UpdateCalendarEventDto dto, CancellationToken ct = default)
    {
        var entity = await _db.CalendarEvents.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException($"Calendar event {id} not found.");

        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new BusinessRuleException("Title must not be empty.");
        if (dto.StartDateTime is null)
            throw new BusinessRuleException("StartDateTime must be provided.");

        var eventType = string.IsNullOrWhiteSpace(dto.EventType) ? null : dto.EventType.Trim();
        if (!CalendarEventType.IsValid(eventType))
            throw new BadRequestException($"Unsupported eventType '{dto.EventType}'. Valid values: {string.Join(", ", AllEventTypes)}.");

        entity.Title = dto.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.EventType = eventType!;
        entity.StartDateTime = dto.StartDateTime.Value;
        entity.EndDateTime = dto.EndDateTime;
        entity.IsAllDay = dto.IsAllDay;
        entity.Location = dto.Location;
        entity.Priority = dto.Priority;
        entity.OrganizerEmployeeId = dto.OrganizerEmployeeId;
        entity.OrganizerName = dto.OrganizerName;
        entity.Notes = dto.Notes;
        entity.IsCompleted = dto.IsCompleted;
        entity.ModifiedBy = Actor;
        entity.ModifiedDate = Clock.UtcNowTz;

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteEventAsync(long id, CancellationToken ct = default)
    {
        var entity = await _db.CalendarEvents.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException($"Calendar event {id} not found.");

        entity.IsDeleted = true;
        entity.ModifiedBy = Actor;
        entity.ModifiedDate = Clock.UtcNowTz;
        await _db.SaveChangesAsync(ct);
    }

    private static CalendarEventDto ToDto(CalendarEvent c) => new()
    {
        Id = c.Id,
        EventKey = $"{CalendarEventSource.Calendar}:{c.Id}",
        Source = CalendarEventSource.Calendar,
        Title = c.Title,
        Description = c.Description,
        EventType = c.EventType,
        StartDateTime = c.StartDateTime,
        EndDateTime = c.EndDateTime,
        IsAllDay = c.IsAllDay,
        Location = c.Location,
        Priority = c.Priority,
        OrganizerEmployeeId = c.OrganizerEmployeeId,
        OrganizerName = c.OrganizerName,
        Notes = c.Notes,
        IsCompleted = c.IsCompleted,
        CreatedDate = c.CreatedDate,
        ModifiedDate = c.ModifiedDate,
    };

    private static HashSet<string> NormalizeEventTypes(string[]? requested)
    {
        if (requested is null || requested.Length == 0)
            return new HashSet<string>(FilterEventTypes, StringComparer.Ordinal);

        // Map to the constants' own casing (never the caller's) so the values that reach the
        // SQL IN-clause always match what is actually stored, regardless of input casing.
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        var invalid = new List<string>();
        foreach (var t in requested)
        {
            var match = FilterEventTypes.FirstOrDefault(e => string.Equals(e, t, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                invalid.Add(t);
            else
                normalized.Add(match);
        }

        if (invalid.Count > 0)
            throw new BadRequestException($"Unsupported eventType(s): {string.Join(", ", invalid)}. Valid values: {string.Join(", ", FilterEventTypes)}.");

        return normalized;
    }

    // =================================================================================== module entries (live, read-only)

    private static HashSet<string> NormalizeSources(string[]? requested)
    {
        if (requested is null || requested.Length == 0) return new HashSet<string>(CalendarEventSource.All, StringComparer.Ordinal);
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in requested)
            normalized.Add(CalendarEventSource.All.FirstOrDefault(a => string.Equals(a, s?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new BadRequestException($"Unsupported source '{s}'. Valid values: {string.Join(", ", CalendarEventSource.All)}."));
        return normalized;
    }

    /// <summary>
    /// The EA's work from the business modules inside [from, to), read live (nothing is copied):
    /// Meeting at its date/time; Delegation from start to due date; Approval on its required date;
    /// Travel from departure to return; Follow-up at its due time. An entry is included when it
    /// overlaps the range and (for a specific EA) she is involved in it.
    /// </summary>
    private async Task<List<CalendarEventDto>> LoadLinkedAsync(DateTime from, DateTime to, HashSet<string> sources, Person person, CancellationToken ct)
    {
        var list = new List<CalendarEventDto>();

        if (sources.Contains(CalendarEventSource.Meeting))
        {
            var meetings = await _db.Meetings.AsNoTracking()
                .Where(m => !m.IsDeleted && (m.StartDateTime ?? m.MeetingDate) != null
                    && (m.StartDateTime ?? m.MeetingDate) < to && (m.EndDateTime ?? m.StartDateTime ?? m.MeetingDate) >= from)
                .ToListAsync(ct);
            HashSet<long> attended = [];
            if (!person.IsEveryone && meetings.Count > 0)
            {
                var ids = meetings.Select(m => m.Id).ToList();
                attended = (await _db.MeetingAttendees.AsNoTracking().Where(a => !a.IsDeleted && ids.Contains(a.MeetingId)).ToListAsync(ct))
                    .Where(a => person.Is(a.ParticipantId, a.ParticipantName)).Select(a => a.MeetingId).ToHashSet();
            }
            foreach (var m in meetings.Where(m => person.IsEveryone || person.Is(m.CreatedBy, m.OrganizerId, m.OrganizerName)
                         || person.IsAny(m.DoerIds) || person.IsAny(m.DoerNames) || attended.Contains(m.Id)))
            {
                var start = (m.StartDateTime ?? m.MeetingDate)!.Value;
                var kind = $"{m.MeetingType} {m.Category}";
                var external = kind.Contains("client", StringComparison.OrdinalIgnoreCase) || kind.Contains("external", StringComparison.OrdinalIgnoreCase);
                list.Add(Linked(CalendarEventSource.Meeting, m.Id, m.MeetingNumber, m.Title ?? m.MeetingNumber ?? "Meeting", m.Description,
                    external ? CalendarEventType.ClientMeeting : CalendarEventType.InternalMeeting,
                    start, m.EndDateTime, isAllDay: m.StartDateTime is null, m.Location, m.Priority, m.OrganizerId, m.OrganizerName,
                    completed: m.CompletedAt.HasValue, status: m.CompletedAt.HasValue ? "Completed" : "Scheduled"));
            }
        }

        if (sources.Contains(CalendarEventSource.Delegation))
        {
            var delegations = await _db.Delegations.AsNoTracking()
                .Where(d => !d.IsDeleted && (d.StartDate ?? d.DueDate) != null && (d.StartDate ?? d.DueDate) < to && (d.DueDate ?? d.StartDate) >= from)
                .ToListAsync(ct);
            foreach (var d in delegations.Where(d => person.IsEveryone || person.Is(d.DoerId, d.DoerNameSnapshot) || person.Is(d.AssignedById, d.AssignedByNameSnapshot)))
                list.Add(Linked(CalendarEventSource.Delegation, d.Id, d.ReferenceNo, d.Title, d.Description, CalendarEventType.Task,
                    (d.StartDate ?? d.DueDate)!.Value, d.DueDate, isAllDay: true, null, d.Priority, d.AssignedById, d.AssignedByNameSnapshot,
                    completed: d.Status == DelegationStatus.Completed, status: d.Status));
        }

        if (sources.Contains(CalendarEventSource.Approval))
        {
            var approvals = await _db.ApprovalRequests.AsNoTracking()
                .Where(a => !a.IsDeleted && a.RequiredApprovalDate != null && a.RequiredApprovalDate < to && a.RequiredApprovalDate >= from)
                .ToListAsync(ct);
            foreach (var a in approvals.Where(a => person.IsEveryone || person.Is(a.RequestedBy, a.CreatedBy) || person.Is(a.ApproverId, a.ApproverName)))
            {
                var status = a.WorkflowStatus ?? "Draft";
                list.Add(Linked(CalendarEventSource.Approval, a.Id, a.ReferenceNo, a.RequestTitle ?? a.ReferenceNo, a.Description, CalendarEventType.Task,
                    a.RequiredApprovalDate!.Value, null, isAllDay: true, null, a.Priority, null, a.RequestedBy ?? a.CreatedBy,
                    completed: status is "Approved" or "Rejected", status: status));
            }
        }

        if (sources.Contains(CalendarEventSource.Travel))
        {
            var travel = await _db.TravelRequests.AsNoTracking()
                .Where(t => !t.IsDeleted && (t.DepartureDate ?? t.RequiredDate) != null && (t.DepartureDate ?? t.RequiredDate) < to
                    && (t.ReturnDate ?? t.DepartureDate ?? t.RequiredDate) >= from)
                .ToListAsync(ct);
            foreach (var t in travel.Where(t => person.IsEveryone || person.Is(t.CreatedBy) || person.Is(t.ApproverId, t.ApproverNameSnapshot)))
            {
                var route = string.Join(" → ", new[] { t.FromLocation, t.ToLocation }.Where(x => !string.IsNullOrWhiteSpace(x)));
                list.Add(Linked(CalendarEventSource.Travel, t.Id, t.ReferenceNo, t.Purpose ?? (route.Length > 0 ? route : $"Travel {t.ReferenceNo}"),
                    route.Length > 0 ? route : null, CalendarEventType.Travel,
                    (t.DepartureDate ?? t.RequiredDate)!.Value, t.ReturnDate, isAllDay: true, t.ToLocation, t.Priority, null, t.CreatedBy,
                    completed: t.CompletedAt.HasValue, status: t.BusinessState));
            }
        }

        if (sources.Contains(CalendarEventSource.Followup))
        {
            var followups = await _db.Followups.AsNoTracking()
                .Where(f => !f.IsDeleted && f.DueAt < to && f.DueAt >= from)
                .ToListAsync(ct);
            foreach (var f in followups.Where(f => person.IsEveryone || person.Is(f.DoerId, f.DoerName)
                         || person.Is(f.CreatedByEmployeeId, f.CreatedByEmployeeName, f.CreatedBy)))
                list.Add(Linked(CalendarEventSource.Followup, f.Id, null, f.Subject ?? "Follow-up", f.Note, CalendarEventType.Task,
                    f.DueAt, null, isAllDay: false, null, null, f.CreatedByEmployeeId, f.CreatedByEmployeeName ?? f.CreatedBy,
                    completed: f.CompletedAt.HasValue, status: f.CompletedAt.HasValue ? "Completed" : "Pending"));
        }

        return list;
    }

    private static CalendarEventDto Linked(string source, long recordId, string? reference, string title, string? description, string eventType,
        DateTime start, DateTime? end, bool isAllDay, string? location, string? priority, string? organizerId, string? organizerName,
        bool completed, string? status) => new()
    {
        Id = 0,
        EventKey = $"{source}:{recordId}",
        Source = source,
        SourceRecordId = recordId,
        ReferenceNo = reference,
        IsReadOnly = true,
        Status = status,
        Title = title,
        Description = description,
        EventType = eventType,
        StartDateTime = start,
        EndDateTime = end,
        IsAllDay = isAllDay,
        Location = location,
        Priority = priority,
        OrganizerEmployeeId = organizerId,
        OrganizerName = organizerName,
        IsCompleted = completed,
    };

    /// <summary>Whose calendar: matched on a stored id or display name (several modules store only a name).</summary>
    private sealed record Person(string? Id, string? Name)
    {
        public bool IsEveryone => Id is null && Name is null;

        public static Person From(string? id, string? name) =>
            new(string.IsNullOrWhiteSpace(id) ? null : id.Trim(), Norm(name));

        public bool Is(params string?[] stored)
        {
            foreach (var v in stored)
            {
                if (string.IsNullOrWhiteSpace(v)) continue;
                if (Id is not null && string.Equals(v.Trim(), Id, StringComparison.OrdinalIgnoreCase)) return true;
                if (Name is not null && Norm(v) == Name) return true;
            }
            return false;
        }

        public bool IsAny(IEnumerable<string>? stored) => stored is not null && stored.Any(v => Is(v));

        private static string? Norm(string? v) =>
            string.IsNullOrWhiteSpace(v) ? null : string.Join(' ', v.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }
}
