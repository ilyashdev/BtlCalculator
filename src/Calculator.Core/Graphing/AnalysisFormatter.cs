using System.Globalization;

namespace Calculator.Core.Graphing;

/// <summary>Plain text of the key graph features: "x ∈ ℝ", "x ≠ 0", "−1 ≤ y ≤ 1", "(0, −4)", "y = 2x + 1".</summary>
public sealed class AnalysisFormatter(CultureInfo culture)
{
    private readonly string _listSeparator = culture.NumberFormat.NumberDecimalSeparator == "," ? "; " : ", ";

    public string Number(double value) => NiceNumber.Format(value, culture, 1e-7);

    /// <summary>A union of intervals written with inequalities in the given variable.</summary>
    public string Set(string variable, IReadOnlyList<AnalysisInterval> intervals)
    {
        if (intervals.Count == 0)
        {
            return string.Empty;
        }

        if (intervals is [{ Low: double.NegativeInfinity, High: double.PositiveInfinity }])
        {
            return variable + " ∈ ℝ";
        }

        // The real line without some points: "x ≠ −1 ∧ x ≠ 1".
        bool punctured = double.IsNegativeInfinity(intervals[0].Low) && double.IsPositiveInfinity(intervals[^1].High) && intervals.Count > 1;
        for (int i = 1; i < intervals.Count && punctured; i++)
        {
            punctured = intervals[i - 1].High == intervals[i].Low && !intervals[i - 1].HighClosed && !intervals[i].LowClosed;
        }

        if (punctured)
        {
            return string.Join(" ∧ ", intervals.Skip(1).Select(interval => $"{variable} ≠ {Number(interval.Low)}"));
        }

        return string.Join(" ∨ ", intervals.Select(interval => Interval(variable, interval)));
    }

    public string Point(AnalysisPoint point) => $"({Number(point.X)}{_listSeparator}{Number(point.Y)})";

    public string Values(string variable, IReadOnlyList<double> values) =>
        variable + " = " + string.Join(_listSeparator, values.Select(Number));

    public string Line(double slope, double intercept)
    {
        string slopeText = slope switch
        {
            1 => string.Empty,
            -1 => "−",
            _ => Number(slope),
        };

        string text = $"y = {slopeText}x";
        if (intercept != 0)
        {
            text += intercept > 0 ? " + " : " − ";
            text += Number(Math.Abs(intercept));
        }

        return text;
    }

    /// <summary>An open interval "(a, b)" used as the monotonicity row header.</summary>
    public string OpenInterval(AnalysisInterval interval) => $"({Number(interval.Low)}{_listSeparator}{Number(interval.High)})";

    // Periodic features, written like the original: "x = πn₁ + π/2; n₁ ∈ ℤ".

    private const string Integer = "n₁";
    private const string IntegerSuffix = "; n₁ ∈ ℤ";

    /// <summary>"πn₁ + π/2", "2πn₁", "n₁/2".</summary>
    public string Family(PeriodicFamily family)
    {
        // The integer goes after the numerator of the period: π/2 → πn₁/2, 1 → n₁.
        string period = Number(family.Period);
        int slash = period.IndexOf('/', StringComparison.Ordinal);
        string numerator = slash < 0 ? period : period[..slash];
        string denominator = slash < 0 ? string.Empty : period[slash..];
        string term = (numerator == "1" ? string.Empty : numerator) + Integer + denominator;

        return family.Offset == 0 ? term : $"{term} + {Number(family.Offset)}";
    }

    /// <summary>"x = πn₁; n₁ ∈ ℤ" (or "x ≠ …; ∀n₁ ∈ ℤ" for the domain).</summary>
    public string Families(string relation, IReadOnlyList<PeriodicFamily> families)
    {
        bool excluded = relation == "≠";
        string values = string.Join(excluded ? " ∧ " : " ∨ ", families.Select(family => $"x {relation} {Family(family)}"));
        return excluded ? values + "; ∀n₁ ∈ ℤ" : values + IntegerSuffix;
    }

    /// <summary>"(2πn₁ + π/2; 1); n₁ ∈ ℤ".</summary>
    public string Point(PeriodicPoint point) => $"({Family(point.X)}{_listSeparator}{Number(point.Y)}){IntegerSuffix}";

    /// <summary>"(πn₁ + π/2; πn₁ + 3π/2); n₁ ∈ ℤ".</summary>
    public string Interval(PeriodicInterval interval) =>
        $"({Family(new PeriodicFamily(interval.Low, interval.Period))}{_listSeparator}{Family(new PeriodicFamily(interval.High, interval.Period))}){IntegerSuffix}";

    private string Interval(string variable, AnalysisInterval interval)
    {
        if (interval.Low == interval.High)
        {
            return $"{variable} = {Number(interval.Low)}";
        }

        if (double.IsNegativeInfinity(interval.Low))
        {
            return $"{variable} {(interval.HighClosed ? "≤" : "<")} {Number(interval.High)}";
        }

        if (double.IsPositiveInfinity(interval.High))
        {
            return $"{variable} {(interval.LowClosed ? "≥" : ">")} {Number(interval.Low)}";
        }

        return $"{Number(interval.Low)} {(interval.LowClosed ? "≤" : "<")} {variable} {(interval.HighClosed ? "≤" : "<")} {Number(interval.High)}";
    }
}
