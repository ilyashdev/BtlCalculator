namespace Calculator.Core.Graphing;

/// <summary>
/// Finds the period of y = f(x). A candidate comes from the expression: trigonometric functions of a linear argument
/// a·x + b have the period 2π/|a| (π/|a| for tan and cot), anything built from periodic parts has the least common
/// multiple of their periods. The candidate is then checked numerically and reduced to the smallest period of the
/// form candidate/k (|sin x| has π, not 2π).
/// </summary>
public static class PeriodFinder
{
    private const int MaxRatioDenominator = 12;
    private const int MaxDivisor = 12;

    /// <summary>The smallest period, or null when the function is not periodic (or constant).</summary>
    public static double? Find(GraphNode function, GraphEvaluationContext context)
    {
        double? candidate = Candidate(function, context);
        if (candidate is not double period || period <= 0 || !IsPeriod(function, context, period))
        {
            return null;
        }

        for (int divisor = MaxDivisor; divisor >= 2; divisor--)
        {
            if (IsPeriod(function, context, period / divisor))
            {
                return NiceNumber.Snap(period / divisor, 1e-12);
            }
        }

        return NiceNumber.Snap(period, 1e-12);
    }

    /// <summary>A period from the expression: 0 for a subexpression without x (any period fits), null when aperiodic.</summary>
    private static double? Candidate(GraphNode node, GraphEvaluationContext context)
    {
        if (!node.UsesX)
        {
            return 0;
        }

        switch (node)
        {
            case FunctionNode { Arguments.Count: 1 } trig when TrigonometricPeriod(trig.Function, context.TrigonometricUnit) is double basePeriod:
                return LinearSlope(trig.Arguments[0], context) is double slope
                    ? slope == 0 ? 0 : basePeriod / Math.Abs(slope)
                    : Candidate(trig.Arguments[0], context);

            case FunctionNode function:
                return function.Arguments.Aggregate((double?)0, (period, argument) => Combine(period, Candidate(argument, context)));

            case BinaryNode binary:
                return Combine(Candidate(binary.Left, context), Candidate(binary.Right, context));

            case NegateNode negate:
                return Candidate(negate.Operand, context);

            case FactorialNode factorial:
                return Candidate(factorial.Operand, context);

            default:
                return null; // x itself
        }
    }

    private static double? TrigonometricPeriod(GraphFunction function, TrigonometricUnit unit)
    {
        double fullTurn = unit switch
        {
            TrigonometricUnit.Degrees => 360,
            TrigonometricUnit.Gradians => 400,
            _ => 2 * Math.PI,
        };

        return function switch
        {
            GraphFunction.Sin or GraphFunction.Cos or GraphFunction.Sec or GraphFunction.Csc => fullTurn,
            GraphFunction.Tan or GraphFunction.Cot => fullTurn / 2,
            _ => null,
        };
    }

    /// <summary>The slope a when the expression is a·x + b, otherwise null.</summary>
    private static double? LinearSlope(GraphNode node, GraphEvaluationContext context)
    {
        double At(double x)
        {
            context.X = x;
            return node.Evaluate(context);
        }

        double intercept = At(0);
        double slope = At(1) - intercept;
        if (!double.IsFinite(intercept) || !double.IsFinite(slope))
        {
            return null;
        }

        foreach (double x in (double[])[-2.5, 3.7, 11.3, -40.1])
        {
            double expected = (slope * x) + intercept;
            double value = At(x);
            if (!double.IsFinite(value) || Math.Abs(value - expected) > 1e-9 * (1 + Math.Abs(expected)))
            {
                return null;
            }
        }

        return slope;
    }

    /// <summary>The least common multiple of two periods when their ratio is a simple fraction.</summary>
    private static double? Combine(double? first, double? second)
    {
        if (first is not double a || second is not double b)
        {
            return null;
        }

        if (a == 0 || b == 0)
        {
            return a + b;
        }

        double ratio = a / b;
        for (int q = 1; q <= MaxRatioDenominator; q++)
        {
            double p = Math.Round(ratio * q);
            if (p >= 1 && Math.Abs((ratio * q) - p) < 1e-9 * ratio * q)
            {
                return a * q;
            }
        }

        return null;
    }

    private static bool IsPeriod(GraphNode function, GraphEvaluationContext context, double period)
    {
        int compared = 0;
        for (int i = 0; i < 200; i++)
        {
            double x = -47.3 + (i * 0.4731);
            context.X = x;
            double a = function.Evaluate(context);
            context.X = x + period;
            double b = function.Evaluate(context);

            // Values near poles are too imprecise to compare.
            if (!double.IsFinite(a) || !double.IsFinite(b) || Math.Abs(a) > 1e6 || Math.Abs(b) > 1e6)
            {
                continue;
            }

            if (Math.Abs(a - b) > 1e-7 * (1 + Math.Abs(a)))
            {
                return false;
            }

            compared++;
        }

        return compared >= 50;
    }
}
