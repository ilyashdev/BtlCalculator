using Calculator.Core.Converters;
using Calculator.Core.Numerics;

namespace Calculator.Core.Tests;

public class UnitConverterTests
{
    private static BigDecimal Convert(UnitCategory category, string value, string from, string to) =>
        UnitConversion.Convert(BigDecimal.Parse(value), UnitCatalog.Find(category, from), UnitCatalog.Find(category, to))
            .RoundToSignificantDigits(15);

    [Theory]
    [InlineData(UnitCategory.Length, "1", "Inch", "Centimeter", "2.54")]
    [InlineData(UnitCategory.Length, "1", "Mile", "Kilometer", "1.609344")]
    [InlineData(UnitCategory.Weight, "1", "Pound", "Kilogram", "0.45359237")]
    [InlineData(UnitCategory.Temperature, "100", "DegreesCelsius", "DegreesFahrenheit", "212")]
    [InlineData(UnitCategory.Temperature, "32", "DegreesFahrenheit", "DegreesCelsius", "0")]
    [InlineData(UnitCategory.Temperature, "0", "Kelvin", "DegreesCelsius", "-273.15")]
    [InlineData(UnitCategory.Temperature, "-40", "DegreesCelsius", "DegreesFahrenheit", "-40")]
    [InlineData(UnitCategory.Speed, "36", "KilometersPerHour", "MetersPerSecond", "10")]
    [InlineData(UnitCategory.Data, "1", "Gibibytes", "Mebibytes", "1024")]
    [InlineData(UnitCategory.Data, "1", "Byte", "Bit", "8")]
    [InlineData(UnitCategory.Time, "1", "Day", "Hour", "24")]
    [InlineData(UnitCategory.Angle, "180", "Degree", "Radian", "3.14159265358979")]
    [InlineData(UnitCategory.Pressure, "1", "Atmosphere", "Pascal", "101325")]
    [InlineData(UnitCategory.Volume, "1", "GallonUS", "Liter", "3.785411784")]
    [InlineData(UnitCategory.Area, "1", "Hectare", "SquareMeter", "10000")]
    public void Converts(UnitCategory category, string value, string from, string to, string expected) =>
        Assert.Equal(BigDecimal.Parse(expected), Convert(category, value, from, to));

    [Fact]
    public void EveryCategoryHasUnitsAndDefaults()
    {
        foreach (UnitCategory category in UnitCatalog.Categories)
        {
            Assert.NotEmpty(UnitCatalog.UnitsOf(category));
            (Unit from, Unit to) = UnitCatalog.DefaultUnits(category, "RU");
            Assert.NotEqual(from, to);
        }
    }

    [Fact]
    public void RegionalDefaults()
    {
        Assert.Equal("Inch", UnitCatalog.DefaultUnits(UnitCategory.Length, "RU").From.Key);
        Assert.Equal("Centimeter", UnitCatalog.DefaultUnits(UnitCategory.Length, "US").From.Key);
    }

    [Fact]
    public void EngineConvertsTypedValue()
    {
        var engine = new UnitConverterEngine(UnitCategory.Length, "RU"); // inch → centimeter
        engine.EnterDigit(1);
        engine.EnterDigit(0);
        Assert.Equal(BigDecimal.Parse("25.4"), engine.ToValue);
    }

    [Fact]
    public void SwitchingFieldKeepsValues()
    {
        var engine = new UnitConverterEngine(UnitCategory.Length, "RU");
        engine.EnterDigit(2);
        engine.Activate(fromField: false);
        Assert.Equal(BigDecimal.Parse("5.08"), engine.InputValue);
        engine.Backspace();
        Assert.Equal(BigDecimal.Parse("5.0"), engine.ToValue);
    }

    [Fact]
    public void NegativeOnlyWhereSupported()
    {
        var length = new UnitConverterEngine(UnitCategory.Length, "RU");
        length.EnterDigit(5);
        length.Negate();
        Assert.False(length.InputValue.IsNegative);

        var temperature = new UnitConverterEngine(UnitCategory.Temperature, "RU");
        temperature.EnterDigit(5);
        temperature.Negate();
        Assert.True(temperature.InputValue.IsNegative);
    }
}
