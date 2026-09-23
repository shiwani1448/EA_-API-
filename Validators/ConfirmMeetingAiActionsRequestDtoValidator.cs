using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

/// <summary>Reuses the existing MeetingActionDtoValidator per submitted action — the same
/// column-length guards the manual create endpoint already enforces, nothing new.</summary>
public class ConfirmMeetingAiActionsRequestDtoValidator : AbstractValidator<ConfirmMeetingAiActionsRequestDto>
{
    public ConfirmMeetingAiActionsRequestDtoValidator()
    {
        RuleForEach(x => x.Actions).SetValidator(new MeetingActionDtoValidator());
    }
}
