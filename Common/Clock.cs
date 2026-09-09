namespace Jarvis5.Common;

/// <summary>
/// SCIH_Request/SCIH_RequestHistory timestamp columns are "timestamp without time zone"
/// per spec. Npgsql rejects Kind=Utc values against that column type, so all "now" values
/// written to the DB go through here to store UTC wall-clock time with Kind=Unspecified.
/// </summary>
public static class Clock
{
    public static DateTime UtcNow => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    /// <summary>For "timestamp with time zone" columns (SCIH_Task/SCIH_StageMaster/
    /// SCIH_TaskHistory) — Npgsql requires Kind=Utc for timestamptz, the opposite of
    /// the Kind=Unspecified UtcNow above needs for "timestamp without time zone".</summary>
    public static DateTime UtcNowTz => DateTime.UtcNow;
}
