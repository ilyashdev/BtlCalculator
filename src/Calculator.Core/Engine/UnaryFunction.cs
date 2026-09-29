using Calculator.Core.Numerics;

namespace Calculator.Core.Engine;

public enum UnaryFunction
{
    Negate,
    Square,
    Cube,
    SquareRoot,
    CubeRoot,
    Reciprocal,
    Abs,
    Floor,
    Ceiling,
    Factorial,
    PowerOfTen,
    PowerOfTwo,
    PowerOfE,
    Log10,
    Ln,
    ToDegreesMinutesSeconds,
    FromDegreesMinutesSeconds,

    Sin,
    Cos,
    Tan,
    Sec,
    Csc,
    Cot,
    Asin,
    Acos,
    Atan,
    Asec,
    Acsc,
    Acot,

    Sinh,
    Cosh,
    Tanh,
    Sech,
    Csch,
    Coth,
    Asinh,
    Acosh,
    Atanh,
    Asech,
    Acsch,
    Acoth,
}

public static class UnaryFunctionExtensions
{
    /// <summary>Whether the result depends on the angle unit (so the unit is shown in the expression, e.g. sin₀).</summary>
    public static bool UsesAngleUnit(this UnaryFunction function) => function is
        UnaryFunction.Sin or UnaryFunction.Cos or UnaryFunction.Tan or
        UnaryFunction.Sec or UnaryFunction.Csc or UnaryFunction.Cot or
        UnaryFunction.Asin or UnaryFunction.Acos or UnaryFunction.Atan or
        UnaryFunction.Asec or UnaryFunction.Acsc or UnaryFunction.Acot;

    public static BigDecimal Apply(this UnaryFunction function, BigDecimal x, AngleUnit unit, int precision)
    {
        BigDecimal result = function switch
        {
            UnaryFunction.Negate => x.Negate(),
            UnaryFunction.Square => x * x,
            UnaryFunction.Cube => x * x * x,
            UnaryFunction.SquareRoot => BigMath.Sqrt(x, precision),
            UnaryFunction.CubeRoot => BigMath.NthRoot(x, 3, precision),
            UnaryFunction.Reciprocal => BigMath.Divide(BigDecimal.One, x, precision),
            UnaryFunction.Abs => x.Abs(),
            UnaryFunction.Floor => x.Floor(),
            UnaryFunction.Ceiling => x.Ceiling(),
            UnaryFunction.Factorial => BigMath.Factorial(x, precision),
            UnaryFunction.PowerOfTen => BigMath.PowerOfTen(x, precision),
            UnaryFunction.PowerOfTwo => BigMath.Pow(BigDecimal.Two, x, precision),
            UnaryFunction.PowerOfE => BigMath.Exp(x, precision),
            UnaryFunction.Log10 => BigMath.Log10(x, precision),
            UnaryFunction.Ln => BigMath.Ln(x, precision),
            UnaryFunction.ToDegreesMinutesSeconds => DegreesMinutesSeconds.FromDecimalDegrees(x, precision),
            UnaryFunction.FromDegreesMinutesSeconds => DegreesMinutesSeconds.ToDecimalDegrees(x, precision),

            UnaryFunction.Sin => BigMath.Sin(x, unit, precision),
            UnaryFunction.Cos => BigMath.Cos(x, unit, precision),
            UnaryFunction.Tan => BigMath.Tan(x, unit, precision),
            UnaryFunction.Sec => BigMath.Sec(x, unit, precision),
            UnaryFunction.Csc => BigMath.Csc(x, unit, precision),
            UnaryFunction.Cot => BigMath.Cot(x, unit, precision),
            UnaryFunction.Asin => BigMath.Asin(x, unit, precision),
            UnaryFunction.Acos => BigMath.Acos(x, unit, precision),
            UnaryFunction.Atan => BigMath.Atan(x, unit, precision),
            UnaryFunction.Asec => BigMath.Asec(x, unit, precision),
            UnaryFunction.Acsc => BigMath.Acsc(x, unit, precision),
            UnaryFunction.Acot => BigMath.Acot(x, unit, precision),

            UnaryFunction.Sinh => BigMath.Sinh(x, precision),
            UnaryFunction.Cosh => BigMath.Cosh(x, precision),
            UnaryFunction.Tanh => BigMath.Tanh(x, precision),
            UnaryFunction.Sech => BigMath.Sech(x, precision),
            UnaryFunction.Csch => BigMath.Csch(x, precision),
            UnaryFunction.Coth => BigMath.Coth(x, precision),
            UnaryFunction.Asinh => BigMath.Asinh(x, precision),
            UnaryFunction.Acosh => BigMath.Acosh(x, precision),
            UnaryFunction.Atanh => BigMath.Atanh(x, precision),
            UnaryFunction.Asech => BigMath.Asech(x, precision),
            UnaryFunction.Acsch => BigMath.Acsch(x, precision),
            UnaryFunction.Acoth => BigMath.Acoth(x, precision),
            _ => throw new ArgumentOutOfRangeException(nameof(function), function, null),
        };

        return BigMath.EnsureInRange(result.RoundToSignificantDigits(precision));
    }
}
