using System.Globalization;
using Calculator.Core.Graphing;

namespace Calculator.Core.Tests;

public class GraphingTests
{
    private static double Evaluate(string input, double x, double y = 0)
    {
        GraphEquation equation = GraphEquation.Parse(input);
        return equation.Expression.Evaluate(new GraphEvaluationContext { X = x, Y = y });
    }

    [Theory]
    [InlineData("y = x^2", 3, 9)]
    [InlineData("x^2 + 2x - 1", 2, 7)]
    [InlineData("y = sin(x)", 0.5, 0.479425538604203)]
    [InlineData("y = sin x", 0.5, 0.479425538604203)]
    [InlineData("y = 2sin(3x)", 0.5, 1.99498997320811)]
    [InlineData("y = sin^2 x", 0.7, 0.415016428549879)]
    [InlineData("y = |x - 2|", -1, 3)]
    [InlineData("y = abs(x)", -5, 5)]
    [InlineData("y = sqrt(x + 1)", 8, 3)]
    [InlineData("y = √x", 16, 4)]
    [InlineData("y = x^(1/3)", -27, -3)]
    [InlineData("y = log(2, x)", 8, 3)]
    [InlineData("y = log_2 x", 32, 5)]
    [InlineData("y = ln(e)", 0, 1)]
    [InlineData("y = pi x", 2, 6.28318530717959)]
    [InlineData("y = 3!", 0, 6)]
    [InlineData("y = -x^2", 3, -9)]
    [InlineData("y = 1/2x", 4, 2)]
    [InlineData("y = (x+1)(x-1)", 3, 8)]
    public void EvaluatesExplicitFunctions(string input, double x, double expected) =>
        Assert.Equal(expected, Evaluate(input, x), 9);

    [Theory]
    [InlineData("y = x^2", GraphEquationKind.FunctionOfX)]
    [InlineData("x^2", GraphEquationKind.FunctionOfX)]
    [InlineData("x = y^2 - 3", GraphEquationKind.FunctionOfY)]
    [InlineData("x^2 + y^2 = 25", GraphEquationKind.Implicit)]
    [InlineData("y > x^2", GraphEquationKind.InequalityY)]
    [InlineData("x^2 + y^2 <= 9", GraphEquationKind.InequalityImplicit)]
    [InlineData("x >= 1", GraphEquationKind.InequalityX)]
    public void Classifies(string input, GraphEquationKind kind) => Assert.Equal(kind, GraphEquation.Parse(input).Kind);

    /// <summary>Formulas copied from web pages often have en or em dashes instead of the minus sign (the heart curve).</summary>
    [Theory]
    [InlineData("(x^2 + y^2 – 1)^3 – x^2*y^3 = 0")]
    [InlineData("(x^2 + y^2 — 1)^3 — x^2*y^3 = 0")]
    public void DashesAreMinusSigns(string input)
    {
        GraphEquation equation = GraphEquation.Parse(input);
        Assert.Equal(GraphEquationKind.Implicit, equation.Kind);

        var viewport = new GraphViewport(-2, 2, -1.5, 1.8, 800, 660);
        EquationGeometry geometry = CurveBuilder.Build(equation, viewport, new GraphEvaluationContext());
        Assert.NotEmpty(geometry.Curves);
    }
    [Fact]
    public void FlipsRelationWhenYIsOnTheRight()
    {
        GraphEquation equation = GraphEquation.Parse("x^2 < y");
        Assert.Equal(GraphEquationKind.InequalityY, equation.Kind);
        Assert.Equal(GraphRelation.Greater, equation.Relation);
    }

    [Fact]
    public void CollectsParameters() => Assert.Equal(["a", "b"], GraphEquation.Parse("y = a x^2 + b").Parameters);

    [Theory]
    [InlineData("", GraphInputError.EmptyExpression)]
    [InlineData("y = (x", GraphInputError.UnmatchedParenthesis)]
    [InlineData("y = x +", GraphInputError.UnexpectedEndOfExpression)]
    [InlineData("2 = 3", GraphInputError.EqualWithoutGraphVariable)]
    [InlineData("y = x = 1", GraphInputError.TooManyEquals)]
    [InlineData("x + y", GraphInputError.InvalidEquationSyntax)]
    [InlineData("y = 1.2.3", GraphInputError.TooManyDecimalPoints)]
    [InlineData("y = x $", GraphInputError.InvalidToken)]
    public void ReportsErrors(string input, GraphInputError expected) =>
        Assert.Equal(expected, Assert.Throws<GraphInputException>(() => GraphEquation.Parse(input)).Error);

