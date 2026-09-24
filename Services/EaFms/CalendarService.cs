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

    private readonly EaFmsDbContext _db;
    private readonly ICurrentUserService _user;

    public CalendarService(EaFmsDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    private string Actor => _user.ActorDisplay();

    public async Task<List<CalendarEventDto>> GetEventsAsync(CalendarEventsQueryDto query, CancellationToken ct = default)
    {
        var events = await FilteredQuery(query).ToListAsync(ct);
        return events.OrderBy(e => e.StartDateTime).Select(ToDto).ToList();
    }

    public async Task<CalendarStatsDto> GetStatsAsync(CalendarEventsQueryDto query, CancellationToken ct = default)
    {
        // Stats always look across every event in range regardless of the caller's
        // IncludeCompleted filter — "how many are done" needs the completed ones counted.
        var statsQuery = new CalendarEventsQueryDto
        {
            From = query.From,
            To = query.To,
            EventTypes = query.EventTypes,
            OrganizerEmployeeId = query.OrganizerEmployeeId,
            Priority = query.Priority,
            IncludeCompleted = true,
        };
        var events = await FilteredQuery(statsQuery).ToListAsync(ct);
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

    private IQueryable<CalendarEvent> FilteredQuery(CalendarEventsQueryDto query)
    {
        if (query.From > query.To)
            throw new BadRequestException("From must be on or before To.");

        var eventTypes = NormalizeEventTypes(query.EventTypes);

        var q = _db.CalendarEvents.AsNoTracking()
            .Where(c => !c.IsDeleted && c.StartDateTime >= query.From && c.StartDateTime < query.To
                && eventTypes.Contains(c.EventType));

        if (!query.IncludeCompleted)
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
            return new HashSet<string>(AllEventTypes, StringComparer.Ordinal);

        // Map to the constants' own casing (never the caller's) so the values that reach the
        // SQL IN-clause always match what is actually stored, regardless of input casing.
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        var invalid = new List<string>();
        foreach (var t in requested)
        {
            var match = AllEventTypes.FirstOrDefault(e => string.Equals(e, t, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                invalid.Add(t);
            else
                normalized.Add(match);
        }

        if (invalid.Count > 0)
            throw new BadRequestException($"Unsupported eventType(s): {string.Join(", ", invalid)}. Valid values: {string.Join(", ", AllEventTypes)}.");

        return normalized;
    }
}
