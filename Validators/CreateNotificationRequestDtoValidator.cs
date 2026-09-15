using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateNotificationRequestDtoValidator : AbstractValidator<CreateNotificationRequestDto>
{
    public CreateNotificationRequestDtoValidator()
    {
        // Limits from migration/designer: RecipientId 100, RecipientName 200, Type 100, Title 500, Message 4000, ReferenceModule 200, ReferenceId 200, CreatedBy 100
        RuleFor(x => x.RecipientId).MaximumLength(100);
        RuleFor(x => x.RecipientName).MaximumLength(200);
        RuleFor(x => x.Type).MaximumLength(100);
        RuleFor(x => x.Title).MaximumLength(500);
        RuleFor(x => x.Message).MaximumLength(4000);
        RuleFor(x => x.ReferenceModule).MaximumLength(200);
        RuleFor(x => x.ReferenceId).MaximumLength(200);
    }
}
