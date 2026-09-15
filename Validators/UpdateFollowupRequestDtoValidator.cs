using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class UpdateFollowupRequestDtoValidator : AbstractValidator<UpdateFollowupRequestDto>
{
    public UpdateFollowupRequestDtoValidator()
    {
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Type).MaximumLength(100);
        RuleFor(x => x.AssignedToId).MaximumLength(100);
        RuleFor(x => x.AssignedToName).MaximumLength(200);
        RuleFor(x => x.PriorityLevelId).GreaterThan(0).When(x => x.PriorityLevelId.HasValue);
        RuleFor(x => x.ReminderAt).LessThanOrEqualTo(x => x.DueAt).When(x => x.ReminderAt.HasValue && x.DueAt != default);
        RuleFor(x => x.NextFollowupAt).GreaterThanOrEqualTo(x => x.DueAt).When(x => x.NextFollowupAt.HasValue && x.DueAt != default);
        RuleFor(x => x.WaitingOnId).MaximumLength(100);
        RuleFor(x => x.WaitingOnName).MaximumLength(200);
        RuleFor(x => x.WaitingOnExternal).MaximumLength(200);
        RuleFor(x => x.ResponseOwnerId).MaximumLength(100);
        RuleFor(x => x.ResponseOwnerName).MaximumLength(200);
        RuleFor(x => x.ExpectedResponseAt).GreaterThanOrEqualTo(DateTime.MinValue).When(x => x.ExpectedResponseAt.HasValue);
        RuleFor(x => x.SequenceNumber).GreaterThan(0).When(x => x.SequenceNumber.HasValue);
    }
}
