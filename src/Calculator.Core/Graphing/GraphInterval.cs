namespace Calculator.Core.Graphing;

/// <summary>
/// A closed range of real numbers that contains every value an expression takes on a range of x (interval
/// arithmetic). It may be wider than the true range but never narrower, which is what makes it useful: when the
/// bound of tan(100x) over one pixel column is (−∞, ∞), the column really contains a pole, however unlucky the
/// sampled points are. Empty means the expression is undefined everywhere on the range.
/// </summary>
public readonly record struct GraphInterval(double Low, double High)
{
    public static GraphInterval Entire { get; } = new(double.NegativeInfinity, double.PositiveInfinity);

    public static GraphInterval Empty { get; } = new(double.NaN, double.NaN);

    public bool IsEmpty => double.IsNaN(Low) || double.IsNaN(High);

    public double Width => High - Low;

    public static GraphInterval Point(double value) => double.IsNaN(value) ? Empty : new GraphInterval(value, value);

    /// <summary>The smallest interval containing the values; any NaN (such as ∞ − ∞) gives up with <see cref="Entire"/>.</summary>
    public static GraphInterval Hull(params ReadOnlySpan<double> values)
    {
        double low = double.PositiveInfinity;
        double high = double.NegativeInfinity;
        foreach (double value in values)
        {
            if (double.IsNaN(value))
            {
                return Entire;
            }

            low = Math.Min(low, value);
            high = Math.Max(high, value);
        }

        return new GraphInterval(low, high);
    }

    public bool Contains(double value) => !IsEmpty && value >= Low && value <= High;

    /// <summary>The image under a non-decreasing function.</summary>
    public GraphInterval Increasing(Func<double, double> function) => IsEmpty ? Empty : Hull(function(Low), function(High));

    /// <summary>The image under a non-increasing function.</summary>
    public GraphInterval Decreasing(Func<double, double> function) => IsEmpty ? Empty : Hull(function(High), function(Low));

    /// <summary>The part inside [low, high], or <see cref="Empty"/> (the domain of sqrt, ln, asin, ...).</summary>
    public GraphInterval Clamp(double low, double high)
    {
        if (IsEmpty || High < low || Low > high)
        {
            return Empty;
        }

        return new GraphInterval(Math.Max(Low, low), Math.Min(High, high));
    }
}

/// <summary>Evaluates a <see cref="GraphNode"/> over intervals of x and y with the rules of <see cref="GraphMath"/>.</summary>
public static class IntervalEvaluator
{
    private const double TwoPi = 2 * Math.PI;

    /// <param name="context">Parameter values and the angle unit (x and y come from the intervals).</param>
    public static GraphInterval Evaluate(GraphNode node, GraphInterval x, GraphInterval y, GraphEvaluationContext context) => node switch
    {
        NumberNode number => GraphInterval.Point(number.Value),
        XNode => x,
        YNode => y,
        ParameterNode parameter => GraphInterval.Point(parameter.Evaluate(context)),
        NegateNode negate => Negate(Evaluate(negate.Operand, x, y, context)),
        BinaryNode binary => Binary(binary.Operator, Evaluate(binary.Left, x, y, context), Evaluate(binary.Right, x, y, context)),
        FunctionNode function => Function(function, function.Arguments.Select(argument => Evaluate(argument, x, y, context)).ToArray(), context.TrigonometricUnit),
        _ => GraphInterval.Entire, // factorial and anything new: no bound known
    };

    private static GraphInterval Negate(GraphInterval a) => a.IsEmpty ? a : new GraphInterval(-a.High, -a.Low);

    private static GraphInterval Binary(GraphOperator op, GraphInterval a, GraphInterval b)
    {
        if (a.IsEmpty || b.IsEmpty)
        {
            return GraphInterval.Empty;
        }

        return op switch
        {
            GraphOperator.Add => GraphInterval.Hull(a.Low + b.Low, a.High + b.High),
            GraphOperator.Subtract => GraphInterval.Hull(a.Low - b.High, a.High - b.Low),
            GraphOperator.Multiply => Multiply(a, b),
            GraphOperator.Divide => Divide(a, b),
            GraphOperator.Power => Power(a, b),
            _ => GraphInterval.Entire,
        };
    }

    private static GraphInterval Multiply(GraphInterval a, GraphInterval b) =>
        GraphInterval.Hull(a.Low * b.Low, a.Low * b.High, a.High * b.Low, a.High * b.High);

    private static GraphInterval Divide(GraphInterval a, GraphInterval b)
    {
        if (a.IsEmpty || b.IsEmpty || (b.Low == 0 && b.High == 0))
        {
            return GraphInterval.Empty; // x / 0 is undefined (GraphMath gives NaN)
        }

        if (b.Contains(0))
        {
            return a.Low == 0 && a.High == 0 ? GraphInterval.Point(0) : GraphInterval.Entire;
        }

        return Multiply(a, GraphInterval.Hull(1 / b.Low, 1 / b.High));
    }

