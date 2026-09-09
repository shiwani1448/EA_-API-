using FluentValidation;
using Jarvis5.Dtos.Approval;

namespace Jarvis5.Validators;

public class RejectRequestDtoValidator : AbstractValidator<RejectRequestDto>
{
    public RejectRequestDtoValidator()
    {
        RuleFor(d => d.ApprovedBy)
            .GreaterThan(0)
            .WithMessage("ApprovedBy is required.");

        RuleFor(d => d.RejectionReason)
            .NotEmpty()
            .WithMessage("Rejection reason is required.");

        RuleFor(d => d.Comments)
            .NotEmpty()
            .WithMessage("Comments are required.");

        RuleFor(d => d.ImprovementAreas)
            .NotEmpty()
            .WithMessage("At least one improvement area is required.");
    }
}
