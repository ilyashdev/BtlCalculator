using Calculator.Core.Engine;
using Calculator.Core.Numerics;
using Calculator.Core.Programmer;

namespace Calculator.Core.Tests;

public class ProgrammerModeTests
{
    private static readonly ProgrammerExpressionFormatter ExpressionFormatter = new(new EnglishProgrammerVocabulary());

    private static ProgrammerEngine Engine(Radix radix = Radix.Decimal, WordSize size = WordSize.QWord) =>
        new() { WordSize = size, Radix = radix };

    private static void Type(ProgrammerEngine engine, string digits)
    {
        foreach (char c in digits)
        {
            engine.EnterDigit(Convert.ToInt32(c.ToString(), 16));
        }
    }

    private static string Display(ProgrammerEngine engine) =>
        engine.Error is { } error ? error.ToString() : ProgrammerFormatter.Format(engine.DisplayValue, engine.Radix, engine.WordSize);

    [Fact]
    public void IntegerArithmeticWithPrecedence()
    {
        var engine = Engine();
        Type(engine, "2");
        engine.ApplyOperator(ProgrammerOperator.Add);
        Type(engine, "3");
        engine.ApplyOperator(ProgrammerOperator.Multiply);
        Type(engine, "4");
        engine.Evaluate();
        Assert.Equal("14", Display(engine));
    }

    [Fact]
    public void DivisionTruncates()
    {
        var engine = Engine();
        Type(engine, "7");
        engine.ApplyOperator(ProgrammerOperator.Divide);
        Type(engine, "2");
        engine.Evaluate();
        Assert.Equal("3", Display(engine));
    }

    [Fact]
    public void DivideByZero()
    {
        var engine = Engine();
        Type(engine, "7");
        engine.ApplyOperator(ProgrammerOperator.Divide);
        Type(engine, "0");
        engine.Evaluate();
        Assert.Equal(CalculationError.DivideByZero, engine.Error);
    }

    [Fact]
    public void HexInputAndBitwise()
    {
        var engine = Engine(Radix.Hexadecimal);
        Type(engine, "F0");
        engine.ApplyOperator(ProgrammerOperator.Or);
        Type(engine, "F");
        engine.Evaluate();
        Assert.Equal("FF", Display(engine));
        Assert.Equal("F0 OR F =", ExpressionFormatter.Format(engine.Expression, engine.Radix, engine.WordSize));
    }

    [Fact]
    public void AndBindsTighterThanOr()
    {
        var engine = Engine(Radix.Binary);
        Type(engine, "1");
        engine.ApplyOperator(ProgrammerOperator.Or);
        Type(engine, "110");
        engine.ApplyOperator(ProgrammerOperator.And);
        Type(engine, "10");
        engine.Evaluate();
        Assert.Equal("0011", Display(engine));
    }

    [Fact]
    public void NegativeNumbersUseTwosComplement()
    {
        var engine = Engine(size: WordSize.Byte);
        Type(engine, "1");
        engine.ApplyFunction(ProgrammerFunction.Negate);
        engine.Evaluate();
        Assert.Equal("-1", Display(engine));
        engine.Radix = Radix.Hexadecimal;
        Assert.Equal("FF", Display(engine));
        engine.Radix = Radix.Binary;
        Assert.Equal("1111 1111", Display(engine));
    }

    [Fact]
    public void OverflowWrapsAround()
    {
        var engine = Engine(size: WordSize.Byte);
        Type(engine, "127");
        engine.ApplyOperator(ProgrammerOperator.Add);
        Type(engine, "1");
        engine.Evaluate();
        Assert.Equal("-128", Display(engine));
    }

    [Fact]
    public void DecimalInputIsLimitedToSignedRange()
    {
        var engine = Engine(size: WordSize.Byte);
        Type(engine, "128");
        Assert.Equal("12", Display(engine));
    }

    [Fact]
    public void HexInputCanUseAllBits()
    {
        var engine = Engine(Radix.Hexadecimal, WordSize.Byte);
        Type(engine, "FFF");
        Assert.Equal("FF", Display(engine));
        engine.Radix = Radix.Decimal;
        Assert.Equal("-1", Display(engine));
    }

    [Theory]
    [InlineData(ShiftMode.Arithmetic, "-4")]
    [InlineData(ShiftMode.Logical, "124")]
    public void RightShift(ShiftMode mode, string expected)
    {
        var engine = Engine(size: WordSize.Byte);
        engine.ShiftMode = mode;
        Type(engine, "8");
        engine.ApplyFunction(ProgrammerFunction.Negate);
        engine.ApplyOperator(ProgrammerOperator.RightShift);
        Type(engine, "1");
        engine.Evaluate();
        Assert.Equal(expected, Display(engine));
    }

    [Fact]
    public void LeftShift()
    {
        var engine = Engine();
        Type(engine, "3");
        engine.ApplyOperator(ProgrammerOperator.LeftShift);
        Type(engine, "4");
        engine.Evaluate();
        Assert.Equal("48", Display(engine));
    }

    [Fact]
    public void RotateAndNot()
    {
        var engine = Engine(Radix.Binary, WordSize.Byte);
        Type(engine, "10000001");
        engine.ApplyFunction(ProgrammerFunction.RotateLeft);
        Assert.Equal("0011", Display(engine));
        engine.ApplyFunction(ProgrammerFunction.Not);
        Assert.Equal("1111 1100", Display(engine));
    }

    [Fact]
    public void RotateThroughCarry()
    {
        var engine = Engine(Radix.Binary, WordSize.Byte);
        Type(engine, "10000000");
        engine.ApplyFunction(ProgrammerFunction.RotateLeftThroughCarry);
        Assert.Equal("0", Display(engine));
        Assert.True(engine.Carry);
        engine.ApplyFunction(ProgrammerFunction.RotateLeftThroughCarry);
        Assert.Equal("0001", Display(engine));
        Assert.False(engine.Carry);
    }

    [Fact]
    public void WordSizeChangeTruncates()
    {
        var engine = Engine();
        Type(engine, "300");
        engine.WordSize = WordSize.Byte;
        Assert.Equal("44", Display(engine));
    }

    [Fact]
    public void ToggleBit()
    {
        var engine = Engine();
        engine.ToggleBit(3);
        Assert.Equal("8", Display(engine));
    }

    [Fact]
    public void DigitsAboveRadixAreIgnored()
    {
        var engine = Engine(Radix.Octal);
        Type(engine, "78");
        Assert.Equal("7", Display(engine));
    }
}
