namespace Calculator.Core.Graphing;

public sealed record AnalysisInterval(double Low, double High, bool LowClosed, bool HighClosed);

public readonly record struct AnalysisPoint(double X, double Y);

public enum FunctionParity
{
    Unknown,
    Odd,
    Even,
    Neither,
}

public enum FunctionMonotonicity
{
    Unknown,
    Increasing,
    Decreasing,
    Constant,
}

/// <summary>
/// Key graph features of y = f(x). A null list means "too complex" (for example infinitely many zeros of x·sin x).
/// Periodic functions have <see cref="Period"/> and <see cref="Periodic"/>; their features repeat, so they are
/// described in <see cref="Periodic"/> and the lists of single values here are null.
/// </summary>
public sealed record FunctionAnalysisResult(
    IReadOnlyList<AnalysisInterval>? Domain,
    AnalysisInterval? Range,
    IReadOnlyList<double>? Zeros,
    double? YIntercept,
    IReadOnlyList<AnalysisPoint>? Minima,
    IReadOnlyList<AnalysisPoint>? Maxima,
    IReadOnlyList<AnalysisPoint>? InflectionPoints,
    IReadOnlyList<double>? VerticalAsymptotes,
    IReadOnlyList<double> HorizontalAsymptotes,
    IReadOnlyList<(double Slope, double Intercept)> ObliqueAsymptotes,
    FunctionParity Parity,
    IReadOnlyList<(AnalysisInterval Interval, FunctionMonotonicity Direction)>? Monotonicity,
    double? Period = null,
    PeriodicFeatures? Periodic = null);

/// <summary>The values x = Offset + Period·n for every integer n.</summary>
public readonly record struct PeriodicFamily(double Offset, double Period);

/// <summary>The points (Offset + Period·n, Y).</summary>
public readonly record struct PeriodicPoint(PeriodicFamily X, double Y);

/// <summary>The intervals (Low + Period·n, High + Period·n) on which the function has the given direction.</summary>
public readonly record struct PeriodicInterval(double Low, double High, double Period, FunctionMonotonicity Direction);

/// <summary>Features of a periodic function as families of values; a null list means "too complex".</summary>
public sealed record PeriodicFeatures(
    IReadOnlyList<PeriodicFamily>? DomainExclusions,
    IReadOnlyList<PeriodicFamily>? Zeros,
    IReadOnlyList<PeriodicPoint>? Minima,
    IReadOnlyList<PeriodicPoint>? Maxima,
    IReadOnlyList<PeriodicPoint>? InflectionPoints,
    IReadOnlyList<PeriodicFamily>? VerticalAsymptotes,
    IReadOnlyList<PeriodicInterval>? Monotonicity);

/// <summary>
/// Numeric analysis of y = f(x) by dense sampling on [−1000, 1000] (finer on [−20, 20]), refined by bisection and
/// golden section search. Features far outside the sampled range are extrapolated from the behavior at ±10^9.
/// Periodic functions (see <see cref="PeriodFinder"/>) are sampled over one period with margins instead, and every
/// feature found is reported once per period.
/// </summary>
public sealed class FunctionAnalyzer
{
    private const int MaxFeatureCount = 20;
    private const double PoleMagnitude = 1e9;

    private readonly GraphNode _function;
    private readonly GraphEvaluationContext _context;
    private readonly List<double> _xs = [];
    private readonly List<double> _values = [];
    private readonly List<(AnalysisInterval Interval, int First, int Last)> _domain = [];
    private readonly List<double> _zeros = [];
    private readonly List<AnalysisPoint> _minima = [];
    private readonly List<AnalysisPoint> _maxima = [];
    private readonly List<double> _inflectionPoints = [];
    private readonly List<double> _poles = [];
    private readonly List<double> _horizontal = [];
    private bool _isConstant;
    private bool _zerosTooComplex;

    private FunctionAnalyzer(GraphNode function, GraphEvaluationContext context)
    {
        _function = function;
        _context = context;
    }

