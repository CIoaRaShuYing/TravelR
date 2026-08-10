using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class MealAllowanceCalculatorTests
{
    [Fact]
    public void Same_day_round_trip_counts_as_one_day()
    {
        var date = new DateOnly(2026, 8, 10);

        Assert.Equal(1, MealAllowanceCalculator.CalculateDays(date, date));
    }

    [Fact]
    public void Multi_day_trip_includes_departure_and_return_dates()
    {
        Assert.Equal(3, MealAllowanceCalculator.CalculateDays(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12)));
    }

    [Theory]
    [InlineData(null, "2026-08-10")]
    [InlineData("2026-08-10", null)]
    [InlineData("2026-08-11", "2026-08-10")]
    public void Incomplete_or_reversed_trip_has_no_allowance(string? departure, string? returnDate)
    {
        DateOnly? departureDate = departure is null ? null : DateOnly.Parse(departure);
        DateOnly? parsedReturnDate = returnDate is null ? null : DateOnly.Parse(returnDate);

        Assert.Equal(0, MealAllowanceCalculator.CalculateDays(departureDate, parsedReturnDate));
    }
}
