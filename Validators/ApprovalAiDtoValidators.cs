using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class ApplyApproverSuggestionRequestDtoValidator : AbstractValidator<ApplyApproverSuggestionRequestDto>
{
    public ApplyApproverSuggestionRequestDtoValidator()
    {
        RuleFor(x => x.ApproverName).NotEmpty().WithMessage("Approver name must not be empty.");
    }
}