    /// <summary>Analyzes y = f(x); only <see cref="GraphEquationKind.FunctionOfX"/> equations can be analyzed.</summary>
    public static FunctionAnalysisResult Analyze(GraphNode function, GraphEvaluationContext context)
    {
        double? period = PeriodFinder.Find(function, context);
        var analyzer = new FunctionAnalyzer(function, context);
        return period is double value ? analyzer.RunPeriodic(value) : analyzer.Run();
    }

    private double F(double x)
    {
        _context.X = x;
        return _function.Evaluate(_context);
    }

    private FunctionAnalysisResult Run()
    {
        Sample(DefaultGrid());
        FindDomain();
        if (_domain.Count == 0)
        {
            return new FunctionAnalysisResult([], null, [], null, [], [], [], [], [], [], FunctionParity.Unknown, []);
        }

        _isConstant = IsConstant();
        FindCriticalPoints();
        FindZeros();
        FindInflectionPoints();
        List<(double, double)> oblique = FindAsymptotesAtInfinity();

        bool extremaTooComplex = _minima.Count > MaxFeatureCount || _maxima.Count > MaxFeatureCount;
        bool polesTooComplex = _poles.Count > MaxFeatureCount;
        _zeros.Sort();
        _poles.Sort();

        List<(AnalysisInterval, FunctionMonotonicity)>? monotonicity = extremaTooComplex || polesTooComplex ? null : FindMonotonicity();
        if (monotonicity?.Count > MaxFeatureCount)
        {
            monotonicity = null;
        }

        return new FunctionAnalysisResult(
            polesTooComplex ? null : SplitAtPoles(_domain.Select(entry => entry.Interval)),
            FindRange(),
            _zerosTooComplex ? null : _zeros,
            YIntercept(),
            extremaTooComplex ? null : _minima,
            extremaTooComplex ? null : _maxima,
            _inflectionPoints.Count > MaxFeatureCount ? null : _inflectionPoints.Select(x => new AnalysisPoint(x, NiceNumber.Snap(F(x), 1e-9))).ToList(),
            polesTooComplex ? null : _poles,
            _horizontal,
            oblique,
            FindParity(),
            monotonicity);
    }

    /// <summary>Analysis of a periodic function over the window [−T/4, 5T/4]: every feature has a copy inside it.</summary>
    private FunctionAnalysisResult RunPeriodic(double period)
    {
        Sample(Enumerable.Range(-4000, 24001).Select(k => period * k / 16000));
        FindDomain();
        _isConstant = IsConstant();
        if (_domain.Count == 0 || _isConstant)
        {
            // Nothing repeats in a useful way; the general analysis covers these cases.
            return new FunctionAnalyzer(_function, _context).Run();
        }

        FindCriticalPoints();
        FindZeros();
        FindInflectionPoints();

        var periodic = new PeriodicFeatures(
            PeriodicDomainExclusions(period),
            Families(_zeros, period),
            PointFamilies(_minima, period),
            PointFamilies(_maxima, period),
            PointFamilies(_inflectionPoints.Select(x => new AnalysisPoint(x, NiceNumber.Snap(F(x), 1e-9))), period),
            Families(_poles, period),
            PeriodicMonotonicity(period));

        return new FunctionAnalysisResult(
            Domain: null,
            FindRange(),
            Zeros: null,
            YIntercept(),
            Minima: null,
            Maxima: null,
            InflectionPoints: null,
            VerticalAsymptotes: null,
            HorizontalAsymptotes: [],
            ObliqueAsymptotes: [],
            FindParity(),
            Monotonicity: null,
            period,
            periodic);
    }

    private double? YIntercept()
    {
        double value = F(0);
        return double.IsFinite(value) && Math.Abs(value) < PoleMagnitude ? NiceNumber.Snap(value, 1e-10) : null;
    }

    /// <summary>Integers are represented exactly on both grids: step 0.1 on [−1000, 1000] and 0.001 on [−20, 20].</summary>
    private static SortedSet<double> DefaultGrid()
    {
        var xs = new SortedSet<double>();
        for (int i = -10000; i <= 10000; i++)
        {
            xs.Add(i / 10.0);
        }

        for (int i = -20000; i <= 20000; i++)
        {
            xs.Add(i / 1000.0);
        }

        return xs;
    }

