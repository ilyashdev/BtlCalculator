using Calculator.Core.Graphing;

namespace Calculator.Core.Tests;

/// <summary>Continuous functions must stay one unbroken curve at any zoom level.</summary>
public class CurveContinuityTests
{
    [Theory]
    [InlineData("y = x^2")]
    [InlineData("y = sin x")]
    [InlineData("y = x^3 - 3x")]
    [InlineData("y = e^x")]
    [InlineData("y = sqrt(x^2 + 1)")]
    public void ContinuousFunctionIsOneCurveWhenZoomed(string input)
    {
        GraphEquation equation = GraphEquation.Parse(input);
        GraphViewport viewport = GraphViewport.Default(800, 600).MakeProportional();

        // Zoom in step by step around a point that is on screen, like the mouse wheel does.
        for (int step = 0; step < 40; step++)
        {
            viewport = viewport.Zoom(500, 250, 1 / 1.5);
            EquationGeometry geometry = CurveBuilder.Build(equation, viewport, new GraphEvaluationContext());
            int visibleCurves = geometry.Curves.Count(curve => curve.Any(point => point.Y >= 0 && point.Y <= viewport.Height));
            Assert.True(visibleCurves <= 1, $"{input}: {visibleCurves} curves after {step + 1} zoom steps (x {viewport.XMin}…{viewport.XMax})");
        }
    }

    [Fact]
    public void SteepLineIsNotBroken()
    {
        GraphEquation equation = GraphEquation.Parse("y = 1000x");
        GraphViewport viewport = GraphViewport.Default(800, 600).MakeProportional();
        EquationGeometry geometry = CurveBuilder.Build(equation, viewport, new GraphEvaluationContext());
        Assert.Single(geometry.Curves);
    }

    /// <summary>
    /// Steep but continuous parts near poles used to be cut at the finest sampling depth, leaving visible gaps
    /// (tan(x)/x with ±200 on screen had 91). Pieces may only end off screen, next to the poles.
    /// </summary>
    [Theory]
    [InlineData("y = tan(x)/x", 200)]
    [InlineData("y = tan(x)", 100)]
    [InlineData("y = tan(x)", 1000)]
    [InlineData("y = 1/(x - 0.3)", 100)]
    public void CurvesBreakOnlyAtPoles(string input, double halfWidth)
    {
        var viewport = new GraphViewport(-halfWidth, halfWidth, -halfWidth * 0.6, halfWidth * 0.6, 1000, 600);
        EquationGeometry geometry = CurveBuilder.Build(GraphEquation.Parse(input), viewport, new GraphEvaluationContext());

        foreach (IReadOnlyList<GraphPoint> curve in geometry.Curves)
        {
            Assert.False(IsInside(curve[0]) && IsInside(curve[^1]) && curve.Count > 2, $"a piece of {input} starts and ends on screen");
        }

        static bool IsInside(GraphPoint point) => point.Y > 1 && point.Y < 599 && point.X > 1 && point.X < 999;
    }

    [Fact]
    public void JumpsAreNotConnected()
    {
        var viewport = new GraphViewport(-10, 10, -6, 6, 1000, 600);
        EquationGeometry geometry = CurveBuilder.Build(GraphEquation.Parse("y = floor(x)"), viewport, new GraphEvaluationContext());

        // One horizontal step per integer interval in view, without vertical connectors on screen
        // (pieces off screen are not checked for jumps, they are invisible anyway).
        Assert.Equal(21, geometry.Curves.Count);
        Assert.All(geometry.Curves, curve =>
        {
            List<GraphPoint> visible = curve.Where(point => point.Y >= 0 && point.Y <= 600).ToList();
            Assert.True(visible.Count == 0 || visible.Max(point => point.Y) - visible.Min(point => point.Y) < 1);
        });
    }

    /// <summary>
    /// Oscillation finer than a pixel is drawn as the band it fills: sin(1000x) with ±10 on screen covers −1 … 1 in
    /// every column. Point samples used to land at random heights and draw false spikes.
    /// </summary>
    [Fact]
    public void SubPixelOscillationFillsItsBand()
    {
        var viewport = new GraphViewport(-10, 10, -6, 6, 1000, 600);
        EquationGeometry geometry = CurveBuilder.Build(GraphEquation.Parse("y = sin(1000x)"), viewport, new GraphEvaluationContext());

        double[] spans = ColumnSpans(geometry);
        float bandHeight = viewport.ScreenY(-1) - viewport.ScreenY(1);
        Assert.True(spans.Count(span => span > bandHeight * 0.9) > 900, "the band -1..1 is not filled");
    }

    /// <summary>tan(100x) zoomed out has poles in every column, so every column spans the whole height.</summary>
    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(40000)] // the view of the user's screenshot: samples there are tiny next to the range, the poles are not
    public void PolesFinerThanPixelsFillTheScreen(double halfWidth)
    {
        var viewport = new GraphViewport(-halfWidth, halfWidth, -halfWidth * 0.6, halfWidth * 0.6, 1000, 600);
        EquationGeometry geometry = CurveBuilder.Build(GraphEquation.Parse("y = tan(100x)"), viewport, new GraphEvaluationContext());

        Assert.True(ColumnSpans(geometry).Count(span => span >= 599) > 990);
    }

    /// <summary>The largest vertical extent of the drawn lines, per pixel column, clipped to the 600 pixel high screen.</summary>
    private static double[] ColumnSpans(EquationGeometry geometry)
    {
        var spans = new double[1000];
        foreach (IReadOnlyList<GraphPoint> curve in geometry.Curves)
        {
            for (int k = 1; k < curve.Count; k++)
            {
                int column = (int)Math.Clamp(curve[k].X, 0, 999);
                double top = Math.Max(0, Math.Min(curve[k].Y, curve[k - 1].Y));
                double bottom = Math.Min(600, Math.Max(curve[k].Y, curve[k - 1].Y));
                spans[column] = Math.Max(spans[column], bottom - top);
            }
        }

        return spans;
    }
}
