using FluentValidation;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Validators;

/// <summary>
/// Technical/data-integrity validation for creating a Travel Draft.
/// DO NOT add frontend-mandatory field checks here.
/// DO NOT add submission-completeness rules here.
/// </summary>
public class CreateTravelRequestDtoValidator : AbstractValidator<CreateTravelRequestDto>
{
    public CreateTravelRequestDtoValidator()
    {
        // ---- String length guards (db limits) ----
        RuleFor(x => x.TravellerName).MaximumLength(200);
        RuleFor(x => x.EmployeePersonId).MaximumLength(100);
        RuleFor(x => x.Department).MaximumLength(200);
        RuleFor(x => x.ContactInformation).MaximumLength(500);
        RuleFor(x => x.Purpose).MaximumLength(1000);
        RuleFor(x => x.TravelType).MaximumLength(100);
        RuleFor(x => x.FromLocation).MaximumLength(500);
        RuleFor(x => x.ToLocation).MaximumLength(500);
        RuleFor(x => x.Priority).MaximumLength(100);
        RuleFor(x => x.SpecialRequirements).MaximumLength(4000);
        RuleFor(x => x.TransportType).MaximumLength(100);
        RuleFor(x => x.ClassPreference).MaximumLength(200);
        RuleFor(x => x.BookingRequirements).MaximumLength(4000);
        RuleFor(x => x.Hotel).MaximumLength(500);
        RuleFor(x => x.RoomPreference).MaximumLength(500);
        RuleFor(x => x.LocationPreference).MaximumLength(500);
        RuleFor(x => x.PickupLocation).MaximumLength(500);
        RuleFor(x => x.DropLocation).MaximumLength(500);
        RuleFor(x => x.VehiclePreference).MaximumLength(500);
        RuleFor(x => x.ClientGuestDetails).MaximumLength(4000);
        RuleFor(x => x.HospitalityRequirement).MaximumLength(4000);
        RuleFor(x => x.MeetingEventPurpose).MaximumLength(1000);
        RuleFor(x => x.SpecialArrangements).MaximumLength(4000);
        RuleFor(x => x.ItineraryNotes).MaximumLength(4000);
        RuleFor(x => x.AdditionalInstructions).MaximumLength(4000);
        RuleFor(x => x.Currency).MaximumLength(10);
        RuleFor(x => x.ApproverId).MaximumLength(100);

        // ---- Date relationship rules (only when both dates are supplied) ----
        When(x => x.DepartureDate.HasValue && x.ReturnDate.HasValue, () =>
        {
            RuleFor(x => x.ReturnDate)
                .GreaterThanOrEqualTo(x => x.DepartureDate)
                .WithMessage("ReturnDate must be on or after DepartureDate.");
        });

        When(x => x.CheckInDate.HasValue && x.CheckOutDate.HasValue, () =>
        {
            RuleFor(x => x.CheckOutDate)
                .GreaterThanOrEqualTo(x => x.CheckInDate)
                .WithMessage("CheckOutDate must be on or after CheckInDate.");
        });

        // ---- Non-negative count rules (only when supplied). Zero is a legitimate
        // supplied value (e.g. no rooms needed yet) and must not be rejected — only a
        // physically-impossible negative count is a real integrity violation. ----
        When(x => x.NumberOfTravellers.HasValue, () =>
        {
            RuleFor(x => x.NumberOfTravellers)
                .GreaterThanOrEqualTo(0)
                .WithMessage("NumberOfTravellers must be >= 0 when supplied.");
        });

        When(x => x.NumberOfRooms.HasValue, () =>
        {
            RuleFor(x => x.NumberOfRooms)
                .GreaterThanOrEqualTo(0)
                .WithMessage("NumberOfRooms must be >= 0 when supplied.");
        });

        When(x => x.NumberOfGuests.HasValue, () =>
        {
            RuleFor(x => x.NumberOfGuests)
                .GreaterThanOrEqualTo(0)
                .WithMessage("NumberOfGuests must be >= 0 when supplied.");
        });

        // ---- Non-negative monetary values (only when supplied) ----
        When(x => x.EstimatedTravelCost.HasValue, () =>
        {
            RuleFor(x => x.EstimatedTravelCost)
                .GreaterThanOrEqualTo(0)
                .WithMessage("EstimatedTravelCost must be >= 0 when supplied.");
        });

        When(x => x.EstimatedHotelCost.HasValue, () =>
        {
            RuleFor(x => x.EstimatedHotelCost)
                .GreaterThanOrEqualTo(0)
                .WithMessage("EstimatedHotelCost must be >= 0 when supplied.");
        });

        When(x => x.EstimatedLocalTransportCost.HasValue, () =>
        {
            RuleFor(x => x.EstimatedLocalTransportCost)
                .GreaterThanOrEqualTo(0)
                .WithMessage("EstimatedLocalTransportCost must be >= 0 when supplied.");
        });

        When(x => x.EstimatedHospitalityCost.HasValue, () =>
        {
            RuleFor(x => x.EstimatedHospitalityCost)
                .GreaterThanOrEqualTo(0)
                .WithMessage("EstimatedHospitalityCost must be >= 0 when supplied.");
        });
    }
}

