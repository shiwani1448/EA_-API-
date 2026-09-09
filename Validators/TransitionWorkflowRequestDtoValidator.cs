using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class TransitionWorkflowRequestDtoValidator : AbstractValidator<TransitionWorkflowRequestDto>
{
    public TransitionWorkflowRequestDtoValidator()
    {
        RuleFor(x => x.TargetStatusId).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
