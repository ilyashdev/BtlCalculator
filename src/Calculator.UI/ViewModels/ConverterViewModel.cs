using System.Globalization;
using Calculator.UI.Localization;
using Calculator.UI.Platform;
using Calculator.Core.Converters;
using Calculator.Core.Formatting;
using Calculator.Core.Numerics;

namespace Calculator.UI.ViewModels;

/// <summary>A unit in the unit pickers.</summary>
/// <param name="symbol">Shown after the value (currencies: "$", "€"); empty for units.</param>
public sealed class UnitItemViewModel(Unit unit, string name, string abbreviation, string symbol = "")
{
    public Unit Unit { get; } = unit;

    public string Name { get; } = name;

    public string Abbreviation { get; } = abbreviation;

    public string Symbol { get; } = symbol;

    /// <summary>A unit of the built-in categories, named by its resource strings.</summary>
    public static UnitItemViewModel FromResources(Unit unit) =>
        new(unit, AppStrings.Get(unit.NameResourceKey), AppStrings.Get(unit.AbbreviationResourceKey));

    public override string ToString() => Name;
}

/// <summary>
/// What differs between converters: how units are named, how results are rounded, whether the "about equal to" line
/// is shown and how the list is ordered. Units keep the order of the original; currencies are listed by their name in
/// the UI language, rounded to cents and have no "about equal to" line.
/// </summary>
public sealed record ConverterStyle(
    Func<Unit, UnitItemViewModel> DescribeUnit,
    Func<BigDecimal, BigDecimal> RoundResult,
    bool ShowsSupplementaryResults,
    bool SortsByName = false)
{
    public static ConverterStyle Units { get; } = new(UnitItemViewModel.FromResources, value => value, ShowsSupplementaryResults: true);
}

/// <summary>One converter: a unit category (volume, length, ...) or currencies.</summary>
public sealed class ConverterViewModel : ObservableObject
{
    private readonly UnitConverterEngine _engine;
    private readonly ConverterStyle _style;

    public ConverterViewModel(UnitCategory category)
        : this(new UnitConverterEngine(category, RegionInfo.CurrentRegion.TwoLetterISORegionName), ConverterStyle.Units)
    {
    }

    public ConverterViewModel(UnitConverterEngine engine, ConverterStyle style)
    {
        _engine = engine;
        _style = style;
        Units = DescribeUnits();
        _engine.Changed += (_, _) => Update();

        DigitCommand = new DelegateCommand<string>(digit => _engine.EnterDigit(int.Parse(digit, CultureInfo.InvariantCulture)));
        DecimalPointCommand = new DelegateCommand(_engine.EnterDecimalPoint);
        BackspaceCommand = new DelegateCommand(_engine.Backspace);
        ClearEntryCommand = new DelegateCommand(_engine.ClearEntry);
        NegateCommand = new DelegateCommand(_engine.Negate, () => _engine.SupportsNegative);
        ActivateFromCommand = new DelegateCommand(() => _engine.Activate(fromField: true));
        ActivateToCommand = new DelegateCommand(() => _engine.Activate(fromField: false));
    }

    public IReadOnlyList<UnitItemViewModel> Units { get; private set; }

    public UnitItemViewModel SelectedFromUnit
    {
        get => Units.First(item => item.Unit == _engine.FromUnit);
        set => _engine.SetFromUnit(value.Unit);
    }

    public UnitItemViewModel SelectedToUnit
    {
        get => Units.First(item => item.Unit == _engine.ToUnit);
        set => _engine.SetToUnit(value.Unit);
    }

    public string FromText => _engine.IsFromActive ? InputText : Format(_engine.FromValue);

    public string ToText => _engine.IsFromActive ? Format(_engine.ToValue) : InputText;

    public bool IsFromActive => _engine.IsFromActive;

    public bool IsToActive => !_engine.IsFromActive;

    public bool SupportsNegative => _engine.SupportsNegative;

    public string DecimalSeparator => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

    /// <summary>"About equal to 0.33 bathtubs, ..." under the fields.</summary>
    public string SupplementaryText
    {
        get
        {
            IReadOnlyList<(BigDecimal Value, Unit Unit)> results = _style.ShowsSupplementaryResults ? _engine.SupplementaryResults() : [];
            if (results.Count == 0)
            {
                return string.Empty;
            }

            var parts = results.Select(result =>
                AppStrings.Format("SupplementaryUnit_AutomationName", Format(result.Value), AppStrings.Get(result.Unit.AbbreviationResourceKey)));
            return AppStrings.Get("SupplementaryResultsHeader.Text") + "  " + string.Join("   ", parts);
        }
    }

    public DelegateCommand<string> DigitCommand { get; }

    public DelegateCommand DecimalPointCommand { get; }

    public DelegateCommand BackspaceCommand { get; }

    public DelegateCommand ClearEntryCommand { get; }

    public DelegateCommand NegateCommand { get; }

    public DelegateCommand ActivateFromCommand { get; }

    public DelegateCommand ActivateToCommand { get; }

    public void HandleKeyboardInput(KeyboardInput input)
    {
        switch (input.Key)
        {
            case KeyboardKey.Backspace:
                _engine.Backspace();
                return;
            case KeyboardKey.Escape:
            case KeyboardKey.Delete:
                _engine.ClearEntry();
                return;
            case KeyboardKey.F9:
                _engine.Negate();
                return;
        }

        if (input.Control || input.Character is not char character)
        {
            return;
        }

        if (character is >= '0' and <= '9')
        {
            _engine.EnterDigit(character - '0');
        }
        else if (character == '.' || DecimalSeparator.Contains(character, StringComparison.Ordinal))
        {
            _engine.EnterDecimalPoint();
        }
    }

    private NumberFormatOptions FormatOptions =>
        NumberFormatOptions.FromCulture(CultureInfo.CurrentCulture, UnitConverterEngine.MaxInputDigits);

    private string InputText => NumberFormatter.FormatInput(_engine.Input, FormatOptions);

    /// <summary>Replaces the units (new currency rates), keeping the selected units and the typed value.</summary>
    public void ReplaceUnits(IReadOnlyList<Unit> units)
    {
        _engine.ReplaceUnits(units);
        Units = DescribeUnits();
        OnPropertyChanged(nameof(Units));
        Update();
    }

    private List<UnitItemViewModel> DescribeUnits()
    {
        List<UnitItemViewModel> items = _engine.Units.Select(_style.DescribeUnit).ToList();
        if (_style.SortsByName)
        {
            items.Sort((a, b) => string.Compare(a.Name, b.Name, CultureInfo.CurrentUICulture, CompareOptions.IgnoreCase));
        }

        return items;
    }

    private string Format(BigDecimal value) => NumberFormatter.Format(_style.RoundResult(value), FormatOptions);

    private void Update()
    {
        OnPropertyChanged(nameof(FromText));
        OnPropertyChanged(nameof(ToText));
        OnPropertyChanged(nameof(IsFromActive));
        OnPropertyChanged(nameof(IsToActive));
        OnPropertyChanged(nameof(SelectedFromUnit));
        OnPropertyChanged(nameof(SelectedToUnit));
        OnPropertyChanged(nameof(SupplementaryText));
    }
}
