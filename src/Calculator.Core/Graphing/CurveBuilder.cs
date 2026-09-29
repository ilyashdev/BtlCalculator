namespace Calculator.Core.Graphing;

/// <summary>A filled region of an inequality: polygons (for y ≷ f(x) and x ≷ g(y)) or axis-aligned cells (general case).</summary>
public sealed record GraphRegion(IReadOnlyList<IReadOnlyList<GraphPoint>> Polygons, IReadOnlyList<(GraphPoint TopLeft, GraphPoint BottomRight)> Cells);

/// <summary>What to draw for one equation, in screen coordinates.</summary>
public sealed record EquationGeometry(IReadOnlyList<IReadOnlyList<GraphPoint>> Curves, GraphRegion? Region);

/// <summary>
/// Turns equations into polylines and regions for a viewport:
/// explicit curves by adaptive sampling (with discontinuity and domain edge detection),
/// implicit curves by marching squares, inequality regions column by column or cell by cell.
/// </summary>
public static class CurveBuilder
{
    private const double ContourCellSize = 3;
    private const double RegionCellSize = 3;

    public static EquationGeometry Build(GraphEquation equation, GraphViewport viewport, GraphEvaluationContext context)
    {
        if (!viewport.IsValid)
        {
            return new EquationGeometry([], null);
        }

        IReadOnlyList<IReadOnlyList<GraphPoint>> curves = equation.Kind switch
        {
            GraphEquationKind.FunctionOfX or GraphEquationKind.InequalityY => new CurveSampler(equation.Expression, context, viewport, swapped: false).Run(),
            GraphEquationKind.FunctionOfY or GraphEquationKind.InequalityX => new CurveSampler(equation.Expression, context, viewport, swapped: true).Run(),
            _ => TraceContour(equation.Expression, context, viewport),
        };

        GraphRegion? region = equation.Kind switch
        {
            GraphEquationKind.InequalityY => ExplicitRegion(equation, context, viewport, swapped: false),
            GraphEquationKind.InequalityX => ExplicitRegion(equation, context, viewport, swapped: true),
            GraphEquationKind.InequalityImplicit => ImplicitRegion(equation, context, viewport),
            _ => null,
        };

        return new EquationGeometry(curves, region);
    }

    private static bool Holds(double value, GraphRelation relation) => relation switch
    {
        GraphRelation.Less => value < 0,
        GraphRelation.LessOrEqual => value <= 0,
        GraphRelation.Greater => value > 0,
        GraphRelation.GreaterOrEqual => value >= 0,
        _ => false,
    };

    private static float Clamp(double value, double low, double high) => (float)Math.Clamp(value, low, high);

    /// <summary>y ≷ f(x): the area between the curve and the top or bottom edge, one polygon per defined run of columns.</summary>
    private static GraphRegion ExplicitRegion(GraphEquation equation, GraphEvaluationContext context, GraphViewport viewport, bool swapped)
    {
        bool towardsMaximum = equation.Relation is GraphRelation.Greater or GraphRelation.GreaterOrEqual;
        float edge = swapped
            ? (towardsMaximum ? (float)viewport.Width + 2 : -2)
            : (towardsMaximum ? -2 : (float)viewport.Height + 2);

        var polygons = new List<IReadOnlyList<GraphPoint>>();
        var run = new List<GraphPoint>();

        void FlushRun()
        {
            if (run.Count >= 2)
            {
                var polygon = new List<GraphPoint>(run.Count + 2)
                {
                    swapped ? new GraphPoint(edge, run[0].Y) : new GraphPoint(run[0].X, edge),
                };
                polygon.AddRange(run);
                polygon.Add(swapped ? new GraphPoint(edge, run[^1].Y) : new GraphPoint(run[^1].X, edge));
                polygons.Add(polygon);
            }

            run.Clear();
        }

        int count = (int)Math.Ceiling(swapped ? viewport.Height : viewport.Width) + 1;
        for (int i = -1; i <= count; i++)
        {
            double value;
            if (swapped)
            {
                context.Y = viewport.GraphY(i);
                value = equation.Expression.Evaluate(context);
            }
            else
            {
                context.X = viewport.GraphX(i);
                value = equation.Expression.Evaluate(context);
            }

            if (!double.IsFinite(value))
            {
                FlushRun();
                continue;
            }

            run.Add(swapped
                ? new GraphPoint(Clamp(viewport.ScreenX(value), -2 * viewport.Width, 3 * viewport.Width), i)
                : new GraphPoint(i, Clamp(viewport.ScreenY(value), -2 * viewport.Height, 3 * viewport.Height)));
        }

        FlushRun();
        return new GraphRegion(polygons, []);
    }

