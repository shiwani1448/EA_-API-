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
        RuleFor(x => x.ModuleId).GreaterThan(0);
        RuleFor(x => x.TatMinutes).GreaterThan(0);
        RuleFor(x => x.IsActive).NotNull();
    }
}
