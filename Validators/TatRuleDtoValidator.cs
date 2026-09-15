using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class TatRuleDtoValidator : AbstractValidator<SaveTatRuleDto>
{
    public TatRuleDtoValidator()
    {
        RuleFor(x => x.Type).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subtype).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ModuleId).GreaterThan(0);
        RuleFor(x => x.TatMinutes).GreaterThan(0);
        RuleFor(x => x.IsActive).NotNull();
    }
}
