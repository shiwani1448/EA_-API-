using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class MeetingActionDtoValidator : AbstractValidator<CreateMeetingActionDto>
{
    public MeetingActionDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PriorityLevelId).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.OwnerName).MaximumLength(200);
    }
}
