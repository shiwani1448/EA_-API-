using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class TatRuleDtoValidator : AbstractValidator<SaveTatRuleDto>
{
    public TatRuleDtoValidator()
    {
        RuleFor(x => x.Type).NotEmpty().MaximumLength(200);
        // Presence is module-dependent (required except for Delegation) and is enforced by TatRuleService.
        RuleFor(x => x.Subtype).MaximumLength(200);
        // Presence is the inverse — required only for Delegation — and, like Subtype, enforced by TatRuleService.
        // Format (one of Actual/Review/Rework) is checked here since it never depends on which module was picked.
        RuleFor(x => x.TaskType).Must(Jarvis5.Common.EaFms.DelegationTaskType.IsValid)
            .WithMessage("TaskType must be one of: Actual, Review, Rework.")
            .When(x => x.TaskType is not null);
        RuleFor(x => x.ModuleId).GreaterThan(0);
        RuleFor(x => x.TatMinutes).GreaterThan(0);
        RuleFor(x => x.IsActive).NotNull();
    }
}
