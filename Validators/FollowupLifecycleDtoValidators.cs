using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class LogFollowupReminderRequestDtoValidator : AbstractValidator<LogFollowupReminderRequestDto>
{
    public LogFollowupReminderRequestDtoValidator()
    {
        RuleFor(x => x.Channel).Must(c => c is "Email" or "WhatsApp").WithMessage("Channel must be Email or WhatsApp.");
        RuleFor(x => x.Recipient).NotEmpty().WithMessage("Recipient must not be empty.").MaximumLength(300);
        RuleFor(x => x.RecipientName).MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().WithMessage("Message must not be empty.");
    }
}

public class FollowupPauseRequestDtoValidator : AbstractValidator<FollowupPauseRequestDto>
{
    public FollowupPauseRequestDtoValidator()
    {
        RuleFor(x => x.PauseReason).MaximumLength(2000).WithMessage("pauseReason must be at most 2000 characters.");
    }
}
