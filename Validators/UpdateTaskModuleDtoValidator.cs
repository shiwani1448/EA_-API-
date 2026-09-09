using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Dtos.DevelopmentPlan;

namespace Jarvis5.Validators;

public class UpdateTaskModuleDtoValidator : AbstractValidator<UpdateTaskModuleDto>
{
    public UpdateTaskModuleDtoValidator()
    {
        RuleFor(m => m.Module)
            .NotEmpty().WithMessage("Module name is required.")
            .MaximumLength(300).WithMessage("Module name must not exceed 300 characters.")
            .When(m => m.Module is not null);

        RuleFor(m => m.DoerLead)
            .NotEmpty().WithMessage("A project lead is required.")
            .When(m => m.DoerLead is not null);

        RuleFor(m => m.Priority)
            .Must(p => SCIHPriority.Allowed.Contains(p!.Trim().ToUpperInvariant()))
            .WithMessage($"Priority must be one of: {string.Join(", ", SCIHPriority.Allowed)}.")
            .When(m => m.Priority is not null);

        RuleFor(m => m.OverallEndDate)
            .GreaterThanOrEqualTo(m => m.OverallStartDate!.Value)
            .WithMessage("Overall end date must not be before the overall start date.")
            .When(m => m.OverallStartDate.HasValue && m.OverallEndDate.HasValue);
    }
}
