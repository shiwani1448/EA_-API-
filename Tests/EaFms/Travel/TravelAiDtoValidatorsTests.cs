using Jarvis5.Dtos.EaFms;
using Jarvis5.Validators;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

/// <summary>
/// Closes a gap flagged in the SCIH backend review: /ai/options/confirm and
/// /ai/options/compare previously had no FluentValidation validator of their own for the
/// CreateTravelBookingDto shape they accept — bad input was only ever caught deep inside
/// TravelBookingService as a BusinessRuleException/BadRequestException. These tests exercise
/// the validators directly — the same ones FluentValidation's ASP.NET Core auto-validation
/// applies before either AI endpoint's request ever reaches the service.
/// </summary>
public class TravelAiDtoValidatorsTests
{
    [Theory]
    [InlineData("Flight", true)]
    [InlineData("Train", true)]
    [InlineData("RoadCar", true)]
    [InlineData("Hotel", true)]
    [InlineData("LocalTransport", true)]
    [InlineData("NotARealType", false)]
    [InlineData("", false)]
    public void CreateTravelBookingDto_BookingType_OnlyRealTypesAccepted(string bookingType, bool expectedValid)
    {
        var result = new CreateTravelBookingDtoValidator().Validate(new CreateTravelBookingDto { BookingType = bookingType });
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("Requested", true)]
    [InlineData("NotRequired", true)]
    [InlineData("Bogus", false)]
    public void CreateTravelBookingDto_BookingStatus_NullOrRealStatusAccepted_BogusRejected(string? status, bool expectedValid)
    {
        var dto = new CreateTravelBookingDto { BookingType = "Flight", BookingStatus = status };
        var result = new CreateTravelBookingDtoValidator().Validate(dto);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(15000.50, true)]
    [InlineData(-1, false)]
    [InlineData(15000.567, false)] // more than 2 decimal places
    public void CreateTravelBookingDto_Cost_NonNegativeTwoDecimalsAccepted(double? cost, bool expectedValid)
    {
        var dto = new CreateTravelBookingDto { BookingType = "Flight", Cost = cost.HasValue ? (decimal)cost.Value : null };
        var result = new CreateTravelBookingDtoValidator().Validate(dto);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void CreateTravelBookingDto_ProviderTooLong_Rejected()
    {
        var dto = new CreateTravelBookingDto { BookingType = "Flight", Provider = new string('x', 201) };
        Assert.False(new CreateTravelBookingDtoValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void ConfirmTravelAiOptionsRequestDto_ValidatesEachOption()
    {
        var dto = new ConfirmTravelAiOptionsRequestDto
        {
            Options =
            {
                new CreateTravelBookingDto { BookingType = "Flight" },
                new CreateTravelBookingDto { BookingType = "NotARealType" },
            },
        };
        Assert.False(new ConfirmTravelAiOptionsRequestDtoValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void ConfirmTravelAiOptionsRequestDto_AllOptionsValid_Passes()
    {
        var dto = new ConfirmTravelAiOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight" }, new CreateTravelBookingDto { BookingType = "Hotel" } },
        };
        Assert.True(new ConfirmTravelAiOptionsRequestDtoValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void TravelAiCompareOptionsRequestDto_ValidatesEachOption()
    {
        var dto = new TravelAiCompareOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight" }, new CreateTravelBookingDto { BookingType = "Bogus" } },
        };
        Assert.False(new TravelAiCompareOptionsRequestDtoValidator().Validate(dto).IsValid);
    }
}
