using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateFollowupRequestDtoValidator : AbstractValidator<CreateFollowupRequestDto>
{
    public CreateFollowupRequestDtoValidator()
    {
        RuleFor(x => x.IntakeRequestId).GreaterThan(0).When(x => x.IntakeRequestId.HasValue);
        RuleFor(x => x.BusinessModuleId).GreaterThan(0).When(x => x.BusinessModuleId.HasValue);
        RuleFor(x => x.BusinessRecordId).MaximumLength(200).When(x => x.BusinessRecordId != null);
        RuleFor(x => x).Must(x => x.BusinessModuleId.HasValue == (x.BusinessRecordId != null))
            .WithMessage("BusinessModuleId and BusinessRecordId must be provided together.");
        RuleFor(x => x.WorkflowInstanceId).GreaterThan(0).When(x => x.WorkflowInstanceId.HasValue);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Remark).MaximumLength(2000);
        RuleFor(x => x.Subject).MaximumLength(500);
        RuleFor(x => x.Type).MaximumLength(100);
        RuleFor(x => x.DoerId).MaximumLength(100);
        RuleFor(x => x.DoerName).MaximumLength(200);
        RuleFor(x => x.PriorityLevelId).GreaterThan(0).When(x => x.PriorityLevelId.HasValue);
        RuleFor(x => x.ResponseOwnerId).MaximumLength(100);
        RuleFor(x => x.ResponseOwnerName).MaximumLength(200);
        RuleFor(x => x.ExpectedResponseAt).GreaterThanOrEqualTo(DateTime.MinValue).When(x => x.ExpectedResponseAt.HasValue);
        RuleFor(x => x.ReminderAt).LessThanOrEqualTo(x => x.DueAt).When(x => x.ReminderAt.HasValue && x.DueAt != default);
        RuleFor(x => x.ReminderRecipientUserId).GreaterThan(0).When(x => x.ReminderRecipientUserId.HasValue);
        RuleFor(x => x.ReminderRecipientEmployeeId).MaximumLength(100);
        RuleFor(x => x.ReminderRecipientName).MaximumLength(200);
        RuleFor(x => x.ReminderWhatsAppNumber).MaximumLength(50);
        RuleFor(x => x.ReminderRecipientEmail).MaximumLength(300);
        RuleFor(x => x.WaitingOnId).MaximumLength(100);
        RuleFor(x => x.WaitingOnName).MaximumLength(200);
        RuleFor(x => x.WaitingOnExternal).MaximumLength(200);
        RuleFor(x => x.SequenceNumber).GreaterThan(0).When(x => x.SequenceNumber.HasValue);
    }
}
