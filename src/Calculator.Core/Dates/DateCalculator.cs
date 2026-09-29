namespace Calculator.Core.Dates;

/// <summary>The difference between two dates in whole years, months, weeks and days.</summary>
public sealed record DateDifference(int Years, int Months, int Weeks, int Days, int TotalDays)
{
    public bool IsZero => TotalDays == 0;
}

/// <summary>
/// Date calculation mode (legacy DateCalculationEngine) on the Gregorian calendar.
/// </summary>
public static class DateCalculator
{
    private const int MinYear = 1;
    private const int MaxYear = 9999;

    /// <summary>
    /// Whole years first, then whole months from there, then weeks and days (order of the dates does not matter).
    /// 2024-01-31 → 2024-03-01 is 1 month and 1 day (via 2024-02-29).
    /// </summary>
    public static DateDifference Difference(DateOnly first, DateOnly second)
    {
        DateOnly start = first <= second ? first : second;
        DateOnly end = first <= second ? second : first;

        int years = CountWholeUnits(start, end, TryAddYears, end.Year - start.Year);
        TryAddYears(start, years, out DateOnly pivot);

        int monthEstimate = ((end.Year - pivot.Year) * 12) + end.Month - pivot.Month;
        int months = CountWholeUnits(pivot, end, TryAddMonths, monthEstimate);
        TryAddMonths(pivot, months, out pivot);

        int remainingDays = end.DayNumber - pivot.DayNumber;
        return new DateDifference(years, months, remainingDays / 7, remainingDays % 7, end.DayNumber - start.DayNumber);
    }

    /// <summary>Adds years, then months, then days. Null when the result is outside 0001–9999.</summary>
    public static DateOnly? Add(DateOnly date, int years, int months, int days)
    {
        if (!TryAddYears(date, years, out DateOnly result) || !TryAddMonths(result, months, out result))
        {
            return null;
        }

        return TryAddDays(result, days, out result) ? result : null;
    }

    /// <summary>Subtracts days, then months, then years (the order of the original app). Null when out of range.</summary>
    public static DateOnly? Subtract(DateOnly date, int years, int months, int days)
    {
        if (!TryAddDays(date, -days, out DateOnly result) || !TryAddMonths(result, -months, out result))
        {
            return null;
        }

        return TryAddYears(result, -years, out result) ? result : null;
    }

    private delegate bool DateShift(DateOnly date, int amount, out DateOnly result);

    /// <summary>The largest n with shift(start, n) &lt;= end, starting from an estimate that is at most a little too big.</summary>
    private static int CountWholeUnits(DateOnly start, DateOnly end, DateShift shift, int estimate)
    {
        int count = Math.Max(0, estimate);
        while (count > 0 && !(shift(start, count, out DateOnly candidate) && candidate <= end))
        {
            count--;
        }

        return count;
    }

    private static bool TryAddYears(DateOnly date, int years, out DateOnly result)
    {
        int year = date.Year + years;
        if (year is < MinYear or > MaxYear)
        {
            result = date;
            return false;
        }

        // 29 February moves to 28 February in non-leap years.
        result = date.AddYears(years);
        return true;
    }

    private static bool TryAddMonths(DateOnly date, int months, out DateOnly result)
    {
        long monthIndex = ((long)date.Year * 12) + (date.Month - 1) + months;
        if (monthIndex < MinYear * 12L || monthIndex > (MaxYear * 12L) + 11)
        {
            result = date;
            return false;
        }

        result = date.AddMonths(months);
        return true;
    }

    private static bool TryAddDays(DateOnly date, int days, out DateOnly result)
    {
        long dayNumber = (long)date.DayNumber + days;
        if (dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber)
        {
            result = date;
            return false;
        }

        result = DateOnly.FromDayNumber((int)dayNumber);
        return true;
    }
}
