using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class ResolveEscalationRequestDtoValidator : AbstractValidator<ResolveEscalationRequestDto>
{
    public ResolveEscalationRequestDtoValidator()
    {
        RuleFor(x => x.ResolutionNote).MaximumLength(2000);
    }
}
