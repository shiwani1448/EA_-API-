using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateAssignmentRequestDtoValidator : AbstractValidator<CreateAssignmentRequestDto>
{
    public CreateAssignmentRequestDtoValidator()
    {
        RuleFor(x => x.DoerId).MaximumLength(100);
        RuleFor(x => x.DoerName).MaximumLength(200);
        RuleFor(x => x.AssignmentType).MaximumLength(100);
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}
