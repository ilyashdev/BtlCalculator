using Avalonia.Controls;
using Calculator.Core.Converters;
using Calculator.Core.Engine;
using Calculator.UI.Localization;
using Calculator.UI.Services;
using Calculator.UI.ViewModels;
using Calculator.UI.Views;

namespace Calculator.UI.Navigation;

/// <summary>
/// Creates the view of each mode on first use and keeps it, so every mode keeps its state while switching.
/// <see cref="ReleaseViews"/> releases the views that listen to shared services (the main view is replaced when the
/// language changes).
/// </summary>
public sealed class ModeViewFactory(AppServices services)
{
    private readonly Dictionary<AppMode, Control> _views = [];

    public Control GetView(AppMode mode)
    {
        if (!_views.TryGetValue(mode, out Control? view))
        {
            view = CreateView(mode);
            _views[mode] = view;
        }

        return view;
    }

    public void ReleaseViews()
    {
        foreach (IDisposable view in _views.Values.OfType<IDisposable>())
        {
            view.Dispose();
        }

        _views.Clear();
    }

    private Control CreateView(AppMode mode) => mode switch
    {
        AppMode.Standard => new CalculatorView(
            new CalculatorViewModel(AppStrings.Get("StandardModeText"), CalculatorOptions.Standard, services.Memory, services.System),
            new StandardKeypad()),
        AppMode.Scientific => new CalculatorView(
            new CalculatorViewModel(AppStrings.Get("ScientificModeText"), CalculatorOptions.Scientific, services.Memory, services.System),
            new ScientificKeypad()),
        AppMode.Programmer => new ProgrammerView(new ProgrammerViewModel(services.Memory)),
        AppMode.Graphing => new GraphingView(new GraphingViewModel(services.Settings, () => AppThemeService.Current), services.Share),
        AppMode.DateCalculation => new DateCalculationView(new DateCalculationViewModel()),
        AppMode.Volume => Converter(UnitCategory.Volume),
        AppMode.Length => Converter(UnitCategory.Length),
        AppMode.Weight => Converter(UnitCategory.Weight),
        AppMode.Temperature => Converter(UnitCategory.Temperature),
        AppMode.Energy => Converter(UnitCategory.Energy),
        AppMode.Area => Converter(UnitCategory.Area),
        AppMode.Speed => Converter(UnitCategory.Speed),
        AppMode.Time => Converter(UnitCategory.Time),
        AppMode.Power => Converter(UnitCategory.Power),
        AppMode.Data => Converter(UnitCategory.Data),
        AppMode.Pressure => Converter(UnitCategory.Pressure),
        AppMode.Angle => Converter(UnitCategory.Angle),
        AppMode.Currency => new CurrencyView(new CurrencyViewModel(services.ExchangeRates, services.System)),
        AppMode.Settings => new SettingsView(services.Themes, services.Languages, services.System),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "A mode without a view."),
    };

    private static ConverterView Converter(UnitCategory category) => new(new ConverterViewModel(category));
}
