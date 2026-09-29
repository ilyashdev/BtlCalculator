namespace Calculator.Core.Graphing;

/// <param name="EquationIndex">Index of the traced equation in the list passed to <see cref="GraphTracer.FindNearest"/>.</param>
public sealed record TracePoint(int EquationIndex, double X, double Y, GraphPoint Screen);

/// <summary>Finds the point of the drawn curves nearest to the pointer ("tracing").</summary>
public static class GraphTracer
{
    public const float MaxDistance = 20;

    /// <summary>
    /// Returns the nearest curve point within <see cref="MaxDistance"/> pixels, or null. Values are rounded like the
    /// legacy engine, to 10^(floor(log10(x range)) − 3).
    /// </summary>
    public static TracePoint? FindNearest(
        IReadOnlyList<(GraphEquation Equation, EquationGeometry Geometry)> equations,
        GraphViewport viewport,
        GraphEvaluationContext context,
        GraphPoint pointer)
    {
        float bestDistance = MaxDistance * MaxDistance;
        int bestIndex = -1;
        GraphPoint bestPoint = default;

        for (int index = 0; index < equations.Count; index++)
        {
            foreach (IReadOnlyList<GraphPoint> curve in equations[index].Geometry.Curves)
            {
                for (int k = 1; k < curve.Count; k++)
                {
                    GraphPoint closest = ClosestOnSegment(curve[k - 1], curve[k], pointer);
                    float dx = closest.X - pointer.X;
                    float dy = closest.Y - pointer.Y;
                    float distance = (dx * dx) + (dy * dy);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = index;
                        bestPoint = closest;
                    }
                }
            }
        }

        if (bestIndex < 0)
        {
            return null;
        }

        double precision = Math.Pow(10, Math.Floor(Math.Log10(viewport.XMax - viewport.XMin)) - 3);
        GraphEquation equation = equations[bestIndex].Equation;
        double x;
        double y;

        // Explicit curves are evaluated exactly at the rounded coordinate so the point lies on the curve.
        switch (equation.Kind)
        {
            case GraphEquationKind.FunctionOfX or GraphEquationKind.InequalityY:
                x = RoundTo(viewport.GraphX(bestPoint.X), precision);
                context.X = x;
                y = equation.Expression.Evaluate(context);
                break;
            case GraphEquationKind.FunctionOfY or GraphEquationKind.InequalityX:
                y = RoundTo(viewport.GraphY(bestPoint.Y), precision);
                context.Y = y;
                x = equation.Expression.Evaluate(context);
                break;
            default:
                x = viewport.GraphX(bestPoint.X);
                y = viewport.GraphY(bestPoint.Y);
                break;
        }

        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            return null;
        }

        return new TracePoint(bestIndex, RoundTo(x, precision), RoundTo(y, precision), new GraphPoint(viewport.ScreenX(x), viewport.ScreenY(y)));
    }

    private static GraphPoint ClosestOnSegment(GraphPoint a, GraphPoint b, GraphPoint p)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float lengthSquared = (dx * dx) + (dy * dy);
        float t = lengthSquared > 0 ? (((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared : 0;
        t = Math.Clamp(t, 0, 1);
        return new GraphPoint(a.X + (t * dx), a.Y + (t * dy));
    }

    private static double RoundTo(double value, double precision)
    {
        double rounded = Math.Round(value / precision) * precision;
        return rounded == 0 ? 0 : rounded;
    }
}
