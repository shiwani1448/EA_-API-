using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class TatRuleDtoValidator : AbstractValidator<TatRuleDto>
{
    public TatRuleDtoValidator()
    {
        RuleFor(x => x.BusinessModuleId).GreaterThan(0);
        RuleFor(x => x.Minutes).GreaterThanOrEqualTo(0);
    }
}
