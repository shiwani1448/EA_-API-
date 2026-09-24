using FluentValidation;
using Jarvis5.Common.EaFms;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateCalendarEventDtoValidator : AbstractValidator<CreateCalendarEventDto>
{
    public CreateCalendarEventDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title must not be empty.").MaximumLength(500);
        RuleFor(x => x.StartDateTime).NotNull().WithMessage("StartDateTime must be provided.");
        RuleFor(x => x.EventType).Must(CalendarEventType.IsValid).WithMessage("EventType is required and must be one of: ClientMeeting, InternalMeeting, Personal, Travel.");
    }
}

public class UpdateCalendarEventDtoValidator : AbstractValidator<UpdateCalendarEventDto>
{
    public UpdateCalendarEventDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title must not be empty.").MaximumLength(500);
        RuleFor(x => x.StartDateTime).NotNull().WithMessage("StartDateTime must be provided.");
        RuleFor(x => x.EventType).Must(CalendarEventType.IsValid).WithMessage("EventType is required and must be one of: ClientMeeting, InternalMeeting, Personal, Travel.");
    }
}
