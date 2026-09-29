using Calculator.Core.Engine;

namespace Calculator.Core.Tests;

public class StandardModeTests
{
    private static CalculatorDriver Calculator() => new(CalculatorOptions.Standard);

    [Theory]
    [InlineData("2+3=", "5")]
    [InlineData("2+3*4=", "20")] // immediate execution
    [InlineData("10-4-3=", "3")]
    [InlineData("1/3*3=", "1")]
    [InlineData("0.1+0.2=", "0.3")]
    [InlineData("7/2=", "3.5")]
    [InlineData("5+=", "10")] // "5 + =" uses 5 as the second operand
    [InlineData("2+3==", "8")] // "=" repeats the last operation
    [InlineData("9n+1=", "-8")]
    public void Calculates(string keys, string expected) => Assert.Equal(expected, Calculator().Press(keys).Display);

    [Fact]
    public void ShowsRunningResultInExpression()
    {
        var calculator = Calculator().Press("2+3*");
        Assert.Equal("5", calculator.Display);
        Assert.Equal("5 ×", calculator.Expression);
    }

    [Fact]
    public void ReplacesOperator()
    {
        var calculator = Calculator().Press("8+-*");
        Assert.Equal("8 ×", calculator.Expression);
        Assert.Equal("16", calculator.Press("2=").Display);
    }

    [Fact]
    public void ShowsCompletedExpression()
    {
        var calculator = Calculator().Press("12+7=");
        Assert.Equal("12 + 7 =", calculator.Expression);
        Assert.Equal("19", calculator.Display);
    }

    [Fact]
    public void RepeatShowsNewExpression() =>
        Assert.Equal("5 + 3 =", Calculator().Press("2+3==").Expression);

    [Fact]
    public void AddsCalculationsToHistory()
    {
        var calculator = Calculator().Press("2+3=4*5=");
        Assert.Equal(2, calculator.History.Count);
        Assert.Equal("20", calculator.Display);
    }

    [Fact]
    public void PlainNumberIsNotAddedToHistory() => Assert.Empty(Calculator().Press("5=").History);

    [Theory]
    [InlineData("200+10%", "20")]
    [InlineData("200+10%=", "220")]
    [InlineData("200*10%=", "20")]
    [InlineData("50%", "0")]
    public void Percent(string keys, string expected) => Assert.Equal(expected, Calculator().Press(keys).Display);

    [Fact]
    public void UnaryFunctionUsesLeftOperandAfterOperator()
    {
        var calculator = Calculator().Press("9+").Apply(UnaryFunction.SquareRoot);
        Assert.Equal("3", calculator.Display);
        Assert.Equal("9 + √(9)", calculator.Expression);
        Assert.Equal("12", calculator.Press("=").Display);
    }

    [Fact]
    public void NestedFunctionsAreShownInExpression()
    {
        var calculator = Calculator().Press("3").Apply(UnaryFunction.Square).Apply(UnaryFunction.Reciprocal);
        Assert.Equal("1/(sqr(3))", calculator.Expression);
        Assert.Equal("0.1111111111111111", calculator.Display);
    }

    [Fact]
    public void DigitAfterResultStartsNewCalculation()
    {
        var calculator = Calculator().Press("2+3=7");
        Assert.Equal("7", calculator.Display);
        Assert.Equal(string.Empty, calculator.Expression);
    }

    [Fact]
    public void OperatorAfterResultContinuesWithResult() =>
        Assert.Equal("10", Calculator().Press("2+3=*2=").Display);

    [Fact]
    public void DivideByZeroShowsErrorUntilNextInput()
    {
        var calculator = Calculator().Press("5/0=");
        Assert.Equal("DivideByZero", calculator.Display);
        Assert.Equal("DivideByZero", calculator.Press("+").Display); // operators are ignored in the error state
        Assert.Equal("4", calculator.Press("4").Display);
    }

    [Fact]
    public void ZeroDividedByZeroIsUndefined() => Assert.Equal("Undefined", Calculator().Press("0/0=").Display);

    [Fact]
    public void SquareRootOfNegativeIsInvalid() =>
        Assert.Equal("InvalidInput", Calculator().Press("4n").Apply(UnaryFunction.SquareRoot).Display);

    [Fact]
    public void ClearEntryKeepsPendingOperation()
    {
        var calculator = Calculator().Press("7+5e");
        Assert.Equal("0", calculator.Display);
        Assert.Equal("7 +", calculator.Expression);
        Assert.Equal("10", calculator.Press("3=").Display);
    }

    [Fact]
    public void ClearResetsEverything()
    {
        var calculator = Calculator().Press("7+5c");
        Assert.Equal("0", calculator.Display);
        Assert.Equal(string.Empty, calculator.Expression);
    }

    [Theory]
    [InlineData("123b", "12")]
    [InlineData("1.50b", "1.5")]
    [InlineData("1.b", "1")]
    [InlineData("5bb", "0")]
    public void Backspace(string keys, string expected) => Assert.Equal(expected, Calculator().Press(keys).Display);

    [Fact]
    public void TypingKeepsTrailingZerosAndPoint()
    {
        Assert.Equal("1.50", Calculator().Press("1.50").Display);
        Assert.Equal("0.", Calculator().Press(".").Display);
        Assert.Equal("0", Calculator().Press("000").Display);
    }

    [Fact]
    public void InputIsLimitedToSixteenDigits() =>
        Assert.Equal("1234567890123456", Calculator().Press("12345678901234567").Display);

    [Fact]
    public void NegateWhileTyping() => Assert.Equal("-12", Calculator().Press("12n").Display);

    [Fact]
    public void LargeResultsUseScientificNotation() =>
        Assert.Equal("1e+16", Calculator().Press("9999999999999999+1=").Display);

    [Fact]
    public void RoundsToSixteenDigits() => Assert.Equal("0.6666666666666667", Calculator().Press("2/3=").Display);
}
