using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class MeetingActionDtoValidator : AbstractValidator<CreateMeetingActionDto>
{
    public MeetingActionDtoValidator()
    {
        // Frontend-owned business fields — optional; only DB column length is guarded.
        RuleFor(x => x.Title).MaximumLength(500);
        RuleFor(x => x.Priority).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.DoerId).MaximumLength(100);
        RuleFor(x => x.DoerName).MaximumLength(200);
    }
}
