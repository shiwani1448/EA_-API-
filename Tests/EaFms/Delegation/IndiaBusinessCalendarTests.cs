using System;
using System.Globalization;
using Jarvis5.Common;
using Xunit;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// Boundary tests for the India (Asia/Kolkata, UTC+05:30) business-date conversion used by
/// Delegation's Due Today / Overdue calculations. The interesting case is the ~5.5 hour
/// window where the UTC calendar date and the India business date disagree.
/// </summary>
public class IndiaBusinessCalendarTests
{
    private static DateTime Utc(string iso) =>
        DateTime.Parse(iso, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

    [Theory]
    [InlineData("2026-09-17T18:29:59Z", "2026-09-17")] // 23:59:59 IST — still the same UTC day
    [InlineData("2026-09-17T18:30:00Z", "2026-09-18")] // 00:00:00 IST — already the next UTC day
    [InlineData("2026-09-17T23:59:59Z", "2026-09-18")] // late UTC evening, well into IST's next day
    [InlineData("2026-09-18T00:00:00Z", "2026-09-18")] // 05:30 IST — unambiguously same day
    [InlineData("2026-09-17T12:00:00Z", "2026-09-17")] // midday, no boundary ambiguity at all
    public void ToIndiaDate_ShiftsAcrossTheMidnightIstBoundary_NotTheUtcBoundary(string utcIso, string expectedIndiaDate)
    {
        var result = IndiaBusinessCalendar.ToIndiaDate(Utc(utcIso));
        Assert.Equal(DateTime.Parse(expectedIndiaDate, CultureInfo.InvariantCulture), result);
    }

    [Fact]
    public void ToIndiaDate_DiffersFromPlainUtcDate_InTheEveningBoundaryWindow()
    {
        // 19:00 UTC = 00:30 IST the next calendar day — the exact case a naive
        // DateTime.UtcNow.Date comparison would get wrong for an India business audience.
        var instant = Utc("2026-09-17T19:00:00Z");
        Assert.Equal(new DateTime(2026, 9, 17), instant.Date);
        Assert.Equal(new DateTime(2026, 9, 18), IndiaBusinessCalendar.ToIndiaDate(instant));
    }

    [Fact]
    public void Today_ReturnsIndiaDateForTheCurrentInstant()
    {
        // Cross-checked against the pure conversion rather than a fixed literal, so this
        // stays correct on every future test run.
        Assert.Equal(IndiaBusinessCalendar.ToIndiaDate(Clock.UtcNowTz), IndiaBusinessCalendar.Today);
    }
}
