using Calculator.Core.Graphing;

namespace Calculator.Core.Tests;

/// <summary>Interval bounds must contain every value of the expression on the range (they may be wider).</summary>
public class IntervalTests
{
    private static GraphInterval Bound(string equation, double low, double high) =>
        IntervalEvaluator.Evaluate(GraphEquation.Parse(equation).Expression, new GraphInterval(low, high), GraphInterval.Entire, new GraphEvaluationContext());

    [Fact]
    public void TangentIsUnboundedOnlyAcrossAPole()
    {
        Assert.Equal(GraphInterval.Entire, Bound("y = tan(x)", 1, 2)); // π/2 inside
        GraphInterval noPole = Bound("y = tan(x)", 0, 1);
        Assert.Equal(0, noPole.Low, 12);
        Assert.Equal(Math.Tan(1), noPole.High, 12);
    }

    [Fact]
    public void SineReachesItsExtremaInside()
    {
        Assert.Equal(new GraphInterval(-1, 1), Bound("y = sin(1000x)", 0, 1));
        GraphInterval top = Bound("y = sin(x)", 1, 2);
        Assert.Equal(1, top.High);
        Assert.Equal(Math.Sin(1), top.Low, 12);
    }

    [Fact]
    public void DivisionByARangeWithZeroIsUnbounded()
    {
        Assert.Equal(GraphInterval.Entire, Bound("y = 1/x", -1, 1));
        GraphInterval positive = Bound("y = 1/x", 1, 2);
        Assert.Equal(new GraphInterval(0.5, 1), positive);
    }

    [Fact]
    public void DomainsAreRespected()
    {
        Assert.True(Bound("y = sqrt(x)", -3, -1).IsEmpty);
        Assert.Equal(new GraphInterval(0, 2), Bound("y = sqrt(x)", -3, 4));
        Assert.Equal(double.NegativeInfinity, Bound("y = ln(x)", 0, 1).Low);
    }

    /// <summary>Random samples of the expression always lie inside its bound.</summary>
    [Theory]
    [InlineData("y = x^3 - 2x^2 + sin(5x)")]
    [InlineData("y = abs(x) * cos(x) / (2 + sin(x))")]
    [InlineData("y = e^(-x^2) + floor(x) - sqrt(abs(x))")]
    [InlineData("y = tanh(x) * sec(x)")]
    [InlineData("y = mod(x, 3) + log(x^2 + 1)")]
    public void BoundsContainTheValues(string equation)
    {
        GraphEquation parsed = GraphEquation.Parse(equation);
        var random = new Random(7);
        var context = new GraphEvaluationContext();
        for (int i = 0; i < 500; i++)
        {
            double low = (random.NextDouble() * 20) - 10;
            double high = low + (random.NextDouble() * 3);
            GraphInterval bound = IntervalEvaluator.Evaluate(parsed.Expression, new GraphInterval(low, high), GraphInterval.Entire, context);
            for (int k = 0; k <= 10; k++)
            {
                context.X = low + ((high - low) * k / 10);
                double value = parsed.Expression.Evaluate(context);
                if (double.IsFinite(value))
                {
                    Assert.True(value >= bound.Low - 1e-9 && value <= bound.High + 1e-9, $"{equation}: f({context.X}) = {value} outside [{bound.Low}, {bound.High}]");
                }
            }
        }
    }
}
