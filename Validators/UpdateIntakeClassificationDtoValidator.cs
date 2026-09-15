using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class UpdateIntakeClassificationDtoValidator : AbstractValidator<UpdateIntakeClassificationDto>
{
    public UpdateIntakeClassificationDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.Details).MaximumLength(2000);
    }
}
