using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateMeetingRequestDtoValidator : AbstractValidator<CreateMeetingRequestDto>
{
    public CreateMeetingRequestDtoValidator()
    {
        // Frontend-owned form requiredness — Meeting.Title is nullable at the DB/entity
        // level, so backend only guards the technical DB column length, not presence.
        RuleFor(x => x.Title).MaximumLength(500);
        RuleFor(x => x.Priority).MaximumLength(100);
        RuleFor(x => x.StatusId).GreaterThan(0).When(x => x.StatusId.HasValue);
        RuleFor(x => x.IntakeRequestId).GreaterThan(0).When(x => x.IntakeRequestId.HasValue);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Purpose).MaximumLength(2000);
        RuleFor(x => x.OrganizerId).MaximumLength(100);
        RuleFor(x => x.OrganizerName).MaximumLength(200);
        RuleFor(x => x.MeetingLink).MaximumLength(1000);
        RuleForEach(x => x.Doers).SetValidator(new MeetingDoerDtoValidator()).When(x => x.Doers is not null);
    }
}
