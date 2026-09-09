using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Dtos.SnagList;

namespace Jarvis5.Validators;

public class UpdateSnagListDtoValidator : AbstractValidator<UpdateSnagListDto>
{
    public UpdateSnagListDtoValidator()
    {
        RuleFor(d => d.Module)
            .NotEmpty().WithMessage("Module name is required.")
            .MaximumLength(300).WithMessage("Module name must not exceed 300 characters.")
            .When(d => d.Module is not null);

        RuleFor(d => d.SnagDescription)
            .NotEmpty().WithMessage("Snag description is required.")
            .When(d => d.SnagDescription is not null);

        RuleFor(d => d.Priority)
            .Must(p => SCIHPriority.Allowed.Contains(p!.Trim().ToUpperInvariant()))
            .WithMessage($"Priority must be one of: {string.Join(", ", SCIHPriority.Allowed)}.")
            .When(d => d.Priority is not null);

        RuleFor(d => d.CurrentStatus)
            .Must(s => SCIHSnagStatus.Allowed.Contains(s) && s != SCIHSnagStatus.Closed)
            .WithMessage($"CurrentStatus must be one of: {string.Join(", ", SCIHSnagStatus.Allowed.Where(s => s != SCIHSnagStatus.Closed))}. Use POST /api/snaglist/{{id}}/close to close a Snag List.")
            .When(d => d.CurrentStatus is not null);
    }
}
