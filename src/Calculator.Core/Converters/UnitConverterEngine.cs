using Calculator.Core.Engine;
using Calculator.Core.Numerics;

namespace Calculator.Core.Converters;

/// <summary>
/// The unit converter of one category: the user types into the active field, the other field shows the conversion.
/// </summary>
public sealed class UnitConverterEngine
{
    public const int MaxInputDigits = 16;
    private const int MaxSupplementaryResults = 3;

    private NumberInput _input = new(MaxInputDigits);

    /// <summary>A converter of a fixed category with the default units of the region.</summary>
    public UnitConverterEngine(UnitCategory category, string regionCode)
    {
        Units = UnitCatalog.UnitsOf(category);
        (FromUnit, ToUnit) = UnitCatalog.DefaultUnits(category, regionCode);
        SupportsNegative = UnitCatalog.SupportsNegative(category);
    }

    /// <summary>A converter of any unit list, e.g. currencies whose rates are downloaded.</summary>
    public UnitConverterEngine(IReadOnlyList<Unit> units, Unit fromUnit, Unit toUnit, bool supportsNegative)
    {
        Units = units;
        FromUnit = fromUnit;
        ToUnit = toUnit;
        SupportsNegative = supportsNegative;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<Unit> Units { get; private set; }

    public Unit FromUnit { get; private set; }

    public Unit ToUnit { get; private set; }

    /// <summary>True when the user types into the first field.</summary>
    public bool IsFromActive { get; private set; } = true;

    public NumberInput Input => _input;

    public bool SupportsNegative { get; }

    public BigDecimal InputValue => _input.ToValue();

    /// <summary>The value of the field the user is not typing into.</summary>
    public BigDecimal ConvertedValue => IsFromActive
        ? UnitConversion.Convert(InputValue, FromUnit, ToUnit)
        : UnitConversion.Convert(InputValue, ToUnit, FromUnit);

    public BigDecimal FromValue => IsFromActive ? InputValue : ConvertedValue;

    public BigDecimal ToValue => IsFromActive ? ConvertedValue : InputValue;

    public void EnterDigit(int digit)
    {
        _input.TryAppendDigit(digit);
        NotifyChanged();
    }

    public void EnterDecimalPoint()
    {
        _input.TryAddDecimalPoint();
        NotifyChanged();
    }

    public void Backspace()
    {
        _input.Backspace();
        NotifyChanged();
    }

    public void ClearEntry()
    {
        _input = new NumberInput(MaxInputDigits);
        NotifyChanged();
    }

    public void Negate()
    {
        if (SupportsNegative)
        {
            _input.ToggleSign();
            NotifyChanged();
        }
    }

    /// <summary>Makes the other field active; its current (converted) value becomes the input.</summary>
    public void Activate(bool fromField)
    {
        if (IsFromActive == fromField)
        {
            return;
        }

        BigDecimal value = fromField ? FromValue : ToValue;
        IsFromActive = fromField;
        _input = InputFromValue(value);
        NotifyChanged();
    }

    public void SetFromUnit(Unit unit)
    {
        FromUnit = unit;
        NotifyChanged();
    }

    public void SetToUnit(Unit unit)
    {
        ToUnit = unit;
        NotifyChanged();
    }

    /// <summary>Replaces the units (new currency rates); the selected units are kept by key when they still exist.</summary>
    public void ReplaceUnits(IReadOnlyList<Unit> units)
    {
        Units = units;
        FromUnit = units.FirstOrDefault(unit => unit.Key == FromUnit.Key) ?? units[0];
        ToUnit = units.FirstOrDefault(unit => unit.Key == ToUnit.Key) ?? units[0];
        NotifyChanged();
    }

    /// <summary>"About equal to" results: the input expressed in other units with values between 1 and 1000, whimsical first.</summary>
    public IReadOnlyList<(BigDecimal Value, Unit Unit)> SupplementaryResults()
    {
        BigDecimal source = IsFromActive ? InputValue : ToValue;
        Unit sourceUnit = IsFromActive ? FromUnit : ToUnit;
        if (source.IsZero)
        {
            return [];
        }

        return Units
            .Where(unit => unit != FromUnit && unit != ToUnit)
            .Select(unit => (Value: UnitConversion.Convert(source, sourceUnit, unit), Unit: unit))
            .Where(result => result.Value.Abs() >= BigDecimal.One && result.Value.Abs() < 1000)
            .OrderByDescending(result => result.Unit.IsWhimsical)
            .ThenBy(result => result.Unit.Order)
            .Take(MaxSupplementaryResults)
            .ToList();
    }

    /// <summary>Rebuilds the typed text from a value (used when switching the active field).</summary>
    private static NumberInput InputFromValue(BigDecimal value)
    {
        var input = new NumberInput(MaxInputDigits);
        BigDecimal rounded = value.RoundToSignificantDigits(MaxInputDigits);
        if (rounded.IsZero)
        {
            return input;
        }

        // Only values that can be typed in fixed notation are carried over; others start from zero.
        string text = rounded.Abs().ToInvariantString();
        if (text.Contains('e', StringComparison.Ordinal))
        {
            return input;
        }

        foreach (char c in text)
        {
            if (c == '.')
            {
                input.TryAddDecimalPoint();
            }
            else
            {
                input.TryAppendDigit(c - '0');
            }
        }

        if (rounded.IsNegative)
        {
            input.ToggleSign();
        }

        return input;
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
