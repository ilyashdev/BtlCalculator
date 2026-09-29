using Calculator.Core.Numerics;

namespace Calculator.Core.Converters;

public static class UnitConversion
{
    private const int Precision = 40;

    /// <summary>Converts through the base unit: base = (x + offset) × multiplier ÷ divisor, then the inverse for the target.</summary>
    public static BigDecimal Convert(BigDecimal value, Unit from, Unit to)
    {
        if (from == to)
        {
            return value;
        }

        BigDecimal inBase = BigMath.Divide((value + from.Offset) * from.Multiplier, from.Divisor, Precision);
        BigDecimal result = BigMath.Divide(inBase * to.Divisor, to.Multiplier, Precision) - to.Offset;
        return result.RoundToSignificantDigits(Precision);
    }
}
