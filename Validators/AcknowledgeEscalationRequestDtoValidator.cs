using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class AcknowledgeEscalationRequestDtoValidator : AbstractValidator<AcknowledgeEscalationRequestDto>
{
    public AcknowledgeEscalationRequestDtoValidator()
    {
        RuleFor(x => x.AcknowledgementNote).MaximumLength(2000);
    }
}
