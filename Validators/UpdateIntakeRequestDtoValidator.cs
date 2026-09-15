using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class UpdateIntakeRequestDtoValidator : AbstractValidator<UpdateIntakeRequestDto>
{
    public UpdateIntakeRequestDtoValidator()
    {
        RuleFor(x => x.Title).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.RequiredDate).Must(d => d == null || d.Value.Kind == DateTimeKind.Utc).WithMessage("RequiredDate must be UTC or null");
        RuleFor(x => x.BusinessModuleId).GreaterThan(0).When(x => x.BusinessModuleId.HasValue);
        RuleFor(x => x.StatusId).GreaterThan(0).When(x => x.StatusId.HasValue);
        RuleFor(x => x.PriorityLevelId).GreaterThan(0).When(x => x.PriorityLevelId.HasValue);
    }
}