    [Fact]
    public void DecimalCommaCulture()
    {
        GraphEquation equation = GraphEquation.Parse("y = 2,5 + log(2; 8)", decimalSeparator: ',');
        Assert.Equal(5.5, equation.Expression.Evaluate(new GraphEvaluationContext()), 12);
    }

    [Fact]
    public void ParabolaCurveIsContinuous()
    {
        var viewport = GraphViewport.Default(400, 400);
        EquationGeometry geometry = CurveBuilder.Build(GraphEquation.Parse("y = x^2"), viewport, new GraphEvaluationContext());
        Assert.Single(geometry.Curves);
    }

    [Fact]
    public void TangentBreaksAtAsymptotes()
    {
        var viewport = GraphViewport.Default(400, 400);
        EquationGeometry geometry = CurveBuilder.Build(GraphEquation.Parse("y = tan x"), viewport, new GraphEvaluationContext());
        Assert.Equal(7, geometry.Curves.Count); // asymptotes at ±π/2, ±3π/2, ±5π/2 in [−10, 10]
    }

    [Fact]
    public void CircleIsOneClosedContour()
    {
        var viewport = GraphViewport.Default(400, 400);
        EquationGeometry geometry = CurveBuilder.Build(GraphEquation.Parse("x^2 + y^2 = 25"), viewport, new GraphEvaluationContext());
        Assert.Single(geometry.Curves);
        GraphPoint first = geometry.Curves[0][0];
        GraphPoint last = geometry.Curves[0][^1];
        Assert.True(Math.Abs(first.X - last.X) < 1 && Math.Abs(first.Y - last.Y) < 1);
    }

    [Fact]
    public void InequalityHasRegion()
    {
        var viewport = GraphViewport.Default(400, 400);
        EquationGeometry geometry = CurveBuilder.Build(GraphEquation.Parse("y > x"), viewport, new GraphEvaluationContext());
        Assert.NotNull(geometry.Region);
        Assert.NotEmpty(geometry.Region!.Polygons);
    }

    [Fact]
    public void GridUsesNiceSteps()
    {
        Assert.Equal((2, 4), GraphGrid.NiceStep(20, 1000));
        Assert.Equal("2.5", GraphGrid.FormatLabel(2.5, 0.5, "."));
        Assert.Equal("−4", GraphGrid.FormatLabel(-4, 2, "."));
    }

    [Fact]
    public void AnalyzesParabola()
    {
        FunctionAnalysisResult result = FunctionAnalyzer.Analyze(GraphEquation.Parse("y = x^2 - 4").Expression, new GraphEvaluationContext());
        Assert.Equal([-2.0, 2.0], result.Zeros);
        Assert.Equal(-4, result.YIntercept);
        Assert.Equal([new AnalysisPoint(0, -4)], result.Minima);
        Assert.Empty(result.Maxima!);
        Assert.Equal(FunctionParity.Even, result.Parity);
        Assert.Equal(new AnalysisInterval(-4, double.PositiveInfinity, true, false), result.Range);
    }

    [Fact]
    public void AnalyzesHyperbola()
    {
        FunctionAnalysisResult result = FunctionAnalyzer.Analyze(GraphEquation.Parse("y = 1/x").Expression, new GraphEvaluationContext());
        Assert.Equal([0.0], result.VerticalAsymptotes);
        Assert.Equal([0.0], result.HorizontalAsymptotes);
        Assert.Equal(FunctionParity.Odd, result.Parity);
        Assert.Equal(2, result.Domain!.Count);
    }

    [Fact]
    public void AnalyzesSineAsPeriodic()
    {
        FunctionAnalysisResult result = FunctionAnalyzer.Analyze(GraphEquation.Parse("y = sin x").Expression, new GraphEvaluationContext());
        PeriodicFeatures periodic = result.Periodic!;
        var format = new AnalysisFormatter(CultureInfo.InvariantCulture);

        Assert.Equal(2 * Math.PI, result.Period!.Value, 12);
        Assert.Equal(new AnalysisInterval(-1, 1, true, true), result.Range);
        Assert.Empty(periodic.DomainExclusions!);
        Assert.Equal("x = πn₁; n₁ ∈ ℤ", format.Families("=", periodic.Zeros!));
        Assert.Equal("(2πn₁ + π/2, 1); n₁ ∈ ℤ", format.Point(periodic.Maxima!.Single()));
        Assert.Equal("(2πn₁ + 3π/2, −1); n₁ ∈ ℤ", format.Point(periodic.Minima!.Single()));
        Assert.Equal(
            ["(2πn₁ + π/2, 2πn₁ + 3π/2); n₁ ∈ ℤ Decreasing", "(2πn₁ + 3π/2, 2πn₁ + 5π/2); n₁ ∈ ℤ Increasing"],
            periodic.Monotonicity!.Select(piece => $"{format.Interval(piece)} {piece.Direction}"));
    }

