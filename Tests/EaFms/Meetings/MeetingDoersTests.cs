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

    // ----------------------------------------------------------------
    // EA-wide frontend-owned-requiredness cleanup: DoerId/DoerName are ordinary
    // frontend form fields, not backend-mandatory. Blank/null values are accepted
    // and stored as "" (Meeting.DoerIds/DoerNames are non-nullable string[] columns
    // with a DB check constraint forbidding NULL array elements, so "" is the
    // storage-permitted representation of "no value supplied"). Neither field is
    // ever fabricated or copied from the other.
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(null, "Person One", "", "Person One")]
    [InlineData("", "Person One", "", "Person One")]
    [InlineData("   ", "Person One", "", "Person One")]
    [InlineData("EMP001", null, "EMP001", "")]
    [InlineData("EMP001", "", "EMP001", "")]
    [InlineData("EMP001", "   ", "EMP001", "")]
    [InlineData(null, null, "", "")]
    public void ToArrays_BlankOrNullDoerData_IsAcceptedAndStoredAsEmptyString(
        string? doerId, string? doerName, string expectedId, string expectedName)
    {
        var result = MeetingDoers.ToArrays([
            new MeetingDoerDto { DoerId = doerId, DoerName = doerName }
        ]);

        Assert.Single(result.Ids);
        Assert.Single(result.Names);
        Assert.Equal(expectedId, result.Ids[0]);
        Assert.Equal(expectedName, result.Names[0]);
    }

    [Fact]
    public void ToArrays_DoerIdSuppliedWithoutDoerName_NeverFabricatesOrCopies()
    {
        var result = MeetingDoers.ToArrays([new MeetingDoerDto { DoerId = "EMP001", DoerName = null }]);
        Assert.Equal("EMP001", result.Ids[0]);
        Assert.Equal(string.Empty, result.Names[0]); // never copied from DoerId
    }

    [Fact]
    public void ToArrays_DoerNameSuppliedWithoutDoerId_NeverFabricatesOrCopies()
    {
        var result = MeetingDoers.ToArrays([new MeetingDoerDto { DoerId = null, DoerName = "Person One" }]);
        Assert.Equal(string.Empty, result.Ids[0]); // never copied from DoerName
        Assert.Equal("Person One", result.Names[0]);
    }

    [Fact]
    public void ToArrays_CardinalityStaysEqual_AcrossMixedBlankAndFilledDoers()
    {
        var result = MeetingDoers.ToArrays([
            new MeetingDoerDto { DoerId = "EMP001", DoerName = "Person One" },
            new MeetingDoerDto { DoerId = null, DoerName = null },
            new MeetingDoerDto { DoerId = "EMP003", DoerName = null }
        ]);

        Assert.Equal(3, result.Ids.Length);
        Assert.Equal(3, result.Names.Length);
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
