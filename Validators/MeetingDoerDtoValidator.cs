using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class MeetingDoerDtoValidator : AbstractValidator<MeetingDoerDto>
{
    public MeetingDoerDtoValidator()
    {
        RuleFor(x => x.DoerId).Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("DoerId is required.").MaximumLength(100);
        RuleFor(x => x.DoerName).Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("DoerName is required.").MaximumLength(200);
    }
}