    [Fact]
    public void AnalyzesTangentLikeTheOriginal()
    {
        FunctionAnalysisResult result = FunctionAnalyzer.Analyze(GraphEquation.Parse("y = tan(x)").Expression, new GraphEvaluationContext());
        PeriodicFeatures periodic = result.Periodic!;
        var format = new AnalysisFormatter(CultureInfo.InvariantCulture);

        Assert.Equal(Math.PI, result.Period!.Value, 12);
        Assert.Equal(new AnalysisInterval(double.NegativeInfinity, double.PositiveInfinity, false, false), result.Range);
        Assert.Equal("x ≠ πn₁ + π/2; ∀n₁ ∈ ℤ", format.Families("≠", periodic.DomainExclusions!));
        Assert.Equal("x = πn₁; n₁ ∈ ℤ", format.Families("=", periodic.Zeros!));
        Assert.Equal("x = πn₁ + π/2; n₁ ∈ ℤ", format.Families("=", periodic.VerticalAsymptotes!));
        Assert.Empty(periodic.Minima!);
        Assert.Empty(periodic.Maxima!);
        Assert.Equal(FunctionParity.Odd, result.Parity);
        PeriodicInterval piece = Assert.Single(periodic.Monotonicity!);
        Assert.Equal("(πn₁ + π/2, πn₁ + 3π/2); n₁ ∈ ℤ", format.Interval(piece));
        Assert.Equal(FunctionMonotonicity.Increasing, piece.Direction);
    }

    [Theory]
    [InlineData("y = sin(2x)", Math.PI)]
    [InlineData("y = |sin x|", Math.PI)]
    [InlineData("y = sin x + cos(2x)", 2 * Math.PI)]
    [InlineData("y = sin(x)^2", Math.PI)]
    public void FindsPeriods(string equation, double period) =>
        Assert.Equal(period, PeriodFinder.Find(GraphEquation.Parse(equation).Expression, new GraphEvaluationContext())!.Value, 12);

    [Theory]
    [InlineData("y = x sin x")]
    [InlineData("y = sin(x^2)")]
    [InlineData("y = 5")]
    public void FindsNoPeriod(string equation) =>
        Assert.Null(PeriodFinder.Find(GraphEquation.Parse(equation).Expression, new GraphEvaluationContext()));

    [Fact]
    public void ManyPolesAreTooComplexButNotListed()
    {
        FunctionAnalysisResult result = FunctionAnalyzer.Analyze(GraphEquation.Parse("y = x tan x").Expression, new GraphEvaluationContext());
        Assert.Null(result.Periodic);
        Assert.Null(result.VerticalAsymptotes);
        Assert.Null(result.Monotonicity);
    }

    [Fact]
    public void FormatsNiceNumbers()
    {
        CultureInfo culture = CultureInfo.InvariantCulture;
        Assert.Equal("π/2", NiceNumber.Format(Math.PI / 2, culture));
        Assert.Equal("−1/3", NiceNumber.Format(-1.0 / 3, culture));
        Assert.Equal("1.7321", NiceNumber.Format(Math.Sqrt(3), culture));
    }

    /// <summary>
    /// A range too small for double precision far from zero used to hang the grid (k++ no longer changed k) until the
    /// memory ran out. Such ranges are refused, and the grid returns no lines if it gets one anyway.
    /// </summary>
    [Fact]
    public void RangesBeyondDoublePrecisionDoNotHang()
    {
        var tiny = new GraphViewport(1 - 1e-300, 1 + 1e-300, -1, 1, 1000, 600);
        var farAway = new GraphViewport(1e17, 1e17 + 100, -1, 1, 1000, 600);

        Assert.False(tiny.IsUsable);
        Assert.False(farAway.IsUsable);
        Assert.Empty(GraphGrid.Compute(farAway, sameStepOnBothAxes: false, ".").VerticalLines);
        Assert.Equal(farAway, farAway.Pan(-1e9, 0));
        Assert.True(GraphViewport.Default(1000, 600).IsUsable);
    }
}
