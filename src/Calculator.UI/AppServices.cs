using Calculator.Core.Storage;
using Calculator.UI.Platform;
using Calculator.UI.Services;

namespace Calculator.UI;

/// <summary>
/// The services of the app, created once at startup and passed on explicitly (composition root). The views of a
/// language are created from these; see App.ShowMainView.
/// </summary>
public sealed class AppServices
{
    public AppServices()
        : this(new JsonSettingsStore())
    {
    }

    public AppServices(ISettingsStore settings)
        : this(settings, new ExchangeRateService())
    {
    }

    public AppServices(ISettingsStore settings, ExchangeRateService exchangeRates)
    {
        Settings = settings;
        ExchangeRates = exchangeRates;
        Themes = new AppThemeService(Settings);
        Languages = new LanguageService(Settings);
    }

    public ISettingsStore Settings { get; }

    public AppThemeService Themes { get; }

    public LanguageService Languages { get; }

    /// <summary>Clipboard and links; connected to the window (or the phone view) once it exists.</summary>
    public TopLevelSystemServices System { get; } = new();

    public WindowModeService WindowMode { get; } = new();

    /// <summary>The share sheet of the platform, or none (the graph picture is saved instead).</summary>
    public IShareService Share { get; init; } = new NoShareService();

    /// <summary>The memory is shared by all calculator modes.</summary>
    public CalculatorMemory Memory { get; } = new();

    public ExchangeRateService ExchangeRates { get; }
}
