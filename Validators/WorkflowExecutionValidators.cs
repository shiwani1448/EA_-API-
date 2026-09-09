using FluentValidation;
using Jarvis5.Dtos.EaFms;
namespace Jarvis5.Validators;

public class StartWorkRequestDtoValidator : AbstractValidator<StartWorkRequestDto>
{
    public StartWorkRequestDtoValidator() => RuleFor(x => x.Notes).MaximumLength(2000);
}
public class CreateWaitingRequestDtoValidator : AbstractValidator<CreateWaitingRequestDto>
{
    public CreateWaitingRequestDtoValidator()
    {
        Include(new CreateWorkPauseRequestDtoValidator());
        RuleFor(x => x.Reason).NotEmpty();
        RuleFor(x => x.ExpectedResponseAt).NotNull();
        RuleFor(x => x.NextFollowupAt).NotNull();
        RuleFor(x => x.NextFollowupAt).Must(x => !x.HasValue || x.Value.Kind == DateTimeKind.Utc)
            .WithMessage("NextFollowupAt must be UTC.");
        RuleFor(x => x.CurrentRemarks).MaximumLength(2000);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.ResponseOwnerId)
            || !string.IsNullOrWhiteSpace(x.ResponseOwnerName))
            .WithMessage("A response owner is required.");
        RuleFor(x => x.ExpectedResponseAt).GreaterThanOrEqualTo(x => x.RequestSentAt)
            .When(x => x.ExpectedResponseAt.HasValue && x.RequestSentAt.HasValue);
        RuleFor(x => x.NextFollowupAt).GreaterThanOrEqualTo(x => x.RequestSentAt)
            .When(x => x.NextFollowupAt.HasValue && x.RequestSentAt.HasValue);
    }
}
public class CompleteWorkRequestDtoValidator : AbstractValidator<CompleteWorkRequestDto>
{
    public CompleteWorkRequestDtoValidator()
    {
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.EvidenceAttachmentIds).NotNull();
        RuleForEach(x => x.EvidenceAttachmentIds).GreaterThan(0);
    }
}
