using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class StartWorkflowRequestDtoValidator : AbstractValidator<StartWorkflowRequestDto>
{
    public StartWorkflowRequestDtoValidator()
    {
        RuleFor(x => x.IntakeRequestId).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
