using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class CreateWorkPauseRequestDtoValidator : AbstractValidator<CreateWorkPauseRequestDto>
{
    public CreateWorkPauseRequestDtoValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(2000);
        RuleFor(x => x.WaitingOnId).MaximumLength(100);
        RuleFor(x => x.WaitingOnName).MaximumLength(200);
        RuleFor(x => x.WaitingOnExternal).MaximumLength(200);
        RuleFor(x => x.ResponseOwnerId).MaximumLength(100);
        RuleFor(x => x.ResponseOwnerName).MaximumLength(200);
        RuleFor(x => x.ExpectedResponseAt).GreaterThanOrEqualTo(DateTime.MinValue).When(x => x.ExpectedResponseAt.HasValue);
        RuleFor(x => x.RequestSentAt).NotNull()
            .WithMessage("RequestSentAt is required when entering Waiting.");
        RuleFor(x => x.RequestSentAt).Must(x => !x.HasValue || x.Value.Kind == DateTimeKind.Utc)
            .WithMessage("RequestSentAt must be UTC.");
        RuleFor(x => x.ExpectedResponseAt).Must(x => !x.HasValue || x.Value.Kind == DateTimeKind.Utc)
            .WithMessage("ExpectedResponseAt must be UTC.");

        RuleFor(x => x)
            .Must(HasWaitingDependency)
            .WithMessage("Waiting requires who/what the work is waiting on (WaitingOnId, WaitingOnName, or WaitingOnExternal).");
    }

    private static bool HasWaitingDependency(CreateWorkPauseRequestDto x) =>
        !string.IsNullOrWhiteSpace(x.WaitingOnId)
        || !string.IsNullOrWhiteSpace(x.WaitingOnName)
        || !string.IsNullOrWhiteSpace(x.WaitingOnExternal);
}
