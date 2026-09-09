using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class UpdateMeetingAgendaDtoValidator : AbstractValidator<UpdateMeetingAgendaDto>
{
    public UpdateMeetingAgendaDtoValidator()
    {
        RuleFor(x => x.OrderNo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Title).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.OwnerName).MaximumLength(200);
    }
}
