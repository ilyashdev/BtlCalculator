using Calculator.Core.Graphing;

namespace Calculator.Core.Tests;

/// <summary>
/// The drawn curve must lie on the function: the middle of every segment is compared with the true point there. A
/// simplification that compared each point with its neighbours only once let the error add up, so tan(10x) crossed
/// the x axis 10 pixels away from the origin at some zoom levels.
/// </summary>
public class CurveAccuracyTests
{
    private const double TolerancePixels = 1;

    public static TheoryData<string, double, double, double, double, double, double> Views() => new()
    {
        // equation, xMin, xMax, yMin, yMax, width, height
        { "y = tan(10x)", -10, 10, -6, 6, 1322, 1294 },
        { "y = tan(10x)", -0.125, 0.135, -0.0975, 0.0925, 1322, 1294 },
        { "y = tan(10x)", -0.4, 0.4, -0.6, 0.8, 800, 1000 },
        { "y = sin x", -20, 20, -2, 2, 1600, 600 },
        { "y = x^3", -3, 3, -10, 10, 1000, 800 },
        { "y = sqrt x", -1, 50, -1, 8, 1200, 600 },
    };

    [Theory]
    [MemberData(nameof(Views))]
    public void EverySegmentLiesOnTheFunction(string input, double xMin, double xMax, double yMin, double yMax, double width, double height)
    {
        Func<double, double> function = input switch
        {
            "y = tan(10x)" => x => Math.Tan(10 * x),
            "y = sin x" => Math.Sin,
            "y = x^3" => x => x * x * x,
            "y = sqrt x" => Math.Sqrt,
            _ => throw new ArgumentOutOfRangeException(nameof(input)),
        };
        var viewport = new GraphViewport(xMin, xMax, yMin, yMax, width, height);
        EquationGeometry geometry = CurveBuilder.Build(GraphEquation.Parse(input), viewport, new GraphEvaluationContext());

        foreach (IReadOnlyList<GraphPoint> curve in geometry.Curves)
        {
            for (int i = 1; i < curve.Count; i++)
            {
                GraphPoint from = curve[i - 1];
                GraphPoint to = curve[i];

                // Points far outside the view are clamped; only what is on screen must be exact.
                if (!OnScreen(from, viewport) && !OnScreen(to, viewport) && !Crosses(from, to, viewport))
                {
                    continue;
                }

                if (Math.Abs(from.Y) >= 2 * viewport.Height || Math.Abs(to.Y) >= 2 * viewport.Height)
                {
                    continue;
                }

                double middleX = (from.X + to.X) / 2;
                double trueY = viewport.ScreenY(function(viewport.GraphX(middleX)));
                double distance = DistanceToSegment(middleX, trueY, from, to);
                Assert.True(distance <= TolerancePixels,
                    $"{input} in x {xMin}…{xMax}: segment ({from.X:F1}; {from.Y:F1})–({to.X:F1}; {to.Y:F1}) is {distance:F1} px off the function");
            }
        }
    }

    private static bool OnScreen(GraphPoint point, GraphViewport viewport) =>
        point.Y >= 0 && point.Y <= viewport.Height && point.X >= 0 && point.X <= viewport.Width;

    private static bool Crosses(GraphPoint from, GraphPoint to, GraphViewport viewport) =>
        (from.Y < 0 && to.Y > viewport.Height) || (to.Y < 0 && from.Y > viewport.Height);

    private static double DistanceToSegment(double x, double y, GraphPoint from, GraphPoint to)
    {
        double chordX = to.X - from.X;
        double chordY = to.Y - from.Y;
        double lengthSquared = (chordX * chordX) + (chordY * chordY);
        double t = lengthSquared < 1e-12 ? 0 : Math.Clamp((((x - from.X) * chordX) + ((y - from.Y) * chordY)) / lengthSquared, 0, 1);
        double dx = x - (from.X + (t * chordX));
        double dy = y - (from.Y + (t * chordY));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
