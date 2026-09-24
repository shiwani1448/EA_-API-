using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class ApplyApproverSuggestionRequestDtoValidator : AbstractValidator<ApplyApproverSuggestionRequestDto>
{
    public ApplyApproverSuggestionRequestDtoValidator()
    {
        RuleFor(x => x.ApproverName).NotEmpty().WithMessage("Approver name must not be empty.");
        RuleFor(x => x.ApproverId).MaximumLength(100).WithMessage("ApproverId must not exceed 100 characters.");
        RuleFor(x => x.ApproverName).MaximumLength(200).WithMessage("ApproverName must not exceed 200 characters.");
    }
}
