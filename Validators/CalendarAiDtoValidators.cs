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
        RuleFor(x => x.StartDateTime).Must(value => value.HasValue && value.Value != default)
            .WithMessage("StartDateTime must be provided.");
        RuleFor(x => x.EndDateTime).Must((dto, end) => !end.HasValue || !dto.StartDateTime.HasValue || end.Value >= dto.StartDateTime.Value)
            .WithMessage("EndDateTime must be on or after StartDateTime.");
        RuleFor(x => x.Location).MaximumLength(500).WithMessage("Location must not exceed 500 characters.");
        RuleFor(x => x.Description).MaximumLength(4000).WithMessage("Description must not exceed 4000 characters.");
        RuleFor(x => x.OrganizerEmployeeId).MaximumLength(100).WithMessage("OrganizerEmployeeId must not exceed 100 characters.");
        RuleFor(x => x.OrganizerName).MaximumLength(200).WithMessage("OrganizerName must not exceed 200 characters.");
        RuleFor(x => x.Notes).MaximumLength(4000).WithMessage("Notes must not exceed 4000 characters.");
        RuleFor(x => x.EventType).MaximumLength(50).WithMessage("EventType must not exceed 50 characters.");
        RuleFor(x => x.EventType).NotEmpty().WithMessage("EventType is required and must be one of: ClientMeeting, InternalMeeting, Personal, Travel.");
    }
}

public class CalendarConflictCheckRequestDtoValidator : AbstractValidator<CalendarConflictCheckRequestDto>
{
    public CalendarConflictCheckRequestDtoValidator()
    {
        RuleFor(x => x.From).NotEqual(default(DateTime)).WithMessage("From must be provided.");
        RuleFor(x => x.To).NotEqual(default(DateTime)).WithMessage("To must be provided.");
        RuleFor(x => x).Must(x => x.From <= x.To).WithMessage("From must be on or before To.");
    }
}
