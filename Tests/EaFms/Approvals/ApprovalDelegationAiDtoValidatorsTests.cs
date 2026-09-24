using Jarvis5.Dtos.EaFms;
using Jarvis5.Validators;
using Xunit;

namespace Jarvis5.Tests.EaFms.Approvals;

public class ApprovalDelegationAiDtoValidatorsTests
{
    [Fact]
    public void ApplyApproverSuggestion_AcceptsOptionalIdAndMappedLengthBoundaries()
    {
        var validator = new ApplyApproverSuggestionRequestDtoValidator();
        Assert.True(validator.Validate(new ApplyApproverSuggestionRequestDto
        {
            ApproverName = new string('n', 200), ApproverId = new string('i', 100)
        }).IsValid);
        Assert.True(validator.Validate(new ApplyApproverSuggestionRequestDto { ApproverName = "External Approver" }).IsValid);
    }

    [Theory]
    [InlineData(101, 1, "ApproverId", "ApproverId must not exceed 100 characters.")]
    [InlineData(1, 201, "ApproverName", "ApproverName must not exceed 200 characters.")]
    public void ApplyApproverSuggestion_RejectsValuesBeyondMappedLengths(int idLength, int nameLength, string field, string message)
    {
        var result = new ApplyApproverSuggestionRequestDtoValidator().Validate(new ApplyApproverSuggestionRequestDto
        {
            ApproverId = new string('i', idLength), ApproverName = new string('n', nameLength)
        });
        var error = Assert.Single(result.Errors);
        Assert.Equal(field, error.PropertyName);
        Assert.Equal(message, error.ErrorMessage);
    }

    [Fact]
    public void ApplySuggestedOwner_AcceptsMappedLengthBoundaries()
    {
        var result = new ApplySuggestedOwnerRequestDtoValidator().Validate(new ApplySuggestedOwnerRequestDto
        {
            DoerId = new string('i', 100), DoerName = new string('n', 200)
        });
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(101, 1, "DoerId", "DoerId must not exceed 100 characters.")]
    [InlineData(1, 201, "DoerName", "DoerName must not exceed 200 characters.")]
    public void ApplySuggestedOwner_RejectsValuesBeyondMappedLengths(int idLength, int nameLength, string field, string message)
    {
        var result = new ApplySuggestedOwnerRequestDtoValidator().Validate(new ApplySuggestedOwnerRequestDto
        {
            DoerId = new string('i', idLength), DoerName = new string('n', nameLength)
        });
        var error = Assert.Single(result.Errors);
        Assert.Equal(field, error.PropertyName);
        Assert.Equal(message, error.ErrorMessage);
    }
}
