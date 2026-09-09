using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateEscalationRequestDtoValidator : AbstractValidator<CreateEscalationRequestDto>
{
    public CreateEscalationRequestDtoValidator()
    {
        RuleFor(x => x.FollowupId).GreaterThan(0);
        RuleFor(x => x.EscalationLevelId).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.EscalatedToId).MaximumLength(100);
        RuleFor(x => x.EscalatedToName).MaximumLength(200);
        RuleFor(x => x.NextEscalationLevelId).GreaterThan(0).When(x => x.NextEscalationLevelId.HasValue);
        RuleFor(x => x.NextEscalationAt).GreaterThan(DateTime.MinValue).When(x => x.NextEscalationAt.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
        // Ensure level ids reference active/non-deleted entries at service level
    }
}