    private void Sample(IEnumerable<double> xs)
    {
        foreach (double x in xs)
        {
            _xs.Add(x);
            _values.Add(F(x));
        }
    }

    /// <summary>
    /// Poles where the function is still finite in double precision (tan x near π/2) are not in the domain either:
    /// the domain intervals are split there.
    /// </summary>
    private List<AnalysisInterval> SplitAtPoles(IEnumerable<AnalysisInterval> intervals)
    {
        var result = new List<AnalysisInterval>();
        foreach (AnalysisInterval interval in intervals)
        {
            AnalysisInterval rest = interval;
            foreach (double pole in _poles.Where(pole => pole > interval.Low && pole < interval.High))
            {
                result.Add(rest with { High = pole, HighClosed = false });
                rest = rest with { Low = pole, LowClosed = false };
            }

            result.Add(rest);
        }

        return result;
    }

    // Periodic features --------------------------------------------------------------------------------------------

    /// <summary>The position of x within one period, in [0, period).</summary>
    private static double Reduce(double x, double period)
    {
        double offset = NiceNumber.Snap(x - (period * Math.Floor(x / period)), 1e-7);
        return Math.Abs(offset - period) < 1e-7 * Math.Max(1, period) ? 0 : offset;
    }

    private static List<double> ReduceAll(IEnumerable<double> xs, double period)
    {
        var offsets = new List<double>();
        foreach (double x in xs)
        {
            AddUnique(offsets, Reduce(x, period));
        }

        offsets.Sort();
        return offsets;
    }

