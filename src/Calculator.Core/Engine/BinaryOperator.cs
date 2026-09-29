using Calculator.Core.Numerics;

namespace Calculator.Core.Engine;

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,

    /// <summary>x mod y.</summary>
    Modulo,

    /// <summary>x ^ y.</summary>
    Power,

    /// <summary>x yroot y: the y-th root of x.</summary>
    Root,

    /// <summary>x log base y.</summary>
    LogBase,
}

public static class BinaryOperatorExtensions
{
    /// <summary>Higher binds tighter. Operators of equal precedence are evaluated left to right.</summary>
    public static int Precedence(this BinaryOperator op) => op switch
    {
        BinaryOperator.Add or BinaryOperator.Subtract => 1,
        BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo => 2,
        BinaryOperator.Power or BinaryOperator.Root or BinaryOperator.LogBase => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public static BigDecimal Apply(this BinaryOperator op, BigDecimal left, BigDecimal right, int precision)
    {
        BigDecimal result = op switch
        {
            BinaryOperator.Add => left + right,
            BinaryOperator.Subtract => left - right,
            BinaryOperator.Multiply => left * right,
            BinaryOperator.Divide => BigMath.Divide(left, right, precision),
            BinaryOperator.Modulo => BigMath.Modulo(left, right),
            BinaryOperator.Power => BigMath.Pow(left, right, precision),
            BinaryOperator.Root => Root(left, right, precision),
            BinaryOperator.LogBase => BigMath.LogBase(left, right, precision),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
        };

        return BigMath.EnsureInRange(result.RoundToSignificantDigits(precision));
    }

    private static BigDecimal Root(BigDecimal x, BigDecimal degree, int precision)
    {
        if (degree.IsZero)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        // Integer degrees allow odd roots of negative numbers: 3rd root of −8 is −2.
        if (degree.IsInteger)
        {
            return BigMath.NthRoot(x, degree.ToBigInteger(), precision);
        }

        return BigMath.Pow(x, BigMath.Divide(BigDecimal.One, degree, precision + 10), precision);
    }
}
