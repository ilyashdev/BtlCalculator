using Calculator.Core.Dates;

namespace Calculator.Core.Tests;

public class DateCalculatorTests
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Fact]
    public void SameDates() => Assert.True(DateCalculator.Difference(D(2026, 9, 28), D(2026, 9, 28)).IsZero);

    [Fact]
    public void YearsMonthsWeeksDays()
    {
        DateDifference difference = DateCalculator.Difference(D(2020, 1, 15), D(2023, 4, 25));
        Assert.Equal(new DateDifference(3, 3, 1, 3, 1196), difference);
    }

    [Fact]
    public void OrderDoesNotMatter() =>
        Assert.Equal(DateCalculator.Difference(D(2020, 1, 15), D(2023, 4, 25)), DateCalculator.Difference(D(2023, 4, 25), D(2020, 1, 15)));

    [Fact]
    public void EndOfMonth()
    {
        // 31 January + 1 month is 29 February (2024 is a leap year), then 1 more day.
        DateDifference difference = DateCalculator.Difference(D(2024, 1, 31), D(2024, 3, 1));
        Assert.Equal(new DateDifference(0, 1, 0, 1, 30), difference);
    }

    [Fact]
    public void LeapDayToNextYear()
    {
        DateDifference difference = DateCalculator.Difference(D(2024, 2, 29), D(2025, 2, 28));
        Assert.Equal(1, difference.Years);
        Assert.Equal(0, difference.Days);
    }

    [Fact]
    public void AddYearsMonthsDays() => Assert.Equal(D(2027, 11, 3), DateCalculator.Add(D(2026, 9, 28), 1, 1, 6));

    [Fact]
    public void SubtractDaysFirst() => Assert.Equal(D(2025, 7, 26), DateCalculator.Subtract(D(2026, 9, 28), 1, 2, 2));

    [Fact]
    public void OutOfRange()
    {
        Assert.Null(DateCalculator.Add(D(9999, 12, 1), 0, 0, 31));
        Assert.Null(DateCalculator.Subtract(D(1, 1, 5), 1, 0, 0));
    }
}
