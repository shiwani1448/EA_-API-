using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class UpdateMeetingMinutesDtoValidator : AbstractValidator<UpdateMeetingMinutesDto>
{
    public UpdateMeetingMinutesDtoValidator()
    {
        RuleFor(x => x.Summary).MaximumLength(4000);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.PreparedByName).MaximumLength(200);
    }
}
