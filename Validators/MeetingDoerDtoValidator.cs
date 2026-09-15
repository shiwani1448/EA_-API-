using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class MeetingDoerDtoValidator : AbstractValidator<MeetingDoerDto>
{
    public MeetingDoerDtoValidator()
    {
        // Frontend supplies doerId/doerName; only enforce safe storage length.
        RuleFor(x => x.DoerName).MaximumLength(200);
    }
}