/// <summary>
/// Technical/data-integrity validation for updating a Travel Draft.
/// Same rules as create; submission completeness belongs to the Submit action.
/// </summary>
public class UpdateTravelDraftDtoValidator : AbstractValidator<UpdateTravelDraftDto>
{
    public UpdateTravelDraftDtoValidator()
    {
        // ---- String length guards (db limits) ----
        RuleFor(x => x.TravellerName).MaximumLength(200);
        RuleFor(x => x.EmployeePersonId).MaximumLength(100);
        RuleFor(x => x.Department).MaximumLength(200);
        RuleFor(x => x.ContactInformation).MaximumLength(500);
        RuleFor(x => x.Purpose).MaximumLength(1000);
        RuleFor(x => x.TravelType).MaximumLength(100);
        RuleFor(x => x.FromLocation).MaximumLength(500);
        RuleFor(x => x.ToLocation).MaximumLength(500);
        RuleFor(x => x.Priority).MaximumLength(100);
        RuleFor(x => x.SpecialRequirements).MaximumLength(4000);
        RuleFor(x => x.TransportType).MaximumLength(100);
        RuleFor(x => x.ClassPreference).MaximumLength(200);
        RuleFor(x => x.BookingRequirements).MaximumLength(4000);
        RuleFor(x => x.Hotel).MaximumLength(500);
        RuleFor(x => x.RoomPreference).MaximumLength(500);
        RuleFor(x => x.LocationPreference).MaximumLength(500);
        RuleFor(x => x.PickupLocation).MaximumLength(500);
        RuleFor(x => x.DropLocation).MaximumLength(500);
        RuleFor(x => x.VehiclePreference).MaximumLength(500);
        RuleFor(x => x.ClientGuestDetails).MaximumLength(4000);
        RuleFor(x => x.HospitalityRequirement).MaximumLength(4000);
        RuleFor(x => x.MeetingEventPurpose).MaximumLength(1000);
        RuleFor(x => x.SpecialArrangements).MaximumLength(4000);
        RuleFor(x => x.ItineraryNotes).MaximumLength(4000);
        RuleFor(x => x.AdditionalInstructions).MaximumLength(4000);
        RuleFor(x => x.Currency).MaximumLength(10);
        RuleFor(x => x.ApproverId).MaximumLength(100);

        // ---- Date relationship rules (only when both dates are supplied) ----
        When(x => x.DepartureDate.HasValue && x.ReturnDate.HasValue, () =>
        {
            RuleFor(x => x.ReturnDate)
                .GreaterThanOrEqualTo(x => x.DepartureDate)
                .WithMessage("ReturnDate must be on or after DepartureDate.");
        });

        When(x => x.CheckInDate.HasValue && x.CheckOutDate.HasValue, () =>
        {
            RuleFor(x => x.CheckOutDate)
                .GreaterThanOrEqualTo(x => x.CheckInDate)
                .WithMessage("CheckOutDate must be on or after CheckInDate.");
        });

        // ---- Non-negative count rules (only when supplied). Zero is a legitimate
        // supplied value; only a physically-impossible negative count is invalid. ----
        When(x => x.NumberOfTravellers.HasValue, () =>
        {
            RuleFor(x => x.NumberOfTravellers)
                .GreaterThanOrEqualTo(0)
                .WithMessage("NumberOfTravellers must be >= 0 when supplied.");
        });

        When(x => x.NumberOfRooms.HasValue, () =>
        {
            RuleFor(x => x.NumberOfRooms)
                .GreaterThanOrEqualTo(0)
                .WithMessage("NumberOfRooms must be >= 0 when supplied.");
        });

        When(x => x.NumberOfGuests.HasValue, () =>
        {
            RuleFor(x => x.NumberOfGuests)
                .GreaterThanOrEqualTo(0)
                .WithMessage("NumberOfGuests must be >= 0 when supplied.");
        });

        // ---- Non-negative monetary values (only when supplied) ----
        When(x => x.EstimatedTravelCost.HasValue, () =>
        {
            RuleFor(x => x.EstimatedTravelCost)
                .GreaterThanOrEqualTo(0)
                .WithMessage("EstimatedTravelCost must be >= 0 when supplied.");
        });

        When(x => x.EstimatedHotelCost.HasValue, () =>
        {
            RuleFor(x => x.EstimatedHotelCost)
                .GreaterThanOrEqualTo(0)
                .WithMessage("EstimatedHotelCost must be >= 0 when supplied.");
        });

        When(x => x.EstimatedLocalTransportCost.HasValue, () =>
        {
            RuleFor(x => x.EstimatedLocalTransportCost)
                .GreaterThanOrEqualTo(0)
                .WithMessage("EstimatedLocalTransportCost must be >= 0 when supplied.");
        });

        When(x => x.EstimatedHospitalityCost.HasValue, () =>
        {
            RuleFor(x => x.EstimatedHospitalityCost)
                .GreaterThanOrEqualTo(0)
                .WithMessage("EstimatedHospitalityCost must be >= 0 when supplied.");
        });
    }
}
