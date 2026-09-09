using FluentValidation;
using Jarvis5.Dtos;

namespace Jarvis5.Validators;

public class UpdateRequestOverallDatesDtoValidator : AbstractValidator<UpdateRequestOverallDatesDto>
{
    public UpdateRequestOverallDatesDtoValidator()
    {
        RuleFor(r => r)
            .Must(r => r.OverallStartDate.HasValue || r.OverallEndDate.HasValue)
            .WithMessage("At least one of OverallStartDate or OverallEndDate must be provided.");
    }
}
