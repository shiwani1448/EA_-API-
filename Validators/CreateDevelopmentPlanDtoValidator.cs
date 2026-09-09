using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Dtos.DevelopmentPlan;

namespace Jarvis5.Validators;

public class CreateDevelopmentPlanDtoValidator : AbstractValidator<CreateDevelopmentPlanDto>
{
    public CreateDevelopmentPlanDtoValidator()
    {
        RuleFor(d => d.Modules)
            .NotEmpty().WithMessage("At least one module is required.");

        RuleForEach(d => d.Modules).SetValidator(new CreateModuleDtoValidator());
    }
}

public class CreateModuleDtoValidator : AbstractValidator<CreateModuleDto>
{
    public CreateModuleDtoValidator()
    {
        RuleFor(m => m.Module)
            .NotEmpty().WithMessage("Module name is required.")
            .MaximumLength(300).WithMessage("Module name must not exceed 300 characters.");

        RuleFor(m => m.DoerLead)
            .NotEmpty().WithMessage("A project lead is required.");

        RuleFor(m => m.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .Must(p => SCIHPriority.Allowed.Contains(p.Trim().ToUpperInvariant()))
            .WithMessage($"Priority must be one of: {string.Join(", ", SCIHPriority.Allowed)}.");

        RuleFor(m => m.OverallEndDate)
            .GreaterThanOrEqualTo(m => m.OverallStartDate)
            .WithMessage("Overall end date must not be before the overall start date.");
    }
}
