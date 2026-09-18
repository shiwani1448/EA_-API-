namespace Jarvis5.Common;

/// <summary>
/// India (Asia/Kolkata) business-date helper. A fixed +05:30 offset is used instead of
/// TimeZoneInfo.FindSystemTimeZoneById — India has observed one unchanging UTC+05:30
/// offset since 1945 (no DST), so the fixed offset is exact, not an approximation, and it
/// avoids depending on the host's timezone database ("India Standard Time" on Windows vs
/// "Asia/Kolkata" on Linux/ICU, which can differ or be missing between environments).
/// </summary>
public static class IndiaBusinessCalendar
{
    private static readonly TimeSpan Offset = new(5, 30, 0);

    /// <summary>Today's calendar date in India business time, derived from the current UTC
    /// instant — NOT the same as DateTime.UtcNow.Date near the midnight IST boundary
    /// (00:00-05:29 IST falls on the previous UTC calendar day).</summary>
    public static DateTime Today => ToIndiaDate(Clock.UtcNowTz);

    /// <summary>Pure conversion, extracted for deterministic boundary testing: the India
    /// business calendar date for a given UTC instant.</summary>
    public static DateTime ToIndiaDate(DateTime utcInstant) => (utcInstant + Offset).Date;
}
