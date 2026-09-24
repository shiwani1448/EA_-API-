using Jarvis5.Dtos.EaFms;
using Jarvis5.Validators;
using Xunit;

namespace Jarvis5.Tests.EaFms.Calendar;

public class CalendarAiDtoValidatorsTests
{
    private static ApplyQuickAddSuggestionRequestDto Valid() => new()
    {
        Title = "Meeting", EventType = "InternalMeeting", StartDateTime = new DateTime(2026, 10, 1)
    };

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void Apply_EndMustNotPrecedeStart(int? minutes, bool valid)
    {
        var dto = Valid();
        dto.EndDateTime = minutes.HasValue ? dto.StartDateTime!.Value.AddMinutes(minutes.Value) : null;
        var result = new ApplyQuickAddSuggestionRequestDtoValidator().Validate(dto);
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Equal("EndDateTime must be on or after StartDateTime.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Apply_RejectsMissingOrDefaultStart(bool explicitDefault)
    {
        var dto = Valid();
        dto.StartDateTime = explicitDefault ? default(DateTime) : null;
        var result = new ApplyQuickAddSuggestionRequestDtoValidator().Validate(dto);
        Assert.Equal("StartDateTime must be provided.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("Title", 500)]
    [InlineData("EventType", 50)]
    [InlineData("Location", 500)]
    [InlineData("Description", 4000)]
    [InlineData("OrganizerEmployeeId", 100)]
    [InlineData("OrganizerName", 200)]
    [InlineData("Notes", 4000)]
    public void Apply_LengthBoundaryMatchesCalendarMapping(string property, int limit)
    {
        var dto = Valid();
        var field = typeof(ApplyQuickAddSuggestionRequestDto).GetProperty(property)!;
        var validator = new ApplyQuickAddSuggestionRequestDtoValidator();
        field.SetValue(dto, new string('x', limit));
        Assert.True(validator.Validate(dto).IsValid);
        field.SetValue(dto, new string('x', limit + 1));
        var error = Assert.Single(validator.Validate(dto).Errors);
        Assert.Equal(property, error.PropertyName);
        Assert.Equal("MaximumLengthValidator", error.ErrorCode);
        if (property != "Title") Assert.Equal($"{property} must not exceed {limit} characters.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Conflict_RangeMustBeOrdered(int days, bool valid)
    {
        var from = new DateTime(2026, 10, 1);
        var result = new CalendarConflictCheckRequestDtoValidator().Validate(new CalendarConflictCheckRequestDto { From = from, To = from.AddDays(days) });
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Equal("From must be on or before To.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Conflict_RejectsUnsetDates(bool missingFrom, bool missingTo)
    {
        var date = new DateTime(2026, 10, 1);
        var result = new CalendarConflictCheckRequestDtoValidator().Validate(new CalendarConflictCheckRequestDto
        {
            From = missingFrom ? default : date, To = missingTo ? default : date
        });
        Assert.False(result.IsValid);
        Assert.Equal(missingFrom, result.Errors.Any(e => e.ErrorMessage == "From must be provided."));
        Assert.Equal(missingTo, result.Errors.Any(e => e.ErrorMessage == "To must be provided."));
    }
}
