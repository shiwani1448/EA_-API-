using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class UpdateMeetingRequestDtoValidator : AbstractValidator<UpdateMeetingRequestDto>
{
    public UpdateMeetingRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Purpose).MaximumLength(2000);
        RuleFor(x => x.OrganizerId).MaximumLength(100);
        RuleFor(x => x.OrganizerName).MaximumLength(200);
        RuleFor(x => x.MeetingLink).MaximumLength(1000);
    }
}
