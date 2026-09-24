using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class QuickAddCalendarEventRequestDtoValidator : AbstractValidator<QuickAddCalendarEventRequestDto>
{
    public QuickAddCalendarEventRequestDtoValidator()
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Text must not be empty.").MaximumLength(2000);
    }
}

public class ApplyQuickAddSuggestionRequestDtoValidator : AbstractValidator<ApplyQuickAddSuggestionRequestDto>
{
    public ApplyQuickAddSuggestionRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title must not be empty.").MaximumLength(500);
        RuleFor(x => x.StartDateTime).NotNull().WithMessage("StartDateTime must be provided.");
        RuleFor(x => x.EventType).NotEmpty().WithMessage("EventType is required and must be one of: ClientMeeting, InternalMeeting, Personal, Travel.");
    }
}

public class CalendarConflictCheckRequestDtoValidator : AbstractValidator<CalendarConflictCheckRequestDto>
{
    public CalendarConflictCheckRequestDtoValidator()
    {
        RuleFor(x => x).Must(x => x.From <= x.To).WithMessage("From must be on or before To.");
    }
}
