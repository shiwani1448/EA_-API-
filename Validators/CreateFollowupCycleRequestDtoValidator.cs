using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateFollowupCycleRequestDtoValidator : AbstractValidator<CreateFollowupCycleRequestDto>
{
    public CreateFollowupCycleRequestDtoValidator()
    {
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.OutcomeCode).MaximumLength(100);
        RuleFor(x => x.NextFollowupAt).GreaterThan(DateTime.MinValue).When(x => x.NextFollowupAt.HasValue);
        RuleFor(x => x.ExpectedResponseAt).GreaterThan(DateTime.MinValue).When(x => x.ExpectedResponseAt.HasValue);
    }
}
