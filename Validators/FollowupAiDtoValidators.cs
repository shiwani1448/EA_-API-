using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class SendFollowupReminderRequestDtoValidator : AbstractValidator<SendFollowupReminderRequestDto>
{
    public SendFollowupReminderRequestDtoValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Subject must not be empty.").MaximumLength(500);
        RuleFor(x => x.Body).NotEmpty().WithMessage("Body must not be empty.");
    }
}

public class ApplySuggestedEscalationRequestDtoValidator : AbstractValidator<ApplySuggestedEscalationRequestDto>
{
    public ApplySuggestedEscalationRequestDtoValidator()
    {
        RuleFor(x => x.EscalationLevelId).GreaterThan(0).WithMessage("EscalationLevelId must be provided.");
    }
}
