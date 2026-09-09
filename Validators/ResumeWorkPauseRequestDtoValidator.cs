using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class ResumeWorkPauseRequestDtoValidator : AbstractValidator<ResumeWorkPauseRequestDto>
{
    public ResumeWorkPauseRequestDtoValidator()
    {
        RuleFor(x => x.ResumedReason).MaximumLength(2000);
        RuleFor(x => x.TargetStatusName)
            .NotEmpty()
            .WithMessage("TargetStatusName is required.")
            .Must(IsAllowedResumeTarget)
            .WithMessage("TargetStatusName must be 'In Progress' or 'Submitted'.");
    }

    private static bool IsAllowedResumeTarget(string? name) =>
        string.Equals(name?.Trim(), "In Progress", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name?.Trim(), "Submitted", StringComparison.OrdinalIgnoreCase);
}