    private static GraphInterval Reciprocal(GraphInterval a) => Divide(GraphInterval.Point(1), a);

    /// <summary>x^y as <see cref="GraphMath.RealPower"/>: exact for integer and positive-base cases, otherwise unbounded.</summary>
    private static GraphInterval Power(GraphInterval x, GraphInterval y)
    {
        if (y.Low == y.High)
        {
            double exponent = y.Low;
            if (exponent == Math.Floor(exponent) && Math.Abs(exponent) < 1e9)
            {
                return IntegerPower(x, (long)exponent);
            }

            if (x.Low >= 0)
            {
                return exponent > 0 ? x.Increasing(v => Math.Pow(v, exponent)) : x.Decreasing(v => Math.Pow(v, exponent));
            }

            return GraphInterval.Entire; // negative bases with fractional exponents: defined only for some
        }

        if (x.Low > 0)
        {
            // x^y = e^(y·ln x) is monotone in each variable, so the extremes are at the corners.
            return GraphInterval.Hull(
                Math.Pow(x.Low, y.Low), Math.Pow(x.Low, y.High), Math.Pow(x.High, y.Low), Math.Pow(x.High, y.High));
        }

        return GraphInterval.Entire;
    }

    private static GraphInterval IntegerPower(GraphInterval x, long n)
    {
        if (n == 0)
        {
            return GraphInterval.Point(1);
        }

        if (n < 0)
        {
            return Reciprocal(IntegerPower(x, -n));
        }

        if (n % 2 == 1)
        {
            return x.Increasing(v => Math.Pow(v, n));
        }

        double low = Math.Pow(x.Low, n);
        double high = Math.Pow(x.High, n);
        return x.Contains(0) ? new GraphInterval(0, Math.Max(low, high)) : GraphInterval.Hull(low, high);
    }

    private static GraphInterval Function(FunctionNode node, GraphInterval[] arguments, TrigonometricUnit unit)
    {
        if (arguments.Any(argument => argument.IsEmpty))
        {
            return GraphInterval.Empty;
        }

        GraphInterval a = arguments[0];
        GraphInterval radians = a.Increasing(v => GraphMath.ToRadians(v, unit));
        Func<double, double> toUnit = v => GraphMath.FromRadians(v, unit);

        return node.Function switch
        {
            GraphFunction.Sin => Sin(radians),
            GraphFunction.Cos => Sin(Shift(radians, Math.PI / 2)),
            GraphFunction.Tan => Tan(radians),
            GraphFunction.Sec => Reciprocal(Sin(Shift(radians, Math.PI / 2))),
            GraphFunction.Csc => Reciprocal(Sin(radians)),
            GraphFunction.Cot => Negate(Tan(Shift(radians, -Math.PI / 2))), // cot x = −tan(x − π/2)
            GraphFunction.Asin => a.Clamp(-1, 1).Increasing(v => toUnit(Math.Asin(v))),
            GraphFunction.Acos => a.Clamp(-1, 1).Decreasing(v => toUnit(Math.Acos(v))),
            GraphFunction.Atan => a.Increasing(v => toUnit(Math.Atan(v))),
            GraphFunction.Asec => Reciprocal(a).Clamp(-1, 1).Decreasing(v => toUnit(Math.Acos(v))),
            GraphFunction.Acsc => Reciprocal(a).Clamp(-1, 1).Increasing(v => toUnit(Math.Asin(v))),
            GraphFunction.Acot => a.Decreasing(v => toUnit((Math.PI / 2) - Math.Atan(v))),
            GraphFunction.Sinh => a.Increasing(Math.Sinh),
            GraphFunction.Cosh => Even(a, Math.Cosh),
            GraphFunction.Tanh => a.Increasing(Math.Tanh),
            GraphFunction.Sech => Reciprocal(Even(a, Math.Cosh)),
            GraphFunction.Csch => Reciprocal(a.Increasing(Math.Sinh)),
            GraphFunction.Coth => Reciprocal(a.Increasing(Math.Tanh)),
            GraphFunction.Asinh => a.Increasing(Math.Asinh),
            GraphFunction.Acosh => a.Clamp(1, double.PositiveInfinity).Increasing(Math.Acosh),
            GraphFunction.Atanh => a.Clamp(-1, 1).Increasing(Math.Atanh),
            GraphFunction.Asech => Reciprocal(a).Clamp(1, double.PositiveInfinity).Increasing(Math.Acosh),
            GraphFunction.Acsch => Reciprocal(a).Increasing(Math.Asinh),
            GraphFunction.Acoth => Reciprocal(a).Clamp(-1, 1).Increasing(Math.Atanh),
            GraphFunction.Abs => Even(a, Math.Abs),
            GraphFunction.Floor => a.Increasing(Math.Floor),
            GraphFunction.Ceiling => a.Increasing(Math.Ceiling),
            GraphFunction.Round => a.Increasing(v => Math.Round(v, MidpointRounding.AwayFromZero)),
            GraphFunction.Sign => a.Increasing(v => Math.Sign(v)),
            GraphFunction.Sqrt => a.Clamp(0, double.PositiveInfinity).Increasing(Math.Sqrt),
            GraphFunction.Cbrt => a.Increasing(Math.Cbrt),
            GraphFunction.Root => Root(a, arguments.Length > 1 ? arguments[1] : GraphInterval.Point(2)),
            GraphFunction.Log => a.Clamp(0, double.PositiveInfinity).Increasing(Math.Log10),
            GraphFunction.Ln => a.Clamp(0, double.PositiveInfinity).Increasing(Math.Log),
            GraphFunction.LogBase => LogBase(a, arguments[1]),
            GraphFunction.Exp => a.Increasing(Math.Exp),
            GraphFunction.Min => new GraphInterval(arguments.Min(argument => argument.Low), arguments.Min(argument => argument.High)),
            GraphFunction.Max => new GraphInterval(arguments.Max(argument => argument.Low), arguments.Max(argument => argument.High)),
            GraphFunction.Mod => Mod(a, arguments[1]),
            _ => GraphInterval.Entire,
        };
    }

