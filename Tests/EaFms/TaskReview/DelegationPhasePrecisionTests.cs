using System.Reflection;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Xunit;

namespace Jarvis5.Tests.EaFms.TaskReview;

public class DelegationPhasePrecisionTests
{
    private static DelegationPhaseTatDto Map(DelegationPhaseTat phase, DateTime now, params WorkPause[] pauses) =>
        (DelegationPhaseTatDto)typeof(DelegationService).GetMethod("ToPhaseTatDto", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { phase, pauses, now })!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveReview_PreservesFractionalPauseSecondsAndComplement(bool multiple)
    {
        var start = new DateTime(2026, 9, 23, 9, 0, 0, DateTimeKind.Utc);
        var phase = new DelegationPhaseTat { TaskType = "Review", ReviewCycleNumber = 1,
            StartedAt = start, AllottedTatMinutes = 10 };
        var pauses = new List<WorkPause> { new() { StartAt = start.AddSeconds(10), EndAt = start.AddSeconds(10).AddTicks(33997470) } };
        if (multiple) pauses.Add(new WorkPause { StartAt = start.AddSeconds(30), EndAt = start.AddSeconds(30).AddTicks(184648690) });
        var dto = Map(phase, start.AddTicks(1302646160), pauses.ToArray());
        var paused = multiple ? 21.864616m : 3.399747m;
        Assert.Equal(paused, dto.TatPausedSeconds);
        Assert.Equal(130.264616m - paused, dto.TatUsedSeconds);
        Assert.Equal(600m, dto.TatUsedSeconds + dto.TatDifferenceSeconds);
        Assert.Equal(multiple ? 2 : 1, dto.PauseCount);
        Assert.Equal(0, dto.TatPausedMinutes);
        Assert.Equal((int)((130.264616m - paused) / 60m), dto.TatUsedMinutes);
        Assert.Equal(10 - dto.TatUsedMinutes, dto.TatDifferenceMinutes);
    }

    [Fact]
    public void ActiveOverdueReview_PreciseDifferenceCanBeNegative()
    {
        var start = DateTime.UtcNow;
        var dto = Map(new DelegationPhaseTat { TaskType = "Review", StartedAt = start, AllottedTatMinutes = 10 },
            start.AddTicks(6001250000));
        Assert.Equal(600.125m, dto.TatUsedSeconds);
        Assert.Equal(-0.125m, dto.TatDifferenceSeconds);
        Assert.Equal(600m, dto.TatUsedSeconds + dto.TatDifferenceSeconds);
        Assert.Equal(0, dto.TatDifferenceMinutes);
    }

    [Fact]
    public void LegacyClosedPhase_DoesNotInventSecondsFromTruncatedMinutes()
    {
        var start = DateTime.UtcNow.AddMinutes(-5);
        var dto = Map(new DelegationPhaseTat { TaskType = "Review", StartedAt = start, EndedAt = start.AddMinutes(2),
            AllottedTatMinutes = 10, TatUsedMinutes = 1, TatPausedMinutes = 0, PauseCount = 2 }, DateTime.UtcNow);
        Assert.Null(dto.TatUsedSeconds);
        Assert.Null(dto.TatPausedSeconds);
        Assert.Null(dto.TatDifferenceSeconds);
        Assert.Equal(1, dto.TatUsedMinutes);
        Assert.Equal(9, dto.TatDifferenceMinutes);
        Assert.Equal(2, dto.PauseCount);
    }

    [Fact]
    public void NotStarted_ZeroElapsed_FullBudgetRemaining_NeverTouchesTatSummaryCalculator()
    {
        // StartedAt null, EndedAt null — opened idle, waiting for its explicit Start action.
        var dto = Map(new DelegationPhaseTat { TaskType = "Review", AllottedTatMinutes = 10 }, DateTime.UtcNow,
            new WorkPause { StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddMinutes(1) });
        Assert.Null(dto.StartedAt);
        Assert.Null(dto.EndedAt);
        Assert.Equal(0, dto.TatUsedMinutes);
        Assert.Equal(0m, dto.TatUsedSeconds);
        Assert.Equal(0, dto.TatPausedMinutes);
        Assert.Equal(0m, dto.TatPausedSeconds);
        Assert.Equal(0, dto.PauseCount);
        Assert.Equal(10, dto.TatDifferenceMinutes);
        Assert.Equal(600m, dto.TatDifferenceSeconds);
    }

    [Fact]
    public void NotStarted_NoBudget_UsedAndDifferenceStayNull()
    {
        var dto = Map(new DelegationPhaseTat { TaskType = "Rework" }, DateTime.UtcNow);
        Assert.Null(dto.StartedAt);
        Assert.Null(dto.TatUsedMinutes);
        Assert.Null(dto.TatUsedSeconds);
        Assert.Null(dto.TatDifferenceMinutes);
        Assert.Null(dto.TatDifferenceSeconds);
        Assert.Equal(0, dto.TatPausedMinutes);
        Assert.Equal(0, dto.PauseCount);
    }

    [Fact]
    public void NoBudget_KeepsUsedAndDifferenceNullButPreservesPausedSeconds()
    {
        var start = DateTime.UtcNow;
        var dto = Map(new DelegationPhaseTat { TaskType = "Review", StartedAt = start }, start.AddSeconds(20),
            new WorkPause { StartAt = start.AddSeconds(5), EndAt = start.AddSeconds(8.5) });
        Assert.Null(dto.TatUsedSeconds);
        Assert.Null(dto.TatDifferenceSeconds);
        Assert.Null(dto.TatUsedMinutes);
        Assert.Equal(3.5m, dto.TatPausedSeconds);
        Assert.Equal(0, dto.TatPausedMinutes);
        Assert.Equal(1, dto.PauseCount);
    }
}
