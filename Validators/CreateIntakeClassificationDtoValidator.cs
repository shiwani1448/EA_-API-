using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateIntakeClassificationDtoValidator : AbstractValidator<CreateIntakeClassificationDto>
{
    public CreateIntakeClassificationDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Details).MaximumLength(2000);
    }
}
