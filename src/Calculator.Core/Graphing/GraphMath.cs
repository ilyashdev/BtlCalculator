namespace Calculator.Core.Graphing;

/// <summary>Real-valued functions for graphs; undefined results are NaN.</summary>
public static class GraphMath
{
    /// <summary>
    /// x^y for real numbers. A negative base is allowed when the exponent is a fraction with an odd denominator,
    /// so x^(1/3) is defined for negative x.
    /// </summary>
    public static double RealPower(double x, double y)
    {
        if (x >= 0 || y == Math.Floor(y))
        {
            return Math.Pow(x, y);
        }

        for (int denominator = 3; denominator < 100; denominator += 2)
        {
            double numerator = y * denominator;
            double rounded = Math.Round(numerator);
            if (Math.Abs(numerator - rounded) < 1e-9)
            {
                double magnitude = Math.Pow(-x, y);
                return ((long)rounded % 2 != 0) ? -magnitude : magnitude;
            }
        }

        return double.NaN;
    }

    public static double Factorial(double x)
    {
        if (x < 0 && x == Math.Floor(x))
        {
            return double.NaN;
        }

        return Gamma(x + 1);
    }

    private static readonly double[] LanczosCoefficients =
    [
        0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313, -176.61502916214059,
        12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7,
    ];

    /// <summary>Lanczos approximation (g = 7), accurate to about 15 digits.</summary>
    public static double Gamma(double z)
    {
        if (z < 0.5)
        {
            // Reflection formula
            return Math.PI / (Math.Sin(Math.PI * z) * Gamma(1 - z));
        }

        z -= 1;
        double sum = LanczosCoefficients[0];
        for (int i = 1; i < LanczosCoefficients.Length; i++)
        {
            sum += LanczosCoefficients[i] / (z + i);
        }

        double t = z + 7.5;
        return Math.Sqrt(2 * Math.PI) * Math.Pow(t, z + 0.5) * Math.Exp(-t) * sum;
    }

    public static double Apply(GraphFunction function, double[] arguments, TrigonometricUnit unit)
    {
        double a = arguments[0];
        return function switch
        {
            GraphFunction.Sin => Math.Sin(ToRadians(a, unit)),
            GraphFunction.Cos => Math.Cos(ToRadians(a, unit)),
            GraphFunction.Tan => Math.Tan(ToRadians(a, unit)),
            GraphFunction.Sec => 1 / Math.Cos(ToRadians(a, unit)),
            GraphFunction.Csc => 1 / Math.Sin(ToRadians(a, unit)),
            GraphFunction.Cot => 1 / Math.Tan(ToRadians(a, unit)),
            GraphFunction.Asin => FromRadians(Math.Asin(a), unit),
            GraphFunction.Acos => FromRadians(Math.Acos(a), unit),
            GraphFunction.Atan => FromRadians(Math.Atan(a), unit),
            GraphFunction.Asec => FromRadians(Math.Acos(1 / a), unit),
            GraphFunction.Acsc => FromRadians(Math.Asin(1 / a), unit),
            GraphFunction.Acot => FromRadians((Math.PI / 2) - Math.Atan(a), unit),
            GraphFunction.Sinh => Math.Sinh(a),
            GraphFunction.Cosh => Math.Cosh(a),
            GraphFunction.Tanh => Math.Tanh(a),
            GraphFunction.Sech => 1 / Math.Cosh(a),
            GraphFunction.Csch => 1 / Math.Sinh(a),
            GraphFunction.Coth => 1 / Math.Tanh(a),
            GraphFunction.Asinh => Math.Asinh(a),
            GraphFunction.Acosh => Math.Acosh(a),
            GraphFunction.Atanh => Math.Atanh(a),
            GraphFunction.Asech => Math.Acosh(1 / a),
            GraphFunction.Acsch => Math.Asinh(1 / a),
            GraphFunction.Acoth => Math.Atanh(1 / a),
            GraphFunction.Abs => Math.Abs(a),
            GraphFunction.Floor => Math.Floor(a),
            GraphFunction.Ceiling => Math.Ceiling(a),
            GraphFunction.Round => Math.Round(a, MidpointRounding.AwayFromZero),
            GraphFunction.Sign => double.IsNaN(a) ? double.NaN : Math.Sign(a),
            GraphFunction.Sqrt => Math.Sqrt(a),
            GraphFunction.Cbrt => Math.Cbrt(a),
            GraphFunction.Root => arguments.Length > 1 ? NthRoot(a, arguments[1]) : Math.Sqrt(a),
            GraphFunction.Log => Math.Log10(a),
            GraphFunction.LogBase => arguments.Length > 1 && a > 0 && a != 1 ? Math.Log(arguments[1]) / Math.Log(a) : double.NaN,
            GraphFunction.Ln => Math.Log(a),
            GraphFunction.Exp => Math.Exp(a),
            GraphFunction.Min => arguments.Min(),
            GraphFunction.Max => arguments.Max(),
            GraphFunction.Mod => arguments.Length > 1 && arguments[1] != 0 ? a - (arguments[1] * Math.Floor(a / arguments[1])) : double.NaN,
            _ => double.NaN,
        };
    }

    private static double NthRoot(double x, double n)
    {
        if (n == 0)
        {
            return double.NaN;
        }

        if (x < 0)
        {
            bool oddInteger = n == Math.Floor(n) && (long)n % 2 != 0;
            return oddInteger ? -Math.Pow(-x, 1 / n) : double.NaN;
        }

        return Math.Pow(x, 1 / n);
    }

    internal static double ToRadians(double value, TrigonometricUnit unit) => unit switch
    {
        TrigonometricUnit.Degrees => value * Math.PI / 180,
        TrigonometricUnit.Gradians => value * Math.PI / 200,
        _ => value,
    };

    internal static double FromRadians(double value, TrigonometricUnit unit) => unit switch
    {
        TrigonometricUnit.Degrees => value * 180 / Math.PI,
        TrigonometricUnit.Gradians => value * 200 / Math.PI,
        _ => value,
    };
}
