using Calculator.Core.Numerics;

namespace Calculator.Core.Converters;

public enum UnitCategory
{
    Volume,
    Length,
    Weight,
    Temperature,
    Energy,
    Area,
    Speed,
    Time,
    Power,
    Data,
    Pressure,
    Angle,
}

/// <summary>
/// A unit and its relation to the base unit of the category:
/// <c>base = (value + Offset) × Multiplier ÷ Divisor</c>.
/// Most units only have a multiplier; fractions such as km/h = 1/3.6 m/s keep a divisor so they stay exact,
/// temperatures also have an offset (°F → K is (°F + 459.67) × 5 ÷ 9).
/// </summary>
/// <param name="Key">Stable identifier, also the suffix of the resource keys UnitName_{Key} and UnitAbbreviation_{Key}.</param>
/// <param name="Order">Position in the unit list (legacy order).</param>
/// <param name="IsWhimsical">Everyday comparisons ("bathtubs", "bananas") used for the "about equal to" results.</param>
public sealed record Unit(
    string Key,
    int Order,
    BigDecimal Multiplier,
    BigDecimal Divisor,
    BigDecimal Offset,
    bool IsWhimsical = false)
{
    public string NameResourceKey => "UnitName_" + Key;

    public string AbbreviationResourceKey => "UnitAbbreviation_" + Key;

    public static Unit Factor(string key, int order, string multiplier, bool isWhimsical = false) =>
        new(key, order, BigDecimal.Parse(multiplier), BigDecimal.One, BigDecimal.Zero, isWhimsical);

    public static Unit Fraction(string key, int order, string multiplier, string divisor) =>
        new(key, order, BigDecimal.Parse(multiplier), BigDecimal.Parse(divisor), BigDecimal.Zero);

    public static Unit Affine(string key, int order, string offset, string multiplier, string divisor) =>
        new(key, order, BigDecimal.Parse(multiplier), BigDecimal.Parse(divisor), BigDecimal.Parse(offset));
}
