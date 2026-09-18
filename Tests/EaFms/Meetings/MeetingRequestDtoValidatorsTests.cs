using Jarvis5.Dtos.EaFms;
using Jarvis5.Validators;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// EA-wide frontend-owned-requiredness cleanup: Meeting.Title was backend-mandatory
/// (NotEmpty) purely as form requiredness — Meeting.Title is nullable at both the entity
/// and DB level, so there was no technical reason to reject a null/empty/whitespace
/// title. MaximumLength (a real DB-column-size integrity guard) is preserved.
/// </summary>
public class MeetingRequestDtoValidatorsTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("Weekly Review", true)]
    public void Create_Title_NullEmptyWhitespaceAndValue_AllAccepted(string? title, bool expectedValid)
    {
        var result = new CreateMeetingRequestDtoValidator().Validate(new CreateMeetingRequestDto { Title = title });
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Create_TitleOver500Chars_StillRejected_MaxLengthUnchanged()
    {
        var result = new CreateMeetingRequestDtoValidator().Validate(
            new CreateMeetingRequestDto { Title = new string('x', 501) });
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("Weekly Review", true)]
    public void Update_Title_NullEmptyWhitespaceAndValue_AllAccepted(string? title, bool expectedValid)
    {
        var result = new UpdateMeetingRequestDtoValidator().Validate(new UpdateMeetingRequestDto { Title = title });
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Update_TitleOver500Chars_StillRejected_MaxLengthUnchanged()
    {
        var result = new UpdateMeetingRequestDtoValidator().Validate(
            new UpdateMeetingRequestDto { Title = new string('x', 501) });
        Assert.False(result.IsValid);
    }
}
