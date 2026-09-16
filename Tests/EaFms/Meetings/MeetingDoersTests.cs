using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

public sealed class MeetingDoersTests
{
    [Fact]
    public void ToArrays_EmptyInput_ProducesPairedEmptyArrays()
    {
        var result = MeetingDoers.ToArrays([]);
        Assert.Empty(result.Ids);
        Assert.Empty(result.Names);
    }

    [Fact]
    public void ToArrays_MultipleDoers_TrimsAndPreservesOrder()
    {
        var result = MeetingDoers.ToArrays([
            new MeetingDoerDto { DoerId = " EMP001 ", DoerName = " Person One " },
            new MeetingDoerDto { DoerId = "EMP002", DoerName = "Person Two" }
        ]);

        Assert.Equal(["EMP001", "EMP002"], result.Ids);
        Assert.Equal(["Person One", "Person Two"], result.Names);
    }

    [Theory]
    [InlineData("", "Person One")]
    [InlineData("   ", "Person One")]
    [InlineData("EMP001", "")]
    [InlineData("EMP001", "   ")]
    public void ToArrays_BlankDoerData_IsRejected(string doerId, string doerName)
    {
        Assert.Throws<BadRequestException>(() => MeetingDoers.ToArrays([
            new MeetingDoerDto { DoerId = doerId, DoerName = doerName }
        ]));
    }

    [Fact]
    public void ToDtos_MapsPersistedPairedArrays()
    {
        var result = MeetingDoers.ToDtos(new Meeting
        {
            DoerIds = ["EMP001", "EMP002"],
            DoerNames = ["Person One", "Person Two"]
        });

        Assert.Collection(result,
            doer => Assert.Equal(("EMP001", "Person One"), (doer.DoerId, doer.DoerName)),
            doer => Assert.Equal(("EMP002", "Person Two"), (doer.DoerId, doer.DoerName)));
    }
}
