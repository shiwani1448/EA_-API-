using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class ApplySuggestedOwnerRequestDtoValidator : AbstractValidator<ApplySuggestedOwnerRequestDto>
{
    public ApplySuggestedOwnerRequestDtoValidator()
    {
        RuleFor(x => x.DoerId).NotEmpty().WithMessage("DoerId must not be empty.");
        RuleFor(x => x.DoerId).MaximumLength(100).WithMessage("DoerId must not exceed 100 characters.");
        RuleFor(x => x.DoerName).MaximumLength(200).WithMessage("DoerName must not exceed 200 characters.");
    }
}

public class ApplyPredictedDueDateRequestDtoValidator : AbstractValidator<ApplyPredictedDueDateRequestDto>
{
    public ApplyPredictedDueDateRequestDtoValidator()
    {
        RuleFor(x => x.EndDate).NotEqual(default(DateTime)).WithMessage("EndDate must be provided.");
    }
}
