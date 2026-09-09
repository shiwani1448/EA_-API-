using FluentValidation;
using Jarvis5.Dtos.Approval;

namespace Jarvis5.Validators;

public class ApproveRequestDtoValidator : AbstractValidator<ApproveRequestDto>
{
    public ApproveRequestDtoValidator()
    {
        RuleFor(d => d.ApprovedBy)
            .GreaterThan(0)
            .WithMessage("ApprovedBy is required.");
    }
}
