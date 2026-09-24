using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class ApplySuggestedOwnerRequestDtoValidator : AbstractValidator<ApplySuggestedOwnerRequestDto>
{
    public ApplySuggestedOwnerRequestDtoValidator()
    {
        RuleFor(x => x.DoerId).NotEmpty().WithMessage("DoerId must not be empty.");
    }
}

public class ApplyPredictedDueDateRequestDtoValidator : AbstractValidator<ApplyPredictedDueDateRequestDto>
{
    public ApplyPredictedDueDateRequestDtoValidator()
    {
        RuleFor(x => x.EndDate).NotEqual(default(DateTime)).WithMessage("EndDate must be provided.");
    }
}
