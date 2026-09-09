using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Dtos.DevelopmentPlan;

namespace Jarvis5.Validators;

public class UpdateStageDtoValidator : AbstractValidator<UpdateStageDto>
{
    public UpdateStageDtoValidator()
    {
        RuleFor(s => s.Status)
            .NotEmpty().WithMessage("Stage status is required.")
            .Must(status => SCIHTaskStatus.Allowed.Contains(status))
            .WithMessage($"Stage status must be one of: {string.Join(", ", SCIHTaskStatus.Allowed)}.");

        RuleFor(s => s.PlannedEnd)
            .GreaterThanOrEqualTo(s => s.PlannedStart)
            .When(s => s.PlannedStart.HasValue && s.PlannedEnd.HasValue)
            .WithMessage("Planned end date must not be before the planned start date.");

        RuleFor(s => s.ActualEnd)
            .GreaterThanOrEqualTo(s => s.ActualStart)
            .When(s => s.ActualStart.HasValue && s.ActualEnd.HasValue)
            .WithMessage("Actual end date must not be before the actual start date.");

        RuleFor(s => s.RrrCount)
            .GreaterThanOrEqualTo(0).WithMessage("RRR count cannot be negative.");
    }
}