    private static GraphInterval Shift(GraphInterval a, double offset) => new(a.Low + offset, a.High + offset);

    /// <summary>A function decreasing up to 0 and increasing after it (|x|, cosh x).</summary>
    private static GraphInterval Even(GraphInterval a, Func<double, double> function)
    {
        GraphInterval ends = GraphInterval.Hull(function(a.Low), function(a.High));
        return a.Contains(0) ? new GraphInterval(function(0), ends.High) : ends;
    }

    private static GraphInterval Sin(GraphInterval radians)
    {
        if (radians.IsEmpty || double.IsInfinity(radians.Low) || double.IsInfinity(radians.High) || radians.Width >= TwoPi)
        {
            return radians.IsEmpty ? GraphInterval.Empty : new GraphInterval(-1, 1);
        }

        GraphInterval ends = GraphInterval.Hull(Math.Sin(radians.Low), Math.Sin(radians.High));
        double high = ContainsPeriodicPoint(radians, Math.PI / 2, TwoPi) ? 1 : ends.High;
        double low = ContainsPeriodicPoint(radians, -Math.PI / 2, TwoPi) ? -1 : ends.Low;
        return new GraphInterval(low, high);
    }

    private static GraphInterval Tan(GraphInterval radians)
    {
        if (radians.IsEmpty)
        {
            return GraphInterval.Empty;
        }

        // A pole π/2 + kπ inside makes the range unbounded both ways.
        if (double.IsInfinity(radians.Low) || double.IsInfinity(radians.High) || radians.Width >= Math.PI
            || ContainsPeriodicPoint(radians, Math.PI / 2, Math.PI))
        {
            return GraphInterval.Entire;
        }

        return radians.Increasing(Math.Tan);
    }

    /// <summary>Whether the interval contains start + k·period for some integer k.</summary>
    private static bool ContainsPeriodicPoint(GraphInterval interval, double start, double period)
    {
        double k = Math.Ceiling((interval.Low - start) / period);
        return start + (k * period) <= interval.High;
    }

    private static GraphInterval Root(GraphInterval a, GraphInterval n)
    {
        if (n.Low != n.High || n.Low <= 0)
        {
            return GraphInterval.Entire;
        }

        double degree = n.Low;
        bool oddInteger = degree == Math.Floor(degree) && (long)degree % 2 != 0;
        if (oddInteger)
        {
            return a.Increasing(v => v < 0 ? -Math.Pow(-v, 1 / degree) : Math.Pow(v, 1 / degree));
        }

        return a.Clamp(0, double.PositiveInfinity).Increasing(v => Math.Pow(v, 1 / degree));
    }

    private static GraphInterval LogBase(GraphInterval logBase, GraphInterval a)
    {
        if (logBase.Low != logBase.High || logBase.Low <= 0 || logBase.Low == 1)
        {
            return GraphInterval.Entire;
        }

        double scale = 1 / Math.Log(logBase.Low);
        GraphInterval ln = a.Clamp(0, double.PositiveInfinity).Increasing(Math.Log);
        return Multiply(ln, GraphInterval.Point(scale));
    }

    /// <summary>a − b·floor(a / b): between 0 and b, and exactly a shifted range when floor(a / b) does not change.</summary>
    private static GraphInterval Mod(GraphInterval a, GraphInterval b)
    {
        if (b.Low != b.High || b.Low == 0)
        {
            return GraphInterval.Entire;
        }

        double divisor = b.Low;
        double first = Math.Floor(a.Low / divisor);
        double last = Math.Floor(a.High / divisor);
        if (first == last && double.IsFinite(first))
        {
            return GraphInterval.Hull(a.Low - (divisor * first), a.High - (divisor * first));
        }

        return GraphInterval.Hull(0, divisor);
    }
}
