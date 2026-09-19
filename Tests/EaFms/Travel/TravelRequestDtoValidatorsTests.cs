using Jarvis5.Dtos.EaFms;
using Jarvis5.Validators;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

/// <summary>
/// EA-wide frontend-owned-requiredness cleanup: NumberOfTravellers/NumberOfRooms were
/// wrongly rejecting a legitimately-supplied 0 ("must be greater than 0 when supplied").
/// Zero is a valid frontend-supplied count; only a physically-impossible negative count
/// is an integrity violation. These tests exercise the validators directly — the same
/// ones FluentValidation's ASP.NET Core auto-validation applies to
/// POST/PUT /api/ea/travel/requests before the request ever reaches the service.
/// </summary>
public class TravelRequestDtoValidatorsTests
{
    private static CreateTravelRequestDto MinimalCreateDto(int? numberOfTravellers = null, int? numberOfRooms = null) => new()
    {
        Travellers = new List<TravelTravellerDto> { new() { TravellerName = "Test Traveller" } },
        NumberOfTravellers = numberOfTravellers,
        NumberOfRooms = numberOfRooms
    };

    private static UpdateTravelDraftDto MinimalUpdateDto(int? numberOfTravellers = null, int? numberOfRooms = null) => new()
    {
        Travellers = new List<TravelTravellerDto> { new() { TravellerName = "Test Traveller" } },
        NumberOfTravellers = numberOfTravellers,
        NumberOfRooms = numberOfRooms
    };

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void Create_NumberOfTravellers_NullZeroPositiveAccepted_NegativeRejected(int? value, bool expectedValid)
    {
        var result = new CreateTravelRequestDtoValidator().Validate(MinimalCreateDto(numberOfTravellers: value));
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void Create_NumberOfRooms_NullZeroPositiveAccepted_NegativeRejected(int? value, bool expectedValid)
    {
        var result = new CreateTravelRequestDtoValidator().Validate(MinimalCreateDto(numberOfRooms: value));
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void Update_NumberOfTravellers_NullZeroPositiveAccepted_NegativeRejected(int? value, bool expectedValid)
    {
        var result = new UpdateTravelDraftDtoValidator().Validate(MinimalUpdateDto(numberOfTravellers: value));
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void Update_NumberOfRooms_NullZeroPositiveAccepted_NegativeRejected(int? value, bool expectedValid)
    {
        var result = new UpdateTravelDraftDtoValidator().Validate(MinimalUpdateDto(numberOfRooms: value));
        Assert.Equal(expectedValid, result.IsValid);
    }

    // ----------------------------------------------------------------
    // Regression: everything that was already correct must stay correct.
    // ----------------------------------------------------------------

    [Fact]
    public void Create_NegativeNumberOfTravellers_ErrorMessage_NoLongerClaimsGreaterThanZero()
    {
        var result = new CreateTravelRequestDtoValidator().Validate(MinimalCreateDto(numberOfTravellers: -1));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains(">= 0"));
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage.Contains("greater than 0"));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    public void Create_NumberOfGuests_StillNonNegative_Unchanged(int value, bool expectedValid)
    {
        var dto = MinimalCreateDto();
        dto.NumberOfGuests = value;
        var result = new CreateTravelRequestDtoValidator().Validate(dto);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(-0.01, false)]
    [InlineData(0, true)]
    [InlineData(100.50, true)]
    public void Create_EstimatedTravelCost_StillNonNegative_Unchanged(decimal value, bool expectedValid)
    {
        var dto = MinimalCreateDto();
        dto.EstimatedTravelCost = value;
        var result = new CreateTravelRequestDtoValidator().Validate(dto);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Create_ReturnDateBeforeDepartureDate_StillRejected_DateIntegrityUnchanged()
    {
        var dto = MinimalCreateDto();
        dto.DepartureDate = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        dto.ReturnDate = new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc);
        var result = new CreateTravelRequestDtoValidator().Validate(dto);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_PurposeOver1000Chars_StillRejected_MaxLengthUnchanged()
    {
        var dto = MinimalCreateDto();
        dto.Purpose = new string('x', 1001);
        var result = new CreateTravelRequestDtoValidator().Validate(dto);
        Assert.False(result.IsValid);
    }
}
