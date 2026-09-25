namespace Jarvis5.Common.EaFms;

/// <summary>
/// Calendar event category — matches the EA's Calendar filter checkboxes (Client Meetings,
/// Internal Meetings, Personal, Travel). The EA can create these four herself. The calendar
/// also shows her work from the business modules automatically: Meetings as Client/Internal
/// meetings, Travel as Travel, and Delegation / Approval / Follow-up items as Task (Task is
/// read-only — it only ever comes from those modules and cannot be created by hand).
/// </summary>
public static class CalendarEventType
{
    public const string ClientMeeting = "ClientMeeting", InternalMeeting = "InternalMeeting", Personal = "Personal", Travel = "Travel",
        Task = "Task";

    /// <summary>Types the EA can create/update by hand.</summary>
    public static bool IsValid(string? value) => value is ClientMeeting or InternalMeeting or Personal or Travel;
}

/// <summary>Where a calendar entry comes from. Only Calendar entries are editable here.</summary>
public static class CalendarEventSource
{
    public const string Calendar = "Calendar", Meeting = "Meeting", Delegation = "Delegation", Approval = "Approval",
        Travel = "Travel", Followup = "Follow-up";

    public static readonly string[] All = [Calendar, Meeting, Delegation, Approval, Travel, Followup];
}
