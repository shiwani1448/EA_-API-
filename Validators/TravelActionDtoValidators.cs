using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

public class ApproveTravelRequestDtoValidator : AbstractValidator<ApproveTravelRequestDto>
{
    public ApproveTravelRequestDtoValidator()
    {
        RuleFor(x => x.ExpectedCycleNo).GreaterThan(0);
        RuleFor(x => x.DecisionComment).MaximumLength(4000);
    }
}

public class RejectTravelRequestDtoValidator : AbstractValidator<RejectTravelRequestDto>
{
    public RejectTravelRequestDtoValidator()
    {
        RuleFor(x => x.ExpectedCycleNo).GreaterThan(0);
        RuleFor(x => x.DecisionComment).MaximumLength(4000);
    }
}

public class RequestTravelChangesDtoValidator : AbstractValidator<RequestTravelChangesDto>
{
    public RequestTravelChangesDtoValidator()
    {
        RuleFor(x => x.ExpectedCycleNo).GreaterThan(0);
        RuleFor(x => x.ChangeReason).MaximumLength(4000);
    }
}

public class ResubmitTravelRequestDtoValidator : AbstractValidator<ResubmitTravelRequestDto>
{
    public ResubmitTravelRequestDtoValidator()
    {
        RuleFor(x => x.ExpectedCycleNo).GreaterThan(0);
        RuleFor(x => x.ChangesMade).MaximumLength(4000);
    }
}
