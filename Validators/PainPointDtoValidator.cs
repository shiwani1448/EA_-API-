using FluentValidation;
using Jarvis5.Dtos;

namespace Jarvis5.Validators;

public class PainPointDtoValidator : AbstractValidator<PainPointDto>
{
    public PainPointDtoValidator()
    {
        RuleFor(p => p.Title).NotEmpty().WithMessage("Pain point title is required.");
        RuleFor(p => p.Severity).NotEmpty().WithMessage("Pain point severity is required.");
        RuleFor(p => p.Frequency).NotEmpty().WithMessage("Pain point frequency is required.");
    }
}