    /// <summary>G(x, y) ≷ 0: cells whose center satisfies the relation, merged into horizontal runs.</summary>
    private static GraphRegion ImplicitRegion(GraphEquation equation, GraphEvaluationContext context, GraphViewport viewport)
    {
        var cells = new List<(GraphPoint, GraphPoint)>();
        int columns = (int)Math.Ceiling(viewport.Width / RegionCellSize);
        int rows = (int)Math.Ceiling(viewport.Height / RegionCellSize);

        for (int row = 0; row < rows; row++)
        {
            float top = (float)(row * RegionCellSize);
            float bottom = (float)Math.Min((row + 1) * RegionCellSize, viewport.Height);
            context.Y = viewport.GraphY((row + 0.5) * RegionCellSize);
            int runStart = -1;

            for (int column = 0; column <= columns; column++)
            {
                bool inside = false;
                if (column < columns)
                {
                    context.X = viewport.GraphX((column + 0.5) * RegionCellSize);
                    double value = equation.Expression.Evaluate(context);
                    inside = double.IsFinite(value) && Holds(value, equation.Relation);
                }

                if (inside && runStart < 0)
                {
                    runStart = column;
                }
                else if (!inside && runStart >= 0)
                {
                    float left = (float)(runStart * RegionCellSize);
                    float right = (float)Math.Min(column * RegionCellSize, viewport.Width);
                    cells.Add((new GraphPoint(left, top), new GraphPoint(right, bottom)));
                    runStart = -1;
                }
            }
        }

        return new GraphRegion([], cells);
    }

