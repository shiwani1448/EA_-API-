using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class MeetingDecisionDtoValidator : AbstractValidator<CreateMeetingDecisionDto>
{
    public MeetingDecisionDtoValidator()
    {
        RuleFor(x => x.Decision).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.OwnerName).MaximumLength(200);
    }
}
