using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CompleteFollowupRequestDtoValidator : AbstractValidator<CompleteFollowupRequestDto>
{
    public CompleteFollowupRequestDtoValidator()
    {
        RuleFor(x => x.CompletionNote).MaximumLength(2000);
        RuleFor(x => x.OutcomeCode).MaximumLength(100);
        RuleFor(x => x.CompletedAt).Empty(); // clients should not supply CompletedAt (server authoritative)
        // CompletedAt optional but server authoritative will set CompletedAt/CompletedBy
    }
}
