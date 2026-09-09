using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateMeetingAttendeeDtoValidator : AbstractValidator<CreateMeetingAttendeeDto>
{
    public CreateMeetingAttendeeDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(300);
        RuleFor(x => x.Role).MaximumLength(200);
    }
}