    /// <summary>Whether k sorted offsets are evenly spaced by period/k, so they form one family with a shorter period.</summary>
    private static bool AreEvenlySpaced(IReadOnlyList<double> offsets, double period)
    {
        int count = offsets.Count;
        if (count < 2)
        {
            return false;
        }

        double spacing = period / count;
        for (int i = 0; i < count; i++)
        {
            double next = i + 1 < count ? offsets[i + 1] : offsets[0] + period;
            if (Math.Abs(next - offsets[i] - spacing) > 1e-6 * Math.Max(1, period))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>x = c + T·n for every c found in one period; evenly spaced values become one family (sin x: x = πn).</summary>
    private static List<PeriodicFamily>? Families(IEnumerable<double> xs, double period)
    {
        List<double> offsets = ReduceAll(xs, period);
        if (offsets.Count > MaxFeatureCount)
        {
            return null;
        }

        if (AreEvenlySpaced(offsets, period))
        {
            return [new PeriodicFamily(offsets[0], NiceNumber.Snap(period / offsets.Count, 1e-9))];
        }

        return offsets.Select(offset => new PeriodicFamily(offset, period)).ToList();
    }

    private static List<PeriodicPoint>? PointFamilies(IEnumerable<AnalysisPoint> points, double period)
    {
        var byOffset = new List<AnalysisPoint>();
        foreach (AnalysisPoint point in points)
        {
            double offset = Reduce(point.X, period);
            if (!byOffset.Any(existing => Math.Abs(existing.X - offset) < 1e-6 * Math.Max(1, Math.Abs(offset))))
            {
                byOffset.Add(new AnalysisPoint(offset, point.Y));
            }
        }

        if (byOffset.Count > MaxFeatureCount)
        {
            return null;
        }

        byOffset.Sort((a, b) => a.X.CompareTo(b.X));
        List<double> offsets = byOffset.Select(point => point.X).ToList();
        bool sameValue = byOffset.All(point => Math.Abs(point.Y - byOffset[0].Y) < 1e-9 * (1 + Math.Abs(point.Y)));
        if (sameValue && AreEvenlySpaced(offsets, period))
        {
            return [new PeriodicPoint(new PeriodicFamily(offsets[0], NiceNumber.Snap(period / offsets.Count, 1e-9)), byOffset[0].Y)];
        }

        return byOffset.Select(point => new PeriodicPoint(new PeriodicFamily(point.X, period), point.Y)).ToList();
    }

    /// <summary>
    /// Points missing from the domain: isolated points where f is undefined, and poles. A gap of positive length
    /// (ln(sin x)) is too complex to describe this way.
    /// </summary>
    private List<PeriodicFamily>? PeriodicDomainExclusions(double period)
    {
        if (!double.IsNegativeInfinity(_domain[0].Interval.Low) || !double.IsPositiveInfinity(_domain[^1].Interval.High))
        {
            return null;
        }

        var excluded = new List<double>(_poles);
        for (int i = 1; i < _domain.Count; i++)
        {
            double gapStart = _domain[i - 1].Interval.High;
            double gapEnd = _domain[i].Interval.Low;
            if (Math.Abs(gapEnd - gapStart) > 1e-7 * Math.Max(1, Math.Abs(gapStart)))
            {
                return null;
            }

            excluded.Add(gapStart);
        }

        return Families(excluded, period);
    }

    /// <summary>The monotonic pieces of one period, between consecutive extrema and poles.</summary>
    private List<PeriodicInterval>? PeriodicMonotonicity(double period)
    {
        List<double> breaks = ReduceAll([.. _minima.Select(point => point.X), .. _maxima.Select(point => point.X), .. _poles], period);
        if (breaks.Count == 0 || breaks.Count > MaxFeatureCount)
        {
            return null;
        }

        var pieces = new List<PeriodicInterval>();
        for (int i = 0; i < breaks.Count; i++)
        {
            double low = breaks[i];
            double high = i + 1 < breaks.Count ? breaks[i + 1] : breaks[0] + period;
            double[] probes = [.. Enumerable.Range(1, 5).Select(k => low + ((high - low) * k / 6.0))];
            pieces.Add(new PeriodicInterval(low, NiceNumber.Snap(high, 1e-9), period, Direction(probes)));
        }

        // Equal pieces with the same direction repeat with a shorter period (tan x: one increasing piece per π).
        bool uniform = pieces.All(piece => piece.Direction == pieces[0].Direction);
        if (uniform && AreEvenlySpaced(breaks, period))
        {
            double shorter = NiceNumber.Snap(period / pieces.Count, 1e-9);
            return [pieces[0] with { High = NiceNumber.Snap(pieces[0].Low + shorter, 1e-9), Period = shorter }];
        }

        return pieces;
    }

    private bool IsFinite(int index) => double.IsFinite(_values[index]);

    private double RefineBoundary(double inside, double outside)
    {
        for (int i = 0; i < 64; i++)
        {
            double middle = (inside + outside) / 2;
            if (double.IsFinite(F(middle)))
            {
                inside = middle;
            }
            else
            {
                outside = middle;
            }
        }

        return NiceNumber.Snap(inside, 1e-9);
    }

    private void FindDomain()
    {
        int count = _xs.Count;
        int i = 0;
        while (i < count)
        {
            if (!IsFinite(i))
            {
                i++;
                continue;
            }

            int first = i;
            while (i + 1 < count && IsFinite(i + 1))
            {
                i++;
            }

            int last = i;
            i++;

            double low = double.NegativeInfinity;
            bool lowClosed = false;
            if (first > 0)
            {
                low = RefineBoundary(_xs[first], _xs[first - 1]);
                lowClosed = double.IsFinite(F(low));
            }

            double high = double.PositiveInfinity;
            bool highClosed = false;
            if (last < count - 1)
            {
                high = RefineBoundary(_xs[last], _xs[last + 1]);
                highClosed = double.IsFinite(F(high));
            }

            _domain.Add((new AnalysisInterval(low, high, lowClosed, highClosed), first, last));
        }
    }

    private bool IsConstant()
    {
        double reference = F(0.37);
        if (!double.IsFinite(reference))
        {
            return false;
        }

        foreach (double x in (double[])[-7.3, -1.1, 0, 2.9, 13.7, 311])
        {
            double value = F(x);
            if (double.IsFinite(value) && Math.Abs(value - reference) > 1e-12 * (1 + Math.Abs(reference)))
            {
                return false;
            }
        }

        return true;
    }

    private double GoldenSection(double a, double b, Func<double, double> objective)
    {
        double ratio = (Math.Sqrt(5) - 1) / 2;
        double c = b - (ratio * (b - a));
        double d = a + (ratio * (b - a));
        double fc = objective(c);
        double fd = objective(d);
        for (int i = 0; i < 100 && Math.Abs(b - a) > 1e-13 * (1 + Math.Abs(a)); i++)
        {
            if (fc > fd)
            {
                b = d;
                d = c;
                fd = fc;
                c = b - (ratio * (b - a));
                fc = objective(c);
            }
            else
            {
                a = c;
                c = d;
                fc = fd;
                d = a + (ratio * (b - a));
                fd = objective(d);
            }
        }

        return (a + b) / 2;
    }

    private static void AddUnique(List<double> values, double value)
    {
        if (!values.Any(existing => Math.Abs(existing - value) < 1e-6 * Math.Max(1, Math.Abs(value))))
        {
            values.Add(value);
        }
    }

    private void AddPole(double x) => AddUnique(_poles, NiceNumber.Snap(x, 1e-7));

    /// <summary>Local extrema from sign changes of the sampled differences; a "maximum" that grows without bound is a pole.</summary>
    private void FindCriticalPoints()
    {
        if (_isConstant)
        {
            return;
        }

        foreach ((_, int first, int last) in _domain)
        {
            int previousSign = 0;
            for (int i = first; i < last; i++)
            {
                double difference = _values[i + 1] - _values[i];
                double tolerance = 1e-12 * (1 + Math.Abs(_values[i]));
                int sign = difference > tolerance ? 1 : difference < -tolerance ? -1 : 0;
                if (sign == 0)
                {
                    continue;
                }

                if (previousSign != 0 && sign != previousSign && i > first)
                {
                    bool isMaximum = previousSign > 0;
                    double x = GoldenSection(_xs[i - 1], _xs[i + 1], t =>
                    {
                        double value = F(t);
                        return !double.IsFinite(value) ? double.NegativeInfinity : isMaximum ? value : -value;
                    });
                    double y = F(x);

                    double neighbours = Math.Max(Math.Abs(_values[i - 1]), Math.Abs(_values[i + 1]));
                    // A neighbour can itself lie on the pole (tan at the double nearest to π/2 is 1.6e16).
                    bool spike = Math.Abs(y) > PoleMagnitude && (Math.Abs(y) > 1e3 * neighbours || neighbours > PoleMagnitude);
                    if (!double.IsFinite(y) || spike)
                    {
                        AddPole(x); // a spike without a sign change: 1/(x − a)²
                    }
                    else
                    {
                        double snappedX = NiceNumber.Snap(x, 1e-7);
                        double snappedY = F(snappedX);
                        bool snappedIsWorse = isMaximum ? snappedY < y - (1e-9 * (1 + Math.Abs(y))) : snappedY > y + (1e-9 * (1 + Math.Abs(y)));
                        if (!double.IsFinite(snappedY) || snappedIsWorse)
                        {
                            snappedX = x;
                            snappedY = y;
                        }

                        (isMaximum ? _maxima : _minima).Add(new AnalysisPoint(snappedX, NiceNumber.Snap(snappedY, 1e-9)));
                        if (Math.Abs(snappedY) < 1e-10)
                        {
                            AddUnique(_zeros, snappedX); // touching zero, e.g. x²
                        }
                    }
                }

                previousSign = sign;
            }
        }
    }

    private void FindZeros()
    {
        if (_isConstant)
        {
            _zerosTooComplex = Math.Abs(F(0)) < 1e-12; // f(x) = 0 everywhere
            return;
        }

        foreach ((_, int first, int last) in _domain)
        {
            for (int i = first; i <= last; i++)
            {
                if (_values[i] == 0)
                {
                    AddUnique(_zeros, _xs[i]);
                    continue;
                }

                if (i == last || _values[i + 1] == 0 || (_values[i] > 0) == (_values[i + 1] > 0))
                {
                    continue;
                }

                double a = _xs[i];
                double b = _xs[i + 1];
                double fa = _values[i];
                for (int k = 0; k < 100; k++)
                {
                    double middle = (a + b) / 2;
                    double fm = F(middle);
                    if (!double.IsFinite(fm))
                    {
                        break;
                    }

                    if ((fm > 0) == (fa > 0))
                    {
                        a = middle;
                        fa = fm;
                    }
                    else
                    {
                        b = middle;
                    }
                }

                double root = (a + b) / 2;
                double value = F(root);
                double scale = 1 + Math.Max(Math.Abs(_values[i]), Math.Abs(_values[i + 1]));
                if (double.IsFinite(value) && Math.Abs(value) < 1e-6 * scale)
                {
                    double snapped = NiceNumber.Snap(root, 1e-9);
                    AddUnique(_zeros, Math.Abs(F(snapped)) <= Math.Abs(value) ? snapped : root);
                }
                else if (!double.IsFinite(value) || Math.Abs(value) > PoleMagnitude)
                {
                    AddPole(root); // a sign change through a pole, e.g. tan x
                }
            }
        }

        _zerosTooComplex = _zeros.Count > MaxFeatureCount;

        // Domain edges that behave like poles (1/x at 0, ln x at 0).
        foreach ((AnalysisInterval interval, _, _) in _domain)
        {
            if (double.IsFinite(interval.Low) && !interval.LowClosed && GrowsTowards(interval.Low, 1))
            {
                AddPole(interval.Low);
            }

            if (double.IsFinite(interval.High) && !interval.HighClosed && GrowsTowards(interval.High, -1))
            {
                AddPole(interval.High);
            }
        }
    }

    /// <summary>Whether |f| grows without bound when approaching the point from one side (+1 = from the right).</summary>
    private bool GrowsTowards(double point, int side)
    {
        double scale = Math.Max(1, Math.Abs(point));
        double v1 = Math.Abs(F(point + (side * 1e-3 * scale)));
        double v2 = Math.Abs(F(point + (side * 1e-7 * scale)));
        double v3 = Math.Abs(F(point + (side * 1e-11 * scale)));
        if (!double.IsFinite(v1) || !double.IsFinite(v2))
        {
            return false;
        }

        if (!double.IsFinite(v3))
        {
            return v2 > v1;
        }

        return v3 > v2 && v2 > v1 && v3 - v2 > 0.5 * (v2 - v1) && v3 > 10;
    }

    private double SecondDerivative(double x)
    {
        double h = 1e-4 * Math.Max(1, Math.Abs(x));
        return (F(x + h) - (2 * F(x)) + F(x - h)) / (h * h);
    }

    private void FindInflectionPoints()
    {
        if (_isConstant)
        {
            return;
        }

        foreach ((_, int first, int last) in _domain)
        {
            double previous = double.NaN;
            double previousX = 0;
            for (int i = first; i <= last; i += 5)
            {
                double x = _xs[i];
                double second = SecondDerivative(x);
                double tolerance = 1e-6 * (1 + Math.Abs(F(x)));
                if (!double.IsFinite(second) || Math.Abs(second) < tolerance)
                {
                    continue;
                }

                if (double.IsFinite(previous) && (second > 0) != (previous > 0))
                {
                    double a = previousX;
                    double b = x;
                    double sa = previous;
                    for (int k = 0; k < 60; k++)
                    {
                        double middle = (a + b) / 2;
                        double sm = SecondDerivative(middle);
                        if (!double.IsFinite(sm))
                        {
                            break;
                        }

                        if ((sm > 0) == (sa > 0))
                        {
                            a = middle;
                            sa = sm;
                        }
                        else
                        {
                            b = middle;
                        }
                    }

                    double point = NiceNumber.Snap((a + b) / 2, 1e-6);
                    double y = F(point);
                    bool nearPole = _poles.Any(pole => Math.Abs(pole - point) < 1e-3);
                    if (double.IsFinite(y) && Math.Abs(y) < PoleMagnitude && !nearPole)
                    {
                        AddUnique(_inflectionPoints, point);
                    }
                }

                previous = second;
                previousX = x;
            }
        }

        _inflectionPoints.Sort();
    }

    private List<(double Slope, double Intercept)> FindAsymptotesAtInfinity()
    {
        var oblique = new List<(double, double)>();
        if (_isConstant)
        {
            return oblique;
        }

        foreach (int side in (int[])[1, -1])
        {
            bool unbounded = side > 0 ? double.IsPositiveInfinity(_domain[^1].Interval.High) : double.IsNegativeInfinity(_domain[0].Interval.Low);
            if (!unbounded)
            {
                continue;
            }

            double f5 = F(side * 1e5);
            double f7 = F(side * 1e7);
            double f9 = F(side * 1e9);
            if (!double.IsFinite(f5) || !double.IsFinite(f7) || !double.IsFinite(f9))
            {
                continue;
            }

            double scale = 1 + Math.Abs(f9);
            if (Math.Abs(f9 - f7) < 1e-5 * scale && Math.Abs(f7 - f5) < 1e-2 * scale)
            {
                AddUnique(_horizontal, NiceNumber.Snap(f9, 1e-5));
                continue;
            }

            double m7 = f7 / (side * 1e7);
            double m9 = f9 / (side * 1e9);
            if (Math.Abs(m9 - m7) < 1e-5 * (1 + Math.Abs(m9)) && Math.Abs(m9) is > 1e-9 and < 1e9)
            {
                double slope = NiceNumber.Snap(m9, 1e-6);
                double b5 = f5 - (slope * side * 1e5);
                double b7 = f7 - (slope * side * 1e7);
                if (Math.Abs(b7 - b5) < 1e-2 * (1 + Math.Abs(b7)))
                {
                    double intercept = NiceNumber.Snap(b7, 1e-4);

                    // A straight line is not an asymptote of itself.
                    bool isLine = ((double[])[-2.5, 0, 1, 3.7]).All(x =>
                    {
                        double value = F(x);
                        return double.IsFinite(value) && Math.Abs(value - ((slope * x) + intercept)) < 1e-9 * (1 + Math.Abs(value));
                    });
                    bool duplicate = oblique.Any(existing => Math.Abs(existing.Item1 - slope) < 1e-9 && Math.Abs(existing.Item2 - intercept) < 1e-9);
                    if (!isLine && !duplicate)
                    {
                        oblique.Add((slope, intercept));
                    }
                }
            }
        }

        return oblique;
    }

    private FunctionParity FindParity()
    {
        bool even = true;
        bool odd = true;
        for (int i = 1; i <= 400 && (even || odd); i++)
        {
            double x = (i * 0.0537) + (i > 200 ? i * 0.9 : 0);
            double a = F(x);
            double b = F(-x);
            if (double.IsFinite(a) != double.IsFinite(b))
            {
                return FunctionParity.Neither;
            }

            if (!double.IsFinite(a))
            {
                continue;
            }

            double tolerance = 1e-9 * (1 + Math.Abs(a));
            even = even && Math.Abs(a - b) <= tolerance;
            odd = odd && Math.Abs(a + b) <= tolerance;
        }

        return even ? FunctionParity.Even : odd ? FunctionParity.Odd : FunctionParity.Neither;
    }

    /// <summary>+1/−1 when f tends to +∞/−∞ at the point from the given side, 0 otherwise.</summary>
    private int LimitSign(double point, int side)
    {
        if (!GrowsTowards(point, side))
        {
            return 0;
        }

        double scale = Math.Max(1, Math.Abs(point));
        double value = F(point + (side * 1e-11 * scale));
        if (!double.IsFinite(value))
        {
            value = F(point + (side * 1e-7 * scale));
        }

        return !double.IsFinite(value) || value == 0 ? 0 : Math.Sign(value);
    }

    private AnalysisInterval FindRange()
    {
        if (_isConstant)
        {
            double value = NiceNumber.Snap(F(0.37), 1e-10);
            return new AnalysisInterval(value, value, true, true);
        }

        bool upperUnbounded = false;
        bool lowerUnbounded = false;
        void MarkLimit(int sign)
        {
            upperUnbounded |= sign > 0;
            lowerUnbounded |= sign < 0;
        }

        foreach (double pole in _poles)
        {
            MarkLimit(LimitSign(pole, 1));
            MarkLimit(LimitSign(pole, -1));
        }

        double maximum = _values.Where(double.IsFinite).DefaultIfEmpty(double.NegativeInfinity).Max();
        double minimum = _values.Where(double.IsFinite).DefaultIfEmpty(double.PositiveInfinity).Min();
        maximum = _maxima.Select(point => point.Y).Append(maximum).Max();
        minimum = _minima.Select(point => point.Y).Append(minimum).Min();

        foreach (int side in (int[])[1, -1])
        {
            double f5 = F(side * 1e5);
            double f7 = F(side * 1e7);
            double f9 = F(side * 1e9);
            if (double.IsFinite(f9) && double.IsFinite(f7) && double.IsFinite(f5))
            {
                // Still growing without slowing down towards a limit (this also catches slow growth such as ln x).
                bool growing = Math.Abs(f9 - f7) > 0.5 * Math.Abs(f7 - f5);
                upperUnbounded |= f9 > f7 && f7 > f5 && f9 > maximum && growing;
                lowerUnbounded |= f9 < f7 && f7 < f5 && f9 < minimum && growing;
            }
            else if (double.IsFinite(f7) && Math.Abs(f7) > 1e10)
            {
                MarkLimit(Math.Sign(f7));
            }
        }

        // A bound that is only approached by a horizontal asymptote is not attained.
        bool maximumAttained = true;
        bool minimumAttained = true;
        foreach (double value in _horizontal)
        {
            if (value >= maximum - (1e-9 * (1 + Math.Abs(value))))
            {
                maximum = value;
                maximumAttained = false;
            }

            if (value <= minimum + (1e-9 * (1 + Math.Abs(value))))
            {
                minimum = value;
                minimumAttained = false;
            }
        }

        return new AnalysisInterval(
            lowerUnbounded ? double.NegativeInfinity : NiceNumber.Snap(minimum, 1e-7),
            upperUnbounded ? double.PositiveInfinity : NiceNumber.Snap(maximum, 1e-7),
            !lowerUnbounded && minimumAttained,
            !upperUnbounded && maximumAttained);
    }

    private List<(AnalysisInterval, FunctionMonotonicity)> FindMonotonicity()
    {
        var result = new List<(AnalysisInterval, FunctionMonotonicity)>();
        List<double> breaks = [.. _minima.Select(point => point.X), .. _maxima.Select(point => point.X), .. _poles];
        breaks.Sort();

        foreach ((AnalysisInterval interval, _, _) in _domain)
        {
            List<double> points = [interval.Low, .. breaks.Where(b => b > interval.Low && b < interval.High), interval.High];
            for (int i = 0; i + 1 < points.Count; i++)
            {
                double low = points[i];
                double high = points[i + 1];
                double[] probes = (double.IsFinite(low), double.IsFinite(high)) switch
                {
                    (true, true) => [.. Enumerable.Range(1, 5).Select(k => low + ((high - low) * k / 6.0))],
                    (false, true) => [.. ((double[])[16, 8, 4, 2, 1]).Select(d => high - (d * Math.Max(1, Math.Abs(high)) * 0.05))],
                    (true, false) => [.. ((double[])[1, 2, 4, 8, 16]).Select(d => low + (d * Math.Max(1, Math.Abs(low)) * 0.05))],
                    _ => [-8, -4, 0.5, 4, 8],
                };

                result.Add((new AnalysisInterval(low, high, false, false), Direction(probes)));
            }
        }

        return result;
    }

    private FunctionMonotonicity Direction(double[] probes)
    {
        bool increasing = true;
        bool decreasing = true;
        bool constant = true;
        for (int k = 1; k < probes.Length; k++)
        {
            double a = F(probes[k - 1]);
            double b = F(probes[k]);
            if (!double.IsFinite(a) || !double.IsFinite(b))
            {
                return FunctionMonotonicity.Unknown;
            }

            double tolerance = 1e-12 * (1 + Math.Abs(a));
            increasing &= b > a + tolerance;
            decreasing &= b < a - tolerance;
            constant &= Math.Abs(b - a) <= tolerance;
        }

        return increasing ? FunctionMonotonicity.Increasing
            : decreasing ? FunctionMonotonicity.Decreasing
            : constant ? FunctionMonotonicity.Constant
            : FunctionMonotonicity.Unknown;
    }
}
