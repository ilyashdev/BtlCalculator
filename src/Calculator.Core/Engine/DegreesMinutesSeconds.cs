using Calculator.Core.Numerics;

namespace Calculator.Core.Engine;

/// <summary>
/// Conversion between decimal degrees and the calculator's D.MMSS notation,
/// where 12.3045 means 12° 30′ 45″ (and further digits are fractions of a second).
/// </summary>
public static class DegreesMinutesSeconds
{
    private static readonly BigDecimal Sixty = 60;
    private static readonly BigDecimal ThirtySixHundred = 3600;

    /// <summary>12.5125 (decimal degrees) → 12.3045 (12° 30′ 45″).</summary>
    public static BigDecimal FromDecimalDegrees(BigDecimal value, int precision)
    {
        bool negative = value.IsNegative;
        BigDecimal absolute = value.Abs();

        BigDecimal degrees = absolute.Truncate();
        BigDecimal totalMinutes = (absolute - degrees) * Sixty;
        BigDecimal minutes = totalMinutes.Truncate();
        BigDecimal seconds = (totalMinutes - minutes) * Sixty;

        BigDecimal result = degrees + minutes.ScaleByPowerOfTen(-2) + seconds.ScaleByPowerOfTen(-4);
        result = result.RoundToSignificantDigits(precision);
        return negative ? result.Negate() : result;
    }

    /// <summary>12.3045 (12° 30′ 45″) → 12.5125 (decimal degrees).</summary>
    public static BigDecimal ToDecimalDegrees(BigDecimal value, int precision)
    {
        bool negative = value.IsNegative;
        BigDecimal absolute = value.Abs();

        BigDecimal degrees = absolute.Truncate();
        BigDecimal minutesAndSeconds = (absolute - degrees).ScaleByPowerOfTen(2);
        BigDecimal minutes = minutesAndSeconds.Truncate();
        BigDecimal seconds = (minutesAndSeconds - minutes).ScaleByPowerOfTen(2);

        BigDecimal result = degrees
            + BigMath.Divide(minutes, Sixty, precision + 5)
            + BigMath.Divide(seconds, ThirtySixHundred, precision + 5);
        result = result.RoundToSignificantDigits(precision);
        return negative ? result.Negate() : result;
    }
}
