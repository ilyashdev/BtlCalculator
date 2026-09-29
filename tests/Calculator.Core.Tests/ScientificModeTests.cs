using Calculator.Core.Engine;
using Calculator.Core.Numerics;

namespace Calculator.Core.Tests;

public class ScientificModeTests
{
    private static CalculatorDriver Calculator() => new(CalculatorOptions.Scientific);

    [Theory]
    [InlineData("2+3*4=", "14")]
    [InlineData("2*3+4=", "10")]
    [InlineData("2+3*4^2=", "50")]
    [InlineData("2^3^2=", "64")] // left to right, as in Windows Calculator
    [InlineData("(2+3)*4=", "20")]
    [InlineData("2*(3+4=", "14")] // "=" closes open parentheses
    [InlineData("2(3+4)=", "14")] // implicit multiplication
    [InlineData("(2+3)4=", "20")]
    [InlineData("1/3*3=", "1")]
    [InlineData("2^100=", "1267650600228229401496703205376")]
    public void Calculates(string keys, string expected) => Assert.Equal(expected, Calculator().Press(keys).Display);

    [Fact]
    public void ShowsIntermediateValues()
    {
        var calculator = Calculator().Press("2+3*");
        Assert.Equal("3", calculator.Display);
        Assert.Equal("2 + 3 ×", calculator.Expression);
        Assert.Equal("14", calculator.Press("4+").Display);
    }

    [Fact]
    public void ReplacingOperatorWithTighterOneAddsParentheses()
    {
        var calculator = Calculator().Press("2*3+^");
        Assert.Equal("(2 × 3) ^", calculator.Expression);
        Assert.Equal("36", calculator.Press("2=").Display);
    }

    [Fact]
    public void ParenthesesAreShownInExpression()
    {
        var calculator = Calculator().Press("2*(3+4)");
        Assert.Equal("7", calculator.Display);
        Assert.Equal("2 × (3 + 4)", calculator.Expression);
        Assert.Equal(0, calculator.Engine.OpenParenthesisCount);
        Assert.Equal("2 × (3 + 4) =", calculator.Press("=").Expression);
    }

    [Fact]
    public void CountsOpenParentheses() => Assert.Equal(2, Calculator().Press("((1+").Engine.OpenParenthesisCount);

    [Fact]
    public void ShowsThirtyTwoDigits() =>
        Assert.Equal("0.33333333333333333333333333333333", Calculator().Press("1/3=").Display);

    [Fact]
    public void ExponentEntry()
    {
        var calculator = Calculator().Press("1.5x3");
        Assert.Equal("1.5e+3", calculator.Display);
        Assert.Equal("1500", calculator.Press("=").Display);
        Assert.Equal("1.5e-3", Calculator().Press("1.5x3n").Display);
    }

    [Fact]
    public void TrigonometryInDegreesIsExact()
    {
        Assert.Equal("0", Calculator().Press("180").Apply(UnaryFunction.Sin).Display);
        Assert.Equal("0.5", Calculator().Press("30").Apply(UnaryFunction.Sin).Display);
        Assert.Equal("0", Calculator().Press("90").Apply(UnaryFunction.Cos).Display);
        Assert.Equal("1", Calculator().Press("45").Apply(UnaryFunction.Tan).Display);
        Assert.Equal("InvalidInput", Calculator().Press("90").Apply(UnaryFunction.Tan).Display);
        Assert.Equal("30", Calculator().Press("0.5").Apply(UnaryFunction.Asin).Display);
    }

    [Fact]
    public void TrigonometryInRadians()
    {
        var calculator = Calculator();
        calculator.Engine.AngleUnit = AngleUnit.Radians;
        calculator.Engine.InsertConstant(MathConstant.Pi);
        Assert.Equal("0", calculator.Apply(UnaryFunction.Sin).Display);
        Assert.Equal("sinᵣ(π)", calculator.Expression);
    }

    [Fact]
    public void ShowsAngleUnitInExpression() =>
        Assert.Equal("sin₀(30)", Calculator().Press("30").Apply(UnaryFunction.Sin).Expression);

    [Theory]
    [InlineData("100", UnaryFunction.Log10, "2")]
    [InlineData("1000", UnaryFunction.Log10, "3")]
    [InlineData("1", UnaryFunction.Ln, "0")]
    [InlineData("27", UnaryFunction.CubeRoot, "3")]
    [InlineData("2", UnaryFunction.SquareRoot, "1.4142135623730950488016887242097")]
    [InlineData("5", UnaryFunction.Factorial, "120")]
    [InlineData("0.5", UnaryFunction.Factorial, "0.88622692545275801364908374167057")]
    [InlineData("3", UnaryFunction.PowerOfTen, "1000")]
    [InlineData("10", UnaryFunction.PowerOfTwo, "1024")]
    [InlineData("1", UnaryFunction.PowerOfE, "2.7182818284590452353602874713527")]
    [InlineData("2.5", UnaryFunction.Floor, "2")]
    [InlineData("2.5", UnaryFunction.Ceiling, "3")]
    [InlineData("1.5", UnaryFunction.ToDegreesMinutesSeconds, "1.3")]
    [InlineData("1.3", UnaryFunction.FromDegreesMinutesSeconds, "1.5")]
    [InlineData("0", UnaryFunction.Cosh, "1")]
    public void Functions(string input, UnaryFunction function, string expected) =>
        Assert.Equal(expected, Calculator().Press(input).Apply(function).Display);

    [Fact]
    public void NegativeCubeRoot() => Assert.Equal("-2", Calculator().Press("8n").Apply(UnaryFunction.CubeRoot).Display);

    [Fact]
    public void FactorialOfNegativeIntegerIsInvalid() =>
        Assert.Equal("InvalidInput", Calculator().Press("3n").Apply(UnaryFunction.Factorial).Display);

    [Fact]
    public void FactorialOverflow() =>
        Assert.Equal("Overflow", Calculator().Press("3249").Apply(UnaryFunction.Factorial).Display);

    [Fact]
    public void Modulo()
    {
        var calculator = Calculator().Press("7n");
        calculator.Engine.ApplyBinaryOperator(BinaryOperator.Modulo);
        Assert.Equal("2", calculator.Press("3=").Display);
    }

    [Fact]
    public void Root()
    {
        var calculator = Calculator().Press("8n");
        calculator.Engine.ApplyBinaryOperator(BinaryOperator.Root);
        Assert.Equal("-2", calculator.Press("3=").Display);
    }

    [Fact]
    public void LogBase()
    {
        var calculator = Calculator().Press("8");
        calculator.Engine.ApplyBinaryOperator(BinaryOperator.LogBase);
        Assert.Equal("3", calculator.Press("2=").Display);
    }

    [Fact]
    public void PiIsShownWithFullPrecision()
    {
        var calculator = Calculator();
        calculator.Engine.InsertConstant(MathConstant.Pi);
        Assert.Equal("3.1415926535897932384626433832795", calculator.Display);
    }
}
