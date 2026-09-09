using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Dtos;

namespace Jarvis5.Validators;

public class CreateRequestDtoValidator : AbstractValidator<CreateRequestDto>
{
    public CreateRequestDtoValidator()
    {
        RuleFor(r => r.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(300).WithMessage("Title must not exceed 300 characters.");

        RuleFor(r => r.DepartmentId).NotEmpty().WithMessage("Department is required.");
        RuleFor(r => r.RaisedBy).NotEmpty().WithMessage("Raised By is required.");
        RuleFor(r => r.ExpectedBenefit).NotEmpty().WithMessage("Expected Benefit is required.");

        RuleFor(r => r.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .Must(p => SCIHPriority.Allowed.Contains(p.Trim().ToUpperInvariant()))
            .WithMessage($"Priority must be one of: {string.Join(", ", SCIHPriority.Allowed)}.");

        RuleFor(r => r.PainPoints)
            .NotEmpty().WithMessage("At least one pain point is required.");

        RuleForEach(r => r.PainPoints).SetValidator(new PainPointDtoValidator());
        RuleForEach(r => r.Attachments).SetValidator(new AttachmentDtoValidator());
    }
}