    /// <summary>Marching squares for F(x, y) = 0, with pole rejection, one false-position refinement and chaining into polylines.</summary>
    private static List<IReadOnlyList<GraphPoint>> TraceContour(GraphNode expression, GraphEvaluationContext context, GraphViewport viewport)
    {
        int columns = Math.Max(1, (int)Math.Ceiling(viewport.Width / ContourCellSize));
        int rows = Math.Max(1, (int)Math.Ceiling(viewport.Height / ContourCellSize));
        double cellWidth = viewport.Width / columns;
        double cellHeight = viewport.Height / rows;
        int stride = columns + 1;

        double EvaluateAt(double screenX, double screenY)
        {
            context.X = viewport.GraphX(screenX);
            context.Y = viewport.GraphY(screenY);
            return expression.Evaluate(context);
        }

        var values = new double[stride * (rows + 1)];
        for (int j = 0; j <= rows; j++)
        {
            for (int i = 0; i <= columns; i++)
            {
                values[(j * stride) + i] = EvaluateAt(i * cellWidth, j * cellHeight);
            }
        }

        // Edge id: 2 × vertex + 0 for the horizontal edge to the right, + 1 for the vertical edge downwards.
        int edgeCount = values.Length * 2;
        var edgeState = new sbyte[edgeCount]; // 0 unknown, 1 crossing, -1 none
        var edgePoints = new GraphPoint[edgeCount];
        var links = new (int First, int Second)[edgeCount];
        Array.Fill(links, (-1, -1));

        bool Crossing(int edge)
        {
            if (edgeState[edge] != 0)
            {
                return edgeState[edge] > 0;
            }

            edgeState[edge] = -1;
            int vertex = edge / 2;
            int i0 = vertex % stride;
            int j0 = vertex / stride;
            int i1 = edge % 2 == 0 ? i0 + 1 : i0;
            int j1 = edge % 2 == 0 ? j0 : j0 + 1;
            if (i1 > columns || j1 > rows)
            {
                return false;
            }

            double va = values[(j0 * stride) + i0];
            double vb = values[(j1 * stride) + i1];
            if (!double.IsFinite(va) || !double.IsFinite(vb) || (va > 0) == (vb > 0))
            {
                return false;
            }

            double ax = i0 * cellWidth;
            double ay = j0 * cellHeight;
            double bx = i1 * cellWidth;
            double by = j1 * cellHeight;
            double t = va / (va - vb);
            double px = ax + ((bx - ax) * t);
            double py = ay + ((by - ay) * t);
            double pv = EvaluateAt(px, py);

            // A sign change through a pole (y = 1/x) is not a zero.
            if (!double.IsFinite(pv) || Math.Abs(pv) > Math.Max(Math.Abs(va), Math.Abs(vb)))
            {
                return false;
            }

            if (pv != 0)
            {
                if ((pv > 0) == (va > 0))
                {
                    double t2 = pv / (pv - vb);
                    px += (bx - px) * t2;
                    py += (by - py) * t2;
                }
                else
                {
                    double t2 = va / (va - pv);
                    px = ax + ((px - ax) * t2);
                    py = ay + ((py - ay) * t2);
                }
            }

            edgePoints[edge] = new GraphPoint((float)px, (float)py);
            edgeState[edge] = 1;
            return true;
        }

        void Link(int a, int b)
        {
            void Add(int from, int to)
            {
                if (links[from].First == -1)
                {
                    links[from].First = to;
                }
                else if (links[from].Second == -1)
                {
                    links[from].Second = to;
                }
            }

            Add(a, b);
            Add(b, a);
        }

        Span<int> found = stackalloc int[4];
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < columns; i++)
            {
                int top = ((j * stride) + i) * 2;
                int bottom = (((j + 1) * stride) + i) * 2;
                int left = (((j * stride) + i) * 2) + 1;
                int right = (((j * stride) + i + 1) * 2) + 1;

                int count = 0;
                foreach (int edge in (ReadOnlySpan<int>)[top, right, bottom, left])
                {
                    if (Crossing(edge))
                    {
                        found[count++] = edge;
                    }
                }

                if (count == 2)
                {
                    Link(found[0], found[1]);
                }
                else if (count == 4)
                {
                    // Saddle: the center value decides which corners are connected.
                    double center = EvaluateAt((i + 0.5) * cellWidth, (j + 0.5) * cellHeight);
                    if (!double.IsFinite(center))
                    {
                        continue;
                    }

                    if ((center > 0) == (values[(j * stride) + i] > 0))
                    {
                        Link(top, right);
                        Link(bottom, left);
                    }
                    else
                    {
                        Link(top, left);
                        Link(bottom, right);
                    }
                }
            }
        }

        var lines = new List<IReadOnlyList<GraphPoint>>();
        var visited = new bool[edgeCount];

        void Follow(int start)
        {
            var line = new List<GraphPoint>();
            int previous = -1;
            int current = start;
            while (current != -1 && !visited[current])
            {
                visited[current] = true;
                line.Add(edgePoints[current]);
                int next = links[current].First != previous ? links[current].First : links[current].Second;
                if (next != -1 && visited[next] && next != previous)
                {
                    line.Add(edgePoints[next]); // closes a loop
                }

                previous = current;
                current = next;
            }

            if (line.Count >= 2)
            {
                lines.Add(line);
            }
        }

        // Open chains first (they start at an end), then closed loops.
        for (int edge = 0; edge < edgeCount; edge++)
        {
            if (!visited[edge] && links[edge].First != -1 && links[edge].Second == -1)
            {
                Follow(edge);
            }
        }

        for (int edge = 0; edge < edgeCount; edge++)
        {
            if (!visited[edge] && links[edge].First != -1)
            {
                Follow(edge);
            }
        }

        return lines;
    }

    /// <summary>
    /// Adaptive sampler for y = f(x) (or x = g(y) when swapped). Each pixel column is subdivided until the curve is
    /// straight on screen (the middle point lies within <see cref="StraightnessTolerance"/> pixels of the chord, measured
    /// perpendicular to it, so steep parts do not need extra samples). Before a straight piece that still spans several
    /// pixels is drawn, <see cref="FindJump"/> checks whether it hides a jump (tan x at π/2, floor x at integers):
    /// a steep but continuous piece gets shorter when bisected, a jump does not.
    /// </summary>
    private sealed class CurveSampler(GraphNode expression, GraphEvaluationContext context, GraphViewport viewport, bool swapped)
    {
        private const int MaxDepth = 14;

        // Every pixel column is split at least into 4 pieces, so narrow spikes (tan(x)/x near its poles) are not missed.
        private const int MinDepth = 2;
        private const double StraightnessTolerance = 0.35;

        // Points removed from a finished curve lie at most this far (in pixels) from the path that is kept.
        private const double SimplifyTolerance = 0.25;
        private const double JumpPixels = 2;

        // Evaluations allowed per pixel column. A budget per column (not per curve) keeps a hard region, such as many
        // poles, from using up the samples of the columns after it, which were then joined by straight lines.
        private const int ColumnBudget = 4000;

        // Sub-pixel oscillation (sin(1000x), tan(100x) zoomed out): samples per column and the turns that reveal it.
        private const int EnvelopeSamples = 32;
        private const int EnvelopeTurns = 4;

        private readonly double _dependentExtent = swapped ? viewport.Width : viewport.Height;
        private readonly List<IReadOnlyList<GraphPoint>> _lines = [];
        private List<GraphPoint> _current = [];
        private int _budget;

        public List<IReadOnlyList<GraphPoint>> Run()
        {
            double pixels = swapped ? viewport.Height : viewport.Width;
            int count = (int)Math.Ceiling(pixels) + 2;

            double u0 = Independent(-1);
            double v0 = Evaluate(u0);
            if (double.IsFinite(v0))
            {
                Add(u0, v0);
            }

            for (int i = 0; i <= count; i++)
            {
                _budget = ColumnBudget;
                double u1 = Independent(i);
                double v1 = Evaluate(u1);
                if (!TryDrawEnvelope(u0, v0, u1, v1))
                {
                    Segment(u0, v0, u1, v1, 0);
                }

                u0 = u1;
                v0 = v1;
            }

            Flush();
            return _lines;
        }

        /// <summary>
        /// When the function turns up and down several times within one pixel column, no polyline through samples can
        /// show it: sampled points land at random heights and draw false spikes (aliasing). The column is then drawn
        /// as what the eye sees, a vertical stroke over the range of the values, like a plotter would.
        /// </summary>
        private bool TryDrawEnvelope(double u0, double v0, double u1, double v1)
        {
            if (!double.IsFinite(v0) || !double.IsFinite(v1))
            {
                return false;
            }

            var us = new double[EnvelopeSamples + 2];
            var vs = new double[EnvelopeSamples + 2];
            (us[0], vs[0]) = (u0, v0);
            (us[^1], vs[^1]) = (u1, v1);

            double previous = v0;
            int previousDirection = 0;
            int turns = 0;
            for (int k = 1; k <= EnvelopeSamples; k++)
            {
                us[k] = u0 + ((u1 - u0) * k / (EnvelopeSamples + 1));
                vs[k] = Evaluate(us[k]);
                if (!double.IsFinite(vs[k]))
                {
                    return false; // domain edges are handled by the adaptive sampler
                }

                // Turns are counted on the values, however small on screen: zoomed far out, the samples of
                // tan(100x) are tiny next to the visible range, yet they jump around. Rounding noise is not a turn.
                double change = vs[k] - previous;
                if (Math.Abs(change) > 1e-12 * (1 + Math.Abs(previous)))
                {
                    int direction = Math.Sign(change);
                    if (previousDirection != 0 && direction != previousDirection)
                    {
                        turns++;
                    }

                    previousDirection = direction;
                    previous = vs[k];
                }
            }

            if (turns < EnvelopeTurns)
            {
                return false;
            }

            // The samples show the oscillation but not its extent: between them lie poles (tan(100x) runs off to
            // ±∞ there) that no sample may come near. The interval bound of the column knows: it is unbounded
            // exactly when the function is. Finite bounds can be wider than the truth, so the samples are kept then.
            double minimum = vs.Min();
            double maximum = vs.Max();
            GraphInterval column = GraphInterval.Hull(u0, u1);
            GraphInterval bound = swapped
                ? IntervalEvaluator.Evaluate(expression, GraphInterval.Entire, column, context)
                : IntervalEvaluator.Evaluate(expression, column, GraphInterval.Entire, context);
            if (double.IsNegativeInfinity(bound.Low))
            {
                minimum = double.NegativeInfinity;
            }

            if (double.IsPositiveInfinity(bound.High))
            {
                maximum = double.PositiveInfinity;
            }

            if (_current.Count == 0)
            {
                Add(u0, v0);
            }

            // Go to the nearer end of the stroke first, so the line continues without crossing itself.
            double middle = (u0 + u1) / 2;
            bool minimumFirst = Math.Abs(v0 - minimum) < Math.Abs(v0 - maximum);
            Add(middle, minimumFirst ? minimum : maximum);
            Add(middle, minimumFirst ? maximum : minimum);
            Add(u1, v1);
            return true;
        }

        private double Independent(double screen) => swapped ? viewport.GraphY(screen) : viewport.GraphX(screen);

        private double Evaluate(double u)
        {
            _budget--;
            if (swapped)
            {
                context.Y = u;
            }
            else
            {
                context.X = u;
            }

            return expression.Evaluate(context);
        }

        private double DependentScreen(double v) => swapped ? viewport.ScreenX(v) : viewport.ScreenY(v);

        private void Add(double u, double v)
        {
            float dependent = Clamp(DependentScreen(v), -4 * _dependentExtent, 5 * _dependentExtent);
            _current.Add(swapped ? new GraphPoint(dependent, viewport.ScreenY(u)) : new GraphPoint(viewport.ScreenX(u), dependent));
        }

        private void Flush()
        {
            if (_current.Count >= 2)
            {
                _lines.Add(Simplify(_current));
            }

            _current = [];
        }

        /// <summary>
        /// Drops points that lie on the line through their neighbours (within a quarter of a pixel): straight parts are
        /// sampled at least four times per pixel, but drawing needs only their ends.
        /// </summary>
        /// <summary>
        /// Removes points that add nothing visible (DouglasвЂ“Peucker): every removed point lies within
        /// <see cref="SimplifyTolerance"/> pixels of the path that is kept. Comparing each point only with its
        /// neighbours would let the error add up along a gently bending curve until whole arcs are cut off.
        /// </summary>
        private static List<GraphPoint> Simplify(List<GraphPoint> points)
        {
            if (points.Count <= 2)
            {
                return points;
            }

            var keep = new bool[points.Count];
            keep[0] = true;
            keep[^1] = true;
            var ranges = new Stack<(int First, int Last)>();
            ranges.Push((0, points.Count - 1));
            while (ranges.Count > 0)
            {
                (int first, int last) = ranges.Pop();
                int farthest = -1;
                double farthestDistance = SimplifyTolerance;
                for (int i = first + 1; i < last; i++)
                {
                    double distance = DistanceToSegment(points[i], points[first], points[last]);
                    if (distance > farthestDistance)
                    {
                        farthest = i;
                        farthestDistance = distance;
                    }
                }

                if (farthest >= 0)
                {
                    keep[farthest] = true;
                    ranges.Push((first, farthest));
                    ranges.Push((farthest, last));
                }
            }

            var result = new List<GraphPoint>();
            for (int i = 0; i < points.Count; i++)
            {
                if (keep[i])
                {
                    result.Add(points[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// The distance from a point to a segment (not to its line), so a spike that goes beyond the ends of the chord
        /// is never taken for a straight piece.
        /// </summary>
        private static double DistanceToSegment(GraphPoint point, GraphPoint from, GraphPoint to)
        {
            double chordX = to.X - from.X;
            double chordY = to.Y - from.Y;
            double lengthSquared = (chordX * chordX) + (chordY * chordY);
            double t = lengthSquared < 1e-12
                ? 0
                : Math.Clamp((((point.X - from.X) * chordX) + ((point.Y - from.Y) * chordY)) / lengthSquared, 0, 1);
            double dx = point.X - (from.X + (t * chordX));
            double dy = point.Y - (from.Y + (t * chordY));
            return Math.Sqrt((dx * dx) + (dy * dy));
        }
        private void Segment(double u0, double v0, double u1, double v1, int depth)
        {
            bool finite0 = double.IsFinite(v0);
            bool finite1 = double.IsFinite(v1);

            if (!finite0 && !finite1)
            {
                // A small piece of the domain may lie between two undefined samples.
                if (depth < 2 && _budget > 0)
                {
                    double um = (u0 + u1) / 2;
                    double vm = Evaluate(um);
                    if (double.IsFinite(vm))
                    {
                        Segment(u0, v0, um, vm, depth + 1);
                        Segment(um, vm, u1, v1, depth + 1);
                        return;
                    }
                }

                Flush();
                return;
            }

            if (finite0 != finite1)
            {
                // Domain edge: bisect for the last defined point.
                double inside = finite0 ? u0 : u1;
                double outside = finite0 ? u1 : u0;
                double insideValue = finite0 ? v0 : v1;
                for (int i = 0; i < 48; i++)
                {
                    double middle = (inside + outside) / 2;
                    double value = Evaluate(middle);
                    if (double.IsFinite(value))
                    {
                        inside = middle;
                        insideValue = value;
                    }
                    else
                    {
                        outside = middle;
                    }
                }

                if (finite0)
                {
                    SegmentFinite(u0, v0, inside, insideValue, depth);
                    Flush();
                }
                else
                {
                    Flush();
                    Add(inside, insideValue);
                    SegmentFinite(inside, insideValue, u1, v1, depth);
                }

                return;
            }

            SegmentFinite(u0, v0, u1, v1, depth);
        }

        private void SegmentFinite(double u0, double v0, double u1, double v1, int depth)
        {
            if (_current.Count == 0)
            {
                Add(u0, v0);
            }

            double s0 = DependentScreenExact(v0);
            double s1 = DependentScreenExact(v1);
            double margin = _dependentExtent * 0.25;
            bool outsideSameSide = (s0 < -margin && s1 < -margin) || (s0 > _dependentExtent + margin && s1 > _dependentExtent + margin);

            if (outsideSameSide || _budget <= 0)
            {
                Add(u1, v1);
                return;
            }

            if (depth < MaxDepth)
            {
                double um = (u0 + u1) / 2;
                double vm = Evaluate(um);
                if (!double.IsFinite(vm))
                {
                    Segment(u0, v0, um, vm, depth + 1);
                    Segment(um, vm, u1, v1, depth + 1);
                    return;
                }

                if (depth < MinDepth || !IsStraight(u0, s0, um, DependentScreenExact(vm), u1, s1))
                {
                    SegmentFinite(u0, v0, um, vm, depth + 1);
                    Segment(um, vm, u1, v1, depth + 1);
                    return;
                }
            }

            // A straight piece (or the finest one): draw it, unless it hides a jump.
            if (Math.Abs(s1 - s0) > JumpPixels && FindJump(u0, v0, u1, v1) is var (before, beforeValue, after, afterValue))
            {
                Add(before, beforeValue);
                Flush();
                Add(after, afterValue);
            }

            Add(u1, v1);
        }

        /// <summary>
        /// Bisects towards the larger change of the piece. A continuous piece becomes shorter than
        /// <see cref="JumpPixels"/> on screen (null is returned); a jump keeps its height until the two samples are
        /// adjacent doubles, and the samples on both sides of it are returned.
        /// </summary>
        private (double Before, double BeforeValue, double After, double AfterValue)? FindJump(double a, double va, double b, double vb)
        {
            while (_budget > 0)
            {
                double sa = DependentScreenExact(va);
                double sb = DependentScreenExact(vb);
                if (Math.Abs(sb - sa) <= JumpPixels)
                {
                    return null;
                }

                double middle = (a + b) / 2;
                if (middle <= a || middle >= b)
                {
                    return (a, va, b, vb);
                }

                double value = Evaluate(middle);
                if (!double.IsFinite(value))
                {
                    return (a, va, b, vb); // a hole or a pole between the samples
                }

                double sm = DependentScreenExact(value);
                if (Math.Abs(sm - sa) >= Math.Abs(sb - sm))
                {
                    b = middle;
                    vb = value;
                }
                else
                {
                    a = middle;
                    va = value;
                }
            }

            return null;
        }

        /// <summary>Whether the middle sample lies on the chord, measured perpendicular to it in screen pixels.</summary>
        private bool IsStraight(double u0, double s0, double um, double sm, double u1, double s1)
        {
            if (!double.IsFinite(s0) || !double.IsFinite(sm) || !double.IsFinite(s1))
            {
                return false;
            }

            double i0 = IndependentScreenExact(u0);
            double im = IndependentScreenExact(um);
            double i1 = IndependentScreenExact(u1);
            double chordI = i1 - i0;
            double chordS = s1 - s0;
            double length = Math.Sqrt((chordI * chordI) + (chordS * chordS));
            double offsetI = im - i0;
            double offsetS = sm - s0;
            double distance = length < 1e-9
                ? Math.Sqrt((offsetI * offsetI) + (offsetS * offsetS))
                : Math.Abs((chordI * offsetS) - (chordS * offsetI)) / length;
            return distance <= StraightnessTolerance;
        }

        // Screen coordinates in double precision without clamping (values far outside the screen are compared too).
        private double DependentScreenExact(double v) => swapped
            ? (v - viewport.XMin) / (viewport.XMax - viewport.XMin) * viewport.Width
            : (viewport.YMax - v) / (viewport.YMax - viewport.YMin) * viewport.Height;

        private double IndependentScreenExact(double u) => swapped
            ? (viewport.YMax - u) / (viewport.YMax - viewport.YMin) * viewport.Height
            : (u - viewport.XMin) / (viewport.XMax - viewport.XMin) * viewport.Width;
    }
}
