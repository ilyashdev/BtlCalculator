using System.Globalization;
using Calculator.Core.Formatting;
using Calculator.Core.Numerics;

namespace Calculator.Core.Tests;

public class BigDecimalTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("-0.0", "0")]
    [InlineData("1.500", "1.5")]
    [InlineData("1200", "1200")]
    [InlineData("-12.5e3", "-12500")]
    [InlineData("0.000123", "0.000123")]
    public void ParsesAndNormalizes(string text, string expected) => Assert.Equal(expected, BigDecimal.Parse(text).ToString());

    [Fact]
    public void AdditionIsExact() => Assert.Equal(BigDecimal.Parse("0.3"), BigDecimal.Parse("0.1") + BigDecimal.Parse("0.2"));

    [Theory]
    [InlineData("2.5", "3")]
    [InlineData("-2.5", "-3")]
    [InlineData("2.4", "2")]
    public void RoundsHalfAwayFromZero(string value, string expected) =>
        Assert.Equal(BigDecimal.Parse(expected), BigDecimal.Parse(value).RoundToInteger());

    [Theory]
    [InlineData("-2.5", "-3", "-2")]
    [InlineData("2.5", "2", "3")]
    [InlineData("3", "3", "3")]
    public void FloorAndCeiling(string value, string floor, string ceiling)
    {
        Assert.Equal(BigDecimal.Parse(floor), BigDecimal.Parse(value).Floor());
        Assert.Equal(BigDecimal.Parse(ceiling), BigDecimal.Parse(value).Ceiling());
    }

    [Fact]
    public void Compares()
    {
        Assert.True(BigDecimal.Parse("-1") < BigDecimal.Parse("0.5"));
        Assert.True(BigDecimal.Parse("1e3") > BigDecimal.Parse("999.9"));
        Assert.Equal(BigDecimal.Parse("1.0"), BigDecimal.Parse("1"));
    }

    [Theory]
    [InlineData("-7", "3", "2")]
    [InlineData("7", "-3", "-2")]
    [InlineData("5.5", "2", "1.5")]
    public void ModuloFollowsSignOfDivisor(string x, string y, string expected) =>
        Assert.Equal(BigDecimal.Parse(expected), BigMath.Modulo(BigDecimal.Parse(x), BigDecimal.Parse(y)));

    [Fact]
    public void PiHasManyCorrectDigits() =>
        Assert.Equal(
            "3.14159265358979323846264338327950288419716939937510582097494",
            BigMath.Pi(60).ToString());

    [Theory]
    [InlineData("1234567.891", "1,234,567.891")]
    [InlineData("-1000", "-1,000")]
    [InlineData("123", "123")]
    public void FormatsWithGrouping(string value, string expected)
    {
        var options = NumberFormatOptions.FromCulture(CultureInfo.InvariantCulture, 16);
        Assert.Equal(expected, NumberFormatter.Format(BigDecimal.Parse(value), options));
    }

    [Theory]
    [InlineData("0.00001", "0.00001")]
    [InlineData("1e-17", "1e-17")]
    [InlineData("12345678901234567890", "1.234567890123457e+19")]
    public void SwitchesToScientificNotation(string value, string expected)
    {
        var options = NumberFormatOptions.FromCulture(CultureInfo.InvariantCulture, 16) with { UseDigitGrouping = false };
        Assert.Equal(expected, NumberFormatter.Format(BigDecimal.Parse(value), options));
    }

    [Fact]
    public void UsesCultureSeparators()
    {
        var options = NumberFormatOptions.FromCulture(CultureInfo.GetCultureInfo("de-DE"), 16);
        Assert.Equal("1.234,5", NumberFormatter.Format(BigDecimal.Parse("1234.5"), options));
    }
}
