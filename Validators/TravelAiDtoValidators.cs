using FluentValidation;
using Jarvis5.Common.EaFms;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

/// <summary>Same field-level guards TravelBookingService.Validate/IsType already enforce
/// for the manual bookings endpoint — /ai/options/confirm and /ai/options/compare accept
/// the same CreateTravelBookingDto shape but previously had no FluentValidation validator
/// of their own (caught only as a BusinessRuleException/BadRequestException deep inside the
/// service). This gives the same bad input a 400 at the API boundary instead.</summary>
public class CreateTravelBookingDtoValidator : AbstractValidator<CreateTravelBookingDto>
{
    public CreateTravelBookingDtoValidator()
    {
        RuleFor(x => x.BookingType).Must(TravelBookingRules.IsType).WithMessage("Unsupported bookingType.");
        RuleFor(x => x.BookingStatus).Must(s => s is null || TravelBookingRules.IsStatus(s)).WithMessage("Unsupported bookingStatus.");
        RuleFor(x => x.Cost)
            .Must(c => !c.HasValue || (c.Value >= 0 && c.Value <= 9999999999999999.99m && decimal.Round(c.Value, 2) == c.Value))
            .WithMessage("Cost must be nonnegative, fit numeric(18,2), and have at most two decimal places.");
        RuleFor(x => x.Provider).MaximumLength(200);
        RuleFor(x => x.BookingReference).MaximumLength(200);
        RuleFor(x => x.Currency).MaximumLength(10);
    }
}

public class ConfirmTravelAiOptionsRequestDtoValidator : AbstractValidator<ConfirmTravelAiOptionsRequestDto>
{
    public ConfirmTravelAiOptionsRequestDtoValidator()
    {
        RuleForEach(x => x.Options).SetValidator(new CreateTravelBookingDtoValidator());
    }
}

public class TravelAiCompareOptionsRequestDtoValidator : AbstractValidator<TravelAiCompareOptionsRequestDto>
{
    public TravelAiCompareOptionsRequestDtoValidator()
    {
        RuleForEach(x => x.Options).SetValidator(new CreateTravelBookingDtoValidator());
    }
}
