namespace Jarvis5.Common.EaFms;

/// <summary>
/// Calendar event category — matches the EA's own Calendar filter checkboxes exactly
/// (Client Meetings, Internal Meetings, Personal, Travel). This is a standalone calendar,
/// like Google Calendar: the EA types every entry in herself; nothing here is ever read
/// from the Meeting/Delegation/Approval/Travel business modules.
/// </summary>
public static class CalendarEventType
{
    public const string ClientMeeting = "ClientMeeting", InternalMeeting = "InternalMeeting", Personal = "Personal", Travel = "Travel";

    public static bool IsValid(string? value) => value is ClientMeeting or InternalMeeting or Personal or Travel;
}
