using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Dtos.SnagList;

namespace Jarvis5.Validators;

public class CreateSnagListDtoValidator : AbstractValidator<CreateSnagListDto>
{
    public CreateSnagListDtoValidator()
    {
        RuleFor(d => d.RequestId)
            .GreaterThan(0).WithMessage("RequestId must be a positive number when given.")
            .When(d => d.RequestId.HasValue);

        RuleFor(d => d.TaskId)
            .GreaterThan(0).WithMessage("TaskId must be a positive number when given.")
            .When(d => d.TaskId.HasValue);

        RuleFor(d => d.Module)
            .NotEmpty().WithMessage("Module name is required.")
            .MaximumLength(300).WithMessage("Module name must not exceed 300 characters.");

        RuleFor(d => d.SnagDescription)
            .NotEmpty().WithMessage("Snag description is required.");

        RuleFor(d => d.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .Must(p => SCIHPriority.Allowed.Contains(p.Trim().ToUpperInvariant()))
            .WithMessage($"Priority must be one of: {string.Join(", ", SCIHPriority.Allowed)}.");

        RuleFor(d => d.StageDetails)
            .NotEmpty().WithMessage("At least one stage is required.");
    }
}
